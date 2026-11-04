# Plan: incidencias de ausencia y redistribución en Supervisión

Fecha: 2026-11-04. Zona operativa: `America/Mexico_City`.
Estado: **desplegado en la simulación compartida por autorización posterior del usuario; validación principal ejecutada y cobertura ampliada pendiente según las secciones 12 y 14**. Cambio mixto: reglas, persistencia e interfaz.
Auditoría posterior del 04/11/2026: **correcciones locales sin desplegar**, resultados y límites en la sección 22. La autorización de esta auditoría excluye despliegue, commit, push y escrituras operativas compartidas; las publicaciones descritas en secciones anteriores son antecedentes.
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

Publicado commit 6519aa2 desde .local/runtime/incidencias-precision-20261104/Sgol.Web.dll, Web 9200/lanzador 27752. Proxy 20652 conservado. Live/ready internos y acceso LAN: 200, TLS normal. Bóveda y copia versionada reconciliadas tras verificar publicación; sin migraciones ni confirmación de ausencia compartida.

## 21. Orden determinista y diagnóstico de confirmación (04/11/2026)

El nuevo rechazo compartido ocurrió unos 18 segundos después de la revisión. El registro anterior no guardaba las huellas por componente ni distinguía la causa; no permite atribuir retrospectivamente ese intento a un vencimiento o a una diferencia de estado. No se afirma que la pérdida de precisión de la sección anterior explicara todos los rechazos.

Investigación y corrección:
- La copia del estado actual confirmó con el servicio tras 15 segundos de reloj simulado y con el formulario/navegador y reloj real, tanto usando Debug como el ensamblado Release efectivamente publicado. Eso no prueba que el envío compartido tuviera iguales datos o estado concurrente.
- Se reprodujo otro defecto: hechos de bono con fecha/revisión iguales podían salir en distinto orden físico y cambiar la huella sin modificar su contenido. La regresión reordena físicamente filas únicamente en una base aislada y falla antes de la corrección.
- La huella usa orden total por identificador en los empates de hechos, transferencias y etapas; orden por versión/posición para pasos preparados y por identificador para disposiciones. Algoritmo 4; requiere nueva revisión de propuestas anteriores.
- El rechazo diferencia conflictos de la propuesta, tarea vencida, inicio propuesto ya pasado y huella distinta. Los dos límites temporales identifican la tarea; se conserva el control antes de guardar y al commit.
- Se registran clave de intención y hashes por componente (petición, asignaciones, procesos, bono, disponibilidad y evidencia), sin cuerpos de evidencia, motivos ni datos personales. Permiten localizar una divergencia posterior; no constituyen autorización para confirmar.
- Las decisiones ocultas sobreviven al rechazo del formulario. Volver a Revisar cambios conserva acción, destinataria y sobrecarga; sigue requiriendo nueva confirmación.

| Escenario | Entorno y evidencia | Estado |
|---|---|---|
| Servicio con reloj fraccionario y 15 segundos entre revisión y confirmación | Copia `sgol_task_checks_incidence_clock_20261104`, `.local/incidence-clock-20261104/probe.log`; confirmación y reintento | Aprobado |
| Formulario real con estado copiado y reloj real | Copias `sgol_task_checks_incidence_form_20261104` y `sgol_task_checks_incidence_release_20261104`; Debug y DLL Release previa, navegador Chromium, recibo | Aprobado |
| Orden físico de hechos sin cambios lógicos | Base `sgol_task_checks_incidence_order_before_20261104`, `.local/incidence-order-before.log` | Fallido antes del arreglo; defecto reproducido |
| Mismo escenario con orden determinista | Base nueva `sgol_task_checks_incidence_order_ui_20261104`, `.local/incidence-order-ui.log` | Aprobado |
| Regresiones, permisos, reloj, integridad, procesos, bono, formularios y decisiones conservadas | 92 comprobaciones funcionales/HTTP en base aislada | Aprobado |
| Navegadores, temas, tamaños, teclado, rechazo y posterior confirmación | 20 escenarios Chromium/WebKit; `.local/incidence-order-browser.log`; captura móvil de error inspeccionada | Aprobado |
| Compilación Debug y Release | Cero errores/avisos en compilación final; `.local/incidence-order-publish.log` | Aprobado |
| Compilación intermedia | Intento de ordenar pasos por Id inexistente; corregido usando su clave versión/posición | Fallido inicialmente; aprobado final |
| Causa exacta del envío compartido anterior | No había trazas suficientes; la reproducción demuestra un defecto adicional, no su ocurrencia en aquel envío | Pendiente de evidencia; sin atribución categórica |
| Confirmación operativa compartida, dispositivo físico y sesión compartida | No se registró ausencia ni se aplicó reparto compartido; pruebas con sesión sintética aislada | No ejecutado |

Sin migraciones ni alteración del historial compartido. Evidencias y copias privadas en `.local/`. Bóveda pendiente de reconciliación tras verificar despliegue.

Publicado a3fd69e desde .local/runtime/incidencias-concurrency-20261104/Sgol.Web.dll, Web 34452/lanzador 31712. Proxy 20652 conservado. Live/ready internos y acceso LAN: 200, TLS normal; error de arranque vacío. Referencias funcionales, de pantalla e infraestructura reconciliadas y copia versionada sincronizada. Sin migraciones ni confirmaciones compartidas.

## 22. Auditoría ampliada independiente, sin despliegue (04/11/2026)

### Alcance, entorno y procedencia

Cambio funcional con comprobación de integración de interfaz: servicio de incidencias y productor de revisiones de etapas. No se modificaron Razor, CSS ni JavaScript servido por el host compartido. Checkout inicialmente limpio; se conservaron los archivos existentes. No se efectuaron commit, push, migraciones compartidas, confirmaciones de ausencias compartidas ni cambios de asignación compartidos.

Se leyeron AGENTS, RTK, protocolo, este plan, Graphify verificado, Inicio, dominio, reglas, supervisión, bono, diseño consolidado y el recorrido de infraestructura → instalación local → base documental. La comprobación de procesos/listeners confirmó Web 34452 con `.local/runtime/incidencias-concurrency-20261104/Sgol.Web.dll`, lanzador 31712, proxy 20652 y PostgreSQL local 5440. La Web compartida permaneció con ese artefacto; las DLL de esta auditoría **no están activas allí**.

Se creó `.local/incidence-audit-20261104/shared.dump` mediante `pg_dump` de lectura. Restauración exclusivamente en `sgol_task_checks_incidence_audit_copy_20261104`, comprobada con `current_database()` y 791 asignaciones. Esta copia ya contenía una incidencia confirmada por una operación anterior; no se revirtió. Para reproducir el caso previo se restauró además `.local/incidence-diagnosis-20261104/snapshot.dump` en otra base, `sgol_task_checks_incidence_audit_replay_20261104`. No se restauró ninguna base compartida.

Los fixtures usan bases nuevas `sgol_task_checks_incidence_audit_*` en localhost:5440, protegidas por el guard existente de las herramientas. Los hosts Razor aislados usaron 7297 (fixtures, reloj controlado) y 7298 (copia, reloj real y temporizador cada 15 s), con sesiones sintéticas expresamente identificadas. No representan autenticación real del host compartido. Datos, HTML, capturas, cuerpos de formularios, respaldos y logs quedaron en `.local/`, comprobado como ignorado por Git.

### Productores y consumidores comprobados

| Dato | Productor / validación | Persistencia y consumidores |
|---|---|---|
| Persona, día, clave, motivo, NotBefore, decisiones y Reflow | `Pages/Validations/Incidents.cshtml(.cs)`, binding/CSRF, `OperationalIncidenceService.PreviewAsync/BuildAsync/ApplyAsync` | Huella, intención y recibo `OperationalIncidence`; consulta `HistoryAsync`, `SummariesAsync` y renderizado del recibo |
| Revisión de etapa, reserva y dependencia | `Sequences/OperationalProcessService.EnableActive/EnableScheduledStage`, `TickAsync`, timer de 15 s | `OperationalSequenceStage`, intentos, reservas del ledger; huella de Incidencias y bloqueo/activación del proceso |
| Responsable, fechas, esfuerzo y actividad | Búsqueda conjunta, `OperationalAgenda`, ledger y `ApplyAsync` | Misma asignación, `OperationalTransfer`, disposiciones antes/después; filtros activos, supervisión, cálculo de capacidad y bono |
| Ausencia | Sucesor de `AvailabilityDayVersion` en la transacción de confirmación | Disponibilidad/ledger, hechos de disponibilidad de bono; productor ocasional real y guard de persistencia de nuevas asignaciones |
| Exclusión y raíz económica | `ApplyAsync`, `TaskBonusPersistence`, interceptor de commit, `TaskBonusService.Materialize/IncidenceRoot` | Exclusiones, hechos, transferencias y cierres; consulta de bono sin duplicar obligación |
| Evidencia | Asignación, checklist con autor, fotografías y adjuntos | Conservación de IDs y contenido al transferir/retirar; componentes de huella. Las pruebas de archivo aquí no recorren una nueva subida a almacenamiento externo |

### Defectos y correcciones

1. **Revisiones fabricadas por el temporizador.** Dos `TickAsync` con una etapa programada sin cambios aumentaban su revisión. La huella de Incidencias incluía esa revisión; esperar podía invalidar una propuesta sin cambio lógico. Reproducido antes de corregir en `process-before.log`. `EnableActive` compara los valores persistidos antes/después y conserva la revisión si no cambió ninguno; si cambian, garantiza su incremento. No se eliminó la revisión de la huella ni se dispensó un cambio real. Regresión mantenible en `IncidenceProcessChecks.cs`; confirmación adicional en copia con temporizador activo y espera real de 35 s.
2. **Orden de decisiones confundido con intención.** Invertir dos decisiones equivalentes cambiaba la huella; reproducido en Release en `before.log`. El servicio normaliza por AssignmentId antes de revisar y confirmar, sin eliminar duplicados. Algoritmo de propuesta 5: una propuesta anterior requerirá nueva revisión cuando se active esta versión. Los recibos anteriores admiten replay comparando la intención normalizada del recibo persistido; no se reescriben. Acciones distintas con igual clave siguen rechazándose.
3. **Contrato Retirar diferente entre formulario y servicio.** Razor descartaba destinataria/sobrecarga, pero el servicio rechazaba sobrecarga en una retirada y podía incluir campos irrelevantes en su hash. Ahora el servicio descarta ambos para RETIRE. No cambia la semántica de TRANSFER ni se descartan acciones inválidas. Regresión de equivalencia, replay y manipulación en `IncidenceAuditChecks.cs`; formulario real de retiro ya cubierto por `IncidenceHttpChecks.cs` y vuelto a ejecutar.

Los cambios son locales, sin migraciones. La restauración automática de NuGet añadió incidentalmente Markdig al lockfile de herramientas; se revirtió exclusivamente esa modificación generada, ajena a la auditoría. No se cambió ninguna dependencia del producto.

### Trazas compartidas: qué permiten concluir

Se extrajeron únicamente eventos del servicio desde `.local/incidence-order-web.log` a `shared-traces.json` privado. Para una misma clave, revisión a las **13:22:48** y confirmación a las **13:23:00**, hora de México, conservaron los hashes de petición, asignaciones, bono, disponibilidad y evidencia, pero cambió el de **procesos**, seguido de `fingerprint mismatch`.

Esto localiza el componente de un rechazo posterior al despliegue de las trazas. El aumento de revisión sin cambio lógico está reproducido y es compatible con ese patrón y con el timer de 15 s. Las trazas no guardaban las filas de etapas antes/después: **no demuestran retrospectivamente qué etapa produjo aquel cambio ni excluyen un cambio real de proceso**. Tampoco permiten atribuir los rechazos anteriores sin trazas a esta misma causa.

### Matriz de escenarios y resultados de esta auditoría

`F` = fixtures aislados con servicios reales y PostgreSQL; `H` = Razor/HTTP reales aislados con CSRF; `B` = navegador sobre H; `C` = copia aislada de simulación. Estados se refieren a esta ejecución, no a promesas de la suite.

| Escenario / precondición | Acción y resultado esperado | Entorno y evidencia | Estado |
|---|---|---|---|
| Dos decisiones, una transferencia y un retiro | Invertir enumeración: misma intención y huella | F Release, `before.log`, `final2.log` | Fallido antes; corregido y aprobado |
| Etapa programada esperando su hora | Repetir timer: sin nueva revisión si no hay cambio; activación posterior sigue funcionando | F Release, `process-before.log`, `final2.log` | Fallido antes; corregido y aprobado |
| Retiro con campos irrelevantes | Destinataria/sobrecarga descartadas; misma huella y replay | F + H, `final2.log` | Aprobado |
| Recibo histórico con hash no canónico | Reintentar igual intención con otro orden: mismo recibo; acción distinta rechazada | F, `final2.log`, `state2.log` | Aprobado |
| Hoy/futuro × Reflow sí/no × destinataria automática/explícita × sobrecarga sí/no | Ocho combinaciones cubren cada par de niveles; revisar, confirmar y consultar; esfuerzo y responsable coinciden | F, `IncidenceAuditChecks`, `state2.log` | Aprobado |
| Vencimiento almacenado | Revisar a -1 tick, exactamente y +1 tick: transferible antes, retiro en/después del límite | F, `final2.log` | Aprobado |
| Hora fraccionaria y demora | Round-trip de NotBefore, confirmación tras 15/45 s controlados | F + H, `final2.log` | Aprobado |
| Estado copiado, reloj real y timer activo | Abrir → revisar → resolver → revisar → esperar 35 s → confirmar → recibo | C + Chromium + DLL Release, `replay-browser.log`, `replay-host.log` | Aprobado |
| Medianoche local, cambio semanal y mensual | Propuesta del día anterior rechazada sin ausencia; día nuevo aceptado | F, 08→09/11 y 30/11→01/12, sin tareas en este caso, `state2.log` | Aprobado, alcance limitado a fecha de incidencia |
| Fecha pasada o decisiones duplicadas manipuladas | Rechazo antes de guardar | F, `state2.log` | Aprobado |
| Sin tareas, una tarea, mezcla vencidas/vigentes y deudas anteriores | Ausencia futura vacía; transferencia/retiro del día; deuda histórica intacta | F + H + C, `final2.log` | Aprobado |
| Progreso, comentario, checklist con autor y archivo | Exigir decisión explícita; transferir o retirar conservando avance, carga, autoría y bytes | F, `IncidenceStateAuditChecks`, `state2.log` | Aprobado |
| Obligación de varios días y carga imposible | Conservar plazo/bono; no dispensar esfuerzo fuera del turno | F, regresión RiskChecks, `final2.log` | Aprobado |
| Capacidad cero, ausencia, rol incompatible, fin de turno | Sobrecarga solo dispensa capacidad/empalmes permitidos; sigue rechazando restricciones no dispensables | F + H + B, `final2.log`, `browser.log` | Aprobado |
| Reacomodo innecesario / necesario / reparto imposible | Conservar vecinas primero; mover cuando hace falta; rechazo sin reparto parcial | F, SearchChecks, `final2.log` | Aprobado |
| Orden físico de hechos de bono sin cambio lógico | Reordenar físicamente filas: misma huella | F, SearchChecks, `final2.log` | Aprobado |
| Etapa activa y proceso preparado | Retirar, conservar proceso, bloquear sucesoras/activación y no recrear con timer | F, ProcessChecks, `final2.log` | Aprobado |
| Cambio concurrente de rol, capacidad, disponibilidad, horario, adjunto o comentario de actividad | Confirmación obsoleta rechazada; responsable original y ausencia no parcial | F, StateAuditChecks, `state2.log` | Aprobado; mutaciones entre llamadas, no seis carreras simultáneas |
| Doble confirmación concurrente | Una confirmación; serialización rechazada puede reintentarse con recibo único | F con dos DbContexts, `final2.log` | Aprobado |
| Fallo al commit diferido y vencimiento dentro de callback de commit | Rollback de ausencia, historial y bono; conservar asignación original | F, RiskChecks, `final2.log` | Aprobado |
| Disponibilidad preexistente y fallo tras segundo SaveChanges | Snapshot de asignaciones/disponibilidad y conteos de hechos, exclusiones, recibos, transferencias/auditoría idénticos; retry mismo contexto confirma una vez | F, `atomic2.log`, `state2.log` | Aprobado |
| Bono tras retiro/transferencia y nueva obligación de etapa | Sin base/descuento de retiro ni duplicación; no heredar descuento/exclusión ajenos | F, consulta persistida y Materialize, `final2.log` | Aprobado; parte de los casos económicos son pruebas de materialización |
| Nueva asignación a ausente | Productor ocasional real y guard de persistencia rechazan | F, `final2.log` | Aprobado |
| Dirección, supervisora y colaboradora; recibo ajeno y CSRF | Solo Dirección confirma; consulta filtrada; recibo ajeno/POST sin CSRF rechazados | H, `final2.log` | Aprobado |
| Formulario con rechazo y decisiones previas | Conservar decisiones; volver a revisar; confirmar; repetir envío devuelve recibo | H + B, `final2.log`, `browser.log`, `webkit.log` | Aprobado |
| Transferir → Retirar → Transferir, automático/sobrecarga/reacomodo | Campos se limpian/deshabilitan y vuelven a requerir revisión | B Chromium, `browser.log` | Aprobado |
| 320/768/1440, light/dark/special, teclado, foco y texto largo | Sin desborde; controles accesibles por teclado; error/success/recibo reales | B, 10 escenarios por motor; capturas `.local/incidence-ui/` | Aprobado; captura de rechazo 320 inspeccionada |
| Autenticación real compartida, dispositivos físicos y lector de pantalla | No equivalen a la sesión sintética | No ejecutado | Pendiente de esa validación |
| Ausencia operativa compartida, despliegue, commit/push | Prohibidos en esta petición | Sin ejecución | No aplicable a esta entrega |

### Comandos, artefacto y fallos de preparación

- `rtk dotnet build tools/Sgol.TaskChecks/Sgol.TaskChecks.csproj -c Release --no-restore`: compilación final sin errores/avisos; logs `build-final2.log`, `build-state2.log`.
- `rtk proxy dotnet tools/Sgol.TaskChecks/bin/Release/net10.0/Sgol.TaskChecks.dll --incidences --ui`: **122 comprobaciones** aprobadas de servicio/HTTP en `final2.log`.
- `rtk proxy dotnet tools/Sgol.TaskChecks/bin/Release/net10.0/Sgol.TaskChecks.dll --incidence-audit`: **61 comprobaciones** aprobadas en `state2.log`; incluye 29 ya ejecutadas en la suite anterior y 32 adicionales. **154 comprobaciones distintas**, no 183 independientes.
- Conexiones mediante `SGOL_TASK_CHECKS_CONNECTION`, construidas desde el almacén protegido y con nombre aislado verificado; no se guardan contraseñas en este plan. Creación explícita mediante `docker exec sgol-local-postgres-1 createdb -U sgol_local -O sgol_migrator <nombre-aislado>`; restauración con `pg_restore --role=sgol_migrator --no-owner --no-privileges --exit-on-error -d <nombre-aislado>`.
- `node tools/Sgol.MobileChecks/incidence-checks.cjs`: Chromium completó sus 10 escenarios; el primer arranque de WebKit falló por usar una instalación de Playwright distinta a la de los navegadores disponibles. Se ejecutó después únicamente WebKit con el runtime 1.62.1 compatible, 10 escenarios aprobados en `webkit.log`; no se repitieron las confirmaciones ya consumidas de Chromium. `NODE_PATH` se obtiene de `dependencies/node/node_modules` del runtime de `Get-Command node`.
- Harness privado `.local/incidence-audit-20261104/Probe.csproj`, `Program.cs` y `copy-check.cjs`: Razor de la DLL Release, reloj real, timer real, guard de nombre de base y puerto 7298; `replay-browser.log` registra confirmación tras 35 s. No inicia el servicio compartido.
- SHA-256 de `Sgol.Web.dll` compilada, usada por TaskChecks y cargada por el harness de copia: `40CE74BF5F14C94C3121912E7FDE4E4B218688347CDB31A002AE6B8E20215DBE`; comparación en `assembly-hashes.json`. Es el candidato Release probado, no el binario compartido.
- El primer intento de suite no pudo crear base con el rol migrador, correctamente sin privilegio CREATEDB; se creó una base aislada explícita con el administrador local. Se corrigieron tres problemas de fixtures, conservando las validaciones: tomar el vencimiento persistido a precisión PostgreSQL; configurar el reloj del interceptor de bono en el modo independiente; crear sucesor de horario en lugar de modificar una revisión inmutable. Sus logs iniciales quedan como fallidos de preparación, no defectos del producto.
- `rtk proxy git diff --check` y comprobación de enlaces locales del plan antes del cierre. No se actualizó la bóveda porque estas correcciones no están desplegadas; reconciliación pendiente de una activación futura autorizada.

### Evaluación y cobertura todavía pendiente

Los dos rechazos artificiales reproducidos quedaron corregidos y cubiertos por regresiones. El servicio y el formulario confirmaron con el candidato Release, tanto en fixtures como con datos copiados y reloj real. No hay fallos pendientes en los escenarios aprobados de esta matriz. **Esto no acredita integración completa de todas las combinaciones solicitadas.**

Quedan sin comprobar de extremo a extremo en esta auditoría: subida/lectura real de fotos y archivos por almacenamiento externo después de transferir; todos los estados históricos de entregas/revisión/No satisfecha combinados con incidencias; especialidad/periodo de prueba y cobertura de cierre en combinaciones adversas; arrastres/posposiciones pendientes con resolución desde sus propios formularios; cierres semanales reales persistidos y obligaciones mensuales atravesando el cambio de mes; agotamiento efectivo de 20,000 pasos provocado en la versión final; dos usuarios distintos confirmando simultáneamente; pérdida real de respuesta de red; y recorridos completos de Mi trabajo, planificación y todas las rutas productoras tras la incidencia. El rollover probado no sustituye esas obligaciones multidiarias, y una prueba de materialización no sustituye un cierre real.

La huella sigue incluyendo un conjunto amplio de datos: un **cambio real**, incluso de otra operación, puede requerir nueva revisión. Esta auditoría corrige revisiones sin cambios; no recorta el alcance de concurrencia sin demostrar primero qué dependencias se pueden excluir con seguridad. No surgió una nueva decisión de negocio necesaria para estas correcciones.

Evaluación: candidato corregido con evidencia sólida sobre los fallos encontrados y los recorridos descritos; **validación integral aún pendiente** en los puntos anteriores. No se recomienda describirlo como «completamente integrado» ni convertir los resultados aislados en una validación operativa compartida.

## 23. Cierre de los recorridos pendientes en entorno aislado (04/11/2026)

Esta sección actualiza la evaluación de la sección 22. Se conservaron sus cambios locales y las evidencias de la copia representativa. Cambio **mixto**: consulta histórica de evidencia y presentación en el recibo, más pruebas de integración de los productores y consumidores existentes. No se desplegó, hizo commit/push ni se escribió en la simulación compartida.

### Defecto reproducido y corrección

La carga real de cámara, confirmación de ausencia y retiro dejaban la fotografía almacenada pero inaccesible desde el recibo. `run02.log` reproduce el fallo después de cargar mediante formulario, normalizar y analizar con ClamAV. Las consultas ordinarias de Mi trabajo excluyen asignaciones retiradas; no se amplió su autorización de escritura para resolverlo.

`OperationalIncidenceService.Evidence.cs` incorpora listado y descarga de solo lectura autorizados mediante `HistoryAsync`: rol supervisor, persona dentro de alcance, asignación incluida en las disposiciones visibles y archivo creado como máximo en la fecha de confirmación. No permite usar un identificador de otra asignación ni consultar entregas posteriores desde el recibo antiguo. `Incidents.cshtml(.cs)` presenta enlaces dentro de un desplegable, fecha, autora de captura y distinción entre captura sin entregar y entrega; preserva codificación Razor, descarga como adjunto, `nosniff` y `private, no-store`. No convierte capturas anteriores en una entrega de la destinataria.

Precisión respecto a la sección anterior: **las fotos y adjuntos operativos de esta ruta se guardan en PostgreSQL**, no en SeaweedFS. La dependencia externa recorrida en la carga fue el antivirus ClamAV local. Configurar un bucket no era una precondición de estos escenarios; el host de prueba registra el scanner real y mantiene el almacenamiento ajeno sin uso. El PDF histórico se introduce como registro legado en un fixture y se descarga por el servicio real; no se presenta como una subida de PDF desde formulario.

### Entornos y matriz ejecutada

Todas las bases de esta sección son nuevas, localhost:5440, con prefijo `sgol_task_checks_`. La herramienta comprueba host y prefijo antes de migrar o escribir. El host Razor aislado usó HTTPS 7296 y **autenticación real del producto**: credenciales sintéticas privadas, contraseña, enrolamiento MFA/TOTP, sesión persistida, cookies, CSRF y filtros de pantalla reales. No se sustituyó la sesión por un usuario fijo. Esto tampoco constituye autenticación ni validación operativa en la app compartida.

Evidencia privada bajo `.local/incidence-integration-20261104/`. Estados siguientes corresponden a ejecuciones, no a escenarios propuestos:

| Precondición / acción | Resultado esperado y observado | Entorno / prueba / evidencia | Estado |
|---|---|---|---|
| Entrega real pendiente de revisión, concluida, no satisfecha o reasignada; intentar ausencia del mismo día | Bloqueo específico de trabajo entregado; no reescribir asignación ni corrección | `domain-final.log`, `IncidenceIntegrationChecks` | Aprobado |
| Cierre semanal materializado por lectura real de bono; confirmar ausencia en semana posterior | Cierre existente y versión de resultado idénticos tras incidencia y nueva lectura | Misma suite, comparación serializada antes/después | Aprobado |
| Obligación mensual 30/11–02/12; ausencia y avance del reloj a diciembre | Conservar responsable, plazo, esfuerzo, instantánea y bono, sin exclusión | Misma suite, reloj controlado | Aprobado |
| Destinataria sin especialidad, en prueba o habilitada; sobrecarga explícita | Rechazar las dos incompatibles, aceptar la habilitada sin dispensar restricciones | Misma suite | Aprobado |
| Dos directoras distintas, contextos y claves diferentes, mismo día/persona | Una única confirmación y recibo; contendiente sin cambios parciales | Misma suite, llamadas simultáneas | Aprobado |
| Nueve tareas y ventana insuficiente diseñada para consumir 20,000 pasos | Diagnóstico de búsqueda agotada; cero ausencias y nueve asignaciones intactas | Misma suite, búsqueda real | Aprobado |
| Ausencia de persona con cobertura de cierre; nueva elección y asignación ocasional | Persona ausente excluida, rechazo de asignación incompatible, reemplazo explícito y destinataria elegible aceptados | Misma suite, servicios reales con huella de agenda | Aprobado |
| Subir fotos mediante cámara; transferir una tarea y retirar otra | Bytes verificados conservados, recibo descargable, autora original y estado sin entregar visibles | `run07.log`, formularios Razor, PostgreSQL y ClamAV reales | Aprobado |
| Solicitar evidencia con empleada, recibo falso, asignación ajena o foto de otra tarea | 403/404 según autorización y pertenencia; no filtrar archivo | Mismo recorrido HTTP | Aprobado |
| Destinataria carga y entrega otra foto después de transferir | Entrega con su captura propia; descarga autorizada; responsable anterior sin acceso y recibo antiguo excluye archivo posterior | Mismo recorrido HTTP | Aprobado |
| Transferir tarea EXCEL, entregar XLSX multipart y descargar | Validación real de Excel y antivirus; bytes descargados idénticos; anterior responsable sin acceso | Mismo recorrido HTTP | Aprobado |
| Crear posposición y arrastre por incidente desde sus formularios; revisar ausencia; resolver en Validación | Incidencia bloqueada mientras petición pendiente; resolución por formularios, original conservado, sin ítems pendientes | Mismo recorrido HTTP; posposición queda RESUELTA con resultado RECHAZO; incidente RECHAZADA | Aprobado |
| Abrir Mi trabajo, Validación, Planificación, bono y Reportes tras confirmar/entregar | Páginas autenticadas responden 200; persistencia y permisos de las operaciones se verifican separadamente | Mismo recorrido HTTP; estos GET no equivalen a probar cada acción de cada pantalla | Aprobado en alcance descrito |
| Ausencia futura antes de crear plan ordinario y plan semanal de operaciones | Generación real excluye a la persona ausente; actualización de catálogo aplicada sin reasignarle trabajo | `planning03.log`, `IncidencePlanningIntegrationChecks` | Aprobado |
| Entrega de ayer, ausencia hoy y mañana; generar corrección | Fecha personalizada incompatible rechazada sin hijo; búsqueda de hueco salta ambas ausencias y persiste en día permitido | Misma suite de planificación | Aprobado |
| Transferir tarea de cierre con sobrecarga a persona sin cobertura; cambiar cobertura explícitamente | Primer intento bloqueado sin cambios; nueva propuesta y transferencia válidas después de cambiar cobertura | Misma suite de planificación | Aprobado |
| PDF legado en tarea retirada, supervisor dentro/fuera de alcance después de cambio de rol | Listado y bytes conservados dentro de alcance; acceso denegado fuera; Dirección conserva consulta autorizada | `evidence-final.log`, `IncidenceEvidenceIntegrationChecks` | Aprobado |
| Recibo real en Chromium/Edge y WebKit, 320/768/1440 px, temas light/dark/special | Sin desbordamiento, desplegable y enlace operables con teclado/foco, descarga real, sin errores JavaScript | `browser-results.json`, 18 capturas; inspección visual adicional móvil oscuro y escritorio claro | Aprobado |
| Cortar respuesta de red **después** del commit; reenviar mismo cuerpo/clave, recargar y abrir segunda pestaña | Se recupera el mismo recibo; no se pide una segunda confirmación distinta | Script Playwright, `route.fetch` observa 302 antes de `connectionreset`; 2 motores | Aprobado |
| Regresiones anteriores: tiempo, historial, atomicidad, procesos/preparados, concurrencia, idempotencia, huellas y modelo | Conservar las garantías anteriores al añadir lectura histórica | `regression-final.log`, `--incidences` | Aprobado |

### Resultados reproducibles y fallos de preparación

- Release final: compilación sin errores ni advertencias en `build-final.log`. `Sgol.Web.dll` probada: SHA-256 `AD9F47F3FFE0D8066F14B35878C89635C4FC6DEC21D429DACF84968951744FDC`.
- `rtk proxy dotnet tools/Sgol.TaskChecks/bin/Release/net10.0/Sgol.TaskChecks.dll --incidences`: **135** comprobaciones de incidencias; base `sgol_task_checks_incidence_final_20261104`.
- Mismo comando con `--incidence-integration`: **33**, base `sgol_task_checks_incidence_domain_final_20261104`.
- Con `--incidence-integration-http`: **51**, base `sgol_task_checks_incidence_integration_07_20261104`; incluye espera de navegador mediante `SGOL_INCIDENCE_BROWSER=1`.
- Con `--incidence-planning-integration`: **18**, base `sgol_task_checks_incidence_planning_03_20261104`.
- Con `--incidence-evidence-integration`: **4**, base `sgol_task_checks_incidence_evidence_20261104`.
- `node tools/Sgol.MobileChecks/incidence-integration-checks.cjs`: **22** escenarios de navegador con sesiones autenticadas reales del host aislado. `NODE_PATH` corresponde a `node_modules` del runtime instalado de Node; Chromium usa canal `msedge`, WebKit la instalación compatible de Playwright. Cookies privadas y capturas no se versionan.
- Total de esta sección: **241 comprobaciones de servicio/HTTP y 22 escenarios de navegador**, todos aprobados en sus ejecuciones finales. No se suman los preámbulos de reglas generales de TaskChecks ni se suman nuevamente los resultados de la sección 22.
- Conservaron logs de intentos intermedios. `run01` falló por scanner fail-closed del host incompleto; `run02` reprodujo el defecto del producto; `run04` detectó expectativa incorrecta del fixture (RESUELTA con RECHAZO no es RECHAZADA); `run05` requirió la huella de revisión ocasional; `run06` carecía de tarea dentro del tramo del incidente. `planning01` omitía un ítem del catálogo semanal. Se corrigieron fixtures, sin dispensar validaciones. Un build intermedio encontró TaskChecks bloqueado por su host aún activo; se compiló después de su cierre normal. Ninguno se contabiliza como una ejecución final aprobada.
- Revisión del diff, `rtk proxy git diff --check`, enlaces locales del documento y exclusión Git de evidencias privadas. Sin migración nueva; la regresión verifica concordancia del modelo.

### Evaluación final y límites

Se cerraron en entorno aislado los recorridos críticos enumerados como pendientes en la sección 22, con la precisión de almacenamiento y alcance de pruebas indicada arriba. El candidato queda **listo para una activación autorizada y su comprobación posterior en la app compartida**. Las operaciones de procesos/preparados y la copia representativa mantienen la evidencia detallada de la sección 22; la regresión final volvió a cubrir procesos y bloqueos de activación. No se afirma que cada combinación imaginable ni cada acción de todas las pantallas haya sido ejecutada.

No ejecutado por alcance vigente: despliegue, confirmación operativa compartida ni modificación de asignaciones compartidas. No ejecutado por disponibilidad de equipo: dispositivo móvil físico y lector de pantalla; se verificaron viewport móvil y teclado en los dos motores. SeaweedFS no aplica a estos adjuntos operativos. No surgió una decisión de negocio nueva pendiente.

El host aislado terminó normalmente; 7296/7297/7298 quedaron sin listeners. Se comprobaron nuevamente Web compartida 34452 / padre 31712 con publicación `incidencias-concurrency-20261104` y proxy 20652, sin reiniciarlos. Por no estar desplegada la nueva consulta, **la bóveda permanece sin cambios**: tras una activación autorizada habrá que reconciliar las referencias de ejecución/evidencia y de la pantalla de incidencias con el comportamiento realmente publicado. Esta documentación de ingeniería registra el candidato, no sustituye la referencia vigente de la app compartida.

## 24. Publicación autorizada después de commit y push (04/11/2026)

La petición posterior autorizó commit, push y despliegue. Se guardaron los cambios y regresiones en `400b6a3` y se subieron a `origin/master` antes de sustituir Web. No había divergencia remota ni cambios ajenos al alcance.

`rtk dotnet publish src/Sgol.Web/Sgol.Web.csproj -c Release --no-build --no-restore -o .local/runtime/incidencias-integration-20261104` produjo la misma DLL SHA-256 de la sección 23. Se verificaron nuevamente identidad, argumentos y listener de la publicación anterior; únicamente se detuvieron Web 34452 y su lanzador 31712. Nueva publicación efectiva: `.local/runtime/incidencias-integration-20261104/Sgol.Web.dll`, Web 40904, lanzador 21908, listener `127.0.0.1:7241`. Se actualizó `.local/processes.json` preservando el proxy 20652 / lanzador 23392. El artefacto anterior se conserva para recuperación. No hubo migraciones ni operaciones manuales sobre asignaciones o ausencias.

Readiness/live internos, `/acceso` local y LAN y live LAN devolvieron 200; TLS se validó normalmente. Incidencias sin sesión redirigió a `/acceso`, el CSS servido coincide con el checkout y el log de errores de arranque está vacío. Estas comprobaciones acreditan publicación y acceso, no un recorrido autenticado compartido ni una nueva confirmación operativa. Evidencia privada: `.local/deploy-incidencias-integration-20261104/`.

La revisión automática rechazó el primer comando combinado de sustitución sin ejecutarlo, con motivo genérico «blocked by policy». Las operaciones separadas y acotadas de comprobación, detención de los dos PID identificados y arranque fueron aceptadas y completadas; no queda un bloqueo de despliegue.

Se reconciliaron seis notas existentes de la bóveda y sus copias versionadas: supervisión, ejecución/evidencia, procesos, pantallas, instalación local y base documental. No se sobrescribieron divergencias ni se añadieron informes a la bóveda. Graphify se actualizó y verificó; se comprobaron 351 enlaces locales entre referencias afectadas, mapas e inventario. Esta sección sustituye el estado «sin desplegar / actualización documental pendiente» de las secciones anteriores para el candidato publicado, conservando sus límites de pruebas compartidas y dispositivos físicos.
