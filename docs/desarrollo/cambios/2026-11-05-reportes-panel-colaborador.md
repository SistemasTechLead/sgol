# Panel por colaborador en Reportes

## Alcance y estado

Cambio mixto implementado, probado en host aislado y publicado el 05/11/2026 tras autorización expresa de incluir todos los cambios pendientes. Se conservan las atribuciones de los cambios previos. No se ejecutaron modificaciones de datos de la simulación compartida.

El usuario eligió explícitamente cumplimiento de la plantilla para el velocímetro, no Bono de tareas. Se reemplaza la tercera gráfica por una tabla de ancho completo debajo de las dos gráficas restantes. El periodo y todos los filtros de la plantilla se conservan, al igual que el enfoque temporal por estado/grupo de las gráficas. La búsqueda y paginación del detalle no recortan el panel.

## Datos, reglas e impacto

- `Pages/Reports/Index.cshtml.cs`, `OnGetAsync`: conserva autorización por sesión, rol y sucursal; lee snapshots de `OperationalAssignments` y sus planes, metas, entregas, revisiones y arrastres. La fecha de referencia y `ReportAnalysis.Filter` delimitan el conjunto antes de resumirlo y antes de paginar.
- `Reporting/ReportPerson.cs`: agrupa por identidad, no nombre. Excluye intentos reasignados del denominador. Entregadas son registros con entrega; cumplimiento es concluidas / total. Pendientes excluyen entregas y cierres. La carga suma sus minutos restantes, respetando el cálculo existente de metas y transferencias.
- La capacidad procede del mismo `OperationalCapacityService.LedgerAsync` que ya usaba el reporte de carga, sumada en el periodo completo. No se calcula sobre el número de tareas de la plantilla. Ausencia de trabajador elegible se representa como capacidad no disponible; cero capacidad y carga positiva es sobrecapacidad. Ocupación puede superar 100 %, aunque la barra visual se limita a 100 %.
- El estado prioriza sobrecapacidad, vencidas, revisión, capacidad desconocida, sin carga y en rango. No se introducen umbrales arbitrarios de bajo cumplimiento ni reglas que cambien asignaciones.
- `Pages/Reports/_People.cshtml`, `reports.css`: tabla, resumen y velocímetro con geometría basada en el componente existente, sin conectarlo al bono. `mobile.js` conserva todos sus indicadores visibles en tarjetas móviles.
- `reports.js`: PDF incorpora las personas y las mismas cifras exportadas. Las plantillas guardadas mantienen su contrato; no necesitan migración.
- No cambia creación, almacenamiento, ejecución, revisión, permisos, historial, plazos ni cargas operativas. Es una proyección de lectura al corte; no reconstruye una fotografía histórica de la capacidad ni presenta la carga filtrada como carga global.

## Matriz de validación

| Escenario / precondición | Acción y esperado | Entorno y prueba | Estado |
|---|---|---|---|
| Concluida, entregada, vencida y reasignada | Resumir; denominador 3, cumplimiento 33.3 %, pendiente 1, carga 30 | `Sgol.ReportChecks` | Aprobado |
| Capacidad 20, cero o desconocida | Ocupación 150 %, sobrecapacidad sin división por cero, desconocido separado de cero | Reglas aisladas | Aprobado |
| Homónimos y solo reasignados | Identidades separadas; sin cumplimiento ficticio | Reglas aisladas | Aprobado |
| Ocho plantillas predefinidas | Aplicar desde selector real; panel y JSON exportado coinciden con filas filtradas | Razor y PostgreSQL aislados, Chromium/WebKit | Aprobado |
| Enfoque de pendientes de revisión | Panel sin carga ni pendientes de ejecución | Navegador y exportación real | Aprobado |
| Escritorio/móvil y tres temas | Panel debajo, mismo ancho; sin desbordamiento; región enfocable y velocímetro visible | 320/390/768/1440, Chromium/WebKit | Aprobado |
| PDF | Abrir y generar vista previa con panel nuevo | Ambos navegadores | Aprobado |
| Trabajador vs Dirección | Mantener permisos y colas; consulta sin cambios en asignaciones | `ReportOverviewHttpChecks` | Aprobado |
| Filtros combinados, fechas y valores inválidos | Mantener selección e invalidación existentes | 120 comprobaciones de Reportes | Aprobado |
| Cambiar registros simultáneamente / rollback | No hay nuevas escrituras ni transacciones | Inspección del alcance | No aplicable |
| Autenticación y datos de la app compartida | Validar pantalla publicada | No se publicó este cambio | No ejecutado |
| Muchas personas, texto extremadamente largo, capacidad histórica y carga de consultas grandes | Comprobar rendimiento y presentación sobre datos extensos | Fixture actual pequeño; no prueba exhaustiva | No ejecutado |

## Evidencia y limitaciones

Compilación Web: 0 errores y 0 advertencias. `Sgol.ReportChecks`: 120 comprobaciones aprobadas. Integración: 13 comprobaciones específicas de Reportes en base exclusiva local `sgol_task_checks_report_people_ui_20261105`. Autenticación sustituida solo en ese host; Razor, servicios, SQL y formularios son reales. Navegador: `tools/Sgol.MobileChecks/report-people-checks.cjs`; capturas y resultado privado en `.local/report-people-20261105/`. Capturas revisadas en escritorio oscuro y móvil claro. Sintaxis de ambos JavaScript verificada.

El primer arranque con `dotnet run` resolvió incorrectamente el directorio de recursos y produjo CSS 404; se corrigió ejecutando el DLL desde la raíz del checkout. La prueba inicial de desbordamiento falló por ello; pasó con recursos reales. La revisión visual detectó indicadores ocultos por el patrón genérico móvil y se corrigió específicamente para este panel.

Referencias reconciliadas tras publicación: Reportes en `docs/design/05-pantallas.md`, instalación local y base documental, en bóveda y copia versionada mediante cambios puntuales. No se agregaron notas, mapas ni enlaces nuevos; se conservaron modificaciones previas.

## Publicación autorizada

El usuario pidió actualizar la app compartida e incluir lo pendiente. Publicado todo el checkout en `.local/runtime/report-people-all-20261105`, conservando el artefacto anterior y proxy 20652. Lanzador 2892, Web 32196. SHA-256 CC0FEBC3DF42B48344A644FDCEBC54000F3B567AC829BD2D442F0D5B247140CF igual a la DLL probada. Las 93 migraciones ya estaban aplicadas; no se ejecutaron migraciones. Readiness interno, acceso local y LAN: 200 con TLS normal; estilos del panel disponibles. Sin recorrido autenticado compartido ni commit/push.

## Ajuste de separación solicitado

Reducidos los espacios entre indicadores, enfoque, gráficas, personas y detalle mediante CSS específico de Reportes. Se eliminó la suma de márgenes genéricos de los paneles con los de su contenedor. Verificación en Razor aislado con Chromium a 320, 768 y 1440 px: 8 px entre enfoque/gráficas y 12 px entre gráficas/personas; sin desbordamiento horizontal. Captura de escritorio inspeccionada. HTTP HTTPS compartido confirma que sirve el CSS nuevo; no requiere reinicio ni recompilación. Referencia de pantalla reconciliada en bóveda y copia, sin enlaces nuevos.
