# EasyFileExplorer — propuesta de producto y plan de desarrollo

Fecha: 22 de septiembre de 2026. Estado: propuesta para revisión; no es una implementación ni una especificación aprobada.

## 1. Objetivo y decisiones de partida

Crear un explorador de archivos para **Windows 11 x64 y ARM64**, con interfaz **WinUI 3 nativa**, diseño Fluent actual y prioridad en rapidez, consumo contenido, integridad de los archivos y estabilidad. La plataforma está confirmada por el usuario. El directorio del proyecto está vacío al preparar este documento.

El objetivo funcional es reunir las capacidades del Explorador de Windows y las ventajas de Finder aplicables a Windows. La paridad será una matriz de comportamientos y pruebas, no una promesa indefinida de «todas las funciones» ni una reproducción de la interfaz de Apple.

Orden de prioridad: **integridad y seguridad → capacidad de respuesta → compatibilidad → eficiencia → amplitud funcional**. Una optimización que pierda archivos o altere silenciosamente su contenido no se acepta.

La primera versión convivirá con Explorer. Sustituir el escritorio, la barra de tareas o los diálogos Abrir/Guardar del sistema queda fuera del producto inicial. Las integraciones para abrir carpetas con EasyFileExplorer se evaluarán después, con instalación y reversión explícitas.

WinUI usa Fluent y permite aplicaciones con C++ y C#. La actualización oficial del 27–28 de agosto de 2026 sitúa su desarrollo principal en GitHub; el mismo anuncio todavía distingue ese hito de la apertura general de contribuciones externas. Adoptaremos distribuciones estables del framework, sin depender de compilar su rama principal. [WinUI](https://github.com/microsoft/microsoft-ui-xaml), [estado de apertura](https://github.com/microsoft/microsoft-ui-xaml/discussions/10700).

## 2. Alternativas tecnológicas

| Alternativa | Ventajas | Costes y límites | Decisión propuesta |
|---|---|---|---|
| C++/WinRT + WinUI 3; núcleo C++ moderno | Integración directa con Win32/COM; control de memoria y del trabajo por frame; un lenguaje principal | Mayor coste de desarrollo; errores de memoria y concurrencia requieren controles estrictos | **Recomendada** para la prioridad nativa y el control del sistema |
| C# + WinUI 3; adaptadores nativos donde se necesiten | Desarrollo más rápido, gestión de memoria más segura y ecosistema amplio | Asignaciones, GC e interoperabilidad necesitan medición; no implica ser lento | Alternativa válida si el equipo domina .NET |
| C++/WinRT + WinUI 3; núcleo Rust | Seguridad de memoria en buena parte del núcleo | Dos toolchains, FFI y más complejidad de depuración y empaquetado | Reservada para un equipo con experiencia real en ambos |

WinUI en C# también es una interfaz nativa. La recomendación C++ no constituye una garantía de mayor rendimiento: el primer prototipo debe demostrar que el diseño cumple los presupuestos antes de ampliar funcionalidades.

Base propuesta: C++20, XAML, C++/WinRT, Win32/COM, SQLite para datos propios y MSIX para distribución. MSBuild organiza la solución Windows; no se necesita un orquestador de monorepo de JavaScript. Las dependencias se fijarán y restaurarán de forma reproducible.

Usar el canal Stable de Windows App SDK. La página oficial consultada lista 2.5.1, publicada el 16 de septiembre de 2026; verificar el identificador concreto del paquete y compatibilidad del toolchain al crear la solución. Las APIs experimentales no serán requisitos del producto. [Canales oficiales](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels).

## 3. Experiencia y diseño

Aplicar los estilos Fluent vigentes que distribuya WinUI estable, con controles nativos y recursos de tema. «Nuevo lenguaje de diseño» significa seguir la guía Windows vigente y la WinUI Gallery, no inventar un tema web parecido ni depender de propuestas aún experimentales. [Guía de diseño Windows](https://learn.microsoft.com/en-us/windows/apps/design/), [WinUI Gallery](https://github.com/microsoft/WinUI-Gallery).

- Barra superior con pestañas, atrás/adelante/subir, ruta editable con breadcrumbs y búsqueda contextual.
- Panel lateral con ubicaciones conocidas, favoritos, unidades, red, etiquetas y colecciones inteligentes.
- Área central con detalles, lista, iconos, columnas y galería; estado de selección y orden independiente por pestaña.
- Panel de detalles/vista previa y centro de operaciones que no impidan seguir navegando.
- Tema claro, oscuro y alto contraste; tipografía y símbolos del sistema; Mica cuando proceda y fondo sólido cuando el sistema lo requiera.
- Densidad compacta y cómoda; columnas configurables; preferencias por carpeta sin generar archivos ocultos dentro de carpetas del usuario.
- Teclado completo, Narrator/UI Automation, escalado de texto y DPI, multimonitor, foco visible y movimiento reducido.
- Atajos conocidos: Ctrl+C/X/V/Z, F2, Delete, Shift+Delete, Alt+arriba, Alt+izquierda/derecha, Ctrl+L, Ctrl+F y Ctrl+T. Espacio abre la vista rápida fuera de campos de edición.
- Ninguna animación retrasa la ejecución de una acción. Mostrar errores junto al contexto afectado y ofrecer reintentar, omitir o cancelar cuando corresponda.

Las vistas avanzadas reutilizarán selección, navegación y comandos comunes. Evitar implementar cinco exploradores diferentes dentro del mismo programa.

## 4. Inventario de paridad con Explorer

Se fijarán versiones de referencia de Windows y macOS durante la fase 0. Cada fila tendrá identificador, comportamiento esperado, proveedor compatible, prueba de aceptación, fase, estado y limitaciones. Un control visual parecido no cuenta como paridad si faltan sus operaciones.

| Área | Alcance objetivo | Entrega |
|---|---|---|
| Navegación | Rutas locales/UNC, historial, subir, favoritos, carpetas conocidas, unidades, pestañas y ventanas | MVP |
| Listado | Detalles/lista/iconos, selección múltiple, rangos, ordenar, agrupar, filtros, extensiones y ocultos | MVP |
| Operaciones | Crear, copiar, cortar, pegar, mover, renombrar, duplicar, borrar, papelera y restaurar | MVP |
| Conflictos | Omitir, reemplazar, conservar ambos, aplicar decisión a un lote; errores por elemento | MVP |
| Interoperabilidad | Portapapeles y arrastrar/soltar con otras aplicaciones, abrir, abrir con, propiedades y accesos directos | MVP; ampliar compatibilidad en beta |
| Búsqueda | Nombre, extensión, tipo, tamaño, fechas; contenido indexado y ubicación de resultados | Básica en MVP; completa en beta |
| Visualización | Miniaturas, panel de detalles, propiedades bajo demanda y vistas recordadas | MVP; ampliar formatos en beta |
| Shell | Menús contextuales, verbos de aplicaciones, papelera, bibliotecas y ubicaciones virtuales | Básico en MVP; matriz extensa en beta |
| Almacenamiento | NTFS, exFAT/FAT32, solo lectura, extraíbles, SMB y dispositivos desconectados | Local en MVP; red/extraíbles en beta |
| Nube | Carpetas sincronizadas, estado del proveedor, placeholders, descarga bajo demanda | Beta |
| Archivo comprimido | Navegar y extraer ZIP; crear ZIP; otros formatos según bibliotecas y licencias verificadas | ZIP en beta; ampliar después |
| Sistema de archivos avanzado | Rutas largas, Unicode, enlaces, junctions, streams alternativos, archivos sparse y atributos | Casos de seguridad desde MVP; paridad en beta |
| Integración del sistema | Compartir, terminal en ubicación, expulsar dispositivos y abrir herramientas de Windows | Beta |
| Capacidades especiales | WSL, MTP/teléfonos, permisos avanzados, EFS, herramientas de unidad y versiones anteriores | Fase de compatibilidad; delegación al sistema cuando proceda |
| Recuperación | Deshacer/rehacer soportado, estado de operaciones, sesiones y pestañas tras reinicio | Parcial en MVP; endurecimiento en beta |

No se supondrá que todos los elementos son rutas de disco. Papelera, resultados, dispositivos y proveedores virtuales requieren identidades y capacidades propias. Para acciones complejas del sistema se podrá abrir la herramienta nativa correspondiente, dejando claro cuándo se delega.

## 5. Capacidades inspiradas en Finder

Apple documenta vistas de columnas y galería, etiquetas, carpetas inteligentes y acciones rápidas. Windows ya posee capacidades relacionadas —por ejemplo, búsqueda y metadatos—, por lo que verificaremos las diferencias en la experiencia completa antes de afirmar que una función «no existe en Windows». [Organización en Finder](https://support.apple.com/guide/mac-help/organize-your-files-in-the-finder-mchle9f0a1b2/mac), [búsqueda en Windows](https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/find-your-files-and-apps-in-windows).

| Capacidad | Implementación propuesta en Windows | Entrega |
|---|---|---|
| Vista rápida con Espacio | Ventana inmediata con navegación entre elementos; imágenes, texto y PDF primero, audio/vídeo después | MVP básico; ampliar en beta |
| Vista por columnas | Jerarquía horizontal con selección y carga independiente por columna | Beta |
| Galería | Vista grande, tira de elementos, metadatos y acciones relevantes | Beta |
| Árbol dentro de la lista | Expandir subcarpetas sin abandonar la ubicación; virtualización de filas anidadas | Beta |
| Etiquetas universales de colores | Varias etiquetas por archivo o carpeta, filtros y acceso lateral | Beta |
| Carpetas inteligentes | Consultas guardadas por nombre, tipo, fechas, ubicación y etiquetas; archivos en su ubicación original | Beta |
| Renombrado por lotes | Sustituir/agregar texto, numeración, formato, vista previa y detección de colisiones | Beta |
| Nueva carpeta con selección | Crear carpeta y mover elementos con informe de resultados parciales | Beta |
| Arrastre con apertura de carpetas | Abrir una carpeta tras mantener el arrastre encima, con cancelación y retorno predecibles | Beta |
| Acciones rápidas | Rotar/convertir imágenes, crear PDF, marcar documentos y recortar medios, según formato | Acciones básicas en beta; edición avanzada después |
| Inspección múltiple | Resumen de selección, tamaño calculado bajo demanda y navegación de vistas previas | Beta |
| Automatizaciones/servicios | Acciones explícitas sobre selección con permisos, argumentos estructurados y sin ejecución automática | Posterior |
| Funciones de escritorio | Agrupación equivalente a Stacks como colecciones dentro de la app | Posterior; no sustituye el escritorio |

Vista rápida y renombrado por lotes tendrán comportamientos comprobables basados en las guías de Apple. [Quick Look](https://support.apple.com/en-my/guide/mac-help/mh14119/mac), [renombrado múltiple](https://support.apple.com/en-nz/guide/mac-help/mchlp1144/mac).

AirDrop, Handoff, sincronización de iPhone, Time Machine, iCloud y clonación APFS dependen de servicios o del sistema de Apple. Registrar cada caso: equivalente Windows cuando exista —compartir, proveedor de sincronización o versiones anteriores—, integración externa o no aplicable. No prometer compatibilidad con protocolos privados ni copias instantáneas en sistemas de archivos que no lo soporten.

Las capacidades pendientes seguirán visibles en la matriz. La versión 1.0 no se anunciará como paridad total si conserva filas sin implementar o sin equivalencia validada.

## 6. Arquitectura del monorepo

```text
EasyFileExplorer/
  EasyFileExplorer.sln
  apps/
    Explorer/                 # Ventana WinUI, XAML y modelos de presentación
    OperationsHost/           # Operaciones persistentes fuera del proceso de UI
    PreviewHost/              # Decodificación y vistas previas aisladas
    ShellHost/                # Integración Shell/COM y extensiones compatibles
    ElevationBroker/          # Operaciones concretas que requieren UAC
  libs/
    Domain/                   # Identidades, capacidades, comandos y resultados
    Navigation/               # Sesiones, pestañas, selección e historial
    FileSystem/               # Enumeración, cambios y metadatos mínimos
    Operations/               # Planificación, conflictos y diario de operaciones
    Providers/                # Local, Shell, red, archivos y colecciones
    Search/                   # Adaptadores de búsqueda y consultas
    Metadata/                 # Etiquetas, preferencias y persistencia
    Ipc/                      # Contratos versionados y validación de mensajes
    DesignSystem/             # Recursos, estilos y controles compartidos
    Diagnostics/              # Eventos, mediciones y diagnósticos locales
  tests/
    unit/
    integration/
    ui/
    reliability/
    security/
    performance/
  benchmarks/                 # Generadores, escenarios y resultados comparables
  packaging/                  # MSIX x64/ARM64, manifiestos y actualización
  build/                      # Configuración y scripts reproducibles
  docs/                       # Arquitectura, matriz, ADR y guía de contribución
  .github/workflows/          # CI propuesta; adaptar al proveedor elegido
```

El árbol describe responsabilidades finales; no exige crear proyectos vacíos para todo desde el primer día. La primera solución incorporará solo las piezas necesarias para un recorrido vertical completo.

Dependencias: la UI usa casos de uso y contratos; el dominio no conoce XAML, SQLite ni detalles COM. Los adaptadores concretos implementan capacidades. Los procesos auxiliares se inician bajo demanda y tienen límites de memoria/concurrencia; no habrá uno por archivo o pestaña.

Contratos iniciales:

- `Location`/`ItemId`: proveedor, identidad estable cuando exista y representación de ubicación. La ruta no será el único identificador universal.
- `Enumerate`: lotes cancelables con generación de navegación, estado parcial y finalización explícita.
- `Capabilities`: leer, escribir, renombrar, papelera, previsualizar, vigilar, deshacer y otras operaciones por proveedor.
- `OperationRequest`/`OperationResult`: identificador único, origen/destino, política de conflictos, progreso y resultado por elemento.
- `PreviewRequest`: identidad, versión, formato y límites; respuesta de contenido validado o error seguro.
- `SearchQuery`: ámbito, filtros, orden, estado del índice y cancelación; distinguir resultados parciales de completos.

### Flujo de navegación

1. Navegar crea una nueva generación y cancela la anterior.
2. El proveedor enumera fuera del hilo de UI y entrega metadatos básicos en lotes.
3. La UI presenta las primeras filas utilizables y actualiza en lotes pequeños.
4. Iconos, miniaturas y propiedades costosas se solicitan solo para elementos visibles y una zona cercana acotada.
5. Las respuestas antiguas se descartan; un resultado de la carpeta anterior no puede aparecer en la nueva.
6. Los cambios externos actualizan el modelo. Si se pierden eventos, se reconcilia con una enumeración nueva y se conserva selección por identidad.

Virtualizar controles no basta: también se limitarán modelos detallados, metadatos, imágenes, colas y cachés. Ordenar globalmente una carpeta no enumerada requiere trabajo adicional; la interfaz distinguirá la vista progresiva del orden final sin fingir que ha terminado. Las especificaciones de WinUI describen virtualización y acceso indexado, pero el control elegido debe validarse en la versión estable concreta. [ItemsView](https://github.com/microsoft/microsoft-ui-xaml/blob/main/docs/design-notes/ItemsView_spec.md).

### Procesos y extensiones

La UI no cargará decodificadores o extensiones arbitrarias en el camino de navegación. Los proveedores de lectura potencialmente bloqueantes se ejecutarán en workers y, cuando no admitan cancelación fiable, en procesos reiniciables. Separar procesos mejora la contención de fallos; **no equivale por sí solo a sandboxing**.

PreviewHost tendrá permisos mínimos, acceso limitado al archivo solicitado y bloqueo de red cuando sea técnicamente viable. ShellHost puede necesitar compatibilidad más amplia: se documentará esa frontera de confianza. Los menús COM, objetos OLE y extensiones con supuestos de proceso requieren un prototipo de interoperabilidad; no se prometerá trasladar cualquier DLL a otro proceso sin cambios.

En ARM64, priorizar componentes nativos. La compatibilidad con extensiones x64 de terceros se evaluará con un host separado compatible; cada extensión no soportada tendrá salida controlada. Los binarios de distinta arquitectura no se cargarán dentro del mismo proceso.

## 7. Rendimiento como requisito de entrega

Los valores siguientes son **presupuestos iniciales propuestos**, no resultados obtenidos. Se validarán en la fase 1. Publicar hardware, versión del SO, compilación Release, antivirus activo, estado del índice y cachés; medir x64 y ARM64 por separado.

Referencia inicial: SSD NVMe, 16 GB de RAM y equipo de gama media de cada arquitectura. Añadir 8 GB, HDD, USB, SMB con latencia/pérdida y nube sin hidratar como escenarios diferenciados.

| Métrica | Objetivo inicial | Condiciones |
|---|---|---|
| Inicio hasta primera ventana local utilizable | p95 ≤ 1 s en caliente; ≤ 2 s en frío | Incluye listado inicial; no solo ventana vacía |
| Navegar a carpeta de 10.000 elementos | Primeras 100 filas p95 ≤ 150 ms en caliente; ≤ 500 ms en frío | SSD local; nombre/tipo/atributos básicos, sin esperar miniaturas |
| Carpeta de 100.000 elementos | Primeras filas p95 ≤ 300 ms en caliente; ≤ 1 s en frío | Medir aparte enumeración y ordenación completas |
| Respuesta al teclado/clic | p95 ≤ 50 ms; p99 ≤ 100 ms | Durante enumeración, búsqueda y copias |
| Desplazamiento | p95 de frame ≤ 16,7 ms y p99 ≤ 33,3 ms | Equipo de referencia a 60 Hz |
| Búsqueda de nombres indexada | Primeros resultados p95 ≤ 200 ms | Corpus e índice declarados; no incluye indexación inicial |
| RAM del conjunto de procesos | ≤ 200 MB en reposo; ≤ 400 MB navegando 100.000 elementos | Medir private bytes y working set; caché de imágenes acotada |
| CPU en reposo | Promedio < 0,5 % del total de máquina durante 60 s | Ventana quieta, sin indexación ni operaciones pendientes |
| Transferencia local | Sobrecoste ≤ 10 % frente a la referencia equivalente | Mismos archivos, políticas, volumen y seguridad |

Protocolos: al menos 30 repeticiones por escenario; p50/p95/p99, dispersión y trazas de los casos lentos. Distinguir inicio de proceso frío, caché del SO fría y caché propia fría. No comparar nuestra caché caliente con Explorer frío. Medir asimismo tiempo total de enumeración, ordenación y búsqueda, bytes leídos, uso de disco, energía y latencia de cancelación.

La versión de referencia de Explorer quedará registrada. Como objetivo comparativo, buscar al menos 2× de mejora en los escenarios donde se reproduzca lentitud; si no hay mejora medible, no anunciarla. Las carpetas de red/nube no tendrán el mismo SLA de lectura que el SSD: sí deben conservar la respuesta de UI y permitir abandonar la ubicación.

Medidas de diseño: carga diferida, actualización por lotes, prioridades para el viewport, límites de concurrencia por dispositivo, cachés con presupuesto e invalidación y ausencia de escaneos globales al arrancar. No calcular tamaños recursivos ni leer contenido de todos los archivos para mostrar una carpeta.

Una navegación normal no descargará archivos solo en la nube para crear miniaturas. La lectura de contenido remoto se hará bajo demanda y con estado visible. Los procesos de indexación y vista previa cederán recursos durante interacción y operaciones prioritarias.

## 8. Operaciones fiables y recuperación

Usar inicialmente `IFileOperation` mediante un adaptador en un hilo COM STA dentro de OperationsHost. Permite operaciones del Shell y notificaciones de progreso; hay que comprobar resultados por elemento y operaciones abortadas, además del resultado global. [Referencia oficial](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileoperation).

- Diario persistente con intención, elementos iniciados, completados, fallidos, cancelados y estado incierto tras un cierre abrupto.
- La app solo declara éxito después de comprobar el resultado real; no al aceptar una tarea en la cola.
- Reiniciar nunca repite ciegamente movimientos o borrados. Primero reconcilia identidades, existencia y estado; solicita una decisión si no puede determinar el resultado.
- Un lote no se tratará como una transacción atómica. Mostrar qué se completó y qué quedó pendiente.
- Deshacer/rehacer solo se ofrece para operaciones con inversa verificable. El diario no proporciona un rollback universal ni garantiza recuperar un archivo borrado permanentemente.
- Papelera preferida cuando el proveedor la soporte. Si una operación será permanente, debe indicarse antes de ejecutarla.
- Resolución explícita de reemplazos, cambios concurrentes, nombres equivalentes por mayúsculas y movimientos dentro del propio subárbol.
- Semántica documentada y probada para ACL, streams alternativos, atributos, enlaces, timestamps, EFS y archivos sparse. Si el destino no soporta preservar información, no perderla silenciosamente.
- Pausa entre elementos como capacidad inicial. Pausa a mitad de archivo y reanudación tras reinicio requieren un motor con soporte real; no se atribuyen automáticamente a IFileOperation.
- Si se incorpora un motor propio de copia: temporales, verificación, publicación final según garantías del sistema de archivos, gestión de destinos existentes y limpieza recuperable. En movimientos entre volúmenes, no eliminar origen antes de confirmar la copia satisfactoria.
- Verificación opcional por hash para transferencias críticas; no duplicar por defecto todo el tráfico de disco de cada copia.

Cerrar la ventana mientras hay trabajo requiere una política visible: mantener OperationsHost y permitir reconectar, o solicitar cancelación segura. Un timeout de lectura puede terminar un worker de lectura; un timeout nunca se resolverá matando automáticamente un proceso que está modificando archivos.

## 9. Búsqueda, etiquetas y persistencia

Primera entrega: búsqueda por nombre progresiva y cancelable en el ámbito elegido. Integrar Windows Search para contenido y propiedades ya indexadas, con estado explícito cuando el servicio no esté disponible o la ubicación no esté cubierta. No arrancar un segundo indexador global sin demostrar su necesidad. [Indexación de Windows](https://support.microsoft.com/en-US/Windows/Experience/Performance-Optimization/search-indexing-in-windows).

Las carpetas inteligentes serán consultas guardadas, no copias. Las operaciones sobre un resultado actúan sobre el archivo original, y la interfaz mostrará su ubicación. Los cambios de permisos invalidarán el acceso; un índice no autoriza leer archivos inaccesibles.

SQLite guardará etiquetas, colecciones, preferencias y diario; separar datos de usuario de cachés reconstruibles. Una base corrupta no debe impedir navegar por el sistema de archivos. Versionar esquema y migraciones, conservar recuperación y probar actualización desde versiones previas.

Etiquetas: usar identidad del proveedor/volumen/archivo donde sea estable; en redes o proveedores sin identidad persistente, emplear un mecanismo de reconciliación conservador. Los movimientos realizados por la app actualizarán asociaciones; los externos se reconciliarán sin asignar etiquetas a otro archivo solo porque reutilizó la misma ruta. Copiar/mover entre volúmenes y reutilización de IDs son casos de prueba obligatorios.

Las etiquetas serán locales inicialmente. Exportación/importación permitirá trasladarlas con comprobación de correspondencia; no prometer que viajan automáticamente con los archivos, que sobreviven a cualquier copia externa o que son interoperables con las etiquetas de macOS. No usar streams NTFS como único almacenamiento por su falta de portabilidad.

Un índice propio acelerado será una evolución condicionada por mediciones. Si se usa el diario USN, deberá tolerar truncamiento, pérdida de historial y sistemas sin NTFS; la aplicación básica no requerirá privilegios de administrador para indexar.

## 10. Seguridad y privacidad

- Ejecutar la aplicación con privilegios normales. ElevationBroker tendrá comandos acotados, consentimiento UAC cuando corresponda y ninguna ejecución arbitraria de shell.
- IPC con permisos del usuario, autenticación del interlocutor, versión, límites de mensajes y validación de capacidades; una ruta enviada por un cliente no constituye autorización suficiente para actuar como administrador.
- Revalidar el objeto de destino antes de mutaciones y mitigar cambios entre comprobación y uso, redirecciones por enlaces/reparse points y confusión de rutas. Usar handles/identidades cuando sea posible.
- Preservar marcas de procedencia y respetar protecciones del sistema; no desactivar antivirus ni usar exclusiones para mejorar benchmarks.
- Vistas previas sin macros, scripts ni descargas de recursos externos. Limitar tamaños, tiempo de decodificación y expansión de archivos comprimidos.
- Extracción resistente a traversal de rutas, enlaces maliciosos, colisiones y archivos bomba. En caso dudoso, rechazar con explicación en vez de escribir fuera del destino.
- C++ con RAII, ownership explícito, análisis estático, comprobaciones de límites y sanitizers en configuraciones compatibles; fuzzing para parsers y contratos de IPC.
- Logs locales sin contenido de archivos y con rutas sensibles minimizadas. Envío de diagnósticos voluntario; telemetría desactivada por defecto.
- Dependencias fijadas, inventario de componentes y licencias, revisión de actualizaciones, binarios firmados y actualización autenticada.

## 11. Estrategia de pruebas y CI

La compilación y las pruebas de WinUI se ejecutarán en Windows. Este plan se preparó en macOS; no se ha compilado ni medido una aplicación. Se necesitan runners Windows y hardware real de ambas arquitecturas para validar rendimiento y compatibilidad nativa.

| Nivel | Verificación |
|---|---|
| Unitario | Navegación, selección, orden, filtros, conflictos, políticas, consultas e identidades |
| Integración | Directorios temporales y volúmenes de prueba; portapapeles, operaciones, diario, SQLite y proveedores |
| UI | Flujos completos con teclado y ratón; accesibilidad, foco, Narrator, DPI, temas y varias ventanas |
| Fiabilidad | Desconexión USB/SMB, disco lleno, acceso denegado, archivo bloqueado, cierre abrupto y estado parcial |
| Concurrencia | Renombrados externos, eliminación durante lectura, cambios masivos, pérdida de eventos y cambio rápido de pestañas |
| Seguridad | Entradas malformadas, previews hostiles, extracción, IPC, elevación y carreras con enlaces |
| Rendimiento | Corpus de 1k/10k/100k/1M elementos, contenido mixto, cachés frías/calientes y sesiones largas |
| Distribución | Instalación, actualización, migración, desinstalación y conservación de configuración en x64/ARM64 |

Cada PR: compilar ambas arquitecturas, análisis y pruebas rápidas relevantes. Las pruebas gráficas requieren sesión interactiva; no asumir que funcionan en un servicio de CI sin escritorio. Programar pruebas largas y de fallos en máquinas dedicadas; nunca usar documentos reales del usuario como corpus destructivo.

Los benchmarks de aceptación se ejecutan en equipos estables, no en runners compartidos ruidosos. Una regresión reproducible superior al 10 % exige análisis y decisión documentada. Toda entrega debe cerrar defectos de pérdida/corrupción conocidos y cumplir las pruebas de integridad; un test aprobado no es prueba de ausencia universal de fallos.

## 12. Fases, dependencias y criterios de salida

Estimación orientativa para **3–4 personas con experiencia** en Windows: 10–16 semanas para un MVP de uso controlado; 9–15 meses para una versión amplia con las prioridades principales y endurecimiento. La paridad completa, edición multimedia avanzada y compatibilidad extensa con terceros pueden requerir más. Las duraciones se solapan parcialmente y se revisarán tras la fase 1; no son fechas comprometidas.

| Fase | Duración orientativa | Entregable | Criterio de salida |
|---|---|---|---|
| 0. Definición y referencia | 1–2 semanas | Matriz Explorer/Finder, hardware, builds de referencia, ADR de stack y límites | Casos de uso y aceptación definidos; riesgos de Shell/ARM64 identificados |
| 1. Prueba de arquitectura | 2–3 semanas | Ventana nativa, enumeración virtualizada de 100k, cancelación, worker y mediciones | Primeras filas y UI dentro de presupuesto; prueba de interoperabilidad Shell y empaquetado en ambas arquitecturas |
| 2. Núcleo utilizable | 5–7 semanas | Navegación, pestañas, operaciones seguras, papelera, búsqueda básica, vista rápida inicial y accesibilidad | Recorrido abrir → localizar → copiar/mover → verificar → deshacer soportado; errores y parciales visibles |
| 3. Compatibilidad Windows | 5–8 semanas | Red, extraíbles, nube, Shell, ZIP, búsqueda indexada y formatos adicionales | Matriz de proveedores aprobada, desconexiones y formatos adversos sin bloquear UI |
| 4. Productividad Finder | 5–8 semanas | Columnas, galería, árbol en lista, etiquetas, colecciones, renombrado y acciones básicas | Flujos completos, persistencia y rendimiento con cada vista |
| 5. Endurecimiento y beta | 6–8 semanas | Recuperación, seguridad, estrés, ARM64 real, migraciones, instalación y documentación | Sin defectos conocidos bloqueantes de integridad/seguridad; presupuestos y pruebas prolongadas aprobados |
| 6. Versión 1.0 y evolución | 3–4 semanas iniciales | Distribución firmada, notas de versión, matriz publicada y mantenimiento | Instalación/actualización verificadas; limitaciones claras; diagnóstico y reversión definidos |

Ruta crítica: decisiones de identidad/proveedor → enumeración/cancelación → navegación virtualizada → operaciones y recuperación → compatibilidad → funciones avanzadas. Rendimiento, seguridad y accesibilidad se validan durante todas las fases, no solo al final.

Distribución sugerida del equipo: una persona UI/experiencia, una núcleo/rendimiento, una operaciones/integración Windows y una calidad/seguridad/build. En un equipo menor, reducir trabajo simultáneo antes que omitir las pruebas de integridad.

## 13. Primer backlog ejecutable

1. Registrar builds de Windows y Finder, inventario de capacidades y conjunto mínimo de pruebas comparativas.
2. Fijar toolchain, Windows App SDK Stable y versiones de dependencias; documentar build x64/ARM64.
3. Crear solución, CI de compilación, app WinUI mínima y MSIX instalable en ambas arquitecturas.
4. Implementar identidades, capacidades y proveedor local de enumeración cancelable por lotes.
5. Construir vista de detalles virtualizada con navegación rápida entre carpetas y pruebas de resultados obsoletos.
6. Instrumentar inicio, primeras filas, listado completo, frames, memoria y cancelación; comparar con Explorer.
7. Probar el límite entre UI y ShellHost: un menú contextual, arrastre/portapapeles y fallo deliberado de un worker de lectura.
8. Incorporar OperationsHost con una copia real, colisión, error parcial, diario y reconexión tras cerrar UI.
9. Probar papelera y recuperación documentada antes de ampliar operaciones destructivas.
10. Revisar presupuestos y ADR con evidencia; ampliar a MVP únicamente con el núcleo estable.

Las pruebas de viabilidad son tareas futuras del plan. No se han ejecutado ni sus resultados se dan por supuestos.

## 14. Riesgos y decisiones pendientes

| Riesgo | Tratamiento |
|---|---|
| WinUI añade coste de inicio o layout | Medir pronto, simplificar plantillas y carga; documentar y reportar problemas reproducibles |
| Compatibilidad Shell incompatible con aislamiento absoluto | Adaptadores y hosts por confianza; matriz y rutas delegadas al sistema |
| Un lenguaje nativo introduce errores de memoria | Diseño de ownership, sanitizers, fuzzing y revisión de fronteras de entrada |
| «Todas las funciones» impide entregar | Inventario versionado, fases y limitaciones públicas; no rebajar objetivos silenciosamente |
| Paridad funcional deteriora rendimiento | Cada proveedor/vista pasa la misma suite; carga diferida y presupuestos por proceso |
| Pausa/deshacer aparentan garantías inexistentes | Capacidades explícitas y semántica por operación/proveedor; UI basada en estados reales |
| Etiquetas pierden asociación tras cambios externos | Identidad estable donde exista y reconciliación conservadora, con exportación |
| Copias o recuperación causan pérdida de datos | Operaciones del sistema primero, diario sin reejecución ciega y fallos inyectados |

Por decidir antes de implementar: aceptar el stack recomendado, equipo disponible, proveedores obligatorios para 1.0 y canal de distribución. La versión mínima exacta de Windows 11 se fijará según builds soportadas y pruebas del SDK, manteniendo x64 y ARM64.

El usuario describió WinUI como open source; eso no determina la licencia de EasyFileExplorer. Si la aplicación también será abierta, elegir licencia y añadir CONTRIBUTING, SECURITY, inventario de dependencias y proceso de releases antes de publicarla. Este plan no publica el repositorio ni elige una licencia en nombre del usuario.

**Criterio de producto:** permitir gestionar archivos con la familiaridad de Explorer, las mejoras de productividad de Finder y una respuesta mediblemente mejor, manteniendo comportamiento predecible cuando un disco, archivo, red o extensión falla.
