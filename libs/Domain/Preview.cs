namespace EasyFileExplorer.Domain;

/// <summary>
/// Solicitud de vista previa: identidad, versión, formato y límites.
/// La respuesta es contenido validado o un error seguro.
/// </summary>
public sealed record PreviewRequest
{
    public required ItemId Item { get; init; }

    /// <summary>Versión/contenido del elemento (timestamp+size) para invalidar cachés.</summary>
    public string? ContentVersion { get; init; }

    /// <summary>Formato deseado ("thumbnail", "text", "image").</summary>
    public required string Format { get; init; }

    /// <summary>Límite de bytes que el consumidor acepta decodificar.</summary>
    public long MaxBytes { get; init; } = 32 * 1024 * 1024;

    /// <summary>Ancho máximo en píxeles para imágenes/miniaturas.</summary>
    public int MaxWidth { get; init; } = 512;

    /// <summary>Alto máximo en píxeles.</summary>
    public int MaxHeight { get; init; } = 512;
}

/// <summary>Resultado de una vista previa.</summary>
public sealed record PreviewResult
{
    public required bool Succeeded { get; init; }
    public string? MimeType { get; init; }
    public byte[]? Data { get; init; }
    public string? Error { get; init; }

    /// <summary>true si el contenido fue truncado por <see cref="PreviewRequest.MaxBytes"/>.</summary>
    public bool Truncated { get; init; }
}
