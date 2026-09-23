using System.Runtime.InteropServices;

namespace EasyFileExplorer.Domain;

/// <summary>
/// Comparador "natural" de nombres: "file2" < "file10" (numérico-aware),
/// como hace Explorer (StrCmpLogicalW en Windows).
/// </summary>
public static class NaturalSort
{
    public static int Compare(string? a, string? b)
    {
        if (OperatingSystem.IsWindows())
            return StrCmpLogicalW(a, b);

        // Fallback portable: ordinal ignore-case.
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string? psz1, string? psz2);
}
