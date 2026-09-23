using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using EasyFileExplorer.Domain;

namespace EasyFileExplorer.Explorer.Services;

/// <summary>
/// Portapapeles interoperable con Explorer: usa StorageItems (CF_HDROP) y
/// "Preferred DropEffect" para distinguir copiar de cortar.
/// </summary>
public sealed class ClipboardService
{
    private bool _isCut;

    public bool HasFiles { get; private set; }
    public bool IsCut => _isCut;

    public async Task CopyAsync(IReadOnlyList<ItemId> items, bool cut)
    {
        var package = new DataPackage { RequestedOperation = cut ? DataPackageOperation.Move : DataPackageOperation.Copy };

        var storageItems = new List<IStorageItem>();
        foreach (var item in items)
        {
            try
            {
                if (Directory.Exists(item.Path))
                    storageItems.Add(await StorageFolder.GetFolderFromPathAsync(item.Path));
                else if (File.Exists(item.Path))
                    storageItems.Add(await StorageFile.GetFileFromPathAsync(item.Path));
            }
            catch { /* elemento desaparecido */ }
        }
        if (storageItems.Count == 0)
            return;

        package.SetStorageItems(storageItems);

        // CFSTR_PREFERREDDROPEFFECT: 2 = copiar, 5 = cortar (DROPEFFECT_MOVE).
        using var stream = new MemoryStream(BitConverter.GetBytes(cut ? 5 : 2));
        package.SetData("Preferred DropEffect", Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(stream.AsRandomAccessStream()));

        Clipboard.SetContent(package);
        Clipboard.Flush();

        HasFiles = true;
        _isCut = cut;
    }

    /// <summary>Lee los elementos del portapapeles.</summary>
    public async Task<IReadOnlyList<string>> GetFilesAsync()
    {
        var view = Clipboard.GetContent();
        if (!view.Contains(StandardDataFormats.StorageItems))
            return [];

        var items = await view.GetStorageItemsAsync();
        return items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToArray();
    }
}
