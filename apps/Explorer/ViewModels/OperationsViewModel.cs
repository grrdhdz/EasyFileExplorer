using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using EasyFileExplorer.Domain;
using EasyFileExplorer.Operations;

namespace EasyFileExplorer.Explorer.ViewModels;

/// <summary>
/// Centro de operaciones: ejecuta en background con progreso visible sin
/// impedir seguir navegando (plan §3). Muestra qué se completó y qué quedó
/// pendiente — nunca declara éxito al encolar.
/// </summary>
public sealed partial class OperationsViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;
    private readonly OperationEngine _engine;
    private readonly OperationJournal _journal;

    /// <summary>
    /// Resolvedor de conflictos inyectado por la vista (necesita XamlRoot).
    /// Si es null, los conflictos se omiten de forma segura.
    /// </summary>
    public Func<OperationConflict, Task<ConflictResolution>>? ConflictUI { get; set; }

    public OperationsViewModel(OperationJournal journal, DispatcherQueue dispatcher)
    {
        _journal = journal;
        _dispatcher = dispatcher;
        _engine = new OperationEngine(journal);
        _engine.Progress += OnProgress;
    }

    public ObservableCollection<OperationViewModel> Active { get; } = [];

    [ObservableProperty]
    private bool _hasActive;

    /// <summary>
    /// Ejecuta una petición fuera del hilo UI; el resultado por elemento
    /// se muestra al terminar. Devuelve el resultado real verificado.
    /// </summary>
    public Task<OperationResult> RunAsync(OperationRequest request)
    {
        var vm = new OperationViewModel(request);
        _dispatcher.TryEnqueue(() =>
        {
            Active.Add(vm);
            HasActive = true;
        });

        return Task.Run(async () =>
        {
            var ui = ConflictUI;
            var req = request.ConflictPolicy == ConflictPolicy.AskEach
                ? request with
                {
                    ConflictCallback = conflict =>
                    {
                        if (ui is null)
                            return new ConflictResolution { Policy = ConflictPolicy.Skip };
                        var tcs = new TaskCompletionSource<ConflictResolution>();
                        _dispatcher.TryEnqueue(async () =>
                        {
                            try { tcs.SetResult(await ui(conflict)); }
                            catch (Exception ex) { tcs.SetException(ex); }
                        });
                        return tcs.Task.Result;
                    },
                }
                : request;

            var result = await _engine.RunAsync(req, CancellationToken.None);

            _dispatcher.TryEnqueue(() =>
            {
                vm.Complete(result);
                Active.Remove(vm);
                HasActive = Active.Count > 0;
            });
            return result;
        });
    }

    private void OnProgress(object? sender, OperationProgress progress)
    {
        _dispatcher.TryEnqueue(() =>
        {
            // El progreso detallado por elemento se refleja en el centro de operaciones.
        });
    }
}

public sealed partial class OperationViewModel : ObservableObject
{
    public OperationViewModel(OperationRequest request)
    {
        Request = request;
        Description = Describe(request);
    }

    public OperationRequest Request { get; }
    public string Description { get; }

    [ObservableProperty]
    private string _resultText = "Running...";

    public void Complete(OperationResult result)
    {
        ResultText = result.FullySucceeded
            ? $"Done — {result.Succeeded} item(s)"
            : $"{result.Succeeded} done, {result.Failed} failed, {result.Skipped} skipped";
    }

    private static string Describe(OperationRequest r) => r.Kind switch
    {
        OperationKind.Copy => $"Copy {r.Sources.Count} item(s) → {r.TargetDirectory?.Path}",
        OperationKind.Move => $"Move {r.Sources.Count} item(s) → {r.TargetDirectory?.Path}",
        OperationKind.Delete => $"Delete {r.Sources.Count} item(s) ({r.DeleteTarget})",
        OperationKind.Rename => $"Rename → {r.NewName}",
        OperationKind.CreateFolder => "New folder",
        _ => r.Kind.ToString(),
    };
}
