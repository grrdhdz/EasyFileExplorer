# Contribuir

## Entorno

- Windows 10 1809+ (objetivo: Windows 11).
- .NET SDK 8 (`dotnet --version` ≥ 8.0.400).
- Todo lo demás viene por NuGet (CPM en `Directory.Packages.props`); para MSIX,
  Visual Studio 2022 + Windows SDK.

## Flujo

1. `dotnet build EasyFileExplorer.sln -c Debug`
2. `dotnet test tests/unit/EasyFileExplorer.UnitTests.csproj`
3. Rama `feat/…` o `fix/…`, commit en español o inglés consistente con el repo,
   PR contra `main` con CI verde.

## Reglas del proyecto

- **`libs/` no conoce la UI.** Nada de `Microsoft.UI.*`, `Windows.Storage` ni
  `System.IO` directo fuera de las capas de adaptación (`FileSystem`,
  `Operations`, servicios del shell en `apps/Explorer/Services`).
- **La UI nunca toca `System.IO` directamente.** Lista y muta mediante
  `IItemProvider`/`OperationEngine`.
- **Cancelación por generación**, no por `bool` ad hoc: cualquier trabajo
  asíncrono de una pestaña va ligado a `NavigationSession`.
- **Nada de éxito sin verificación**: una operación no se marca `done` hasta
  comprobar el resultado real; los inciertos quedan en el diario.
- **Papelera por defecto**: borrado permanente solo con opt-in explícito del
  usuario.
- **Rendimiento**: no leer contenido ni calcular tamaños recursivos para
  listar; miniaturas y contenido bajo demanda.
- **Tests**: todo comportamiento de `libs/` con lógica no trivial necesita test
  unitario en `tests/unit`. Fixtures con temporales reales, sin mocks del
  filesystem.

## Estilo

- C# moderno (`LangVersion latest`, `Nullable` habilitado). Tipos de dominio
  como `record` inmutable; servicios como `sealed`.
- Comentarios en español, explicando *por qué*, no *qué*.
- xunit + `[Fact]`; nombres de test en inglés descriptivos.
