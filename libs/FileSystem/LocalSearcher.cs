using System.Runtime.CompilerServices;
using EasyFileExplorer.Domain;

namespace EasyFileExplorer.FileSystem;

/// <summary>
/// Búsqueda local por nombre y filtros básicos, progresiva y cancelable.
/// No arranca un indexador global: recorre el ámbito elegido respetando
/// límites y entrega resultados en lotes.
/// </summary>
public sealed class LocalSearcher
{
    private readonly EnumerateOptions _enumOptions = new() { BatchSize = 64 };

    public async IAsyncEnumerable<EnumerationBatch> SearchAsync(
        SearchQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return EnumerationBatch.Start();

        var provider = new LocalFileSystemProvider();
        var results = new List<FileItem>(64);
        var yielded = 0;
        var stack = new Stack<string>();
        stack.Push(query.Scope.Address);

        while (stack.Count > 0 && yielded < query.MaxResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dir = stack.Pop();

            await foreach (var batch in provider.EnumerateAsync(
                               Location.LocalPath(dir), _enumOptions, cancellationToken))
            {
                if (batch.Error is not null)
                    yield return batch;

                foreach (var item in batch.Items)
                {
                    if (Matches(item, query))
                    {
                        results.Add(item);
                        yielded++;

                        if (results.Count >= 64)
                        {
                            yield return EnumerationBatch.Batch(results.ToArray());
                            results.Clear();
                        }
                    }

                    if (query.Recursive && item.IsFolder && !item.IsReparsePoint && yielded < query.MaxResults)
                        stack.Push(item.Id.Path);
                }

                if (yielded >= query.MaxResults)
                    break;
            }
        }

        yield return EnumerationBatch.Done(results);
    }

    private static bool Matches(FileItem item, SearchQuery query)
    {
        if (query.NameContains is { Length: > 0 } needle &&
            !item.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Extension is { Length: > 0 } ext &&
            !item.Extension.Equals("." + ext, StringComparison.OrdinalIgnoreCase))
            return false;

        if (item.IsFolder)
            return query.NameContains is { Length: > 0 }; // carpetas solo por nombre

        if (query.MinSize is { } min && item.Size < min)
            return false;
        if (query.MaxSize is { } max && item.Size > max)
            return false;
        if (query.ModifiedAfterUtc is { } after && item.ModifiedUtc < after)
            return false;
        if (query.ModifiedBeforeUtc is { } before && item.ModifiedUtc > before)
            return false;

        return true;
    }
}
