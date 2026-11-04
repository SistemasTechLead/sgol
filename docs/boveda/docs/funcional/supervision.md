---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-04
verificado_en: codigo-publicado-y-pruebas-aisladas
---

# Supervisión, correcciones y transferencias

## Revisión

La revisión autorizada distingue aprobación, No satisfecha y devolución para corrección. Entrega y decisión tienen fechas y autores separados. Una conclusión administrativa no se atribuye a un envío del colaborador. El alcance depende del rol, persona supervisada y operación; ver una pantalla no concede todas sus acciones.

La corrección conserva el origen mediante un vínculo y nuevo intento. El anterior queda consultable y el nuevo usa requisito y carga configurada según el flujo. Revisión masiva conserva atomicidad: no deja decisiones parciales si una tarea falla. En las tarjetas de Validación y en la revisión individual de Supervisión, Validar guarda en un solo envío sin vista previa ni segunda confirmación. Calcula las correcciones y comprueba permisos, revisión vigente, evidencia, comentario y programación dentro de la transacción. Los errores identifican la tarea y conservan las selecciones y comentarios. La reprogramación posterior de una corrección mantiene propuesta y confirmación.

## Colas de revisión desde Reportes

El Resumen operativo muestra pendientes por nivel revisor, no por rol del autor. Subcoordinación incluye entregas de Piso de ventas; Dirección incluye entregas de Subcoordinación y tareas con especialización dentro de sus niveles supervisables. Solo cuentan asignaciones entregadas con revisión nula o pendiente; no se cuentan concluidas, no satisfechas, reasignadas ni retiradas por el filtro global.

Las tarjetas consultan todas las fechas y no heredan filtros del reporte. Se intersectan con el alcance autorizado de quien consulta; no amplían permisos ni asignan un revisor exclusivo. Una entrega especializada de Piso de ventas puede pertenecer a ambas colas, por lo que sus cantidades no se suman. Ir abre Validación con el filtro visible Nivel de revisión, que se conserva al filtrar y paginar. Mi nivel mantiene la selección predeterminada anterior. Las cuentas sin niveles supervisables no reciben estas tarjetas.

Fuente compartida de conteo y listado: [OperationalSupervisionService.ReviewQueues.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.ReviewQueues.cs). Presentación: [Reportes](../design/05-pantallas.md#reportes). El flujo se comprobó con PostgreSQL y formularios reales en una copia aislada, con sesión sintética; no equivale a una prueba autenticada del host compartido.

## Programar una corrección

| Modalidad | Condición |
|---|---|
| Cuanto antes | Primer tramo permitido; admite superposición y exceso de capacidad |
| Primer espacio libre | Exige hueco entre tareas; admite exceso de capacidad diaria |
| Personalizada | Exige hueco dentro del día/rango; admite exceso de capacidad diaria |

Siempre debe caber completa la duración de carga más margen en el tramo permitido; no se recorta al fin del turno. El horizonte llega hasta el final del día local hoy+7. Se mantienen rol/especialidad, ausencias y cobertura. Puede superar el fin del plan original conservándolo como origen histórico. Cambiar disponibilidad exige revisión explícita, no movimiento silencioso. Los procesos mantienen su flujo de etapas.

## Ajustar capacidad y redistribuir

«Carga y reasignación» (/supervision/carga) reúne el ajuste temporal por persona y la transferencia individual. Supervisión (/validaciones) se concentra en entregas, revisiones y peticiones de arrastre; no carga el editor de capacidad.

El ajuste selecciona vigencia, porcentaje, minutos diarios de referencia y motivo. Ver redistribución propuesta no guarda: muestra tareas, responsables, horarios y exceso que permanece. Aplicar capacidad y redistribuir confirma el conjunto. Se revalidan permisos, capacidad y actividad concurrente; una propuesta desactualizada exige otra revisión y no deja escrituras parciales. El formulario conserva motivo y opciones tras un error, abre la persona editada y enfoca el aviso. La fecha interna de la propuesta conserva su precisión completa al confirmar.

La capacidad vuelve al valor aplicable fuera de la vigencia; las tareas transferidas conservan su nuevo responsable. Un ajuste no reduce el esfuerzo de las tareas ni garantiza eliminar la sobrecarga: trabajo protegido o falta de destinatarios pueden dejar exceso visible. Las tareas activadas no seleccionadas mantienen su bloque; avances, evidencia y trabajos históricos conservan las protecciones de su contrato. Las tareas ocasionales mantienen su responsable en este ajuste. Capacidad, movimientos y auditoría se guardan juntos.

El ajuste y la transferencia usan formularios independientes en la misma página. Se comprobaron mediante formularios HTTP/SQL y navegadores Chromium/WebKit en una copia aislada, con sesión sintética y reloj en marcha; la comprobación del host compartido cubre publicación, salud y acceso, sin efectuar ajustes allí.

Fuentes: [página de carga](../../../../src/Sgol.Web/Pages/Validations/Workload.cshtml), [acciones de capacidad](../../../../src/Sgol.Web/Pages/Validations/Workload.Capacity.cs), [servicio de capacidad](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCapacityService.cs). Relación: [Planificación y capacidad](planificacion.md).

## Transferir trabajo pendiente

La transferencia conserva la misma asignación, avance, borrador y evidencias. Cambia responsable y condiciones de recepción autorizadas, con recibo e historial antes/después. No crea una corrección.

Vista previa y confirmación comprueban elegibilidad, presencia, horario, capacidad y actividad concurrente. La carga consultada es la operativa actual, con reservas y pendientes según el alcance de la consulta; no reconstruye automáticamente la carga histórica previa a transferencias.

Relacionados: [Historial](modelo-historial.md), [Posposiciones](arrastres-posposiciones.md), [Bono](bono-tareas.md).

## Incidencias de ausencia

Incidencias (`/supervision/incidencias`) registra ausencias de día completo de hoy o de una fecha futura. Dirección confirma; los demás supervisores consultan personas y disposiciones dentro de su alcance. La consulta por fecha y nombre muestra disponibilidad, pendientes, vencidas, minutos y recibos. No tener ausencia registrada no acredita asistencia.

La vista previa reúne disponibilidad, transferencias, retiros y efecto en el bono. Las vencidas del día se retiran y excluyen conservando historial; deudas de otros días permanecen. Las no vencidas se distribuyen según tiempo de turno restante, capacidad, demanda y elegibilidad, sin reducir esfuerzo. Avances y evidencia requieren una decisión explícita y se conservan. Los casos identifican tarea, causa, efecto, detalles y opciones pertinentes.

La sobrecarga se autoriza por tarea, con destinataria elegida o reparto automático. Antes de confirmar, la propuesta identifica la destinataria concreta, el exceso y los empalmes. No elimina ausencias, especialidades, turno, dependencias ni reservas exclusivas; no cambia la capacidad global. El reacomodo opcional intenta primero conservar las agendas existentes. Solo si hace falta busca mover tareas futuras sin actividad protegida de destinatarias posibles y muestra sus cambios. La búsqueda comprueba alternativas pendientes antes de explorar combinaciones; si agota su límite, no afirma que falte espacio. Una transferencia que vence durante la revisión ofrece Retirar por ausencia para continuar sin transferirla. Las obligaciones que vencen otro día conservan plazo y bono; se revisa su esfuerzo en los días restantes.

Destinataria y sobrecarga solo se muestran al elegir Transferir. Retirar o quitar la decisión oculta, deshabilita y limpia ambas opciones; el servidor descarta esos datos para decisiones que no transfieren.

Las destinatarias ofrecidas cumplen rol o especialidad, disponibilidad y un tramo de horario suficiente. La falta de capacidad por sí sola no las oculta: requiere revisar la carga o autorizar sobrecarga explícita. Los huecos ocupados, dependencias y condiciones conjuntas se validan al revisar. Si una selección anterior deja de ser elegible, se informa y se exige nueva revisión; los envíos manipulados no dispensan la regla. Los conflictos de rol y presupuesto identifican destinataria y causa.

El formulario conserva el instante de la propuesta con su precisión completa al confirmar. Cambiar datos exige revisar de nuevo. La comparación usa orden determinista de los registros: un cambio de orden físico no invalida la propuesta. Los rechazos distinguen conflicto vigente, vencimiento de tarea, inicio propuesto pasado y diferencias de estado; un límite temporal identifica la tarea. El formulario conserva decisiones tras un rechazo para volver a revisar. La confirmación conjunta revalida el estado y vencimientos, incluso al commit; un fallo revierte el conjunto. Repetir la misma intención devuelve el recibo existente. No se permite restaurar solo disponibilidad para deshacer la incidencia. Ausencias parciales, reversión automática y formulario de reprogramación posterior de procesos no están disponibles.

El recibo permite descargar evidencia anterior a la confirmación, incluso de tareas retiradas, dentro del alcance del supervisor. Distingue capturas sin entregar, fecha y autora; no expone entregas posteriores desde el recibo antiguo. La comparación de intención normaliza el orden de decisiones y descarta destinataria/sobrecarga al retirar; los recibos anteriores siguen admitiendo reintentos equivalentes sin reescribir su historial.

Fuentes: [servicio de incidencias](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalIncidenceService.cs), [formulario](../../../../src/Sgol.Web/Pages/Validations/Incidents.cshtml). Relaciones: [Disponibilidad](horarios-cobertura.md), [Procesos](procesos.md), [Bono](bono-tareas.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Incidencias se contrastó con el código publicado y pruebas funcionales aisladas. En el host compartido se comprobaron salud, acceso y conservación de datos al migrar; no se confirmó ninguna ausencia ni se repitió un recorrido autenticado. Las demás reglas conservan su alcance de revisión estática.

- [OperationalSupervisionService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.cs)
- [OperationalSupervisionService.Reassignment.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Reassignment.cs)
- [OperationalSupervisionService.Transfer.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Transfer.cs)
- [OperationalSupervisionService.Batch.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Batch.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
