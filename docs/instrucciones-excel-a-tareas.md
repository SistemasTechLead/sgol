# Instrucciones para Codex: convertir el Excel de lógica de tareas a un MD de carga masiva SGOL

## Solicitud

Usa el Excel que se ha desarrollado en este mismo chat de Codex, sus archivos disponibles y las aclaraciones previas de su propietario. Se espera que ese trabajo ya esté en el contexto del chat; no supongas que se adjuntará un Excel nuevo junto con este MD. Devuelve **`tareas-para-carga-masiva.md`**, en español, con los parámetros que realmente puedas recuperar para las 202 tareas del catálogo incluido al final. El resultado se entregará al Codex que trabaja en SistemaSGOL para preparar su alta masiva en el catálogo **Tareas (`/tareas`)**.

La prioridad es determinar **si la información disponible alcanza para llenar estos parámetros** y mostrar con precisión todo lo que falta. No se da por hecho que esta acción sea útil ni que el Excel contenga la lógica necesaria. Si es imposible recuperar la información, o el Excel no permite completar las tareas, dilo directamente, explica por qué y entrega el diagnóstico y los datos parciales verificables. No produzcas una apariencia de catálogo completo rellenando huecos con suposiciones.

Este archivo contiene el contrato del formulario vigente, verificado contra el código el 5 de octubre de 2026. No necesitas acceso al proyecto para analizar el Excel. El MD resultante será un insumo estructurado para el Codex receptor; **no existe un importador de MD acreditado por estas instrucciones**. No afirmes que el archivo ya se cargó ni crees planes, asignaciones, entregas o evidencias.

## Cómo analizar el Excel

Antes de analizar, identifica el archivo o versión más reciente que puedas verificar, las hojas disponibles y las decisiones del propietario que siguen vigentes en el contexto accesible. Distingue el Excel real, fragmentos citados, especificaciones conversadas, propuestas de Codex y decisiones confirmadas. Haber desarrollado el Excel en este chat no garantiza que el archivo o todo el historial sean accesibles ahora. No afirmes haber leído un archivo, hoja o conversación que no puedes consultar.

Si el archivo ya es accesible, úsalo sin pedir que lo adjunten otra vez. Si solo tienes fragmentos o aclaraciones del chat, trabaja con ellos identificando ese alcance y explica qué no puedes verificar del Excel. Solicita el archivo o una aclaración únicamente si necesitas recuperar una fuente que no está accesible; continúa con la evidencia disponible. No interpretes inaccesibilidad como ausencia comprobada de un dato en el Excel.

1. Lee todas las hojas relevantes, encabezados, tablas, fórmulas, resultados, notas, leyendas, filas y columnas ocultas. Usa la habilidad de hojas de cálculo disponible en tu entorno. Si no puedes interpretar algún elemento, reporta esa limitación. No ejecutes macros.
2. Identifica dónde se define cada tarea, su responsable, frecuencia, calendario, evidencia, tiempo de ejecución y plazo. Distingue duración del trabajo, plazo de entrega, frecuencia y cantidad objetivo: son conceptos diferentes.
3. Cruza primero por código `TAR-####`; si no hay código, por nombre y contexto. Conserva exactamente los códigos y nombres del catálogo de este documento. Documenta coincidencias por nombre, discrepancias, duplicados y tareas adicionales del Excel.
4. Usa las reglas explícitas del Excel y las aclaraciones de su propietario. No deduzcas parámetros operativos solamente por el título, color, cargo aparente o ejemplos de este documento. Un título como «Día 20» o «8:00» requiere comprobar su significado y compatibilidad.
5. Conserva la referencia de origen de cada decisión: archivo, hoja, celdas/rango y una explicación breve. Si una fórmula determina el dato, registra la fórmula y el resultado utilizado; indica resultados no calculados o potencialmente desactualizados. Para aclaraciones del chat, indica el mensaje o fecha si son accesibles y la decisión confirmada, sin inventar referencias de celdas. Una propuesta previa de Codex no constituye una regla confirmada por el propietario.
6. Si falta información indispensable, pregunta al propietario del Excel agrupando preguntas comunes. Continúa con las otras tareas. Si no recibes respuesta, entrega el archivo completo con pendientes; nunca inventes respuestas.
7. Una definición representa una configuración compartida. No multipliques registros por empleado, semana o repetición. Si una tarea tiene configuraciones distintas por rol o frecuencia, registra el conflicto y las variantes propuestas fuera de la lista lista para cargar, hasta que el propietario confirme cómo separarlas.
8. El catálogo suministrado es el alcance: las tareas adicionales del Excel se reportan aparte y no se incorporan automáticamente. Deben quedar contabilizadas las 202 tareas, incluso las no encontradas o incompatibles.

## Comunicar limitaciones durante el llenado

No guardes los problemas para el resultado final. Comunica al propietario las limitaciones conforme las encuentres, con actualizaciones breves por hoja o grupo de tareas. Agrupa carencias repetidas y enumera los códigos afectados; evita 202 mensajes separados y no repitas avisos sin novedades.

Cada actualización debe explicar qué revisaste, qué parámetros sí están respaldados, cuáles faltan o no se pueden verificar, qué tareas quedan bloqueadas y cuál es la consecuencia para su alta. Por ejemplo: «En la hoja revisada encontré responsables y frecuencia para estas tareas, pero no carga en minutos ni plazo de entrega; todavía no pueden entrar al lote». Este ejemplo describe el formato del aviso, no un hallazgo real.

Clasifica las limitaciones por campo y tarea usando estos motivos (pueden coexistir):

| Motivo | Cuándo usarlo |
| --- | --- |
| `AUSENTE_EN_EXCEL` | Revisaste las fuentes relevantes accesibles y el dato no aparece. Indica las hojas/rangos revisados. |
| `FUENTE_NO_ACCESIBLE` | No puedes abrir el archivo, recuperar una hoja, una versión o el contexto previo necesario. No permite afirmar que el dato no existe. |
| `AMBIGUO` | El texto admite varias interpretaciones, falta unidad o no distingue responsable, supervisor, duración, plazo o frecuencia. |
| `CONTRADICTORIO` | Hay versiones, celdas o aclaraciones incompatibles y no puedes determinar cuál rige. |
| `SOLO_PROPUESTA_NO_CONFIRMADA` | El valor aparece como sugerencia de Codex o borrador, sin aprobación verificable del propietario. |
| `NO_REPRESENTABLE_EN_SGOL` | La regla existe en la fuente, pero el formulario actual no puede expresarla. |
| `FUERA_DE_LIMITES` | El dato está respaldado, pero incumple los tipos o rangos admitidos. |
| `NO_APLICA` | El campo no tiene efecto en esa modalidad; documenta la normalización técnica. No lo cuentes como información faltante. |

No confundas estos motivos con los estados excluyentes de preparación. Una tarea `PENDIENTE` puede tener varios motivos; `NO_ENCONTRADA` solo se usa cuando la búsqueda en fuentes accesibles sustenta que no se encontró una correspondencia. Si no hay acceso suficiente para buscarla, usa `PENDIENTE` con `FUENTE_NO_ACCESIBLE`.

Si detectas una carencia general que impide la carga (por ejemplo, no hay tiempos de esfuerzo ni plazos para ninguna tarea revisada), adviértelo en cuanto lo compruebes. Explica que el Excel puede servir como inventario o guía de procesos y aun así ser insuficiente para crear tareas en SGOL. No sugieras que extraer más filas solucionará parámetros que la fuente no define. Continúa el inventario de carencias sin fabricar valores, y no declares imposible toda la extracción si aún hay información verificable que rescatar.

Al terminar, expresa uno de estos diagnósticos y justifícalo con los conteos: **suficiente para carga completa**, **útil para carga parcial**, **insuficiente para cualquier alta completa** o **no evaluable por falta de acceso**. Si `TareasListas` queda vacío, dilo expresamente. No fuerces un diagnóstico negativo por la expectativa del solicitante ni positivo por cumplir la petición: informa el resultado de la revisión.

## Todos los parámetros actuales de creación

Los nombres técnicos sirven para el bloque JSON del resultado. Valores enumerados sin acentos y exactamente como aparecen aquí. Los enteros no admiten decimales; los booleanos son `true` o `false`.

| Campo del formulario | Clave técnica | Valores y validación | Interpretación |
| --- | --- | --- | --- |
| Nombre | `Name` | Texto de 1 a 500 caracteres, sin caracteres de control, sin espacios externos | Nombre exacto del catálogo. Si requiere corrección, reportarla y solicitar aclaración. |
| Rol asignado | `AssignedRole` | `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION`, `PISO_VENTAS` | Rol ejecutor, no quien autoriza o supervisa. Un único rol por definición. |
| Requisito para terminar | `Requirement` | `FOTOGRAFIA`, `COMENTARIO`, `DOCUMENTO`, `NINGUNO` | Un único requisito de cierre. Fotografía JPG/PNG; documento PDF. |
| Periodicidad / tareas predefinidas | `PeriodType` | `SEMANAL`, `MENSUAL`, `TRIMESTRAL`, `ANUAL` | Ciclo de generación; no confundir con plazo o fechas del plan. |
| Repeticiones por periodo | `Repetitions` | Entero de 1 a 100 | En distribuida: número de ejecuciones por ciclo. En días fijos: cantidad de días seleccionados. En progreso: siempre 1. |
| Calendario de ejecución | `ScheduleMode` | `DISTRIBUIDA`, `DIAS_FIJOS`, `PROGRESO` | Consulta las reglas siguientes. |
| Días de ejecución | `WeekDays` | Entero de 0 a 127, máscara de días | Para días fijos, selecciona al menos un día. Incluye también `InputDays` en el resultado para facilitar la captura. |
| Hora de activación · Ciudad de México | `StartHour` | Entero de 0 a 23 | Apertura a la hora exacta, sin minutos, zona `America/Mexico_City`. No es automáticamente la hora límite. |
| Personas distintas | `DifferentPeople` | Booleano | Exige personas distintas para repeticiones de la misma tarea y semana. No equivale a asignar a todo el equipo. No permitido en progreso. |
| Plazo para terminar | `DeadlineHours` | Entero de 1 a 8760 | Horas transcurridas desde la apertura. En progreso se almacena y valida, pero el vencimiento lo determina el cierre semanal. |
| Carga por repetición | `LoadMinutes` | Entero de 1 a 4800 | Minutos de esfuerzo por ejecución; en progreso, esfuerzo de la meta semanal completa. No confundir con horas disponibles para entregarla. |
| Meta semanal | `GoalUnits` | Entero de 1 a 1000000 | Cantidad objetivo en progreso. También se almacena y valida en otras modalidades aunque no se usa para su ejecución. |
| Unidad de progreso | `ProgressUnit` | Texto de 1 a 60 caracteres, sin espacios externos | Ejemplo: `pares revisados`. También se almacena y valida en otras modalidades. |
| Activa para nuevos planes | `Active` | Booleano | Determina si estará disponible para nuevos planes. No usar inactividad para disimular una incompatibilidad o un dato faltante. |

### Calendarios y combinaciones

- **`DISTRIBUIDA`**: admite los cuatro ciclos. La semana empieza el lunes; los otros ciclos el primer día del mes, trimestre natural (enero, abril, julio, octubre) o año. Para un ciclo de `D` días y `R` repeticiones, la repetición `i`, desde cero, cae en el día inicial más `floor(i × D / R)` días. Dos ejecuciones semanales caen lunes y jueves. Una mensual cae el día 1; una anual, el 1 de enero. Si hay más repeticiones que días, puede haber varias el mismo día. Para esta modalidad normaliza `InputDays` a `[]` y `WeekDays` a `0`.
- **`DIAS_FIJOS`**: exige `PeriodType = SEMANAL`. Cada día seleccionado genera una ejecución; `Repetitions` debe coincidir con el número de días distintos (1 a 7). El servidor recalcula ese número. Los índices son domingo `0`, lunes `1`, martes `2`, miércoles `3`, jueves `4`, viernes `5`, sábado `6`. `WeekDays` es la suma de `1 << día`. Lunes y jueves: `InputDays = [1, 4]`, `WeekDays = 18`, `Repetitions = 2`. No son números de día del mes.
- **`PROGRESO`**: exige `PeriodType = SEMANAL`, `Repetitions = 1`, `DifferentPeople = false`. Normaliza `InputDays = []`, `WeekDays = 0`. La tarea se abre el lunes a `StartHour` y vence el siguiente lunes a las 00:00 de Ciudad de México. Requiere meta y unidad con respaldo en el Excel o confirmación del propietario. La carga corresponde a toda la meta. Se acumulan incrementos positivos; solo se entrega la evidencia final cuando se completa la meta.
- **`DifferentPeople`**: la reserva se hace por tarea y semana, también si el ciclo de la definición es mensual, trimestral o anual. Si faltan personas elegibles, el plan se rechaza. No garantiza una persona diferente en cada semana ni permite elegir nombres de empleados.
- Los planes generan únicamente las fechas del calendario que caen dentro de su intervalo. Un plan semanal puede incluir tareas mensuales; su frecuencia mensual se conserva. No conviertas todas las tareas a semanal para que aparezcan en un plan.
- En distribuida y días fijos, el vencimiento es apertura + `DeadlineHours`. En progreso rige el cierre semanal indicado arriba.

### Valores iniciales del formulario y datos faltantes

El formulario nuevo parte de: `Requirement = NINGUNO`, `AssignedRole = PISO_VENTAS`, `PeriodType = SEMANAL`, `Repetitions = 1`, `DeadlineHours = 24`, `LoadMinutes = 30`, `ScheduleMode = DISTRIBUIDA`, `WeekDays = 0`, `StartHour = 0`, `DifferentPeople = false`, `GoalUnits = 100`, `ProgressUnit = unidades`, `Active = true`.

**Estos valores iniciales no son reglas del negocio del Excel.** No los uses para resolver información ausente sin confirmación. Para modalidades distintas de progreso se permiten `GoalUnits = 100` y `ProgressUnit = unidades` como relleno técnico de campos sin efecto operativo; identifica ese origen como `RELLENO_TECNICO`. Los ceros de días no aplicables son normalización técnica. Todo otro valor operativo debe tener una fuente explícita o confirmación. Para un dato sin resolver usa `null` solamente en el reporte de pendientes y excluye esa tarea de `TareasListas`.

### Límites que debes detectar

- No existen periodicidades `DIARIA`, `QUINCENAL`, `SEMESTRAL`, `POR_EVENTO` ni fechas únicas en este formulario. Una tarea diaria puede representarse por días semanales fijos cuando los días aplicables estén confirmados. Quincenal no equivale automáticamente a dos veces al mes ni semestral a dos veces al año.
- No se puede fijar directamente el día 20 del mes, una hora con minutos, una fecha anual específica, alternancia de semanas, días hábiles con exclusión automática de festivos, condición de ventas o vencimiento de una garantía. Señala `NO_REPRESENTABLE` salvo que el propietario confirme una adaptación que conserve la intención.
- Una tarea por evento (queja, emergencia, solicitud, autorización, contingencia, etc.) no debe transformarse en una obligación recurrente sin una regla de adaptación confirmada. `Active = false` no agrega soporte de eventos.
- Si el Excel exige varios archivos o comentario y archivo como requisitos simultáneos, el catálogo solo configura un requisito. Reporta qué condición no puede exigir automáticamente; no pierdas la regla original ni elijas una evidencia arbitrariamente.
- `NINGUNO` elimina el requisito específico, pero la entrega siempre exige una conclusión. La conclusión y los comentarios de entrega admiten hasta 4000 caracteres; los archivos, hasta 15 MB. Estas restricciones de entrega no son campos de creación.
- El nombre no tiene un campo de descripción asociado. Instrucciones largas, protocolos, condiciones y reglas no representables deben conservarse en observaciones del MD, sin afirmar que quedarán aplicadas por el sistema.
- No conviertas supervisor, autorizador, puesto libre o persona nominada a un rol ejecutor sin un mapeo justificado. La asignación posterior considera rol, disponibilidad, capacidad y carga; no se define un trabajador específico al crear la tarea.
- Si una conversión de unidades produce fracciones de hora/minuto, no redondees silenciosamente. Documenta y confirma la adaptación. Todos los límites de esta guía siguen aplicando.

## Formato obligatorio de `tareas-para-carga-masiva.md`

### 1. Resumen de cobertura

Abre con el diagnóstico de suficiencia y explica qué se puede cargar y qué no. Indica archivo(s), versiones, hojas analizadas, aclaraciones del chat utilizadas, fecha, fuentes inaccesibles, limitaciones y los conteos de `LISTA`, `PENDIENTE`, `NO_REPRESENTABLE` y `NO_ENCONTRADA`. Esos estados son excluyentes y deben sumar **202**. Una adaptación confirmada y compatible puede quedar `LISTA`, con su explicación. Incluye aparte duplicados y tareas adicionales del Excel. Si el acceso es parcial, los conteos describen la evidencia disponible y no una revisión completa del Excel.

Agrega una **matriz de cobertura por parámetro** con: `Parametro | TareasConDatoEnExcel | TareasResueltasPorAclaracionConfirmada | TareasSinDatoEnFuentesRevisadas | TareasNoVerificables | TareasConAmbiguedadOConflicto | TareasConDatoIncompatible | NoAplica | CodigosAfectados`. Asigna a cada tarea una categoría principal por parámetro para que cada fila sume 202, y detalla motivos adicionales en el reporte por tarea. Los nombres aportados por el catálogo de este MD no prueban que el Excel contenga esos nombres: identifica su origen. El relleno técnico va en `NoAplica`, no en datos encontrados. Enumera todos los códigos afectados por las carencias; no sustituyas el detalle por «varias tareas».

### 2. Tabla de control completa

Incluye una fila por cada código suministrado, ordenada de `TAR-0001` a `TAR-0202`, con columnas:

`CodigoOrigen | Nombre | EstadoPreparacion | Rol | Requisito | Periodicidad | Modalidad | Repeticiones | Dias | HoraActivacion | PersonasDistintas | PlazoHoras | CargaMinutos | Meta | Unidad | Activa | Fuentes | Observaciones/Pendientes`.

En tablas Markdown escapa `|` y usa `<br>` para separar notas; no deformes los nombres del JSON por necesidades de formato. Si un campo falta, escribe `PENDIENTE`; no un valor supuesto. Incluye los valores técnicos cuando estén resueltos.

### 3. Bloque JSON para carga

Incluye **un único bloque `json`**, válido y sin comentarios, con `VersionFormato = SGOL-TAREAS-EXCEL-v1`, `ZonaHoraria = America/Mexico_City` y `TareasListas`. En este arreglo incluye exclusivamente tareas `LISTA`, sin `null`, puntos suspensivos, campos omitidos ni ejemplos ficticios. Si ninguna está lista, entrega un arreglo vacío. Cada objeto debe contener todas estas claves:

```json
{
  "VersionFormato": "SGOL-TAREAS-EXCEL-v1",
  "ZonaHoraria": "America/Mexico_City",
  "TareasListas": [
    {
      "CodigoOrigen": "TAR-0201",
      "Name": "Regar Arboles",
      "AssignedRole": "PISO_VENTAS",
      "Requirement": "FOTOGRAFIA",
      "PeriodType": "SEMANAL",
      "Repetitions": 2,
      "ScheduleMode": "DIAS_FIJOS",
      "InputDays": [1, 4],
      "WeekDays": 18,
      "StartHour": 9,
      "DifferentPeople": false,
      "DeadlineHours": 12,
      "LoadMinutes": 20,
      "GoalUnits": 100,
      "ProgressUnit": "unidades",
      "Active": true,
      "Fuentes": [
        {
          "Archivo": "ejemplo.xlsx",
          "Hoja": "Tareas",
          "Celdas": "A2:H2",
          "Campos": ["AssignedRole", "Requirement", "PeriodType", "Repetitions", "ScheduleMode", "InputDays", "StartHour", "DifferentPeople", "DeadlineHours", "LoadMinutes", "Active"],
          "Justificacion": "Ejemplo de referencia de origen; sustituir por evidencia real."
        }
      ],
      "Observaciones": ["GoalUnits y ProgressUnit son RELLENO_TECNICO sin efecto en DIAS_FIJOS."]
    }
  ]
}
```

**El objeto anterior solo ilustra la estructura. Ningún parámetro del ejemplo está confirmado para TAR-0201.** Sustituye completamente el ejemplo por el resultado real. Cada campo operativo debe quedar cubierto por `Fuentes` o por una aclaración del propietario identificada expresamente como tal. Explica también normalizaciones y conversiones.

Para una fuente del contexto confirmado del chat, incluye en `Fuentes` un objeto con `Tipo = ACLARACION_CONFIRMADA_CHAT`, `ReferenciaMensaje`, `Campos` y `Justificacion`; si no hay fecha o identificador accesible, usa una descripción localizable del mensaje sin inventarlos. Para datos leídos del archivo, conserva `Archivo`, `Hoja`, `Celdas`, `Campos` y `Justificacion` como en el ejemplo. Distingue siempre lo encontrado en el Excel de lo resuelto por conversación.

`CodigoOrigen`, `InputDays`, `Fuentes` y `Observaciones` son metadatos de intercambio de este MD. `InputDays` corresponde a la selección del formulario y debe concordar con `WeekDays`; los demás no son propiedades persistidas de la tarea. No incluyas `Id`, `TaskCode`, `Revision` ni `Deleted` en el JSON: se resuelven en el sistema receptor.

### 4. Pendientes y reglas no representables

Por código, indica los datos ya encontrados, sus fuentes, campos faltantes/conflictivos, motivo de cada limitación, regla original y preguntas concretas. Incluye una tabla explícita de **información que NO se encuentra en el Excel revisado**, con `CodigoOrigen | Parametro | FuentesRevisadas | DatoQueFalta | ImpactoEnCarga | AclaracionNecesaria`. Debe contener todos los faltantes comprobados, incluso si una aclaración del chat permitió resolverlos; en ese caso identifica cómo se resolvieron. Si no pudiste acceder a las fuentes, coloca esos casos en una tabla aparte de **información no verificable** y no declares su ausencia en el Excel.

Conserva los valores parciales verificables aunque no se pueda dar de alta la tarea. Si hay un faltante común, puedes agrupar códigos en una fila siempre que enumeres todos los afectados y no pierdas particularidades. Propón adaptaciones por separado y márcalas como propuestas hasta su confirmación. Conserva toda lógica del Excel que no cabe en los 14 parámetros y explica qué exige una decisión nueva del propietario o un cambio en SGOL. No conviertas este análisis en una creación de reglas nuevas para ocultar insuficiencias.

### 5. Instrucciones para el Codex receptor en SistemaSGOL

- Usa `TareasListas` como insumo de preparación de carga del catálogo operativo `/tareas`; revalida contra la versión vigente del código antes de modificar datos. Las secciones de pendientes no autorizan altas incompletas.
- Verifica las tareas existentes y resuelve coincidencias por código de origen, nombre y parámetros. Los códigos `TAR-####` de este documento identifican la fuente: las altas nuevas actuales generan un `TaskCode` interno `T-…`. No asumas que `CodigoOrigen` se guarda automáticamente ni que coincide con el código interno. Conserva una relación entre código de origen e identificador/código creado para evitar duplicados en reintentos.
- Distingue altas, coincidencias existentes y posibles actualizaciones. No sobrescribas, desactives o elimines tareas existentes basándote solamente en similitud del nombre. Para editar, recupera `Id` y `Revision` vigentes y respeta el control de concurrencia.
- Realiza la carga mediante el flujo autorizado que reutiliza validaciones y auditoría del catálogo (`OperationalTaskService.SaveAsync`), con autorización de Dirección. No uses la API del catálogo antiguo de definiciones como sustituto del catálogo operativo. No insertes directamente a la base de datos omitiendo estos controles.
- Este documento no implementa un endpoint masivo ni promete atomicidad del lote: el guardado actual es por tarea. Prepara el mecanismo apropiado, prevención de duplicados y registro de resultados antes de aplicar el lote.
- Devuelve un resumen de tareas creadas, ya existentes, omitidas y errores, con correspondencia `CodigoOrigen → Id/TaskCode` donde haya sido resuelta. La carga del catálogo no crea planes ni asigna trabajo a personas. No declares éxito de registros que no se guardaron.

## Verificación antes de entregar

- [ ] Hay exactamente 202 códigos únicos del catálogo, sin pérdida de nombres.
- [ ] Los estados de cobertura suman 202; el conteo `LISTA` coincide con `TareasListas`.
- [ ] El JSON se puede parsear y solo contiene datos reales de tareas listas.
- [ ] Están los 14 parámetros por tarea lista, más los metadatos requeridos.
- [ ] Los valores enumerados, tipos y límites son válidos.
- [ ] Días, máscara y repeticiones concuerdan; progreso es semanal, una repetición y sin personas distintas.
- [ ] Cada dato operativo tiene una fuente o confirmación, y las conversiones están explicadas.
- [ ] No se confundieron meta, repetición, carga, plazo, hora de apertura y hora de cierre.
- [ ] Se reportaron eventos, reglas incompatibles, frecuencias ambiguas y requisitos múltiples.
- [ ] Los pendientes no entran al lote listo; los ejemplos no se presentaron como datos del Excel.
- [ ] Se usaron el archivo y el contexto ya accesibles sin exigir un adjunto nuevo innecesario.
- [ ] Se comunicaron las limitaciones durante el análisis y se incluyeron todas en el MD final.
- [ ] Se distinguieron ausencias comprobadas, fuentes inaccesibles, propuestas no confirmadas e incompatibilidades de SGOL.
- [ ] La matriz por parámetro y el detalle por tarea muestran toda la información faltante sin contar campos no aplicables como carencias.
- [ ] El diagnóstico dice expresamente si es posible completar el lote, solo una parte, ninguna alta o si no se puede evaluar.

## Catálogo proporcionado por el solicitante

Conserva los siguientes códigos y nombres como referencia de origen:

- **TAR-0001** — Analizar causas de ventas perdidas y proponer soluciones
- **TAR-0002** — Impulsar venta cruzada, upselling y apartados
- **TAR-0003** — Diseñar y coordinar acciones comerciales locales
- **TAR-0004** — Elaborar el precorte de ventas de las 19:00
- **TAR-0005** — Monitorear el avance de ventas y activar acciones correctivas
- **TAR-0006** — Monitorear ticket promedio y unidades por transacción
- **TAR-0007** — Liberar la mercancía al vencer un Separado de Cortesía
- **TAR-0008** — Resolver una controversia de asignación de venta con base en evidencia
- **TAR-0009** — Gestionar alternativas por falta de talla o producto
- **TAR-0010** — Gestionar pedidos e incidencias de tienda en línea
- **TAR-0011** — Gestionar la reparación o el cambio de una garantía autorizada después de 90 días
- **TAR-0012** — Recibir y documentar solicitud de garantía
- **TAR-0013** — Resolver quejas y reclamos de clientes
- **TAR-0014** — Analizar la reincidencia de quejas y aplicar acciones correctivas
- **TAR-0015** — Medir el tiempo de atención y la satisfacción del cliente
- **TAR-0016** — Atender clientes especiales o casos complejos
- **TAR-0017** — Analizar el desempeño de exhibición por zona y familia y proponer ajustes
- **TAR-0018** — Montar o actualizar exhibiciones conforme al planograma y la zonificación
- **TAR-0019** — Observar y documentar el recorrido del cliente por zonas de exhibición
- **TAR-0020** — Registrar cambios, incidencias y evidencias en la Bitácora de Exhibición
- **TAR-0021** — Reponer productos vendidos y corregir huecos de exhibición
- **TAR-0022** — Retirar y reportar productos dañados o deteriorados de exhibición
- **TAR-0023** — Revisar precios, etiquetas y señalización conforme a PROFECO
- **TAR-0024** — Solicitar altas, bajas o justificaciones en la Lista Institucional de Exhibición
- **TAR-0025** — Verificar el estado de mobiliario, fachada, escaparates y letreros
- **TAR-0026** — Programar y realizar el pago de servicios básicos
- **TAR-0027** — Elaborar el análisis administrativo trimestral de tendencias
- **TAR-0028** — Preparar la requisición mensual de insumos de limpieza y oficina
- **TAR-0029** — Documentar cancelaciones, reembolsos y movimientos especiales
- **TAR-0030** — Aplicar descuento autorizado en venta
- **TAR-0031** — Consultar y documentar el tipo de cambio para una operación en dólares
- **TAR-0032** — Ejecutar cierre y conciliación de caja
- **TAR-0033** — Realizar arqueos sorpresa durante la jornada
- **TAR-0034** — Realizar cambio de encargado de caja
- **TAR-0035** — Regularizar identificaciones pendientes detectadas en operaciones de crédito o vale
- **TAR-0036** — Resguardar documentos y valores del turno
- **TAR-0037** — Validar el cierre operativo y documental de la sucursal
- **TAR-0038** — Validar las operaciones de contado, crédito y vales realizadas por caja
- **TAR-0039** — Monitorear morosidad y acciones de recuperación
- **TAR-0040** — Emitir y revisar estado de cuenta de crédito directo
- **TAR-0041** — Evaluar la elegibilidad para migrar de Distribuidora de Vales a Crédito Directo
- **TAR-0042** — Integrar solicitud y expediente de crédito directo
- **TAR-0043** — Aplicar descuento de pronto pago a distribuidora
- **TAR-0044** — Clasificar relaciones de distribuidoras según estado de pago
- **TAR-0045** — Conciliar vales, relaciones, sobres y Sizes & Colors
- **TAR-0046** — Consultar y conciliar estado de cuenta de Distribuidora de Vales Loretta
- **TAR-0047** — Entregar relaciones y recibos con acuse firmado
- **TAR-0048** — Escalar cobranza vencida a la etapa correspondiente
- **TAR-0049** — Evaluar y aprobar línea inicial de distribuidora
- **TAR-0050** — Notificar la línea y condiciones autorizadas a la distribuidora
- **TAR-0051** — Preparar y entregar valera con folios controlados
- **TAR-0052** — Recibir pago adelantado de distribuidora bajo control
- **TAR-0053** — Recibir pago o abono parcial de Distribuidora Loretta
- **TAR-0054** — Recibir y validar solicitud de alta de distribuidora
- **TAR-0055** — Resolver comprobante oportuno de Distribuidora no capturado en tiempo
- **TAR-0056** — Solicitar ajuste de fecha de inicio de pago
- **TAR-0057** — Solicitar y documentar descuento extraordinario de distribuidora
- **TAR-0058** — Suspender o cancelar línea de distribuidora en todas las sucursales
- **TAR-0059** — Gestionar autorización telefónica de vale sin documento físico
- **TAR-0060** — Asignar, recuperar y conciliar equipo, accesos y materiales entregados
- **TAR-0061** — Definir y comunicar medidas temporales de continuidad laboral en contingencia
- **TAR-0062** — Ejecutar la apertura operativa de la sucursal
- **TAR-0063** — Asignar, verificar y cerrar el rol semanal de limpieza
- **TAR-0064** — Operar ventiladores elevados con apoyo de resguardo y precauciones de escalera
- **TAR-0065** — Realizar la limpieza técnica del difusor de aroma
- **TAR-0066** — Recargar la esencia del difusor de aroma
- **TAR-0067** — Solicitar y coordinar mantenimiento preventivo y correctivo
- **TAR-0068** — Supervisar la limpieza de áreas visibles al cliente
- **TAR-0069** — Coordinar el inventario físico anual
- **TAR-0070** — Revisar la cartera anual de créditos y vales
- **TAR-0071** — Verificar y organizar la documentación anual de sucursal
- **TAR-0072** — Realizar el corte y arqueo final anual
- **TAR-0073** — Dar seguimiento trimestral al plan estratégico de sucursal
- **TAR-0074** — Definir metas y plan anual de la sucursal
- **TAR-0075** — Participar en la selección de surtido con información de campo
- **TAR-0076** — Realizar sesiones de cierre y planeación con el equipo
- **TAR-0077** — Analizar KPI y definir el plan correctivo mensual
- **TAR-0078** — Realizar reunión breve de cierre
- **TAR-0079** — Transmitir lineamientos corporativos y verificar su comprensión
- **TAR-0080** — Definir y autorizar la compra a proveedor
- **TAR-0081** — Registrar una compra directa sin pedido previo
- **TAR-0082** — Controlar la entrada y salida de mercancía con documentación
- **TAR-0083** — Marcar y resguardar la caja vacía cuando el cliente no requiere empaque
- **TAR-0084** — Registrar y validar el etiquetado y acomodo del lote recibido
- **TAR-0085** — Ejecutar el conteo cíclico formal mensual por zona
- **TAR-0086** — Apoyar la búsqueda física y el seguimiento de diferencias de inventario
- **TAR-0087** — Organizar conteos parciales semanales
- **TAR-0088** — Aplicar en sistema el descuento autorizado a mercancía de baja rotación
- **TAR-0089** — Reportar pérdidas por robo, faltantes o merma
- **TAR-0090** — Solicitar y documentar la autorización de baja de producto dañado o defectuoso
- **TAR-0091** — Cargar y validar la fotografía de estudio del artículo en Sizes & Colors
- **TAR-0092** — Coordinar la recepción de mercancía contra la nota de envío
- **TAR-0093** — Documentar y notificar incidencia de recepción de mercancía
- **TAR-0094** — Imprimir y colocar la etiqueta inicial de código de barras
- **TAR-0095** — Registrar la recepción de mercancía en F-ENT-001
- **TAR-0096** — Preparar y registrar el envío de mercancía por traspaso
- **TAR-0097** — Activar y validar el control manual de asistencia durante una contingencia
- **TAR-0098** — Aplicar medidas disciplinarias autorizadas con derecho de audiencia
- **TAR-0099** — Comunicar cambios de horario o turno con anticipación documentada
- **TAR-0100** — Integrar y validar el expediente laboral de ingreso
- **TAR-0101** — Monitorear asistencia y puntualidad del equipo
- **TAR-0102** — Programar y comunicar vacaciones del personal
- **TAR-0103** — Recabar recibos de nómina firmados y gestionar inconformidades
- **TAR-0104** — Resolver y documentar permisos o admisiones tardías
- **TAR-0105** — Documentar y autorizar descuentos o reposiciones por responsabilidad
- **TAR-0106** — Preparar y entregar un aviso de rescisión con causa documentada
- **TAR-0107** — Gestionar afiliación y movimientos de seguridad social
- **TAR-0108** — Elaborar el plan mensual y trimestral de capacitación
- **TAR-0109** — Elaborar materiales de apoyo para capacitación de servicio
- **TAR-0110** — Evaluar la efectividad de la capacitación y ajustar el plan
- **TAR-0111** — Inducir a nuevos ingresos durante su primera semana
- **TAR-0112** — Mantener el expediente de capacitación por colaborador
- **TAR-0113** — Participar y acreditar capacitación práctica de servicio y ventas
- **TAR-0114** — Programar sesiones, espacios y responsables de capacitación
- **TAR-0115** — Participar y acreditar la capacitación obligatoria del puesto
- **TAR-0116** — Recibir, resguardar e investigar una queja ética o de acoso
- **TAR-0117** — Promover propuestas de innovación y mejora continua
- **TAR-0118** — Reforzar la filosofía y valores Loretta
- **TAR-0119** — Dar seguimiento a los planes de mejora individuales
- **TAR-0120** — Evaluar el desempeño de colaboradores
- **TAR-0121** — Realizar sesiones de retroalimentación y acordar planes de mejora
- **TAR-0122** — Supervisar el cumplimiento de bonos de tareas y reconocimientos
- **TAR-0123** — Integrar y documentar las comisiones de seguridad y capacitación
- **TAR-0124** — Gestionar exámenes médicos ocupacionales y resguardar resultados
- **TAR-0125** — Gestionar permisos y adecuaciones por maternidad o paternidad
- **TAR-0126** — Reportar actos, condiciones inseguras o personas sospechosas
- **TAR-0127** — Auditar inventario y movimientos de mercancía
- **TAR-0128** — Comunicar el programa mensual de auditoría y los roles asignados
- **TAR-0129** — Delimitar las zonas y la secuencia de lectura del inventario
- **TAR-0130** — Ejecutar ajustes y traspasos correctivos de inventario autorizados
- **TAR-0131** — Ejecutar la lectura física y digital del inventario por zonas
- **TAR-0132** — Gestionar la reprogramación documentada de una auditoría
- **TAR-0133** — Investigar diferencias mediante revisión física y Kardex
- **TAR-0134** — Realizar la revisión técnica y documentar mejoras del proceso de auditoría
- **TAR-0135** — Resolver el estatus de artículos en zonas grises antes de la auditoría
- **TAR-0136** — Suspender y reanudar la auditoría ante una contingencia técnica
- **TAR-0137** — Verificar condiciones técnicas y materiales para la auditoría
- **TAR-0138** — Programar el calendario de auditorías operativas
- **TAR-0139** — Realizar la auditoría visual mensual de la sucursal
- **TAR-0140** — Documentar hallazgos, causa raíz y evidencia
- **TAR-0141** — Implementar y verificar el cierre de acciones correctivas y preventivas
- **TAR-0142** — Auditar crédito, vales, pagarés y morosidad
- **TAR-0143** — Auditar servicio, devoluciones y cumplimiento PRC
- **TAR-0144** — Controlar ventas, devoluciones y movimientos realizados durante la auditoría
- **TAR-0145** — Asegurar el cumplimiento de políticas y manuales de sucursal
- **TAR-0146** — Auditar caja y movimientos extraordinarios
- **TAR-0147** — Elaborar y archivar el acta administrativa de una sanción
- **TAR-0148** — Investigar una posible falta y realizar audiencia interna
- **TAR-0149** — Mantener actualizadas las guías visuales y planogramas
- **TAR-0150** — Actualizar formatos y anexos cuando cambien políticas o procesos
- **TAR-0151** — Difundir y capacitar al equipo en el uso de anexos y manuales
- **TAR-0152** — Mantener manuales y anexos accesibles con control de versiones
- **TAR-0153** — Recabar y resguardar acuses de lectura de documentos normativos
- **TAR-0154** — Analizar incidentes y actualizar la base de riesgos
- **TAR-0155** — Atender una emergencia médica
- **TAR-0156** — Capacitar al equipo en seguridad y evacuación
- **TAR-0157** — Gestionar una evacuación por sismo o incendio
- **TAR-0158** — Gestionar una falla eléctrica o de sistema
- **TAR-0159** — Mantener actualizado el directorio de emergencias
- **TAR-0160** — Realizar inspecciones de seguridad y corregir riesgos
- **TAR-0161** — Resguardar llaves, controles y accesos restringidos
- **TAR-0162** — Revisar semanalmente la seguridad y actualizar el plan de emergencias
- **TAR-0163** — Verificar cámaras, alarmas, sensores, señalización y botiquín
- **TAR-0164** — Registrar y escalar incidencia de caja
- **TAR-0165** — Controlar el ingreso y acompañamiento de visitantes
- **TAR-0166** — Ejecutar una revisión de seguridad conforme a protocolo autorizado
- **TAR-0167** — Coordinar la reposición de herramientas dañadas o faltantes
- **TAR-0168** — Verificar el funcionamiento de equipos operativos
- **TAR-0169** — Auditar accesos y uso autorizado de sistemas corporativos
- **TAR-0170** — Brindar soporte inicial y verificar permisos en S&C
- **TAR-0171** — Capacitar al personal en S&C y cambios de versión
- **TAR-0172** — Resguardar credenciales y accesos de sistemas
- **TAR-0173** — Consultar y vincular transacciones en el módulo Relaciones
- **TAR-0174** — Gestionar recuperación o bloqueo de acceso a Sizes & Colors
- **TAR-0175** — Reportar y escalar fallas técnicas
- **TAR-0176** — Ejecutar pagos fiscales, de seguridad social y honorarios autorizados
- **TAR-0177** — Abrochado De Correas Y Cintas
- **TAR-0178** — Limpiar Bolsas
- **TAR-0179** — Limpiar y revisar el calzado exhibido
- **TAR-0180** — Limpiar las suelas del calzado exhibido
- **TAR-0181** — Lavar Cintas Percudidas
- **TAR-0182** — Registrar facturas en Cash Planner
- **TAR-0183** — Conciliar documentos contra Cash Planner
- **TAR-0184** — Preparar y entregar papelería al contador
- **TAR-0185** — Mantener limpia y ordenada la zona de caja y exhibiciones cercanas
- **TAR-0186** — Lavar y secar el tapete de entrada
- **TAR-0187** — Limpiar computadoras y equipos electrónicos
- **TAR-0188** — Programar Festejos (Aniversarios/Cumpleaños)
- **TAR-0189** — Conteo Cpu’S Y Pantallas Tio Pepe
- **TAR-0190** — Confundir Zapatos (Día 20)
- **TAR-0191** — Revisión De Pares Confundidos
- **TAR-0192** — Requisición Mensual Surtida
- **TAR-0193** — Dar seguimiento a pedidos por recibir y actualizar fechas de compromiso
- **TAR-0194** — Revisar el estado y vigencia de los extintores
- **TAR-0195** — Conciliar Apartados En Sistema Vs Físicos
- **TAR-0196** — Reporte De Activaciones De Cd Y Dv
- **TAR-0197** — Solicitar Comisión Artículos De Promoción
- **TAR-0198** — Notas Generales Capturadas / Ultima Nota General En Blanco ___________
- **TAR-0199** — Mensajes De Whatsapp Respondidos
- **TAR-0200** — Cierre De Tienda / Poner Alarma 8:00
- **TAR-0201** — Regar Arboles
- **TAR-0202** — Bono Mercancía Colaboradores Lyfe / Canaco

