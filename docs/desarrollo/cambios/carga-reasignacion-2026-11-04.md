# Carga y reasignación — traslado y validación operativa (2026-11-04)

## Objetivo y alcance

Cambio mixto: retirar «Carga de trabajo por persona» de `/validaciones` e integrar su consulta, vista previa y confirmación en `/supervision/carga`. Conservar transferencia individual, autorizaciones, capacidad temporal, protección de actividad, historial y transacción. No cambia el esquema ni se escriben casos de prueba en la base compartida.

## Investigación y fuentes propietarias

- `Pages/Validations/Index.cshtml.cs` cargaba capacidad junto con revisiones y arrastres. `_Capacity.cshtml` enviaba ajustes a esta página.
- `Pages/Validations/Workload.cshtml.cs` consultaba `OperationalSupervisionService.WorkloadAsync` y ofrecía transferencia individual con recibo.
- `OperationalCapacityService`: `RowsAsync`, `PreviewAsync`, `BuildAsync`, `ApplyAsync`, `StoreSettingAsync`. La propuesta captura entradas; la confirmación bloquea, recalcula y comprueba huella antes de guardar capacidad, responsables/horarios y auditoría juntos. Los intervalos superpuestos se dividen preservando los tramos exteriores.
- `CapacityLedger` comparte capacidad con planificación y reservas; los cambios se consumen al consultar y asignar. Un ajuste no cambia carga configurada del catálogo. El exceso protegido puede permanecer y se comunica.
- `OperationalSupervisionService.Transfer.cs`: conserva asignación, avance y evidencia, registra recibo de transferencia individual. El ajuste colectivo conserva su auditoría de redistribución.
- La bóveda consultada: Inicio, mapa de dominio, reglas, supervisión, planificación y referencias visuales 00/05. Infraestructura: mapa, instalación local y base documental.

## Entorno y modificaciones previas

Rama master con cambios previos en Personas, Validación, catálogo, scripts móviles y pruebas. Se conservan. Publicación observada: `.local/runtime/task-descriptions-20261104/Sgol.Web.dll`, Web 13868, lanzador 9792, proxy 20652. No hay watch. Datos compartidos en PostgreSQL local, no producción.

Pruebas en `sgol_task_checks_workload_20261104` (localhost:5440), restaurada desde un dump consistente de `sgol_local`. Host HTTP aislado 7293, sesión sintética y servicios/formularios/CSRF/SQL reales. Reloj de operación del 04/11/2026 a las 10:00 de Ciudad de México que avanza con tiempo transcurrido; se adelanta también un día para probar vencimiento. Personas sintéticas con especialidad propia evitan alterar asignaciones copiadas de otros usuarios.

## Implementación

Formulario de capacidad y sus handlers pasan a `Workload.Capacity.cs`; nombres `Adjustment`/`CapacityFingerprint` separados de `Change`/`Fingerprint` de transferencias. Supervisión deja de consultar capacidad. Se reutiliza el componente, estilos y script existentes. Cada acción valida su formulario y conserva vista previa/confirmación.

## Matriz de escenarios

Resultado final: 24 comprobaciones HTTP/SQL aprobadas y 26 escenarios de navegador aprobados. Evidencia: .local/workload-20261104/http.log, browser.log y browser-results.json.

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Ubicación y navegación | Editor solo en Carga y reasignación | HTTP real / copia aislada | Aprobado |
| Vista previa y guardado después de esperar | Consulta sin escrituras; confirmación guarda y reabre | HTTP + PostgreSQL + reloj en marcha | Aprobado |
| Tarea futura y compañera elegible | Redistribución visible y persistida | HTTP + SQL | Aprobado |
| Activada sin seleccionar, avances y concluida | Conservación; exceso explícito | HTTP + comparación SQL | Aprobado |
| Caducidad de capacidad | Regreso a capacidad base al terminar vigencia | Ledger persistido | Aprobado |
| Progreso concurrente y reintento | Conflicto sin cambios parciales ni duplicados | Segundo contexto SQL + HTTP | Aprobado |
| Permisos, CSRF, valores inválidos | Rechazo del servidor | HTTP real | Aprobado |
| Transferencia individual | Vista previa y guardado independientes del ajuste | HTTP + SQL | Aprobado |
| Cambio de día | No confirmar ajuste ya pasado | HTTP + reloj adelantado | Aprobado |
| Móvil/escritorio, temas, teclado, foco y textos largos | Controles accesibles sin desbordamiento | Navegador de aplicación aislada | Aprobado |
| App compartida | Publicación y disponibilidad | Hash de DLL, procesos y health.json | Aprobado |

## Evidencia y documentación

Evidencias privadas en `.local/workload-20261104/`. Las referencias de bóveda afectadas son supervisión, planificación y pantallas; se actualizaron con el comportamiento efectivo publicado, sin incorporar este informe ni los datos sintéticos.

## Fallos encontrados y correcciones verificadas

- La incorporación de dos formularios hizo necesario separar su validación y conservar Day/Range en el envío de capacidad. Los tests envían únicamente los campos de la acción pertinente.
- El hidden original del instante de planificación perdía precisión; la confirmación HTTP fallaba aun sin actividad concurrente. Se usa formato O completo. Regresión: esperar 1.2 segundos y confirmar con el reloj avanzando.
- Los errores conservan motivo y opciones, y enfocan el mensaje. Los nombres en móvil ya no compiten como elementos flex separados del resto del resumen.
- Dos fallos de la propia prueba se corrigieron: leer TempData antes del reintento y obtener un token CSRF asociado a la identidad de la cuenta sin permiso.

## Publicación y límites

Publicada Web en .local/runtime/workload-capacity-20261104/Sgol.Web.dll, con hash igual al ensamblado probado; proceso 18972 y lanzador 31084. Proxy 20652 conservado. Salud live/ready y acceso local/LAN: HTTP 200 con TLS normal. Sin migraciones ni ajustes de cargas en la simulación compartida. Sin commit ni push. El artefacto anterior se conserva.

Pruebas: dotnet build tools/Sgol.TaskChecks --no-restore; runner --workload-http --ui, conexión protegida a la copia local; node tools/Sgol.MobileChecks/workload-capacity-checks.cjs .local/workload-20261104. Navegadores Chromium/Edge y WebKit, 320/390/768/1280 px, temas light/dark/special. El host de pruebas sustituye la sesión; formularios Razor, CSRF, autorización de servicios y persistencia son reales. No se certifica inicio de sesión real ni modificación autenticada en la aplicación compartida. No se ejecutó una nueva regresión completa de procesos, especialidades cruzadas o todas las modalidades históricas; su motor no cambió. Esquema nuevo: no aplicable.

Aparecieron ediciones concurrentes ajenas en arrastres, reportes y colas de revisión. Se conservaron; no se atribuyen a este cambio. La revisión de whitespace se limita a los archivos de esta tarea, pues el diff global contiene avisos de otros cambios.
