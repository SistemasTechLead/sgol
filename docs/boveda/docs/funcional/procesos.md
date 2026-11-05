---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-05
verificado_en: codigo-publicado-y-pruebas-aisladas
---

# Procesos de trabajo

## Definición y publicación

Un proceso reúne tareas ordenadas y reglas de etapa. El borrador es editable; publicar crea una versión con instantáneas que las ejecuciones conservan. Guardar o publicar no equivale a asignar trabajo.

Publicar comprueba pertenencia completa, actividad, tareas eliminadas, duplicados y miembros ausentes del orden. Los conflictos identifican tarea y causa. Quitar una tarea o eliminar un proceso respeta referencias, versiones y actividad; no equivale a borrar sus entregas históricas.

## Inicio y reservas

Activar versión publicada genera un calendario de los próximos doce meses, desde el día local de activación hasta el anterior al aniversario. Excluye ocasiones y procesos marcados para operaciones semanales. Cada ejecución recibe responsables por elegibilidad y capacidad; una fecha conflictiva queda pendiente con su causa, sin reservar carga ni impedir las demás. Las fechas de hoy y futuras se reintentan cada cinco minutos mientras el proceso esté habilitado. Las vencidas requieren reprogramación manual, conservando fecha original, hora publicada e historial; la nueva fecha debe estar entre hoy y el final del plan. Si el calendario terminó, se prepara una ocasión nueva.

Reactivar completa fechas faltantes sin duplicar ni sustituir las ya capturadas. No hay renovación indefinida por temporizador. Desactivar pausa la asignación de pendientes, sin cancelar ejecuciones ya creadas. La migración conserva los planes existentes y no genera automáticamente calendarios para todos los procesos ya habilitados.

Los inicios admiten ocasional, semestral y recurrencias semanal, mensual, trimestral y anual. Las recurrentes capturan modalidad distribuida/días fijos, repeticiones, fechas y hora con minutos. Solo la semanal participa mediante su opción específica en operaciones semanales. Las versiones nuevas que reportan conteos se excluyen de esa generación semanal y se preparan en Planificación → Procesos; allí se capturan los vínculos de la versión, sin volver a elegirlos.

Los distribuidos admiten hasta uno por fecha nominal, con límites de 7 semanales, 28 mensuales, 90 trimestrales y 100 anuales. Las fechas mensuales explícitas determinan repeticiones y omiten días inexistentes. La clave proceso/fecha evita duplicación entre rutas de planificación. Una repetición puede iniciar aunque la anterior siga pendiente: son ejecuciones distintas.

Preparar reserva responsables y capacidad de las etapas; activar crea ejecución e intentos. La primera se programa y las siguientes esperan la condición de entrega o aprobación definida. Programar el inicio no reemplaza cargas ni condiciones de las tareas. Una ocasión exige fecha y hora explícitas al preparar el plan; calendario usa la hora publicada. Los planes ya preparados conservan su instante capturado.

## Ejecución y revisión

Cada etapa conserva responsable, intento, fechas, evidencia y eventos. Solo el intento vigente ejecutable recibe la entrega. Una devolución conserva el intento anterior y crea la continuación o ronda correspondiente, sin simular aprobaciones. El proceso documental conserva entradas, resultados y decisiones sobre documentos.

Sin tramo compatible, la etapa puede permanecer pendiente de asignación. Los reintentos de activación no fuerzan capacidad ni sustituyen personas silenciosamente. Mi trabajo muestra reservas de ejecución sin duplicar la asignación cuando se habilita.

La [carga por ítems](carga-por-items.md) calcula las cargas de las receptoras configuradas en Editar detalles y tiene reglas para cero, corrección de conteo y falta de capacidad. Las versiones e instantáneas protegen ejecuciones preparadas o iniciadas frente a cambios posteriores.

Relacionados: [Planificación](planificacion.md), [Ejecución](ejecucion-evidencias.md), [Actualización](actualizacion-asignaciones.md), [Modelo](modelo-historial.md).

Retirar una etapa por [incidencia de ausencia](supervision.md#incidencias-de-ausencia) deja la etapa y las reservas dependientes inviables en Pendiente de reprogramación. No cancela la ejecución ni satisface dependencias. Se conservan intentos y evidencia; el temporizador no recrea el trabajo retirado. Los inicios preparados afectados quedan retenidos y liberan sus reservas. Activar el plan o cambiar solo responsables no resuelve esa retención. El formulario para reprogramar posteriormente la cadena no está disponible.


El temporizador conserva la revisión de una etapa cuando una comprobación no modifica sus valores persistidos. Un cambio real de estado o reserva sí actualiza la revisión; esperar sin cambios no invalida por sí solo una propuesta de incidencia.

## Fuentes y alcance

Calendario automático: código publicado y pruebas de servicios/formularios en PostgreSQL aislado, más Chromium/WebKit en tres tamaños y temas. La publicación compartida se comprobó por hash, salud y acceso; no se realizó una activación autenticada de prueba en ella.

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Incidencias se contrastó con el código publicado y pruebas funcionales aisladas. En el host compartido se comprobaron salud, acceso y conservación de datos al migrar; no se confirmó ninguna ausencia ni se repitió un recorrido autenticado. Las demás reglas conservan su alcance de revisión estática.

- [OperationalSequences.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalSequences.cs)
- [OperationalProcessCatalogService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessCatalogService.cs)
- [ProcessMembershipConflict.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessMembershipConflict.cs)
- [ProcessStartSchedule.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessStartSchedule.cs)
- [ProcessPlanningService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessPlanningService.cs)
- [OperationalProcessService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
