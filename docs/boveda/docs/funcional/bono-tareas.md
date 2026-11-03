---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Bono de tareas

## Qué calcula

Evalúa exclusivamente el componente de tareas, no ventas, dinero ni bono total. La primera semana vigente inicia el 02/11/2026. La zona operativa es America/Mexico_City.

Cada obligación tiene identidad, persona, día original, clasificación y puntos capturados. Flexible vale 1, Importante 5 y Crítica 10 puntos por repetición. No se duplica trabajo por correcciones, consultas ni por etapa y proceso completo.

Calificación = 100 × (puntos evaluables − puntos descontados) / puntos evaluables. Desde 95% conserva 100% del componente; desde 85% y menos de 95%, 50%; por debajo de 85%, 0%. Se usan valores reales sin redondeo previo.

## Tiempo y revisión

Se evalúa el día original, no la hora del vencimiento operativo. Una Flexible enviada durante el día siguiente conserva descuento del 50%; sin envío válido al terminar recuperación pierde el 100%. Importantes y Críticas pierden el 100% si no cumplen dentro del día. La recuperación nunca supera el fin del domingo.

El envío fija provisionalmente la fecha; aprobar después no vuelve tardía la entrega. Una devolución sin corrección válida o No satisfecha antes del corte descuentan el valor de la obligación. La conclusión administrativa tiene procedencia propia.

La ejecución cierra al terminar el domingo. La revisión cierra el lunes siguiente a las 12:00. Los eventos exactamente a las 12:00 quedan fuera del cierre. Una entrega aún en revisión conserva el resultado provisional; no equivale a aprobación operativa. Decisiones posteriores no reescriben el bono cerrado. Entregar el lunes no recupera la semana anterior.

## Base, excepciones y estados

Las etapas no disponibles no generan automáticamente incumplimiento al siguiente responsable. La procedencia de transferencias y correcciones evita trasladar sin explicación atraso a otra persona. Los arrastres conservan día original y descuentos.

Una cancelación temprana sin descuento puede excluir la obligación; no borra automáticamente una penalización generada. Las exclusiones y excepciones expresas de Dirección tienen semántica distinta: excluir quita base; perdonar conserva base sin descuento.

Sin base y con ausencia de trabajo acreditada aparece Sin tareas evaluables y bono de tareas 0%, sin inventar calificación. Si trabajó sin base, falta acreditar trabajo o existen datos inconsistentes, aparece Incidencia y no se convierte el resultado en cero. Las semanas previas a vigencia se distinguen como No vigente.

## Presentación

El velocímetro es calificación, no avance operativo. Mi trabajo presenta semana actual; supervisión dispone de consultas autorizadas. El modo pantalla no expone este indicador personal. La persistencia conserva hechos y cierres y no depende de contar adjuntos.

Relacionados: [Ejecución](ejecucion-evidencias.md), [Supervisión](supervision.md), [Historial](modelo-historial.md), [Interfaces](interfaces.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [TaskBonusCalculator.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskBonusCalculator.cs)
- [TaskBonusService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskBonusService.cs)
- [TaskBonusPersistence.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskBonusPersistence.cs)
- [TaskBonusCommitInterceptor.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskBonusCommitInterceptor.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
