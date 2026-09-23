# EasyFileExplorer

Explorador de archivos nativo para Windows 11 (x64 y ARM64), escrito con WinUI 3,
que combina la paridad funcional de Explorador de archivos de Windows con las
capacidades de productividad de Finder (vista rápida, columnas, etiquetas,
carpetas inteligentes, renombrado por lotes, vista galería).

Prioridades del producto (en orden): **integridad y seguridad → capacidad de
respuesta → compatibilidad → eficiencia → amplitud funcional**.

## Estado actual

Recorrido vertical completo (fases 0–1 del [plan](docs/plan.md)):

- Ventana WinUI 3 nativa con pestañas, panel lateral y vista de detalles virtualizada.
- Enumeración por lotes cancelable con generación de navegación (los resultados
  obsoletos se descartan).
- Navegación atrás/adelante/subir, migas de pan editables, selección por
  identidad estable.
- Operaciones de archivos con diario persistente (JSONL), papelera por defecto,
  políticas de conflicto (reemplazar / conservar ambos / omitir), pausa y
  cancelación, y rechazo de mover una carpeta dentro de su propio subárbol.
- Búsqueda incremental, atajos de teclado principales, vista rápida (Espacio),
  miniaturas bajo demanda, arrastrar y soltar, portapapeles (Ctrl+C/X/V),
  renombrado (F2), borrado (Delete / Shift+Delete), propiedades y "abrir con".

La matriz de paridad con Explorer y Finder está en
[docs/matriz-paridad.md](docs/matriz-paridad.md).

## Requisitos

- Windows 10 1809+ (objetivo: Windows 11). SDK de .NET 8.
- Windows App SDK 2.5.1 (autocontenido; no requiere runtime instalado).
- Para compilar MSIX: Visual Studio 2022 con Windows SDK 10.0.22621+.

## Compilar y ejecutar

```powershell
dotnet build EasyFileExplorer.sln -c Release
dotnet build apps/Explorer/Explorer.csproj -c Release -r win-x64   # binario autocontenido
dotnet test tests/unit/EasyFileExplorer.UnitTests.csproj -c Release
.\apps\Explorer\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\EasyFileExplorer.exe
```

Para ARM64, usar `-r win-arm64`. La aplicación se distribuye sin empaquetar
(`WindowsAppSDKSelfContained`) en la fase actual; el empaquetado MSIX está
preparado en [packaging/](packaging/README.md) para fases siguientes.

## Estructura del monorepo

| Ruta | Contenido |
| --- | --- |
| `apps/Explorer` | Aplicación WinUI 3 (UI, viewmodels, servicios de portapapeles/miniaturas). |
| `libs/Domain` | Contratos puros: `ItemId`, `Location`, `IItemProvider`, lotes, operaciones, búsqueda, vista previa. |
| `libs/FileSystem` | Proveedor local NTFS/ReFS, observador de cambios, identidad estable, buscador, ubicaciones conocidas. |
| `libs/Navigation` | Sesión de navegación con generaciones, historial, selección, pestaña. |
| `libs/Operations` | Motor de operaciones con diario, papelera, seguridad de rutas, conflictos. |
| `tests/unit` | Tests unitarios del núcleo (xunit). |
| `docs` | Arquitectura, ADR, matriz de paridad, backlog. |
| `packaging`, `build`, `benchmarks` | Empaquetado MSIX, scripts, microbenchmarks (fases siguientes). |
| `tests/{integration,ui,reliability,security,performance}` | Suites planificadas por fase. |

Las futuras piezas del plan (processes aislados `OperationsHost`, `PreviewHost`,
`ShellHost`, `ElevationBroker` y librerías `Search`, `Metadata`, `Ipc`,
`DesignSystem`, `Diagnostics`) tienen su lugar reservado en el contrato; el plan
de encaje está en [docs/arquitectura.md](docs/arquitectura.md).

## Contribuir

Ver [CONTRIBUTING.md](CONTRIBUTING.md).
