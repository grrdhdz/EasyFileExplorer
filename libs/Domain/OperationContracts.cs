namespace EasyFileExplorer.Domain;

/// <summary>Tipos de operación sobre elementos.</summary>
public enum OperationKind
{
    Copy,
    Move,
    Rename,
    Delete,
    CreateFolder,
}

/// <summary>Política ante un conflicto de destino existente.</summary>
public enum ConflictPolicy
{
    /// <summary>Preguntar por cada conflicto (la UI decide).</summary>
    AskEach,

    /// <summary>Omitir el elemento en conflicto.</summary>
    Skip,

    /// <summary>Reemplazar el destino.</summary>
    Replace,

    /// <summary>Conservar ambos renombrando el nuevo ("nombre (2)").</summary>
    KeepBoth,
}

/// <summary>Dónde enviar los elementos eliminados.</summary>
public enum DeleteTarget
{
    /// <summary>Papelera cuando el proveedor la soporte; indicar antes si será permanente.</summary>
    RecycleBin,

    /// <summary>Borrado permanente explícito (Shift+Delete).</summary>
    Permanent,
}

/// <summary>
/// Petición de operación: identificador único, origen/destino,
/// política de conflictos. Sin ejecución automática implícita.
/// </summary>
public sealed record OperationRequest
{
    public required Guid OperationId { get; init; }
    public required OperationKind Kind { get; init; }

    /// <summary>Elementos de origen (rutas del proveedor local en esta fase).</summary>
    public required IReadOnlyList<ItemId> Sources { get; init; }

    /// <summary>Directorio destino para Copy/Move (ItemId de carpeta).</summary>
    public ItemId? TargetDirectory { get; init; }

    /// <summary>Nuevo nombre para Rename (un solo origen).</summary>
    public string? NewName { get; init; }

    /// <summary>Para Delete.</summary>
    public DeleteTarget DeleteTarget { get; init; } = DeleteTarget.RecycleBin;

    public ConflictPolicy ConflictPolicy { get; init; } = ConflictPolicy.AskEach;

    /// <summary>Callback para resolver conflictos cuando la política es AskEach. Devuelve la política efectiva para ese elemento.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<OperationConflict, ConflictResolution>? ConflictCallback { get; init; }
}

/// <summary>Conflicto concreto que requiere decisión.</summary>
public sealed record OperationConflict
{
    public required ItemId Source { get; init; }
    public required string TargetPath { get; init; }
    public required ConflictKind Kind { get; init; }
    public long? SourceSize { get; init; }
    public long? TargetSize { get; init; }
    public DateTimeOffset? SourceModified { get; init; }
    public DateTimeOffset? TargetModified { get; init; }
}

public enum ConflictKind
{
    TargetExists,
    TargetIsInsideSource,
    NameCaseOnlyDiffers,
}

/// <summary>Decisión sobre un conflicto; puede aplicarse al resto del lote.</summary>
public sealed record ConflictResolution
{
    public required ConflictPolicy Policy { get; init; }

    /// <summary>Aplicar la misma decisión a los conflictos restantes de la operación.</summary>
    public bool ApplyToRemaining { get; init; }
}

/// <summary>Estado por elemento dentro de una operación.</summary>
public enum ItemOperationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped,
    Cancelled,

    /// <summary>No se puede determinar el resultado tras un cierre abrupto.</summary>
    Uncertain,
}

/// <summary>Resultado individual de un elemento.</summary>
public sealed record ItemOperationResult
{
    public required ItemId Item { get; init; }
    public required ItemOperationStatus Status { get; init; }
    public string? Error { get; init; }
    public int? NativeErrorCode { get; init; }

    /// <summary>Ruta final efectiva (tras KeepBoth puede diferir de la pedida).</summary>
    public string? EffectivePath { get; init; }
}

/// <summary>Progreso agregado de una operación.</summary>
public sealed record OperationProgress
{
    public required Guid OperationId { get; init; }
    public int ItemsCompleted { get; init; }
    public int ItemsTotal { get; init; }
    public long BytesCompleted { get; init; }
    public long BytesTotal { get; init; }

    /// <summary>Elemento en curso, para mostrar nombre/velocidad.</summary>
    public string? CurrentItemPath { get; init; }
}

/// <summary>
/// Resultado de una operación. La app solo declara éxito tras comprobar el
/// resultado real por elemento, nunca al aceptar la tarea en cola.
/// </summary>
public sealed record OperationResult
{
    public required Guid OperationId { get; init; }
    public required IReadOnlyList<ItemOperationResult> Items { get; init; }
    public bool WasCancelled { get; init; }

    public int Succeeded => Items.Count(i => i.Status == ItemOperationStatus.Completed);
    public int Failed => Items.Count(i => i.Status == ItemOperationStatus.Failed);
    public int Skipped => Items.Count(i => i.Status == ItemOperationStatus.Skipped);
    public bool FullySucceeded => Failed == 0 && !WasCancelled;
}
