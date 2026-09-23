using System.Collections.Concurrent;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using EasyFileExplorer.Explorer.ViewModels;

namespace EasyFileExplorer.Explorer.Services;

/// <summary>
/// Miniaturas bajo demanda: solo para elementos visibles/cercanos, con
/// concurrencia acotada y generación de navegación para descartar respuestas
/// obsoletas (plan §6: las respuestas antiguas se descartan).
/// </summary>
public sealed class ThumbnailService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".ico",
    };

    private readonly DispatcherQueue _dispatcher;
    private readonly ConcurrentQueue<(ItemViewModel vm, long generation)> _queue = new();
    private readonly SemaphoreSlim _slots = new(4);
    private readonly ConcurrentDictionary<string, long> _requested = new();
    private int _workers;

    public ThumbnailService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>Solicita miniatura si es imagen y el elemento sigue vigente.</summary>
    public void Request(ItemViewModel vm, long generation, Func<long, bool> isCurrent)
    {
        if (vm.IsFolder || !ImageExtensions.Contains(vm.Item.Extension))
            return;

        if (!_requested.TryAdd(vm.Path, generation))
            return;

        _queue.Enqueue((vm, generation));
        EnsureWorkers(isCurrent);
    }

    /// <summary>Nueva generación: invalida solicitudes pendientes.</summary>
    public void Invalidate()
    {
        _requested.Clear();
        while (_queue.TryDequeue(out _)) { }
    }

    private void EnsureWorkers(Func<long, bool> isCurrent)
    {
        while (_workers < 4 && Interlocked.Increment(ref _workers) <= 4)
            _ = WorkerLoop(isCurrent);
    }

    private async Task WorkerLoop(Func<long, bool> isCurrent)
    {
        try
        {
            while (_queue.TryDequeue(out var job))
            {
                await _slots.WaitAsync();
                try
                {
                    if (!isCurrent(job.generation))
                        continue;

                    var bitmap = await LoadThumbnailAsync(job.vm.Path);
                    if (bitmap is null || !isCurrent(job.generation))
                        continue;

                    _dispatcher.TryEnqueue(() =>
                    {
                        if (isCurrent(job.generation))
                            job.vm.Thumbnail = bitmap;
                    });
                }
                finally
                {
                    _slots.Release();
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _workers);
        }
    }

    private static async Task<BitmapImage?> LoadThumbnailAsync(string path)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 96);
            if (thumb is null || thumb.Size == 0)
                return null;

            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(thumb);
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
