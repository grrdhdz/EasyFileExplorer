using EasyFileExplorer.Domain;

namespace EasyFileExplorer.Navigation;

/// <summary>
/// Historial atrás/adelante por pestaña con selección conservada por identidad.
/// </summary>
public sealed class NavigationHistory
{
    private readonly List<HistoryEntry> _entries = [];
    private int _index = -1;

    public sealed record HistoryEntry(Location Location, IReadOnlyCollection<ItemId> Selection)
    {
        /// <summary>Override manual para guards: selección guardada al salir de la entrada.</summary>
        public HistoryEntry WithSelection(IReadOnlyCollection<ItemId> selection) => this with { Selection = selection };
    }

    public bool CanGoBack => _index > 0;
    public bool CanGoForward => _index >= 0 && _index < _entries.Count - 1;
    public Location? Current => _index >= 0 ? _entries[_index].Location : null;

    public event EventHandler? Changed;

    /// <summary>Registra una navegación nueva, descartando el "adelante" pendiente.</summary>
    public void Push(Location location)
    {
        if (Current is { } current && current == location)
            return;

        if (_index < _entries.Count - 1)
            _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);

        _entries.Add(new HistoryEntry(location, []));
        _index = _entries.Count - 1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Guarda la selección de la entrada actual antes de abandonarla.</summary>
    public void SaveSelection(IReadOnlyCollection<ItemId> selection)
    {
        if (_index >= 0)
            _entries[_index] = _entries[_index].WithSelection(selection);
    }

    public Location? GoBack()
    {
        if (!CanGoBack)
            return null;
        _index--;
        Changed?.Invoke(this, EventArgs.Empty);
        return _entries[_index].Location;
    }

    public Location? GoForward()
    {
        if (!CanGoForward)
            return null;
        _index++;
        Changed?.Invoke(this, EventArgs.Empty);
        return _entries[_index].Location;
    }

    /// <summary>Selección guardada en la entrada actual, si existe.</summary>
    public IReadOnlyCollection<ItemId> SelectionOfCurrent() =>
        _index >= 0 ? _entries[_index].Selection : [];
}
