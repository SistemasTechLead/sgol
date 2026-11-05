---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Catálogo de tareas

## Definición y compromisos

El catálogo define nombre, instrucciones, requisito de entrega, carga, rol o especialidad, criticidad, calendario, repeticiones y pertenencia a un proceso. Las tareas ocasionales describen necesidades concretas y preparan sus asignaciones. Las progresivas agregan unidades hacia una meta; su carga y vencimiento tienen reglas distintas de una reserva continua.

Las asignaciones y versiones publicadas capturan su definición. Guardar un cambio de catálogo no reescribe automáticamente trabajo asignado o entregado. Para adoptar cambios en pendientes se utiliza [Actualizar asignaciones](actualizacion-asignaciones.md); los [procesos](procesos.md) conservan además sus versiones publicadas.

## Calendario y consulta

La periodicidad y modalidad determinan ocurrencias; días fijos, repeticiones y fechas mensuales no son equivalentes. La activación dominical opcional sustituye la hora base del domingo. La planificación aplica las ventanas del calendario y las restricciones de cada modalidad.

Los filtros combinan búsqueda, pertenencia a proceso, requisito, periodicidad, repeticiones, criticidad, rol, estado y calendario antes del conteo y paginación. Editar o filtrar no equivale a publicar un plan.

## Instrucciones y evidencia

Fotografías y capturas admiten mínimo y máximo configurables entre 1 y 50. El máximo predeterminado de nuevas tareas es 50. Editar una tarea conserva su límite anterior hasta cambiarlo explícitamente; ampliar el rango admitido no actualiza asignaciones ni versiones existentes.

Las descripciones se guardan como Markdown y se presentan con formato seguro: HTML deshabilitado, enlaces HTTP/HTTPS/mailto y sin carga de imágenes remotas. La edición conserva el texto original. Los requisitos se explican en [Ejecución y evidencias](ejecucion-evidencias.md).

Desactivar una definición no borra por sí solo entregas ni historias. Quitar tareas de un proceso exige conciliar pertenencia, orden y publicación. El retiro de pendientes se decide explícitamente en la actualización.

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [OperationalTasks.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTasks.cs)
- [OperationalTaskService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTaskService.cs)
- [TaskCatalogFilter.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskCatalogFilter.cs)
- [MarkdownContent.cs](../../../../src/Sgol.Web/Presentation/Components/MarkdownContent.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
