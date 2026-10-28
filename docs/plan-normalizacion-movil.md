# Plan de implementación de la normalización móvil de SGOL

Fecha: 20 de octubre de 2026. Estado: plan aprobado por el responsable e implementado. Véanse el [registro de verificación](normalizacion-movil-verificacion.md) y la [comparación visual](../outputs/normalizacion-movil-20261020/index.html). La validación en iPhone físico queda pendiente.

Normalizar todas las pantallas actuales para que el trabajo principal aparezca pronto, las acciones se puedan ejecutar con el pulgar y la consulta habitual no obligue a desplazar la página hacia la derecha. Mi trabajo es la referencia de jerarquía y compacidad; Configuración conserva su composición. Se mantiene la identidad visual de SGOL y la presentación actual de escritorio.

Este documento conserva el alcance y los criterios aprobados. Su entrega inicial no modificó la aplicación; la implementación posterior y su evidencia se registran en el documento enlazado. El inventario corresponde al checkout revisado, incluidos sus cambios locales.

## 1 Alcance y protección de la funcionalidad

Se permite modificar CSS, estructura de presentación Razor, etiquetas accesibles y JavaScript dedicado a abrir, cerrar, enfocar y conservar el estado visual de componentes. Reordenar información, plegar filtros o adaptar filas a una lectura vertical son cambios de presentación. Cambiar qué tareas se consultan, quién puede actuar o cuándo se permite una acción queda fuera de alcance.

Límites obligatorios:

- No modificar servicios, contratos, PageModels, handlers, endpoints, autenticación, permisos, cálculos, migraciones ni base de datos para resolver un problema visual.
- Conservar todos los datos y opciones actuales, así como sus etiquetas, valores, mensajes, estados y acciones permitidas. La información secundaria podrá estar plegada, nunca desaparecer.
- Conservar formularios, `action`, método, `enctype`, `name`, valores, restricciones, antiforgery, revisiones, intenciones e idempotencia. No añadir envíos al abrir filtros ni cambiar cuándo se aplica una consulta.
- Conservar búsquedas, selección, orden de resultados, paginación, parámetros de URL, vínculos directos, retorno, descargas, impresión y exportación.
- Mantener evidencia, cámara, Excel, documentos, fotografías, revisiones, reasignaciones, preparación y guardado de planes, procesos, horarios, ausencias e historial con los mismos contratos.
- No duplicar formularios completos para tener una versión de escritorio y otra móvil. Evitar controles e identificadores duplicados, listeners dobles y envíos adicionales.
- No resetear preferencias de apariencia, tamaño de navegación o vista. Mantener `auto`, `mobile` y `desktop` de `sgol.appearance.layout`.
- Mantener escritorio visual y funcionalmente igual mediante reglas acotadas al contenedor móvil y comparación contra capturas previas del mismo estado.
- Respetar los cambios locales existentes. Registrar el diff inicial y comparar contra esa base; no limpiar, revertir ni incorporar trabajo ajeno.

Un filtro plegado sigue siendo un filtro activo: no deshabilitar sus campos ni eliminarlos del envío por estar cerrado. Esto se distingue de los campos condicionales funcionalmente inactivos, que mantienen exactamente las reglas actuales de `hidden`, `disabled` y validación.

## 2 Hallazgos que orientan la intervención

| Área | Evidencia en el código actual | Consecuencia para el plan |
| --- | --- | --- |
| Supervisión | `Validations/Index.cshtml` coloca `_Capacity` antes de las secciones; cada sección antepone `_Filters` a una tabla de siete columnas, con Revisión al final. | Reducir el contenido previo a los pendientes y poner Supervisar dentro del ancho visible de cada registro. Mantener las secciones y la consulta que entrega el servidor. |
| Tablero | `_BoardContent.cshtml` dispone título, contexto, dos selectores, progreso de día y semana y progreso por colaboradores antes de las tareas. | Crear una cabecera breve y plegar el progreso detallado por persona. Mantener visibles el contexto y un resumen compacto. |
| Reportes | `Reports/Index.cshtml` antepone plantillas, seis filtros principales y otros filtros a los resultados. | Resumir la consulta aplicada y abrir el formulario completo bajo demanda, sin cambiar su funcionamiento. |
| Planificación | `_DirectionPlans`, `_DirectionLoad` y `_Closing` contienen selección, tablas, incidencias y confirmaciones; hay además planes de procesos y versiones. | Adaptar todo el recorrido, incluidos errores y confirmaciones, y no solamente la lista inicial. |
| Navegación | `_Layout.cshtml` comparte un `dialog` móvil; `components.js` ya implementa apertura modal y retorno de foco. | Reproducir el fallo de Personas y accesos; revisar estilos computados, dimensiones, foco y scroll antes de escoger la corrección. No asumir un problema de `z-index`. |
| Vista guardada | `theme.js` aplica el modo antes del primer pintado; `components.css` usa `@container sgol-viewport` y fuerza dimensiones según la preferencia. | Resolver según el contenedor existente. Probar móvil forzado en un monitor y escritorio forzado en un teléfono. |

Archivos base de diseño: `docs/design/ESTILO_VISUAL_V2.md`, `componentes.md`, `tokens.md`, `navegacion.md` y `accesibilidad.md`. Reutilizar tipografía, logo, iconos, tokens, colores y mensajes actuales. La guía v2 prescribe tablas con desplazamiento local; para este trabajo se propone una presentación móvil vertical de los registros operativos. Documentar esa excepción móvil al implementar, conservando escritorio y acceso a todos los datos; no extenderla indiscriminadamente a tablas comparativas.

## 3 Reglas comunes de experiencia móvil

### 3.1 Jerarquía y navegación

1. Cabecera de aplicación breve, con menú y cuenta accesibles. Título de página y contexto esencial en un bloque compacto.
2. Una acción principal clara. Las acciones auxiliares se agrupan bajo una opción con etiqueta, sin convertir todos los botones en iconos sin texto.
3. Resumen de filtros aplicados y acceso a Filtros. Las explicaciones largas, leyendas y configuraciones avanzadas se abren bajo demanda.
4. Lista, resultado o formulario principal inmediatamente después. Evitar grandes paneles informativos antes del trabajo.
5. Regreso visible en detalles y procesos, preservando los enlaces y parámetros existentes. Volver y cerrar deben devolver al usuario a un contexto reconocible.

No crear una navegación inferior paralela ni cambiar el orden o los permisos del menú. Normalizar la navegación compartida existente. Los cambios de orden visual deben mantener un orden de lectura y foco coherente; evitar resolverlos solo mediante `order` de CSS.

### 3.2 Controles y lectura

- Objetivo propio de SGOL: área táctil de al menos 44 × 44 píxeles CSS en botones y controles operativos, con separación suficiente. Es una adaptación web de las recomendaciones de interacción de Apple, no una equivalencia universal entre puntos nativos y píxeles CSS.
- Texto de controles de al menos 16 píxeles CSS como punto de partida para iPhone; no reducir tipografía para encajar columnas. Conservar zoom y probar ampliación de texto al 200 %.
- Mantener etiquetas visibles, contraste, foco, estados de carga, éxito y error. El color nunca será la única señal de estado.
- Usar los espaciados y radios de SGOL. Ajustar densidad reduciendo vacíos y explicaciones iniciales, sin reducir las áreas táctiles.
- Considerar áreas seguras mediante `env(safe-area-inset-*)` donde corresponda, orientación, barras variables del navegador y teclado virtual. Usar alturas dinámicas con alternativa compatible; no fijar formularios largos a `100vh`.
- Evitar animación innecesaria y respetar `prefers-reduced-motion`. No depender de hover o gestos de deslizamiento para descubrir una acción.

### 3.3 Filtros y paneles plegables

Usar un patrón común de botón o resumen Filtros, indicador de filtros activos y panel completo con los controles actuales. En móviles pequeños, preferir un desplegable dentro del flujo; recurrir a un diálogo únicamente si la propuesta visual demuestra una ventaja y se puede mantener el mismo formulario.

Mostrar el alcance aplicado con los datos existentes, sin inventar un nuevo resumen de negocio. Conservar valores al abrir y cerrar. Si hay errores en un bloque plegado, abrirlo y enfocar el error. No colapsar un editor que el usuario está utilizando ni cambiarlo durante una actualización automática.

### 3.4 Registros y tablas

Para tareas, personas y planes: lectura vertical por registro, con identidad, estado y acción principal dentro del ancho disponible. Los demás datos quedan en líneas etiquetadas o en Ver detalle. Elegir una implementación de un solo origen y verificar su semántica con lector de pantalla; no aprobar una transformación de tabla que pierda asociaciones de encabezado y celda.

Para matrices genuinamente comparativas —calendario semanal, capacidad por días y columnas arbitrarias de reportes— puede mantenerse una vista tabular con scroll local, encabezados claros e indicación visible de desplazamiento. Debe ofrecerse acceso vertical al registro o día necesario para el trabajo ordinario. Ningún botón necesario para operar dependerá exclusivamente de arrastrar hasta una columna lejana. Nunca esconder el desbordamiento global para disimular contenido inaccesible.

### 3.5 Diálogos, teclado y actualizaciones

Los diálogos caben en el ancho y alto disponibles, con título, cierre visible, contenido desplazable y acciones alcanzables con teclado abierto. Conservar contención y devolución del foco, Escape y la confirmación original. No añadir cierres por tocar fuera a formularios que puedan perder información.

Evitar barras fijas adicionales. Si una acción necesita permanecer visible, verificar que no tape campos, errores, contenido ni foco y reservar el espacio correspondiente. Las actualizaciones del Tablero conservarán filtros, foco, posición y estado plegado siempre que el contrato actual permita conservarlos.

## 4 Inventario completo por pantalla

El inventario contiene las 23 páginas con `@page` actuales. Cada fila requiere revisión, incluso cuando el resultado sea conservar su composición. Las variantes de consulta y los diálogos se prueban como estados de su pantalla, no se cuentan como rutas nuevas.

| N | Ruta y archivo en `src/Sgol.Web/Pages` | Adaptación prevista y comprobación específica | Prioridad |
| --- | --- | --- | --- |
| 01 | `/` · `Entry.cshtml` | Revisar entrada, redirección y sesión no disponible. Mensaje legible y foco visible; conservar destino y resolución de sesión. | P2 |
| 02 | `/acceso` · `Access/Index.cshtml` | Revisar inicio de sesión, cambio de contraseña, enrolamiento MFA y llave, verificación, recuperación y finalización. Campos accesibles con teclado; códigos y errores sin recorte. | P2 |
| 03 | `/mi-trabajo` · `MyWork/Index.cshtml` | Conservar como referencia. Auditar filtros, tareas, paginación y diálogos de entrega; cámara, fotografías, archivo, comentario y progreso deben seguir accesibles. | P1 |
| 04 | `/mi-trabajo/tareas/{obligationId:guid}` · `MyWork/Details.cshtml` | Detalle en columna; evidencia, versiones, revisión, reemplazo, conclusión, decisiones e historial con acciones visibles. Historial adaptable y retorno conservado. | P1 |
| 05 | `/mi-trabajo/fotografias/{id:guid}` · `MyWork/Photographs.cshtml` | Galería y acciones ajustadas al ancho, imágenes con proporción conservada, apertura y descarga completas. No alterar permisos o manejo del archivo. | P1 |
| 06 | `/mi-trabajo/procesos` · `MyWork/Processes.cshtml` | Revisar lista y detalle por `id`, etapas, etapa activa, entrega, decisiones, diferencias e historial. Mostrar el trabajo activo sin atravesar toda la historia. | P1 |
| 07 | `/tablero` · `Board/Index.cshtml` | Cabecera breve, filtros plegables y progreso de día/semana compacto. Progreso por colaborador bajo demanda. Tareas visibles antes y sin perder carga, estado, responsables o actualización en vivo. | P0 |
| 08 | `/tablero/rol-de-tareas` · `Board/Weekly.cshtml` | Revisar navegación semanal y consulta por día/persona. Ofrecer lectura vertical de la información existente y mantener la matriz comparativa accesible. No cambiar generación o pertenencia al día. | P1 |
| 09 | `/validaciones` · `Validations/Index.cshtml` | Encabezado breve, acceso directo a las secciones, capacidad plegada en móvil y filtros bajo demanda. Supervisar visible por registro. Diálogo completo, evidencia y decisiones sin desplazamiento lateral. | P0 |
| 10 | `/asignar-tarea` · `Occasional/Index.cshtml` | Lista con acciones visibles; creación ocasional en una columna con condiciones, adjuntos y validaciones actuales. Mantener el paso para elegir destinatarios. | P1 |
| 11 | `/tareas` · `Tasks/Index.cshtml` | Revisar catálogo, filtros, creación, edición y demás variantes existentes. Formulario dividido visualmente por secciones, sin nuevo asistente ni guardados por paso. Conservar todos los tipos y condiciones de tarea. | P1 |
| 12 | `/tareas/asignar` · `Tasks/Assign.cshtml` | Adaptar instrucciones, publicación, material, fechas, selección de destinatarios, capacidad y confirmación. Selección y asignación automática conservan su significado actual. | P1 |
| 13 | `/tareas/procesos` · `Tasks/Processes.cshtml` | Revisar lista, alta y editor: proceso e inicio, etapas y condiciones, tareas, revisión y publicación. El flujo y orden de sus pasos actuales se conservan; adaptar rejillas y acciones. | P1 |
| 14 | `/planificacion` · `Planning/Index.cshtml` | Revisar lista, creación normal, operaciones semanales, búsqueda y selección de tareas, preparación, incidencias, capacidad, confirmación, guardado, detalle, archivo y eliminación donde existan. Incluir Cobertura de cierre. | P0 |
| 15 | `/planificacion/procesos` · `Planning/Processes.cshtml` | Adaptar selección de proceso, responsables, etapas, fechas, capacidad, vista previa, plan preparado, detalle, activación y eliminación. Conservar los pasos y estados originales. | P1 |
| 16 | `/planificacion/versiones` · `Planning/Releases.cshtml` | Historial por registro, creación de borrador y diálogo de publicación legibles. Versiones y acciones con la misma autorización y confirmación. | P2 |
| 17 | `/reportes` · `Reports/Index.cshtml` | Resultados antes que la configuración extensa en móvil; resumen de consulta y Filtros. Plantillas bajo demanda, gráficos legibles, detalle accesible, exportación PDF y diálogos completos. El PDF conserva su presentación propia. | P0 |
| 18 | `/personas-y-accesos` · `People/Index.cshtml` | Corregir menú compartido después de reproducir el fallo. Revisar lista, alta, edición, acciones de cuenta, credenciales temporales y acceso al horario. Botones dentro del ancho disponible. | P0 |
| 19 | `/personas-y-accesos/personas/{personId:guid}` · `People/Details.cshtml` | Una columna para empleo, vigencia, disponibilidad e historial. Diálogos de baja/reactivación con motivo y confirmación completos. Revisar persona ausente y conflicto de versión. | P1 |
| 20 | `/personas-y-accesos/especializaciones` · `People/Specializations.cshtml` | Lista, creación, edición y activación con controles táctiles y retorno visible. No alterar las restricciones de desactivación. | P2 |
| 21 | `/configuracion` · `Configuration/Index.cshtml` | Conservar composición. Revisar avatar y personalización actuales, temas y modos auto/móvil/escritorio tras los cambios compartidos. Corregir únicamente regresiones demostradas. | P2 |
| 22 | `/branches/{branchCode}` · `Branches/Details.cshtml` | Incluir la pantalla secundaria de sucursal: datos, mensajes, acciones y tablas presentes sin recorte, con acceso por el rol vigente. | P2 |
| 23 | `/pantalla` · `Screen/Index.cshtml` | Revisar conexión, reconexión y errores. Mantener el flujo y permisos de pantalla dedicada. Verificar que el Tablero de exhibición siga igual en escritorio. | P2 |

### 4.1 Componentes y estados que no deben omitirse

- `_Layout`, `_BoardLayout`: menú hamburguesa, cuenta, cierre de sesión, marca, salto al contenido, navegación activa y retorno de foco. Probar navegación después de entrar en Personas, abrir otro diálogo, cerrar, volver y cambiar orientación.
- Supervisión: `_Filters`, `_Capacity`, detalle y revisión. Cada sección conserva su significado, filtros y resultados; no mezclar conjuntos ni cambiar el orden de tareas del servidor para conseguir una vista más corta.
- Planificación: `_DirectionPlans`, `_DirectionLoad`, `_Closing`, tablas y confirmaciones generadas por sus scripts. Preparar continúa siendo vista previa; guardar sigue siendo una acción explícita.
- Personas: `_Schedule`, horario semanal, varios intervalos, excepciones por fecha, vista previa del impacto y conflictos. Presentar cada día de forma vertical sin unir guardados independientes.
- Mi trabajo: todos los parciales de evidencia, galería, cámara, tarea operacional, progreso, conclusión, revisión, reemplazo, historial, leyenda, avisos y paginación.
- Compartidos: formularios, selectores de fecha, credenciales, alertas, estados vacíos, confirmaciones motivadas, tablas y botones de carga. Incluir elementos que se insertan después del primer render.
- Errores: `_SystemError`, `_ProblemAlert`, validación, 403, 404, sesión expirada, conflicto y resultado incierto donde existan. Mantener acciones, identificador de correlación y mensajes actuales; no ocultarlos detrás de un panel cerrado.
- Estados generales: sin datos, nombres largos, archivos con nombres largos, muchos registros, sin permisos, carga, éxito, error, envío en curso, conexión perdida y reconexión.

Antes de implementar, volver a extraer las páginas con `@page` y los estados por query string, GET/POST y diálogo de cada página. Si aparecieron rutas nuevas, incorporarlas al inventario y a la matriz de pruebas; no usar el menú como único inventario.

## 5 Propuesta de composición para las pantallas prioritarias

| Pantalla | Primera vista móvil propuesta | Contenido disponible bajo demanda |
| --- | --- | --- |
| Supervisión | Título, acceso breve a secciones con sus cantidades actuales, resumen de consulta y primer registro con Supervisar. | Capacidad por persona, filtros completos y datos secundarios de cada registro. |
| Tablero | Título y fecha, acceso a filtros, resumen breve de día/semana y primera tarea. | Progreso por colaborador, explicación y contexto extendido. |
| Reportes | Título, tipo y periodo aplicados, Filtros y primer resultado o estado vacío. | Plantillas, configuración completa, presentación y acciones auxiliares. Exportar conserva acceso claro. |
| Planificación | Título, acción principal y primer plan; en edición, contexto y primer bloque del formulario. | Acciones secundarias, detalle de cargas y consultas auxiliares. Conflictos y confirmación siguen visibles al necesitarlos. |
| Personas y accesos | Título, Crear usuario, filtros resumidos y primera persona con acciones accesibles. | Datos secundarios y paneles auxiliares. El menú se abre completo dentro del área visible. |

Objetivo de diseño en 390 × 844 píxeles CSS, sin alertas y con contenido de prueba normal: encabezado, navegación y controles previos ocupan como máximo aproximadamente el 35 % del alto; al menos un registro útil y su acción principal caben en la primera vista de las listas prioritarias. Es un criterio propio del proyecto, no una regla de Apple. Con texto ampliado, alertas o títulos extensos prevalecen legibilidad e integridad sobre ese porcentaje.

No intentar suprimir todo el scroll vertical: listas e historiales largos lo necesitan. Se elimina el desplazamiento previo innecesario y la búsqueda lateral de acciones.

## 6 Secuencia de implementación

### Fase 0 Inventario de estados y línea base

1. Registrar archivos modificados y huellas de archivos de lógica. Capturar pantallas de escritorio y móvil con datos y preferencias controlados, sin exponer secretos de acceso.
2. Completar para cada una de las 23 rutas sus variantes de query string, rol, formulario y diálogo. Registrar accesibilidad actual, altura antes del primer resultado y desbordamiento.
3. Reproducir el menú de Personas y accesos en vista automática y móvil forzada, con barra lateral previamente compactada y ampliada. Revisar si afectan cascada, ancestros, top layer de `dialog`, ancho, altura o foco. Probar el mismo menú desde otras rutas para aislar el problema.

Entregable: matriz ruta/estado/rol, capturas base y defectos reproducibles. No declarar que el fallo del menú está resuelto solo por revisar el HTML.

### Fase 1 Propuesta visual revisable

Preparar una maqueta navegable con datos ficticios de Supervisión, Tablero, Reportes, Planificación y Personas, más un ejemplo de formulario, menú y diálogo. Mostrar Mi trabajo y Configuración como referencia de coherencia. Incluir un ancho pequeño, uno habitual y la comparación con escritorio.

Resolver en la maqueta el patrón de registro vertical y su acceso a datos completos, Filtros, acciones, retorno y progreso compacto. Presentar esta propuesta antes de modificar las vistas reales. Los prototipos no envían acciones al sistema ni cuentan como implementación funcional.

### Fase 2 Base compartida y menú

Corregir la causa reproducida del menú, normalizar cabecera, áreas táctiles, paneles plegables y diálogos. Encapsular los estilos móviles bajo `@container sgol-viewport (max-width: 48rem)`, usando el límite existente como punto inicial y sin alterar el comportamiento superior a ese límite. Usar consultas de dispositivo solo donde la capacidad del dispositivo lo requiera, no para sustituir el modo de vista guardado.

Preferir una hoja móvil específica, cargada después de la base donde corresponda, con reglas de alcance claras. Revisar de forma explícita estilos de `dialog` en la capa superior y el layout dedicado del Tablero; no asumir que heredan la misma geometría del contenedor. No modificar tokens globales para resolver una sola pantalla.

Cierre: menú correcto en todas las rutas autorizadas y escritorio igual a la base, incluidas anchuras de barra lateral y temas.

### Fase 3 Pantallas P0

Orden: Supervisión, Tablero, Reportes, Planificación y Personas y accesos. Aplicar los patrones compartidos, validar cada flujo completo y revisar escritorio después de cada pantalla. Incluir la conservación del estado visual durante las actualizaciones en vivo.

### Fase 4 Pantallas P1 y P2

Cubrir todas las filas restantes del inventario y todos sus componentes. Mi trabajo y Configuración se revisan como referencias y controles de regresión; no se rediseñan por uniformidad si ya cumplen. Validar acceso, páginas secundarias, errores y pantalla dedicada.

### Fase 5 Verificación integral y entrega

Ejecutar la matriz del apartado siguiente. Entregar capturas antes/después, cobertura de las 23 rutas y variantes, resultados funcionales, diff limitado a presentación y pendientes concretos si existen. Un pendiente impide declarar finalizada la normalización integral.

Aplicar cada fase en cambios pequeños y separables para poder revertir la presentación sin afectar datos o cambios previos. No hacer una sustitución global de tablas, modales o estilos sin revisión por componente.

## 7 Matriz de pruebas y aceptación

### 7.1 Dimensiones y navegadores

| Grupo | Comprobación |
| --- | --- |
| Móvil estrecho | 320 × 568 y 360 × 800 píxeles CSS; texto, botones, formularios, filtros y errores completos. |
| Móvil habitual | 390 × 844 y 430 × 932; medir primera vista, acciones y paneles plegables. |
| Horizontal y transición | Orientación horizontal; 768 y 769 de ancho y límites reales de `48rem`; sin elementos desaparecidos o dos navegaciones activas. |
| Escritorio | 1024, 1280, 1440 y 1920 de ancho; comparación visual antes/después a igual alto, rol, datos, tema y preferencia. |
| Preferencias | Auto, móvil forzado en escritorio y escritorio forzado en móvil; persistencia al recargar y navegar. Claro, oscuro y apariencia especial. |
| Safari en iPhone | Equipo real para teclado, áreas seguras, barras del navegador, selects, cámara, archivos, foco y diálogos. La emulación no acredita estos puntos por sí sola. |
| Chromium | Chrome Android y Edge/Chrome de escritorio en modo adaptable para cobertura adicional. |
| Accesibilidad | Teclado, VoiceOver en iPhone, zoom/texto al 200 %, contraste y movimiento reducido. |

Ejecutar todas las rutas en tamaños base de móvil y escritorio; ampliar las combinaciones sobre componentes compartidos y pantallas con formularios, cámara y diálogos. Registrar por separado las pruebas pendientes de equipo real si no está disponible; no afirmar compatibilidad verificada sin ejecutarlas.

### 7.2 Criterios funcionales

- Comparar antes/después los contratos de los controles y los payloads de las acciones afectadas. Mismos campos, valores, handler y número de peticiones; abrir o cerrar paneles no hace POST.
- Ejecutar recorridos con los roles existentes de Dirección, Administración, Subcoordinación y Piso de Ventas, más acceso sin sesión y pantalla dedicada. Conservar visibilidad y restricciones del servidor.
- Supervisión: filtrar, abrir evidencia, revisar favorablemente y revisar con las alternativas negativas existentes; cerrar y volver al contexto correcto. Usar datos aislados para acciones con efectos.
- Mi trabajo: entregar cada requisito disponible, cámara, carga/descarga, progreso, errores, reemplazo y revisión; procesos con su entrega y decisión actuales.
- Planificación: seleccionar, preparar, revisar incidencias, cancelar y guardar; verificar diferencias entre vista previa, guardado y activación. Incluir procesos, cobertura de cierre y confirmaciones destructivas en datos de prueba.
- Personas: alta/edición, horario, excepciones, disponibilidad y confirmaciones; preservar guardados separados. No usar personas operativas para pruebas de escritura.
- Reportes: aplicar filtros y plantillas, guardar/duplicar/eliminar plantilla cuando esté permitido y exportar PDF. La presentación móvil no debe cambiar columnas exportadas, cálculos ni contenido del archivo.
- Tablero: actualización, filtros, selección, detalle, cambio de día, desconexión/reconexión y progreso. No perder el estado visual al refrescar parciales ni acumular listeners.

Las pruebas de escritura se ejecutan en entorno o datos aislados. No crear, revisar, reasignar, eliminar ni publicar registros operativos para demostrar un cambio visual.

### 7.3 Criterios visuales y de integridad

1. Cero desbordamiento horizontal de página a 320 píxeles CSS en vista automática/móvil. Revisar también el contenido interno: que el documento no desborde no garantiza que un botón dentro de una tabla sea alcanzable.
2. Acciones principales de listas operativas visibles sin desplazamiento lateral. Toda excepción de matriz comparativa queda identificada, confinada al componente y con alternativa útil.
3. Filtros completos y valores aplicados accesibles; estado vacío y errores visibles y comprensibles.
4. Diálogos utilizables con teclado virtual; ninguna acción o error queda fuera de alcance. Cierre y devolución de foco correctos.
5. Escritorio sin cambios de composición, tamaño, orden, controles o navegación respecto a la base. En capturas dinámicas enmascarar únicamente relojes y otros valores variables; revisar aparte que sus contratos sigan iguales.
6. Sin errores de consola, solicitudes duplicadas, identificadores DOM repetidos o controles ocultos que bloqueen el envío.
7. Huellas de lógica sin modificaciones atribuibles a este trabajo, contratos de formulario equivalentes y diff revisado. Cualquier necesidad real de tocar lógica exige redefinir el alcance antes de hacerlo.
8. Compilación de Razor y comprobaciones existentes pertinentes. Añadir verificaciones de UI sobre menú, filtros y acciones accesibles cuando cubran una regresión real; evitar pruebas que solo repitan CSS.
9. Tras servir los cambios, comprobar `/health/ready`, `/acceso` y las rutas autenticadas afectadas en el runtime activo, junto con sus assets actualizados. Build correcto no sustituye evidencia visual y de navegación.
10. Inventario reconciliado: 23 de 23 rutas actuales y todas las variantes levantadas en fase 0 con resultado registrado. Si crece el inventario, actualizar el denominador.

## 8 Archivos previstos

| Grupo | Archivos o carpetas candidatos | Límite |
| --- | --- | --- |
| Base móvil | `wwwroot/css/components.css`, posible hoja móvil adicional, `Pages/Shared/_Layout.cshtml`, `_BoardLayout.cshtml` y parciales visuales. | Encapsular reglas; respetar estilos de escritorio y mensajes. |
| Interacción visual | `wwwroot/js/components.js`, `sidebar.js`, `theme.js` solo si la causa reproducida lo exige; posible módulo visual pequeño. | Conservar preferencias, foco, semántica y eventos de negocio. |
| Pantallas | Las páginas y parciales del inventario; CSS de `live-board`, `reports`, `processes`, `task-form`, `personal-schedules` y cámara según necesidad. | Cambiar marcado y presentación; no reglas de negocio dentro de Razor. |
| Scripts de pantallas | `supervision.js`, `live-board.js`, `reports.js`, `direction-plans.js` y los demás solo para integrar presentación cuando sea necesario. | Revisar sus dependencias del DOM y comprobar que peticiones, validaciones y cálculos no cambien. |
| Evidencia y guías | Este plan, matriz de cobertura, capturas y excepción móvil documentada en guías de diseño. | No sustituir datos reales por resultados de maqueta. |

No es una autorización para editar todos estos archivos: cada cambio debe responder a una necesidad demostrada. Priorizar la solución compartida más pequeña que cumpla los criterios.

## 9 Referencias de diseño y accesibilidad

Aplicar criterios de interacción de iOS a una aplicación web sin prometer una interfaz nativa. Las medidas de densidad y primera vista de este plan son decisiones de SGOL; los estándares orientan accesibilidad y adaptación.

- [Apple Human Interface Guidelines sobre accesibilidad](https://developer.apple.com/design/human-interface-guidelines/accessibility): interacción y adaptación a distintas necesidades.
- [Apple Human Interface Guidelines sobre disposición](https://developer.apple.com/design/human-interface-guidelines/layout): adaptación y áreas seguras.
- [Apple Human Interface Guidelines sobre botones](https://developer.apple.com/design/human-interface-guidelines/buttons): controles reconocibles y áreas de interacción.
- [WCAG 2.2 Reflow](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html): lectura a 320 píxeles CSS sin scroll bidimensional general; contempla excepciones para contenido que requiere dos dimensiones.
- [WCAG 2.2 Target Size Minimum](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html): mínimo AA de 24 × 24 píxeles CSS con excepciones; el objetivo táctil de SGOL de 44 × 44 es más exigente.
- [WCAG 2.2 Focus Not Obscured](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html): evitar que contenido superpuesto o fijo oculte el elemento enfocado.

La entrega final de la implementación deberá demostrar facilidad de operación móvil, cobertura completa y escritorio conservado. No bastará con capturas de la cabecera o con declarar que todas las tablas tienen scroll.
