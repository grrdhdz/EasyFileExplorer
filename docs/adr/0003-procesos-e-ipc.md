# ADR 0003 — Procesos aislados e IPC (planificado, fases 2–3)

**Estado:** propuesto (contratos ya preparados; implementación en fases 2–3)

## Contexto

El plan exige que una operación destructiva o un decodificador de vista previa
defectuoso nunca puedan tumbar la ventana principal ni bloquear la navegación.
También exige elevación puntuada (UAC por operación, no por proceso) y
aislamiento de extensiones del shell.

## Decisión

Modelo multiproceso con mensajes tipados sobre named pipes (`libs/Ipc`):

| Proceso | Responsabilidad | Estado |
| --- | --- | --- |
| `apps/Explorer` | UI, navegación, coordinación. Nunca ejecuta mutaciones de sistema de archivos ni decodificadores. | hecho (hoy sí las ejecuta en-proceso; migración transparente) |
| `apps/OperationsHost` | Ejecuta `OperationRequest` con el diario; reporta progreso por ítem. | reservado |
| `apps/PreviewHost` | Decodifica vistas previas y miniaturas en proceso sacrificable. | reservado |
| `apps/ShellHost` | Aloja extensiones del shell (menús contextuales de terceros, columnas). | reservado |
| `apps/ElevationBroker` | Recibe una solicitud ya autorizada, eleva por operación con consentimiento del usuario. | reservado |

Reglas:

1. Los mensajes son serializables; `OperationRequest` ya lo es salvo
   `ConflictCallback` (se serializa como `AskEach` + canal de respuesta).
2. La UI nunca confía en el resultado: tras `item-done` el host re-verifica el
   estado real del elemento antes de escribirlo en el diario.
3. Un host caído no pierde datos: el diario registra `item-started` sin cierre
   → `uncertain`, y el siguiente arranque reconcilia.

## Consecuencias

- La migración a procesos no cambia `Domain` ni la UI: solo el transporte de
  `OperationEngine`/`PreviewService`.
- Mientras no existan los hosts, el engine corre in-proceso con la misma API; el
  diario se escribe igual, por lo que la reconciliación post-cierre ya funciona.
- La decisión de named pipes sobre sockets locales: sin puertos, ACL por
  SID de usuario, mismo mecanismo que Explorer para shell integration.
