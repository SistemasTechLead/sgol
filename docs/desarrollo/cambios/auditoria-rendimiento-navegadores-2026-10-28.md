# Auditoría de peticiones, rendimiento y navegadores — 28/10/2026

## Resultado y alcance

Se ejecutó un agente para auditar peticiones del servidor y se realizaron pruebas de navegación real. Se reprodujo una amplificación de trabajo en la lectura de Excel. Hay riesgos de contención entre operaciones y crecimiento con el historial; todavía no se midió su umbral de degradación. Esta revisión **no certifica preparación para producción ni cobertura de la mayoría de dispositivos**.

Trabajo de diagnóstico funcional y de interfaz, sin implementar correcciones de producto. Se conservaron las modificaciones previas del repositorio, incluidas las de checklist. Las pruebas HTTP autenticadas fueron lecturas de la simulación compartida; el inicio de sesión puede generar sesiones y auditoría de acceso. No se enviaron entregas, importaciones ni cambios operativos. La reproducción del Excel no utilizó HTTP ni base de datos.

## Entorno observado

- Entrada utilizada: `https://localhost:7240`; redirige al origen efectivo `https://192.168.1.92:7440`.
- Ambos puertos pertenecían al proceso `dotnet` PID 10448. Por tanto, no representan dos entornos independientes. Las navegaciones recorrieron el origen LAN desde esta misma computadora, sin simular Wi-Fi remoto.
- Chromium y WebKit de Playwright, sin interfaz, con cuenta de simulación existente con acceso a Supervisión. No equivalen a ejecutar todas las versiones comerciales de Chrome, Edge o Safari ni a un iPhone físico.
- Anchos: 1366, 768, 390 y 320 px; alto 900 px. Chromium usó CPU ralentizada 4× en 390/320 px. No se limitó la red ni la RAM. Se utilizó el tema vigente de la sesión; no se cubrieron todos los temas ni todos los roles.
- Firefox se descargó como runtime de prueba, pero su inicio falló con `browserType.launch: spawn UNKNOWN`. No se desactivó su aislamiento para sortear el fallo.

## Hallazgos priorizados

### 1. Prioridad alta: Excel pequeño con amplificación de CPU y memoria

`OperationalExcel.ReadAsync`, en `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalExcel.cs:51-77`, abre y analiza XML una vez por referencia de hoja. Acepta referencias repetidas a la misma entrada ZIP. El límite de 60 MiB de expansión inicial no limita el trabajo acumulado al volver a procesarla. El parseo XML es síncrono y no comprueba cancelación.

La ruta de entrega lo invoca antes del escáner en `OperationalTaskService.cs:180`. El archivo de prueba se construyó en memoria; no se subió al servidor. El harness ejecutó una copia idéntica del lector actual, verificada por SHA-256, con `FormFile` real y stubs únicamente para constante de límite y excepción.

| Referencias a una hoja de 1000 filas | ZIP | Tiempo de una ejecución | Memoria asignada acumulada | Resultado |
|---|---:|---:|---:|---|
| 1 | 3673 bytes | 20,7 ms | 0,52 MiB | Aceptado |
| 100 | 4123 bytes | 109,6 ms | 34,64 MiB | Aceptado |
| 500 | 5981 bytes | 704,8 ms | 172,64 MiB | Aceptado |

La memoria indicada es asignación acumulada, **no pico residente**. Una única ejecución no demuestra caída del servidor ni constituye un benchmark estadístico. Sí demuestra amplificación de trabajo y presión potencial sobre el recolector de basura por una petición pequeña.

Corrección recomendada: validar unicidad de hojas y destinos, analizar cada entrada una sola vez, presupuestar trabajo acumulado y añadir cancelación y concurrencia acotada. La regresión debe rechazar el archivo repetido sin penalizar libros legítimos de varias hojas.

### 2. Prioridad alta: planificación y entregas comparten bloqueo global

`OperationalTaskService.LockAsync`, en `OperationalTaskService.cs:97-98`, utiliza la misma clave `pg_advisory_xact_lock(734651001)` para operaciones diferentes, incluidas entregas, fotos, checklist y planificación. `Sequences/ProcessPlanningService.cs:91-99` adquiere el bloqueo antes de recalcular `PreviewAsync`; la activación también mantiene el bloqueo mientras inicia procesos.

La serialización está confirmada por código. Que llegue a producir un timeout bajo la carga prevista todavía es una hipótesis que requiere concurrencia aislada. El timeout SQL de 15 s está configurado en `Infrastructure/Persistence/Jobs/JobServiceCollectionExtensions.cs:39`.

Corrección recomendada: medir duración y espera del bloqueo; reducir la sección crítica y estudiar partición por entidad/sucursal conservando capacidad, atomicidad y dependencias. Quitar el bloqueo directamente podría permitir sobreasignación.

### 3. Prioridad media: reportes repiten trabajo sobre todo el historial

`OperationalCarryoverReader.cs:23-54` carga auditorías históricas y solicitudes de arrastre, sin propagar `CancellationToken` a esas consultas. Cada `ForAsync` recorre y deserializa nuevamente solicitudes. `Pages/Reports/Index.cshtml.cs:100` lo ejecuta por cada asignación. El trabajo crece con filas del reporte × solicitudes históricas, aunque la caché por petición evite repetir las consultas SQL.

Además, `Pages/Reports/Index.cshtml.cs:73` construye opciones a partir del historial antes de filtrar fechas. `OperationalSupervisionService.cs:72` recupera el conjunto visible antes de filtros posteriores. Un rango de fechas acotado no establece un máximo de filas.

Corrección recomendada: filtrar en SQL, indexar los metadatos una sola vez por petición, propagar cancelación y paginar el listado. Separar la exportación completa de la carga inicial. Conservar permisos y resultados históricos.

### 4. Prioridad media: DOM considerable en móviles

La navegación real produjo 7288 nodos en Mi trabajo y hasta 8472 en Validaciones. En Chromium con CPU 4×, `loadEventEnd` fue 867–1063 ms para Mi trabajo y 1765–1790 ms para Validaciones en 320/390 px. Son muestras únicas sobre este equipo, sin red limitada; no son percentiles ni tiempos garantizados para teléfonos.

Los valores `elapsedMs` de la evidencia también incluyen espera artificial de 250 ms y la propia inspección del DOM. **No deben interpretarse como tiempo puro de carga**; la estimación de 2,2–2,8 s comunicada durante la exploración correspondía a esa medida total. El heap JavaScript tampoco incluye toda la memoria de imágenes, GPU o proceso del navegador.

No se observó bloqueo en esas visitas. Conviene reducir el contenido inicial y cargar detalles bajo demanda, conservando búsqueda, accesibilidad, permisos y acceso al historial. El volumen de DOM es una señal de riesgo, no prueba suficiente de un fallo por sí solo.

### 5. Prioridad media/baja: el tablero diario actualiza aunque esté oculto

`wwwroot/js/live-board.js:121,193-199` no pausa `refresh` ni el coloreado por `document.hidden`; el tablero semanal sí evita la actualización en ese estado (`weekly-board.js:7`).

Se reprodujo con HTML obtenido de la pantalla real y sus scripts, en una reproducción de componente con `document.hidden=true`, reloj simulado, EventSource no disponible y `fetch` interceptado con respuesta 503. En 16 s simulados el tablero diario intentó una actualización; el semanal, cero. Es una prueba controlada del comportamiento de los scripts, **no una prueba de pestaña física en segundo plano ni carga contra el servidor**.

Recomendación: pausar trabajo no necesario cuando se oculte la pestaña y actualizar al volver. Mantener recuperación de sesión, reconexión y funcionamiento del quiosco visible.

## Defensas existentes verificadas por inspección

- Tableros evitan peticiones de actualización superpuestas y abortan la espera HTTP a los 10 s. El semanal comprueba visibilidad; el diario aplica espera tras error.
- El lector Excel limita archivo y expansión ZIP, prohíbe DTD y relaciones externas. Esas defensas no impiden la repetición descrita.
- La normalización fotográfica limita decodificaciones concurrentes a dos y aplica máximo de 40 millones de píxeles.
- Se encontraron límites de frecuencia para acceso/vinculación de pantallas; no límites equivalentes de concurrencia para Excel, reportes y planificación.

## Matriz de ejecución

| Escenario / precondición | Acción y resultado esperado | Entorno y prueba | Estado / evidencia |
|---|---|---|---|
| Sesión simulada con permisos; seis pantallas | Abrir Mi trabajo, Tablero, Rol de tareas, Reportes, Validaciones y Carga; responder 200, sin errores JS ni desbordamiento global | Aplicación real, Chromium/WebKit, 4 anchos | **Aprobado para carga inicial**: 48 visitas, cero `pageerror`, cero desbordamientos del documento. No cubre interacciones completas ni todos los errores de consola/recursos. |
| Intento de rutas exploratorias | Distinguir URL inexistente y acceso no permitido de un fallo de rendimiento | Aplicación real | `/supervision` respondió 404: ruta exploratoria incorrecta, sustituida por `/validaciones` y `/supervision/carga`. `/tareas` respondió 403 con esta cuenta; no se considera pantalla validada. |
| Excel con referencias repetidas | Rechazar o acotar trabajo duplicado | Harness aislado, lector idéntico al código real, sin BD | **Fallido**: aceptó 500 referencias; 172,64 MiB asignados. |
| Tablero oculto y sin EventSource | No actualizar mientras está oculto | Reproducción de componente con fetch interceptado y reloj simulado | **Fallido diario / aprobado semanal** para esa propiedad: 1/0 intentos. |
| Navegación Firefox | Abrir las mismas pantallas | Runtime descargado | **No ejecutado**: fallo de inicio `spawn UNKNOWN`. |
| Planificación simultánea con entregas, checklist y fotos | Evitar esperas excesivas; preservar atomicidad y capacidad | Requiere copia/base aislada | **No ejecutado**: se inspeccionó el bloqueo; no se hizo carga automatizada sobre la simulación compartida. |
| Acumulación de meses de historial y cancelación de reportes | Latencia/memoria acotadas y trabajo cancelado | Requiere datos aislados de volumen conocido | **No ejecutado**: riesgo identificado por código; umbral sin medir. |
| CPU modesta | Observar carga inicial con ralentización | Chromium 4× en 320/390 px | **Aprobado para la muestra**; sin bloqueo observado. RAM y red no limitadas. |
| Dispositivos físicos, cámara, exportación, uso prolongado, red lenta, todos los roles/temas | Completar flujos y medir memoria, red y tarea principal | Requiere matriz adicional | **No ejecutado**; no sustituido por emulación de tamaño. |
| Producción futura | Verificar capacidad y comportamiento sobre su infraestructura | Aún no evaluada en este trabajo | **No ejecutado**; esta revisión no extrapola capacidad a producción. |

## Evidencia y reproducción local

- `artifacts/browser-audit-20261028/results.json`: visitas iniciales Chromium/WebKit, incluidas rutas exploratorias 404/403.
- `artifacts/browser-audit-20261028/supervision-results.json`: Validaciones/Carga en ambos motores.
- `artifacts/browser-audit-20261028/firefox-results.json`: causa de no ejecución.
- `artifacts/browser-audit-20261028/excel-amplification.txt`: medición del lector aislado.
- `artifacts/browser-audit-20261028/background.json`: reproducción controlada de visibilidad.
- Harness local: `.local/request-audit/Audit.csproj`; comando `dotnet run --project .local/request-audit/Audit.csproj -c Release`.
- Sondas de navegador locales: `.local/browser-audit-20261028.cjs` y `.local/background-audit.cjs`. Reutilizan una cuenta del fixture existente sin copiar sus credenciales a la evidencia. Los directorios `.local/` y `artifacts/` permanecen locales/ignorados.

Orden propuesto: contener Excel; medir y reducir contención del bloqueo; optimizar historial/consultas; reducir DOM y actividad oculta; repetir recorridos completos y ejecutar carga en copia aislada con métricas de latencia, esperas de BD, CPU, GC, memoria y errores. La cantidad de usuarios simultáneos y los dispositivos objetivo deben formar parte de los criterios antes de declarar preparación para producción.
