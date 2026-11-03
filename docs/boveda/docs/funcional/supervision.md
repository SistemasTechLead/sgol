---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Supervisión, correcciones y transferencias

## Revisión

La revisión autorizada distingue aprobación, No satisfecha y devolución para corrección. Entrega y decisión tienen fechas y autores separados. Una conclusión administrativa no se atribuye a un envío del colaborador. El alcance depende del rol, persona supervisada y operación; ver una pantalla no concede todas sus acciones.

La corrección conserva el origen mediante un vínculo y nuevo intento. El anterior queda consultable y el nuevo usa requisito y carga configurada según el flujo. Revisión masiva conserva atomicidad: no deja decisiones parciales si una tarea falla.

## Programar una corrección

| Modalidad | Condición |
|---|---|
| Cuanto antes | Primer tramo permitido; admite superposición y exceso de capacidad |
| Primer espacio libre | Exige hueco entre tareas; admite exceso de capacidad diaria |
| Personalizada | Exige hueco dentro del día/rango; admite exceso de capacidad diaria |

Siempre debe caber completa la duración de carga más margen en el tramo permitido; no se recorta al fin del turno. El horizonte llega hasta el final del día local hoy+7. Se mantienen rol/especialidad, ausencias y cobertura. Puede superar el fin del plan original conservándolo como origen histórico. Cambiar disponibilidad exige revisión explícita, no movimiento silencioso. Los procesos mantienen su flujo de etapas.

## Transferir trabajo pendiente

La transferencia conserva la misma asignación, avance, borrador y evidencias. Cambia responsable y condiciones de recepción autorizadas, con recibo e historial antes/después. No crea una corrección.

Vista previa y confirmación comprueban elegibilidad, presencia, horario, capacidad y actividad concurrente. La carga consultada es la operativa actual, con reservas y pendientes según el alcance de la consulta; no reconstruye automáticamente la carga histórica previa a transferencias.

Relacionados: [Historial](modelo-historial.md), [Posposiciones](arrastres-posposiciones.md), [Bono](bono-tareas.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [OperationalSupervisionService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.cs)
- [OperationalSupervisionService.Reassignment.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Reassignment.cs)
- [OperationalSupervisionService.Transfer.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Transfer.cs)
- [OperationalSupervisionService.Batch.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalSupervisionService.Batch.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
