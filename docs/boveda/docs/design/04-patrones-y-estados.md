---
categoria: diseno
tipo: referencia
estado_documental: inspeccionado
clasificado_el: 2026-11-03
---

# Patrones y estados de SGOL

[Diseño actual](00-diseno-sgol.md) · [Componentes](03-componentes.md) · [Pantallas](05-pantallas.md)

## Consultar y filtrar

La consulta conserva sus filtros en campos y parámetros GET. La navegación, los filtros y las acciones sobre datos se presentan como grupos diferentes. Los apartados de Supervisión comparten el contexto de filtros al cambiar de vista. En Reportes, cambiar controles marca la configuración como pendiente de aplicar hasta Generar reporte.

El vacío explica qué consulta no tiene resultados y ofrece una salida cuando corresponde. No encontrar coincidencias con filtros no equivale a que el catálogo completo esté vacío. La paginación conserva la intención de consulta y no representa una modificación de registros.

Fuentes: [filtros de Validación](../../../../src/Sgol.Web/Pages/Validations/_ValidationFilters.cshtml), [navegación](../../../../src/Sgol.Web/Presentation/Navigation/SectionNavigation.cs), [Reportes](../../../../src/Sgol.Web/Pages/Reports/Index.cshtml).

## Seleccionar y actuar

El catálogo Tareas usa selección explícita de una fila y una barra contextual para editar, asignar, activar/desactivar o eliminar según la elegibilidad de la tarea. Actualizar Asignaciones es una acción global del catálogo. La selección no concede permisos y no ejecuta una modificación por sí misma.

En Mi trabajo, cada registro muestra una acción según su estado: Registrar avance, Adjuntar evidencia, Ver evidencia o Ver proceso. Una etapa pendiente del proceso muestra su situación y fechas estimadas; no ofrece la entrega de una etapa todavía no habilitada.

Fuentes: [Tareas](../../../../src/Sgol.Web/Pages/Tasks/Index.cshtml), [acciones de catálogo](../../../../src/Sgol.Web/wwwroot/js/task-actions.js), [tabla de Mi trabajo](../../../../src/Sgol.Web/Pages/MyWork/_OperationalTaskTable.cshtml).

## Preparar y confirmar

Las acciones que necesitan revisar consecuencias presentan una propuesta y una confirmación diferenciadas. La vista previa expone responsables, fechas, carga o registros afectados según el recorrido. Los cambios a la intención pueden invalidar la propuesta antes de confirmar.

| Recorrido | Información que se revisa |
|---|---|
| Asignar tarea ocasional | Destinatarios y horarios propuestos. |
| Planificación | Tareas, responsables, capacidad, incidencias y ajustes que se guardarán. |
| Plan de proceso | Fechas y capacidad antes de guardar; activación posterior del plan preparado. |
| Actualizar Asignaciones | Casos que requieren decisión, cambios agrupados y asignaciones afectadas. |
| Transferir responsabilidad | Persona, carga, fechas y conservación de lo realizado. |
| Revisión o corrección | Decisión, motivo y programación aplicable a la corrección. |
| Posposición por tarea | Aprobar, reasignar, cambiar propuesta o rechazar, con el resultado antes de confirmar. |

Los conflictos de actualización se presentan como tarea, causa y solución, con detalles adicionales desplegables. En posposiciones, los obstáculos tienen título y explicación contextual; la falta de destinatarios elegibles se explica junto a Reasignar. Cuando una tarea ya fue entregada, el editor puede ofrecer cerrar la petición sin cambios.

Fuentes: [actualización](../../../../src/Sgol.Web/Pages/Tasks/Refresh.cshtml), [posposición](../../../../src/Sgol.Web/Pages/Validations/_PostponementRequest.cshtml), [obstáculos](../../../../src/Sgol.Web/Pages/Validations/_PostponementBlocks.cshtml), [transferencia](../../../../src/Sgol.Web/Pages/Validations/Workload.cshtml).

## Estados de interacción

| Estado | Manifestación implementada |
|---|---|
| Disponible | Controles con etiqueta y foco, datos consultables y acciones permitidas. |
| Procesando | Formularios conectados al controlador compartido usan `aria-busy`, deshabilitan el envío y muestran Verificando, Guardando o Cerrando sesión. |
| Cargando datos | El componente de tabla admite filas de esqueleto y región ocupada; no todas las páginas lo utilizan. |
| Sin resultados | Mensaje vacío contextual, con acción cuando el modelo la proporciona. |
| Error de campo | Mensaje asociado, estilo de error y estado accesible en los componentes que lo implementan. |
| Error de operación | Aviso con explicación; puede incluir identificador técnico desplegable. |
| Conflicto | Causa y entidad cuando el recorrido las conoce; permite revisar o recalcular según la acción. |
| Éxito | Confirmación o mensaje de estado posterior al resultado confirmado. |
| Deshabilitado o lectura | Control inactivo o evidencia de consulta; no significa que la acción haya sido ejecutada. |
| Resultado incierto | Los recorridos que lo contemplan conservan la intención y ofrecen comprobación o reenvío explícito; no presentan éxito sin confirmación. |

Fuentes: [components.js](../../../../src/Sgol.Web/wwwroot/js/components.js), [_DataTable](../../../../src/Sgol.Web/Pages/Shared/_DataTable.cshtml), [planificación](../../../../src/Sgol.Web/Pages/Planning/_DirectionPlans.cshtml).

## Tiempo, entrega y calidad

El color temporal de una tarea no sustituye la decisión de revisión. Mi trabajo y el tablero comparten familias visuales de tiempo y entrega:

| Familia | Significado visible |
|---|---|
| Rojo | Vencida o en el último 10 % del tiempo. |
| Verde | Activa fuera de los tramos de advertencia. |
| Amarillo | Último 30 % del tiempo, antes del tramo urgente. |
| Gris | Por activar; en Mi trabajo también hay una variante neutra para tareas cerradas sin satisfacción. |
| Azul | Entregada. La revisión puede seguir pendiente. |

Mi trabajo añade etiquetas como En revisión, Concluida, No satisfecha y Reasignada. Una tarea cerrada como No satisfecha no se presenta como si estuviera programada para el futuro. Las etapas de proceso distinguen Disponible, En espera, Programada, Pendiente de asignación y Concluida.

El listado de Mi trabajo ordena grupos por rojo, verde, amarillo, gris y azul, y por vencimiento dentro del grupo. Las etiquetas y acciones específicas siguen el estado de cada registro. La presentación de un plazo puede incluir información de arrastre y el vencimiento original; no se sustituye silenciosamente la historia por el último plazo.

Fuentes: [presentación operativa](../../../../src/Sgol.Web/Pages/MyWork/Index.Operational.cs), [leyenda](../../../../src/Sgol.Web/Pages/MyWork/_TaskColorLegend.cshtml), [estilos](../../../../src/Sgol.Web/wwwroot/css/components.css), [plazos con arrastre](../../../../src/Sgol.Web/Pages/Shared/_CarryoverTiming.cshtml).

## Progreso, capacidad y calificación

El progreso de tareas, el avance en unidades, la capacidad en minutos y el porcentaje de bono son métricas distintas y tienen etiquetas propias. El diálogo del bono aclara que evalúa tareas, no el avance ni el bono total. Su resultado incluye estado y periodo; la consulta de Supervisión permite examinar tareas y descuentos.

Las fechas operativas se presentan en hora de Ciudad de México. Los rangos de semana, fechas de cierre y cortes de reporte se muestran con su contexto. La localidad de marca que aparece en el acceso no cambia esa zona operativa.

Fuentes: [_TaskBonusGauge](../../../../src/Sgol.Web/Pages/Shared/_TaskBonusGauge.cshtml), [bono en Supervisión](../../../../src/Sgol.Web/Pages/Validations/Bonus.cshtml), [presentación de fechas](../../../../src/Sgol.Web/Interface/MyWork/MyWorkPresentation.cs).

## Ubicación documental

Pertenece a [Diseño e interacción](../../mapas/diseno.md). Estado documental: **inspeccionado**. La fecha de clasificación no es una fecha de verificación funcional.
