---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-04
verificado_en: copia-aislada-formulario-real-y-publicacion
---

# Actualizar asignaciones existentes

## Alcance

Dirección adopta diferencias efectivas de catálogo en pendientes mediante una vista previa global, independiente de fila o filtros. Una revisión diferente sin cambios de parámetros no basta. Incluye etapas pendientes sin asignación ejecutable.

Entregadas, pendientes de revisión, concluidas, no satisfechas y sustituidas quedan fuera del conjunto actualizable. Se conservan identificadores, autoría, avances, evidencias, correcciones y transferencias.

## Decisiones según el cambio

| Cambio | Tratamiento |
|---|---|
| Nombre, instrucciones, requisito, checklist y límites de imágenes | Adoptar metadatos sin mover agenda por ese solo cambio |
| Carga, meta, elegibilidad, criticidad o calendario | Revisar conjunto con capacidad, horario y dependencias |
| Meta menor que avance | Conservar unidades reales, sin simular entrega ni carga negativa |
| Cambio de unidad con avance o abandono de PROGRESO | Resolver conversión, sin inventar equivalencias |
| Estimación manual restante | Sustituir solo con autorización explícita |
| Arrastre aprobado | Reemplazar plazo solo con decisión explícita |
| Agenda histórica sin instantánea verificable | Solicitar adopción cuando el cambio de agenda lo exige |

## Pendientes protegidos

Una vencida independiente de un día anterior sin actividad ni vínculos protegidos puede adoptar determinados datos y carga configurada sin reprogramar el pasado. Conserva carga histórica, fechas, responsable y estado; una corrección nueva usa la carga configurada adoptada.

Si la conciliación pretende retirar una asignación con actividad, captura, texto, checklist o solicitud pendiente, puede elegirse conservar íntegro ese pendiente y actualizar los demás. Conserva reserva, instantánea y solicitud; no queda falsamente sincronizado. Al confirmar, la conservación queda registrada para la versión actual del catálogo. No vuelve a aparecer como diferencia pendiente ni se reacomoda como vecina mientras esa definición no cambie. Cualquier cambio posterior del catálogo, incluida una nueva revisión, vuelve a exigir revisión; no se marca la asignación como sincronizada. La decisión conserva actor, motivo y huella en el historial inmutable, sin modificar la asignación.

Ante cambios de pertenencia a procesos, la opción de conservar asignaciones mantiene compromisos existentes y aplica el proceso a nuevas ejecuciones según el flujo autorizado. No convierte pendientes independientes en etapas ficticias.

La conservación puede confirmarse aunque no haya asignaciones para actualizar o retirar. La vista previa separa las decisiones por confirmar de los cambios materiales. Tras confirmar todas las decisiones y cambios, el botón de actualización del catálogo se deshabilita y la pantalla informa que no quedan acciones pendientes. Seleccionar la casilla y revisar no guarda la excepción; requiere confirmación con motivo.

## Capacidad de las vencidas conservadas

Durante la actualización, los pendientes cuyo plazo ya venció (incluido el instante exacto del cálculo) y que no se reprograman no consumen capacidad de la nueva agenda. Conservan fechas, responsable, avances, archivos, estado pendiente e historial. La exclusión no los entrega, retira ni modifica la carga de las consultas operativas.

Una asignación vencida que entra al reacomodo sí consume capacidad en su nueva ubicación. También siguen contando los compromisos no vencidos y futuros. La vista previa informa cuántos pendientes vencidos quedan fuera del cálculo; la confirmación vuelve a comprobar el conjunto bajo bloqueo y rechaza propuestas obsoletas.

## Detalles de los conflictos

Los casos bloqueados muestran en «Ver detalles» los valores conservados por la asignación y los del catálogo actual. La comparación identifica los cambios pendientes de adopción; no demuestra que un único parámetro sea la causa del bloqueo. Cuando la asignación no tiene cambios propios, el conflicto de capacidad explica que se detectó durante el reacomodo de otras asignaciones. Los calendarios anteriores sin instantánea verificable se identifican como tales.

La búsqueda conserva el límite de 20 000 pasos. Si se interrumpe, identifica la tarea, persona y fecha exploradas como punto de interrupción, sin atribuirles una falta de capacidad no demostrada. Los candidatos fallidos no quedan como movimientos en la propuesta; no se guardan actualizaciones parciales.

Las solicitudes pendientes de arrastre o posposición ofrecen «Ir a Supervisión», hacia la pantalla principal `/validaciones`.

## Retiro y concurrencia

Retirar tareas inactivas y retirar tareas que salieron del plan son decisiones distintas. El retiro lógico preserva actividad e historia; no elimina entregas ni cierres. Desactivar una etapa pendiente puede cancelar proceso y pendientes conservando lo entregado y sus versiones.

La actualización coordina etapa, intento, reserva y asignación si existe. Las cargas calculadas por ítems y contratos documentales conservan su procedencia. La propuesta muestra movimientos directos e indirectos y se confirma con motivo, huella y clave idempotente en una transacción. Los cambios relevantes obligan a recalcular; la entrega revalida el requisito bajo bloqueo.

Relacionados: [Catálogo](catalogo.md), [Planificación](planificacion.md), [Procesos](procesos.md), [Evidencias](ejecucion-evidencias.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). El flujo de actualización, confirmación y reintento se comprobó sobre una copia aislada mediante formulario Razor real; los estados de capacidad e historial tienen regresiones de integración. La publicación compartida se contrastó por proceso, listeners y acceso HTTP. No se ejecutó una confirmación autenticada sobre los datos compartidos.

- [AssignmentRefreshService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/AssignmentRefreshService.cs)
- [AssignmentDefinition.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/AssignmentDefinition.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
