using EasyFileExplorer.Domain;
using EasyFileExplorer.Operations;
using Xunit;

namespace EasyFileExplorer.UnitTests;

public class OperationEngineTests : IDisposable
{
    private readonly string _root;
    private readonly string _journalPath;
    private readonly OperationEngine _engine;

    public OperationEngineTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "efe-ops-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _journalPath = Path.Combine(_root, "journal.jsonl");
        _engine = new OperationEngine(new OperationJournal(_journalPath));
    }

    public void Dispose() => Directory.Delete(_root, true);

    private static ItemId Id(string p) => ItemId.ForPath(WellKnownProviders.Local, p);

    [Fact]
    public async Task CreateFolder_Works()
    {
        var target = Path.Combine(_root, "NewFolder");
        var result = await _engine.RunAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = OperationKind.CreateFolder,
            Sources = [Id(target)],
        }, CancellationToken.None);

        Assert.True(result.FullySucceeded);
        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public async Task Copy_ProducesVerifiedResult()
    {
        var src = Path.Combine(_root, "src.txt");
        File.WriteAllText(src, "hello");
        var dstDir = Path.Combine(_root, "dst");
        Directory.CreateDirectory(dstDir);

        var result = await _engine.RunAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = OperationKind.Copy,
            Sources = [Id(src)],
            TargetDirectory = Id(dstDir),
        }, CancellationToken.None);

        Assert.True(result.FullySucceeded);
        var expected = Path.Combine(dstDir, "src.txt");
        Assert.True(File.Exists(expected));
        Assert.Equal("hello", File.ReadAllText(expected));
        Assert.Equal(expected, result.Items[0].EffectivePath);
    }

    [Fact]
    public async Task CopyConflict_KeepBoth_RenamesDestination()
    {
        var src = Path.Combine(_root, "same.txt");
        File.WriteAllText(src, "new content");
        var dstDir = Path.Combine(_root, "dst");
        Directory.CreateDirectory(dstDir);
        File.WriteAllText(Path.Combine(dstDir, "same.txt"), "old content");

        var result = await _engine.RunAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = OperationKind.Copy,
            Sources = [Id(src)],
            TargetDirectory = Id(dstDir),
            ConflictPolicy = ConflictPolicy.KeepBoth,
        }, CancellationToken.None);

        Assert.True(result.FullySucceeded);
        Assert.True(File.Exists(Path.Combine(dstDir, "same.txt")));
        var renamed = result.Items[0].EffectivePath!;
        Assert.NotEqual(Path.Combine(dstDir, "same.txt"), renamed);
        Assert.True(File.Exists(renamed));
        Assert.Equal("old content", File.ReadAllText(Path.Combine(dstDir, "same.txt")));
        Assert.Equal("new content", File.ReadAllText(renamed));
    }

    [Fact]
    public async Task CopyConflict_Skip_LeavesTarget()
    {
        var src = Path.Combine(_root, "same.txt");
        File.WriteAllText(src, "new");
        var dstDir = Path.Combine(_root, "dst");
        Directory.CreateDirectory(dstDir);
        var existing = Path.Combine(dstDir, "same.txt");
        File.WriteAllText(existing, "old");

        var result = await _engine.RunAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = OperationKind.Copy,
            Sources = [Id(src)],
            TargetDirectory = Id(dstDir),
            ConflictPolicy = ConflictPolicy.Skip,
        }, CancellationToken.None);

        Assert.Equal(ItemOperationStatus.Skipped, result.Items[0].Status);
        Assert.Equal("old", File.ReadAllText(existing));
    }

    [Fact]
    public async Task Move_InsideOwnSubtree_IsRejected()
    {
        var dir = Path.Combine(_root, "parent");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "child.txt"), "x");
        var subdir = Path.Combine(dir, "inner");
        Directory.CreateDirectory(subdir);

        var result = await _engine.RunAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = OperationKind.Move,
            Sources = [Id(dir)],
            TargetDirectory = Id(subdir),
            ConflictPolicy = ConflictPolicy.Skip,
        }, CancellationToken.None);

        Assert.NotEmpty(result.Items);
        Assert.NotEqual(ItemOperationStatus.Completed, result.Items[0].Status);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public async Task Rename_Works()
    {
        var src = Path.Combine(_root, "old.txt");
        File.WriteAllText(src, "x");

        var result = await _engine.RunAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = OperationKind.Rename,
            Sources = [Id(src)],
            NewName = "new.txt",
        }, CancellationToken.None);

        Assert.True(result.FullySucceeded);
        Assert.False(File.Exists(src));
        Assert.True(File.Exists(Path.Combine(_root, "new.txt")));
    }

    [Fact]
    public async Task Delete_Permanent_RemovesFile()
    {
        var src = Path.Combine(_root, "gone.txt");
        File.WriteAllText(src, "x");

        var result = await _engine.RunAsync(new OperationRequest
        {
            OperationId = Guid.NewGuid(),
            Kind = OperationKind.Delete,
            Sources = [Id(src)],
            DeleteTarget = DeleteTarget.Permanent,
        }, CancellationToken.None);

        Assert.True(result.FullySucceeded);
        Assert.False(File.Exists(src));
    }

    [Fact]
    public async Task Journal_RecordsIntentAndCompletion()
    {
        var journal = new OperationJournal(_journalPath);
        var engine = new OperationEngine(journal);
        var src = Path.Combine(_root, "j.txt");
        File.WriteAllText(src, "x");

        var opId = Guid.NewGuid();
        await engine.RunAsync(new OperationRequest
        {
            OperationId = opId,
            Kind = OperationKind.Rename,
            Sources = [Id(src)],
            NewName = "renamed.txt",
        }, CancellationToken.None);

        var entries = journal.ReadAll();
        Assert.Contains(entries, e => e.Stage == "intent" && e.OperationId == opId);
        Assert.Contains(entries, e => e.Stage == "item-started" && e.OperationId == opId);
        Assert.Contains(entries, e => e.Stage == "item-done" && e.OperationId == opId);
        Assert.Contains(entries, e => e.Stage == "completed" && e.OperationId == opId);
        Assert.Empty(journal.FindUncertainItems());
    }

    [Fact]
    public void KeepBothName_GeneratesIncrementingNames()
    {
        var dir = Path.Combine(_root, "kb");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.txt"), "x");
        File.WriteAllText(Path.Combine(dir, "a (2).txt"), "x");

        var next = PathSafety.KeepBothName(Path.Combine(dir, "a.txt"), p => File.Exists(p) || Directory.Exists(p));
        Assert.EndsWith("a (3).txt", next);
    }

    [Fact]
    public void SubtreeDetection()
    {
        Assert.True(PathSafety.IsInsideSubtree(@"C:\a", @"C:\a\b"));
        Assert.False(PathSafety.IsInsideSubtree(@"C:\a", @"C:\ab"));
        Assert.False(PathSafety.IsInsideSubtree(@"C:\a", @"C:\a"));
    }
}
