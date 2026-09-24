# Diseño: ventana flotante de progreso de operaciones de archivo

## Objetivo

Mostrar el progreso de copiar / mover / eliminar (incluida papelera) en una ventana
pequeña separada — al estilo del diálogo nativo de Windows — sin perder las
tarjetas del centro de actividades, que siguen existiendo como historial.

## Decisiones tomadas

- **Auto-apertura**: la ventana aparece sola al iniciarse una operación.
- **Ventana única agrupada**: una sola ventana lista todas las operaciones activas
  (no una ventana por operación).
- **Auto-cierre**: cuando todas las operaciones terminan (éxito, error o cancelación),
  la ventana se cierra sola.
- **Solo cancelar** (v1): sin pausa; el motor (`IFileOperation`/`FilesystemOperations`)
  no la soporta hoy (`PauseTimer` → `E_NOTIMPL`).

## Arquitectura

### `FileOperationProgressWindow : Microsoft.UI.Xaml.Window`

Ventana WinUI 3 real (multi-ventana en el mismo hilo es soportado):

- `AppWindow` con presenter `OverlappedPresenter`: sin maximizar, ~480 px de ancho,
  altura dinámica según operaciones activas (scroll si supera tope).
- `SystemBackdrop` con Mica y tema igual que `MainWindow` (`RequestedTheme` del
  elemento raíz ligado al tema de la app).
- Contenido: `ListView` de `StatusCenterItem` (binding directo — el item ya expone
  `Header`, `CurrentProcessingItemName`, `ProgressPercentage`, `SpeedText`,
  `Message`, `SpeedGraphValues`, `CancelCommand`, `IsCancelable`).
- Plantilla por fila reutiliza el lenguaje visual de `StatusCenter.xaml`
  (icono + título, ProgressBar, sección expandible con `SpeedGraph` y velocidad,
  botón cancelar por fila). Mismos recursos `StatusCenterStyles.xaml` y strings
  localizadas (`ResourceString`) — nada de texto hardcodeado.

### `FileOperationProgressWindowController`

Singleton que engancha el pipeline existente **sin tocar el motor**:

- `StatusCenterViewModel.NewItemAdded` → si el item es `InProgress` y su
  `FileOperationType` es `Copy | Move | Delete | Recycle` → añade fila y abre
  la ventana (`Activate()`).
- `StatusCenterItem.PropertyChanged` (`FileSystemOperationReturnResult` ≠
  `InProgress`) o `StatusCenterItems` remove → quita la fila.
- Lista vacía → `Close()` la ventana.
- Cierre manual por el usuario → solo oculta (no cancela nada); se reabre cuando
  llega la siguiente operación `InProgress`.

## Flujo de datos

`FilesystemHelpers` → `StatusCenterHelper.AddCard_*` → `StatusCenterItem` (nuevo)
→ `IProgress<StatusCenterItemProgressModel>` alimenta el item → la ventana solo
observa el item. Cero cambios en `IFilesystemOperations`.

## Errores y bordes

- Operación cancelada por el usuario desde la ventana → `CancelCommand` existente;
  la tarjeta termina como `Cancelled` y la fila desaparece.
- Cierre de la ventana durante operaciones → se suprime auto-apertura solo para
  esas operaciones; la siguiente operación nueva la reabre.
- App principal cerrada → la ventana muere con el proceso.

## Verificación

- `msbuild -restore Files.slnx -p:Configuration=Debug -p:Platform=x64` sin errores.
- Manual: copiar una carpeta grande (progreso, velocidad, gráfica, nombre actual),
  cancelar a mitad, dos operaciones simultáneas, cierre automático al completar.
