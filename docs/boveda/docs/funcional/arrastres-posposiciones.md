---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Arrastres y posposiciones

## Petición y contexto

La petición conserva causa, explicación y fotografía de las tareas al solicitarla. La persona revisora debe tener alcance y nivel de supervisión requeridos y no resolver su propia solicitud. Declarar falta de capacitación no cambia automáticamente especialidad ni acredita una inhabilitación técnica registrada.

Arrastre y posposición tienen validadores propios; no toda vencida es elegible. La ruta de posposición excluye procesos, metas progresivas y dependencias de caja no admitidas. Esa restricción evita separar trabajo de su flujo.

## Arrastre por incidente de la jornada

Solicitar conserva los vencimientos hasta la decisión. El incidente debe haber terminado, pertenecer a la jornada local actual y estar dentro de disponibilidad verificable; el descanso no se concede como incidente ni absorbe propagación. Se comprueba duración, alcance y solapamiento con otros incidentes.

La aprobación integral ajusta el vencimiento de pendientes dentro del alcance, sin mover el inicio. Las tareas entregadas o revisadas durante la espera conservan sus campos y reciben reconocimiento histórico cuando corresponde. El plazo puede superar la salida dentro del mismo día con advertencia y aprobación expresa; no extiende el turno. Se rechazan dependencias de procesos, PROGRESO, ventanas de varias jornadas y cierres que crucen de día por esta ruta.

Si cambia la estructura o el horario relevante, se rechaza la propuesta obsoleta y debe formularse otra; no se reescribe la fotografía original. Estas reglas no son las de la posposición de vencidas.

## Resolución por tarea de posposición

La bandeja permite aprobar propuesta, cambiar día, reasignar o rechazar por tarea. Cambiar día o responsable es una alternativa a aprobar lo propuesto. Cada resultado conserva su tipo; la solicitud permanece pendiente mientras tenga elementos sin resolver.

Aprobar, reprogramar o reasignar exige referencia de carga, capacidad y horario disponibles. No hereda la excepción de sobrecarga de correcciones. El día debe estar dentro de la ventana y límites exclusivos solicitados. Un horario pasado, cruce o falta de elegibilidad bloquean la decisión con su causa.

La confirmación revalida solicitud, revisión, estructura y huella de agenda. Conserva evidencia y autoría, registra movimientos e impide duplicados. Si ya fue entregada, no se reasigna ni reprograma desde esa petición; se conserva el cumplimiento y se cierra el elemento sin cambios según el resultado permitido.

## Historial y evaluación

La historia distingue propuesta aprobada, día cambiado, reasignación y rechazo. No toda solicitud resuelta equivale a arrastre aprobado. El vencimiento original sigue disponible para explicar puntualidad.

Cambiar plazo operativo no cambia día original ni borra descuentos del [bono](bono-tareas.md). Una petición pendiente no desaparece al editar catálogo: [conservar el pendiente](actualizacion-asignaciones.md) mantiene la solicitud para resolverla normalmente.

Relacionados: [Supervisión](supervision.md), [Planificación](planificacion.md), [Historial](modelo-historial.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [OperationalCarryover.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCarryover.cs)
- [OperationalCarryover.Postponement.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCarryover.Postponement.cs)
- [OperationalCarryover.Resolution.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCarryover.Resolution.cs)
- [OperationalCarryoverReader.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCarryoverReader.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
