namespace EasyFileExplorer.Operations;

/// <summary>Utilidades de seguridad de rutas para operaciones de mutación.</summary>
public static class PathSafety
{
    /// <summary>true si <paramref name="target"/> está dentro del subárbol de <paramref name="source"/>.</summary>
    public static bool IsInsideSubtree(string source, string target)
    {
        var src = NormalizeWithSep(source);
        var dst = NormalizeWithSep(target);
        // Mismo camino no cuenta como "dentro del subárbol": es un caso aparte.
        return !dst.Equals(src, StringComparison.OrdinalIgnoreCase) &&
               dst.StartsWith(src, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeWithSep(string path)
    {
        var p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return p + Path.DirectorySeparatorChar;
    }

    /// <summary>true si solo difieren en mayúsculas (renombrado case-only en NTFS).</summary>
    public static bool DiffersOnlyInCase(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(a, b, StringComparison.Ordinal);

    /// <summary>
    /// Genera un nombre alternativo "nombre (n)" para conservar ambos.
    /// </summary>
    public static string KeepBothName(string targetPath, Func<string, bool> exists)
    {
        var dir = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var ext = Path.GetExtension(targetPath);
        var isFolder = string.IsNullOrEmpty(ext) || Directory.Exists(targetPath);

        for (var i = 2; i < 1000; i++)
        {
            var candidate = isFolder
                ? Path.Combine(dir, $"{name} ({i})")
                : Path.Combine(dir, $"{name} ({i}){ext}");
            if (!exists(candidate))
                return candidate;
        }
        throw new IOException($"No free alternative name for '{targetPath}'.");
    }
}
