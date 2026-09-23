# Empaquetado

## Estado actual

La aplicación se distribuye **sin empaquetar** con `WindowsAppSDKSelfContained`
(recorrido vertical): el binario de `dotnet publish -r win-x64` corre en
cualquier Windows 10 1809+ sin Windows App Runtime instalado.

## MSIX (fase 2+)

Para publicar como MSIX (requisito: Visual Studio 2022 + Windows SDK
10.0.22621+ en la máquina de build):

1. En `apps/Explorer/Explorer.csproj` cambiar `<WindowsPackageType>None</WindowsPackageType>`
   a `<WindowsPackageType>MSIX</WindowsPackageType>` y quitar
   `WindowsAppSDKSelfContained` (o crear un perfil `Package` separado).
2. Incluir `Package.appxmanifest` de este directorio en el proyecto y añadir
   los assets en `apps/Explorer/Assets/` (iconos tiles, splash).
3. `msbuild /p:Configuration=Release /p:Platform=x64 /p:AppxPackageDir=…/out/`
   genera `EasyFileExplorer_<ver>_x64.msix` + dependencias de Windows App SDK.
4. Firmar con el certificado de publicación (`signtool sign /fd SHA256`).

`Package.appxmanifest` declara identidad, arquitecturas x64+arm64 y las
capacidades mínimas (`runFullTrust` para el modelo de aplicación de
escritorio sin empaquetar-en-sandbox que necesita un explorador de archivos).
