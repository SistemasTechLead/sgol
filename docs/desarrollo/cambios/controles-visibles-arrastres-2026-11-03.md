# Controles distinguibles y peticiones de arrastre

Cambio de interfaz solicitado el 03/11/2026. Los botones textuales, destinos móviles, controles de apertura, velocímetro y filas interactivas de reportes reciben superficie neutra, borde y sombra discreta. Los desplegables nativos y móviles comparten una flecha de 9 px con trazo de 2 px, centrada verticalmente junto al contenido y orientada según apertura. Se conservan los enlaces de prosa y la ayuda de información independiente.

Esta petición sustituye la preferencia histórica de acciones auxiliares transparentes y destinos móviles sin fondo de ESTILO_VISUAL_V2. Usa sus tokens de superficie, borde, radios y colores por tema. El selector Semana actual del velocímetro de Mi trabajo ya había sido eliminado por otro cambio previo: no se restaura. La consulta histórica de Supervisión sigue disponible.

## Rutas y conservación

- `Pages/Shared/_Layout.cshtml` carga `wwwroot/css/interactive-controls.css` con versión de caché, después de los estilos compartidos.
- `Pages/Validations/_Carryovers.cshtml`: una tarjeta por petición; instrucciones comunes una vez por tipo presente; comentario sin identificador visible. Conserva acción, antiforgery, requestId, requestRevision, approve, validación requerida y límite de 4000 caracteres.
- `Pages/Shared/_CarryoverHistory.cshtml`: cabecera con persona/tipo, tareas, fecha y estado. Causa/minutos y demás detalles permanecen desplegables. Se quitan repeticiones del estado y fecha; identificador técnico permanece dentro del detalle histórico, fuera del comentario.
- Los productores OperationalCarryoverService y consumidores OperationalCarryoverReader e IndexModel mantienen snapshots, consultas, permisos, revisiones y decisiones. No se cambia persistencia, historial, cargas ni fechas. Sin migraciones.

Había modificaciones staged y unstaged ajenas al alcance, incluidas Layout y velocímetro; se conservaron. Simulación compartida: backend 7241, proxy LAN existente, PostgreSQL local 5440/sgol_local. Pruebas finales: HTTPS localhost:7286 y base `sgol_task_checks_controls_20261103`, copia de la base aislada desktop. Se añadió un duplicado exclusivamente visual de una solicitud pendiente para probar dos tarjetas, sin escribir en la simulación compartida. La prueba POST rechaza una de esas peticiones en la copia.

## Matriz ejecutada

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Supervisión, Validación, bono, Mi trabajo, tareas, planificación, reportes, tablero y arrastre | Controles con fondo y flecha consistente | `interactive-controls-checks.cjs`, Razor autenticado, Edge/WebKit | Aprobado: 162 combinaciones |
| 320, 768 y 1440 px; claro, oscuro y especial | Sin desbordamiento y campos conservados | Misma suite, captura y estilos calculados | Aprobado |
| Teclado Enter/Espacio y foco | Abrir/cerrar sin perder accesibilidad nativa | Misma suite | Aprobado |
| Dos peticiones y texto largo | Tarjetas separadas, guía única, comentario sin UUID | Fixture aislado y capturas revisadas de móvil/escritorio | Aprobado |
| Texto al 200 % en arrastres | Reflujo sin desbordamiento | Misma suite | Aprobado |
| Comentario vacío | No enviar POST | Navegador real, `.local/controls-20261103/form.cjs` | Aprobado |
| Rechazar con comentario | Modificar solo la petición elegida, conservar otra pendiente y mostrar decisión en historial | Formulario POST real con antiforgery y servicio en copia aislada | Aprobado |
| Compilación y publicación | Razor y aplicación compilables | `dotnet publish --no-restore`, registro local | Aprobado |
| Aprobación con conflicto, concurrencia y rollback | Conservar contratos existentes | No se modifican servicios; no se repitió suite funcional | No ejecutado |
| Lector de pantalla y dispositivo físico | Comprobación específica | No disponibles | No ejecutado |

Evidencia privada: `.local/controls-20261103/` (results.json, browser.log, form.log, publish.log, capturas). Artefacto: `.local/runtime/controls-final-20261103/`. Se conserva el tamaño compacto anterior del velocímetro del tablero y su área ampliada de interacción. No se confunden las copias con entornos productivos.

Los primeros intentos detectaron sesión expirada y comparación de formularios dependiente de orden DOM; se corrigió la preparación de sesión y la comparación sin orden, conservando cada payload. La primera copia contenía una única petición: se creó una base propia con dos casos antes de aprobar esa comprobación.

## Entrega compartida

Servidor compartido reiniciado con el artefacto final. Se verificaron proceso/binario activo, readiness `ready`, acceso LAN HTTP 200 y CSS nuevo servido por el proxy. Los hashes del CSS fuente y publicado coinciden; el script comprobó que las fuentes no cambiaron desde la preparación del artefacto. Registro `deployment.json` en el directorio de evidencia. No se reinició el proxy ni se modificaron registros operativos compartidos. Sin commit ni push.

## Corrección de navegación activa, 03/11/2026

El usuario detectó una regresión que la prueba anterior no cubría: la regla con `#contenido-principal` sobrescribía fondo y sombra del destino activo, conservando el texto de botón primario. En oscuro combinaba fondo `rgb(41,35,31)` con texto oscuro previsto para el acento claro. La comprobación previa solo exigía fondo no transparente; eso no validaba el contraste ni la selección.

Se limita el sombreado neutro a destinos inactivos. La selección aplica juntos `--color-acento` y `--color-acento-texto`, borde e indicador; hover usa `--color-acento-hover`, y se conserva el anillo de foco. La regla es común a escritorio y móvil.

| Escenario | Prueba / entorno | Estado |
|---|---|---|
| Reproducción de fondo incorrecto | `navigation-color-checks.cjs`, Edge, 375 px, oscuro, antes de corregir | Fallido esperado: confirmó la regresión |
| Activo, hover y foco visible; tres temas; 375/1440; Supervisión, Tareas y Planificación | Misma suite sobre Razor real aislado, Edge/WebKit | Aprobado: 36 combinaciones |
| Inspección visual del resultado oscuro | Captura escritorio Edge y capturas de ambos motores | Aprobado |
| CSS compartido y disponibilidad | CSS publicado actualizado, LAN 200 y readiness ready | Aprobado |

Actualización exclusivamente CSS; no requiere recompilar ni reiniciar el proceso. Se actualizó también el CSS del artefacto activo y se verificó su entrega por LAN. Evidencia: `navigation.log`, `navigation-results.json` y `navigation-*.png` en `.local/controls-20261103/`. No se ejecutaron escrituras operativas.

## Ancho estable de desplegables, 03/11/2026

El sombreado compartido usaba `display:flex` con ancho automático: el summary llenaba el details. Catálogo, filtros de Validación y filtros de escritorio amplían ese contenedor al abrir, trasladando el cambio de ancho al activador. Se reprodujo con clic real en Ver descripción de Tareas.

El activador ahora mide su contenido (`width:max-content`), se limita al espacio disponible y evita estirarse en grid. Se mantiene el ancho independiente del cuerpo desplegado. Los details permiten encogerse dentro de grid en móvil; sin esa regla, la primera corrección desbordaba las tarjetas de arrastre a 320 px. Los filtros abiertos utilizan una base flexible automática sin encoger el texto del activador (se encontró reducción de 301 a 289 px en Tipo de reporte). Los controles móviles de apertura también conservan ancho de contenido. Ver/Ocultar carga reserva el espacio de ambas etiquetas con grid y visibilidad, manteniendo solo la etiqueta vigente accesible; se elimina su flecha duplicada.

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Descripciones, arrastres, Actualizar asignaciones | Ancho igual cerrado/abierto; activadores cortos no llenan la fila | `disclosure-width-checks.cjs`, Razor autenticado con datos reales de la copia aislada | Aprobado |
| Supervisión, Validación, bono, carga, Mi trabajo, planificación, procesos, personas, configuración, reportes y tablero | Misma regla compartida; cuerpo desplegado utilizable | Misma suite, 15 rutas totales | Aprobado |
| 320/768/1440 px, Edge/WebKit | Sin desbordamiento global | 90 combinaciones y 532 mediciones cerrado/abierto, más clics reales en summary y controles móviles | Aprobado |
| Ver carga/Ocultar carga y filtros | No cambiar ancho al alternar texto o ampliar el cuerpo | Medición más clic real | Aprobado |
| Presentación de Actualizar asignaciones y controles móviles | Tamaño de contenido, flecha y sombreado conservados | Capturas de pantalla real revisadas | Aprobado |
| Entrega compartida | CSS corregido servido por LAN, aplicación disponible | `width-deployment.json`: HTTP 200 y readiness ready | Aprobado |
| Nuevas escrituras de negocio, migraciones, recompilación | No requeridas por este cambio CSS | Sin modificación de formularios, datos ni servicios | No aplicable |

La suite candidata sirvió exclusivamente el CSS fuente al navegador de prueba sobre el host aislado 7286; la aplicación compartida se actualizó después de aprobarla. Evidencias: `width-before.log`, `width.log`, `width-results.json` y capturas `width-*.png` en el directorio local ya indicado. Se conservaron la corrección de color activo y los cambios anteriores. No se reinició el servidor: esta entrega actualiza únicamente el CSS del artefacto activo, cuya identidad se comprobó antes de copiarlo.

## Filtros agrupados y campos compactos, 03/11/2026

La captura del usuario mostró otra consecuencia del crecimiento flexible: aunque el summary conservaba ancho, los details abiertos seguían repartiéndose todo el espacio libre y desplazaban Buscar al extremo. Se reprodujo en Personas a 1440 px con un filtro de 530.67 px y un campo de 506.67 px. La prueba anterior medía el activador, no el espacio ocupado por el grupo abierto.

Se sustituye `flex:1 0 auto` por grupos sin crecimiento, de ancho de contenido y con máximo de 24 rem o el espacio disponible. Los cuerpos de campo usan 18 rem, fechas 14 rem y números 12 rem, siempre limitados por su contenedor. Las barras alinean elementos al inicio y sus acciones no reciben márgenes automáticos. Se conservan nombres, valores, formularios GET, desplegables nativos y adaptación móvil existente. Los textos largos pueden envolver sin ampliar la página.

| Escenario | Prueba / entorno | Estado |
|---|---|---|
| Personas con búsqueda y estado abiertos | Captura oscura a 1920 px: campos compactos y Buscar junto al último filtro | Aprobado |
| Personas, tareas, Supervisión, Validación, carga, bono, reportes, tablero y planificación; 320/768/1440/1920 px | `filter-layout-checks.cjs`, 72 combinaciones Edge/WebKit en Razor aislado; todos los filtros abiertos, sin crecimiento excesivo ni desbordamiento | Aprobado |
| Abrir/cambiar tamaño | Comparación de todos los payloads de formulario | Aprobado |
| Buscar nombre + estado y Limpiar | GET real y retorno a campos vacíos en ambos motores | Aprobado |
| Etiqueta extensa y texto al 200 % | Navegador real, 320/1440, valor conservado y sin overflow | Aprobado |
| No reintroducir salto de ancho | `disclosure-width-checks.cjs`, 90 combinaciones y 532 mediciones cerrado/abierto más clics | Aprobado |
| Entrega LAN | CSS nuevo servido, HTTP 200 y readiness ready | Aprobado |

Prueba inicial fallida registrada en `filter-before.log`. Evidencia final: `filter-layout.log`, `filter-layout-results.json`, `filters-*-dark.png`, `width-after-filters.log` y `filter-deployment.json`, dentro de `.local/controls-20261103/`. CSS candidato probado mediante interceptación local antes de actualizar el artefacto compartido; no se modificaron datos ni servicios ni se requirió compilación/reinicio.
