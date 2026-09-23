# Backlog — primeras iteraciones (plan §13)

Ordenado por prioridad del plan (integridad → respuesta → compatibilidad →
eficiencia → amplitud). Estado al final de la fase 1.

| # | Ítem | Estado | Notas |
| --- | --- | --- | --- |
| 1 | Estructura del monorepo + solución + CI | hecho | `EasyFileExplorer.sln`, workflow build x64/ARM64 + tests |
| 2 | `IItemProvider` local: enumeración por lotes cancelable + generación | hecho | `LocalFileSystemProvider`, 21 tests |
| 3 | Ventana WinUI con pestañas, breadcrumb, lateral, vista de detalles | hecho | `apps/Explorer` |
| 4 | Operaciones: copiar/mover/renombrar/borrar con diario y conflictos | hecho | `OperationEngine` + diario JSONL + papelera |
| 5 | Selección por identidad estable + historial con selección | hecho | `ItemId` vol+FileID, `NavigationHistory` |
| 6 | Búsqueda contextual incremental | hecho | `LocalSearcher`; filtros avanzados en UI pendientes |
| 7 | Vista rápida (Espacio) + miniaturas bajo demanda | parcial | Texto/imagen/info; formatos avanzados (fase 4) |
| 8 | Tests unitarios del núcleo | hecho | Proveedor, navegación, operaciones, rutas |
| 9 | Docs fase 0: arquitectura, matriz de paridad, ADRs, CONTRIBUTING | hecho | Esta carpeta |
| 10 | Empaquetado MSIX + procesos aislados | plan (2–3) | `packaging/` preparado; hosts reservados |

## Siguientes candidatos (fase 2)

- Reconciliación de `uncertain` al arrancar + superficie en centro de operaciones.
- Botón de pausa/reanudar en operaciones; deshacer (Ctrl+Z).
- Persistencia de pestañas y preferencias (`libs/Metadata`, SQLite).
- `apps/OperationsHost` + `libs/Ipc` (named pipes, mensajes tipados).
- Vista de iconos/galería real y redimensionado de columnas.
