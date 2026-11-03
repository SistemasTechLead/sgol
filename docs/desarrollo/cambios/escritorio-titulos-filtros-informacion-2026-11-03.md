# Escritorio: títulos, filtros y ayuda — 2026-11-03

## Alcance y criterio

Cambio de interfaz autorizado en las pantallas operativas del layout compartido. El título ocupa el lugar de SGOL en la barra superior; se conserva el título original en móvil, impresión y sin JavaScript. Los IDs que nombran secciones permanecen. Acceso, errores de sistema y quiosco conservan sus layouts.

La aclaración posterior del usuario requiere distinguir filtros de navegación mediante un apartado sombreado y trasladar descripciones a un botón de información junto al título. La ayuda es flotante, no modal, ligeramente transparente y no desplaza contenido; reutiliza icono, colores y comportamiento de cierre de la ayuda móvil. Sustituye la presentación histórica de las descripciones permanentes en escritorio de `ESTILO_VISUAL_V2.md`.

Se conservan mensajes de error/éxito, conflictos, consecuencias de confirmaciones, instrucciones de campos y datos de registros. Se trasladan las introducciones de cabecera y, en Tareas, ayuda de filtros, calendario y selección. En Mi trabajo, la misma ayuda reúne la descripción, explicación de orden y leyenda de colores; los dos encabezados redundantes del listado permanecen solo como nombres accesibles.

## Implementación e invariantes

- `_Layout.cshtml`, `desktop-workspace.js` y `desktop-workspace.css`: título y ayuda compartidos; filtros mediante disclosures nativos con selección visible. Superficie, borde, sombra y rótulo «Filtros» separan consultas de navegación.
- La mejora envuelve los controles originales; no sustituye nombres, valores, opciones, formularios, endpoints ni handlers. Restaura el DOM de campos en móvil/impresión. Revela controles inválidos antes del enfoque nativo. Los formularios de edición no se clasifican indiscriminadamente como filtros por usar GET.
- Consumidores: Mi trabajo, catálogos de tareas/procesos, asignación ocasional, tablas compartidas, Personas, Supervisión/Validación, capacidad, bono, cobertura de cierre, Reportes y Tablero. Las opciones avanzadas ya agrupadas mantienen su agrupación.
- `reports.js`: notifica la aplicación programática de una plantilla para actualizar los resúmenes. El editor puede enfocar su campo dentro del filtro abierto.
- `live-board.js`: eventos de presentación antes/después de reemplazar el contenido; conservan filtro abierto y foco durante la actualización. No cambian consulta, permisos ni frecuencia de actualización.
- No cambian almacenamiento, copias, asignaciones, ejecución, revisión, historial ni esquema. La persistencia solo se consulta mediante los mismos GET existentes.

## Entorno y cambios ajenos

Workspace `master` con modificaciones previas de procesos, Markdown, bono, calendario y asignaciones; se conservaron. Durante el trabajo se incorporaron y activaron cambios de renglones compactos de Tareas y actualización de asignaciones. El candidato final incorpora el código vigente, sin atribuir esos cambios a este trabajo.

Prueba en copia de la simulación `sgol_task_checks_desktop_20261103`, PostgreSQL localhost:5440, HTTPS localhost:7284. Se autentica mediante el acceso real. Credenciales, sesiones, copia y capturas permanecen privadas en `.local/`. Fecha operativa 03/11/2026, America/Mexico_City. No se escriben tareas, asignaciones o planes en la simulación compartida.

## Matriz de validación

| Escenario / precondición | Acción y resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| 20 rutas operativas con sesión Dirección | Un título visible en la barra; contenido consultable | Razor real, Edge y WebKit, copia aislada | Aprobado en primera versión; repetición final en curso |
| 1440/1024 y retorno desde 768/320 | Campos/valores idénticos, títulos y controles móviles restaurados | FormData y DOM reales por ruta/motor | Aprobado en primera versión; repetición final en curso |
| Tres temas | Superficies y contenido sin overflow global en escritorio | Capturas y medidas por ruta/motor | Aprobado en primera versión; repetición final en curso |
| Teclado | Enter abre, Tab accede al campo, Escape cierra y devuelve foco | Disclosures visibles por ruta/motor | Aprobado en primera versión; repetición final en curso |
| Ayuda superior | Enter abre sin desplazar main; Escape cierra y devuelve foco | Capturas, medidas y DOM reales | En curso |
| Impresión | Campos restituidos, mismos datos de formulario | Emulación print por ruta/motor | Aprobado en primera versión; repetición final en curso |
| Búsqueda sin coincidencias y limpieza | GET aplica y conserva búsqueda; Limpiar la retira | Mi trabajo, Tareas, Personas; ambos motores | Aprobado en primera versión; repetición final en curso |
| Número inválido plegado | Envío no navega; revela campo y lo enfoca | Formulario real Tareas | Aprobado Edge; repetición final en curso |
| Texto largo y texto al 200% | Sin pérdida de valor ni overflow por resumen | Tareas real | Aprobado Edge; repetición final en curso |
| Fechas personalizadas de Reportes | Fechas conservadas tras GET y reporte visible | Formulario real Reportes | Aprobado Edge; repetición final en curso |
| Tablero con actualización en vivo | Mantiene apertura, selección y foco | Selección real y respuesta Refresh | En curso; se detectó y corrigió pérdida de foco durante reemplazo |
| Compilación final | Razor y aplicación compilan | `dotnet publish --no-restore` | Aprobado |
| Roles distintos, lector de pantalla y dispositivos físicos | Cobertura específica | Fuera de esta ejecución | No ejecutado; no se modifican permisos |
| Escrituras operativas, concurrencia de asignación, migración | Conservación de contratos de negocio | Inspección del alcance | No aplicable: cambio de presentación, sin escrituras operativas |

Ejecutores mantenibles: `tools/Sgol.MobileChecks/desktop-workspace-checks.cjs` y `desktop-workspace-flows.cjs`. Evidencia privada: `.local/desktop-workspace-20261103/`. Fallos intermedios de la suite incluyeron selección de elementos dentro de contenedores cerrados, orden DOM móvil de formularios y sesión vencida; se corrigieron los selectores/comparaciones y se renovó la sesión mediante login. No se contaron como cobertura aprobada.

## Entrega

Usuario solicitó recargar al terminar. Activación y recarga pendientes de la comprobación final. Sin commit ni push.
