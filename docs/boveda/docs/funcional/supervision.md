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

La corrección conserva el origen mediante un vínculo y nuevo intento. El anterior queda consultable y el nuevo usa requisito y carga configurada según el flujo. Revisión masiva conserva atomicidad: no deja decisiones parciales si una tarea falla.

## Programar una corrección

| Modalidad | Condición |
|---|---|
| Cuanto antes | Primer tramo permitido; admite superposición y exceso de capacidad |
| Primer espacio libre | Exige hueco entre tareas; admite exceso de capacidad diaria |
| Personalizada | Exige hueco dentro del día/rango; admite exceso de capacidad diaria |

Siempre debe caber completa la duración de carga más margen en el tramo permitido; no se recorta al fin del turno. El horizonte llega hasta el final del día local hoy+7. Se mantienen rol/especialidad, ausencias y cobertura. Puede superar el fin del plan original conservándolo como origen histórico. Cambiar disponibilidad exige revisión explícita, no movimiento silencioso. Los procesos mantienen su flujo de etapas.

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

El formulario conserva el instante de la propuesta con su precisión completa al confirmar. Cambiar datos exige revisar de nuevo. La confirmación conjunta revalida el estado y vencimientos, incluso al commit; un fallo revierte el conjunto. Repetir la misma intención devuelve el recibo existente. No se permite restaurar solo disponibilidad para deshacer la incidencia. Ausencias parciales, reversión automática y formulario de reprogramación posterior de procesos no están disponibles.

Fuentes: [servicio de incidencias](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalIncidenceService.cs), [formulario](../../../../src/Sgol.Web/Pages/Validations/Incidents.cshtml). Relaciones: [Disponibilidad](horarios-cobertura.md), [Procesos](procesos.md), [Bono](bono-tareas.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Incidencias se contrastó con el código publicado y pruebas funcionales aisladas. En el host compartido se comprobaron salud, acceso y conservación de datos al migrar; no se confirmó ninguna ausencia ni se repitió un recorrido autenticado. Las demás reglas conservan su alcance de revisión estática.

- [OperationalSupervisionService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.cs)
- [OperationalSupervisionService.Reassignment.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Reassignment.cs)
- [OperationalSupervisionService.Transfer.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Transfer.cs)
- [OperationalSupervisionService.Batch.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Batch.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
