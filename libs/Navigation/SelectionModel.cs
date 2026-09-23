using EasyFileExplorer.Domain;

namespace EasyFileExplorer.Navigation;

/// <summary>
/// Selección por identidad: al reconciliar tras cambios externos, la selección
/// sobrevive mientras los <see cref="ItemId"/> sigan presentes.
/// </summary>
public sealed class SelectionModel
{
    private readonly HashSet<ItemId> _selection = [];

    public event EventHandler? Changed;

    public IReadOnlyCollection<ItemId> Items => _selection;
    public int Count => _selection.Count;

    public bool IsSelected(ItemId id) => _selection.Contains(id);

    public void Select(ItemId id)
    {
        if (_selection.Add(id))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Deselect(ItemId id)
    {
        if (_selection.Remove(id))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle(ItemId id)
    {
        if (!_selection.Remove(id))
            _selection.Add(id);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Replace(IEnumerable<ItemId> ids)
    {
        _selection.Clear();
        foreach (var id in ids)
            _selection.Add(id);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_selection.Count > 0)
        {
            _selection.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Tras cambios externos: descarta seleccionados que ya no existen.</summary>
    public void RetainExisting(IEnumerable<ItemId> existing)
    {
        var set = new HashSet<ItemId>(existing);
        var removed = _selection.RemoveWhere(id => !set.Contains(id));
        if (removed > 0)
            Changed?.Invoke(this, EventArgs.Empty);
    }
}
