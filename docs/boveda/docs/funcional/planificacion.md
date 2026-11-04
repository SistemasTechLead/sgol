---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-04
verificado_en: codigo-publicado-y-pruebas-aisladas
---

# Planificación, capacidad y criticidad

## Preparar y confirmar

La planificación combina catálogo, personas elegibles, horarios, ausencias, capacidad, trabajo existente y reservas de procesos. Una vista previa no crea el compromiso definitivo. La confirmación vuelve a comprobar entradas y agenda; si cambian, exige recalcular. La persistencia agrupa plan, asignaciones, reservas y ajustes autorizados con protección contra reintentos.

Las operaciones semanales incluyen tareas participantes y procesos semanales configurados para ello. Un proceso se representa como unidad de inicio y reserva sus etapas; no se duplican sus tareas como independientes. Véase [Procesos](procesos.md).

## Carga y tiempo

La carga son minutos de trabajo; el intervalo ordinario incorpora el margen capturado: duración = techo(carga × (100 + margen) / 100). El margen no aumenta la carga. Criticidad, activación nominal y política se capturan en el compromiso.

Las críticas actúan como anclas; importantes y flexibles se colocan según el motor, respetando turno, descansos, dependencias y ocupación. Las metas progresivas no son bloques continuos ordinarios. Una asignación histórica sin la política actual conserva su contrato: no se recalcula por abrir la pantalla.

El circuito de caja contiene precedencias explícitas, no relaciones deducidas del nombre. [Horarios y cobertura](horarios-cobertura.md) determinan quién puede recibir trabajo y cuándo.

## Conflictos y decisiones

El conflicto identifica tarea, candidatos y causa. Alcanzar un límite de búsqueda es resultado inconcluso, no prueba de imposibilidad. La revisión desde la fila permite elegir ajustes compatibles y recalcular el conjunto.

Los aumentos de capacidad por fecha se guardan al confirmar el plan y quedan disponibles para otras asignaciones de esa persona y fecha. No superan el horario permitido ni el máximo admitido de 480 minutos. Explorar una alternativa no guarda capacidad. Los movimientos de ocurrencias flexibles se limitan a las reglas y fechas autorizadas; no omiten tareas ni alteran anclas para forzar una solución.

Las vencidas anteriores conservan seguimiento; no se recrean ni mueven silenciosamente para construir otro plan. La carga de una consulta operativa y la de un nuevo plan tienen alcances distintos.

Las [correcciones](supervision.md), [posposiciones](arrastres-posposiciones.md) y [resoluciones por ítems](carga-por-items.md) tienen excepciones propias. No trasladar una excepción de sobrecarga de una operación a otra.

El reparto por [incidencia de ausencia](supervision.md#incidencias-de-ausencia) distingue capacidad diaria de tiempo de turno restante y descuenta esfuerzo completado sin duplicar demanda. Una sustitución administrativa no acredita esfuerzo realizado. La excepción de sobrecarga es local a la tarea revisada; no altera configuraciones globales. Las obligaciones de varios días se distribuyen en días compatibles sin extender el vencimiento.


## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Incidencias se contrastó con el código publicado y pruebas funcionales aisladas. En el host compartido se comprobaron salud, acceso y conservación de datos al migrar; no se confirmó ninguna ausencia ni se repitió un recorrido autenticado. Las demás reglas conservan su alcance de revisión estática.

- [OperationalScheduling.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalScheduling.cs)
- [PlanningCapacity.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/PlanningCapacity.cs)
- [PlanningConflictResolution.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/PlanningConflictResolution.cs)
- [EfDirectionPreparedPlanService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/EfDirectionPreparedPlanService.cs)
- [OperationalCapacityService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCapacityService.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
