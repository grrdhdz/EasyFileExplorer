# ADR 0001 — Stack: WinUI 3 con C# (núcleo migrable a C++/WinRT)

**Estado:** aceptado (2026-09-23)

## Contexto

El plan recomienda C++/WinRT como stack principal y lista C# + WinUI 3 como
alternativa válida. El entorno de desarrollo actual carece de toolchain C++
(Visual Studio Build Tools), mientras que `dotnet` 8 y Windows App SDK 2.5.1
están disponibles y generan binarios autocontenidos (`WindowsAppSDKSelfContained`)
para x64 y ARM64 sin empaquetar.

## Decisión

Implementar el recorrido vertical y las primeras fases en **C# / .NET 8 /
WinUI 3 / Windows App SDK autocontenido**. Los límites de `libs/` (contratos
puros en `Domain`, proveedores, motor de operaciones) se mantienen libres de
dependencias de UI para que el núcleo pueda migrarse incrementalmente a
C++/WinRT si el rendimiento de la capa de sistema de archivos lo exige
(enumeración de >1 M de ítems, presión de GC con miniaturas).

## Consecuencias

- **Positivas:** velocidad de iteración, ecosistema de tests (xunit,
  `TestHost`), `IAsyncEnumerable` para lotes cancelables, gestión de memoria
  segura por defecto.
- **Negativas:** overhead por llamada P/Invoke frente a C++/WinRT; GC visible en
  listados masivos; acceso a APIs shell muy recientes puede requerir shims.
- **Mitigación:** identidades, enumeración y operaciones ya van por Win32
  (`GetFileInformationByHandleEx`, `CreateFile`, `FileIO` del shell); los
  cuellos de botón quedan aislados tras `IItemProvider`/`OperationEngine` y son
  reemplazables sin tocar la UI. La decisión se reevalúa en fase 4 con datos de
  `benchmarks/`.
