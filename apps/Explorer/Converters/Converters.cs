using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using EasyFileExplorer.Domain;

namespace EasyFileExplorer.Explorer.Converters;

public sealed class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not long size)
            return string.Empty;
        return FormatSize(size);
    }

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var v = (double)bytes;
        var i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{bytes} {units[i]}" : $"{v:0.##} {units[i]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class DateFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DateTimeOffset dto)
            return string.Empty;
        return dto.ToLocalTime().ToString("g");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Icono Fluent por tipo de elemento.</summary>
public sealed class ItemGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not FileItem item)
            return "\uE7C3";

        if (item.IsFolder)
            return "\uE8B7"; // folder

        return item.Extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".tiff" or ".ico" => "\uEB9F",
            ".mp3" or ".wav" or ".flac" or ".m4a" or ".ogg" => "\uEC4F",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" => "\uE714",
            ".txt" or ".md" or ".log" or ".json" or ".xml" or ".yaml" or ".yml" or ".csv" => "\uE8A5",
            ".pdf" => "\uEA90",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "\uF012",
            ".exe" or ".msi" or ".msix" or ".bat" or ".cmd" or ".ps1" => "\uE756",
            ".doc" or ".docx" => "\uE8A5",
            ".xls" or ".xlsx" => "\uE8A5",
            ".ppt" or ".pptx" => "\uE8A5",
            ".cs" or ".cpp" or ".h" or ".py" or ".js" or ".ts" or ".html" or ".css" => "\uE943",
            _ => "\uE7C3",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is true;
        if (parameter as string == "invert")
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is not null;
        if (parameter as string == "invert")
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
