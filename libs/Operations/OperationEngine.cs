using EasyFileExplorer.Domain;

namespace EasyFileExplorer.Operations;

/// <summary>
/// Motor de operaciones: ejecuta peticiones secuencialmente con progreso por
/// elemento, políticas de conflicto, pausa entre elementos y diario persistente.
/// Un lote no es una transacción atómica: el resultado por elemento es visible.
/// </summary>
public sealed class OperationEngine
{
    private readonly OperationJournal _journal;
    private volatile bool _paused;

    public OperationEngine(OperationJournal journal)
    {
        _journal = journal;
    }

    public bool Paused => _paused;
    public void Pause() => _paused = true;
    public void Resume() => _paused = false;

    public event EventHandler<OperationProgress>? Progress;

    /// <summary>
    /// Ejecuta la petición en el hilo llamador (debe invocarse fuera del hilo UI).
    /// Devuelve resultado por elemento; el éxito se declara solo tras verificar
    /// el estado real de cada elemento.
    /// </summary>
    public async Task<OperationResult> RunAsync(OperationRequest request, CancellationToken cancellationToken)
    {
        var results = new List<ItemOperationResult>();
        var cancelled = false;

        _journal.Append(new OperationJournal.Entry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            OperationId = request.OperationId,
            Stage = "intent",
            Kind = request.Kind,
        });

        long bytesTotal = 0;
        foreach (var src in request.Sources)
        {
            try
            {
                if (File.Exists(src.Path))
                    bytesTotal += new FileInfo(src.Path).Length;
            }
            catch { /* estimación best-effort */ }
        }

        var policy = request.ConflictPolicy;
        var decidedForRest = false;
        var decidedPolicy = ConflictPolicy.AskEach;

        long bytesCompleted = 0;
        var itemsDone = 0;

        foreach (var source in request.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Pausa entre elementos (capacidad inicial; pausa a mitad de archivo
            // requeriría un motor propio con reanudación real).
            while (_paused && !cancellationToken.IsCancellationRequested)
                await Task.Delay(100, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                results.Add(new ItemOperationResult { Item = source, Status = ItemOperationStatus.Cancelled });
                continue;
            }

            var targetPath = ResolveTargetPath(request, source);
            _journal.Append(new OperationJournal.Entry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                OperationId = request.OperationId,
                Stage = "item-started",
                Kind = request.Kind,
                ItemPath = source.Path,
                TargetPath = targetPath,
            });

            try
            {
                var conflict = DetectConflict(request, source, targetPath);
                if (conflict is not null)
                {
                    var effective = decidedForRest ? decidedPolicy : policy;
                    if (effective == ConflictPolicy.AskEach && request.ConflictCallback is { } callback)
                    {
                        var resolution = callback(conflict);
                        effective = resolution.Policy;
                        if (resolution.ApplyToRemaining)
                        {
                            decidedForRest = true;
                            decidedPolicy = effective;
                        }
                    }

                    if (effective == ConflictPolicy.AskEach || effective == ConflictPolicy.Skip)
                    {
                        results.Add(new ItemOperationResult { Item = source, Status = ItemOperationStatus.Skipped });
                        _journal.Append(Log(request, "item-skipped", source.Path, targetPath, null));
                        itemsDone++;
                        continue;
                    }

                    if (effective == ConflictPolicy.Replace && targetPath is not null)
                    {
                        if (Directory.Exists(targetPath))
                            Directory.Delete(targetPath, recursive: true);
                        else
                            File.Delete(targetPath);
                    }
                    else if (effective == ConflictPolicy.KeepBoth && targetPath is not null)
                    {
                        targetPath = PathSafety.KeepBothName(targetPath, p => File.Exists(p) || Directory.Exists(p));
                    }
                }

                string? effectivePath = await ExecuteItemAsync(request, source, targetPath, cancellationToken,
                    (n) => bytesCompleted += n, () => Progress?.Invoke(this, ProgressOf(request, itemsDone, request.Sources.Count, bytesCompleted, bytesTotal, source.Path)));

                results.Add(new ItemOperationResult
                {
                    Item = source,
                    Status = ItemOperationStatus.Completed,
                    EffectivePath = effectivePath,
                });
                _journal.Append(Log(request, "item-done", source.Path, effectivePath ?? targetPath, null));
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                results.Add(new ItemOperationResult { Item = source, Status = ItemOperationStatus.Cancelled });
                _journal.Append(Log(request, "item-failed", source.Path, targetPath, "cancelled"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
            {
                results.Add(new ItemOperationResult
                {
                    Item = source,
                    Status = ItemOperationStatus.Failed,
                    Error = ex.Message,
                    NativeErrorCode = ex.HResult,
                });
                _journal.Append(Log(request, "item-failed", source.Path, targetPath, ex.Message));
            }

            itemsDone++;
            Progress?.Invoke(this, ProgressOf(request, itemsDone, request.Sources.Count, bytesCompleted, bytesTotal, null));
        }

        _journal.Append(new OperationJournal.Entry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            OperationId = request.OperationId,
            Stage = cancelled ? "cancelled" : "completed",
            Kind = request.Kind,
        });

        return new OperationResult { OperationId = request.OperationId, Items = results, WasCancelled = cancelled };
    }

    private static OperationJournal.Entry Log(OperationRequest request, string stage, string item, string? target, string? error) =>
        new()
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            OperationId = request.OperationId,
            Stage = stage,
            Kind = request.Kind,
            ItemPath = item,
            TargetPath = target,
            Error = error,
        };

    private static OperationProgress ProgressOf(OperationRequest request, int done, int total, long bytesDone, long bytesTotal, string? current) =>
        new()
        {
            OperationId = request.OperationId,
            ItemsCompleted = done,
            ItemsTotal = total,
            BytesCompleted = bytesDone,
            BytesTotal = bytesTotal,
            CurrentItemPath = current,
        };

    private static string? ResolveTargetPath(OperationRequest request, ItemId source)
    {
        return request.Kind switch
        {
            OperationKind.Rename =>
                Path.Combine(Path.GetDirectoryName(source.Path)!, request.NewName ?? Path.GetFileName(source.Path)),
            OperationKind.CreateFolder =>
                source.Path,
            OperationKind.Copy or OperationKind.Move when request.TargetDirectory is { } dir =>
                Path.Combine(dir.Path, Path.GetFileName(source.Path)),
            _ => null,
        };
    }

    private static OperationConflict? DetectConflict(OperationRequest request, ItemId source, string? targetPath)
    {
        if (targetPath is null || request.Kind is OperationKind.Delete or OperationKind.CreateFolder)
            return null;

        if (request.Kind == OperationKind.Move && PathSafety.IsInsideSubtree(source.Path, targetPath))
            return new OperationConflict { Source = source, TargetPath = targetPath, Kind = ConflictKind.TargetIsInsideSource };

        var sourceName = Path.GetFileName(source.Path);
        var targetName = Path.GetFileName(targetPath);
        var sameDir = string.Equals(Path.GetDirectoryName(source.Path), Path.GetDirectoryName(targetPath), StringComparison.OrdinalIgnoreCase);

        if (sameDir && PathSafety.DiffersOnlyInCase(sourceName, targetName))
            return null; // renombrado case-only: permitido, no es conflicto

        var exists = File.Exists(targetPath) || Directory.Exists(targetPath);
        if (!exists)
            return null;

        if (sameDir && string.Equals(sourceName, targetName, StringComparison.OrdinalIgnoreCase))
            return new OperationConflict { Source = source, TargetPath = targetPath, Kind = ConflictKind.TargetExists };

        long? sourceSize = null, targetSize = null;
        DateTimeOffset? sourceModified = null, targetModified = null;
        try
        {
            if (File.Exists(source.Path))
            {
                var fi = new FileInfo(source.Path);
                sourceSize = fi.Length;
                sourceModified = fi.LastWriteTimeUtc;
            }
            if (File.Exists(targetPath))
            {
                var fi = new FileInfo(targetPath);
                targetSize = fi.Length;
                targetModified = fi.LastWriteTimeUtc;
            }
        }
        catch { /* metadatos best-effort */ }

        return new OperationConflict
        {
            Source = source,
            TargetPath = targetPath,
            Kind = ConflictKind.TargetExists,
            SourceSize = sourceSize,
            TargetSize = targetSize,
            SourceModified = sourceModified,
            TargetModified = targetModified,
        };
    }

    private static async Task<string?> ExecuteItemAsync(
        OperationRequest request,
        ItemId source,
        string? targetPath,
        CancellationToken cancellationToken,
        Action<long> onBytes,
        Action reportProgress)
    {
        switch (request.Kind)
        {
            case OperationKind.CreateFolder:
                Directory.CreateDirectory(source.Path);
                reportProgress();
                return source.Path;

            case OperationKind.Rename:
                FileSystemSafe.Move(source.Path, targetPath!);
                reportProgress();
                return targetPath;

            case OperationKind.Move:
                await MoveItemAsync(source.Path, targetPath!, cancellationToken, onBytes, reportProgress);
                return targetPath;

            case OperationKind.Copy:
                await CopyItemAsync(source.Path, targetPath!, cancellationToken, onBytes, reportProgress);
                return targetPath;

            case OperationKind.Delete:
                DeleteItem(source.Path, request.DeleteTarget);
                reportProgress();
                return null;

            default:
                throw new InvalidOperationException($"Unknown operation {request.Kind}");
        }
    }

    private static void DeleteItem(string path, DeleteTarget target)
    {
        if (Directory.Exists(path))
        {
            if (target == DeleteTarget.RecycleBin)
                RecycleBin.SendDirectory(path);
            else
                RecycleBin.DeletePermanently(path, isFolder: true);
        }
        else
        {
            if (target == DeleteTarget.RecycleBin)
                RecycleBin.SendFile(path);
            else
                RecycleBin.DeletePermanently(path, isFolder: false);
        }
    }

    private static async Task MoveItemAsync(string source, string target, CancellationToken ct, Action<long> onBytes, Action reportProgress)
    {
        var sameVolume = string.Equals(
            Path.GetPathRoot(Path.GetFullPath(source)),
            Path.GetPathRoot(Path.GetFullPath(target)),
            StringComparison.OrdinalIgnoreCase);

        if (sameVolume)
        {
            FileSystemSafe.Move(source, target);
            reportProgress();
            return;
        }

        // Entre volúmenes: copiar primero y borrar el origen solo tras
        // confirmar la copia (plan §8).
        await CopyItemAsync(source, target, ct, onBytes, reportProgress);
        if (Directory.Exists(source))
            Directory.Delete(source, recursive: true);
        else
            File.Delete(source);
    }

    private static async Task CopyItemAsync(string source, string target, CancellationToken ct, Action<long> onBytes, Action reportProgress)
    {
        if (Directory.Exists(source))
        {
            Directory.CreateDirectory(target);
            foreach (var entry in Directory.EnumerateFileSystemEntries(source))
            {
                ct.ThrowIfCancellationRequested();
                var childTarget = Path.Combine(target, Path.GetFileName(entry));
                await CopyItemAsync(entry, childTarget, ct, onBytes, reportProgress);
            }
            return;
        }

        const int bufferSize = 1024 * 1024;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        await using var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);

        var buffer = new byte[bufferSize];
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            onBytes(read);
        }
        await dst.FlushAsync(ct);
        reportProgress();
    }
}

/// <summary>Move con soporte de carpetas: File.Move no mueve directorios entre volúmenes.</summary>
internal static class FileSystemSafe
{
    public static void Move(string source, string target)
    {
        if (Directory.Exists(source))
            Directory.Move(source, target);
        else
            File.Move(source, target);
    }
}
