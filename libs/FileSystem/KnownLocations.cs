using EasyFileExplorer.Domain;

namespace EasyFileExplorer.FileSystem;

/// <summary>
/// Ubicaciones conocidas (carpetas del usuario) y unidades, para el panel lateral.
/// </summary>
public static class KnownLocations
{
    public sealed record SideEntry(string Name, string Glyph, Location Location);

    /// <summary>Acceso rápido: carpetas conocidas del usuario.</summary>
    public static IReadOnlyList<SideEntry> QuickAccess()
    {
        var list = new List<SideEntry>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        list.Add(new SideEntry("Home", "\uE80F", Location.LocalPath(userProfile)));

        AddKnown(list, Environment.SpecialFolder.Desktop, "Desktop", "\uE8FC");
        AddKnown(list, Environment.SpecialFolder.MyDocuments, "Documents", "\uE8A5");

        var downloads = Path.Combine(userProfile, "Downloads");
        if (Directory.Exists(downloads))
            list.Add(new SideEntry("Downloads", "\uE896", Location.LocalPath(downloads)));

        AddKnown(list, Environment.SpecialFolder.MyPictures, "Pictures", "\uEB9F");
        AddKnown(list, Environment.SpecialFolder.MyMusic, "Music", "\uEC4F");
        AddKnown(list, Environment.SpecialFolder.MyVideos, "Videos", "\uE714");
        return list;
    }

    private static void AddKnown(List<SideEntry> list, Environment.SpecialFolder folder, string name, string glyph)
    {
        var path = Environment.GetFolderPath(folder);
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            list.Add(new SideEntry(name, glyph, Location.LocalPath(path)));
    }

    /// <summary>Unidades del sistema (This PC).</summary>
    public static IReadOnlyList<SideEntry> Drives()
    {
        var list = new List<SideEntry>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                var label = drive.IsReady && !string.IsNullOrEmpty(drive.VolumeLabel)
                    ? $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})"
                    : $"{DriveTypeName(drive.DriveType)} ({drive.Name.TrimEnd('\\')})";
                list.Add(new SideEntry(label, DriveGlyph(drive.DriveType), Location.LocalPath(drive.RootDirectory.FullName)));
            }
            catch
            {
                // Unidad desconectada o no accesible: mostrarla igualmente como desconectada.
                list.Add(new SideEntry($"{DriveTypeName(drive.DriveType)} ({drive.Name.TrimEnd('\\')})", "\uE88E", Location.LocalPath(drive.Name)));
            }
        }
        return list;
    }

    private static string DriveTypeName(DriveType type) => type switch
    {
        DriveType.Fixed => "Local Disk",
        DriveType.Removable => "Removable Drive",
        DriveType.Network => "Network Drive",
        DriveType.CDRom => "Optical Drive",
        DriveType.Ram => "RAM Drive",
        _ => "Drive",
    };

    private static string DriveGlyph(DriveType type) => type switch
    {
        DriveType.Removable => "\uE88E",
        DriveType.Network => "\uE8CE",
        DriveType.CDRom => "\uE958",
        _ => "\uEDA2",
    };
}
