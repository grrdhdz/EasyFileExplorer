using CommunityToolkit.Mvvm.ComponentModel;
using EasyFileExplorer.Domain;

namespace EasyFileExplorer.Explorer.ViewModels;

/// <summary>Modelo de presentación de un elemento de la lista.</summary>
public sealed partial class ItemViewModel : ObservableObject
{
    public ItemViewModel(FileItem item)
    {
        Item = item;
    }

    public FileItem Item { get; }
    public ItemId Id => Item.Id;
    public string Name => Item.Name;
    public bool IsFolder => Item.IsFolder;
    public long? Size => Item.Size;
    public string SizeText => IsFolder || Size is null ? string.Empty : Converters.FileSizeConverter.FormatSize(Size.Value);
    public DateTimeOffset? ModifiedUtc => Item.ModifiedUtc;
    public string TypeText => Item.TypeName ?? (IsFolder ? "File folder" : ExtensionText);

    public string ExtensionText =>
        string.IsNullOrEmpty(Item.Extension) ? "File" : $"{Item.Extension.TrimStart('.').ToUpperInvariant()} File";

    public string Path => Item.Id.Path;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.ImageSource? _thumbnail;
}
