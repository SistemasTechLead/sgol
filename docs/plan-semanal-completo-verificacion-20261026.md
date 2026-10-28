# Plan semanal completo: búsqueda y tareas vencidas del día inicial

## Resultado final

La semana 44 se generó y guardó mediante el servicio real, exclusivamente en una copia restaurada de los datos actuales: **135 tareas del catálogo, 276 asignaciones y 42 avisos mensuales**. Las 250 asignaciones anteriores conservaron su estado completo. El reintento recuperó el mismo plan sin duplicar trabajo.

La ejecución de las 11:34 del lunes 26/10/2026 incluyó 22 asignaciones ya vencidas. La apertura de ese lunes mantuvo **09:00–09:15**, sin entrega ni revisión fabricadas. No se creó el plan de prueba en la base operativa.

## Causa del agotamiento

La reproducción anterior alcanzó 100001 estados, frente al límite de 100000, en 10,95 segundos. El orden mezclaba mensuales recuperables con trabajo semanal y la búsqueda ampliada enumeraba posiciones minuto a minuto antes de intentar las colocaciones ya disponibles. Esto disparaba retrocesos entre tareas aunque existiera una agenda válida.

La corrección:

- Dentro de cada prioridad coloca el trabajo semanal antes de las mensuales recuperables, respetando especialidades, duraciones y restricciones.
- Prueba primero las colocaciones ya calculadas. Conserva la enumeración completa como alternativa diferida cuando realmente necesita retroceder, sin duplicar opciones ya probadas.
- Mantiene la búsqueda secuencial, con cesiones periódicas para limitar la profundidad de la pila de llamadas al recorrer catálogos grandes. No se ejecutan asignaciones concurrentes sobre el mismo estado.
- Conserva los límites originales de 100000 estados y 2000000 candidatos. No modifica capacidad, turnos, duración, especialidades, tareas guardadas ni restricciones para forzar un resultado.
- Incluye tarea e índice de ocurrencia en el diagnóstico técnico si vuelve a agotarse un límite. La versión de búsqueda pasa a 5 para invalidar preparaciones anteriores.

## Regla solicitada para operaciones semanales

El usuario aclaró que el lunes deben incluirse la apertura y las otras tareas de ese lunes aunque estén vencidas. Operaciones semanales organiza el día inicial desde su comienzo, conservando las horas críticas, en lugar de sustituir la apertura pasada por un aviso o moverla a mañana.

El guardado admite horas pasadas exclusivamente en el día inicial vigente de operaciones semanales. No marca tareas como realizadas. No crea trabajo de fechas anteriores al comienzo del plan ni elimina la protección frente a preparaciones obsoletas de otros tipos de plan. Cuando se prepara a mitad de semana se mantiene el alcance desde ese día; sábado/domingo continúan preparando la semana siguiente.

La preparación y el guardado utilizan la misma regla. Se conserva el calendario automático de una apertura por fecha: no hace falta capturar fechas manuales ni repartir siete aperturas entre menos días.

## Verificación

Se realizó un respaldo nuevo de sgol_local con pg_dump -Fc, se validó el índice con pg_restore --list y se restauraron bases independientes con prefijo sgol_task_checks_full_week_. La ejecución definitiva usa **sgol_task_checks_full_week_final_20261026**.

El verificador permanente es **tools/Sgol.TaskChecks/FullWeeklyPlanChecks.cs**, opción **--full-weekly-current**. El ejecutor común rechaza bases operativas. Comprueba:

- Caso pequeño que obliga a abandonar una elección inicial y desplazar una tarea para conservar el único bloque de una tarea larga: la alternativa diferida sigue resolviendo el caso.
- Preparación de todo el catálogo real dentro del límite normal, sin devolver una agenda parcial.
- Dos preparaciones con el mismo instante producen la misma huella, aunque la búsqueda ceda periódicamente la ejecución.
- Guardado real de todas las asignaciones de la vista previa y claves de ocurrencia únicas.
- Apertura del lunes a las 09:00 y conservación de las tareas vencidas como pendientes, sin entregas ni revisiones inventadas.
- Lectura de los 42 avisos persistidos e idempotencia del reintento.
- Conservación íntegra de las 250 asignaciones originales.

Una ejecución de regresión completa generó la vista previa en **0,80 segundos** y terminó preparación, repetición, guardado, lectura y reintento en **2,54 segundos**. Una ejecución previa con el criterio final tardó 3,49 segundos en preparar y 5,65 segundos en completar el guardado. Los tiempos dependen de la ejecución; las restricciones y límites son los mismos.

La ejecución definitiva, que también incluye el caso de retroceso forzado, preparó en **0,41 segundos** y completó el ciclo en **1,99 segundos**. También pasaron las comprobaciones de apertura de lunes y de mitad de semana, las mensuales existentes, las 60 reglas de horarios y las reglas de agenda con 25 casos exhaustivos.

Los 42 avisos son mensuales: 3 de octubre sin hueco compatible y 39 de noviembre cuyo alcance dentro de esta semana termina el domingo 1. Se muestran explícitamente; el éxito de generación no equivale a afirmar que esas mensuales ya quedaron asignadas. La apertura ya no aparece entre los avisos de operaciones.

Las huellas de las 31 tablas operativas protegidas permanecieron idénticas durante las pruebas. El código se aplica al preparar planes nuevos; no se reescribieron planes, asignaciones o evidencia de la base real.

La aplicación se inició con el código final. /health/ready, /tablero y la hoja de estilos respondieron 200 por el acceso LAN. Las huellas de la fuente siguieron idénticas después de activar la versión.

Respaldo, huellas, instantáneas y logs privados: **outputs/plan-semanal-completo-20261026/**. El diagnóstico inicial y los ensayos intermedios se conservan para distinguirlos de la ejecución final.
