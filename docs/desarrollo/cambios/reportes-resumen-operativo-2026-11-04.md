# Reportes: resumen operativo y colas por nivel revisor

## Alcance y decisiones

Cambio mixto solicitado el 04/11/2026. El usuario confirmó que Subcoordinación/Dirección identifica al nivel que revisa, no al responsable de la tarea. La entrada sin parámetros ya usa overview/semana actual; se conserva. URLs y plantillas explícitas conservan sus filtros.

Filtros y plantillas quedan en details cerrado; errores y edición lo abren. Guardar plantilla/PDF pasan al pie. Se conservan las seis tarjetas y se añaden dos colas actuales, integradas en la misma cuadrícula y rotuladas con todas las fechas. No heredan filtros del informe, para no esconder pendientes históricos. El PDF conserva las métricas del periodo; no mezcla las colas actuales.

## Fuentes y recorrido

La corrección posterior del mismo día separa `ChartStatus`/`ChartGroup` de `F.Status` y los filtros generales. El clic navega con los parámetros aplicados actuales, sin enviar cambios de filtros aún no generados. Solo se filtra la colección de gráficas antes de paginar: indicadores, detalle y colas permanecen completos. Repetir selección o Ver todas quita el enfoque. El PDF incluye tres gráficas y rotula el enfoque; las plantillas no lo persisten. `ReportAnalysis.PeopleChart` agrupa por PersonId y excluye reasignadas salvo selección explícita. El tercer panel se coloca a la derecha y refluye debajo. Más filtros y presentación se integra en la fila principal cerrado; abierto dispone del ancho completo para sus campos.

- `Pages/Reports/Index.cshtml.cs`: autoriza sesión y consulta métricas filtradas; añade conteos usando el servicio de Supervisión antes de paginar. Sin escrituras operativas.
- `OperationalSupervisionService.ReviewQueues.cs`: intersección con Query autorizado; entrega con revisión nula/pendiente, roles de cada nivel y especialización de Dirección. Comparte el predicado con `OperationalSupervisionService.Paging.cs`.
- `Pages/Validations/_ValidationFilters.cshtml`: Nivel de revisión visible; se conserva en GET, paginación y rutas de validación existentes. No cambia autorización de decisiones.
- `Pages/Reports/Index.cshtml`, `reports.css`, `mobile.js`: disclosure nativo y acciones al pie, sin doble plegado móvil ni recolocación del botón Guardar.
- Productores existentes: entrega registra CompletedAt/ReviewStatus; revisión final cambia ReviewStatus. Las consultas consumen el estado presente y conservan registros históricos. No cambian catálogo, agenda, cargas, responsables, evidencia, reglas de revisión ni esquema.

Las colas se solapan en tareas especializadas de Piso de ventas, como permiten las reglas anteriores. No representan asignación exclusiva ni suman un total. Mi nivel conserva la cola predeterminada; parámetros de nivel desconocidos no amplían el alcance.

## Entorno

Checkout master con modificaciones previas en Personas, Validación, capacidad, catálogo y pruebas. Se conservaron; el diff de mobile.js contiene también un cambio previo de descripciones. La restauración inicial de la base de pruebas se solapó con el primer arranque y falló; se descartó como evidencia y se creó una segunda copia completa antes de iniciar pruebas.

Copia válida: `sgol_task_checks_report_overview_20261104_r2` en localhost:5440. Host 7297, actores sintéticos, reloj 04/11/2026 12:00 México, formularios/CSRF/servicios/PostgreSQL reales con autenticación sustituida solo en el host de pruebas. Evidencias privadas: `.local/report-overview-20261104/`.

## Matriz ejecutada

| Escenario / precondición | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Entrada sin parámetros | Resumen semanal visible y filtros cerrados | Razor real + navegador aislado | Aprobado |
| Escritorio/móvil, claro/oscuro/especial | Reflujo, título antes de 400 px, controles al pie | Chromium/WebKit, 320/390/768/1440, 24 combinaciones | Aprobado |
| Teclado en configuración | Enter abre details y formulario visible | Ambos navegadores | Aprobado |
| Conteo por nivel y navegación | Mismo total en tarjeta y Validación; filtro persiste | SQL, HTTP y clic en ambos motores | Aprobado |
| Históricos, especialización, finales y no entregadas | Colas solapadas correctas; exclusiones conservadas | Reglas + SQL sobre copia | Aprobado |
| Alcance autorizado y cuenta de Piso | Consulta nunca amplía Query; Piso no muestra tarjetas | Reglas + HTTP real | Aprobado |
| Guardar plantilla y PDF | Persistencia de plantilla, vista previa descargable | Formularios reales en ambos motores | Aprobado |
| Fechas invertidas | Aviso y configuración abierta | Ambos navegadores | Aprobado |
| Filtros y reportes existentes | Presets, binding, estados, arrastre, orden y métricas | Sgol.ReportChecks: 107 comprobaciones | Aprobado |
| Lecturas de reporte/destino | Sin nuevas asignaciones | Comparación de cantidad SQL antes/después | Aprobado |
| Concurrencia/reintentos de revisión | Sin cambio en handlers de decisión | No se modifica persistencia de revisión | No aplicable al cambio |
| Sesión real compartida | Visualización autenticada del resultado | No se dispuso de sesión automatizada | No ejecutado |
| Alternar Vencidas/Revisión/Concluidas | Indicadores y detalle idénticos al reporte base; suma por persona exacta | Export JSON + clics reales, Chromium/WebKit | Aprobado |
| Quitar enfoque y resultados vacíos | Clic repetido/Ver todas restaura; filtros incompatibles producen gráfica vacía | Navegador real aislado | Aprobado |
| Tres gráficas y PDF enfocado | Reflujo sin desbordamiento y PDF generado | 38 comprobaciones, incluidas 24 combinaciones visuales | Aprobado |
| Más filtros a la derecha | Misma fila a 2560 px; teclado, reflujo 320/390/768/1440 y envío GET | Ambos motores; `report-inline-filters-checks.cjs` | Aprobado |

Compilación aprobada. Una advertencia transitoria de copia de DLL en TaskChecks se resolvió en el mismo build. Restauración actualizó el lock de ReportChecks para incorporar Markdig, dependencia ya presente en Web. Diff revisado con CRLF permitido.

## Publicación y documentación

Publicación Debug en `.local/runtime/report-overview-20261104/`, hash de DLL contrastado con salida de build. Se sustituyó solo Web: lanzador 34944, proceso 23740; proxy 20652 conservado. Artefacto anterior workload-capacity-20261104 disponible para recuperación. Live/ready interno y acceso local/LAN respondieron 200 con TLS normal. Sin migración ni escrituras operativas en la simulación compartida.

Recorrido de bóveda consultado: Inicio, mapa de diseño, reglas, diseño 00/03/05, mapa de infraestructura, instalación local, base documental y acceso LAN. Se actualizaron las notas existentes de pantallas, supervisión, instalación y base documental, sin crear duplicados ni alterar mapa/inventario. Índice Graphify actualizado. No se realizó commit ni push.

La publicación final pasa a `.local/runtime/report-chart-focus-20261104/`, lanzador 39264. Se conserva report-overview-20261104 como recuperación. DLL idéntica a la probada por TaskChecks; live/ready y acceso local/LAN 200, proxy conservado. Evidencias adicionales `focus-*` e `inline-filters.log` en el mismo directorio privado. No se repitió la suite de reglas anterior: no cambiaron sus reglas, y el nuevo enfoque se verificó mediante HTTP/exportación y navegadores reales. Sin migraciones ni datos compartidos modificados.

## Integración de las ocho tarjetas

A petición del usuario, las dos colas ocupan los espacios 7 y 8 de la misma cuadrícula. Se retira el bloque separado y se conserva Todas las fechas · Cola actual dentro de cada tarjeta. Cambio de interfaz sin alterar consultas ni enlaces. Compilación sin advertencias; 24 combinaciones en Chromium/WebKit, tres temas y cuatro anchos aprobadas, alineación comprobada a 1440 y navegación real a Validación aprobada. Evidencia grid-* en el directorio privado existente. Publicación final report-card-grid-20261104, hash idéntico al host aislado; recuperación report-chart-focus-20261104. Notas existentes actualizadas, sin nuevos mapas ni documentos de bóveda.

## Conservación de posición al seleccionar

Se retira el ancla report-charts al navegar por tarjetas y se restaura una posición temporal, asociada a la URL destino exacta y consumida una sola vez. Se conserva foco sin scroll para teclado. Sin almacenamiento disponible se mantiene navegación sin ancla. Ocho combinaciones Chromium/WebKit, 390/1440 y posiciones 0/120 comprobaron posición, indicadores, gráfica por persona y alternancia con Enter. Evidencia scroll-browser.log; sintaxis JS aprobada. El contenido HTTP de reports.js coincide exactamente con el checkout compartido; no se reinició Web ni se alteraron datos o esquema. Cambio visual; no requiere recompilación.

## Separación superior compacta

La separación medida entre encabezado y filtros era 56 px (32 de padding del main y 24 de margen del panel). Se reduce a 12 px con reglas limitadas a Reportes y al encabezado trasladado. No cambian otras pantallas ni controles. Aprobadas 24 combinaciones Chromium/WebKit, tres temas y anchos 320/390/768/1440: separación medida, ausencia de desbordamiento y apertura de filtros. Evidencia spacing-browser.log. CSS servido por la app compartida idéntico al checkout; sin reinicio, compilación ni cambios de datos. Nota existente de pantallas actualizada, mapa e inventario conservados.
