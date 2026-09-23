namespace EasyFileExplorer.Domain;

/// <summary>
/// Capacidades que un proveedor declara honestamente. La UI nunca ofrece
/// una acción que el proveedor no declara (plan §8: capacidades explícitas).
/// </summary>
[Flags]
public enum ProviderCapabilities : long
{
    None = 0,
    Enumerate = 1L << 0,
    Read = 1L << 1,
    Write = 1L << 2,
    Rename = 1L << 3,
    Move = 1L << 4,
    Copy = 1L << 5,
    Recycle = 1L << 6,
    PermanentDelete = 1L << 7,
    Preview = 1L << 8,
    Watch = 1L << 9,
    Undo = 1L << 10,
    Properties = 1L << 11,
    Search = 1L << 12,
    Thumbnails = 1L << 13,
    CreateFolder = 1L << 14,
    OpenWith = 1L << 15,
    LongPaths = 1L << 16,
}

/// <summary>Contrato de capacidades con metadatos.</summary>
public sealed record CapabilitySet
{
    public required ProviderCapabilities Capabilities { get; init; }
    public bool Supports(ProviderCapabilities flag) => (Capabilities & flag) == flag;
}
