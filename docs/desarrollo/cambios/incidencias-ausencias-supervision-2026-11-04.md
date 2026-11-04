# Plan: incidencias de ausencia y redistribución en Supervisión

Fecha: 2026-11-04. Zona operativa: `America/Mexico_City`.
Estado: **propuesto; no implementado ni desplegado**. Cambio mixto: reglas, persistencia e interfaz.
Alcance de esta entrega: investigación estática y plan. No se registró la ausencia ni se modificaron asignaciones, disponibilidad o bono de ninguna persona.

## 1. Objetivo y decisiones confirmadas

Agregar **Incidencias** a Supervisión para registrar la ausencia de una colaboradora hoy o en una fecha futura y resolver, en un mismo recorrido, disponibilidad, redistribución, retiro de pendientes y efecto en el bono.

Decisiones del usuario durante la planificación:

- Listar colaboradoras dentro del alcance del actor. Dirección confirma; otros supervisores solo consultan dentro de su alcance.
- Fecha predeterminada: hoy. Permitir fecha futura. La ausencia afecta el día seleccionado, sin cambiar el catálogo ni los otros días.
- Las tareas pendientes ya vencidas al confirmar no se reasignan: se retiran del trabajo activo y se excluyen del bono de la ausente.
- Distribuir las no vencidas equilibrando la ocupación restante según tiempo disponible, trabajo pendiente y elegibilidad. No reducir artificialmente los minutos de una tarea.
- Explicar lo que no puede reasignarse y ofrecer una o dos decisiones aplicables por caso, siguiendo Actualizar Asignaciones.
- Permitir sobrecarga **explícita dentro del turno**. No autoriza trabajar durante una ausencia, fuera del turno o sin la especialidad requerida.
- Las obligaciones retiradas no aportan base ni descuentos al bono de la ausente. Las transferidas corresponden a quien las recibe, sin duplicar obligación.
- Si se retira una etapa necesaria, conservar el proceso **pendiente de reprogramación**, sin cancelar el proceso completo ni habilitar sucesoras como si se hubiera ejecutado.
- Para tareas semanales o mensuales que vencen otro día, conservar la obligación y su bono; excluir el día ausente de disponibilidad y revisar los días restantes. Si no cabe, mostrar el conflicto sin ampliar el plazo ni retirar toda la obligación automáticamente.

«Una sola acción» se interpreta como una entrada y una confirmación conjunta, después de revisar consecuencias. No equivale a guardar cambios irrevisables al pulsar la fila de una persona.

## 2. Evidencia y límites de la investigación

Se consultaron el protocolo del repositorio, RTK, el índice Graphify vigente, Inicio, mapas de dominio y diseño, reglas documentales, las referencias de actualización de asignaciones, bono y patrones/componentes visuales. Las lecturas de Inicio, dominio y reglas realizadas en el intercambio anterior se reutilizaron.

El checkout contiene modificaciones previas, incluidas `AssignmentRefreshService.cs`, `Refresh.cshtml`, `Refresh.cshtml.cs`, `RefreshCaseText.cs`, arrastres, pruebas y migraciones. Deben conservarse e integrarse; este plan no se atribuye esos cambios. No se comprobó el artefacto compartido ni se hizo una consulta autenticada de los datos del caso. Los hallazgos siguientes describen el **código inspeccionado**, no una certificación del host.

| Fuente propietaria | Comportamiento observado / consecuencia para el plan |
|---|---|
| [Refresh.cshtml](../../../src/Sgol.Web/Pages/Tasks/Refresh.cshtml), [RefreshCaseText.cs](../../../src/Sgol.Web/Pages/Tasks/RefreshCaseText.cs) | Casos con tarea, causa, acción, efecto y detalles; seleccionar opciones exige Revisar cambios y luego confirmar con motivo. Reutilizar el patrón, no las decisiones propias del catálogo. |
| [AssignmentRefreshService.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/AssignmentRefreshService.cs): `PreviewAsync`, `BuildAsync`, `ApplyAsync` | Huella, decisión explícita, reintento por clave, bloqueo y transacción. Se conservan entregas/cierres y el retiro lógico preserva evidencia. El retiro de una etapa puede cancelar la ejecución del proceso: **no reutilizar ese efecto para ausencias**. |
| [OperationalCapacityService.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCapacityService.cs): `LedgerAsync`, `BuildAsync`, `ApplyAsync`, `CapacityLedger` | Consulta asignaciones y reservas; capacidad por fecha y disponibilidad; restricciones para tareas con avances, ocasiones, correcciones y procesos. No es una operación integral de ausencia. `DayCapacity` y `Occupancy` son cálculos diarios, no un balance del tiempo restante desde ahora. |
| [OperationalCapacityService.Scheduling.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCapacityService.Scheduling.cs) y [OperationalAgendaSearch.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalAgendaSearch.cs) | Reacomodo y búsqueda reutilizables. No confundir presupuestos de motores diferentes; agotamiento de búsqueda no prueba imposibilidad. |
| [OperationalSupervisionService.Transfer.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Transfer.cs): `BuildTransferAsync`, `TransferAsync` | Transfiere conservando evidencia y recibo; ajusta etapa/intento cuando corresponde. Valida recepción inmediata en turno; no cubre por sí solo ausencias futuras y reservas no activadas. |
| [EfAvailabilityAdministrationService.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Organization/EfAvailabilityAdministrationService.cs): `PutAsync` | Disponibilidad versionada, idempotencia, auditoría y permiso de Dirección; tiene su propia transacción. No encadenar llamadas públicas con commits separados. |
| [TaskBonusService.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskBonusService.cs): `ExcludeAsync`, `CalculateAsync`, `Materialize` | Exclusión explícita por obligación/persona/semana; cierre protegido; obligaciones agrupadas por raíz de correcciones/etapas. Las transferencias tienen tratamiento según momento y antecedentes. No basta cambiar `PersonId` o retirar una fila para garantizar el resultado solicitado. |
| [TaskBonusCalculator.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskBonusCalculator.cs), [TaskBonusPersistence.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskBonusPersistence.cs), [TaskBonusCommitInterceptor.cs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskBonusCommitInterceptor.cs) | Hechos e historial, cancelación distinta de exclusión y cálculo semanal. Sin base no se inventa un 100 %. Mantener captura de hechos al confirmar. |
| [SectionNavigation.cs](../../../src/Sgol.Web/Presentation/Navigation/SectionNavigation.cs), [Workload.cshtml](../../../src/Sgol.Web/Pages/Validations/Workload.cshtml), [_Capacity.cshtml](../../../src/Sgol.Web/Pages/Validations/_Capacity.cshtml) | Integración de apartado, lista de personas, carga y vista previa existentes. La autorización debe verificarse también en servidor. |

## 3. Recorrido propuesto

Ruta propuesta: `/supervision/incidencias`; archivos nuevos `Pages/Validations/Incidents.cshtml` y `.cshtml.cs`.

1. Lista de colaboradoras con fecha, estado de disponibilidad/incidencia, pendientes no vencidos, vencidos y minutos afectados. Consultas por fecha/nombre; no exponer personas fuera del alcance. El estado inicial no declara que una persona asistió solo porque no tiene ausencia registrada.
2. Acción **Registrar ausencia**. Fecha predeterminada hoy; motivo obligatorio. Una persona y un día por operación en la primera versión. No incluir registro retroactivo de días anteriores.
3. Vista previa sin escrituras: ausencia propuesta, tareas a transferir, tareas a retirar, casos pendientes de decisión y trabajo que se conserva. Mostrar separadamente carga restante antes/después por destinataria y obligaciones excluidas del bono de la ausente.
4. Resolver casos con una o dos opciones pertinentes y **Revisar cambios**. Cambiar fecha, destinataria, excepción o motivo invalida la propuesta correspondiente. No guardar decisiones por marcar una casilla.
5. **Confirmar ausencia y cambios** guarda todo el conjunto revisado en una transacción. No permitir pendientes sin disposición explícita ni presentar como exitosa una redistribución incompleta.
6. Recibo con persona, día, actor, motivo, hora, cambios realizados y procesos pendientes de reprogramación. Reabrir permite consultar y verificar un reintento, sin aplicar otra vez.

Las ausencias futuras se guardan ahora como indisponibilidad de esa fecha. Las asignaciones existentes se redistribuyen para esa fecha, sin activarse antes. Si todavía no hay plan, guardar la incidencia sin inventar tareas: la generación posterior debe respetar la indisponibilidad. Proteger también activación de planes preparados y habilitación de etapas para evitar que reservas antiguas vuelvan a asignar trabajo a la ausente.

## 4. Clasificación de asignaciones y tiempo

Usar reloj de servidor. `DueAt <= instante de confirmación` significa vencida, incluido el límite exacto. La vista previa muestra su hora de cálculo; si una tarea cruza el vencimiento o cambia un estado relevante antes de confirmar, rechazar con explicación y recalcular. No cambiar silenciosamente una transferencia aprobada por un retiro.

| Estado | Propuesta |
|---|---|
| Pendiente del día afectado, no vencida, sin actividad | Buscar destinataria elegible y hueco; si falla, mostrar resolución. |
| Pendiente del día, ya vencida | Retiro lógico y exclusión del bono; jamás reasignar desde esta incidencia. |
| Con avance, borrador, checklist, texto o archivos | Conservar autoría y evidencia. Mostrar transferencia con progreso conservado o retiro con historial; no reiniciar captura ni simular entrega. Usar esfuerzo restante acreditado y revisar explícitamente una estimación manual si hace falta. |
| Entregada, pendiente de revisión, concluida, no satisfecha o sustituida | Conservar. Si contradice una ausencia de día completo, mostrar el conflicto; no retirar ni excluir una obligación cumplida automáticamente. Resolver la contradicción antes de confirmar como ausencia completa. |
| Vencida de otro día / arrastre con día evaluado anterior | Fuera del retiro y exclusión automática de este día. Mostrar por separado si afecta la carga. No borrar deuda histórica por una ausencia nueva. |
| Etapa disponible / reserva sin asignación ejecutable | Coordinar asignación, intento, etapa y reserva. No tratar IDs de reservas como si fueran asignaciones. |
| Ya retirada o afectada por la misma incidencia | No duplicar retiro ni exclusión. Reproducir recibo o informar estado anterior. |
| Tarea que abarca varios días o tiene día operativo distinto del evaluado | Presentar caso específico; no excluir toda la obligación semanal/mensual por un día de ausencia. Aplicar la regla confirmada de la sección 11. |

La clasificación debe usar fechas originales y procedencia, no únicamente `StartsAt` actual: hoy una consulta puede incluir vencidas históricas, arrastres y obligaciones por progreso cuyo día de evaluación es el vencimiento.

## 5. Reparto proporcional del trabajo pendiente

No modificar la duración definida en catálogo, ni subir porcentajes de capacidad permanentemente para ocultar sobrecarga. Calcular un balance específico de la incidencia:

- Tiempo de turno restante desde el momento de aplicación para hoy, y turno efectivo completo para fecha futura, descontando descansos y excepciones.
- Capacidad operativa restante compatible con el límite diario vigente y los compromisos ya consumidos/reservados; evitar contar dos veces los mismos minutos. Una tarea completada no se trata como demanda pendiente, aunque deba respetarse una reserva protegida por su política.
- Demanda pendiente de cada receptora, reservas de procesos incluidas. Los atrasos que siguen siendo responsabilidad de esa receptora se muestran y no se eliminan para conseguir un porcentaje favorable.
- Separar esfuerzo en minutos de ocupación de calendario con margen de cierre. Ambas restricciones deben cumplirse; no sumar o descontar dos veces el margen.
- Objetivo: equilibrar `(demanda pendiente + esfuerzo recibido) / capacidad operativa restante`, sujeto a elegibilidad y huecos reales. Evitar división por cero. Mostrar también minutos libres y exceso en términos comprensibles.

Ejemplo ilustrativo, no prueba ejecutada: A tiene 120 minutos utilizables y 60 pendientes; B tiene 60 utilizables y 15 pendientes. Ante 45 minutos divisibles de trabajo compatible, 20 para A y 25 para B igualan aproximadamente la ocupación al 66,7 %. Con tareas indivisibles, acercarse a ese reparto sin partirlas ni inventar fracciones de entregas.

Ordenar primero compromisos más restrictivos y críticos, luego el resto según su ventana; desempatar de forma estable. Reutilizar las reglas de rol, especialidad, prueba laboral, empleo vigente, participación de personas distintas, calendario, cobertura de cierre y dependencias. Evaluar el rol y empleo en la fecha objetivo, no solo hoy. Simular el conjunto, recalcular después de cada movimiento y permitir retroceso acotado para no declarar imposible un reparto por una primera elección deficiente.

Reacomodar vecinas no vencidas solo dentro del día y las reglas vigentes, mostrando todos los movimientos indirectos. Las tareas protegidas o activadas requieren decisión explícita; no renovar plazos vencidos de ninguna persona. La sobrecarga autorizada es local a las asignaciones/fecha elegidas, nunca una modificación global de horario o capacidad.

## 6. Excepciones siguiendo Actualizar Asignaciones

Separar **caso que admite decisión**, **restricción que no se puede dispensar** y **fallo técnico/revisión obsoleta**. Modelo propuesto: código, IDs de entidades, tarea, persona/día, causa comprobada, opciones permitidas, efecto, datos antes/después, aceptación y detalles. Validar las opciones otra vez en servidor; no aceptar un código arbitrario enviado desde el navegador.

| Causa | Opciones visibles, máximo dos |
|---|---|
| Falta de capacidad con candidatas elegibles | **Reacomodar dentro del día** o **Elegir destinataria y autorizar sobrecarga**. Tras revisar, permitir retiro si ninguna alternativa resuelve el caso. |
| Ninguna persona tiene rol/especialidad o está disponible | **Retirar por ausencia**. Detalles explican por qué se descartó cada candidata; no ofrecer «forzar» que anule elegibilidad. Si existe otra candidata compatible fuera de la selección inicial, ofrecer **Elegir otra colaboradora**. |
| Bloque fuera de turno o duración sin intervalo posible | **Buscar otro horario/persona dentro del día** o **Retirar por ausencia**. La sobrecarga no inventa minutos fuera del turno. |
| Hay actividad/evidencia protegida | **Transferir conservando lo realizado** o **Retirar conservando historial**, si las demás reglas lo permiten. |
| Solicitud de arrastre/posposición pendiente | **Resolver en Supervisión** o **Cerrar la solicitud por incidencia y retirar**, únicamente si se implementa una disposición específica, auditable y coherente con todos sus elementos. No aprobar/rechazar falsamente la petición. |
| Etapa imprescindible sin sustituta | **Retirar asignación y dejar proceso pendiente de reprogramación**. Mostrar las sucesoras bloqueadas, reservas que se liberan y entregas que se conservan. No cancelar el proceso completo. |
| Falta una instantánea histórica o carga fiable | **Acreditar/adoptar los datos necesarios** cuando exista un contrato autorizado; si no, bloquear con entidad y salida concreta. No inventar datos. |
| La búsqueda agotó su presupuesto | **Volver a calcular con selección explícita** o **Retirar la tarea por decisión operativa**, dejando claro que no se demostró falta de espacio. No guardar resultados parciales del buscador. |
| Datos cambiaron, nueva entrega, ausencia simultánea o vencimiento | **Revisar propuesta actualizada**. No conservar autorizaciones sobre consecuencias diferentes. |

La autorización de sobrecarga informa destinataria, minutos de exceso, ocupación y conflictos. Permite exceder el presupuesto operativo y aceptar empalmes expresamente mostrados, conservando intervalos dentro del turno. Si la ejecución exige recursos o bloques exclusivos incompatibles, ese impedimento sigue siendo no dispensable. No marcar por defecto ninguna excepción.

## 7. Procesos, repetición y nuevas asignaciones

Agregar una transición de dominio específica para etapa afectada por ausencia. Investigar y actualizar las restricciones SQL y contratos de estados antes de elegir nombres finales; no reutilizar `CANCELADA` del proceso para representar reprogramación pendiente.

- Retirar la asignación del día sin declarar satisfecha la etapa. Conservar ejecución, intentos, archivos, ítems y etapas entregadas.
- Liberar únicamente las reservas que ya no son viables y dejar la etapa y sucesoras dependientes sin ejecución automática. Mostrar el alcance completo en la propuesta.
- El temporizador y la continuación no deben reintentar indefinidamente ni recrear el trabajo retirado. La reprogramación requiere una acción explícita con nueva comprobación de capacidad.
- La futura reprogramación conserva la raíz histórica, pero debe distinguir la obligación excluida del día ausente de un nuevo compromiso evaluable. No propagar ciegamente la exclusión a todos los intentos posteriores de esa etapa.
- La creación ocasional, generación de planes, copias/activación de planes preparados, actualización de catálogo, correcciones y continuación de procesos deben consultar la ausencia vigente para la fecha. Una incidencia aplicada no es una lista congelada que permita asignaciones posteriores a la ausente.
- Varias ausencias el mismo día se resuelven con revisiones y bloqueo común; la segunda operación recalcula sobre el resultado de la primera.

## 8. Bono e historial

Excluir es quitar la obligación de la base **y** sus descuentos; no registrar entrega, perdón con puntos, ni un 100 % ficticio. Para las transferidas, generar recibo real y comprobar que la base queda únicamente con la receptora elegible. La exclusión de la ausente no debe excluir el trabajo recibido por otra persona.

Persistir vínculo entre incidencia, asignación, raíz de obligación, persona, día evaluado y motivo. Resolver las raíces de correcciones y etapas con `Materialize`; no asumir que `AssignmentId` siempre es `ObligationId`. Verificar especialmente antecedentes de devolución/rechazo, donde la transferencia actual puede conservar la obligación con la persona original.

Los días restantes de la semana conservan su evaluación. Una semana completa sin base y sin trabajo acreditado mantiene «Sin tareas evaluables» y la regla actual de bono 0 %, no una calificación artificial. Si no hay base pero sí otros días trabajados sin obligaciones, conservar la incidencia de datos existente; no ocultarla por esta ausencia. No reabrir cierres semanales ni cambiar resultados históricos.

Mantener una decisión estructurada de exclusión por incidencia, no depender de interpretar el texto del motivo. Una ausencia futura puede registrarse sin obligaciones y debe impedir que se generen después para la ausente. Si aparecen por una ruta inconsistente, identificar el conflicto y corregir el productor; no excluirlas silenciosamente para ocultarlo.

## 9. Persistencia y transacción

Proponer `OperationalIncidence` y detalle por disposición: persona, fecha, tipo AUSENCIA, estado/revisión, actor, motivo, tiempos, clave de intención, huella y recibo; por tarea, identidad, acción, antes/después, destinataria, autorización excepcional y vínculos de proceso/bono. Restricción de una incidencia vigente por persona/fecha/tipo y claves únicas por operación/disposición.

Crear `OperationalIncidenceService` con consulta, `PreviewAsync` y `ApplyAsync`. Extraer operaciones internas reutilizables de disponibilidad, transferencia, retiro y exclusión que participen en la **misma transacción**. No invocar consecutivamente los métodos públicos actuales que abren/cierran sus propias transacciones. Coordinar el bloqueo operativo y los bloqueos/versiones de disponibilidad con orden consistente; verificar que sus otros productores participen en el mismo protocolo o que el aislamiento detecte el conflicto.

Confirmación:

1. Autorizar Dirección, alcance y fecha; comprobar replay por clave y hash de intención.
2. Bajo bloqueo, recargar personas, disponibilidad, agenda, capacidad, tareas, solicitudes, procesos y estado del bono.
3. Revalidar hora real, revisiones, conjunto de afectados y huella. Huella de propuesta no incluye un reloj cambiante que la invalide por cada milisegundo; sí incluye límites temporales y decisiones, comprobados aparte al aplicar.
4. Persistir disponibilidad versionada, transferencias con recibos, retiros, transición de procesos, exclusiones y auditoría.
5. Capturar hechos del bono con el protocolo existente, guardar recibo y confirmar una sola vez. Fallo en cualquier parte revierte todo.

Una repetición con la misma clave devuelve el resultado anterior; misma clave con otra intención se rechaza. Sin commit confirmado, la UI presenta resultado incierto y permite consultar/reintentar con la misma clave. Prohibir alteraciones globales de catálogo, limpieza de evidencia o migraciones que excluyan retrospectivamente obligaciones existentes.

## 10. Secuencia de implementación y validación

1. Formalizar las reglas confirmadas y los límites de la sección 11 en contratos y ejemplos fechados.
2. Implementar clasificador del día y balance de tiempo restante, con pruebas deterministas de límite temporal, esfuerzo y elegibilidad.
3. Añadir entidades/migración y operaciones transaccionales compartidas. Integrar procesos y todas las rutas que producen trabajo, evitando recreaciones.
4. Implementar servicio de vista previa/confirmación, decisiones y diagnósticos. Integrar recibos y cálculo del bono; probar rollback y reintentos antes de la UI.
5. Construir Incidencias con navegación, lista, casos y recibo. Reutilizar tokens, campos, paneles y patrones de `assignment-refresh.css`/`assignment-refresh.js`, extrayendo componentes comunes solo cuando sea útil. No crear un estilo independiente.
6. Ejecutar integración, formularios Razor y navegador sobre base aislada; si hace falta reproducir el estado real, copia aislada de la simulación, identificada antes de escribir. No usar la base compartida como suite.
7. Revisar diff y migración; realizar publicación únicamente cuando forme parte del alcance autorizado. Antes de operar el host, seguir Inicio → infraestructura → instalación local → base documental, y acceso LAN si aplica; verificar artefacto y listeners.
8. Tras activación, actualizar las notas existentes de supervisión, horarios/cobertura, planificación, procesos, bono, modelo de historial, navegación/pantallas y patrones afectadas. Actualizar mapa/inventario y Graphify si corresponde. Mantener este plan fuera de la bóveda; sincronizar copia versionada solo de notas pertinentes y revisando divergencias.

## 11. Límites de alcance y obligaciones de varios días

- **Obligaciones de varios días — decisión confirmada:** conservar la obligación y su bono cuando vence otro día; retirar únicamente la demanda del día ausente y recalcular el trabajo restante en días hábiles compatibles, sin extender el plazo. Si no cabe, mostrar un caso específico para resolver la capacidad o una reasignación explícita. No retirar toda la obligación automáticamente ni dividir sus puntos. La tarea conserva su esfuerzo total pendiente; solo cambia la distribución de ese esfuerzo en los días disponibles.
- **Corrección de una ausencia registrada por error:** primera versión no revierte automáticamente transferencias ni restaura retiros. Debe diseñarse una corrección con vista previa antes de ofrecer «Anular»; restaurar solo disponibilidad no revierte el bono. No prometer reversibilidad automática. El historial permite reconstruir y preparar la corrección.
- **Ausencia parcial:** fuera de esta versión de ausencia de día completo. Evidencia de trabajo ya entregado exige resolver el caso, sin borrar cumplimiento.
- El caso particular que motivó el plan no se ha inspeccionado en base compartida; confirmar identidad por ID, asignaciones y publicación efectiva será parte de una operación posterior, no de este documento.

## 12. Matriz de pruebas propuestas

Todas las filas están **propuestas; no ejecutadas** en esta entrega. «Integración» incluye persistencia real PostgreSQL y verificación antes/después; formularios incluyen autenticación, binding y CSRF.

| Escenario / precondición y acción | Resultado esperado | Prueba y entorno | Estado |
|---|---|---|---|
| Ausente hoy, pendientes compatibles, dos receptoras con turnos/carga distintos | Reparto aproxima ocupación restante, mantiene esfuerzo y no excede restricciones | Reglas + integración aislada | Propuesto |
| Tarea a un instante antes, exactamente al vencimiento y después | Solo la no vencida se transfiere; las demás se retiran/excluyen | Reloj controlado + formulario aislado | Propuesto |
| Tarea vence entre vista previa y confirmación | Conflicto explicativo, ningún cambio parcial y revisión nueva | Integración + formulario aislado | Propuesto |
| Vencida hoy y vencida de día anterior en la misma lista | Retirar solo la del día afectado; conservar deuda de otros días | Integración y consultas aisladas | Propuesto |
| Ausencia futura con plan existente y receptora no presente hoy pero disponible ese día | Programación futura válida, sin activación inmediata | Integración + formulario aislado | Propuesto |
| Ausencia futura sin plan, luego generar/copiar/activar/asignar/corregir | No crear trabajo para la ausente; conservar incidencia sin duplicados | Recorridos de productores reales, aislado | Propuesto |
| Nadie elegible por especialidad, rol, empleo, prueba o ausencia | Causa por candidata; retiro explícito; ninguna excepción invalida requisitos | Integración + navegador aislado | Propuesto |
| Presupuesto operativo insuficiente, turno suficiente | Sobrecarga solo después de autorización explícita y con exceso visible | Servicio + formulario/navegador aislado | Propuesto |
| Fuera de turno, descanso, duración físicamente imposible o recurso exclusivo | Sobrecarga no elimina restricciones; ofrecer salida válida | Integración aislada | Propuesto |
| Reacomodo de vecinas, activadas protegidas, críticas y margen | Mostrar cambios indirectos; preservar anclas y plazos vencidos | Integración + formulario aislado | Propuesto |
| Progreso parcial, checklist, texto, fotos y adjuntos; transferir o retirar | Autoría, evidencia y avance intactos; sin entrega simulada | Integración + formulario aislado | Propuesto |
| Ocasional, corrección, repetición, personas distintas, carga por ítems | Elegibilidad y procedencia correctas; no omisiones silenciosas | Integración aislada | Propuesto |
| Solicitud de arrastre/posposición parcial pendiente | Resolver o cerrar por incidencia explícitamente; restantes conservados | Formularios e integración aislada | Propuesto |
| Entregada, revisión pendiente o cierre antes/durante la operación | No retirar ni mover; conflicto concurrente si cambió | Integración concurrente aislada | Propuesto |
| Etapa activa exclusiva, reserva futura y sucesoras | Proceso pendiente de reprogramación; no cancelado ni satisfecho; reservas coherentes | Integración del proceso y temporizador aislados | Propuesto |
| Reprogramar después una etapa retirada por ausencia | Conserva raíz histórica; nuevo compromiso no hereda indebidamente exclusión | Integración proceso + bono aislada | Propuesto |
| Obligación de varios días / día original distinto del operativo | Conservar obligación y bono, redistribuir esfuerzo restante sin ampliar plazo; explicar falta de capacidad | Integración aislada | Propuesto |
| Retiro con descuento previo; transferencia con antecedentes de devolución | Ausente sin base/descuento de obligaciones afectadas; receptora sin duplicación ni deuda ajena | `TaskBonus*Checks` ampliados, aislado | Propuesto |
| Un día excluido y otros días evaluables; semana entera sin base | Mantener evaluación de otros días y estado sin tareas correcto | Reglas + integración bono aislada | Propuesto |
| Corte semanal y cierre persistido concurrente | No reabrir ni reescribir resultado cerrado | Integración con reloj controlado | Propuesto |
| Dos incidencias simultáneas, doble envío, respuesta perdida | Recalcular cuando proceda; replay único; recibo verificable | Concurrencia + HTTP aislado | Propuesto |
| Fallo después de disponibilidad, transferencia o exclusión | Rollback de todas las entidades y hechos de bono | Inyección de fallo, base aislada | Propuesto |
| Dirección, otros supervisores, persona fuera de alcance, payload manipulado | Solo Dirección confirma; consulta limitada; servidor rechaza abuso | Formularios HTTP reales aislados | Propuesto |
| Búsqueda interrumpida vs imposibilidad demostrada | Mensajes distintos, entidad identificada, sin propuesta parcial falsa | Reglas y presentación aisladas | Propuesto |
| Mi trabajo, supervisión, carga, tablero, detalle, historial y reportes | Retiradas fuera del trabajo activo; historial accesible; carga/bono coherentes | Consultas y navegador aislado | Propuesto |
| 320 px, móvil y escritorio, temas, teclado, foco y texto largo | Lista/casos legibles; foco en errores; estados vacío/carga/error/éxito correctos | Pantalla real autenticada Chromium/WebKit | Propuesto |
| Migración y regresiones de actualización, transferencias y capacidad | Datos previos preservados; sin romper recorridos existentes | Build + migración/integración aislada | Propuesto |

Reutilizar y extender `AssignmentRetirementChecks`, `AssignmentRefreshChecks`, `RefreshOverdueCapacityChecks`, `CalendarPreservationChecks`, `TransferChecks`, `CurrentAssignmentTransferChecks`, `CapacityRuleChecks`, `DistributionChecks` y `TaskBonusChecks`/`TaskBonusLifecycleChecks`/`TaskBonusHttpChecks`; añadir `IncidenceChecks`, `IncidenceHttpChecks` y navegador de Incidencias. Los nombres nuevos son propuestas, no archivos existentes.

## 13. Criterios de aceptación y cierre de esta entrega

El flujo futuro queda aceptable cuando ninguna tarea del alcance quede sin destino explícito, las vencidas no se transfieran, el reparto sea explicable, las excepciones sean particulares y revisadas, el bono refleje exclusión sin duplicación, y todo se confirme atómicamente con historial y reintento seguro. La ausencia no debe cambiar catálogo, empleo, otros días ni cierres ya emitidos.

En esta entrega se investigó el código y se recogieron decisiones de negocio; no se ejecutaron escenarios funcionales, compilación, migración, navegador ni operaciones sobre la app. Esas comprobaciones no corresponden a un cambio exclusivamente documental. La validación del producto permanece propuesta. Las decisiones principales de negocio de este plan quedaron confirmadas por el usuario; la corrección de incidencias erróneas y las ausencias parciales quedan fuera de esta versión.
