# Diagnóstico y decisiones pendientes: bono de tareas

Fecha operativa: 02/11/2026, America/Mexico_City. Alcance: análisis y propuesta, sin implementación. Cambio futuro mixto con foco backend.

## Evidencia y límites

Inspección estática del checkout master, AGENTS.md, PROTOCOLO_IMPLEMENTACION.md y ESTILO_VISUAL_V2.md. Hay modificaciones previas de planificación, resolución de conflictos, interfaz y pruebas: se conservan. El diagnóstico describe este checkout, no certifica la compilación desplegada. No se consultó la base compartida, no se verificó su distribución de registros históricos ni se ejecutaron pruebas de producto. No hubo cambios de aplicación, datos ni despliegue.

## Lo existente y lo que falta

| Área | Evidencia actual | Diferencia con el requisito |
|---|---|---|
| Clasificación | OperationalTasks.cs contiene OperationalCriticality en catálogo y asignación; OperationalScheduling.cs, OperationalAgenda, define FLEXIBLE/IMPORTANTE/CRITICA. OperationalTaskService.SaveAsync exige Dirección y audita | No hay cálculo de puntos de bono localizado. La clasificación también cambia la planificación; decidir si importancia económica y rigidez operativa deben seguir siendo una sola clasificación |
| Repeticiones y copias | OperationalPlanGenerator.GenerateAsync crea asignaciones por ocurrencia y copia definición, revisión y política. OperationalTaskService.Occasions.cs cubre extraordinarias; Sequences/ProcessPlanningService.cs y OperationalProcessService cubren etapas | Capturar puntos, recuperabilidad, día evaluado e identidad de obligación en todas las rutas; no multiplicar Repetitions otra vez sobre filas ya generadas |
| Tiempo | OperationalAgenda.Capture calcula DueAt por carga más margen; IsForDay/Priority en OperationalWorkPlan.cs alimentan Mi trabajo | DueAt no representa el final del día del bono. No sustituirlo globalmente: afecta capacidad, secuencias, agenda y estados visibles |
| Evidencia | OperationalTaskService guarda CompletedAt, CompletedBy, CompletionKind=ENTREGA y PENDIENTE_REVISION; adjuntos, fotografías y checklist tienen almacenamiento existente | Reconocer solo la entrega aceptada; borrador o archivo subido sin entrega no acreditan. NINGUNO existe y requiere decisión |
| Validación | OperationalSupervisionService.ReviewCoreAsync conserva entrega y registra ReviewedAt. Algunas conclusiones sin entrega generan CompletionKind=SUPERVISION | No interpretar cualquier CompletedAt o CONCLUIDA como evidencia válida. Separar entrega de conclusión administrativa |
| Correcciones | ReviewCoreAsync crea otra asignación con ReassignedFromId, conservando la anterior; tiene programación propia | Una corrección no debe crear automáticamente otros puntos ni reiniciar el plazo económico. Seguir cadena de intentos |
| Transferencias | OperationalSupervisionService.TransferAsync cambia PersonId y fechas de la misma fila; OperationalTransfer conserva snapshot, motivo, autor y tiempos | No atribuir todo el pasado al PersonId actual. Definir responsabilidad y descuentos anteriores al traslado |
| Posposición | OperationalCarryover.Postponement.cs: OverdueAsync, PreviewPostponementAsync y ApplyPostponement | Solo vencidas elegibles; excluye procesos, progreso y otros circuitos; busca desde mañana hasta siete días o antes de un día elegido, no una fecha exacta propuesta. La aprobación exige colocación todavía futura; entregadas quedan sin cambio. No cubre por sí sola justificación retroactiva del lunes |
| Causas y autorización | OperationalCarryover.cs guarda causa versionada, explicación, solicitante, decisión, resultados y auditoría. DecideAsync usa autorización operativa y supervisor inmediato para posponer | Reutilizar solicitud e historial; añadir fecha propuesta, exclusión autorizada y efecto explícito sobre evaluación. Aprobar movimiento de agenda no debe equivaler siempre a perdonar atraso |
| Especialidades | SpecializationService.cs modela especialidad y pertenencia de personas. OperationalSupervisionService.Query/AuthorizeAssignmentAsync filtran por roles; posposición usa jerarquía inmediata | No se encontró revisor por especialidad en estas rutas. Confirmar responsables y escalamiento antes de afirmar que ya está cubierto |
| Capacidad | OperationalScheduling.FindForward y OperationalCapacityService usan minutos, disponibilidad, horarios y elegibilidad | Reutilizar distribución. Faltan reglas económicas para cambios de ausencia posteriores, corresponsabilidad y transferencias |
| Consulta y cierre | Existen Mi trabajo, Supervisión e historial operativo; herramientas CarryoverChecks, PostponementChecks, CorrectionSchedulingChecks y CorrectionHttpChecks | Añadir cálculo semanal, cierre/versiones, resumen por colaboradora y contrato API. No se localizó un motor de bono existente |

Rutas indicadas sin prefijo están en `src/Sgol.Web/Infrastructure/Persistence/Planning/`, salvo `Organization/SpecializationService.cs`. Presentación: `Pages/MyWork/Index.Operational.cs`, `_OperationalTasks.cshtml`, `_OperationalTaskTable.cshtml`; catálogo: `Pages/Tasks/Index.cshtml`. Reusar tokens y componentes; no restaurar el límite histórico de ocho tareas de la guía visual, superado por el catálogo actual.

## Riesgos y decisiones de negocio

1. **Cierre con revisiones pendientes.** Recomendar bloquear cierre y escalar a Dirección, sin castigar automáticamente a la trabajadora. Alternativas: cierre provisional con ajuste posterior o incumplimiento automático; ambas cambian el pago/riesgo.
2. **Devolución tardía.** Evidencia del domingo rechazada el lunes no deja tiempo para corregir dentro de la semana. Elegir corte estricto o plazo excepcional desde notificación; la segunda opción modifica el requisito actual.
3. **Alcance y evidencia.** Confirmar ocasionales, etapas, mensual/anual y PROGRESO. Para ventanas largas definir día exigible y unidad puntuable (propuesta: ocurrencia/etapa exigible, nunca cada día o cada ítem automáticamente). Resolver NINGUNO: cambiar catálogo, aceptar constancia validada o excluir explícitamente.
4. **Calendario de recuperación.** El texto dice día calendario siguiente, incluso si hay descanso/ausencia. Confirmar si habrá excepción autorizada o se busca el próximo día laboral; este último contradice la literalidad. Precisar 24:00 frente a fin de turno; propuesta: medianoche local para bono, jornada para planificación.
5. **Semanas sin puntos.** A=0 no permite división. Recomendar SIN_BASE, sin factor pagable automático; Dirección debe definir si el sistema externo conserva, elimina o resuelve el componente.
6. **Transferencia tras incumplimiento.** Decidir a quién se atribuye lo ya vencido y cómo se evalúa a quien recibe. Evitar que reasignar borre descuentos o los transfiera a otra persona. Recomendación inicial: tratar transferencias justificadas antes del vencimiento como sustitución de responsabilidad; después del vencimiento exigir disposición económica explícita.
7. **Compartidas.** Definir si 10 puntos se reparten entre participantes o cada uno asume 10. Propuesta: subtareas personales verificables con carga y puntos explícitos; evitar cumplimiento colectivo que oculte responsabilidad. No repartir puntos proporcionalmente a minutos sin decisión del negocio.
8. **Reprogramación y exclusión.** Definir fecha máxima de solicitud, causas que justifican, alcance del perdón (todo atraso previo o periodo concreto), quién excluye y quién puede hacerlo después del cierre. No deducir perdón del código de causa: OMISION_RESPONSABLE y FALTA_EVIDENCIA también existen.
9. **Revisores.** Confirmar responsables por especialidad, sustitutos/escalamiento y equivalencia de Coordinación con los roles técnicos actuales. No crear un segundo esquema de permisos sin necesidad.
10. **Importancia y recuperación.** Confirmar reutilización de clasificación y quién decide recuperabilidad. Crítica no implica no recuperable. Cambiar importancia hoy también afecta la agenda; si Dirección quiere independencia, separar campos en backend y mantener editor compacto.
11. **Vigencia.** Recomendar comenzar un lunes futuro acordado, con clasificaciones completas, sin recalcular semanas históricas. Decidir tratamiento de pendientes anteriores y cambios de importancia durante una semana.
12. **Cierre y rectificaciones.** Recomendar cierre estable por persona/semana, sin cambios silenciosos. Cualquier rectificación posterior crea revisión con autor/motivo y se concilia con el consumidor externo; definir autoridad y límite temporal.

## Causas reutilizables

CarryoverCauses.All contiene: falta de tiempo operativo, alta afluencia de clientes, prioridad comercial, responsable ausente, omisión del responsable, falta de evidencia, error de asignación, dependencia externa, falla de sistema/herramienta y causa no identificada.

No hace falta duplicar el catálogo. Ausencias, fallas y dependencias ya tienen representación. Añadir una causa solo cuando un caso concreto no encaje. Una exclusión por tarea que dejó de corresponder es una decisión autorizada, no meramente otra etiqueta de causa. Contadores separados: solicitudes por persona, tareas afectadas y solicitudes pendientes; una solicitud puede incluir varias tareas.

## Modelo mínimo propuesto, condicionado a las decisiones

Mantener una aplicación y la base existente; no crear microservicio, motor de nómina, cola de descuentos ni biblioteca visual adicional.

- **Obligación evaluable estable:** identidad de la ocurrencia, vínculos a asignaciones/intentos, persona responsable según la regla acordada, día original y efectivo, semana, puntos capturados, recuperabilidad, versión de regla y disposición autorizada. Una repetición nueva es otra obligación; una corrección no lo es. Transferencias conservan periodos y decisiones de responsabilidad.
- **Extensión de solicitudes existentes:** fecha propuesta, decisión de reprogramar/excluir y alcance sobre la evaluación. Mantener auditoría, revisión optimista, intención idempotente y aplicación transaccional. Separar el reconocimiento justificado de una entrega pasada de la búsqueda de un hueco futuro.
- **Liquidación semanal:** cabecera única por persona/semana/revisión, A y D, estado, factor, fecha y autor de cierre; detalle capturado por obligación con entrega aceptada y justificación aplicada. Puede reutilizarse auditoría existente sin construir un sistema completo de eventos.
- **Calculador central:** recibe hechos y hora de referencia, devuelve descuentos y razones. Misma función para consulta provisional, cierre y pruebas. No incrementa descuentos al ejecutarse: deriva el valor actual y lo limita al valor de la obligación.

Para una obligación recuperable sin excepción: descuento 0 si entrega aceptada dentro del día; 50% durante el siguiente día o para entrega aceptada de ese día; 100% al agotar el siguiente día sin entrega aceptada. El domingo acorta la recuperación: al comenzar el lunes, toda obligación de esa semana sin entrega acreditable queda al 100%, sujeto a revisión/justificaciones pendientes. Una no recuperable pasa a 100% al finalizar su día. La entrega en revisión conserva su hora candidata y obliga a indicar provisionalidad; falta acordar presentación numérica provisional cuando aún no se sabe si será aceptada.

Una solicitud pendiente no mueve fecha ni semana: rige el vencimiento actual hasta autorización. Al autorizar traslado intersemanal antes del cierre, la misma obligación sale de la base anterior y entra una sola vez en la nueva. Las pendientes ya descontadas siguen operativamente visibles, sin volver a descontarse en semanas posteriores. Una corrección programada para después del domingo no equivale a reprogramación justificada.

A es la suma de obligaciones que corresponden a la semana completa, incluidas futuras confirmadas; estas no están realizadas ni incumplidas. Con A>0 y D=0 se muestra 100% provisional incluso sin ninguna entrega. Es consecuencia de la fórmula, no porcentaje de avance. Altas/cancelaciones autorizadas pueden cambiar A y subir/bajar el porcentaje sin borrar el descuento de otra tarea; conservar explicación interna. Nunca contar borradores de plan ni propuestas sin asignar.

Usar aritmética decimal exacta (1 punto puede perder 0.5). Para clasificar, comparar `100*(A-D)` con `95*A` y `85*A`, sin redondeo previo. Con A=0 devolver SIN_BASE. Puntos y recuperabilidad capturados no se reconstruyen consultando el catálogo actual.

Consulta provisional: cálculo acotado por persona/semana con índices. Los vencimientos se derivan del reloj aunque nadie abra la pantalla; la UI refresca al cambiar datos y al siguiente corte temporal. Un proceso de cierre idempotente verifica elegibilidad el lunes al mediodía; si estuvo apagado, recupera cierres pendientes. La regla de revisiones sin resolver debe fijarse antes de automatizar el cierre. Operaciones de entrega, autorización y cierre simultáneas se serializan o detectan por revisión, sin cierres parciales.

## Salida externa propuesta

Un único valor de negocio: `factorBonoTareas`, decimal `1`, `0.5` o `0`. SGOL conserva internamente calificación y trazabilidad; el otro sistema aplica monto, ventas y demás condiciones del bono. Esto no significa que SGOL determine el bono total.

Contrato futuro ilustrativo, no endpoint existente:

```json
{
  "personaId": "identificador-estable",
  "semanaInicio": "2026-11-02",
  "estado": "CERRADA",
  "revision": 1,
  "factorBonoTareas": 0.5
}
```

Para ABIERTA/PENDIENTE/SIN_BASE no entregar un factor listo para pago como si fuera cero. Consulta autenticada de solo lectura por persona/semana, sin evidencias privadas; consumidor registra persona+semana+revisión para no duplicar pagos y detecta rectificaciones. Fecha de pago derivable: para 2–8 de noviembre, revisión 9 de noviembre hasta 12:00 y pago sábado 14. La API puede agregarse después de estabilizar el cálculo; no se requiere integrar nómina ahora.

## Interfaz mínima futura

Mi trabajo: “Bono de tareas”, semana, velocímetro porcentual, factor expresado 100/50/0%, estado provisional/cerrado y pendientes/vencidas existentes. Texto breve “Cumplimiento de tareas”; sin pesos ni desglose principal. No confundir atraso de agenda con atraso puntuable. Reutilizar el detalle/historial para Coordinación y Dirección y sumar contadores por persona en la bandeja de solicitudes. SIN_BASE debe tener estado propio, no velocímetro engañoso en 0 o 100.

## Secuencia de implementación futura

1. Resolver decisiones anteriores, fijar fecha de vigencia y revisar en lectura un inventario de registros existentes (sin clasificación, NINGUNO, cadenas de corrección, transferencias, procesos, progreso, pendientes antiguos y solicitudes cruzando semanas).
2. Modelo y calculador con reloj inyectado; snapshots en generación, ocasionales, procesos, importación/actualización de definición y correcciones. Migración explícita de existentes, sin inferir cumplimiento ni penalizaciones históricas.
3. Adaptar posposición/exclusión y revisión; integrar cierre, atomicidad y trazabilidad. Verificar todos los productores y consumidores antes de habilitar.
4. Resumen de Mi trabajo y Supervisión; permisos backend. API externa de solo lectura cuando el contrato de cierre esté resuelto.
5. Validación aislada y luego verificación de simulación autorizada; identificar despliegue y base efectivos antes de cualquier escritura. Revisar diff separando cambios previos.

## Matriz de escenarios propuesta

Todos los casos siguientes están **propuestos**, no ejecutados. Las decisiones pendientes impiden fijar algunos esperados finales. Entorno: base aislada `sgol_task_checks_*`, reloj controlado; formularios HTTP y navegador de la aplicación real cuando se indica.

| Precondición y acción | Resultado esperado | Prueba / entorno |
|---|---|---|
| Asignar 6 repeticiones de 5 puntos | A=30; contar cada ocurrencia una vez | Generador + persistencia aislada |
| A=50, D=5 | Calificación 90; factor 0.5 | Calculador |
| Casos 85, 95 y fracciones inmediatamente inferiores | 85→0.5; 95→1; inferiores cambian rango sin redondear | Calculador |
| Flexible recuperable de 1 punto vence; entregar al día siguiente | D=0.5, no cero ni 1.5 | Reloj + entrega/revisión HTTP aislada |
| Vence segundo día sin entrega; reintentar cierre | D=puntos, nunca descuento acumulado | Integración e idempotencia |
| No recuperable vence | D=puntos al primer cambio de día | Calculador |
| Domingo asignado y no entregado; sábado entregado el domingo | Domingo D=puntos; sábado recuperable D=50% | Reloj/corte semanal |
| Entrega dominical aprobada el lunes | Usar hora de entrega, no revisión | HTTP + consulta semanal |
| Devolución lunes de evidencia domingo | Según decisión de plazo excepcional/corte estricto | HTTP; esperado pendiente |
| Solicitud domingo pendiente; aprobación lunes a otra semana | Mientras pendiente rige original; aprobación traslada una sola obligación y su base | Solicitud + revisión + dos semanas |
| Tarea entregada antes de autorizar justificación | Reconocimiento económico explícito sin inventar entrega ni movimiento de agenda | HTTP + historial |
| Correcciones sucesivas/reprogramadas después del domingo | Una obligación; no nuevos puntos ni perdón automático | Regresión de correcciones |
| Transferir después de vencer | Conservar historia y atribuir según decisión acordada | Integración; esperado pendiente |
| Exclusión, alta extraordinaria y cambio de catálogo | Base trazable; snapshots históricos intactos | Servicios reales + snapshots |
| Descanso/ausencia al asignar o posterior | No asignar indebidamente; cambio posterior según disposición acordada | Capacidad + integración |
| Progreso, etapa bloqueada, reserva futura, tarea mensual | Sin duplicación ni incumplimiento antes de exigibilidad; alcance por resolver | Procesos/planes reales aislados |
| Sin tareas o todas excluidas | SIN_BASE; no división ni pago automático supuesto | Calculador + contrato |
| Entrega, autorización y cierre concurrentes | Cierre coherente, conflicto identificable, sin pérdida de datos | Transacciones aisladas |
| Consulta repetida, reinicio tras corte, reapertura | Mismo descuento; recuperación de cierre; revisión nueva trazable | Integración/reintentos |
| Usuario ajeno cambia clasificación, decide solicitud o consulta pago | Denegado; Dirección y revisores autorizados conservan acceso | Formularios/API HTTP |
| Semana provisional con futuras y ninguna entrega | 100 si D=0 y A>0; futuras sin marcar realizadas | UI real + servicio |
| Móvil/escritorio, temas, teclado, vacío/error/carga/texto largo | Componente accesible, legible y consistente | Navegador real; no maqueta |
| Consumidor repite lectura de cierre o recibe rectificación | Clave estable y revisión; no duplicar pago | Contrato API/consumidor simulado |

El análisis estático no sustituye esta matriz ni demuestra el estado de datos compartidos. No se ejecutó compilación por tratarse de una entrega documental.
