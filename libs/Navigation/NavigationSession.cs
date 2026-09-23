using EasyFileExplorer.Domain;

namespace EasyFileExplorer.Navigation;

/// <summary>
/// Sesión de navegación de una pestaña. Cada navegación crea una generación
/// nueva y cancela la anterior: un resultado de la carpeta anterior nunca
/// puede aparecer en la nueva.
/// </summary>
public sealed class NavigationSession : IDisposable
{
    private readonly IItemProvider _provider;
    private CancellationTokenSource _generationCts = new();
    private long _generation;
    private Task _watchTask = Task.CompletedTask;

    public NavigationSession(IItemProvider provider)
    {
        _provider = provider;
    }

    /// <summary>Generación actual; sube con cada navegación.</summary>
    public long Generation => Interlocked.Read(ref _generation);

    public Location? CurrentLocation { get; private set; }

    /// <summary>Eventos de cambio externos en la ubicación vigilada.</summary>
    public event EventHandler<FileChangeEvent>? ExternalChange;

    /// <summary>
    /// Inicia una navegación: nueva generación, cancela la anterior y devuelve
    /// el flujo de lotes ya acotado a esta generación.
    /// </summary>
    public NavigationRequest Navigate(Location location, EnumerateOptions? options = null)
    {
        var generation = Interlocked.Increment(ref _generation);
        var cts = Interlocked.Exchange(ref _generationCts, new CancellationTokenSource());
        cts.Cancel();
        cts.Dispose();

        CurrentLocation = location;

        StartWatching(location, _generationCts.Token);

        return new NavigationRequest(
            generation,
            location,
            _provider.EnumerateAsync(location, options ?? new EnumerateOptions(), _generationCts.Token),
            _generationCts.Token);
    }

    /// <summary>true si la generación sigue siendo la vigente (para descartar respuestas antiguas).</summary>
    public bool IsCurrent(long generation) => generation == Generation;

    public void Cancel() => _generationCts.Cancel();

    private void StartWatching(Location location, CancellationToken cancellationToken)
    {
        if (!_provider.Capabilities.Supports(ProviderCapabilities.Watch))
            return;

        _watchTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var ev in _provider.WatchAsync(location, cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    ExternalChange?.Invoke(this, ev);
                }
            }
            catch (OperationCanceledException) { }
            catch { /* la vigilancia es best-effort; nunca derriba la navegación */ }
        }, CancellationToken.None);
    }

    public void Dispose()
    {
        _generationCts.Cancel();
        _generationCts.Dispose();
    }
}

/// <summary>Resultado de una navegación: flujo de lotes + generación para validar obsolescencia.</summary>
public sealed record NavigationRequest(
    long Generation,
    Location Location,
    IAsyncEnumerable<EnumerationBatch> Batches,
    CancellationToken CancellationToken);
