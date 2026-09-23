# Matriz de paridad — Explorer / Finder

Estado: `hecho` (recorrido vertical), `parcial`, `plan` (fase indicada entre
paréntesis), `—` (no aplica). Limitaciones conocidas en la última columna.

## 1. Navegación

| ID | Comportamiento | Estado | Limitación actual |
| --- | --- | --- | --- |
| NAV-01 | Abrir carpeta al doble-clic / Enter | hecho | — |
| NAV-02 | Atrás / Adelante por pestaña | hecho | Selección restaurada por historial |
| NAV-03 | Subir (Alt+↑ / botón) | hecho | — |
| NAV-04 | Ir al inicio / Este equipo | hecho | Botón Home → perfil del usuario |
| NAV-05 | Migas de pan por segmento, clicables | hecho | — |
| NAV-06 | Barra de dirección editable (Ctrl+L) | hecho | Sin autocompletado aún (fase 3) |
| NAV-07 | Pestañas múltiples (Ctrl+T/Ctrl+W/botón +) | hecho | Sin arrastrar entre ventanas (fase 4) |
| NAV-08 | Historial con selección guardada por entrada | hecho | — |
| NAV-09 | Panel lateral: acceso rápido + unidades | hecho | Sin reordenar ni anclar (fase 3) |
| NAV-10 | Actualización automática ante cambios externos | hecho | `Lost` → re-enumeración completa |
| NAV-11 | Restaurar sesión/pestañas al abrir | plan (3) | Requiere persistencia (SQLite) |

## 2. Vista y presentación

| ID | Comportamiento | Estado | Limitación actual |
| --- | --- | --- | --- |
| VIS-01 | Vista de detalles (nombre/fecha/tipo/tamaño) | hecho | Anchos fijos; no redimensionables aún |
| VIS-02 | Orden por columna, carpetas primero, natural sort | hecho | Windows `StrCmpLogicalW` |
| VIS-03 | Vista de iconos / galería | parcial | Miniaturas integradas en lista; vista galería (fase 4) |
| VIS-04 | Miniaturas de imagen bajo demanda | hecho | Solo formatos imagen; sin cache persistente |
| VIS-05 | Panel de vista previa | plan (4) | Vista rápida (Espacio) disponible |
| VIS-06 | Vista rápida tipo Quick Look (Espacio) | parcial | Texto ≤64 KB, imágenes, diálogo info; sin video/audio/fuentes |
| VIS-07 | Vista en columnas (Finder) | plan (4) | — |
| VIS-08 | Tema claro/oscuro/contraste | parcial | Hereda tema de Windows; HC sin validar (fase 4) |
| VIS-09 | Mostrar elementos ocultos/sistema | parcial | `EnumerateOptions` lo soporta; sin toggle UI aún |
| VIS-10 | Etiquetas de color (Finder) | plan (5) | Requiere `libs/Metadata` (SQLite) |

## 3. Selección

| ID | Comportamiento | Estado | Limitación actual |
| --- | --- | --- | --- |
| SEL-01 | Selección extendida (Ctrl/Shift/marquee) | hecho | Marquee nativo del `ListView` |
| SEL-02 | Selección por identidad estable | hecho | `ItemId` vol+FileID en NTFS |
| SEL-03 | Seleccionar todo (Ctrl+A) | hecho | — |
| SEL-04 | Invertir selección | plan (3) | — |
| SEL-05 | Selección sobrevive a renombrado externo | parcial | Solo si FileID se resolvió |

## 4. Operaciones de archivos

| ID | Comportamiento | Estado | Limitación actual |
| --- | --- | --- | --- |
| OPS-01 | Copiar (Ctrl+C/V, menú, DnD) | hecho | DnD entre pestañas/ventanas externas: entrada sí, salida parcial |
| OPS-02 | Mover (Ctrl+X/V, DnD) | hecho | Cross-volume = copia verificada + borrado |
| OPS-03 | Renombrar (F2) | hecho | Case-only permitido |
| OPS-04 | Borrar a papelera (Delete) | hecho | `FileIO` del shell, `FOF_ALLOWUNDO` |
| OPS-05 | Borrado permanente (Shift+Delete, confirmado) | hecho | — |
| OPS-06 | Nueva carpeta (Ctrl+N en el plan = Ctrl+Shift+N impl.) | hecho | Atajo: botón/menú (Ctrl+Shift+N pendiente) |
| OPS-07 | Conflictos: reemplazar/conservar ambos/omitir | hecho | Diálogo con "aplicar a los restantes" |
| OPS-08 | Diario persistente + estados inciertos | hecho | JSONL; recuperación post-cierre en arranque (fase 2) |
| OPS-09 | Pausar / reanudar / cancelar operación | parcial | Engine soporta pausa; UI aún sin botón |
| OPS-10 | Mover carpeta dentro de su subárbol → rechazo | hecho | — |
| OPS-11 | Progreso por ítem y total | hecho | `OperationProgress`; centro de operaciones |
| OPS-12 | Deshacer (Ctrl+Z) | plan (3) | Requiere diario bidireccional |
| OPS-13 | Verificación post-operación por ítem | hecho | `item-done` solo tras resultado real |
| OPS-14 | Renombrado por lotes (Finder) | plan (5) | — |
| OPS-15 | Copiar ruta al portapapeles | hecho | Menú contextual |
| OPS-16 | Abrir con… / Propiedades del shell | hecho | `rundll32 OpenAs_RunDLL`, `explorer /select` |

## 5. Búsqueda

| ID | Comportamiento | Estado | Limitación actual |
| --- | --- | --- | --- |
| BUS-01 | Búsqueda contextual en carpeta actual | hecho | Recursiva DFS, lotes de 64 |
| BUS-02 | Filtros: nombre, extensión, tamaño, fecha | parcial | `SearchQuery` los modela; UI solo nombre |
| BUS-03 | Resultados incrementales y cancelables | hecho | Misma generación que navegación |
| BUS-04 | Carpetas inteligentes (Finder) | plan (5) | Requiere `libs/Search` + persistencia |
| BUS-05 | Índice propio | plan (6) | Sin índice; DFS puro |

## 6. Integridad, seguridad y rendimiento

| ID | Comportamiento | Estado | Limitación actual |
| --- | --- | --- | --- |
| INT-01 | Sin lectura recursiva para listar | hecho | — |
| INT-02 | Enumeración cancelable por lotes | hecho | — |
| INT-03 | Resultados obsoletos descartados (generación) | hecho | Navegación, búsqueda, miniaturas |
| INT-04 | Errores por ítem no abortan el lote | hecho | `EnumerationError` con `ItemPath` |
| INT-05 | Sin buffer overflow del observador | hecho | 64 KB + `Lost` |
| SEG-01 | No seguir junctions/symlinks en mutación | hecho | `OPEN_REPARSE_POINT` + `IsReparsePoint` |
| SEG-02 | Papelera por defecto | hecho | — |
| SEG-03 | Elevación puntuada | plan (3) | `ElevationBroker` reservado |
| SEG-04 | Procesos aislados para operaciones/vista previa | plan (3) | Engine in-proceso hoy |
| PER-01 | Primer lote < 200 ms en carpeta típica | parcial | Medición manual; benchmarks (fase 6) |
| PER-02 | ARM64 nativo | parcial | RID `win-arm64` declarado; CI lo construye, sin hardware para validar |
