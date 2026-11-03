---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
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

Si la conciliación pretende retirar una asignación con actividad, captura, texto, checklist o solicitud pendiente, puede elegirse conservar íntegro ese pendiente y actualizar los demás. Conserva reserva, instantánea y solicitud; no queda falsamente sincronizado. La autorización se revisa en cada actualización.

Ante cambios de pertenencia a procesos, la opción de conservar asignaciones mantiene compromisos existentes y aplica el proceso a nuevas ejecuciones según el flujo autorizado. No convierte pendientes independientes en etapas ficticias.

## Retiro y concurrencia

Retirar tareas inactivas y retirar tareas que salieron del plan son decisiones distintas. El retiro lógico preserva actividad e historia; no elimina entregas ni cierres. Desactivar una etapa pendiente puede cancelar proceso y pendientes conservando lo entregado y sus versiones.

La actualización coordina etapa, intento, reserva y asignación si existe. Las cargas calculadas por ítems y contratos documentales conservan su procedencia. La propuesta muestra movimientos directos e indirectos y se confirma con motivo, huella y clave idempotente en una transacción. Los cambios relevantes obligan a recalcular; la entrega revalida el requisito bajo bloqueo.

Relacionados: [Catálogo](catalogo.md), [Planificación](planificacion.md), [Procesos](procesos.md), [Evidencias](ejecucion-evidencias.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [AssignmentRefreshService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/AssignmentRefreshService.cs)
- [AssignmentDefinition.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/AssignmentDefinition.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
