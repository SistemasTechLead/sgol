---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-05
verificado_en: servicios-sql-y-formularios-aislados; despliegue-y-readiness-compartido
---

# Arrastres y posposiciones

## Petición y contexto

La petición conserva causa, explicación y fotografía de las tareas al solicitarla. La persona revisora debe tener alcance y nivel de supervisión requeridos. Dirección autoriza automáticamente sus nuevas peticiones propias de posposición e incidente al confirmar el envío, con cuenta, empleo y rol vigentes. La creación, aprobación y aplicación comparten una transacción: cualquier conflicto revierte el conjunto. La decisión conserva autor, fecha y motivo automático en el historial; no queda pendiente una segunda autorización en Supervisión. Las peticiones antiguas pendientes conservan su estado y pueden resolverse por el flujo existente. Los demás roles no pueden resolver sus propias solicitudes. Declarar falta de capacitación no cambia automáticamente especialidad ni acredita una inhabilitación técnica registrada.

Arrastre y posposición tienen validadores propios; no toda vencida es elegible. La ruta de posposición excluye procesos, metas progresivas y dependencias de caja no admitidas. Esa restricción evita separar trabajo de su flujo.

## Arrastre por incidente de la jornada

Para los demás roles, solicitar conserva los vencimientos hasta la decisión. Dirección aplica la autorización y los vencimientos propuestos al confirmar su propia petición. El incidente debe haber terminado, pertenecer a la jornada local actual y estar dentro de disponibilidad verificable; el descanso no se concede como incidente ni absorbe propagación. Se comprueba duración, alcance y solapamiento con otros incidentes.

La aprobación integral ajusta el vencimiento de pendientes dentro del alcance, sin mover el inicio. Las tareas entregadas o revisadas durante la espera conservan sus campos y reciben reconocimiento histórico cuando corresponde. El plazo puede superar la salida dentro del mismo día con advertencia y aprobación expresa; no extiende el turno. Se rechazan dependencias de procesos, PROGRESO, ventanas de varias jornadas y cierres que crucen de día por esta ruta.

Si cambia la estructura o el horario relevante, se rechaza la propuesta obsoleta y debe formularse otra; no se reescribe la fotografía original. Estas reglas no son las de la posposición de vencidas.

## Resolución por tarea de posposición

La bandeja permite aprobar propuesta, cambiar día, reasignar o rechazar por tarea. Cambiar día o responsable es una alternativa a aprobar lo propuesto. Cada resultado conserva su tipo; la solicitud permanece pendiente mientras tenga elementos sin resolver.

Aprobar, reprogramar o reasignar exige referencia de carga, capacidad y horario disponibles. No hereda la excepción de sobrecarga de correcciones. Las tareas mensuales permiten buscar desde el día siguiente a la solicitud hasta la misma fecha del mes siguiente, inclusive; cuando esa fecha no existe, se utiliza el último día del mes siguiente. Las demás tareas conservan siete días. Una solicitud mixta mantiene el límite de cada tarea. Los límites explícitos «antes del» siguen excluyendo el día indicado. Un horario pasado, cruce o falta de elegibilidad bloquean la decisión con su causa.

El rango mensual también se aplica al resolver solicitudes pendientes antiguas: se calcula desde la fecha de la solicitud y no desde el día de revisión. La ventana original y la fotografía enviada permanecen inmutables. Las solicitudes ya resueltas conservan sus decisiones.

Los días sin plan semanal de destino estiman demanda con el día equivalente más reciente hasta hoy. La previsión conserva la carga semanal conocida y añade reservas mensuales, ocasionales y de procesos; no supone que las semanas futuras estén vacías. Sin una referencia verificable, el día no se puede asignar.

La confirmación revalida solicitud, revisión, estructura y huella de agenda. Conserva evidencia y autoría, registra movimientos e impide duplicados. Si ya fue entregada, no se reasigna ni reprograma desde esa petición; se conserva el cumplimiento y se cierra el elemento sin cambios según el resultado permitido.

## Historial y evaluación

La historia distingue propuesta aprobada, día cambiado, reasignación y rechazo. No toda solicitud resuelta equivale a arrastre aprobado. El vencimiento original sigue disponible para explicar puntualidad.

Cambiar plazo operativo no cambia día original ni borra descuentos del [bono](bono-tareas.md). Una petición pendiente no desaparece al editar catálogo: [conservar el pendiente](actualizacion-asignaciones.md) mantiene la solicitud para resolverla normalmente.

Relacionados: [Supervisión](supervision.md), [Planificación](planificacion.md), [Historial](modelo-historial.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Rango mensual comprobado con servicios, SQL y formularios reales en base aislada, con autenticación sustituida en el host de prueba; interacción en Chromium y WebKit. Autorización automática de Dirección comprobada con servicios, SQL y formularios Razor reales en copia aislada con sesión sintética: aplicación conjunta, auditoría, reintentos concurrentes, propuesta obsoleta, reversión ante fallo y exclusión de otros roles. Chromium y WebKit comprobaron el aviso, el envío por teclado y la composición conjunta con cuenta lateral en tres tamaños y tres temas. La publicación coincide por SHA-256 con el ensamblado probado. Publicación y readiness compartidos verificados. La operación autenticada con una cuenta real en el host compartido permanece sin comprobar.

- [OperationalCarryover.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCarryover.cs)
- [OperationalCarryover.Postponement.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCarryover.Postponement.cs)
- [OperationalCarryover.Resolution.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCarryover.Resolution.cs)
- [OperationalCarryoverReader.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCarryoverReader.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
