namespace EasyFileExplorer.Domain;

/// <summary>
/// Consulta de búsqueda: ámbito, filtros, orden y cancelación.
/// Distingue resultados parciales de completos.
/// </summary>
public sealed record SearchQuery
{
    /// <summary>Ámbito de la búsqueda (ubicación de partida).</summary>
    public required Location Scope { get; init; }

    /// <summary>Texto a buscar en el nombre (substring, insensible a mayúsculas).</summary>
    public string? NameContains { get; init; }

    /// <summary>Extensión exacta sin punto ("png").</summary>
    public string? Extension { get; init; }

    public long? MinSize { get; init; }
    public long? MaxSize { get; init; }
    public DateTimeOffset? ModifiedAfterUtc { get; init; }
    public DateTimeOffset? ModifiedBeforeUtc { get; init; }

    /// <summary>Buscar también en subcarpetas.</summary>
    public bool Recursive { get; init; } = true;

    /// <summary>Orden de resultados.</summary>
    public SearchSort Sort { get; init; } = SearchSort.Name;

    /// <summary>Límite de resultados (defensa contra enumeraciones infinitas).</summary>
    public int MaxResults { get; init; } = 10000;
}

public enum SearchSort
{
    Name,
    Size,
    Modified,
    Type,
}
