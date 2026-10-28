# Rendimiento: pruebas aisladas y plan de implementación

Fecha operativa: 28/10/2026, America/Mexico_City. Complementa la [auditoría inicial](auditoria-rendimiento-navegadores-2026-10-28.md).

## Conclusión

Se añadieron herramientas reproducibles y se ejecutaron cargas con tareas, entregas, fotos, acceso, consultas, historial y planificación. No hubo pérdida de las 120 entregas ni duplicación de las fotos verificadas. Sí se reprodujeron demoras importantes, amplificación de memoria y pausas largas del navegador. Las mediciones originales se conservan como referencia; la implementación y su validación posterior se registran al final de este documento.

Prioridades: contener Excel y el trabajo concurrente costoso; eliminar la demora de conexión al antivirus; corregir la cuota de acceso entre clientes; reducir la sección crítica del bloqueo compartido; paginar y proyectar las consultas antes de renderizar. El resultado no permite asignar una capacidad de usuarios de producción.

## Entorno e instrumentos

- Fixture nueva, inventada, en `sgol_task_checks_performance`, exclusivamente `localhost:5547`. No se copió ni alteró la simulación diaria.
- Contenedores propios `sgol-performance-*`: PostgreSQL 1 CPU/768 MiB; ClamAV 1 CPU/1536 MiB; almacenamiento 0,5 CPU/384 MiB. El host web de integración y el generador HTTP utilizan el equipo Windows sin límite de CPU propio.
- ClamAV lee la firma existente mediante volumen **de solo lectura**, pero analiza en otro contenedor y puerto 3347. La carga no se envió al escáner compartido 3310. El hardware/disco físico sigue compartido con el equipo de desarrollo.
- Host de integración HTTPS loopback 7267, servicios reales de persistencia, cookies, roles, MFA, CSRF, Razor, API y antivirus. Se utilizaron los registros y manejadores del producto; no es un despliegue del ejecutable de producción. El transporte LAN y sus cabeceras especiales no se validaron en este host.
- Se conservaron las validaciones funcionales y los límites de fotos. No se suprimieron reglas para hacer pasar pruebas.
- Las fotografías operativas de este recorrido se almacenan en PostgreSQL. El contenedor S3 aislado se levantó, pero esto **no valida** la carga legacy por URL firmada ni su almacenamiento completo.
- Métricas por intento: tiempo, estado, tamaño de respuesta; p50/p95/p99 de la muestra; CPU y asignaciones administradas del proceso; muestreo de memoria cada 100 ms. Recursos del proceso incluyen cliente de carga y servidor; no incluyen contenedores ni navegador. Las asignaciones acumuladas no equivalen a memoria residente máxima.
- La configuración de desarrollo produjo logs informativos durante la ejecución HTTP. Estos números no son una estimación directa de producción con otra configuración, recursos o red.

## Resultados ejecutados

Los percentiles de grupos de tres o de un intento son solamente resúmenes de esa muestra; su p95 coincide con su máximo y no estima un percentil poblacional confiable.

| Recorrido | Carga y resultado | Medición observada | Estado |
|---|---|---|---|
| Importación masiva | 1000 definiciones mediante `PreviewImportAsync/ImportAsync`; persistieron todas | 1,02 s, una ejecución | Aprobado en integridad; capacidad sostenida no determinada |
| Importación concurrente idéntica | Dos peticiones de servicio de 50 definiciones; una aceptada y otra `TASK_IMPORT_STALE`/409; quedaron 50 | Máximo 357 ms | Aprobado; no duplicó ni guardó parcialmente |
| Alta por formulario | 120 tareas, grupos de 40, concurrencia 1/8/24; todos 302 y 120 registros nuevos | p95 60/2187/2678 ms | Integridad aprobada; degradación bajo concurrencia |
| Entrega por formulario | 120 entregas, concurrencia 1/8/24; todos 302 y 120 entregadas | p95 62/132/457 ms | Aprobado para la muestra |
| Carga de fotos | 48 JPEG sintéticos de ruido, 1600×1200, doble ClamAV real; concurrencia 1/4/12 | p95 8,33/8,78/10,75 s | Sin pérdidas; rendimiento insuficiente para una interacción ágil |
| Reintento de foto | Seis envíos concurrentes con el mismo ID y bytes | Todos 200; p95 150 ms; ninguna foto adicional | Aprobado |
| Límite por sesión | 21 fotos con concurrencia 8; 20 aceptadas y una 422 | p95 9,76 s del lote | Aprobado en límite; no se rebajaron reglas |
| Adjuntar y repetir entrega | Adjuntar 16 fotos verificadas y repetir formulario | Dos 302; 16 adjuntas | Aprobado; conserva idempotencia de ese recorrido |
| Bloqueo compartido corto | Mantener deliberadamente la clave global 2 s y enviar entrega | 2,03 s, luego guardada | Contención reproducida |
| Bloqueo compartido prolongado | Mantener la clave 17 s y enviar entrega | 17,08 s, luego guardada | Contención reproducida; el timeout SQL configurado a 15 s no fue un plazo total efectivo |
| Reportes, historial creciente | 100/1000/3000 asignaciones históricas adicionales y 10/100/300 solicitudes de arrastre; tres GET por nivel | Máximos 270/531/2742 ms | Crecimiento reproducido |
| Supervisión/Validaciones, mismo historial | Tres GET por nivel, incluidos los arrastres pendientes | Máximos 388/491/2942 ms | Crecimiento reproducido |
| Lectura mixta | 24 GET, concurrencia 8, con historial 3000 | p95 4,91 s; 20 respuestas 200 y cuatro 403 por rol sin acceso a Tablero | Permisos conservados; excluir los 403 de una afirmación de consultas exitosas |
| Cancelación de reportes | Cuatro clientes cancelan a 50 ms | Los cuatro cancelaron; cero consultas SQL activas 1,2 s después | Parcial: no demuestra que todo el trabajo de CPU haya parado |
| Ráfaga de acceso API | 40 intentos, concurrencia 24, misma IP; dos accesos válidos previos de preparación | Ocho 401 por credencial inválida y 32 rechazos 429; p95 164 ms | Limitador activo; no tratar los 429 como caída |
| Formulario después de cuota agotada | 20 intentos Razor, concurrencia 12 | Renderiza error con HTTP 200 | Un 200 no equivale a autenticación exitosa |
| Cuota entre clientes distintos | Agotar loopback; origen 127.0.0.2 recibe 401 por API y “Demasiadas solicitudes” por Razor | Distintas particiones externas, pero reenvío Razor consume cuota interna | Fallo de aislamiento de cuota reproducido |
| Recuperación | Cinco GET de salud tras la carga | Todos 200; máximo 6,7 ms | Aprobado para recuperación inmediata |

La comprobación independiente final contó 68 fotos con 68 IDs distintos y 16 adjuntas. Al finalizar la fase HTTP había 1178 definiciones y 3164 asignaciones sintéticas. Los ensayos posteriores de planificación agregaron más definiciones únicamente en esa misma base aislada; no deben confundirse sus conteos con los de la fase HTTP.

El proceso de integración alcanzó unos **666 MiB de working set muestreado** y **254 MiB administrados muestreados** durante esa ejecución. Son máximos muestreados del proceso combinado, no un dimensionamiento de RAM de producción. El escáner separado utilizó alrededor de 1 GiB en una observación puntual, principalmente para su propia operación/firma.

### Excel y cancelación

Se probaron los métodos reales con archivos inventados, sin HTTP ni base:

- Ocho Excel normales y ocho fotos a concurrencia 1/4 completaron.
- Un Excel con 500 referencias a la misma hoja generó aproximadamente 215 MiB de asignaciones administradas acumuladas. Cuatro simultáneos generaron aproximadamente **859 MiB**, completando en torno a un segundo. No es memoria residente simultánea ni prueba de caída.
- Un token ya cancelado impidió el trabajo de Excel/foto correctamente.
- Cancelar Excel a los 20 ms durante el parseo repetido **no detuvo** ese parseo: completó después de la cancelación, en unos 343–371 ms, asignando aproximadamente 215 MiB.

Fuentes: `OperationalExcel.ReadAsync` y `PhotographNormalizer.NormalizeAsync`; pruebas mantenibles en `PerformanceMediaChecks.cs`. El resultado supera la hipótesis de la auditoría inicial: la cancelación durante parseo también quedó reproducida.

### Separación de costos del antivirus

Con el mismo JPEG 1600×1200 de la prueba HTTP, métodos reales y orden alternado:

| Destino del mismo contenedor | Primer análisis | Normalización | Segundo análisis |
|---|---:|---:|---:|
| localhost, ronda 1 | 2078 ms | 92 ms | 2047 ms |
| IPv4 explícita, ronda 1 | 13 ms | 79 ms | 12 ms |
| IPv4 explícita, ronda 2 | 12 ms | 79 ms | 13 ms |
| localhost, ronda 2 | 2030 ms | 85 ms | 2050 ms |

Todos devolvieron `Limpio`. El control sin archivos midió conexión `localhost` en 2038 ms; `127.0.0.1` en 1,8 ms; `::1` falló a los 2032 ms. `localhost` resolvió primero `::1` y luego IPv4, mientras el contenedor solo publicaba IPv4. Esto es consistente con espera del intento IPv6 antes de recurrir a IPv4. Confirma demora de **conexión**, no de decodificación de la foto.

Ese costo explica aproximadamente cuatro segundos por fotografía por los dos análisis, pero no se atribuyen los ocho segundos HTTP completos a esa sola causa. Una sonda adicional de `NoDelay` no dio una mejora consistente al alternar orden: no hay evidencia para cambiar esa opción como solución principal.

### Navegador con catálogo e historial grandes

24 visitas reales: Tareas, Reportes y Supervisión, Chromium/WebKit, 1366/390 px, claro/oscuro; menú móvil y Escape. No hubo errores JavaScript/console capturados ni desbordamiento horizontal; las capturas seleccionadas también se inspeccionaron visualmente.

**Chromium móvil** usó CPU 4×, red de 1,6 Mbps, latencia 150 ms y subida de 0,75 Mbps:

| Pantalla | DOM móvil observado | HTML recibido sin contar otros recursos | Carga observada | Mayor tarea larga observada |
|---|---:|---:|---:|---:|
| Tareas | 27798 nodos | 1,22 MB | 9,5–10,1 s | 2,9 s |
| Reportes | 28995 nodos | 1,46 MB | 13,2–14,7 s | 3,8 s |
| Supervisión/Validaciones | 78836 nodos | 3,86 MB | Hasta 30,0 s | 6,34 s |

`loadEventEnd` se usa para la carga, no el tiempo total de la sonda que incluye inspecciones y capturas. El DOM y las tareas largas muestran pausas reales bajo la emulación indicada; no son una medición de un teléfono físico. La página de Supervisión también renderiza las 300 peticiones de arrastre: paginar solo la tabla de asignaciones no basta.

WebKit se probó en los mismos tamaños/temas **sin** la limitación de red/CPU de CDP; no comparar sus tiempos como si las condiciones fueran iguales. No todos los motores exponen `longtask`: cero entradas allí no certifica ausencia de pausas. En la primera sonda hubo observadores de tareas largas repetidos; se usa el **máximo**, no el número de entradas. La herramienta quedó corregida para registrar un solo observador por página.

Un benchmark de planificación independiente agregó 200 definiciones durante la exploración inicial de escritorio: las respuestas de Tareas variaron de 1,04 a 1,22 MB. No usar esas muestras para comparar temas o atribuir diferencias exclusivamente al ancho. La guía reproducible indica ejecutar primero las cargas que agregan catálogo y luego mantenerlo constante.

Firefox volvió a fallar al iniciarse con `spawn UNKNOWN`: no se declara probado.

### Planificación anual

Se añadió una prueba con horario sintético de un trabajador de piso, sin guardar asignaciones:

- Cinco definiciones semanales y una mensual: **277 asignaciones de vista previa**, en 3,74 s y aproximadamente 270 MiB de asignaciones administradas acumuladas. No hubo pendientes ni persistencia de asignaciones.
- 50 semanales + 3 mensuales y 175 + 10: `SCHEDULING_SEARCH_INCONCLUSIVE`. No se presenta ese resultado como plan satisfactorio ni como una caída del proceso. Falta perfilar el presupuesto de búsqueda y las restricciones que lo agotaron para ese fixture.
- 190 semanales + 10 mensuales: rechazo `PLAN_TOO_MANY_OCCURRENCES`, sin persistencia. La expansión de semanas completas lleva el caso más allá de 10000; no se desactivó el límite.
- El antiguo `--scheduling-benchmark`, que esperaba exactamente 10000 para el último caso, también se detuvo por ese límite. Es una expectativa de fixture que debe ajustarse, no evidencia para quitar la protección del generador.

## Plan de implementación propuesto

Alcance original de la implementación (estado actualizado al final). Clasificación mixta: servidor, consultas, concurrencia, errores y UI. Mantener roles, CSRF, MFA, evidencias, capacidad, dependencias, criticidad, historial e idempotencia; no resolver rendimiento eliminando validaciones.

### 1. Contener Excel y admisión de trabajo costoso — prioridad alta

Archivos: `OperationalExcel.cs`, ruta de Excel en `OperationalTaskService.cs`, middleware/admisión antes del binding de cuerpos grandes y `PerformanceMediaChecks.cs`.

1. Validar identidades, relaciones y destinos de hojas. Procesar una entrada como máximo una vez y rechazar referencias incompatibles/duplicadas inválidas.
2. Aplicar presupuestos acumulados de hojas, bytes XML y trabajo de parseo, además de los límites ZIP ya existentes. No usar únicamente el tamaño comprimido.
3. Introducir parseo que observe cancelación durante la lectura/recorrido XML. El token precancelado no basta como regresión.
4. Acotar concurrencia y cola antes de reservar grandes buffers; cancelación de espera y respuesta 429/503 con `Retry-After` cuando se alcance la capacidad. Límites iniciales conservadores, configurables y medidos con las mismas cargas.
5. Rechazos con causa comprensible y sin escrituras parciales; mantener malware, validación técnica y permisos.

Validación: archivo normal de varias hojas, destinos repetidos, cuatro archivos amplificados, cancelación a mitad del parseo, cola llena, archivo inválido y reintento; verificar cero evidencia/entregas parciales y memoria acotada.

### 2. Corregir conexión y medir etapas de fotos — prioridad alta

Archivos: configuración del escáner en scripts/entorno y despliegue, `ClamAvScanner.cs`, `OperationalTaskService.Camera.cs`, `PerformanceScannerChecks.cs`.

1. Usar un destino compatible con la publicación real del servicio: IPv4 explícita cuando solo hay IPv4, o publicación/resolución IPv6 coherente. Verificar cada entorno; no sustituir ciegamente nombres de servicios de Docker por direcciones del host.
2. Instrumentar carga/binding, validación, primera conexión/análisis, normalización, segundo análisis, espera de BD y guardado para explicar la demora HTTP restante.
3. Mantener ambos análisis y el límite de dos normalizaciones. Añadir admisión acotada a la carga total, no solo a la decodificación; cancelar desconexiones y no retener colas ilimitadas con archivos en memoria.
4. Conservar 20 fotos, 30 MiB por sesión, hash, reintentos, aislamiento por autor y limpieza de temporales. No rebajar controles para mejorar un cronómetro.

Validación: mismas 48 cargas y 21 ante límite, reintento idéntico/distinto, escáner caído/lento, cancelación, adjuntar y recargar; medir memoria nativa además de GC y recuperación sin huérfanos.

### 3. Corregir cuotas de acceso y estado de errores — prioridad alta

Archivos: `HostedAuthenticationInfrastructure.cs`, `Interface/ApiClient/SgolApiClient.cs`, `RazorAntiforgeryBridge.cs`, `Pages/Access/Index.cshtml.cs`, sonda `performance-login-partitions.cjs`.

1. Aplicar la cuota del formulario al cliente original antes de reenviar, y definir cómo la API distingue llamadas internas verificadas de llamadas externas. Mantener una defensa global contra costo de hash y protección por cuenta/IP, considerando usuarios tras NAT.
2. No confiar en un `X-Forwarded-For` arbitrario: utilizar el proxy validado y un contexto interno verificable, o resolver la llamada dentro del proceso conservando las mismas reglas de autenticación. Elegir la alternativa después de revisar cookies, CSRF y auditoría de extremo a extremo.
3. Conservar MFA, protección de enumeración y bloqueo por cuenta; no desactivar el limitador de la API para “arreglar” el problema.
4. Distinguir observabilidad de éxito de formulario y de autenticación. Evaluar devolver 429 y `Retry-After` también desde Razor al renderizar el mensaje, con regresión de sus consumidores.

Validación: dos IP distintas, NAT compartido, varias cuentas, API directa y Razor, CSRF inválido, IP falsificada, contraseña/MFA incorrectas, cola llena y acceso correcto después de recuperar cuota. La regresión debe comprobar contenido y resultado de acceso, no solo HTTP 200.

### 4. Reducir contención y hacer explícito el presupuesto de espera — prioridad alta

Archivos: `OperationalTaskService.LockAsync`, servicios de catálogo/entrega/fotos/checklist, `Sequences/ProcessPlanningService.cs` y planificación/capacidad.

1. Medir espera y duración del bloqueo por operación. Separar cálculos de vista previa de la transacción; revalidar versión/fingerprint dentro de la sección crítica antes de guardar.
2. Acortar la sección crítica; separar catálogo de ejecución cuando sus invariantes lo permitan. Estudiar bloqueos ordenados por asignación/personas implicadas y una coordinación específica de capacidad. Una partición solo por sucursal no mejora concurrencia dentro de la sucursal única actual.
3. Establecer un plazo explícito y comprobable para adquirir el bloqueo, cancelable. No depender de que `CommandTimeout=15` imponga por sí solo un plazo total de petición.
4. Responder “operación ocupada” con entidad y acción comprensibles. Reintentar escrituras únicamente con intención/idempotencia válida; conservar atomicidad, reservas y dependencias.

Validación: planificación real simultánea con entrega/checklist/fotos; bloqueos artificiales 2/17 s; cancelación y caída a mitad; dos operaciones sobre la misma asignación y sobre asignaciones independientes; ausencia de sobrecapacidad, duplicación o cambios parciales. El ensayo artificial ya ejecutado no sustituye la combinación con un plan real.

### 5. Optimizar consultas, arrastres y renderizado inicial — prioridad alta

Archivos: `OperationalCarryoverReader.cs`, `OperationalSupervisionService.cs`, `Pages/Reports/Index.cshtml.cs`, `Pages/Validations/*`, `Pages/Tasks/*`, `Pages/MyWork/*`, `wwwroot/js/mobile.js` y scripts de reportes.

1. Deserializar solicitudes y resultados una vez por petición y crear índice `AssignmentId → CarryoverTiming`; consultar auditoría solo para asignaciones necesarias y propagar cancelación. Preservar vencimientos de referencia, restauraciones y autorizaciones históricas.
2. Aplicar filtros, proyecciones y paginación en SQL antes de materializar. Orden estable con ID como desempate. Revisar índices y planes `EXPLAIN` de las consultas realmente medidas antes de añadir índices indiscriminadamente.
3. Paginar también solicitudes de arrastre, catálogo y listas auxiliares. Cargar detalle y galerías al abrir; no generar miles de controles ocultos ni todo el PDF/exportación en la carga inicial.
4. Agregar métricas/gráficas en servidor. Mantener exportación completa autorizada como operación explícita, cancelable y acotada, con descarga privada cuando corresponda.
5. Conservar opciones de filtros históricas mediante carga separada/búsqueda, sin cambiar silenciosamente su alcance al periodo visible. Mantener búsqueda sin acentos/mayúsculas, filtros, teclado, foco y acceso a registros históricos.
6. Reutilizar componentes/tokens vigentes; no crear una presentación paralela. Suspender trabajo del tablero cuando está oculto y hacer una actualización al regresar; conservar quiosco visible y recuperación de sesión.

Validación: 100/1000/3000 y después un volumen mayor fijado; resultados idénticos a la consulta completa, cambio de página durante actualizaciones, permisos, fechas, arrastres restaurados, exportación íntegra y cancelada, vacío/error; 320/390/1366 px y ambos temas con CPU/red iguales a la prueba actual.

### 6. Perfilar planificación y consolidar regresiones — prioridad media/alta

1. Desglosar generación de ocurrencias, disponibilidad, candidatos, reservas, búsqueda, backtracking, fingerprint y serialización; medir por volumen y combinaciones de restricciones.
2. Investigar por qué los fixtures anuales mayores devuelven `SCHEDULING_SEARCH_INCONCLUSIVE` pese a finalizar pronto. Registrar la causa del presupuesto agotado; mejorar orden de candidatos, reutilización de disponibilidad y poda antes de considerar cambiar límites.
3. Mantener el límite de ocurrencias y el rechazo sin persistencia. Ajustar el benchmark antiguo a la expansión real de semanas; no subir el límite para aprobarlo.
4. Convertir las reproducciones en regresiones mantenibles con criterios explícitos. Separar una suite rápida de integridad de diagnósticos de volumen y pruebas de capacidad prolongadas.
5. Instrumentar peticiones canceladas hasta que termine realmente el servidor, tiempos de cola, conexiones/pool, bloqueos, GC, memoria nativa y cola del antivirus. No añadir datos privados ni IDs por entidad como etiquetas de métricas sin control de cardinalidad.

Validación: fixture pequeño ya exitoso, casos mayores inconclusos, límite de 10000, restricciones de calendario/persona/especialidad, falta real de capacidad, cancelación durante CPU y concurrencia con entregas, manteniendo todos los invariantes de agenda.

## Criterios propuestos para aceptar las correcciones

Objetivos iniciales para el **mismo fixture y recursos**, pendientes de calibrar con usuarios/dispositivos e infraestructura objetivo. No son promesas ya verificadas:

| Área | Criterio propuesto |
|---|---|
| Integridad | Cero pérdidas, duplicados por reintento y cambios parciales; ningún acceso ajeno; permisos/MFA/antivirus vigentes |
| Saturación | Rechazo acotado y comprensible con reintento; colas limitadas; salud y entrega ligera recuperables sin reiniciar |
| Entregas | p95 menor de 1 s a concurrencia 8 sobre este fixture, sin bloqueo por cálculo de planes fuera de la sección crítica |
| Listados | Primera página paginada menor de 2 s de servidor con historial 3000; respuesta y DOM inicial no proporcionales al historial completo |
| Móvil | Primera vista útil menor de 5 s y sin pausas de varios segundos bajo CPU4×/1,6Mbps; medir interacción, no solo evento load |
| Fotos | Repetir exactamente los tamaños/bytes del diagnóstico; eliminar espera de conexión reproducida; definir p95 por etapa y por concurrencia tras medirla, sin suprimir los dos análisis |
| Cancelación | Trabajo de servidor termina dentro de un presupuesto explícito después de desconexión; verificar con instrumentación del servidor, no solo con timeout cliente |
| Memoria | Medir pico residente/nativo y estabilización en uso prolongado; la admisión debe mantener un presupuesto definido por despliegue |
| Navegadores | Chromium/WebKit con escenarios reales; resolver el entorno de Firefox y comprobar Safari/iOS/Android físicos antes de declarar cobertura amplia |

## Reproducción y evidencia

Guía y comandos: `tools/Sgol.TaskChecks/README.performance.md`. Nuevos archivos: `PerformanceLoadChecks.cs`, `PerformanceMediaChecks.cs`, `PerformanceScannerChecks.cs`, `PerformancePlanningChecks.cs`, contenedores/configuración y sondas de navegador/cuotas. `Program.cs` solo incorpora sus puntos de entrada; se conservaron los cambios previos ajenos a este trabajo.

Evidencia local ignorada:

- `artifacts/performance-20261028/20261028-194856/load-results.json`: peticiones, integridad y recursos del lote HTTP.
- `artifacts/performance-media/performance-media.json`: repetición de medios mediante su punto de entrada final; confirma cancelación ignorada y unas 859 MiB de asignaciones del lote amplificado.
- `artifacts/performance-scanner/`: protocolo, hosts y control de conexión.
- `artifacts/performance-browser/results.json` y capturas: 24 visitas; Firefox no ejecutado.
- `artifacts/performance-login-partitions/results.json`: API 401 frente a bloqueo visible del formulario desde origen distinto.
- `artifacts/performance-planning/planning-results.json`: caso exitoso, búsqueda inconclusa y límite; ninguna asignación guardada por las vistas previas.
- `.local/performance-*.log`: ejecución, compilación y observación del benchmark anterior. No publicar estos directorios, las claves ni las credenciales sintéticas.

Compilación Release de Web/TaskChecks aprobada, sin advertencias ni errores; sintaxis de sondas Node y script PowerShell comprobada. La suite HTTP principal se ejecutó una vez; las sondas pequeñas se repitieron al añadir controles de conexión/cancelación y corregir la instrumentación. No se presenta una reproducción de medios como una subida HTTP ni un HTTP 200 de login como acceso correcto.

## Pendientes que estos ensayos no cierran

- Firefox no inicia en este entorno. Teléfonos físicos, Safari real, permisos de cámara y condiciones térmicas/memoria reales siguen pendientes.
- No se ejecutó una jornada completa, una prueba prolongada de estabilidad ni carga sobre la infraestructura futura. Las ráfagas acotadas no dimensionan producción.
- Falta instrumentar el fin real de CPU tras cancelar reportes, completar el recorrido legacy S3 y medir la planificación real simultánea con las otras operaciones. Hay evidencia de bloqueo, pero el titular artificial no mide la duración de un optimizador real.
- Estado de implementación, comparación y límites: ver el cierre de esta ejecución al final. Las pendientes de hardware y estabilidad no se cierran por compilar o por estas ráfagas.


## Implementación y validación posterior — 28/10/2026

Cambio mixto. Se inspeccionaron `AGENTS.md`, protocolo, referencia visual vigente, estado y diff antes de editar. Se preservaron los cambios previos de checklist, su parcial de definición, sus pruebas y los instrumentos originales de rendimiento. Sin commit, push, despliegue, reinicio ni migración de la simulación compartida. El proceso 10448 seguía sirviendo 7240/7440; los dos accesos corresponden a la misma aplicación. Las escrituras y cargas de esta ejecución se dirigieron exclusivamente al contenedor `sgol-performance-postgres-1`, puerto 5547, y a las dependencias dedicadas 3347/8347.

### Cambios de producto implementados

- **Excel:** `OperationalExcel.cs` valida unicidad de IDs, nombres, relaciones y destinos canónicos antes de analizar hojas; rechaza destinos externos y duplicados; presupuesto de XML acumulado de 60 MiB y metadatos de 4 MiB. Las hojas se recorren con lector XML sin crear un DOM de celdas, observando cancelación por nodo. Se mantienen DTD prohibida, límites originales de archivo/ZIP y análisis antivirus posterior. Admisión del lector: dos trabajos y cuatro esperas, con espera cancelable de cinco segundos.
- **Admisión antes del binding:** `PerformanceWork.cs` y middleware en `Program.cs`; cargas multipart 4/8, reportes 2/4 y login API 4/8 (activos/cola), configurables mediante `Performance:{uploads|reports|login}:{Concurrency|Queue}`. Espera máxima de cinco segundos; saturación 503 y `Retry-After: 5`. Los servicios añaden guardas para llamadas sin HTTP: fotos 4/8 y planificación 2/4. Las guardas de normalización, tamaño, 20 fotos y 30 MiB permanecen vigentes. El cliente de fotos entiende también el error `application/problem+json` y conserva la recuperación explícita por el mismo ID.
- **Fotos y transporte:** `scripts/local-env.ps1` usa IPv4 para PostgreSQL y ClamAV, coherente con sus puertos Docker publicados en IPv4. El harness valida primero su destino exacto y usa IPv4 para la conexión efectiva a la misma base. No se sustituyen los nombres internos de Docker ni se cambia la configuración de otros despliegues. Se conservan ambos análisis antivirus.
- **Validación JPEG:** la instrumentación descubrió que la lectura de entropía byte a byte consumía aproximadamente 4,8 s en validación técnica. `FileTechnicalValidator.cs` ahora lee bloques y conserva los marcadores, bytes de relleno, reinicios, dimensiones y fin exacto del archivo; observa cancelación entre bloques. La comparación diferencial con el ensamblado anterior y las pruebas de cámara verifican que no se relajó la validación.
- **Autenticación:** `InternalLoginOrigin.cs`, `SgolApiClient.cs` y el limitador conservan la partición del cliente original mediante una capacidad aleatoria, interna, de un solo uso, válida como máximo 30 s y eliminada al terminar la llamada. No se usa `X-Forwarded-For` arbitrario para trasladar direcciones. El transporte de proxy conocido conserva sus verificaciones existentes. La API sigue limitada y el costo de hash tiene admisión global. Razor renderiza los rechazos como 429/503 con reintento; MFA, cookies, CSRF, bloqueo por cuenta y errores de credenciales se conservan.
- **Bloqueo y planes:** `OperationalTaskService.LockAsync` conserva la clave global y exige transacción, con `lock_timeout` de PostgreSQL a cinco segundos y cancelación. No se elimina la coordinación de capacidad. El cálculo de planes de Dirección pasa fuera del bloqueo: se reutiliza la agenda calculada y se vuelve a leer el fingerprint de versiones, capacidad, personas, calendarios, reservas y fecha efectiva actual bajo el bloqueo antes de guardar. Se mantienen reintento idempotente, rechazo por datos vencidos y transacción atómica. `ProcessPlanningService` calcula antes de adquirirlo y reutiliza la propuesta al revalidar versiones y capacidad. Activación y otras escrituras que todavía necesitan coordinación conservan el bloqueo.
- **Consultas e historial:** `OperationalCarryoverReader` construye un índice por asignación una vez, deserializa cada solicitud una vez, limita las auditorías al conjunto solicitado y propaga cancelación. Conserva restauraciones, entregas sin cambios y el plazo anterior a actualizaciones de definición. Tareas, Supervisión, Mi trabajo y el detalle de Reportes paginan en SQL, con desempate estable. Pendientes e historial de arrastre se autorizan y paginan en SQL, diez por página; las listas operativas muestran cincuenta. Los filtros conservan opciones históricas; búsquedas sin acentos/mayúsculas se aplican a todo el resultado. La migración `20261028220000_PerformanceSearch` añade la función de búsqueda, sin reescribir registros históricos; solo se aplicó en bases aisladas.
- **Renderizado:** parcial compartido `_ListPage`, navegación con GET, búsqueda global del detalle de Reportes, exportación completa autorizada bajo petición explícita y cancelable durante su descarga de datos; se retira el JSON de exportación del HTML inicial. El progreso diario de Mi trabajo se agrega en SQL sobre todas las páginas, y el detalle se autoriza independientemente de la página visible. Se conserva el estado de búsqueda al abrir evidencia. El tablero diario evita refrescos y coloreado mientras está oculto y consulta al volver.
- **Planificación y métricas:** caché de elegibilidad de trabajadores y de huellas de agenda; los límites de ocurrencias, nodos, candidatos y tamaño de búsqueda se mantienen. Los rechazos inconclusos indican si se agotaron nodos, candidatos o el tamaño admitido. Instrumentos `Sgol.Performance` sin etiquetas de usuario/archivo/asignación: cola, activos, rechazos, espera y retención de bloqueo, indexación de arrastres, generación, validación, normalización, conexiones, dos análisis y persistencia. La instrumentación de retención distingue la identidad EF de transacciones que el driver reutiliza tras una disposición implícita; no atribuye intervalos ociosos al siguiente bloqueo. Retención se observa para commits y rollbacks explícitos.

### Comparación HTTP real con el mismo fixture

Referencia repetida antes de editar: `artifacts/performance-20261028/20261028-202743/load-results.json`. Resultado posterior completo: `artifacts/performance-20261028/20261028-205959/load-results.json`. Misma máquina, límites de contenedores, tamaños/bytes sintéticos, volúmenes y concurrencias. Ambas fases terminan con **1178 definiciones, 3164 asignaciones y 68 fotos**; cero códigos/IDs duplicados. Las pruebas posteriores de integridad, planificación y navegador añaden sus propios registros y no se mezclan con estos conteos. Hardware compartido y muestras acotadas: no son una estimación de capacidad de producción ni una comparación estadística controlada.

| Escenario | Antes | Después | Resultado e interpretación |
|---|---:|---:|---|
| Alta, c8 / c24, p95 | 2185 / 2647 ms | 211 / 999 ms | 120 formularios y registros, sin pérdidas |
| Entrega, c8 / c24, p95 | 166 / 613 ms | 176 / 443 ms | 120 entregas; c8 prácticamente equivalente en estas muestras, no atribuir una mejora que no ocurrió |
| Fotos idénticas, c1 / c4 / c12, p95 | 13881 / 9756 / 14696 ms | 326 / 455 / 1362 ms | 48 aceptadas con doble ClamAV; seis reintentos sin foto adicional |
| Límite de 21 fotos, c8, p95 | 14739 ms | 796 ms | 20 aceptadas y una 422; no se aumentó la cuota |
| Adjuntar 16 y repetir, máximo de dos | 838 ms | 99 ms | Las 16 adjuntas una sola vez |
| Bloqueo mantenido 17 s | 17100 ms, guardaba | 5375 ms, 503 | Rechaza sin completar; reintento tras liberar: 37 ms y guardado. Es un cambio de respuesta esperado, no una entrega exitosa a los cinco segundos |
| Reportes, historial 3000, máximo de tres | 5828 ms; 1464358 bytes | 147 ms; 56555 bytes | Primera página; indicadores completos del filtro, exportación explícita |
| Supervisión, mismo historial | 2805 ms; 3859262 bytes | 405 ms; 113162 bytes | También se paginan los arrastres |
| Lecturas mixtas, 24 a c8, p95 | 7223 ms | 801 ms | En ambos: 20 respuestas 200 y cuatro 403 correctos por rol |
| Cuota agotada, formulario | 20 errores con 200 | 20 errores con 429 | No son autenticaciones exitosas |
| Salud tras carga, máximo de cinco | 17,5 ms | 6,8 ms | Cinco 200, sin reiniciar |

La importación de 1000 definiciones pasó de 977 a 1197 ms y la carrera de dos importaciones de 340 a 489 ms en estas ejecuciones. Se conserva una aceptación y un 409, sin duplicados; **no se presenta como mejora de esas operaciones**. Working set máximo muestreado del proceso combinado: aproximadamente **704 → 588 MiB**; administrado muestreado **237 → 197 MiB**. Incluyen servidor y generador HTTP, no contenedores ni navegador. No confundir con asignaciones acumuladas ni con pico nativo garantizado.

### Etapas y diagnósticos separados

- Excel amplificado de 500 referencias: ahora 422; cuatro simultáneos asignan alrededor de 4,2 MiB acumulados frente a unos 859 MiB anteriores. Los normales completan. La prueba antigua de cancelar el duplicado ya no mide parseo porque este se rechaza antes: la regresión nueva cancela un libro válido después de observar 256 nodos reales, y exige `OperationCanceledException`.
- JPEG 1600×1200 idéntico al HTTP: validación diferencial **3769 → 14 ms** en una ejecución; once casos con el mismo resultado estructural, incluidos finales truncados, bytes después del cierre y marcadores que cruzan el bloque. `artifacts/performance-jpeg/differential.json`.
- Etapas de fotos en el lote HTTP final, p95: validación técnica 14,5 ms; primer análisis 30,3 ms; normalización 176,7 ms; segundo análisis 42,2 ms; prevalidación/BD 115,1 ms; persistencia final 128,3 ms. Son distribuciones separadas, **no se suman sus p95**. La cola de admisión explica parte de la diferencia entre tiempo de servicio y petición a c12. La métrica de conexión agrega también controles deliberados con localhost: su máximo de 2 s no representa el destino IPv4 corregido.
- Cancelación real de PostgreSQL sobre el mismo contenedor: `pg_sleep` cancelado a 50 ms finalizó alrededor de 2096 ms mediante localhost y 67 ms mediante IPv4. Después de corregir el transporte, cuatro cancelaciones HTTP de Reportes terminaron el pipeline del servidor antes de la comprobación de 1200 ms; activos de reportes, login y carga quedaron en cero al cerrar el lote. No se usa solo la desaparición del cliente como evidencia.
- Planificación: 277 asignaciones de vista previa en 3,71 s, sin guardar. Los casos de 2686 y 9395 ocurrencias siguen inconclusos por **OCCURRENCE_SEARCH_SIZE_LIMIT=1000**, antes de explorar nodos; no prueban falta de capacidad. El caso 190/10 rebasa 10000 y se rechaza. Se conservan estos límites y se ajusta la expectativa errónea del benchmark antiguo. No se declara resuelto el escalado de esos planes anuales.

### Matriz de validación posterior

| Escenario | Prueba / entorno | Estado y evidencia |
|---|---|---|
| Excel de tres hojas únicas, duplicado de 500, cancelación durante XML válido, cola llena/cancelada/recuperada | Métodos reales; archivos de memoria, sin HTTP/BD | **Aprobado**: `--performance-regressions`, `.local/performance-regressions.log` |
| JPEG normal, malformado, límites de marcadores entre bloques, contenido disfrazado | Ensamblado anterior frente al nuevo, más cámara/normalización | **Aprobado**: once casos diferenciales y `Sgol.CameraChecks`; no equivale a cámara física |
| Antivirus limpio/infectado/error y metadatos | ClamAV real dedicado 3347 para limpio/EICAR; fallo cerrado y pipeline en componentes | **Aprobado**: `.local/performance-camera.log`; almacenamiento en memoria de ese componente no certifica S3 legacy |
| Importación firmada concurrente; formularios de alta/entrega y MFA/CSRF | Servicios reales y HTTP Razor/API con BD dedicada | **Aprobado**: lote completo antes/después, 120/120 y carrera 200/409 |
| Fotos 48, reintento igual, límite 20, adjuntar 16, repetir | HTTP con PostgreSQL y doble ClamAV reales | **Aprobado**: 68 IDs distintos, 16 adjuntas; límite 422 intacto |
| Saturación durante iteraciones con validación lenta | HTTP con admisión y reintento explícito de mismos IDs | **Aprobado**: 503 acotados y recuperación sin duplicados; resultados de esas iteraciones se conservan y no se mezclan con el lote final optimizado |
| Bloqueos 2/17 s, rechazo sin entrega parcial y reintento | HTTP y bloqueo artificial dedicado | **Aprobado**: 302 / 503, posterior 302; el titular artificial no representa un optimizador real |
| Catálogo + capacidad cambian después del cálculo; entrega simultánea; fecha operativa cambia | Servicios reales, listener de etapas como barrera entre cálculo y lock, BD dedicada | **Aprobado**: `PLAN_CAPACITY_STALE`, sin plan parcial; entrega en unos 137 ms durante preparación; plazo/autor conservados; fecha efectiva revalidada |
| Plan fresco, replay, capacidad/calendario/dependencias, búsqueda limitada | Servicios, agenda y oráculos de reglas | **Aprobado**: guardado/replay sin duplicación, 25 casos contra oráculo; los anuales grandes continúan inconclusos explícitamente |
| Páginas SQL de catálogo, Supervisión, Mi trabajo y arrastres; búsqueda histórica | Comparación de todos los IDs y orden; prueba de detalle fuera de página | **Aprobado**: `.local/performance-integrity.log`; rol de piso rechazado en Supervisión |
| Progreso diario completo pese a paginación; arrastres restaurados, entregas sin cambios y plazo actualizado | Agregación contra lectura completa y fixture histórica dentro de transacción revertida | **Aprobado**: mismos conteos y referencias; no se modificaron las asignaciones de la fixture histórica |
| Cancelación de reportes hasta terminar servidor, recuperación de salud | HTTP, SQL e instrumentos del servidor | **Aprobado** para la ráfaga medida y presupuesto observado; no certifica cancelación de toda futura exportación/CPU cliente |
| Cuotas por cliente, NAT compartido, cabeceras falsificadas y CSRF | Sonda HTTP desde orígenes loopback distintos | **Aprobado**: `artifacts/performance-login-partitions/results.json`; origen nuevo conserva cuota, API/Razor de una IP la comparten, cabecera/ticket forjados no evaden 429 y CSRF ausente devuelve 400 |
| Pantallas reales y recorridos de paginación/búsqueda/detalle/exportación | Chromium/WebKit; 1366/390/320, claro/oscuro, CPU/red iguales antes/después | **Aprobado**: 48 visitas antes y 48 después, cero errores finales/overflow; `artifacts/performance-browser-{before,after}/results.json` y recorridos finales en `artifacts/performance-browser-flows/results.json` |
| Tablero oculto y regreso | Sonda de componente con script real, tiempo/visibilidad simulados y fetch interceptado | **Aprobado como componente**: cero solicitudes oculto y una al volver; no certifica una pestaña física ni carga real |
| Firefox, cámaras/teléfonos/Safari físicos, jornada e infraestructura futura | Fuera de la cobertura alcanzada por estas sondas | **No ejecutado**; permanecen pendientes |

### Comparación de navegador y recorridos finales

Fixture idéntica entre los dos recorridos: 1824 definiciones, 3326 asignaciones totales, 3324 en el período del reporte y 80 propias por cada cuenta sintética. Los conteos finales son mayores porque una regresión posterior añadió una definición y una asignación futura. Cada comparación contiene 48 visitas reales: Chromium/WebKit × 1366/390/320 × claro/oscuro × cuatro rutas. Autenticación con contraseña, enrolamiento MFA y páginas autorizadas; no se infiere éxito de un 200 del formulario de acceso. Chromium móvil usa CPU4× y red1,6Mbps/150ms en ambas versiones; WebKit no dispone de ese mismo control CDP, por lo que se compara solo dentro de su motor. Los rangos siguientes cubren las cuatro visitas móviles Chromium de cada ruta; son navegación, no p95 HTTP.

| Pantalla | Navegación antes → después | Elementos DOM antes → después | Bytes iniciales antes → después |
|---|---|---:|---:|
| Tareas | 11,61–12,22 s → 0,61–0,83 s | 32720 → 1239 | 1430674 → 63004 |
| Reportes | 14,18–14,65 s → 0,57–0,63 s | 30444 → 983 | 1537174 → 57306 |
| Supervisión | 30,68–31,10 s → 1,37–1,68 s | 82279 → 3190 | 4019936 → 166765 |
| Mi trabajo | 4,45–5,10 s → 0,79–0,96 s | 3272 → 2126 | 169877 → 112768 |

Cero errores de página/consola y cero desbordamientos en la matriz final. Un primer recorrido posterior detectó un aviso ResizeObserver en WebKit: se conserva en `artifacts/performance-browser-after-initial.json`, se corrigió el movimiento reiterado de nodos mediante RAF agrupado y se repitió la matriz. Las correcciones finales de cabecera, foco y filtro móvil se verificaron después mediante recorridos específicos en ambos motores; no se atribuyen a una tercera matriz completa cronometrada.

`artifacts/performance-browser-flows/results.json` verifica teclado, menú/Escape, páginas 2 de listas y arrastres, exportación completa de 3324 filas, búsqueda global sin resultados, detalle fuera de la primera página y formulario anual real con diagnóstico de 2686/1000. Las tarjetas reales de revisión muestran 50 elementos por página en PISO_VENTAS, incluida la segunda página; formulario vacío devuelve 422 sin cambios y sin CSRF devuelve 400. Cambiar escritorio→320→escritorio→390 conserva filas, búsqueda y foco. Se inspeccionaron capturas reales de ambas anchuras móviles y temas; el nombre largo no se superpone al logo y la búsqueda de Mi trabajo permanece accesible. No son pruebas de dispositivos físicos.

### Regresión final de procesos y resultados fallidos conservados

`.local/performance-process-final.log`, base nueva `sgol_task_checks_performance_process_final_20261028`: selección automática compatible con capacidad/calendario, personas revisadas estables, preparación y activación idempotentes, publicación concurrente real mediante servicios de catálogo entre cálculo y lock, rechazo 409 sin plan/inicios parciales, vista previa fresca y activación. También ejecuta el escenario vigente de agenda automática: reservas persistidas, política capturada, activación diferida, dependencia sin saltos y tick que conserva el vencimiento. Se añade protección de versión y clave de agenda en `ProcessPlanningService` para evitar KeyNotFound/500 en esa carrera.

El piloto antiguo `--process-plan-only` **falló** al publicar su fixture sin criticidad obligatoria (`SCHEDULING_CRITICALITY_REQUIRED`), antes de probar su recorrido runtime. Se conserva `.local/performance-process-runtime.log` y su base; no se deshabilitó la validación ni se declara aprobado ese piloto. La primera regresión de publicación insertaba pasos sobre una versión ya congelada y fue correctamente rechazada por el trigger de inmutabilidad; se conserva `.local/performance-process-publication-fixture-failed.log`. Se corrigió la fixture para publicar con los servicios reales y pasó en otra base nueva. No confundir estos fallos de fixture con recorridos ejecutados correctamente.

Compilación final Release Web/TaskChecks: cero errores y advertencias (`.local/performance-build.log`); regresiones rápidas repetidas después de la última corrección (`.local/performance-regressions.log`). No se hizo commit, push ni despliegue. Las modificaciones previas de checklist y otras tareas se conservaron; no se atribuyen a este trabajo.

### Límites y tareas que permanecen

Los reportes conservan una proyección escalar del resultado filtrado para indicadores, orden exacto y exportación; la paginación SQL y el HTML acotado no significan que toda agregación histórica tenga memoria constante. Faltan volumen mayor fijado y estabilidad prolongada. La exportación PDF completa sigue teniendo costo de CPU en el navegador; no se certifica su cancelación durante toda esa generación ni el recorrido S3 legacy completo. Algunas activaciones y escrituras retienen coordinación global para preservar atomicidad. Los tickets de origen de login son locales al proceso; un despliegue futuro con varias instancias necesitaría afinidad o un canal interno verificable compartido. Falta verificar el proxy/LAN real con la versión final, sin reiniciar la simulación durante esta ejecución. Ninguno de estos ensayos autoriza ni acredita preparación para producción.

### Estado final de servicios y datos

Comprobación final: `.local/performance-final-state.json`. La base principal aislada conserva **1825 definiciones, 3327 asignaciones y 68 fotografías sintéticas** tras todas las regresiones; esos conteos no son los de la comparación HTTP. Se conservaron también cuatro bases auxiliares: `sgol_task_checks_performance_auto_20261028` (selección automática), `sgol_task_checks_performance_runtime_20261028` (piloto antiguo fallido), `sgol_task_checks_performance_auto_final_20261028` (primera fixture de publicación rechazada por inmutabilidad) y `sgol_task_checks_performance_process_final_20261028` (regresión final aprobada). No se borraron ni restauraron automáticamente.

El host de prueba 7267 quedó detenido. Se verificaron las etiquetas del proyecto `sgol-performance` y se detuvieron únicamente sus tres contenedores PostgreSQL/ClamAV/almacenamiento (5547/3347/8347); los volúmenes y resultados locales se conservan. PostgreSQL/ClamAV/almacenamiento compartidos siguen activos en 5440/3310/8333, y los accesos 7240/7440 conservan el PID 10448. No se reinició, migró ni desplegó la simulación compartida, ni se le enviaron escrituras de estas pruebas. La versión cargada en ese proceso no queda certificada por haber cambiado archivos fuente. La migración de búsqueda está disponible en el código y se aplicó únicamente a bases aisladas.

Los resultados y `.local` están ignorados por Git; no se publican credenciales, semillas MFA, bases, respaldos ni evidencias privadas. No se certifican pico nativo absoluto, estabilización prolongada ni INP de uso real a partir de la navegación y los recorridos automatizados medidos.
