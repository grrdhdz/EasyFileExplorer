using EasyFileExplorer.Domain;
using EasyFileExplorer.Navigation;
using Xunit;

namespace EasyFileExplorer.UnitTests;

public class NavigationHistoryTests
{
    private static Location L(string path) => Location.LocalPath(path);

    [Fact]
    public void PushBackForward()
    {
        var history = new NavigationHistory();
        history.Push(L("C:\\a"));
        history.Push(L("C:\\b"));
        Assert.True(history.CanGoBack);
        Assert.False(history.CanGoForward);

        var back = history.GoBack();
        Assert.Equal("C:\\a", back?.Address);
        Assert.True(history.CanGoForward);

        var fwd = history.GoForward();
        Assert.Equal("C:\\b", fwd?.Address);
    }

    [Fact]
    public void PushAfterBackDiscardsForward()
    {
        var history = new NavigationHistory();
        history.Push(L("C:\\a"));
        history.Push(L("C:\\b"));
        history.GoBack();
        history.Push(L("C:\\c"));
        Assert.False(history.CanGoForward);
        Assert.Equal("C:\\c", history.Current?.Address);
    }

    [Fact]
    public void SelectionIsSavedPerEntry()
    {
        var history = new NavigationHistory();
        history.Push(L("C:\\a"));
        var ids = new[] { ItemId.ForPath("local", "C:\\a\\f.txt") };
        history.SaveSelection(ids);
        Assert.Equal(ids, history.SelectionOfCurrent());
        history.Push(L("C:\\b"));
        Assert.Empty(history.SelectionOfCurrent());
        history.GoBack();
        Assert.Equal(ids, history.SelectionOfCurrent());
    }
}

public class SelectionModelTests
{
    private static ItemId Id(string p) => ItemId.ForPath("local", p);

    [Fact]
    public void ToggleAndRetain()
    {
        var sel = new SelectionModel();
        sel.Toggle(Id("a"));
        sel.Toggle(Id("b"));
        Assert.Equal(2, sel.Count);
        sel.Toggle(Id("a"));
        Assert.True(sel.IsSelected(Id("b")));
        Assert.False(sel.IsSelected(Id("a")));

        sel.RetainExisting([Id("b")]);
        Assert.Equal(1, sel.Count);
    }
}

public class NavigationSessionTests : IDisposable
{
    private readonly string _root;

    public NavigationSessionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "efe-nav-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task NewNavigationInvalidatesPreviousGeneration()
    {
        var provider = new EasyFileExplorer.FileSystem.LocalFileSystemProvider();
        using var session = new NavigationSession(provider);

        var r1 = session.Navigate(Location.LocalPath(_root));
        var g1 = r1.Generation;

        // Simular una segunda navegación antes de consumir la primera.
        var r2 = session.Navigate(Location.LocalPath(_root));
        var g2 = r2.Generation;

        Assert.True(g2 > g1);
        Assert.False(session.IsCurrent(g1));
        Assert.True(session.IsCurrent(g2));

        // La generación obsoleta está cancelada.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in r1.Batches) { }
        });
    }
}
