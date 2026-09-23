namespace EasyFileExplorer.Domain;

/// <summary>Estado de un lote de enumeración.</summary>
public enum EnumerationState
{
    /// <summary>La enumeración comenzó; el lote puede estar vacío.</summary>
    Started,

    /// <summary>Lote parcial; llegarán más.</summary>
    Progress,

    /// <summary>Lote final explícito. La vista puede marcar el orden como definitivo.</summary>
    Complete,
}

/// <summary>Un lote de elementos con estado explícito.</summary>
public sealed record EnumerationBatch
{
    public required EnumerationState State { get; init; }
    public IReadOnlyList<FileItem> Items { get; init; } = [];

    /// <summary>Estimación del total si el proveedor la conoce; -1 si no.</summary>
    public int TotalEstimate { get; init; } = -1;

    /// <summary>Error no fatal asociado a este tramo (acceso denegado en subcarpeta, etc.).</summary>
    public EnumerationError? Error { get; init; }

    public static EnumerationBatch Start() => new() { State = EnumerationState.Started };
    public static EnumerationBatch Batch(IReadOnlyList<FileItem> items, int totalEstimate = -1) =>
        new() { State = EnumerationState.Progress, Items = items, TotalEstimate = totalEstimate };
    public static EnumerationBatch Done(IReadOnlyList<FileItem> items) =>
        new() { State = EnumerationState.Complete, Items = items };
}

/// <summary>Error no fatal durante la enumeración.</summary>
public sealed record EnumerationError
{
    public required string Message { get; init; }
    public string? ItemPath { get; init; }
    public int? NativeErrorCode { get; init; }
}

/// <summary>Opciones de enumeración.</summary>
public sealed record EnumerateOptions
{
    /// <summary>Incluir elementos ocultos.</summary>
    public bool IncludeHidden { get; init; } = true;

    /// <summary>Incluir elementos de sistema.</summary>
    public bool IncludeSystem { get; init; } = true;

    /// <summary>Tamaño sugerido de lote (el proveedor puede ajustar).</summary>
    public int BatchSize { get; init; } = 256;
}
