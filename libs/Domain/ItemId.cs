namespace EasyFileExplorer.Domain;

/// <summary>
/// Identidad de un elemento gestionado por un proveedor.
/// La ruta no es el único identificador universal: <see cref="Identity"/> puede
/// contener una identidad estable del proveedor (p. ej. volumen+FileID en NTFS).
/// </summary>
public sealed record ItemId
{
    /// <summary>Identificador del proveedor ("local", "shell", "zip", "tags", ...).</summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Identidad estable dentro del proveedor. Para NTFS local será
    /// "vol:{serial}:file:{index}" cuando se resuelva; para enumeración rápida
    /// puede ser la ruta normalizada (ver <see cref="IsStable"/>).
    /// </summary>
    public required string Identity { get; init; }

    /// <summary>Representación de ubicación legible (ruta o URI virtual).</summary>
    public required string Path { get; init; }

    /// <summary>
    /// true si <see cref="Identity"/> sobrevive a renombrados/movimientos en el mismo
    /// volumen; false cuando la identidad es solo la ruta.
    /// </summary>
    public bool IsStable { get; init; }

    public static ItemId ForPath(string provider, string path, bool stable = false) =>
        new() { Provider = provider, Identity = path, Path = path, IsStable = stable };

    public override string ToString() => $"{Provider}:{Identity}";
}
