namespace EasyFileExplorer.Domain;

/// <summary>
/// Ubicación navegable: proveedor + dirección + nombre visible.
/// No toda ubicación es una ruta de disco (papelera, resultados, dispositivos).
/// </summary>
public sealed record Location
{
    /// <summary>Proveedor que resuelve la ubicación.</summary>
    public required string Provider { get; init; }

    /// <summary>Dirección interna del proveedor (ruta para "local").</summary>
    public required string Address { get; init; }

    /// <summary>Nombre para mostrar en título/pestaña/breadcrumbs.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Segmentos para breadcrumb (para "local", partes de la ruta).</summary>
    public IReadOnlyList<LocationSegment> Segments { get; init; } = [];

    public static Location LocalPath(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        var root = System.IO.Path.GetPathRoot(full) ?? full;
        var segments = new List<LocationSegment> { new(root.TrimEnd('\\', '/'), root) };

        var rel = System.IO.Path.GetRelativePath(root, full);
        if (!string.IsNullOrEmpty(rel) && rel != ".")
        {
            var current = root;
            foreach (var part in rel.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))
            {
                current = System.IO.Path.Combine(current, part);
                segments.Add(new LocationSegment(part, current));
            }
        }

        var display = segments.Count > 1 ? segments[^1].Name : root;
        return new Location { Provider = WellKnownProviders.Local, Address = full, DisplayName = display, Segments = segments };
    }

    public override string ToString() => $"{Provider}:{Address}";
}

/// <summary>Un eslabón del breadcrumb.</summary>
public sealed record LocationSegment(string Name, string Address);

public static class WellKnownProviders
{
    public const string Local = "local";
    public const string Home = "home";
    public const string ThisPc = "thispc";
}
