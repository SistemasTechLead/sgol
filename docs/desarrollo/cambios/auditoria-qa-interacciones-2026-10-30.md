# Auditoría QA de interacciones y plan de corrección — 30/10/2026

## Resultado y alcance

Se confirmaron **dos bugs del producto mediante formularios/handlers HTTP reales y consulta de persistencia aislada**, y un defecto de la herramienta de QA que comunica éxito de proceso pese a registrar escenarios fallidos. No se implementaron correcciones funcionales.

La investigación cubrió reglas, servicios, integración PostgreSQL, Razor HTTP, permisos, CSRF, autenticación/MFA, concurrencia, reintentos, archivos, rollback, carga acotada y consultas con historial. No constituye cobertura exhaustiva ni certificación para producción. La revisión visual y la ejecución de eventos JavaScript en navegador quedaron pendientes porque Computer Use no pudo verificar la URL de Brave en Windows.

Se ejecutó un agente QA complementario para entrega, checklist, revisión, historial y concurrencia. El agente reprodujo los dos defectos del producto y después investigó discrepancias de las suites de planificación.

## Entorno y conservación

- Investigación de alcance mixto, solo diagnóstico y planificación. Se consultaron `AGENTS.md`, `PROTOCOLO_IMPLEMENTACION.md` y `ESTILO_VISUAL_V2.md`.
- Se conservaron las numerosas modificaciones previas del repositorio. La compilación inicial de `Sgol.TaskChecks` y sus dependencias pasó con 0 errores y 0 advertencias, usando `--artifacts-path .local/qa-20261030/build`.
- Aplicación operativa observada: proceso 31028, `.local/runtime/reassigned-color-20261030/Web/Sgol.Web.dll`; proxy LAN activo y PostgreSQL local en 5440. Los scripts vigentes configuran la simulación `sgol_local` y la entrada interna 7241. No se verificó equivalencia de código entre el binario operativo y el compilado para esta auditoría.
- Las escrituras de pruebas se limitaron a bases `sgol_task_checks_qa_*`, `sgol_profile_checks_qa_*` y al proyecto Docker propio `sgol-qa-20261030`. No se escribieron registros ni se aplicaron migraciones en `sgol_local`; se tomó un `pg_dump` de lectura para pruebas en copias independientes. La simulación consultada contenía 542 asignaciones.
- La carga utilizó PostgreSQL 5547, host HTTPS 7267, scanner 3347 y almacenamiento 8347. Las firmas del antivirus se montaron en solo lectura. Los contenedores de carga quedaron detenidos; se conservaron bases y evidencia para reproducir resultados.
- Los hosts HTTP de pruebas quedaron detenidos. No se reinició ni desplegó la aplicación operativa; no hubo commit ni push.
- Evidencia técnica local ignorada: `.local/qa-20261030/`. No publicar logs completos, bases, snapshots, claves ni archivos de credenciales. Este informe contiene únicamente resultados y rutas.

## Bugs confirmados del producto

### QA-01 · P2 · Pérdida silenciosa de marcas de checklist entre pestañas

**Precondición:** una tarea pendiente CHECKLIST del mismo usuario abierta en dos pestañas/dispositivos, con la misma definición y borrador inicial vacío.

**Reproducción ejecutada:**

1. Obtener el formulario Razor `/mi-trabajo?assignedTask=<id>` y su CSRF real.
2. Pestaña A guarda el punto A mediante `POST /mi-trabajo?handler=ChecklistDraft`, campos `id`, `answer`, `__RequestVerificationToken`.
3. Pestaña B, abierta antes de ese guardado, envía únicamente su punto B con la misma definición.
4. Ambos reciben `200 {saved:true}`. La revisión pasa de 1 a 2 y luego a 3; el borrador persistido contiene B y ha perdido A.

**Esperado:** detectar que el segundo guardado parte de una lectura anterior y evitar la sustitución silenciosa; informar el conflicto conservando el trabajo local.

**Causa y recorrido del dato:**

- `src/Sgol.Web/wwwroot/js/checklist.js:95-118`: construye una respuesta completa a partir de las casillas; envía definición y marcas, sin base o versión del borrador. Su cola protege el orden dentro de una pestaña, pero no coordina otra pestaña.
- `src/Sgol.Web/Pages/MyWork/Index.Operational.cs:12-23`: valida sesión y CSRF y devuelve `saved=true`; el contrato no recibe ni devuelve versión del borrador.
- `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTaskService.Checklist.cs:9-25`: el bloqueo serializa las escrituras, pero sustituye `ChecklistDraftJson` sin comprobar qué borrador leyó el cliente. Incrementar `Revision` después no detecta la lectura obsoleta.
- `OperationalChecklist.ValidateAnswer` comprueba la definición y las marcas permitidas, no la versión previa del borrador. La definición permanece igual durante esta reproducción.

**Impacto comprobado:** pérdida de avance guardado y mensaje de éxito engañoso. La prueba posterior de entrega parcial rechazó correctamente con `TASK_CHECKLIST_PENDING`; no se demostró bypass de completitud ni pérdida de evidencia ya entregada. El recorrido de completar y aprobar terminó en `CONCLUIDA`.

**Evidencia:** `.local/qa-20261030/agent/probe.log`, `agent/probe-http.log`, `agent/probe/Program.cs`, `agent-findings.md`. El host temporal usa una sesión inyectada `TEST_ONLY`; formularios, CSRF, handlers, servicios y persistencia son reales. Esta reproducción no ejecutó eventos de navegador.

**Implementación propuesta:**

1. Exponer una base de borrador independiente de la revisión del intento de proceso: preferentemente un fingerprint determinista del estado persistido, incluyendo el vacío inicial, evitando migrar borradores existentes solo para versionarlos.
2. Recibir esa base al guardar y compararla dentro del bloqueo/transacción antes de sustituir las marcas. Rechazar una base obsoleta con un conflicto específico. Responder con la nueva base tras confirmar persistencia.
3. Mantener la cola por pestaña y actualizar su base después de cada confirmación. Conservar las marcas locales ante conflicto y ofrecer recargar/comparar antes de volver a guardar. No unir conjuntos ciegamente: también existe desmarcado intencional.
4. Definir reintento idempotente para respuesta perdida y evitar que un guardado propio se considere conflicto. No modificar las respuestas históricas ya entregadas ni alterar la liberación de etapas.
5. Integrar las lecturas de la pantalla, borrador independiente y evidencia de proceso; comprobar cambio de definición, reconciliación de asignaciones, transferencia y entrega entre lectura y guardado.

**Aceptación:** dos editores con la misma base no pueden confirmar dos sustituciones incompatibles. El segundo recibe un conflicto con tarea/causa/acción; el primero permanece persistido y el trabajo local del segundo puede recuperarse. Una sola pestaña conserva guardado rápido, desmarcado y entrega válidos.

### QA-02 · P2 · Un extremo válido de fecha rompe Supervisión

**Reproducción ejecutada:**

- `GET /validaciones?PISO_VENTAS.range=CUSTOM&PISO_VENTAS.to=2026-10-30` → 200.
- Mismo GET con `PISO_VENTAS.to=9999-12-31` → 500.

**Causa:** `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Paging.cs:35-37` acepta `DateOnly.MaxValue` y calcula `last.AddDays(1)`. Se produce `ArgumentOutOfRangeException`, que atraviesa `Pages/Validations/Index.cshtml.cs:84` y `OnGetAsync:63`. El manejo existente captura `DirectionPlanException`, no este desbordamiento. El campo `Hasta` en `Pages/Validations/_Filters.cshtml:28` no limita ese valor.

**Impacto:** la página deja de responder al filtrar con ese extremo; no modifica datos. Frecuencia probable baja, reproducción determinista. No se confirmó el mismo fallo en otras pantallas.

**Implementación propuesta:** validar y normalizar el rango en servidor antes de construir instantes; conservar los extremos opcionales vigentes y evitar el cálculo del día siguiente cuando no existe. Preferir un predicado sin desbordamiento a imponer una restricción de negocio arbitraria. Los valores inválidos o rangos invertidos deben producir una explicación en el formulario, conservando filtros, apartado y navegación. Revisar productores/consumidores equivalentes de límites de fecha durante la corrección; añadir límites HTML solo si corresponden al dominio efectivo del servidor.

**Aceptación:** fechas normales y extremas reciben un resultado definido o un error comprensible de campo, nunca 500; los filtros y la paginación conservan su comportamiento. No requiere modificar historial ni migración.

**Evidencia:** `.local/qa-20261030/agent/probe-http.log`, stack exacto de servicio y Razor en host HTTPS aislado 7279; misma limitación de sesión inyectada indicada en QA-01.

## Defecto confirmado de la herramienta de QA

### QA-03 · P2 · Diagnóstico de planificación termina exitosamente con escenarios fallidos

`--performance-planning` terminó con exit 0, aunque `planning-results.json` registró dos muestras con `passed=false`: 2686 y 9395 ocurrencias rechazadas con `SCHEDULING_SEARCH_INCONCLUSIVE`, motivo `OCCURRENCE_SEARCH_SIZE_LIMIT`, máximo 1000. La muestra de 277 ocurrencias completó; el volumen superior al límite global de 10000 fue rechazado como esperaba la prueba.

`tools/Sgol.TaskChecks/PerformancePlanningChecks.cs:35-41` persiste los resultados pero no propaga una muestra fallida al resultado global; solo lanza si cambió el número de asignaciones. Por tanto, exit 0 no equivale a aprobación de esta herramienta.

**Plan:** separar explícitamente modo diagnóstico de modo aserción; agregar evaluación final de todas las muestras y salida no exitosa cuando una expectativa no se cumple. Mantener el JSON incluso al fallar. Establecer expectativas del límite de búsqueda conforme al contrato actual y comprobarlas con un resultado fallido deliberado. No subir límites del producto para lograr verde.

**Evidencia:** `.local/qa-20261030/load-planning.log`, `artifacts/performance-planning/planning-results.json`. Se conservaron 3164 asignaciones antes/después: el preview no creó asignaciones.

El rechazo de 2686/9395 ocurrencias es una limitación observada del buscador, **no prueba por sí solo un bug**: requiere acordar volumen soportado y estrategia para dividir/reducir el horizonte si se desea ampliar capacidad.

## Divergencia que requiere decisión de negocio

La suite `OperationalSchedulingChecks.TargetReflow` espera conservar el responsable de una tarea flexible si puede comenzar a las 10:00 tras cambiar su turno. En la copia aislada, la propuesta la transfiere a otro trabajador para comenzar a las 09:00.

El agente comprobó que conservarla a las 10:00 sí es elegible. `OperationalCapacityService.cs:205-208` agrega esa opción, pero `:224` prioriza el inicio más temprano entre todos los candidatos. La propuesta respeta restricciones y exige confirmación. No se ha demostrado que viole una regla vigente que priorice conservar responsable.

Antes de cambiar esa heurística se debe elegir entre:

- Priorizar conservar responsable cuando existe un hueco válido: reduce transferencias y preserva continuidad, aunque puede comenzar más tarde.
- Priorizar el comienzo más temprano entre responsables elegibles: mantiene el algoritmo actual y puede generar más cambios de responsable.

Este punto queda fuera de los bugs confirmados. No se cambió asignación alguna de la simulación para resolverlo. Evidencia: `.local/qa-20261030/agent/reflow-diagnosis.log`; el preview deja a la tarea crítica como conflicto y no debe describirse como un reacomodo completo aprobado.

## Matriz de ejecución y cobertura

Los recuentos son grupos/ejecuciones, no escenarios únicos: muchas suites repiten el preámbulo de 99 reglas. Se inspeccionaron resultados y datos, además del código de salida.

| Recorrido / escenario | Esperado | Prueba y entorno | Estado y evidencia |
|---|---|---|---|
| Compilar código actual | Artefactos utilizables sin errores | Build aislado `Sgol.TaskChecks` y dependencias | **Aprobado**, `build.log`, 0 advertencias/errores |
| Descripción, secuencias, checklist, arrastre, postergación, agenda, balance, horario, capacidad, operaciones semanales, ítems | Validar reglas y fronteras; rechazos específicos | 11 grupos de reglas sin BD | **Aprobado**, `main-results.json` y logs por grupo |
| JPEG/Excel, cancelación de parseo, cola y origen de login | Rechazar entradas inválidas y recuperar recursos | `--performance-regressions` | **Aprobado**, `performance-regressions.log` |
| Crear/editar/publicar procesos, requisitos de evidencia, borrador, eliminación y reintento | Formularios válidos; conservar historia y ejecuciones iniciadas | `--process-editor-only`, Razor HTTP + BD vacía aislada | **Aprobado**, `process-editor-only.log`; incluye pruebas de retiro del catálogo e integración de etapas |
| Plan automático, reservas, cambios simultáneos, activación repetida | Rechazo obsoleto sin persistencia parcial; una activación | `--process-plan-auto-only`, BD aislada | **Aprobado**, `process-plan-auto-only.log` |
| Actualizar una asignación, lote global y retirar asignación | Conservar historia, rollback completo ante conflicto y preview obsoleto | Tres suites `assignment-*-only`, cada una con BD propia | **Aprobado**, logs y `main-results.json` |
| Entrega anticipada, checklist parcial/completo, revisión individual/masiva, transferencia y proceso | Requisitos completos, autoría, evidencia y atomicidad | Tres suites del agente, servicios con BD propia | **Aprobado**, `agent/{early,batch,checklist}.log`; no sustituye navegador |
| Dos pestañas guardan borrador de la misma tarea | Evitar pérdida silenciosa | HTTP Razor, CSRF y consulta BD | **Fallido**, QA-01 |
| Filtro de Supervisión con fecha normal y máxima | Respuesta válida/error manejado sin 500 | HTTP Razor con sesión de prueba | **Fallido**, QA-02 |
| Menús/URL/POST por cuatro roles y anónimo | Permisos de pantalla coherentes | `Sgol.RoleAccessChecks`, filtros invocados | **Aprobado**; no equivale a todos los permisos de objetos |
| Reportes y tablero | Fechas, métricas, estados, filtros, binding y tareas sustituidas coherentes | `Sgol.ReportChecks`, `Sgol.BoardChecks` | **Aprobado**, logs respectivos; ReportChecks informa 103 checks |
| Fotos, capturas, revisión de conjuntos y archivos malformados | Límites, detección y rechazo de miembros ajenos/infectados | `Sgol.CameraChecks`, `Sgol.SecurityMediaChecks` | **Aprobado**, logs; seguridad media usa entradas sintéticas acotadas |
| Cookies y páginas de error | Sesión y contrato de errores conservados, CSRF comprensible | `Sgol.CookieChecks`, `Sgol.ErrorChecks`, hosts HTTP de prueba | **Aprobado**, logs; no es pentest exhaustivo |
| Login, MFA y sesión reales | Acceso a Razor con autorización efectiva | Host de carga aislado, dos cuentas sintéticas | **Aprobado**, `load.log` / `load-results.json` |
| Importación de 1000 tareas y carrera de 50 idénticas | 1050 definiciones únicas; un duplicado rechazado | Servicio real, contenedor propio | **Aprobado**, carga: 200 y 409 en carrera; 1050 persistidas |
| 120 altas y 120 entregas a concurrencia 1/8/24 | Ninguna pérdida/duplicado de persistencia | Formularios HTTP reales autenticados, BD propia | **Aprobado**, comprobaciones de conteo en carga |
| Fotos concurrentes, mismo ID repetido, límite de 20, entrega repetida | IDs únicos y evidencia adjunta una vez | 48 fotos JPEG, doble ClamAV real, c1/4/12; 6 reintentos; 21 ante límite20 | **Aprobado**; 20 aceptadas + 1 rechazada422; 16 adjuntas una vez |
| Bloqueos artificiales 2s/17s y reintento explícito | Éxito corto; busy acotado largo sin escritura parcial; recuperación | BD/host aislados | **Aprobado**; 503 a ~5,58s, posterior entrega302 |
| Historial 100/1000/3000, lecturas mixtas y cancelación | Respuesta, permisos, cancelación y recuperación | Carga HTTP, BD propia | **Aprobado** en muestra acotada; 403 en lecturas ajenas esperados; salud final 5×200 |
| Cuota de login API y Razor después de agotarla | Rechazo acotado sin evadir cuota | 40 intentos API + 20 Razor sintéticos | **Aprobado**, API8×401+32×429; Razor20×429 |
| Planificación anual a volumen creciente | Resultado coincide con expectativa y no crea asignaciones | 4 muestras, contenedor propio | **Fallido parcialmente**: 2/4 expectativas fallan; exit0 engañoso, QA-03 |
| Importación mensual sobre registros existentes | Reglas de catálogo/periodicidad sin cambios indebidos | `--monthly-import-only` sobre copia de simulación | **Aprobado**, `copy-monthly-import-only.log` |
| Horarios/rotación/operaciones semanales de suites antiguas | Recorridos completos hasta persistencia | Runner principal | **Fallido/no concluyente** por precondiciones/contratos obsoletos; detalle abajo |
| Transferencia/reasignación/ciclo de vida en copia histórica | Comparar historial y cambios | Copias independientes de la simulación | **Fallido/no concluyente**: presupuestos de fixtures no se cumplen; sin atribuir al producto |
| Perfiles: alta, duplicados, roles, desactivación, historia y CSRF | Atomicidad y permisos, formularios reales | Suite original y copia local de diagnóstico | **Parcial**: servicios y alta/desactivación HTTP pasan; suite completa detenida; detalle abajo |
| Pantallas reales: 320px/escritorio, temas, foco, teclado, textos largos, menú, JS y dispositivos | Interacción y presentación utilizables | Navegador real | **No ejecutado**: control terminó al no poder verificar URL de Brave |
| Jornada completa, todos los navegadores, cámaras físicas, pérdida real de red, flujo S3 legacy completo y toda la LAN | Estabilidad/integraciones completas | Entornos/dispositivos pertinentes | **No ejecutado**, fuera de la cobertura lograda; no inferir aprobación por el escáner o S3 levantados |

### Ejecuciones detenidas: causas y límites

Runner principal: **26 grupos; 17 finalizaron correctamente y 9 se detuvieron**. Evidencia: `.local/qa-20261030/main-results.json`. Los 9 no son nueve bugs del producto:

- `scheduling-integration-only`, `transfer-lifecycle`, `reassignment-lifecycle`, `lifecycle-only`, `monthly-import-only`: se despachan antes de crear usuarios y presuponen fixtures existentes; en una base vacía fallan buscando registros. Se repitieron sobre copias de simulación. Importación mensual pasa; las otras cuatro no completan.
- En copia, scheduling llega al caso de preferencia de responsable descrito arriba. Transferencia asume que no existe **ningún** `OperationalTransfer`, en vez de comparar el delta de su tarea; la copia ya contiene transferencias. Reasignación selecciona casos históricos con estado/revisión que ya no cumplen su escenario fijo (`TASK_CONFLICT`). Lifecycle toma usuarios sin garantizar vigencia respecto a su reloj de octubre y recibe `ACCESO_DENEGADO`. No se relajaron esas validaciones.
- `schedule-integration-only` usa `ReadAsync` como si devolviese `TemporalRows`; la generación ahora se realiza con `DailyLoadAsync`. Su tarea además omite criticidad y recibe `SCHEDULING_CRITICALITY_REQUIRED`. El formulario real exige ese campo. `agent/schedule-diagnosis.log` documenta el contraste.
- `rotation-only` y `weekly-operations-only` intentan crear sin preview/fingerprint de agenda exigido por el contrato actual: `PLAN_CAPACITY_STALE`. Los pasos siguientes no se ejecutaron.
- `subcoord-occasional-only` llega a asignar sobre personas de fixture sin disponibilidad suficiente y recibe `PLAN_NO_AVAILABLE_WORKER`; los pasos posteriores quedan sin validar. Los escenarios de permisos previos sí pasaron.
- `Sgol.UserProfileChecks` pasa sus comprobaciones de servicio, pero su host Razor no registra `IPersonAvatarReader`; el runtime lo registra en `PersistenceServiceCollectionExtensions.cs:50`. Una copia local del harness con ese registro permite probar GET, CSRF, alta y desactivación por HTTP. Se detiene después esperando el texto antiguo `Ver especializaciones`. Se conserva como suite incompleta; no se corrigió el producto ni se declaró toda Personas validada. Evidencias `profile.log`, `profile-probe-http.log`, `profile-fixture.log`.

### Muestra de tiempos de carga

Una sola ejecución local Debug con otros checks en curso; no es comparación antes/después ni umbral de servicio:

| Operación | Concurrencia | p95 de muestra |
|---|---:|---:|
| Alta formulario, 40 intentos | 24 | 1129 ms |
| Entrega formulario, 40 intentos | 24 | 620 ms |
| Foto JPEG, 16 intentos | 12 | 1882 ms |
| Reportes con 3000 históricos, 3 solicitudes | 1 | 164 ms |
| Supervisión con 3000 históricos, 3 solicitudes | 1 | 293 ms |

Tres solicitudes no estiman un percentil poblacional. Los contenedores tienen límites de CPU/RAM y comparten hardware con el resto del equipo. Evidencia de esta ejecución: `artifacts/performance-20261028/20261030-213445/load-results.json` (el nombre de carpeta del harness conserva la fecha histórica).

## Plan de implementación priorizado

1. **Restablecer confianza de pruebas:** convertir QA-01/02 en regresiones mantenibles que fallen antes del arreglo. Reparar QA-03; declarar fixtures y contratos de cada entrypoint; usar baseline/deltas y actores vigentes para datos históricos. Actualizar tests a preview+fingerprint y a la generación real de agenda, sin relajar reglas. Corregir DI/asserts obsoletos del harness de perfiles. Conservar casos no concluyentes y decidir la política de responsable antes de cambiar su expectativa.
2. **Corregir QA-01:** implementar base/confirmación de borrador y conflicto recuperable conforme al plan anterior. Validar primero persistencia y luego la pantalla real con dos pestañas, red lenta, reintento, desmarcado y entrega. No reutilizar sin análisis `assignment.Revision` si con ello se invalidan intentos de proceso.
3. **Corregir QA-02:** normalizar rangos y eliminar el desbordamiento; probar el formulario con extremos, campos vacíos, inversión y fechas inválidas; conservar filtros y paginación.
4. **Resolver política de reacomodo y volumen anual:** presentar las dos alternativas de responsable con sus propuestas reales; establecer volumen soportado y estrategia del buscador. Implementar cambios solo después de resolver esas decisiones de negocio; ampliar límites requiere mediciones, cancelación y protección de recursos.
5. **Cerrar validación de interfaz e integración:** navegador real del código corregido, móvil/escritorio, claro/oscuro, teclado/foco, éxito/error/carga/vacío y textos largos. Repetir únicamente regresiones afectadas y carga pertinente. Describir binario efectivamente probado y diferencias con el runtime operativo antes de cualquier despliegue.

No se necesita limpiar datos simulados para implementar QA-01/02. No se propone reescribir evidencia histórica ni recalcular responsables/cargas/plazos existentes como consecuencia de estos arreglos.

## Escenarios de aceptación que aún se deben ejecutar al corregir

| Escenario | Resultado esperado | Prueba/entorno | Estado actual |
|---|---|---|---|
| Dos editores sobre la misma base; guardado A y B | A persiste; B conflicto sin pérdida ni falso éxito | Integración+HTTP y dos pestañas reales | **Propuesto**; reproducción del defecto ejecutada |
| Guardar con base actual, desmarcar, cola rápida y respuesta perdida | Estado intencional e idempotencia correctos | HTTP+red lenta en navegador aislado | **Propuesto** |
| Cambio de definición+guardado, transferencia+guardado, entrega+guardado | Conflicto causal y rollback, historia conservada | BD aislada y formularios reales | **Propuesto** |
| Proceso: borrador, entrega y siguiente etapa; reintento | Borrador no bloquea intento, entrega una vez | Integración+Razor+navegador | **Propuesto**; recorrido actual sin corrección pasó |
| Fecha mínima/máxima, normal, vacía, inválida, invertida | Sin500, diagnóstico de campo y filtros conservados | Servicio+HTTP+navegador | **Propuesto**; normal/máxima actuales reproducidos |
| Herramienta con muestra deliberadamente fallida | Evidencia se conserva y modo aserción sale no0 | Prueba del runner en entorno aislado | **Propuesto** |
| Preferencia de responsable y casos históricos vigentes | Resultado coincide con decisión acordada | Preview real en copia aislada, comparar antes/después | **Propuesto**, decisión pendiente |

El cierre de esta auditoría entrega causas reproducidas y plan. La implementación y su validación posterior siguen pendientes; no se presenta el sistema como libre de bugs.
