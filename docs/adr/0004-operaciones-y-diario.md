# ADR 0004 — Operaciones fiables y diario persistente

**Estado:** aceptado (2026-09-23)

## Contexto

Explorer pierde estado en copias largas: cancelar una copia deja un estado
ambiguo, un cierre abrupto no deja rastro y reintentar puede duplicar archivos.
El plan exige: diario persistente con intención, inicios, completados, fallos,
cancelaciones e incertidumbres; éxito solo tras verificar el resultado real;
reiniciar nunca repite ciegamente; papelera por defecto.

## Decisión

`OperationEngine` (libs/Operations) con estas reglas:

1. **Diario JSONL** en `%LOCALAPPDATA%\EasyFileExplorer\operations-journal.jsonl`.
   Un archivo append-only por sesión; etapas: `intent`, `item-started`,
   `item-done`, `item-failed`, `item-skipped`, `item-cancelled`, `completed`,
   `cancelled`, `uncertain`. JSONL y no SQLite: append barato, tolerante a
   truncado (se ignoran líneas corruptas al leer), legible en depuración.
   SQLite se reserva para `libs/Metadata` (etiquetas, favoritos, preferencias).
2. **Verificación por ítem**: `item-done` se escribe *después* de que la API
   retorne éxito y el estado esperado sea observable (p. ej. el destino existe).
   Un `item-started` sin cierre al siguiente arranque → `uncertain` y la UI lo
   muestra; nunca se re-ejecuta automáticamente.
3. **Conflictos**: `ConflictPolicy { AskEach, Skip, Replace, KeepBoth }` con
   `ApplyToRemaining`; `AskEach` invoca `ConflictCallback` (en UI: diálogo
   Reemplazar / Conservar ambos / Omitir / cancelar, con "aplicar a los
   restantes").
4. **Seguridad**: mover dentro del propio subárbol se rechaza; renombrado
   case-only permitido; move cross-volume = copia verificada + borrado del
   origen; papelera por defecto con `FileIO` del shell (`FOF_ALLOWUNDO` =
   `SendToRecycleBin`), permanente solo si `DeleteTarget.Permanent` y
   confirmación explícita.
5. **Control**: pausa entre ítems (no a mitad de un archivo), cancelación con
   `CancellationToken` → `item-cancelled`/`cancelled`, progreso por ítem y
   estimación por tamaño cuando se conoce.

## Consecuencias

- Una copia interrumpida deja un destino visible y un diario que explica qué
  faltó; el usuario decide reintentar, omitir o limpiar — el sistema nunca lo
  decide por él.
- El diario también alimenta "deshacer" (fase 3) y el centro de operaciones sin
  bloquear navegación.
- Limitación aceptada: `FileIO.FileSystem` de `Microsoft.VisualBasic` es la vía
  oficial a `SHFileOperation` con papelera; no hay alternativa administrada
  oficial. En UNC sin papelera, la app ofrece solo borrado permanente con
  confirmación doble (comportamiento de Explorer).
