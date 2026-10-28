# Pruebas aisladas de rendimiento

Son diagnósticos con cargas acotadas; los tiempos no son umbrales portátiles ni garantizan capacidad de producción. No cambian el producto para que las pruebas pasen.

## Ejecución

Desde la raíz del repositorio, con Docker y el certificado HTTPS de desarrollo disponibles:

```powershell
./tools/Sgol.TaskChecks/run-performance.ps1
```

El script levanta dependencias propias, ejecuta el host de integración y detiene sus contenedores al terminar. Conserva evidencia, base y archivos de prueba. Una repetición con fixture nueva requiere `-ResetFixture`: borra **solo** `sgol_task_checks_performance` del contenedor dedicado, después de comprobar su propietario y que no exista host de pruebas escuchando. No borra la simulación ni la firma del antivirus. Los resultados previos permanecen en `artifacts/`.

La firma ClamAV se toma de `sgol-local_scanner-data` con montaje **de solo lectura**. Se puede seleccionar otra mediante `SGOL_PERF_SIGNATURE_VOLUME`; debe contener firmas válidas previamente descargadas. El escáner que recibe la carga es otro proceso/contenedor, nunca el puerto 3310 de la simulación. Los contenedores nuevos comparten el hardware físico y el disco del equipo; se limita su CPU y RAM para contener interferencias.

Guardas del harness: `localhost:5547`, base exacta `sgol_task_checks_performance`, servidor HTTPS `localhost:7267`, escáner `3347`, almacenamiento `8347`. Las credenciales sintéticas se generan en `.local/performance-environment.json`; no se imprimen. Las claves de protección están en carpetas locales ignoradas. No publicar `.local/`, `artifacts/`, bases ni claves.

## Cobertura y métricas

- Importación real del servicio: 1000 definiciones y carrera de dos importaciones firmadas idénticas de 50 definiciones. No simula un formulario de importación que no existe en la app.
- Formularios Razor reales: 120 altas y 120 entregas, concurrencia 1/8/24, binding, permisos, CSRF, autenticación y MFA reales.
- Fotos: 48 cargas sintéticas JPEG 1600×1200 con doble ClamAV real, concurrencia 1/4/12; seis reintentos con el mismo ID; 21 cargas ante límite 20; adjuntar 16 y repetir entrega. Foto operativa se almacena en BD: levantar S3 no equivale a probar carga/presigned URLs de evidencia legacy.
- Bloqueo: mantener deliberadamente la clave global 2/17 segundos y observar entrega. Es un bloqueo artificial controlado, no una medición de una planificación real de 17 s.
- Historial: 100/1000/3000 asignaciones y 10/100/300 solicitudes de arrastre; Reportes/Validaciones y lecturas mixtas; cancelación cliente y consulta de actividad SQL posterior.
- Acceso: 40 intentos API desde una IP y 20 por Razor después de agotar cuota. Un HTTP 200 del formulario **no** significa login exitoso: el error puede renderizarse con 200.
- Salud después de la carga, datos persistidos, asignaciones entregadas y fotos sin duplicados.
- Excel y normalización reales, archivos sintéticos, amplificación de XML, precancelación y cancelación durante parseo.

`load-results.json` contiene tiempos por intento, p50/p95/p99 de la muestra, estados, bytes, asignaciones administradas y CPU. Muestreo de memoria cada 100 ms. Host y generador HTTP conviven en un proceso: los recursos incluyen ambos y no incluyen contenedores. Las muestras de tres solicitudes de historial son exploratorias: su p95 es el máximo de tres, no una estimación estadística de población.

## Comprobaciones complementarias

Con las dependencias dedicadas levantadas y las variables del script configuradas:

```powershell
dotnet run --project tools/Sgol.TaskChecks -c Release -- --performance-media
dotnet run --project tools/Sgol.TaskChecks -c Release -- --performance-scanner
dotnet run --project tools/Sgol.TaskChecks -c Release -- --performance-planning
```

Scanner compara el protocolo actual y una sonda NoDelay, además de conexiones `localhost`/IPv4; la sonda no altera el scanner del producto. Planning usa generador anual real, horario de trabajo sintético, volumen creciente y rechazo del límite de 10000 ocurrencias; no persiste asignaciones. Ejecutar las pruebas que agregan catálogo **antes** de las mediciones comparativas de navegador, para mantener el volumen constante entre temas/motores.

Para navegador, crear la cuenta sintética adicional con `performance-browser-seed.sql` dentro del contenedor dedicado. Iniciar `--performance-preview` (host de cinco minutos sobre el fixture retenido) y ejecutar con Node/Playwright:

```powershell
node tools/Sgol.TaskChecks/performance-browser.cjs
node tools/Sgol.TaskChecks/performance-login-partitions.cjs
```

El navegador completa contraseña/MFA y conserva sesión en memoria entre motores. Prueba 1366/390 px, claro/oscuro, menú y Escape; Chromium móvil usa CPU4× y red1,6Mbps/150ms. Solo acepta el destino fijo 7267. Firefox queda registrado como no ejecutado si no inicia. La API PerformanceObserver de tareas largas no está disponible en todos los motores: cero observaciones allí no prueba ausencia de pausas.

La prueba de cuotas utiliza direcciones de origen loopback distintas y peticiones con CSRF real; confirma si la API mantiene la cuota del cliente mientras Razor cae en la cuota interna. Conserva correctamente las eliminaciones de cookies. Ejecutarla después de autenticar el navegador para no consumir su cuota inicial.

No cerrar pruebas pendientes de hardware físico, infraestructura futura o duración de una jornada mediante estos ensayos acotados.


## Correcciones y regresiones añadidas durante la implementación

El documento del plan contiene la comparación antes/después y las limitaciones. El harness conserva las mismas cargas originales. La admisión puede devolver 503 con cola llena o espera agotada: el instrumento registra ese primer intento y, cuando corresponde, una fase **separada** de reintento explícito con el mismo ID. No se ocultan esos rechazos ni se aumentan los límites para que termine una carga. El bloqueo de 17 s ahora exige 503, ausencia de entrega parcial y recuperación posterior. Los errores esperados de lock aparecen en los logs SQL.

El guard del script sigue exigiendo localhost:5547 y la base exacta. Tras validarlo, la conexión efectiva del host utiliza `127.0.0.1`: el puerto dedicado solo publica IPv4. Esto elimina también la demora del socket usado para cancelar consultas; la sonda `--performance-cancel-transport` compara ambos destinos sobre `pg_sleep`, sin cambiar datos. Nunca dirigirla a otra base.

Regresiones rápidas sin BD/HTTP:

```powershell
dotnet run --project tools/Sgol.TaskChecks -c Release -- --performance-regressions
```

Incluye libros legítimos de varias hojas, duplicados, cancelación después de 256 nodos XML observados, cola llena/cancelada/recuperada, capacidades internas de origen de un solo uso, cabeceras falsificadas, diagnósticos de búsqueda y casos JPEG de frontera. La regresión JPEG ordinaria no depende del ensamblado anterior. `--performance-jpeg` es un diagnóstico diferencial adicional que exige conservar y compilar el código anterior en `.local/performance-baseline-source`; compara once resultados y tiempos con ese ensamblado, sin base ni HTTP.

Después del lote HTTP completo, con la conexión **dedicada** del script configurada:

```powershell
dotnet run --project tools/Sgol.TaskChecks -c Release -- --performance-integrity
dotnet run --project tools/Sgol.TaskChecks -c Release -- --performance-planning
dotnet run --project tools/Sgol.CameraChecks -c Release -- --performance-clamav
```

Integrity requiere la fixture completa con historial/arrastres; añade una tarea, un calendario futuro si falta, un plan pequeño y cambios simulados de versión/capacidad. Contrasta todas las páginas, progreso diario e historial; revierte su fixture histórica de auditoría; usa una barrera de instrumentación para ejecutar una entrega concurrente y cambiar datos después del cálculo y antes del lock; verifica stale, replay y cambio de fecha operativa. Ejecutarlo **antes** de las mediciones de navegador. Las pruebas de Camera usan ClamAV dedicado 3347 con `--performance-clamav`; no usar `--clamav`, que sigue siendo el recorrido antiguo del puerto compartido.

Para una comparación visual del código anterior y el nuevo sobre exactamente el mismo volumen, crear ambas cuentas `perf-browser-before` y `perf-browser-after` antes de empezar, adaptando en el SQL tanto `perf-browser` como `PERF-BROWSER` a cada nombre/normalización. `--performance-browser-workload` exige esa base exacta y ambas cuentas; añade 80 asignaciones por cuenta (incluye entregadas y descripción extensa). No volver a agregar catálogo, planes ni asignaciones entre los dos recorridos.

`performance-browser.cjs` admite `SGOL_PERF_BROWSER_USER` y `SGOL_PERF_BROWSER_OUTPUT`; el valor predeterminado conserva la cuenta original. El recorrido comparativo cubre 1366/390/320, claro/oscuro y las cuatro pantallas Tareas/Reportes/Supervisión/Mi trabajo. Chromium usa CPU4× y la misma red limitada en ambos tamaños móviles. `SGOL_PERF_BROWSER_VERIFY=1` añade paginación con teclado, búsqueda vacía, exportación completa, detalle y diagnóstico anual inconcluso en formularios reales. Las pruebas de pestaña oculta usan una fixture mínima con el script del producto y fetch/reloj interceptados: se registran como componente, no como pantalla ni pestaña física.

`--performance-preview` sigue durando 300 s por defecto; `SGOL_PERF_PREVIEW_SECONDS` permite de 60 a 900 s para la matriz ampliada. Esa duración del host de pruebas no cambia los límites del producto. Las cuentas de comparación se enrolan con MFA real de forma independiente; nunca guardar sus semillas, cookies o claves en resultados. `performance-login-partitions.cjs` se ejecuta al final: comprueba cliente nuevo, cuota compartida entre Razor/API de la misma IP, cabeceras forjadas y CSRF. Sus cuotas se agotan deliberadamente.

Los histogramas de servidor contienen etapas, espera/retención del lock y activos por categoría. Las etiquetas son de cardinalidad fija. Los p95 de etapas distintas no se suman; la duración de retención solo se registra al commit/rollback explícito. El muestreo residente continúa incluyendo host y generador, y excluye los contenedores.


Para repetir solo los recorridos finales sin otra matriz cronometrada: `SGOL_PERF_BROWSER_VERIFY=1` y `SGOL_PERF_BROWSER_FLOWS_ONLY=1`. Incluyen tarjetas reales de revisión con paginación, formulario vacío/CSRF, cambios de anchura conservando foco/búsqueda y acceso al filtro móvil. Borrar esas variables al terminar. Cualquier reinicio de MFA de cuentas sintéticas debe limitarse a la base exacta dedicada; nunca conservar semillas en los resultados.

`--process-plan-auto-only` requiere una **base vacía nueva** `sgol_task_checks_*` en el contenedor dedicado 5547; añade su fixture válida, publica realmente una nueva versión desde otro contexto entre cálculo y lock y exige 409 atómico; después prepara/activa con datos frescos y ejecuta el escenario actual de calendario/dependencias/política capturada. No ejecutarlo sobre la fixture principal ni sobre una simulación compartida. Las bases usadas en esta implementación y sus resultados están documentados en el plan. El piloto antiguo `--process-plan-only` falló por una fixture sin criticidad y no sustituye este escenario vigente; no quitar la validación para hacerlo pasar.
