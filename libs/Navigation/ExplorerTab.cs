using EasyFileExplorer.Domain;

namespace EasyFileExplorer.Navigation;

/// <summary>
/// Una pestaña: sesión de navegación + historial + selección.
/// Estado de selección y orden independiente por pestaña (plan §3).
/// </summary>
public sealed class ExplorerTab : IDisposable
{
    public ExplorerTab(IItemProvider provider, Location initialLocation)
    {
        Session = new NavigationSession(provider);
        History = new NavigationHistory();
        Selection = new SelectionModel();
        InitialLocation = initialLocation;
    }

    public Location InitialLocation { get; }

    public NavigationSession Session { get; }
    public NavigationHistory History { get; }
    public SelectionModel Selection { get; }

    /// <summary>Orden activo en esta pestaña.</summary>
    public SortState Sort { get; set; } = new(SortColumn.Name, Ascending: true);

    public void Dispose() => Session.Dispose();
}

public enum SortColumn
{
    Name,
    Size,
    Modified,
    Type,
}

public sealed record SortState(SortColumn Column, bool Ascending);
