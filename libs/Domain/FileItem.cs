namespace EasyFileExplorer.Domain;

/// <summary>
/// Metadatos mínimos de un elemento, obtenidos durante la enumeración sin
/// lecturas adicionales de disco (WIN32_FIND_DATA equivalente).
/// </summary>
public sealed record FileItem
{
    public required ItemId Id { get; init; }
    public required string Name { get; init; }
    public bool IsFolder { get; init; }

    /// <summary>Tamaño en bytes; null para carpetas (nunca se calcula recursivo en enumeración).</summary>
    public long? Size { get; init; }
    public DateTimeOffset? ModifiedUtc { get; init; }
    public DateTimeOffset? CreatedUtc { get; init; }
    public string Extension { get; init; } = string.Empty;

    public bool IsHidden { get; init; }
    public bool IsSystem { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsReparsePoint { get; init; }

    /// <summary>Atributos crudos del sistema de archivos.</summary>
    public uint Attributes { get; init; }

    /// <summary>Nombre de tipo legible ("Carpeta", "Archivo PNG"...) si el proveedor lo conoce.</summary>
    public string? TypeName { get; init; }
}
