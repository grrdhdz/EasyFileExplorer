using System.Runtime.CompilerServices;
using EasyFileExplorer.Domain;

namespace EasyFileExplorer.FileSystem;

/// <summary>
/// Proveedor del sistema de archivos local (NTFS, exFAT/FAT32, extraíbles).
/// Enumera fuera del hilo de UI en lotes cancelables con metadatos mínimos
/// (sin lecturas de contenido ni tamaños recursivos).
/// </summary>
public sealed class LocalFileSystemProvider : IItemProvider
{
    public const string ProviderId = WellKnownProviders.Local;

    string IItemProvider.ProviderId => ProviderId;

    public CapabilitySet Capabilities { get; } = new()
    {
        Capabilities = ProviderCapabilities.Enumerate
            | ProviderCapabilities.Read
            | ProviderCapabilities.Write
            | ProviderCapabilities.Rename
            | ProviderCapabilities.Move
            | ProviderCapabilities.Copy
            | ProviderCapabilities.Recycle
            | ProviderCapabilities.PermanentDelete
            | ProviderCapabilities.Preview
            | ProviderCapabilities.Watch
            | ProviderCapabilities.Properties
            | ProviderCapabilities.Search
            | ProviderCapabilities.Thumbnails
            | ProviderCapabilities.CreateFolder
            | ProviderCapabilities.OpenWith
            | ProviderCapabilities.LongPaths,
    };

    public bool TryResolveLocation(string address, out Location? location)
    {
        location = null;
        if (string.IsNullOrWhiteSpace(address))
            return false;

        string full;
        try
        {
            full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(address));
        }
        catch
        {
            return false;
        }

        // Rutas locales y UNC.
        if (!Path.IsPathRooted(full) && !full.StartsWith(@"\\", StringComparison.Ordinal))
            return false;

        location = Location.LocalPath(full);
        return true;
    }

    public async IAsyncEnumerable<EnumerationBatch> EnumerateAsync(
        Location location,
        EnumerateOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return EnumerationBatch.Start();

        var batchSize = Math.Clamp(options.BatchSize, 16, 4096);
        var buffer = new List<FileItem>(batchSize);

        using IEnumerator<FileSystemInfo> enumerator = CreateEnumerator(location.Address, options)
            .GetEnumerator();

        var finished = false;
        while (!finished)
        {
            cancellationToken.ThrowIfCancellationRequested();

            buffer.Clear();
            var count = 0;
            EnumerationError? pendingError = null;
            // Un paso de enumeración nativa por elemento; una excepción del propio
            // enumerador (p. ej. la carpeta desapareció) termina la secuencia con error.
            while (count < batchSize)
            {
                bool has;
                try
                {
                    has = enumerator.MoveNext();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    pendingError = new EnumerationError { Message = ex.Message, ItemPath = location.Address };
                    has = false;
                }

                if (!has)
                {
                    finished = true;
                    break;
                }

                var item = TryMap(enumerator.Current);
                if (item is null)
                    continue;

                if (!options.IncludeHidden && item.IsHidden)
                    continue;
                if (!options.IncludeSystem && item.IsSystem)
                    continue;

                buffer.Add(item);
                count++;
            }

            if (pendingError is not null)
            {
                yield return new EnumerationBatch
                {
                    State = EnumerationState.Progress,
                    Error = pendingError,
                };
            }

            if (finished)
            {
                yield return EnumerationBatch.Done(buffer);
            }
            else if (buffer.Count > 0)
            {
                yield return EnumerationBatch.Batch(buffer.ToArray());
            }

            // Cedemos para que el consumidor pueda programar el lote en la UI.
            await Task.Yield();
        }
    }

    private static IEnumerable<FileSystemInfo> CreateEnumerator(string path, EnumerateOptions options)
    {
        var dir = new DirectoryInfo(path);
        var enumOptions = new System.IO.EnumerationOptions
        {
            RecurseSubdirectories = false,
            AttributesToSkip = 0, // no saltar ocultos/sistema: los filtramos nosotros para poder mostrarlos
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
        };
        return dir.EnumerateFileSystemInfos("*", enumOptions);
    }

    private static FileItem? TryMap(FileSystemInfo info)
    {
        try
        {
            var isFolder = info is DirectoryInfo;
            var attrs = (uint)info.Attributes;
            long? size = null;
            if (info is FileInfo fi)
                size = fi.Length;

            return new FileItem
            {
                Id = ItemId.ForPath(ProviderId, info.FullName),
                Name = info.Name,
                IsFolder = isFolder,
                Size = size,
                ModifiedUtc = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                CreatedUtc = new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero),
                Extension = isFolder ? string.Empty : info.Extension,
                IsHidden = (attrs & 0x2) != 0,
                IsSystem = (attrs & 0x4) != 0,
                IsReadOnly = (attrs & 0x1) != 0,
                IsReparsePoint = (attrs & 0x400) != 0,
                Attributes = attrs,
                TypeName = isFolder ? "File folder" : null,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Elemento borrado entre enumeración y lectura de metadatos.
            return null;
        }
    }

    public IAsyncEnumerable<FileChangeEvent> WatchAsync(Location location, CancellationToken cancellationToken)
    {
        return LocalChangeWatcher.WatchAsync(location.Address, cancellationToken);
    }
}
