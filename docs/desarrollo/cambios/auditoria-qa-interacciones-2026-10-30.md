# Auditoría QA de interacciones y plan de corrección — 30/10/2026

**Actualización:** QA-01, QA-02 y QA-03 implementados. Los resultados de la auditoría original se conservan debajo; la validación posterior, los contratos actualizados y las limitaciones están en «Implementación de correcciones — 30/10/2026». No se desplegó el cambio sobre la aplicación compartida.

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

## Implementación de correcciones — 30/10/2026

Este apartado actualiza el estado de la auditoría inicial; los resultados anteriores se conservan como evidencia histórica. Cambio **mixto**, implementado en el checkout existente. La autorización posterior del usuario sustituyó la prohibición inicial de commit/push: los 65 archivos previos se preservaron primero en `f904269`, publicado en `origin/master`. Las correcciones QA se mantienen en un commit separado. No se autorizó ni realizó despliegue o reinicio compartido.

### Cambios realizados y recorrido del dato

- **QA-01:** `_ChecklistEvidence.cshtml` expone la base de la definición y del borrador persistido, incluido el vacío. `checklist.js` envía esa base y un identificador de guardado; el handler `OnPostChecklistDraftAsync` devuelve la base confirmada después de persistir. `OperationalTaskService.Checklist.cs` compara dentro del bloqueo/transacción y devuelve `TASK_CHECKLIST_DRAFT_STALE` ante una lectura anterior. La respuesta identifica la asignación y permite comparar las marcas del servidor.
- La base es SHA-256 del JSON de definición y borrador. El recibo opcional `ChecklistDraft.Base/SaveId` se guarda en el JSON existente, sin migración ni reescritura de registros anteriores. Los borradores antiguos sin estos campos siguen siendo legibles. El reintento de la última operación exacta reconoce actor, base, identificador y conjunto de marcas; no incrementa revisión ni escribe otra evidencia. Un reintento antiguo después de otra edición recibe conflicto y no resucita casillas desmarcadas.
- La cola por pestaña conserva el orden. Ante conflicto o respuesta no confirmada conserva las casillas locales, bloquea la entrega y ofrece comparación/reintento explícito. Mantiene una copia por pestaña en `sessionStorage`, con el pedido pendiente para recuperar una respuesta perdida incluso tras recargar. Si el almacenamiento está bloqueado, no promete conservar una copia al recargar y permite copiar los textos antes de salir. No une conjuntos automáticamente. Las acciones usan botones y tokens existentes; el foco vuelve al estado de guardado.
- Se mantiene la revisión de asignación existente y **no** se usa como base del borrador ni se modifica la revisión del intento de proceso. La autorización por propietario y la condición de tarea pendiente se comprueban antes de reconocer reintentos. La entrega sigue validando definición y completitud, y conserva su transacción e historial.
- **QA-02:** `OperationalSupervisionService.Paging.cs` evita calcular el día siguiente a `DateOnly.MaxValue`. El día máximo no necesita cota superior. Valida fechas ISO opcionales y rangos invertidos; conserva filtros y opciones del apartado y devuelve un error comprensible con resultado vacío en ese apartado, sin consultar silenciosamente un rango distinto. El modelo de sección y `_Filters.cshtml` muestran el aviso dentro del formulario; la consulta original sigue alimentando navegación y paginación. Sin migración ni cambio del dominio de fechas.
- **QA-03:** `PerformancePlanningChecks` evalúa todas las muestras al terminar y conserva el JSON antes de fallar. `--performance-planning` es aserción; `--diagnostic` identifica explícitamente una ejecución diagnóstica. Los cambios de conteo de asignaciones fallan incluso en modo diagnóstico. Las expectativas distinguen finalización, `SCHEDULING_SEARCH_INCONCLUSIVE` con `OCCURRENCE_SEARCH_SIZE_LIMIT` y `PLAN_TOO_MANY_OCCURRENCES`, según los límites actuales; ningún límite del producto cambió. `scripts/comprobar-salida-planificacion.ps1` comprueba un fallo deliberado y la conservación del JSON mediante procesos reales.

### Contratos y fixtures actualizados

`QaFixtures.cs` crea actores, empleo, roles, horarios, definición y contenedor propios con vigencia para los relojes fijos de las pruebas. Las suites de transferencia, reasignación, ciclo de vida, importación y planificación dejan de depender de usuarios históricos o IDs concretos de la simulación. La transferencia compara el conteo previo más su delta, y usa definiciones distintas para las claves de etapas. La reasignación conserva sus escenarios históricos con entregas sintéticas y comprueba registros ajenos sin modificarlos mediante SQL global.

Rotación, operaciones semanales y horarios usan generación real de agenda y su fingerprint. Se incorporaron criticidad y horarios requeridos por el contrato vigente. La prueba de rotación exige además conservar la corrección protegida e informar exactamente su carga residual; no exige moverla para eliminar el exceso. `ScheduleIntegrationChecks` diferencia filas realmente generadas de candidatos y avanza hasta el inicio reservado de la etapa, en lugar de suponer que es siempre la hora nominal.

Se contrastaron específicamente estas expectativas antiguas con las rutas actuales, sin cambiar permisos ni reglas del producto:

- Una hora crítica ya transcurrida produce aviso `CRITICAL_ACTIVATION_PASSED`; no una asignación desplazada ni un rechazo genérico de capacidad (`OperationalPlanGenerator`).
- La revisión fuera de turno devuelve `SCHEDULE_NOT_PRESENT_AT_ACTIVATION`, con causa específica (`ReviewCoreAsync` y `CorrectionPlacement`).
- `ReadAsync` permite la lectura a Subcoordinación según `CheckAuthorityAsync(...allowSubcoordination:true)`; la suite sigue rechazando las mutaciones que no están autorizadas.
- Un proceso **con agenda capturada** revalida capacidad/disponibilidad antes de crear la ejecución. La ausencia posterior a la reserva rechaza la activación con `PLAN_NO_AVAILABLE_WORKER` y rollback de reserva, plan, auditoría y asignaciones. La expectativa anterior de crear `PENDIENTE_ASIGNACION` correspondía a otra ruta de activación; no se relajó el guard de `OperationalProcessService.StartAsync`.
- El downgrade de agenda publicada está expresamente prohibido por migraciones posteriores. `LifecycleChecks` comprueba ese rechazo y la recuperación hacia el esquema actual, conservando autores, comentarios y decisiones; no desactiva el guard para simular un downgrade admitido.
- `Sgol.UserProfileChecks` registra `IPersonAvatarReader`, `OperationalSchedulingPolicyService` y `AssignmentRefreshService`, y verifica el destino real de especializaciones en vez de su texto histórico. `ProcessEditorChecks` envía el nuevo contrato de borrador con base e identificador.

### Matriz de validación de esta implementación

Evidencia local ignorada: `.local/qa-fix-20261030/`. Cada ejecución automática de servicios/HTTP creó una base `sgol_task_checks_qafix_*` o `sgol_profile_checks_qafix_*` nueva. Los logs pueden contener datos privados y no se publican. Los nombres de log siguientes identifican ejecuciones concretas, no cobertura exhaustiva.

| Escenario y resultado esperado | Prueba / entorno | Resultado real y evidencia |
|---|---|---|
| Reproducir pérdidas entre pestañas y fecha máxima antes del arreglo | Nueva regresión HTTP contra binario anterior, CSRF y BD aislada | **Fallido esperado**: fecha máxima 500; B acepta sustitución y pierde A. `red-reproduced.log`, exit no0 |
| Dos bases iguales: primer guardado confirmado y segundo 409, conservar A | `--qa-interactions-only`, Razor HTTP + PostgreSQL | **Aprobado**, `http-final.log` |
| Dos POST simultáneos: exactamente un ganador | Mismo harness, contextos HTTP independientes | **Aprobado**, `http-final.log` |
| Respuesta perdida: reintentar la operación exacta, sin otra revisión/evidencia; reintento antiguo no resucita marcas | HTTP real + comparación BD | **Aprobado**, `http-final.log` |
| Desmarcado deliberado, CSRF ausente, cambio de definición, rollback sin auditoría adicional | HTTP real + comparación de asignación y auditoría | **Aprobado**, `http-final.log` |
| Parcial/completo, revisión individual/masiva, actualización de definición y snapshots históricos, transferencia, ocasiones | `--checklist-only`, servicios y BD aislada | **Aprobado**, `checklist-final.log` |
| Proceso: guardar borrador con intento abierto, entregar y liberar siguiente etapa | Misma suite, planificación/activación/servicios reales | **Aprobado**, `checklist-final.log`; no invalida la revisión del intento |
| Formularios de catálogo, borrador y entrega parcial/completa, evidencia de solo lectura | `--checklist-http-only` | **Aprobado**, `editor-http-v2.log` |
| Normal, mínima, máxima, vacía, inválida e invertida; conservar búsqueda/formulario | Servicios consumidos por Razor HTTP | **Aprobado**, `http-final.log` |
| Reproducir QA-03 contra herramienta anterior conservada | Binario inicial de `.local/qa-20261030/build`, contenedor propio en 5547 | **Fallido esperado**: exit0 con 2 muestras fallidas; 3164 asignaciones antes/después. `planning-baseline-result.json`, `planning-red-results.json` |
| Planificación anual con límites vigentes; preview no escribe asignaciones | Herramienta corregida, mismo contenedor aislado | **Aprobado**, 4/4 expectativas, 3164 asignaciones antes/después. `planning-real.log`, `planning-green-results.json` |
| Muestra deliberadamente fallida debe conservar JSON y devolver no0 en aserción | `comprobar-salida-planificacion.ps1`, dos procesos reales | **Aprobado**: aserción no0, diagnóstico0 explícito. `exit-regression/` |
| Rotación con corrección protegida, semana con un trabajador, rechazo sin capacidad | `--rotation-only`, BD vacía aislada | **Aprobado**, `rotation-final.log` |
| Maestros semanales, reintentos, unicidad BD, roles, catálogo cambiado | `--weekly-operations-only` | **Aprobado**, `weekly-final.log` |
| Permisos y autoasignación ocasional de Subcoordinación/Asesor Externo, preview y rollback por ausencia | `--subcoord-occasional-only` | **Aprobado**, `subcoord-v4.log` |
| Horarios, agenda, crítica vencida, maestros parciales, proceso y rollback de activación por ausencia | `--schedule-integration-only` | **Aprobado**, 38 comprobaciones, `schedule-v8.log` |
| Transferencia e intento de proceso; conservar evidencia y dependencias | `--transfer-lifecycle` en BD nueva | **Aprobado**, `transfer-v2.log` |
| Transferencia y reasignación con registros históricos ajenos | Dos copias nuevas del dump aislado previo; no se volvió a leer ni escribir la simulación | **Aprobado**, `copy-transfer-lifecycle.log`, `copy-reassignment-lifecycle.log`; delta e historial ajeno conservados |
| Reasignación: pausas, turno lleno, capacidad cero, límite de turno, rol, evidencia actual e historial previo | `--reassignment-lifecycle`, BD nueva | **Aprobado**, `reassignment-v3.log` |
| Fragmentos obligatorios de historial, reintento, downgrade protegido y recuperación | `--lifecycle-only` | **Aprobado**, `lifecycle-v2.log` |
| Importación mensual sobre fixtures propios y rollback de datos del escenario | `--monthly-import-only` | **Aprobado**, `monthly.log` |
| Perfiles: servicios, formularios, especialidades, permisos y sesión | `Sgol.UserProfileChecks`, BD propia | **Aprobado**, `profile-final.log`, suite completa |
| Planificación integral y política de responsable | `--scheduling-integration-only`, fixtures propios | **Aprobado tras decisión del usuario**, `owner-policy.log`, exit0. El resultado parcial anterior permanece en `scheduling-v2.log`; se actualizó la expectativa por la política expresamente elegida, sin modificar el algoritmo |
| Compilación y entradas generadas | Builds aislados TaskChecks/Web y UserProfileChecks; comprobador de exclusiones | **Aprobado**, 0 errores/advertencias; `build-next.log`, `profile-build-final.log`, `build-inputs.log` |
| Dos pestañas reales: conservar local y comparar servidor, resolver, desmarcar y entregar | Navegador integrado, host aislado HTTPS7279, sesión `TEST_ONLY` | **Aprobado** en recorrido acotado: conflicto visible, entrega bloqueada hasta resolver; entrega final “En revisión”. No equivale a autenticación real |
| Respuesta HTTP abortada después del commit, cambio local posterior y reintento | Middleware del harness `SGOL_QA_DROP_DRAFT_RESPONSE=1`; navegador real | **Aprobado**: aviso sin falso éxito; reconoce reintento y conserva desmarcado posterior tras recargar. `ui-loss.log` y observaciones del navegador; no es corte de red física |
| Escritorio, móvil320, claro/oscuro/especial, teclado y foco | Pantallas reales en navegador integrado | **Aprobado** en muestra acotada: comparación legible, botones accesibles, foco de resolución en estado y sin overflow horizontal en 320. Supervisión conserva valores y enlaces al mostrar error de rango |
| Firefox/Brave, dispositivos físicos, pérdida física de red, estabilidad de jornada completa y nueva carga general | No ejecutados en esta corrección | **No ejecutado**: no sustituir por el navegador integrado ni los reintentos sintéticos |

Las ejecuciones intermedias que fallaron permanecen en la carpeta local con su causa; una suite solo se marca aprobada donde existe una ejecución posterior completa. Los hosts de vista previa permanecieron deliberadamente esperando intervención mientras se inspeccionaba la pantalla y fueron detenidos al acabar: su terminación manual no se presenta como exit0 de una suite. No se ejecutaron suites destructivas en `sgol_local`, ni se reinició o desplegó el runtime compartido observado al inicio (`reassigned-color-20261030`).

### Estado de las decisiones

1. **Responsable en reacomodo — resuelto el 30/10/2026:** el usuario decidió «Vamos a priorizar lo que ya hace el sistema». Se mantiene la prioridad del inicio más temprano entre personas elegibles, aunque requiera cambiar de responsable; conservar al responsable no tiene preferencia sobre un inicio anterior válido. Las restricciones de turno, capacidad, rol, especialidad, tareas protegidas y confirmación operativa permanecen vigentes. La expectativa antigua de conservar responsable se actualiza por esta decisión expresa, no para ocultar un fallo. Ver validación de cierre al final.
2. **Volumen anual:** se conserva el buscador limitado a 1000 ocurrencias por búsqueda y el límite global de 10000. Dividir el horizonte reduce presión de recursos y exige coordinar reservas entre planes; ampliar la búsqueda requiere mediciones, cancelación y presupuesto de recursos. No se amplió ningún límite ni se trató un rechazo esperado como finalización de la planificación.

Las correcciones no certifican ausencia total de bugs ni disponibilidad en el runtime compartido: el código se verificó en compilaciones y hosts aislados y no fue desplegado.

También se comprobó en navegador la acción **Recargar tarea (conservar copia local)**: tras la recarga ofreció recuperar las marcas locales, las restauró sin mezclarlas con las guardadas y volvió a pedir resolver el conflicto. La resolución por Enter dejó el foco en el mensaje de guardado. Captura local privada: `.local/qa-fix-20261030/conflicto-checklist.jpg`.

### Archivos de esta corrección

- `docs/desarrollo/cambios/auditoria-qa-interacciones-2026-10-30.md`
- `scripts/comprobar-salida-planificacion.ps1`
- `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalChecklist.cs`
- `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Paging.cs`
- `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTaskService.Checklist.cs`
- `src/Sgol.Web/Pages/MyWork/Index.Operational.cs`
- `src/Sgol.Web/Pages/Shared/_ChecklistEvidence.cshtml`
- `src/Sgol.Web/Pages/Tasks/Index.cshtml.cs`
- `src/Sgol.Web/Pages/Validations/_Filters.cshtml`
- `src/Sgol.Web/Pages/Validations/Index.cshtml.cs`
- `src/Sgol.Web/wwwroot/js/checklist.js`
- `tools/Sgol.TaskChecks/ChecklistChecks.cs`
- `tools/Sgol.TaskChecks/LifecycleChecks.cs`
- `tools/Sgol.TaskChecks/MonthlyImportChecks.cs`
- `tools/Sgol.TaskChecks/OperationalSchedulingChecks.cs`
- `tools/Sgol.TaskChecks/PerformancePlanningChecks.cs`
- `tools/Sgol.TaskChecks/ProcessEditorChecks.cs`
- `tools/Sgol.TaskChecks/Program.cs`
- `tools/Sgol.TaskChecks/QaFixtures.cs`
- `tools/Sgol.TaskChecks/QaInteractionChecks.cs`
- `tools/Sgol.TaskChecks/ReassignmentLifecycleChecks.cs`
- `tools/Sgol.TaskChecks/RotationChecks.cs`
- `tools/Sgol.TaskChecks/ScheduleIntegrationChecks.cs`
- `tools/Sgol.TaskChecks/TransferChecks.cs`
- `tools/Sgol.TaskChecks/WeeklyOperationsChecks.cs`
- `tools/Sgol.UserProfileChecks/Program.cs`

### Cierre adicional de concurrencia de servicios

La revisión final añadió una regresión con un `DbContext` que ya tenía la asignación rastreada antes del guardado de otra pestaña. Sin recargar la entidad, se reproducía `DbUpdateConcurrencyException` en vez del conflicto recuperable; la protección de persistencia evitó sobrescribir las marcas, pero no devolvía el contrato esperado. Evidencia roja: `tracked-red.log`.

`SaveChecklistDraftAsync` ahora refresca la entidad bajo el bloqueo y vuelve a comprobar propietario/estado antes de comparar la base. Esto cubre además una transferencia ocurrida después de una lectura rastreada. La validación final de esta variante y de las regresiones HTTP está en `tracked-green.log`; el recorrido de checklist/proceso se repite en `checklist-close.log`. Las ejecuciones anteriores siguen registradas por separado.

Cierre comprobado: `tracked-green-result.json` y `checklist-close-result.json` registran **exit0**. La compilación final y el diff no presentan errores. El pendiente de política registrado en aquel cierre se resuelve en el apartado siguiente.


### Decisión de reacomodo: priorizar el inicio más temprano — 30/10/2026

La decisión del usuario cierra exclusivamente el pendiente de responsable. No modifica el algoritmo de `OperationalCapacityService.BuildAsync`, que ordena las opciones válidas por `Placement.StartsAt` antes de sus criterios de desempate. El límite del buscador anual permanece sin cambios y su eventual ampliación sigue fuera del alcance.

`OperationalSchedulingChecks.TargetReflow` ahora exige simultáneamente que el responsable original siga siendo una alternativa válida a las 10:00, que la propuesta elija a otra persona elegible a las 09:00 y que la vista previa conserve todas las asignaciones sin aplicarlas. Se conserva la comprobación del conflicto de la tarea crítica. Se repite la suite integral en una base nueva `sgol_task_checks_qafix_*`; no se cambia ni recalcula ninguna asignación de la simulación compartida.

| Escenario | Esperado | Prueba / entorno | Resultado |
|---|---|---|---|
| Alternativa del responsable original | Sigue siendo elegible a las 10:00 | Servicio de agenda y PostgreSQL aislado | **Aprobado** |
| Otra persona puede comenzar antes | Se propone inicio a las 09:00 con otra persona elegible | `TargetReflow`, mismo entorno | **Aprobado** |
| Propuesta sin confirmar | Conserva todas las asignaciones y mantiene el conflicto crítico | Comparación íntegra antes/después del preview | **Aprobado** |
| Suite integral | Completa planificación, concurrencia, selección activa, procesos y reacomodo | `--scheduling-integration-only`, BD nueva | **Aprobado**, exit0; `.local/qa-fix-20261030/owner-policy.log` y `owner-policy-result.json` |

Compilación aislada aprobada con 0 errores y 0 advertencias (`build-owner-policy.log`). Alcance: prueba y documentación; sin cambio funcional, migración ni interfaz. No requiere navegador ni despliegue. El runtime compartido se mantuvo sin reiniciar y no se escribieron datos en `sgol_local`.
