# ADR 0002 — Identidades de ítem y modelo de proveedores

**Estado:** aceptado (2026-09-23)

## Contexto

Windows Explorer usa rutas como identificador casi universal, con resultados
conocidos: selección perdida tras renombrado, duplicados al mezclar junctions,
y colisiones al enumerar unidades distintas con la misma letra. El plan exige
identidad estable con resolución perezosa y un contrato de proveedores donde la
ruta no es la identidad universal.

## Decisión

1. `ItemId { Provider, Identity, Path, IsStable }`. `Identity` es
   `vol:{VolumeSerialNumber}:file:{FileId}` en NTFS/ReFS mediante
   `GetFileInformationByHandleEx(FileIdInfo)`; la apertura usa
   `FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT` para no seguir
   junctions ni symlinks. Si falla, se conserva la ruta (`IsStable = false`).
2. `Location { Provider, Address, DisplayName, Segments }` separa *qué* abre la
   navegación de *cómo* lo muestra; `Segments` alimenta las migas de pan.
3. `IItemProvider` es el único punto de contacto con medios de almacenamiento:
   `ProviderId`, `Capabilities` (`ProviderCapabilities` flags), `TryResolveLocation`,
   `EnumerateAsync` → lotes `EnumerationBatch`, `WatchAsync` → `FileChangeEvent`
   (incluye `Lost` para buffers desbordados).
4. La enumeración es un `IAsyncEnumerable` cancelable: primer `Started`, N
   `Progress` con ítems o `Error` por ítem, y `Complete` explícito.
   `NavigationSession` numera navegaciones con generaciones; los lotes de
   generaciones viejas se descartan.

## Consecuencias

- Selección, historial y ordenación sobreviven a renombrados externos cuando el
  FileID se resolvió; la UI nunca compara rutas para identidad.
- Cualquier medio futuro (nube, MTP, SFTP, archivos comprimidos) encaja como un
  proveedor más sin cambiar `Domain` ni la UI.
- El proveedor local es deliberadamente "tonto": no calcula tamaños de carpeta
  ni lee contenido para listar (presupuesto de rendimiento del plan).
- Limitación aceptada: FileID solo en NTFS/ReFS; en FAT/exFAT/red la identidad
  cae a ruta con `IsStable=false` (mismo comportamiento que Explorer).
