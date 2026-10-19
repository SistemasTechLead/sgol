# Simplificación de horarios y cobertura — 15 de octubre de 2026

Esta decisión posterior del usuario sustituye las políticas temporales, la rotación automática y la restricción dominical del plan original. Los horarios personales, excepciones, capacidad, cualificaciones, permisos y recorridos especializados de Procesos continúan vigentes.

## Comportamiento entregado

- **Tareas:** eliminados del modelo activo, formularios y asignación `ExecutionPolicy`, `Splittable`, `ClosingGraceMinutes` y `DayStartMinutesJson`. No se clasifica una tarea como fija/flexible ni se calcula si su duración cabe en un tramo o si permite fraccionamiento.
- **Horario dominical:** un entero nullable `SundayStartMinute`, editado mediante una hora opcional. Solo sustituye la activación del domingo. Null conserva la hora base. No duplica tareas ni crea una lista especial de operaciones permitidas. Días, periodicidad, carga, plazo, requisitos y pertenencia al maestro semanal siguen sus reglas habituales.
- **Asignación:** una comprobación común de presencia personal en el instante de activación y, si se recibe después, también en el instante de recepción, junto con los controles existentes de rol, especialización, disponibilidad y capacidad. No exige que la carga estimada quepa dentro del tramo. El vencimiento solo es margen de terminación: no permite esperar una entrada posterior, comida o descanso. La carga no tiene que caber antes de salir. No se mueven la activación ni el vencimiento para hacer elegible a una persona.
- **Planificación → Cobertura de cierre:** tabla compartida por fecha, con siete días consultables. Propone a quien llega a las 20:00 (14:00 el domingo), conservando la exclusión del Director. Con los horarios reales propone Mariana, salvo miércoles, que queda sin cobertura automática.
- La elección añade una versión compartida y extiende una hora el último tramo de esa fecha, hasta el límite operativo de las 21:00. No recorta horarios que ya terminen más tarde. Un descanso o ausencia explícitos no se convierten en jornada laboral. Se conservan los horarios semanales y los miércoles siguientes; no hay ciclo automático.
- Con Mariana, el horario efectivo de cierre alcanza las 21:00, o 15:00 el domingo. Para una persona que sale a las 19:00, elegirla el miércoles extiende su salida a las 20:00. La operación de cierre puede terminar hasta las 21:00; ese límite pertenece a la cobertura compartida y no es un parámetro de política de cada tarea. Las entregas conservan el comportamiento existente ante atraso.
- El cierre compartido referencia TAR-0200 por ID verificado en la configuración de esta instalación. No usa coincidencias de nombres. Rol y especialización siguen aplicándose también a esa persona elegida.
- **Personas:** permanece Horario laboral y Excepciones por fecha. Se retiraron la edición de rotación, la consulta de cobertura y la casilla de cierre dentro de las excepciones personales.

## Migración y conservación

Migración aplicada: `20261015235846_SimplifyTaskSchedules`; 59 migraciones en la instalación local; modelo sin cambios pendientes.

La migración copia la configuración retirada de las 28 tareas a `retired_task_time_configuration` antes de quitar las cuatro columnas del catálogo. Extrae el domingo del JSON anterior hacia el campo nullable. Las instantáneas antiguas de asignaciones, versiones publicadas, rotaciones y auditoría se conservan; ya no gobiernan la política temporal de una tarea nueva. No se generan nuevas instantáneas de política de tarea.

Horarios dominicales conservados: TAR-0200 a las 13:50; TAR-0201 y TAR-0224 a las 10:00. Son las dos barridas de zonas distintas confirmadas por el usuario, sin duplicación de catálogo.

Se corrigió además el `search_path` de `sgol_work_week_valid`: al restaurar un dump, PostgreSQL usa un camino de búsqueda vacío y la función anterior no encontraba su validador de tramos. El fallo se detectó y reparó en la copia; una nueva restauración completa conservó las cinco versiones personales.

Respaldo inmediato: [pre-simple-deploy.dump](../../outputs/horarios/pre-simple-deploy.dump). Inventarios [anterior](../../outputs/horarios/simple-shared-before.json) y [posterior](../../outputs/horarios/simple-shared-after.json); [comparación](../../outputs/horarios/simple-preservation.json). Solo cambian migraciones, auditoría y estado de configuración; aparece el archivo histórico de configuración retirada. La repetición de la configuración deja el inventario idéntico.

**Diferencia frente al reporte anterior:** antes de esta simplificación, la base local ya tenía cero planes/asignaciones/entregas/revisiones. Existe un evento `DIRECTION_PLAN_DELETE` del 15/10/2026 a las 17:41:45 de Ciudad de México, anterior al inventario y la migración de esta ejecución. No se restauraron ni recrearon esos datos en la instalación compartida.

Como prueba adicional, se restauró el respaldo anterior que sí contiene 73 asignaciones en una base aislada y se migró hasta el esquema nuevo: se conservaron las **73 asignaciones, 73 entregas, 73 revisiones, cinco personas/cuentas y el plan**, con huellas iguales. [Comparación histórica](../../outputs/horarios/simple-legacy-preservation.json).

## Corrección de elegibilidad por activación · 16/10/2026

La política común exige `entrada <= activación < salida`. No busca tramos laborales posteriores dentro del plazo, ni permite esperar al turno siguiente, al regreso de comida o a otro día. Si una asignación se recibe tarde, la persona debe estar en turno también en ese momento. El plazo sigue siendo margen de terminación; no se desplazan inicio ni vencimiento ni se exige que la carga quepa en el tramo. Se conservan capacidad, roles, especializaciones, ausencias y cobertura de cierre.

El contexto incluye una versión de la política para invalidar previsualizaciones anteriores al cambio. Las asignaciones guardadas conservan su responsable, fechas y evidencia histórica. No se ejecutó una reasignación masiva.

Validación: 55 comprobaciones de reglas (incluido Mariana 09:00–17:00 frente a entrada 11:00), 36 de integración, distribución semanal y 388 de regresión general aprobadas en bases aisladas; compilación sin errores ni advertencias. La integración comprueba rechazo manual y automático de activaciones fuera de turno, margen largo, recepción tardía, redistribución y reserva/activación de Procesos. Los informes de verificación anteriores se conservan como evidencia histórica.

## Apertura dominical y aviso de conflictos · 16/10/2026

El diagnóstico del catálogo real encontró una sola fecha sin candidato: TAR-0062 el domingo 18/10, activación 09:00 y Mariana entrando a las 10:00. El usuario confirmó activarla el domingo a las 10:00. Se guardó `SundayStartMinute=600` mediante el servicio normal de catálogo, con auditoría y revisión 2→3; la hora base sigue siendo 09:00. Respaldo comprobado: `outputs/horarios/apertura-domingo-20261016/before.dump`.

En la copia restaurada se generó el plan completo de 26 asignaciones: las activaciones de las 09:00 quedaron entre Dennis y Alejandra; las tres del domingo a las 10:00 y el cierre a las 13:50 quedaron con Mariana. El diagnóstico posterior en la instalación real no presenta fechas sin candidatos. Huellas de las otras tareas, planes y asignaciones idénticas antes/después; no se guardó el plan de prueba en la base compartida.

El aviso de confirmación ahora enumera únicamente las fechas/tareas sin candidatos e incluye su activación. La tabla detallada también muestra la hora de activación. La lista completa de tareas seleccionadas se conserva como contenido del plan. Compilación sin errores ni advertencias y servicio HTTP 200. Evidencia en `outputs/horarios/apertura-domingo-20261016/`.

## Equilibrio diario final · 16/10/2026

La carga diaria tiene prioridad sobre la rotación de personas distintas; esta continúa como desempate. Al terminar la generación se revisan los responsables de las tareas nuevas de cada día: primero se intenta colocar las de menos candidatos y después se admiten movimientos o intercambios que reduzcan la desigualdad. La comparación considera la capacidad configurada de cada persona. Se validan activación, recepción cuando corresponde, rol, especialización, ausencia y capacidad. No se cambian las fechas, la carga, las repeticiones ni las tareas guardadas. Las metas conservan su atribución habitual y su carga cuenta en la comparación diaria.

Con copia del plan real, el sábado 17/10 pasa de Alejandra 60, Dennis 50 y Mariana 15 minutos a **40, 40 y 45**. Cuatro cambios de responsable permiten ese resultado, sin adelantar entradas ni mover activaciones. El domingo queda con Mariana y 45 minutos. La simulación está en `outputs/horarios/equilibrio-final-20261016/current-plan-simulation.json`.

Se regeneró además el maestro completo en una base aislada con 26 asignaciones: viernes 40/40/40, sábado 40/40/45 y domingo Mariana 45. Evidencia: `outputs/horarios/equilibrio-final-20261016/complete-generation.log`. El plan de la instalación compartida no se modificó; la regla aplica a nuevas generaciones. La huella de previsualización incluye la versión de esta política para evitar guardar confirmaciones del reparto anterior.

Comprobaciones aprobadas: 11 específicas del balance (caso real, estabilidad, turno, domingo, datos guardados, capacidad, ausencias, rol y especialización), regresión general de 388 comprobaciones, distribución semanal, 36 de integración de horarios y rotación. Compilación sin errores ni advertencias; servicio HTTP 200.

## Verificaciones anteriores

Los conteos siguientes pertenecen a conjuntos que comparten algunas comprobaciones; no deben sumarse como casos independientes.

| Comprobación | Resultado y evidencia |
| --- | --- |
| Build de entrega | Sin errores ni advertencias; [log](../../outputs/horarios/simple-deployed-build.log) |
| Reglas simplificadas | 33 aprobadas; [log](../../outputs/horarios/simple-rules.log) |
| Integración de horarios y asignación | 23 aprobadas; [log final](../../outputs/horarios/simple-integration-final.log) |
| Regresión de tareas | 381 comprobaciones PASS; [log](../../outputs/horarios/simple-full.log) |
| Regresión de Procesos | 127 comprobaciones PASS; [log](../../outputs/horarios/simple-process.log) |
| Perfiles y formularios HTTP | 68 aprobadas; [log](../../outputs/horarios/simple-profiles.log) |
| Tablero | 100 aprobadas; [log](../../outputs/horarios/simple-board.log) |
| Accesos por rol | Cinco grupos aprobados; [log](../../outputs/horarios/simple-roles.log) |
| Modelo EF | Sin cambios pendientes; [log](../../outputs/horarios/simple-model.log) |
| Conservación con datos históricos | Huellas iguales de 73 asignaciones/entregas/revisiones; [comparación](../../outputs/horarios/simple-legacy-preservation.json) |
| Servicio real | `/health/ready` HTTP 200; [respuesta](../../outputs/horarios/simple-health.json). JS servido coincide con el archivo fuente |

La integración recorre plan normal y maestro semanal, vista previa y guardado, idempotencia, cambios de horario/excepción, elección de cobertura, invalidación de previsualizaciones por cambio de cobertura, domingo mediante el generador normal, ocasionales manuales/automáticas, Supervisión, redistribución, reservas, activación y reasignación de Procesos. Una ausencia posterior a la reserva conserva la etapa pendiente y a su responsable reservado; no completa ni sustituye por una vía genérica.

En navegador y base aislada se guardó Dennis para el miércoles 21/10: horario resultante 09:00–13:00 / 15:00–20:00. Se guardó y volvió a leer Horario dominical 14:00, luego vacío y finalmente 13:50. Desmarcar domingo oculta y deshabilita el campo. No se creó trabajo ni cobertura de prueba en la base compartida.

Se midieron Tareas, Personas y cobertura en **320, 390, 768 y 1280 px**, claro y oscuro: 24 estados sin desbordamiento de página. Se conservan los desplazamientos internos deliberados de tablas. También se verificó apariencia Móvil guardada en ventana de 1280 px. [Métricas](../../outputs/horarios/simple-ui-metrics.json), [Tareas](../../outputs/horarios/simple-task-form.png), [cobertura con horario resultante](../../outputs/horarios/simple-closing-final.png). La apariencia y el viewport temporales fueron restaurados; navegador y servidor de prueba cerrados.

## Pendientes y operación

- Elegir la cobertura real de cada miércoles en Planificación. Esta ejecución no decide por el usuario quién trabajará esos días.
- Las incompatibilidades de rol de Alejandra y Viri con la tarea actual de cierre continúan vigentes. Se dejó su resolución posterior por especializaciones conforme a la instrucción del usuario; no se alteraron roles ni permisos.
- Los calendarios normales pueden generar otros domingos. Ya no están bloqueados por una lista especial, pero deben tener horario personal disponible y capacidad. Por ejemplo, TAR-0336 conserva su hora base 15:00: si cae en domingo, debe revisarse su Horario dominical, pues coincide con el fin de la jornada efectiva. No se cambiaron tareas ajenas en lote.
- El horario del Director en domingo sigue sin configurar. No impide los tres trabajos dominicales confirmados para Mariana.
- No se verificaron dispositivo físico ni zoom real al 200 %. Las mediciones anteriores corresponden a viewport de navegador.

El lanzador local es `scripts/ejecutar-horarios-local.ps1`, con binario `.local/horarios-sunday-visibility/Sgol.Web.dll`, raíz de contenido `src/Sgol.Web` y backend `127.0.0.1:7241`. Se mantiene la entrada HTTPS local/LAN existente. Logs de ejecución: `outputs/horarios/simple-web.log` y `simple-web.error.log`.

Recuperación: conservar el dump antes de cambios y probarlo primero en otra base. El downgrade se protege si hay horas dominicales o cierre compartido configurados. El binario anterior requiere las columnas retiradas: no basta con cambiar el lanzador hacia atrás. No restaurar sobre la instalación compartida sin revisar las diferencias y preservar cualquier trabajo nuevo.

## Ajuste de presentación: cobertura en diálogo

Cobertura de cierre ahora es un botón de la misma fila de acciones de Planificación, con separación automática hasta el extremo derecho. Abre un diálogo nativo de tamaño acotado (máximo 840 × 640 px), con scroll interno, cierre mediante botón/Escape y devolución de foco al disparador. La tabla ya no ocupa espacio debajo de los planes activos. Consultar fechas, guardar o mostrar un error vuelve a abrir el diálogo mediante el comportamiento compartido existente; no se cambian reglas de asignación ni datos.

Build sin errores ni advertencias: [log](../../outputs/horarios/closing-dialog-build.log). Navegador aislado: alineación en escritorio a la misma coordenada vertical, foco inicial en Cerrar, Escape y retorno al botón, reapertura tras consultar 28/10, tamaños 320/390/768/1280 sin overflow de página y tema oscuro. [Métricas](../../outputs/horarios/closing-dialog-metrics.json), [escritorio](../../outputs/horarios/closing-dialog-desktop.png), [móvil](../../outputs/horarios/closing-dialog-mobile.png), [oscuro](../../outputs/horarios/closing-dialog-dark.png). Sin cambios de esquema ni escrituras de cobertura en la base compartida.

## Restauración de la tabla de cargas por día

Se retiró de «Asignar cargas de trabajo» la tabla adicional de horarios/elegibilidad. El diálogo conserva únicamente la tabla existente con días del periodo, porcentajes editables por persona y total. No se modificaron el promedio, los máximos, la huella de concurrencia, el contrato de ajustes ni la asignación del servidor. La consulta de horarios en la confirmación del plan continúa disponible.

Verificación en navegador aislado para la semana 43: siete columnas diarias; valores iniciales al 100%; cambiar lunes a 0% y martes a 50% produce total 79%; Usar estos ajustes cierra el diálogo e informa un ajuste preparado; al reabrir conserva 0/50 y total 79%. Un valor 101 deshabilita aplicar. En 390 px no hay overflow de página; la tabla conserva su scroll interno. No se guardó un plan ni capacidad de prueba en la instalación compartida. [Evidencia de valores](../../outputs/horarios/capacity-restored-proof.json), [escritorio](../../outputs/horarios/capacity-restored-desktop.png), [móvil](../../outputs/horarios/capacity-restored-mobile.png). `node --check` aprobado y JS servido idéntico al fuente; el ajuste está disponible en la instancia local sin reiniciar binarios.

## Visibilidad condicional de Horario dominical

El campo solo se muestra cuando la tarea tiene periodicidad semanal y domingo seleccionado en Días fijos, o calendario de Repeticiones distribuidas con exactamente siete repeticiones. Se evalúa al renderizar Razor y al cambiar días, calendario, periodicidad o cantidad. Mientras no corresponde se oculta y deshabilita; no agrega validación obligatoria ni cambia la asignación del servidor.

Build sin errores ni advertencias y `node --check` aprobado. Ocho casos verificados en navegador aislado: nueva semanal/1 oculta; semanal/7 visible; semanal/6 oculta; días fijos lunes oculta; lunes+domingo visible; domingo desmarcado oculta; mensual/7 oculta; ocasional oculta. En todos los casos el estado disabled acompaña a hidden. No se guardaron tareas de prueba ni se modificaron datos de la instalación compartida. [Resultados](../../outputs/horarios/sunday-visibility-cases.json), [captura](../../outputs/horarios/sunday-visibility-selected.png), [build](../../outputs/horarios/sunday-visibility-build.log).
