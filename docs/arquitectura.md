# Arquitectura

Este documento describe la arquitectura del monorepo: los límites de las piezas,
las reglas de dependencia y cómo el código actual se encaja en el plan a largo
plazo.

## Principios de diseño

1. **Integridad y seguridad primero.** Ninguna operación destruye datos sin
   verificación por elemento; el diario persistente registra intención,
   progreso por ítem y estados inciertos tras cierres abruptos.
2. **La UI nunca se bloquea por el sistema de archivos.** Toda enumeración,
   búsqueda, miniatura y operación es cancelable por generación y se consume en
   lotes; la navegación es instantánea.
3. **Proveedores con contrato estricto.** La UI nunca habla con `System.IO`
   directamente: habla con `IItemProvider`. Un proveedor declara capacidades
   (`ProviderCapabilities`) y la UI solo ofrece lo que el proveedor soporta.
4. **Identidades estables, no rutas.** Los ítems se seleccionan, ordenan y
   renombran por `ItemId` (volumen + FileID en NTFS, con resolución perezosa);
   la ruta es una forma de llegar, no la identidad.

## Mapa del monorepo

```
apps/
  Explorer/            Aplicación WinUI 3 (empaquetado: autocontenido hoy, MSIX después)
  OperationsHost/      (reservado) proceso aislado para operaciones destructivas
  PreviewHost/         (reservado) proceso aislado para decodificadores de vista previa
  ShellHost/           (reservado) host de extensiones del shell / integración con Explorer
  ElevationBroker/     (reservado) elevación puntuada (UAC por operación, no por proceso)
libs/
  Domain/              Contratos puros; sin dependencias de IO ni de UI.
  Navigation/          Sesión de navegación (generaciones), historial, selección.
  FileSystem/          Proveedor local + observador + identidad estable + buscador.
  Operations/          Motor de operaciones con diario JSONL y papelera.
  Providers/           (reservado) proveedores adicionales (nube, red, archivos comprimidos)
  Search/              (reservado) índice, consultas estructuradas, carpetas inteligentes
  Metadata/            (reservado) etiquetas de color, favoritos, preferencias (SQLite)
  Ipc/                 (reservado) canal entre procesos (named pipes, mensajes tipados)
  DesignSystem/        (reservado) tokens y componentes Fluent reutilizables
  Diagnostics/         (reservado) telemetría local, perfiles de rendimiento, logging
tests/
  unit/                Tests del núcleo (contratos, proveedor, navegación, operaciones)
  integration/         (reservado) temporales de verdad: portapapeles, diario, observador
  ui/                  (reservado) WinAppDriver / pruebas de aceptación
  reliability/         (reservado) caos: cortes de energía simulados, red inestable
  security/            (reservado) rutas maliciosas, junctions, ACLs, firmas
  performance/         (reservado) enumeración de 1M de ítems, presupuesto de memoria
benchmarks/            (reservado) microbenchmarks de ordenación, identidades, lectura
packaging/             MSIX / manifest de publicación
build/                 Scripts de construcción compartidos
docs/                  Este documento, ADRs, matriz de paridad, backlog
```

## Reglas de dependencia

- `Domain` no depende de nada. Todos los demás pueden depender de `Domain`.
- `Navigation`, `FileSystem`, `Operations` dependen solo de `Domain` (y BCL).
- `apps/Explorer` depende de las cuatro librerías actuales. La UI no accede a
  `System.IO` salvo en servicios de integración explícitos (portapapeles,
  miniaturas — código de adaptación al shell, no lógica de dominio).
- Ningún proyecto de `libs/` puede depender de `apps/` ni de WinUI.
- Tests solo consumen APIs públicas de `Domain`/`libs`.

## Contratos clave

### Enumeración

`IItemProvider.EnumerateAsync` devuelve `IAsyncEnumerable<EnumerationBatch>`:

- `Started` — primer mensaje, siempre.
- `Progress` — lotes de ítems (o `Error` por ítem/carpeta con `ItemPath`).
- `Complete` — finalización explícita, con el resto del lote.

El consumidor cancela con el `CancellationToken` de la sesión de navegación.
`NavigationSession.Navigate` incrementa una generación: todo lote anterior a la
generación vigente se descarta, de modo que un resultado de la carpeta anterior
nunca puede aparecer en la nueva.

### Identidades

`ItemId { Provider, Identity, Path, IsStable }`. En NTFS/ReFS el proveedor
resuelve `Identity = vol:{VolumeSerialNumber}:file:{FileId}` mediante
`GetFileInformationByHandleEx(FileIdInfo)`, con apertura
`FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT` (no sigue junctions
ni symlinks). Si la resolución falla, se conserva la identidad por ruta
(`IsStable = false`). Selección, renombrado y ordenación usan `ItemId`.

### Operaciones

`OperationEngine.RunAsync(request, ct)`:

1. Registra `intent` en el diario (`%LOCALAPPDATA%\EasyFileExplorer\operations-journal.jsonl`).
2. Por ítem: `item-started` → detección de conflicto → política o
   `ConflictCallback` → ejecuta → `item-done`/`item-failed`/`item-skipped`.
3. Al final: `completed`, `cancelled`, o queda `item-started` sin cierre →
   `uncertain` al siguiente arranque (`FindUncertainItems`).

Reglas de seguridad implementadas:
- Mover una carpeta dentro de su propio subárbol se rechaza antes de tocar disco.
- Renombrado case-only permitido (NTFS es case-preserving).
- Move entre volúmenes = copia verificada + borrado del origen.
- Borrado a papelera por defecto; permanente solo si `DeleteTarget.Permanent`
  y la UI lo confirma explícitamente.
- Pausa entre ítems; cancelación con `OperationCanceledException` →
  `item-cancelled`/`cancelled` y resultado `Cancelled`.

### Observación

`WatchAsync` envuelve `FileSystemWatcher` en un canal. `InternalBuffer` de 64 KB
y evento `Lost` cuando el buffer nativo se desborda — la pestaña re-enumea en
vez de presentar estado incompleto.

## Presupuesto de rendimiento

Objetivos del plan (fase 1 mínimo viable):

| Escenario | Presupuesto | Estado |
| --- | --- | --- |
| Abrir carpeta típica (≤10 000 ítems) | primer lote < 200 ms | lotes de 256 con `Task.Yield` |
| Memoria al listar | proporcional a ítems visibles | `ItemViewModel` ligero + virtualización de `ListView` |
| Tamaños de carpeta | nunca recursivos al listar | tamaño solo para archivos |
| Miniaturas | bajo demanda, cola de ≤4 workers, generación | `ThumbnailService` |
| Búsqueda | incremental, resultados en lotes de 64 | `LocalSearcher` DFS |

Los benchmarks formales y las suites de fiabilidad/seguridad llegan en fases 4–5.

## Procesos aislados (fases 2–3)

El contrato ya lo permite: `IItemProvider`, `OperationRequest` y
`OperationResult` son serializables salvo `ConflictCallback` (marcado
`[JsonIgnore]`). Los hosts `OperationsHost`/`PreviewHost`/`ShellHost`/
`ElevationBroker` se comunicarán por named pipes con mensajes tipados
(`libs/Ipc`). Mientras no existan, el engine corre en-proceso con las mismas
garantías semánticas; la migración a procesos no cambia la API.

## Concurrencia y UI

- Toda actualización de `ObservableCollection` se encola en el
  `DispatcherQueue` del hilo de UI.
- Generaciones para: navegación (`NavigationSession`), miniaturas
  (`ThumbnailService`), búsqueda (`TabViewModel`).
- Ningún `.Result`/`.Wait()` sobre código de UI; excepciones se propagan a
  `EnumerationError`/`OperationResult`/`StatusText`.
