namespace EasyFileExplorer.Domain;

/// <summary>
/// Proveedor de elementos: enumera ubicaciones en lotes cancelables,
/// declara sus capacidades y opcionalmente vigila cambios.
/// </summary>
public interface IItemProvider
{
    /// <summary>Identificador del proveedor ("local").</summary>
    string ProviderId { get; }

    /// <summary>Capacidades declaradas honestamente.</summary>
    CapabilitySet Capabilities { get; }

    /// <summary>Resuelve una dirección en <see cref="Location"/> si el proveedor la entiende.</summary>
    bool TryResolveLocation(string address, out Location? location);

    /// <summary>
    /// Enumera una ubicación en lotes. Debe respetar <paramref name="cancellationToken"/>
    /// con latencia baja y entregar un lote <see cref="EnumerationState.Complete"/> final
    /// salvo cancelación.
    /// </summary>
    IAsyncEnumerable<EnumerationBatch> EnumerateAsync(
        Location location,
        EnumerateOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Flujo de cambios externos sobre la ubicación. Devuelve un flujo vacío si el
    /// proveedor no soporta vigilancia (capacidad <see cref="ProviderCapabilities.Watch"/>).
    /// </summary>
    IAsyncEnumerable<FileChangeEvent> WatchAsync(Location location, CancellationToken cancellationToken);
}

/// <summary>Tipo de cambio externo detectado.</summary>
public enum FileChangeKind
{
    Created,
    Deleted,
    Renamed,
    Modified,

    /// <summary>Se perdieron eventos; el consumidor debe reconciliar re-enumerando.</summary>
    Lost,
}

/// <summary>Evento de cambio en una ubicación vigilada.</summary>
public sealed record FileChangeEvent
{
    public required FileChangeKind Kind { get; init; }

    /// <summary>Elemento afectado cuando se conoce (en Deleted puede ser solo el Id).</summary>
    public FileItem? Item { get; init; }

    /// <summary>Ruta/identidad afectada.</summary>
    public string? Path { get; init; }

    /// <summary>En Renamed: ruta anterior.</summary>
    public string? OldPath { get; init; }
}
