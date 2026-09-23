using EasyFileExplorer.Domain;
using EasyFileExplorer.FileSystem;
using Xunit;

namespace EasyFileExplorer.UnitTests;

public class LocalFileSystemProviderTests : IDisposable
{
    private readonly string _root;

    public LocalFileSystemProviderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "efe-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private LocalFileSystemProvider Provider { get; } = new();

    private async Task<List<FileItem>> CollectAsync(Location location, EnumerateOptions? options = null, CancellationToken ct = default)
    {
        var items = new List<FileItem>();
        await foreach (var batch in Provider.EnumerateAsync(location, options ?? new EnumerateOptions(), ct))
            items.AddRange(batch.Items);
        return items;
    }

    [Fact]
    public async Task EnumeratesInBatchesWithExplicitCompletion()
    {
        for (var i = 0; i < 300; i++)
            File.WriteAllText(Path.Combine(_root, $"file{i}.txt"), "x");

        var loc = Location.LocalPath(_root);
        var states = new List<EnumerationState>();
        var items = 0;

        await foreach (var batch in Provider.EnumerateAsync(loc, new EnumerateOptions { BatchSize = 64 }, CancellationToken.None))
        {
            states.Add(batch.State);
            items += batch.Items.Count;
        }

        Assert.Equal(EnumerationState.Started, states[0]);
        Assert.Equal(EnumerationState.Complete, states[^1]);
        Assert.Equal(300, items);
    }

    [Fact]
    public async Task CancellationStopsEnumeration()
    {
        for (var i = 0; i < 500; i++)
            File.WriteAllText(Path.Combine(_root, $"f{i}"), "x");

        var loc = Location.LocalPath(_root);
        var cts = new CancellationTokenSource();
        var seen = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var batch in Provider.EnumerateAsync(loc, new EnumerateOptions { BatchSize = 8 }, cts.Token))
            {
                seen += batch.Items.Count;
                if (seen > 0)
                    cts.Cancel();
            }
        });
    }

    [Fact]
    public async Task HiddenAndSystemFlagsAreRespected()
    {
        var hidden = Path.Combine(_root, "hidden.txt");
        File.WriteAllText(hidden, "x");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        File.WriteAllText(Path.Combine(_root, "visible.txt"), "x");

        var loc = Location.LocalPath(_root);
        var withoutHidden = await CollectAsync(loc, new EnumerateOptions { IncludeHidden = false });
        Assert.DoesNotContain(withoutHidden, i => i.Name == "hidden.txt");
        Assert.Contains(withoutHidden, i => i.Name == "visible.txt");

        var withHidden = await CollectAsync(loc, new EnumerateOptions { IncludeHidden = true });
        Assert.Contains(withHidden, i => i.Name == "hidden.txt" && i.IsHidden);
    }

    [Fact]
    public async Task NonexistentLocationReportsErrorOrEmptyCompletion()
    {
        var loc = Location.LocalPath(Path.Combine(_root, "does-not-exist"));
        var errors = new List<EnumerationError>();
        var completed = false;
        try
        {
            await foreach (var batch in Provider.EnumerateAsync(loc, new EnumerateOptions(), CancellationToken.None))
            {
                if (batch.Error is not null)
                    errors.Add(batch.Error);
                if (batch.State == EnumerationState.Complete)
                    completed = true;
            }
        }
        catch (DirectoryNotFoundException)
        {
            completed = true; // también es aceptable: el proveedor lo reporta como error inmediato
        }
        Assert.True(completed || errors.Count > 0);
    }

    [Fact]
    public async Task FolderSizeIsNeverCalculated()
    {
        var sub = Path.Combine(_root, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "big.bin"), new string('x', 1000));
        File.WriteAllText(Path.Combine(_root, "leaf.txt"), "x");

        var loc = Location.LocalPath(_root);
        var items = await CollectAsync(loc);
        var folder = items.First(i => i.Name == "sub");
        Assert.Null(folder.Size);
        var file = items.First(i => i.Name == "leaf.txt");
        Assert.Equal(1, file.Size);
    }

    [Fact]
    public void LocationBreadcrumbs()
    {
        var loc = Location.LocalPath(_root);
        Assert.Equal(WellKnownProviders.Local, loc.Provider);
        Assert.NotEmpty(loc.Segments);
        Assert.Equal(loc.Address, loc.Segments[^1].Address);
    }
}
