# Transferencia de responsabilidad desde Supervisión

En **Supervisión → Carga y reasignación** (`/supervision/carga`) se consulta la carga pendiente por colaborador. La fecha inicial es hoy; puede elegirse un día o la semana de la fecha seleccionada. Los minutos usan el mismo cálculo de capacidad que el resto de la aplicación, incluyendo atrasos pendientes y reservas de procesos. Es una consulta del estado operativo actual, no una reconstrucción histórica de la carga antes de transferencias.

Al seleccionar una tarea programada, activa, vencida o con avances se elige otro colaborador, se indican de 1 a 4,800 minutos pendientes, un vencimiento futuro de hasta 366 días y un motivo obligatorio. La vista previa muestra carga de hoy antes/después, empalmes y advertencias de sobrecarga o de insuficiencia de minutos de jornada hasta el vencimiento. Confirmar transfiere inmediatamente la responsabilidad sin aceptación del destinatario.

## Reglas

- Se mantienen los alcances jerárquicos de Supervisión. El receptor debe tener una cuenta y relación laboral activas, un rol válido o la especialidad requerida. Las personas a prueba no reciben trabajo especializado.
- La recepción exige presencia en el horario base o excepción explícita, sin ausencia ni descanso. La cobertura de cierre no autoriza una extensión de la jornada base. El vencimiento puede caer en un turno posterior; debe estar dentro de un horario válido del receptor. La tarea de cierre conserva además su límite del tramo vigente.
- Se permiten sobrecarga y empalmes mediante la confirmación explícita de la vista previa. No se reacomodan otras tareas ni se cambia la capacidad de ninguna persona.
- Se excluyen entregadas pendientes de revisión y decisiones finales. La transferencia no fabrica una entrega, aprobación o evaluación negativa.
- Se conserva el ID de la asignación, el contrato capturado, las unidades acumuladas y los registros de avance y evidencia. El responsable actual, inicio de recepción y vencimiento cambian; el recibo `operational_transfer` conserva responsable y fechas anteriores, estado completo previo, motivo y supervisor.
- `TransferRemainingMinutes` y `TransferProgressUnits` establecen una nueva base para estimar el trabajo pendiente. Ejemplo: 60/100 unidades y 30 minutos pendientes producen 30 minutos al recibir, 15 al llegar a 80 unidades y 0 al completar 100. No se vuelve a descontar el avance anterior.
- Mi trabajo muestra un historial de los últimos 100 movimientos recibidos o cedidos. El detalle muestra la autoría real de las unidades. Reportes conserva esa autoría en el texto de avance de las tareas transferidas y usa la nueva estimación para carga restante; los porcentajes de meta siguen representando avance acumulado de la tarea bajo su responsabilidad actual.
- Una etapa de proceso debe ser el intento activo y vigente de la posición activa, con dependencia satisfecha. Solo se actualiza la responsabilidad y el vencimiento efectivo de esa etapa; no se habilitan ni reprograman otras etapas.
- Las transferencias se protegen de la redistribución automática de capacidad y del reacomodo semanal. Su historial se incluye en el archivo descargable del plan y se elimina únicamente como parte de la eliminación explícita del plan.

## Transacción y concurrencia

Vista previa y confirmación usan el bloqueo operativo existente. La huella incluye tarea/revisión, carga, capacidades, horarios, ausencias, evidencia y estado del proceso. Cualquier cambio relevante obliga a generar otra vista previa. La confirmación vuelve a validar presencia y plazo y guarda tarea, recibo, evento de proceso y auditoría `OPERATIONAL_TASK_TRANSFERRED` en una misma transacción.

## Verificación del 24 de octubre de 2026

- Compilación de aplicación y `Sgol.TaskChecks` sin errores ni advertencias.
- `--transfer-lifecycle` sobre una copia restaurada con prefijo `sgol_task_checks_*`: transferencia de tarea vencida con 60/100 unidades; 30 minutos pendientes exactos; evidencia preservada; autoría; sobrecarga/empalmes; invariancia de otras asignaciones; historial; solicitudes obsoletas/repetidas; permisos; estados entregados; descanso; plazo dentro del descanso; continuación tras descanso sin contar esa hora como trabajo.
- Etapa activa de proceso: mismo intento y dependencia, siguiente etapa idéntica y rechazo si cambia el estado del proceso.
- `--capacity-rules-only` aprobado.
- Navegador autenticado en la aplicación LAN: consulta de carga, selección de una tarea real con 17/50 zapatos de Alejandra, autoría visible y vista previa hacia Dennis. No se confirmó ninguna transferencia operativa.
- Se revisaron el formulario y el resumen de confirmación en 1,440 y 390 píxeles, sin desbordamiento horizontal y con ambos botones dentro del área visible. Consulta semanal comprobada en navegador; las vistas previas se cancelaron.
- Migración aditiva `20261025001936_AddOperationalTransfers` aplicada con respaldo `pg_dump -Fc` verificado por `pg_restore --list`. Coincidieron las huellas anteriores/posteriores de 249 asignaciones, 249 entregas, 249 revisiones, 5 avances, 132 fotografías y 3 archivos.
- Evidencia de ejecución en `outputs/transferencias-20261024/`; los respaldos contienen datos privados y deben permanecer fuera de Git.
