---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-04
verificado_en: codigo-publicado-y-pruebas-aisladas
---

# Horarios, elegibilidad y cobertura de cierre

## Disponibilidad efectiva

Horarios personales, excepciones por fecha, ausencias, empleo vigente, rol y especialidades alimentan la elegibilidad. Una persona concreta de una simulación no define una regla del sistema. Capacidad y presencia se vuelven a comprobar al confirmar.

La política capturada determina cómo se comprueba el intervalo. Las tareas con planificación automática reservan el tramo completo de carga más margen; las instantáneas antiguas conservan su contrato. La antigua comprobación de presencia solo al activarse no se aplica indiscriminadamente a toda asignación nueva.

## Cobertura compartida

Dirección y Subcoordinación consultan y guardan la cobertura desde Planificación. Este permiso no concede edición de horarios personales ni preparación de planes exclusivos de Dirección. Asesor Externo y Piso no administran esa cobertura.

La selección es por fecha, con actor, revisión e intención. Considera candidatos del horario efectivo, excluye a Dirección como candidato y conserva ausencias, rol y especialidad. Puede extender una hora el último tramo conforme al límite operativo; no convierte ausencias o descansos explícitos en trabajo ni crea rotación automática.

La tarea de cierre se identifica por configuración, no por coincidencia de nombre. Una propuesta obsoleta se rechaza y un reintento no agrega otra versión.

Las tareas y procesos consumen esta disponibilidad. Cambiar el horario puede invalidar una corrección programada y exigir [reprogramación explícita](supervision.md); no autoriza mover compromisos silenciosamente.

Las [incidencias de ausencia](supervision.md#incidencias-de-ausencia) guardan indisponibilidad versionada para la fecha, conjuntamente con tareas y bono. Una ausencia futura impide nuevas asignaciones incompatibles. Cambiar solo disponibilidad no revierte una incidencia confirmada; esa restauración se rechaza.


## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Incidencias se contrastó con el código publicado y pruebas funcionales aisladas. En el host compartido se comprobaron salud, acceso y conservación de datos al migrar; no se confirmó ninguna ausencia ni se repitió un recorrido autenticado. Las demás reglas conservan su alcance de revisión estática.

- [PersonalSchedules.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/PersonalSchedules.cs)
- [PersonalScheduleService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/PersonalScheduleService.cs)
- [ClosingPlanningService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/ClosingPlanningService.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
