# Plan: incidencias de ausencia y redistribución en Supervisión

Fecha: 2026-11-04. Zona operativa: `America/Mexico_City`.
Estado: **desplegado en la simulación compartida por autorización posterior del usuario; validación principal ejecutada y cobertura ampliada pendiente según las secciones 12 y 14**. Cambio mixto: reglas, persistencia e interfaz.
Código confirmado y subido: `e9273b1`, posterior a los cambios previos conservados en `8ee3330`. Se aplicaron únicamente las migraciones necesarias y se publicó Web. No se registró la ausencia de Alejandra ni ninguna otra incidencia en la simulación compartida. El despliegue y sus comprobaciones constan en la sección 15; la sección 14 conserva el registro de implementación y pruebas anteriores.

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

La investigación inicial encontró modificaciones previas, incluidas `AssignmentRefreshService.cs`, `Refresh.cshtml`, `Refresh.cshtml.cs`, `RefreshCaseText.cs`, arrastres, pruebas y migraciones. Se conservaron en `8ee3330` antes de implementar. Se contrastaron productores, consumidores y contratos del código actual; el host compartido se inspeccionó sin reiniciarlo ni publicar. Los hallazgos de esta sección describen el **código previo inspeccionado**, no funciones nuevas disponibles en la app compartida. No se consultaron ni modificaron datos del caso particular.

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

Ruta implementada y publicada: `/supervision/incidencias`; archivos `Pages/Validations/Incidents.cshtml` y `.cshtml.cs`. Requiere una sesión y alcance autorizados.

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

Esta matriz conserva la cobertura amplia prevista. Las filas marcadas **No ejecutado integralmente** están **no ejecutadas en su combinación completa**; no deben interpretarse como aprobadas por las pruebas parciales de la sección 14. La evidencia ejecutada se detalla allí. «Integración» usa PostgreSQL real aislado; los formularios usan sesiones sintéticas inyectadas, binding y CSRF reales, sin probar inicio de sesión ni MFA.

| Escenario / precondición y acción | Resultado esperado | Prueba y entorno | Estado |
|---|---|---|---|
| Ausente hoy, pendientes compatibles, dos receptoras con turnos/carga distintos | Reparto aproxima ocupación restante, mantiene esfuerzo y no excede restricciones | Reglas + integración aislada | No ejecutado integralmente; cobertura parcial en §14 |
| Tarea a un instante antes, exactamente al vencimiento y después | Solo la no vencida se transfiere; las demás se retiran/excluyen | Reloj controlado + formulario aislado | No ejecutado integralmente; cobertura parcial en §14 |
| Tarea vence entre vista previa y confirmación | Conflicto explicativo, ningún cambio parcial y revisión nueva | Integración + formulario aislado | No ejecutado integralmente; cobertura parcial en §14 |
| Vencida hoy y vencida de día anterior en la misma lista | Retirar solo la del día afectado; conservar deuda de otros días | Integración y consultas aisladas | No ejecutado integralmente; cobertura parcial en §14 |
| Ausencia futura con plan existente y receptora no presente hoy pero disponible ese día | Programación futura válida, sin activación inmediata | Integración + formulario aislado | No ejecutado integralmente; cobertura parcial en §14 |
| Ausencia futura sin plan, luego generar/copiar/activar/asignar/corregir | No crear trabajo para la ausente; conservar incidencia sin duplicados | Recorridos de productores reales, aislado | No ejecutado integralmente; cobertura parcial en §14 |
| Nadie elegible por especialidad, rol, empleo, prueba o ausencia | Causa por candidata; retiro explícito; ninguna excepción invalida requisitos | Integración + navegador aislado | No ejecutado integralmente; cobertura parcial en §14 |
| Presupuesto operativo insuficiente, turno suficiente | Sobrecarga solo después de autorización explícita y con exceso visible | Servicio + formulario/navegador aislado | No ejecutado integralmente; cobertura parcial en §14 |
| Fuera de turno, descanso, duración físicamente imposible o recurso exclusivo | Sobrecarga no elimina restricciones; ofrecer salida válida | Integración aislada | No ejecutado integralmente; cobertura parcial en §14 |
| Reacomodo de vecinas, activadas protegidas, críticas y margen | Mostrar cambios indirectos; preservar anclas y plazos vencidos | Integración + formulario aislado | No ejecutado integralmente; cobertura parcial en §14 |
| Progreso parcial, checklist, texto, fotos y adjuntos; transferir o retirar | Autoría, evidencia y avance intactos; sin entrega simulada | Integración + formulario aislado | No ejecutado integralmente; cobertura parcial en §14 |
| Ocasional, corrección, repetición, personas distintas, carga por ítems | Elegibilidad y procedencia correctas; no omisiones silenciosas | Integración aislada | No ejecutado integralmente; cobertura parcial en §14 |
| Solicitud de arrastre/posposición parcial pendiente | Resolver o cerrar por incidencia explícitamente; restantes conservados | Formularios e integración aislada | No ejecutado integralmente; cobertura parcial en §14 |
| Entregada, revisión pendiente o cierre antes/durante la operación | No retirar ni mover; conflicto concurrente si cambió | Integración concurrente aislada | No ejecutado integralmente; cobertura parcial en §14 |
| Etapa activa exclusiva, reserva futura y sucesoras | Proceso pendiente de reprogramación; no cancelado ni satisfecho; reservas coherentes | Integración del proceso y temporizador aislados | No ejecutado integralmente; cobertura parcial en §14 |
| Reprogramar después una etapa retirada por ausencia | Conserva raíz histórica; nuevo compromiso no hereda indebidamente exclusión | Integración proceso + bono aislada | No ejecutado integralmente; cobertura parcial en §14 |
| Obligación de varios días / día original distinto del operativo | Conservar obligación y bono, redistribuir esfuerzo restante sin ampliar plazo; explicar falta de capacidad | Integración aislada | No ejecutado integralmente; cobertura parcial en §14 |
| Retiro con descuento previo; transferencia con antecedentes de devolución | Ausente sin base/descuento de obligaciones afectadas; receptora sin duplicación ni deuda ajena | `TaskBonus*Checks` ampliados, aislado | No ejecutado integralmente; cobertura parcial en §14 |
| Un día excluido y otros días evaluables; semana entera sin base | Mantener evaluación de otros días y estado sin tareas correcto | Reglas + integración bono aislada | No ejecutado integralmente; cobertura parcial en §14 |
| Corte semanal y cierre persistido concurrente | No reabrir ni reescribir resultado cerrado | Integración con reloj controlado | No ejecutado integralmente; cobertura parcial en §14 |
| Dos incidencias simultáneas, doble envío, respuesta perdida | Recalcular cuando proceda; replay único; recibo verificable | Concurrencia + HTTP aislado | No ejecutado integralmente; cobertura parcial en §14 |
| Fallo después de disponibilidad, transferencia o exclusión | Rollback de todas las entidades y hechos de bono | Inyección de fallo, base aislada | No ejecutado integralmente; cobertura parcial en §14 |
| Dirección, otros supervisores, persona fuera de alcance, payload manipulado | Solo Dirección confirma; consulta limitada; servidor rechaza abuso | Formularios HTTP reales aislados | No ejecutado integralmente; cobertura parcial en §14 |
| Búsqueda interrumpida vs imposibilidad demostrada | Mensajes distintos, entidad identificada, sin propuesta parcial falsa | Reglas y presentación aisladas | No ejecutado integralmente; cobertura parcial en §14 |
| Mi trabajo, supervisión, carga, tablero, detalle, historial y reportes | Retiradas fuera del trabajo activo; historial accesible; carga/bono coherentes | Consultas y navegador aislado | No ejecutado integralmente; cobertura parcial en §14 |
| 320 px, móvil y escritorio, temas, teclado, foco y texto largo | Lista/casos legibles; foco en errores; estados vacío/carga/error/éxito correctos | Pantalla real autenticada Chromium/WebKit | No ejecutado integralmente; cobertura parcial en §14 |
| Migración y regresiones de actualización, transferencias y capacidad | Datos previos preservados; sin romper recorridos existentes | Build + migración/integración aislada | No ejecutado integralmente; cobertura parcial en §14 |

Herramientas nuevas existentes: `IncidenceChecks`, `IncidenceRiskChecks`, `IncidenceProcessChecks`, `IncidenceEconomicChecks`, `IncidenceHttpChecks`, `IncidenceMigrationChecks` y `tools/Sgol.MobileChecks/incidence-checks.cjs`. Las suites amplias de actualización, transferencias y distribución siguen siendo candidatas a cobertura adicional, no pruebas ejecutadas por nombrarlas aquí.

## 13. Criterios de aceptación y cierre de esta entrega

El flujo futuro queda aceptable cuando ninguna tarea del alcance quede sin destino explícito, las vencidas no se transfieran, el reparto sea explicable, las excepciones sean particulares y revisadas, el bono refleje exclusión sin duplicación, y todo se confirme atómicamente con historial y reintento seguro. La ausencia no debe cambiar catálogo, empleo, otros días ni cierres ya emitidos.

La entrega pasó de planificación a implementación autorizada. La sección siguiente registra resultados reales y sus límites. La corrección de incidencias erróneas y las ausencias parciales siguen fuera de esta versión. La reprogramación posterior de un proceso es una operación futura: esta versión deja la etapa explícitamente pendiente, bloquea la reactivación implícita y separa las raíces económicas de compromisos posteriores; no ofrece todavía un formulario para reprogramar esa cadena.

## 14. Implementación y resultados reales del 04/11/2026

### Código implementado antes del despliegue de la sección 15

- `OperationalIncidenceService` y `OperationalIncidences`: consulta por alcance, clasificación por fecha, reparto por ocupación restante, búsqueda acotada con retroceso, decisiones explícitas y confirmación serializable bajo el bloqueo operativo común. Recibo único por actor/clave y persona/día; auditoría y disposiciones antes/después. La huella incluye agenda, reservas, disponibilidad, capacidad, hechos del bono, cierres y decisiones anteriores.
- La capacidad restante considera turno restante y esfuerzo completado. La sobrecarga es particular por tarea/destinataria, muestra exceso y empalmes, no cambia capacidades globales y conserva restricciones temporales, especialidad, ausencias y reservas exclusivas. El reacomodo opcional solo incluye vecinas futuras sin actividad protegida.
- `TaskBonusService`: exclusiones ligadas a raíces, transferencia sin descuentos previos ajenos, conservación de entregas posteriores y separación de futuros compromisos de etapas retiradas. `TaskBonusCommitInterceptor` verifica también los límites temporales de la incidencia en el callback de commit.
- Procesos: estado `PENDIENTE_REPROGRAMACION`, liberación de reservas dependientes, bloqueo del temporizador y retención del proceso abierto. Inicios preparados retenidos mediante `IncidenceHoldId`; activarlos no recrea sus reservas. Las pantallas de procesos muestran ese estado y no ofrecen activación del plan retenido.
- `EfAvailabilityAdministrationService` comparte el bloqueo y rechaza restaurar únicamente disponibilidad de una incidencia, porque no desharía tareas y bono.
- Razor `/supervision/incidencias`: consulta paginada, búsqueda, disponibilidad, pendientes/vencidas/minutos, motivo, decisiones, vista previa, confirmación y recibo. Reutiliza navegación, controles, paneles y `assignment-refresh.css`. Los archivos pequeños `incidences.css/js` ajustan la composición de esta página, invalidación de la revisión y estado de envío.
- Migraciones `20261104171654_AddOperationalIncidences` y `20261104173103_AddIncidencePreparedHold`: tablas/índices, estado de etapa, retención preparada y protección de inserciones/cambios de destinataria o fecha incompatibles con ausencias confirmadas. No hay backfill de exclusiones ni cambios de datos operativos previos. La reversión de la primera migración rechaza borrar historial existente.

### Entornos y evidencia

PostgreSQL de pruebas en `localhost:5440`, bases `sgol_task_checks_incidence_20261104`, `sgol_task_checks_incidence_clean_20261104` y `sgol_task_checks_incidence_upgrade_20261104`. La última se creó vacía, migró al esquema anterior, recibió fixtures y luego ambas migraciones nuevas. El host Razor temporal usa `https://localhost:7297`; no es el host compartido. Se consultó previamente el recorrido de infraestructura de la bóveda y se verificaron procesos/argumentos antes de detener exclusivamente hosts de pruebas.

Evidencias privadas: `.local/incidence-service-final.log` (52 comprobaciones de reglas/integración), `.local/incidence-final.log` (incluye 10 comprobaciones HTTP), `.local/incidence-clean.log`, `.local/incidence-migration.log`, `.local/incidence-ui/results.json` (20 escenarios) y capturas en ese directorio. Compilación final: 12 proyectos, cero errores y advertencias; sintaxis JavaScript y `git diff --check` aprobados; enlaces locales del plan sin destinos faltantes. No se versionan credenciales, bases ni capturas. Las sesiones de navegador son de prueba; los navegadores omiten comprobación TLS del certificado local. No se certifican login, MFA, TLS ni acceso LAN por estas pruebas. Al terminar se detuvo el host de pruebas; el host compartido observado continúa en `127.0.0.1:7241` con el artefacto `calendar-preservation-20261104`, sin intervención sobre él.

| Recorrido ejecutado | Entorno / prueba | Estado y resultado |
|---|---|---|
| Vista previa sin escritura; vencida de hoy y deuda anterior; transferencia no vencida | PostgreSQL, `IncidenceChecks` | **Aprobado**: retiro solo del día, historia conservada, deuda anterior intacta y esfuerzo conservado |
| Disponibilidad, recibo de transferencia, retiro, exclusión y auditoría | Servicio real + lectura posterior | **Aprobado**: guardado conjunto y ausencia de duplicación de base en la ausente |
| Replay y misma clave con otra intención | Servicio + formulario real | **Aprobado**: mismo recibo; intención distinta rechazada |
| Hora exacta de vencimiento y cambio entre vista previa/confirmación | Reloj controlado | **Aprobado**: se exige revisar, sin ausencia parcial |
| Vencimiento dentro del callback de commit | Interceptor de prueba + transacción real | **Aprobado**: rollback y asignación original conservada |
| Fallo diferido al commit, después de las escrituras | Trigger de fallo exclusivamente aislado | **Aprobado**: revierte recibo, disponibilidad, retiro y exclusión |
| Dos envíos simultáneos | Dos contextos/conexiones y reintento tras serialización | **Aprobado**: un solo recibo persistido |
| Sobrecarga, evidencia adjunta y ausencia de receptora | `IncidenceRiskChecks` | **Aprobado**: autorización requerida, exceso visible, bytes intactos, capacidad global intacta y ausencia no dispensada |
| Tiempo restante y sobrecarga dentro de turno, incluidos varios días | Servicio y cálculos con reloj controlado | **Aprobado**: respeta fin de turno/duración/plazo; esfuerzo completado consume presupuesto sin duplicar pendientes; sustitución administrativa no simula esfuerzo entregado |
| Obligación de varios días que cabe después de la ausencia | Servicio + comparación JSON antes/después | **Aprobado**: misma obligación, plazo y bono; ningún retiro económico |
| Etapa activa y sucesora; reserva en plan preparado | Catálogo, preparación, activación, incidencia y timer reales | **Aprobado**: proceso abierto, etapas retenidas, reservas liberadas y ninguna recreación automática |
| Devolución anterior y entrega posterior de receptora | Materialización de hechos del bono | **Aprobado**: sin traslado de devolución anterior ni pérdida de la entrega posterior; una obligación |
| Compromiso posterior de la misma etapa retirada | Materialización de snapshots y resolución de raíz | **Aprobado** como regla económica; **no ejecutado** como flujo real de reprogramación, que no tiene formulario en esta versión |
| Ausencia futura sin trabajo y productor ocasional posterior | Confirmación y `PreviewOccasionAsync` reales | **Aprobado**: conserva ausencia sin inventar tareas y rechaza nueva asignación a la ausente |
| Inserción incompatible con ausencia | Persistencia PostgreSQL | **Aprobado**: guardia de base rechaza la asignación |
| Razor, binding, CSRF, confirmación, redirect y recibo | HTTP real con sesiones sintéticas | **Aprobado** |
| Trabajadora y Subcoordinación | HTTP real: GET y POST manipulado | **Aprobado**: trabajadora sin acceso; supervisora consulta sin botón, no puede confirmar por POST ni abrir recibos fuera de alcance adivinando su ID |
| 320/768/1440 px × claro/oscuro/especial × Chromium/WebKit | Pantalla real y estilos reales; capturas, foco, Enter, texto largo | **Aprobado**: 18 combinaciones; se suman confirmación/recibo en WebKit y resolución de conflicto con evidencia/destinataria/sobrecarga/revisión/confirmación en Chromium. No dispositivos físicos ni lector de pantalla |
| Migración desde vacío y desde esquema anterior con datos | `--incidences` / `--incidence-migration` | **Aprobado**: modelo coincide, datos anteriores idénticos, segunda ejecución sin cambios y downgrade rechazado antes de perder recibos o retenciones |
| Reglas existentes de bono, capacidad y secuencias | `--bonus-rules`, `--capacity-rules-only`, `--sequence-rules-only` | **Aprobado**: 34 reglas de bono, 7 de capacidad y suite de calendario/dependencias/secuencias |

### Fallos encontrados y corregidos

- La primera versión del trigger consultaba `CompletedAt` en `operational_assignment`; ese dato está en la tabla de entrega. Falló en la base aislada y se corrigió; el arranque limpio posterior pasó.
- El rollback después de un error diferido al commit ocultaba la excepción original cuando PostgreSQL ya había terminado la transacción. Corregido y comprobado con inyección de fallo.
- La materialización posterior a una transferencia con devolución previa podía perder la entrega nueva al recortar eventos. Se conservan los hechos completos antes del recorte y la regresión pasó.
- Se corrigieron el binding del fingerprint opcional, la conservación de decisiones entre revisiones, raíz económica de posteriores compromisos, capacidad completada omitida y validación conjunta de dependencias.
- Fallos de fixtures: namespace/importación, revisión inicial del catálogo, tipo de adjunto y asignación de propiedad init-only. Corregidos; no se eliminaron validaciones del producto para pasar pruebas.
- Fallo de sincronización del navegador al comprobar el recibo antes de acabar la navegación. Se añadió espera del resultado real y el recorrido pasó. Se revisaron visualmente cabecera móvil y composición de formulario de escritorio, y se ajustaron.

### Cobertura adicional pendiente y documentación vigente

No ejecutados en su combinación completa: generación/copia de cada tipo de plan, todas las modalidades de corrección y conteo por ítems, todas las especialidades/empleos futuros, solicitud de arrastre parcial concurrente, reacomodo complejo con varias anclas, cierres semanales concurrentes, entrega concurrente y todos los consumidores de reportes/tablero. La conservación del cierre se protege en el servicio y por el protocolo existente; las reglas económicas y el rollback sí se probaron, sin afirmar que eso sustituye cada recorrido pendiente.

Durante la implementación no se desplegó ni se operó la simulación compartida, y la bóveda no describía la función como vigente. Tras la autorización posterior de commit, push y despliegue, se completó la reconciliación documental descrita a continuación.

## 15. Commit, push y despliegue autorizado

El usuario autorizó después hacer commit/push y desplegar la simulación compartida. Se confirmó `e9273b1` y se subió a `origin/master` antes de publicar. Compilación Release y publicación en `.local/runtime/incidencias-20261104/` aprobadas. Se conservó el artefacto anterior `calendar-preservation-20261104`.

Se siguió el recorrido de infraestructura de la bóveda y se verificaron procesos/argumentos. Se detuvieron exclusivamente Web 16340 y su lanzador 5028; el proxy 20652 y su lanzador 23392 permanecieron activos. Antes de migrar, con Web detenida, se guardaron respaldo PostgreSQL custom y huellas por tabla en `.local/deploy-incidences-20261104/`. El respaldo privado mide 284,627,061 bytes.

Destino comprobado: `sgol_local` en `127.0.0.1:5440`. El rol `sgol_migrator` aplicó `20261104171654_AddOperationalIncidences` y `20261104173103_AddIncidencePreparedHold`. Las huellas de las 95 tablas anteriores, incluidas **791 asignaciones**, coincidieron exactamente antes/después, antes de iniciar Web. La comparación excluye el campo nuevo nulo `IncidenceHoldId`, las tablas nuevas y el historial de migraciones. Incidencias y disposiciones quedaron con **cero filas**. Se comprobaron permisos de runtime sobre ambas tablas.

Nueva Web observada: proceso 28840, lanzador 19696 registrado en `.local/processes.json`, artefacto `.local/runtime/incidencias-20261104/Sgol.Web.dll`, puerto 7241. Live y readiness internos respondieron 200; `/acceso` local y LAN respondieron 200 con TLS normal. `/supervision/incidencias` sin sesión redirige a Acceso: no se presenta esto como prueba autenticada de la pantalla. No se modificaron ausencias ni se confirmó una propuesta operativa durante el despliegue.

Se reconciliaron las notas existentes de supervisión, disponibilidad, bono, procesos, planificación, historial y pantallas; la instalación local y la base documental identifican la publicación vigente. La copia versionada se sincronizó tras comprobar divergencias, conservando las mejoras anteriores de la bóveda. Graphify actualizado: 42 notas, 288 nodos y 550 relaciones. Enlaces locales comprobados en referencias afectadas, Inicio, mapas e inventario: cero destinos rotos. No se crearon notas de planes ni informes en la bóveda.

Evidencia privada: `before.json`, `before.dump`, `before-fingerprint.json`, `after-fingerprint.json`, `migration.log`, `health.json`, `web.log` y `web.error.log` en `.local/deploy-incidences-20261104/`. Las pruebas ampliadas y el formulario de reprogramación posterior señalados antes siguen pendientes; publicar no amplía su cobertura.

## 16. Ajuste de composición y densidad (04/11/2026)

Cambio de interfaz: registro antes de consulta; fecha editable y visible en el registro; búsqueda compacta debajo, con nombres de parámetros y paginación conservados. Vista previa ordenada por grupos: conflictos que requieren decisión, reasignaciones/otros cambios y retiros al final. Se sustituyen paneles anidados por filas compactas, con historial desplegable, tokens existentes y foco accesible. No cambian servicios, efectos económicos, permisos ni esquema.

Fuentes: `Pages/Validations/Incidents.cshtml`, `wwwroot/css/incidences.css`; regresiones en `IncidenceHttpChecks.cs` e `incidence-checks.cjs`. El árbol estaba limpio al comenzar.

| Escenario | Resultado esperado | Entorno y prueba | Estado |
|---|---|---|---|
| Compilación | Razor y herramientas sin errores | Build Debug, 12 proyectos | Aprobado, 0 errores/avisos |
| Formulario e invariantes | Vista previa, confirmación, reintento, permisos e integridad conservados | Base `sgol_task_checks_incidence_layout_20261104`, 52 comprobaciones de servicio y 10 HTTP | Aprobado |
| Prioridad | Conflictos antes de retiros; transferencias antes de retiros | Fixture mixto y aserciones DOM reales | Aprobado |
| Tamaños y temas | Sin desbordamiento; foco y lectura utilizables | Chromium/Edge y WebKit, 320/768/1440, light/dark/special; 18 combinaciones + 2 recorridos | Aprobado, 20 escenarios |
| Consulta y teclado | Vacío correcto, fecha futura conservada, acceso de consulta para supervisores | Formulario real y teclado en Chromium | Aprobado |
| Inspección visual | Espaciado compacto y jerarquía legible | Capturas 320 claro/conflicto y 1440 oscuro | Aprobado |
| Dispositivo físico/lector de pantalla | Comprobar interacción asistida | No disponibles en esta ejecución | No ejecutado |

Evidencia privada: `.local/incidence-layout-host.log`, `.local/incidence-layout-browser.log`, `.local/incidence-layout-extra.log` y `.local/incidence-ui/`. Sesiones sintéticas en host aislado; no acreditan autenticación compartida. Primer intento de navegador falló por falta de NODE_PATH; corregido con runtime existente. La primera aserción auxiliar de teclado supuso un único Tab en fecha: falló por sus segmentos nativos; corregida para recorrerlos, sin cambios en producto. No quedan fallos de estas pruebas pendientes. Publicación y reconciliación de bóveda se registran después de verificar el host.

Publicación completada: commit `b11d617` subido antes de sustituir únicamente Web. Release en `.local/runtime/incidencias-layout-20261104/Sgol.Web.dll`, proceso 37192/lanzador 2356; proxy 20652 conservado. Live/ready internos y acceso LAN devuelven 200 con TLS normal. Sin migraciones ni escrituras operativas compartidas; artefacto anterior conservado. Reconciliadas las notas existentes de pantallas, instalación y base documental tras verificar el host; copia versionada comparada antes de actualizar. La autenticación del host compartido no se ensayó.

## 17. Destinatarias incompatibles y diagnóstico reproducido (04/11/2026)

Se reprodujeron las decisiones solicitadas en una copia aislada de la simulación (`sgol_task_checks_incidence_diagnosis_20261104`), mediante el servicio real y sin ejecutar Apply. La reproducción fijada a las 12:24 identificó incompatibilidad Subcoordinación/Piso de ventas para dos destinatarias solicitadas y falta de capacidad para otras tres transferencias: 250 min pendientes frente a 235 min de presupuesto restante (240 menos 5 consumidos). El retiro explícito no bloqueaba. La cobertura de cierre estaba correctamente seleccionada; no era la causa. La reproducción posterior a las 12:30 rechazó además la transferencia ya vencida. Activar reacomodo o probar sobrecarga hipotética no elimina una incompatibilidad de rol. Detalles particulares, IDs y copia permanecen privados en `.local/incidence-diagnosis-20261104/`; no se trasladan a la bóveda.

Defecto confirmado: la vista ofrecía a todas las personas del alcance, mientras la búsqueda del servidor exigía elegibilidad. El fallo conjunto repetía una causa genérica para todos los trabajos.

Corrección mixta: cada caso devuelve IDs de destinatarias que cumplen rol/especialidad, disponibilidad y un intervalo de horario suficiente sin considerar la ocupación dispensable. La búsqueda y el selector comparten ese filtro. Se conservan elegibles sin capacidad para permitir sobrecarga explícita; huecos, dependencias y el reparto completo se verifican al revisar. Se explica por persona el rol incompatible, la ausencia o el presupuesto insuficiente. La selección anterior incompatible se descarta con aviso y exige nueva revisión; no se confirma silenciosamente. No se amplían roles, no se autorizan excepciones automáticamente, no cambia el esquema. El algoritmo de huella pasa a versión 2 para invalidar propuestas anteriores.

| Escenario | Esperado y prueba | Entorno | Estado |
|---|---|---|---|
| Reproducción original | El servicio identifica incompatibilidad de rol y falta de capacidad, manteniendo asignaciones | Copia aislada, reloj 12:24 y hora posterior real | Aprobado; fallo operativo reproducido |
| Alternativas hipotéticas | Sobrecarga/reacomodo no dispensan rol; ninguna escritura | Misma copia, solo Preview | Aprobado |
| Destinatarias inválidas | Ausente, rol incompatible y turno insuficiente excluidos; envío manipulado rechazado sin ausencia parcial | Base sintética nueva `sgol_task_checks_incidence_recipients_final_20261104` | Aprobado |
| Capacidad insuficiente | Destinataria elegible permanece disponible con causa numérica; sobrecarga explícita sigue funcionando | Servicio, HTTP y navegador reales aislados | Aprobado |
| Formulario anterior | Selección incompatible recibe causa precisa, desaparece de opciones, muestra aviso y no puede confirmar | POST real con CSRF | Aprobado |
| Regresión completa de Incidencias | Atomicidad, permisos, reintentos, procesos, bono y vencimiento conservados; 70 comprobaciones de servicio/HTTP | Base aislada | Aprobado |
| Interfaz | Selector filtrado y flujo completo; Chromium/WebKit, 320/768/1440, light/dark/special, teclado; 20 escenarios | Razor real con sesión sintética; captura de conflicto móvil inspeccionada | Aprobado |
| Compilación | Debug herramientas sin errores ni avisos | Checkout | Aprobado |
| Operación compartida | Registrar ausencia o transferir las seis tareas | No se ejecuta en esta corrección | No ejecutado, fuera de la reproducción autorizada |

Evidencias privadas: `.local/incidence-recipients-final-host.log`, `.local/incidence-recipients-final-browser.log`, `.local/incidence-ui/`, `.local/incidence-diagnosis-20261104/`. Sin fallos de validación pendientes en esta matriz; pruebas físicas y autenticación compartida no repetidas. La publicación y la reconciliación documental se completan después de verificar el artefacto.

Publicación del ajuste completada: `3e3bb74` subido antes de reemplazar Web por `.local/runtime/incidencias-recipients-20261104/Sgol.Web.dll` (proceso 14692/lanzador 40992), con proxy 20652 conservado. Release y salud live/ready/acceso local/LAN aprobados (200, TLS normal). Sin migraciones, sin registrar ausencia ni confirmar asignaciones compartidas. Reconciliadas referencias de Supervisión, pantallas, instalación y base documental y su copia versionada, tras comprobar el artefacto.

## 18. Opciones dependientes de Transferir (04/11/2026)

Cambio mixto de formulario: destinataria, ayuda y sobrecarga se agrupan en Opciones de transferencia y solo aparecen al seleccionar TRANSFER. Al retirar o quitar la decisión, se ocultan, deshabilitan y limpian; volver a transferir empieza sin destinataria explícita ni sobrecarga. El fieldset deshabilitado evita el envío y el recorrido de teclado de esos campos. El servidor normaliza las decisiones que no transfieren, descartando destinataria/sobrecarga antes de construir la petición y su huella. Los cambios ya revisados mantienen sus campos ocultos para la confirmación atómica.

| Escenario | Entorno/prueba | Estado |
|---|---|---|
| Sin decisión y Retirar ocultan campos; Transferir los muestra | Razor real aislado, Playwright | Aprobado |
| Transferir → elegir persona/sobrecarga → Retirar → Transferir | Limpieza, FormData sin campos deshabilitados y teclado fuera del fieldset | Aprobado |
| POST de retiro con destinataria/sobrecarga manipuladas | Formulario HTTP real con CSRF; propuesta canónica sin destinataria/sobrecarga | Aprobado |
| Regresión de servicio/formularios | Base nueva `sgol_task_checks_incidence_options_20261104`, 71 comprobaciones | Aprobado |
| Navegador | 20 escenarios, Chromium/WebKit, 320/768/1440, tres temas; conflicto móvil inspeccionado | Aprobado |
| Build Debug | Herramientas y Web, sin errores/avisos | Aprobado |
| Dispositivos físicos y sesión compartida autenticada | No repetidos; sesiones sintéticas solo en base aislada | No ejecutado |

Evidencia privada: `.local/incidence-options-host.log`, `.local/incidence-options-browser.log`, `.local/incidence-ui/`. Sin fallos pendientes. No se registró ausencia ni se aplicaron reasignaciones compartidas. No hay migraciones. La referencia de la bóveda se reconciliará con la publicación efectiva.

Publicado `cf93b42` desde `.local/runtime/incidencias-options-20261104/Sgol.Web.dll`, proceso 8972/lanzador 36724; proxy conservado. Release, live/ready internos y acceso LAN aprobados (200, TLS normal). Referencias funcionales, de pantalla e infraestructura reconciliadas después de verificar el proceso y sincronizadas con la copia versionada. Sin migraciones ni operaciones de ausencia/reparto compartidas.

## 19. Sobrecarga automática y búsqueda conjunta (04/11/2026)

Se reprodujo la combinación reportada: Reparto automático + Autorizar sobrecarga por tarea + Reacomodo. La validación agrupaba una destinataria automática (null) con tareas ajenas y devolvía un mensaje falso. Después de admitir esa combinación, la copia reprodujo el segundo fallo: 20,000 pasos agotados al intentar recolocar agendas aunque existía una solución conservándolas.

Correcciones:
- Sobrecarga autorizada por tarea admite reparto automático; la propuesta identifica destinataria concreta, exceso y empalmes antes de confirmar. No se amplían roles ni se dispensan ausencias, especialidades, turno o dependencias.
- Reacomodo es permiso para mover cuando hace falta: primero se intenta una solución completa conservando horarios vecinos. Si no existe, se buscan movimientos en las agendas de las destinatarias posibles.
- La búsqueda comprueba que cada trabajo pendiente tenga al menos una alternativa antes de explorar combinaciones. Conserva el límite de 20,000 pasos por búsqueda y no convierte su agotamiento en prueba de inviabilidad.
- Las dependencias de planes preparados ajenos al reparto no se usan como motivo para rechazar una incidencia que no los modifica. Los planes afectados siguen comprobándose.
- Se separan los errores de tarea fuera de persona/fecha, acción inválida y sobrecarga incompatible. Una transferencia vencida ofrece ahora la opción efectiva Retirar por ausencia, en vez de un texto sin control utilizable.
- La huella usa algoritmo 3 y conserva la intención Reflow incluso cuando no necesita mover vecinas; una propuesta anterior debe revisarse de nuevo.

| Escenario | Entorno y evidencia | Estado |
|---|---|---|
| Fallo original con sobrecarga automática | Copia aislada `sgol_task_checks_incidence_diagnosis_20261104`, servicio real: mensaje de tarea ajena reproducido | Aprobado como reproducción del fallo |
| Límite de pasos | Misma copia y reloj 12:52, después de corregir solo validación: fallo de 20,000 pasos reproducido; `.local/incidence-diagnosis-20261104/search-limit-before.log` | Aprobado como reproducción del fallo |
| Confirmación corregida e idempotencia | Copia aislada, reloj 12:55, propuesta de 14 disposiciones confirmada y reenviada sin duplicación; `probe.log` | Aprobado |
| Hoy y fecha futura | Reparto automático + sobrecarga + reacomodo: destinataria concreta, confirmación, replay y vecinas sin cambios | Aprobado |
| Restricción posterior imposible | Dos tareas con muchos horarios y una destinataria incompatible: poda sin agotar pasos, sin confirmación parcial | Aprobado |
| Reacomodo necesario | Tarea crítica con hueco ocupado; falla sin reacomodo, mueve una vecina pertinente y confirma con reacomodo | Aprobado |
| Vencimiento al revisar | La transferencia vencida ofrece Retirar por ausencia | Aprobado |
| Regresión de servicios/HTTP | Base nueva `sgol_task_checks_incidence_search_final_20261104`, 85 comprobaciones | Aprobado |
| Formularios y navegador | Flujo real con automático/sobrecarga/reacomodo, revisión y confirmación; 20 escenarios Chromium/WebKit y tamaños/temas | Aprobado |
| Compilación | Debug Web/herramientas sin errores ni avisos | Aprobado |
| Primer fixture de reacomodo | Falló al modificar criticidad sin incrementar revisión; corregido el fixture respetando la revisión, sin debilitar validaciones | Fallido inicialmente, aprobado en ejecución final |
| Simulación compartida | No registrar ausencia ni aplicar reparto operativo | No ejecutado; la confirmación del usuario queda pendiente |

Evidencias privadas: `.local/incidence-search-final-host.log`, `.local/incidence-search-final-browser.log`, `.local/incidence-diagnosis-20261104/` y `.local/incidence-ui/`. No hay migraciones. El resultado de la copia no reemplaza la revisión del estado actual compartido: tareas que vencieron después se clasifican de nuevo. Las pruebas usan sesiones sintéticas aisladas; no se repitió autenticación compartida ni dispositivo físico.

Publicado `b27752c` desde `.local/runtime/incidencias-search-20261104/Sgol.Web.dll`, proceso 11944/lanzador 21772. Proxy 20652 conservado. Release, live/ready internos y acceso LAN aprobados (200, TLS normal). Referencias reconciliadas tras comprobar el proceso, sin migraciones ni confirmaciones operativas compartidas.

## 20. Precisión temporal en la confirmación del formulario (04/11/2026)

La captura reportó un rechazo por cambio de propuesta al confirmar. Se identificó y reprodujo una causa del formulario real: `NotBefore` se calculaba con fracciones de segundo, pero el campo oculto Razor lo renderizaba con formato localizado sin fracciones. El servidor recibía otra intención y rechazaba correctamente su huella, aunque la usuaria no hubiese editado nada. Las comprobaciones anteriores usaban relojes con segundos enteros y no detectaban este defecto.

Corrección de interfaz/contrato: `Incidents.cshtml` serializa el campo oculto con formato round-trip `O`, conservando zona y siete decimales. No se debilitan la huella, concurrencia, límites temporales, permisos ni atomicidad; no hay cambios de esquema. Las propuestas ya abiertas deben revisarse otra vez.

| Escenario y resultado esperado | Prueba / entorno | Estado |
|---|---|---|
| Reproducción con reloj fraccionario: conservar instante exacto | Formulario Razor real, base `sgol_task_checks_incidence_precision_before_20261104`; envió `04/11/2026 04:02:00 p. m. +00:00` perdiendo `.1234567`; log privado `incidence-precision-before.log` | Fallido antes de corregir; defecto reproducido |
| Revisión de hoy con transferencia, retiro y reacomodo; confirmar 15 segundos después sin cambio real | Formulario HTTP real y base nueva `sgol_task_checks_incidence_precision_final_20261104`; comparación exacta del campo, confirmación y reintento | Aprobado |
| Permisos, vencimiento real, concurrencia, atomicidad, bono, procesos y reintentos | 89 comprobaciones de incidencias/HTTP, `.local/incidence-precision-final.log` | Aprobado |
| Flujo real y presentación, ahora con reloj fraccionario | 20 escenarios Chromium/WebKit, 320/768/1440, claro/oscuro/especial, foco y teclado; `.local/incidence-precision-browser.log` | Aprobado |
| Compilación Debug y publicación Release | Debug sin errores ni avisos; `.local/incidence-precision-publish.log` | Aprobado |
| Confirmación de ausencia en simulación compartida | Reservada a la usuaria; no se realizó escritura operativa | No ejecutado |
| Dispositivo físico y autenticación compartida | Navegadores automatizados con sesión sintética en base aislada | No ejecutado |

La regresión mantenible se añadió a `IncidenceHttpChecks.cs`. Evidencias privadas en `.local/`; sin migraciones. Referencias de bóveda pendientes de reconciliar al verificar la publicación efectiva.
