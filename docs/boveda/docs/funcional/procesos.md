---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Procesos de trabajo

## Definición y publicación

Un proceso reúne tareas ordenadas y reglas de etapa. El borrador es editable; publicar crea una versión con instantáneas que las ejecuciones conservan. Guardar o publicar no equivale a asignar trabajo.

Publicar comprueba pertenencia completa, actividad, tareas eliminadas, duplicados y miembros ausentes del orden. Los conflictos identifican tarea y causa. Quitar una tarea o eliminar un proceso respeta referencias, versiones y actividad; no equivale a borrar sus entregas históricas.

## Inicio y reservas

Los inicios admiten ocasional, semestral y recurrencias semanal, mensual, trimestral y anual. Las recurrentes capturan modalidad distribuida/días fijos, repeticiones, fechas y hora con minutos. Solo la semanal participa mediante su opción específica en operaciones semanales.

Los distribuidos admiten hasta uno por fecha nominal, con límites de 7 semanales, 28 mensuales, 90 trimestrales y 100 anuales. Las fechas mensuales explícitas determinan repeticiones y omiten días inexistentes. La clave proceso/fecha evita duplicación entre rutas de planificación. Una repetición puede iniciar aunque la anterior siga pendiente: son ejecuciones distintas.

Preparar reserva responsables y capacidad de las etapas; activar crea ejecución e intentos. La primera se programa y las siguientes esperan la condición de entrega o aprobación definida. Programar el inicio no reemplaza cargas ni condiciones de las tareas. Una ocasión exige fecha y hora explícitas al preparar el plan; calendario usa la hora publicada. Los planes ya preparados conservan su instante capturado.

## Ejecución y revisión

Cada etapa conserva responsable, intento, fechas, evidencia y eventos. Solo el intento vigente ejecutable recibe la entrega. Una devolución conserva el intento anterior y crea la continuación o ronda correspondiente, sin simular aprobaciones. El proceso documental conserva entradas, resultados y decisiones sobre documentos.

Sin tramo compatible, la etapa puede permanecer pendiente de asignación. Los reintentos de activación no fuerzan capacidad ni sustituyen personas silenciosamente. Mi trabajo muestra reservas de ejecución sin duplicar la asignación cuando se habilita.

La [carga por ítems](carga-por-items.md) calcula trabajo siguiente y tiene reglas para cero, corrección de conteo y falta de capacidad. Las versiones e instantáneas protegen ejecuciones preparadas o iniciadas frente a cambios posteriores.

Relacionados: [Planificación](planificacion.md), [Ejecución](ejecucion-evidencias.md), [Actualización](actualizacion-asignaciones.md), [Modelo](modelo-historial.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [OperationalSequences.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalSequences.cs)
- [OperationalProcessCatalogService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessCatalogService.cs)
- [ProcessMembershipConflict.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessMembershipConflict.cs)
- [ProcessStartSchedule.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessStartSchedule.cs)
- [ProcessPlanningService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessPlanningService.cs)
- [OperationalProcessService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
