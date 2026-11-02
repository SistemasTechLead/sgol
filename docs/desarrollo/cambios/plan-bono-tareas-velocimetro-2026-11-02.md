# Plan de implementación: Bono de tareas y velocímetro compartido

Fecha: 02/11/2026 · zona operativa America/Mexico_City.

Estado: planificación de un cambio mixto; no implementación. Complementa [las decisiones de cálculo](bono-tareas-decisiones-frontend-api-2026-11-02.md). Las respuestas del usuario recogidas aquí prevalecen en los puntos que precisan. No aplicar propuestas contradictorias del diagnóstico inicial.

## 1. Alcance y decisiones confirmadas durante este plan

- Un componente reutilizable para Mi trabajo, un nuevo apartado de Supervisión llamado **Porcentaje para el bono**, y los indicadores individuales del Tablero. El nombre del indicador sigue siendo **Bono de tareas**.
- La aguja representa **calificación semanal**; el porcentaje conservado del componente de bono (100%, 50% o 0%) se muestra por separado. Ninguno representa avance ni bono total.
- Semana actual por defecto, con acceso a la anterior y sus fechas de cierre y pago. El lunes no se sustituye automáticamente la semana actual por la anterior.
- Inicio: **lunes 2 a domingo 8 de noviembre de 2026**, incluyendo las asignaciones evaluables ya creadas para esos días y sus hechos desde el comienzo del lunes. Cierre: **9 de noviembre, 12:00**. Primer pago de referencia: **sábado 14**. Para el sábado 7: «No aplica: evaluación aún no vigente», nunca 0% por incumplimiento. No evaluar la semana pasada ni traer sus pendientes como puntos nuevos.
- El Tablero muestra bono **solo en sesiones personales**. El modo pantalla/quiosco no recibe ni renderiza estos datos. El detalle de Supervisión respeta los permisos actuales.
- Cancelación/exclusión autorizada antes del incumplimiento: sale de la base. Después: conserva base y descuento, salvo decisión expresa de Dirección con motivo y auditoría, exclusivamente antes del cierre. Un arrastre no es una exclusión.
- Transferencia antes de terminar el día original y sin incumplimiento previo: la obligación pasa al receptor con el plazo autorizado y sale de la base del anterior. Es una excepción explícita para transferencia; no se extiende a arrastres ni transferencias posteriores al incumplimiento.
- Las tareas completadas siguen en la base semanal: no se calcula a partir de pendientes visibles. Si trabajó y completó todo, se conservan los puntos que se le asignaron durante la semana.
- Si realmente trabajó sin ninguna tarea evaluable registrada: cerrar con incidencia y **sin porcentaje calculable**, sin inventar 0% ni 100%. No prolongar el cierre ni permitir modificaciones posteriores silenciosas.
- API externa: **solo documentación** para el sistema futuro. Los handlers internos necesarios para actualizar las pantallas sí forman parte de la futura implementación de SGOL.
- Móvil: generar y presentar ideas visuales compactas para las tres vistas antes de elegir su composición. No adoptar automáticamente el resumen bajo el título ni implementar una variante sin presentar las alternativas al usuario.
- Revisión del lunes: es responsabilidad del supervisor revisar todo antes del mediodía. Una tarea que siga devuelta sin corregir al lunes a las 12:00 recibe **descuento completo de sus puntos** para el bono. Conserva su estado operativo; no se amplía el plazo ni se corrige/aprueba automáticamente. Corregir el lunes no recupera puntos de la semana anterior, porque la ejecución termina el domingo. El descuento total sustituye cualquier descuento parcial previo, sin sumarse por encima del valor de la obligación.

## 2. Preguntas abiertas y puntos de consulta

Estado de las decisiones de presentación y devolución tras las aclaraciones del usuario:

| ID | Decisión | Acuerdo y estado |
|---|---|---|
| D1 | Vistas móviles | Acordado generar y presentar alternativas compactas antes de elegir. La composición final queda pendiente de esa presentación, no de repetir la pregunta inicial |
| D2 | Devolución anterior al corte sin nueva entrega válida al terminar el domingo | Resuelto: descuento completo si sigue devuelta sin corregir al lunes a las 12:00. No conservar el resultado del envío devuelto. Correcciones del lunes no recuperan puntos; el supervisor debe revisar antes del mediodía |

Decisiones que deben consultarse con un caso concreto durante implementación/pruebas, sin inventar resultados:

- Transferencia sin incumplimiento cuyo nuevo plazo cruza de semana: confirmar si los puntos cambian de semana o siguen en la original. La respuesta sobre nuevo responsable/plazo no resuelve expresamente el cruce semanal.
- Transferencia después de incumplir: conservar el incumplimiento original, sin trasladarlo al receptor; concretar evaluación del receptor y recuperación sin duplicar puntos cuando el caso real lo exija.
- Para una exclusión excepcional de Dirección posterior al incumplimiento: precisar si retira la obligación completa de la base o conserva base y perdona descuento. No son equivalentes cuando existen otras tareas descontadas.
- Clasificación modificada durante la semana y captura inicial de asignaciones ya existentes: proponer snapshot al crear la obligación; no recalificar el pasado usando el catálogo actual. Si faltan hechos originales, consultar por registro/grupo afectado.
- Compartidas: verificar denominador de minutos planificados y su trazabilidad. No usar minutos actuales/restantes como sustituto de participaciones históricas.
- Tareas que abarcan varios días, progreso y etapas: resolver con evidencia cualquier ambigüedad entre día originalmente asignado y fecha de exigibilidad; no convertir indiscriminadamente StartsAt o DueAt al día del bono.
- Conclusión administrativa sin envío (`CompletionKind=SUPERVISION`): no equipararla silenciosamente con envío a revisión; consultar si aparece un caso cuya evaluación no esté resuelta.

Las decisiones bloquean únicamente su regla/escenario dependiente. Se puede avanzar en componente, consultas, pruebas independientes y documentación. No habilitar el cálculo general mientras existan casos incluidos sin regla resoluble.

## 3. Investigación actual y límites de evidencia

Inspección estática del checkout `master`, capturas proporcionadas y documentos de protocolo/estilo. Existen cambios locales previos en planificación, catálogo, pruebas y documentos; no pertenecen a este plan y deben conservarse. Este trabajo solo añade este documento.

No se consultó la base compartida, no se inspeccionó la versión desplegada ni se ejecutaron pruebas funcionales o visuales. Las capturas sustentan ubicación deseada, no validación de una implementación. El inventario de registros existentes es una actividad futura obligatoria antes del backfill. No se requieren compilación ni reinicio para este cambio documental.

Rutas siguientes relativas al repositorio:

| Fuente real inspeccionada | Hallazgo e impacto |
|---|---|
| `src/Sgol.Web/Pages/MyWork/Index.cshtml` | Cabecera `my-work-heading`, navegación compartida y ayuda móvil existentes; insertar resumen sin sustituir tareas ni progreso diario |
| `src/Sgol.Web/Pages/MyWork/Index.Operational.cs` | `LoadOperationalAsync` carga listado paginado y progreso diario separados; bono debe consultar semana completa, no la página filtrada. Entrega llama `CompleteAsync` y redirige a `/mi-trabajo#plan-tasks` |
| `src/Sgol.Web/wwwroot/js/assigned-tasks.js` | Preparación de evidencia y envío de formulario existentes; conservar validaciones, cámara, antiforgery y redirección |
| `src/Sgol.Web/Pages/Validations/Index.cshtml` | Supervisión vive en `/validaciones`; navegación compartida, revisión y propuestas actuales deben permanecer intactas |
| `src/Sgol.Web/Presentation/Navigation/SectionNavigation.cs` | Apartados Supervisión, Validación, Carga y reasignación. Añadir destino y corregir selección activa para evitar dos botones activos |
| `src/Sgol.Web/Pages/Board/_BoardProgress.cshtml` | Componente de progreso con PersonId opcional. Bono solo junto a barras individuales, no a agregados Día/Semana |
| `src/Sgol.Web/wwwroot/css/live-board.css` | Barras individuales compactas; incorporar columna al extremo derecho, manteniendo alto y ancho útil para nombre/progreso |
| `src/Sgol.Web/wwwroot/js/live-board.js` y `Pages/Board/Index.cshtml.cs` | SSE ya existente: compara snapshots cada 2 s, refresca contenido y anima barras; no es un bus de eventos de dominio. No copiar lectura completa por cada velocímetro |
| `src/Sgol.Web/Infrastructure/Persistence/Planning/LiveBoardReader.cs` | Versión deriva del snapshot operativo. Cambios de bono/reloj deben invalidar su sección aunque no cambie una tarjeta |
| `.../Planning/OperationalTasks.cs` | Asignación guarda criticidad, snapshot, persona, fechas, entrega, revisión y ReassignedFromId; no basta leer estado actual para reconstruir todos los hechos al corte |
| `.../Planning/OperationalTaskService.cs` | Entrega registra CompletedAt, CompletedBy y CompletionKind=ENTREGA; borradores/adjuntos solos no acreditan |
| `.../Planning/OperationalSupervisionService.cs` | Correcciones crean asignación enlazada; cierre administrativo puede usar CompletionKind=SUPERVISION. No duplicar obligación ni confundirlo con entrega |
| `.../Planning/OperationalSupervisionService.Transfer.cs` | Transferencia modifica persona y fechas actuales, con recibo/snapshot. El bono requiere identidad y atribución histórica propias |
| `.../Planning/OperationalPlanGenerator.cs` | Generación crea ocurrencias con DefinitionSnapshotJson. Capturar evaluación una vez por ocurrencia, no multiplicar otra vez por Repetitions |

Rutas a completar en la auditoría de productores: `OperationalTaskService.Occasions.cs` y parciales de programación, `OperationalCarryover*.cs`, `Sequences/ProcessPlanningService.cs`, `Sequences/OperationalProcessService*.cs`, modificación/retirada de asignaciones y revisiones masivas. Seguir catálogo → copia → asignación → ejecución → revisión → consulta → cierre, incluyendo registros anteriores y no solo nuevos.

## 4. Componente elegido y presentación

### Comparación y propuesta técnica

Se buscó un velocímetro reutilizable. [Apache ECharts ofrece gauge y actualización animada](https://echarts.apache.org/en/feature.html), con [ejemplo oficial de gauge](https://echarts.apache.org/examples/en/editor.html?c=gauge-stage). Es una alternativa válida si después se necesitan gráficos complejos; no se justifica incorporar una biblioteca general para este pequeño indicador.

Propuesta: **un único parcial Razor de velocímetro SVG**, modelo de presentación compartido, un CSS y un controlador JS, con variantes de tamaño. Reutilizar tokens, icono info, navegación y patrón de ayuda de SGOL. No tres gráficos independientes. Semántica de medidor según [W3C Meter Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/meter/), con número visible; SVG decorativo, valor accesible y texto de contexto fuera de él. No usar semántica de progreso de tareas para la calificación.

Archivos nuevos propuestos, todavía inexistentes: `Pages/Shared/_TaskBonusGauge.cshtml`, `Presentation/Components/TaskBonusGaugeViewModel.cs`, `wwwroot/css/task-bonus.css`, `wwwroot/js/task-bonus.js`. Un método de actualización recibe datos calculados por servidor; el cliente no implementa descuentos ni rangos económicos.

- Escala lineal 0–100, aguja y arco discreto, cortes reales en 85 y 95. No repartir bandas en tercios, porque distorsionaría la escala.
- Número prioritario: «Calificación 90%», acompañado de «Bono de tareas 50%». En compacto: etiquetas inequívocas «Calif.» y «Bono» con contexto accesible completo.
- No redondear antes de clasificar. Si la presentación de un valor cercano a 85/95 aparenta cruzar el umbral, aumentar precisión o mostrar «<95%»; detalle con puntos exactos y explicación del rango.
- Sin base/error/no vigente: sin aguja apuntando a cero y sin valor numérico inventado. Diferenciar carga, sin tareas evaluables, incidencia, fuera de vigencia y dato desactualizado.
- Temas vigentes con tokens; no depender solo del color. Información clicable con área de 44 px, Escape/clic fuera y retorno de foco. `prefers-reduced-motion` elimina transición, no actualizaciones.

### Mi trabajo

Escritorio: bloque pequeño alineado a la derecha superior de cabecera, como la captura; reservar ancho y no desplazar acciones. Valores, estado provisional/cerrado y semana visibles. Ayuda info junto al gráfico explica puntos, recuperación, umbrales y calendario.

Ejemplo para primera semana: «Evalúa tareas del 2 al 8 de noviembre. Se cierra el lunes 9 a las 12:00. Este resultado corresponde al componente de tareas para el sábado 14; no al sábado 7. No representa el bono total». Usar fechas calculadas, no un texto fijo «la próxima semana» para históricos.

Selector ligero Semana actual / Semana anterior / semanas disponibles, conservando selección mientras se actualiza. Semana anterior al inicio: no vigente. El lunes mostrar aviso de revisión de la anterior sin mezclar sus puntos con los actuales. Para móvil, cumplir la presentación de alternativas de D1; siempre probar reflujo y evitar colisión con navegación/ayuda existente.

### Presentación de ideas móviles antes de implementar

Preparar dos o tres propuestas visuales comparables para Mi trabajo, Supervisión y Tablero personal, reutilizando el mismo medidor. Puntos de partida para explorar, no diseños aprobados: resumen horizontal con miniaguja y porcentajes; control compacto con porcentaje visible y detalle desplegable; fila de dos niveles que reserva el gráfico ampliado para el detalle.

Presentarlas al usuario en contexto de cada pantalla a 320 y 375 px, con datos equivalentes, semana, ambos porcentajes diferenciados y estados sin resultado. Comparar altura ocupada, legibilidad y cantidad de pulsaciones para consultar explicación/periodo. No reducir el área táctil ni ocultar el porcentaje para hacer caber el gráfico. Conservar acceso a tareas y acciones principales.

Solicitar elección o ajustes después de mostrar las propuestas. Identificarlas como maquetas, sin atribuirles validación del flujo real; la variante elegida se implementará y probará después con estilos, permisos y formularios reales.

### Supervisión

Nuevo botón **Porcentaje para el bono** en navegación existente; ruta propuesta `/supervision/bono-tareas`, con selector de semana y colaboradores permitidos. Un resumen por persona con el mismo velocímetro, puntos base, descontados, conservados, estado y fechas.

Detalle desplegable por persona: tarea/ocurrencia, día evaluado, clasificación capturada, puntos asignados/descontados, motivo, fecha de envío/revisión y vínculo al detalle operativo autorizado. Incluir tareas que alteraron base además de descuentos. Mostrar el historial de cambios con fecha y motivo; no atribuir una variación global a una tarea si hubo varios cambios simultáneos. No ocultar entregadas/concluidas ni confundir ausencia de pendientes con base cero. Paginación del detalle no recorta el cálculo.

Acceso según jerarquía actual; no ampliar a todos los colaboradores por la palabra «cada». Una vista parcial por permisos no debe presentarse como calificación completa de una persona: comprobar si la autorización actual permite su resumen completo y consultar si el caso aparece.

### Tablero

En sesión personal: velocímetro mini a la derecha del porcentaje de avance de cada colaborador, misma altura exterior de la barra individual. Reservar ancho, separador y etiqueta Bono; reducir el ancho del carril antes que el tamaño legible del porcentaje. No añadirlo a los agregados de Día/Semana. Evitar confundir el color de ritmo con el del bono.

El bono representa la semana completa, independientemente de filtros de plan/tarea del Tablero: declararlo en la ayuda/contexto. Reutilizar refresco existente sin exigir animación de aguja. Si no cabe a 320 px, redistribuir las barras por filas conservando la altura individual; verificar en pantalla real antes de fijar medidas.

Quiosco: sin medidor, sin valores en HTML/JSON/SSE ni consultas de detalle autorizadas a esa sesión.

## 5. Actualización y animación honesta

Entregar puntualmente puede conservar 100% sin mover la aguja. Entregar Flexible durante recuperación conserva su descuento de 50%; tampoco implica recuperación visual. Mostrar «Actualizado; calificación sin cambios» cuando corresponda. No animar desde cero ni simular una subida para toda entrega.

- Calcular después del commit de entrega/revisión. Conservar formulario y redirección actuales. Transportar el valor previo mediante estado efímero ligado a usuario/persona/semana/versión y consumo único; el GET de retorno obtiene el valor nuevo del servidor. El valor previo solo sirve para animación y nunca autoriza ni calcula pagos.
- La redirección actual apunta a `#plan-tasks`, por lo que puede dejar el indicador fuera de vista. Si cambió, mostrar aviso de resultado con acceso «Ver bono» y ejecutar transición al entrar el indicador en vista; no secuestrar foco/scroll ni hacer una animación invisible. Verificar posición al retorno y móvil antes de aceptar el recorrido.
- En Supervisión, refrescar al guardar decisiones y mientras está abierta. Propuesta inicial: consulta ligera por lote cada 5 s en pestaña visible y al recuperar foco, con versión/ETag, backoff y cancelación al salir. Objetivo de recepción remota ≤5 s más latencia; medir, no prometer instantaneidad de red. Reutilizar mecanismo SSE si la medición justifica añadirlo, sin una conexión por persona.
- Vigilar transiciones por reloj: medianoche, fin de recuperación, cambio semanal y lunes 12:00, aunque nadie entregue. Servidor devuelve `calculatedAt`, `nextChangeAt` y versión; cliente no decide cierre con su reloj.
- Descartar respuestas viejas, separar semana/persona y evitar que respuesta de semana anterior sobrescriba la actual. Transición breve 400–650 ms solo entre valores válidos comparables; cambios de semana se presentan directamente.
- Sesión expirada: retirar datos y solicitar acceso según patrón existente. Desconexión: conservar último dato rotulado «Desactualizado», fecha y reintento; no cero. Reconexión solicita snapshot completo.

## 6. Motor, trazabilidad y cierre

Crear calculador central determinista con reloj inyectado y aritmética decimal. A=suma de puntos evaluables, D=descuentos limitados por obligación. Calificación `100*(A-D)/A`; bono 100 si calificación ≥95, 50 si ≥85, 0 en otro caso. Comparar sin redondear, por ejemplo productos cruzados.

Persistir identidad de obligación, asignación origen, persona/participación, día original, semana, puntos/clasificación capturados, vínculos de intentos, cambios autorizados y versión de reglas. Las correcciones no crean base nueva. Guardar hechos con instante, actor, revisión y clave idempotente dentro de la transacción operativa; no introducir validaciones económicas que impidan una entrega operativamente válida.

Un registro de hechos económicos mínimo debe permitir reconstruir al corte envíos, devoluciones, revisiones, transferencias y exclusiones. No confiar exclusivamente en el estado mutable actual ni en logs sin garantía de retención. Consultas no incrementan contadores de descuento.

Cierre único por persona/semana/versión, cabecera y detalle atómicos, con restricciones de unicidad y reintentos. Propuesta técnica de frontera: ejecución `[lunes 00:00, lunes siguiente 00:00)` y revisiones económicas anteriores a lunes 12:00; una decisión exactamente a las 12:00 no modifica el cierre. Usar tiempo de servidor dentro de sección transaccional serializada por semana; probar operación iniciada antes y confirmada después del corte. No basarse en hora del navegador ni solo en inicio de petición.

Worker recuperable al arrancar y tarea periódica idempotente. Si estuvo apagado al corte, reconstruir hechos elegibles al corte y persistir el mismo resultado, ignorando revisiones posteriores. Una consulta posterior al corte no debe mostrar una semana todavía provisional por demora del worker: finalizar mediante el servicio central o indicar cierre pendiente sin un resultado pagable nuevo. Nunca extender ejecución hasta el lunes.

Entrega en revisión al corte conserva resultado por fecha de envío; no equivale a aprobación operativa. Revisiones posteriores siguen su flujo pero no alteran el cierre económico. Incidencia de persona con trabajo sin base se cierra sin porcentaje. Sin trabajo toda la semana y sin base: «Sin tareas evaluables», bono 0, calificación nula.

### Inicio con datos ya existentes

Antes de migrar: inventario de solo lectura por ocurrencia/semana de actuales, completadas, revisadas, retiradas, transferidas, correcciones, procesos y compartidas. Identificar qué hechos del 2 de noviembre ya ocurrieron y qué clasificación/día puede acreditarse. Reproducir en copia aislada cuando se necesite historial. No basarse en la bandeja actual.

Backfill idempotente solo para obligaciones elegibles desde 02/11/2026; excluir las de semanas previas aun si se ejecutan esta semana. Presentar discrepancias concretas antes de asignar puntos dudosos. Si no se puede reconstruir un hecho original, registrar incidencia y consultar, no inventarlo. Nuevos registros usarán captura consistente desde su creación; no duplicar existentes en reintentos.

## 7. API futura: documentación, no endpoint externo ahora

El documento de decisiones no contiene contrato aprobado. Dejar una propuesta de contrato de consulta versionado y solo lectura, separada de los handlers internos. No implementar consumidor, pagos, ventas, credenciales de integración ni publicar endpoints externos.

Propuesta de campos: personaId estable, ámbito/sucursal, semanaInicio/Fin, timezone, executionCutoffAt, reviewCutoffAt, paymentReferenceDate, estado, calificacionPercent nullable, bonoTareasPercent nullable (0/50/100), puntosEvaluables, puntosDescontados, calculatedAt, closedAt nullable, rulesVersion y resultVersion. Distinguir PROVISIONAL, CERRADO, SIN_TAREAS_EVALUABLES, INCIDENCIA_SIN_BASE y NO_VIGENTE mediante estado de cierre y estado de cálculo separados si conviene. Cierre puede existir con incidencia, sin número pagable.

Calificación y bono usan unidades explícitas; factor opcional derivado no es decisión aprobada. Documentar consulta individual/lote/histórica, paginación estable, autorización por ámbito, no-store para datos personales o política equivalente, errores 400/401/403/404/409/503 según contrato final y ausencia de adjuntos/detalles privados. La ausencia de resultado nunca se convierte en cero.

Ejemplo semántico: A=50, D=5 → calificación 90, bonoTareasPercent 50; periodo 02–08/11, cierre 09/11 12:00, pago referencia 14/11. Consultar repetidamente devuelve mismo cierre/versionado. El consumidor sigue siendo responsable del bono total y sus demás condiciones. Resolver autenticación y contrato final al desarrollar ese sistema; registrar pruebas de API como futuras, no ejecutadas ahora.

## 8. Orden de implementación y preguntas durante las pruebas

1. Presentar ideas móviles y recoger elección D1; aplicar D2 ya resuelto sin volver a preguntarlo. Identificar runtime/base/migraciones antes de escrituras. Inventariar existentes y registrar hechos faltantes; preguntar por excepciones con una tarea concreta.
2. Crear fixtures y calculador con reloj controlado. Antes de fijar un esperado no resuelto, enviar pregunta con estado inicial, fechas, puntos, alternativas y consecuencia visible. Registrar respuesta aquí con ID y añadir regresión correspondiente.
3. Modelo/migración y captura en todos los productores. Probar backfill en base aislada, conservación operativa y reintento. No habilitar globalmente casos cuya atribución siga pendiente.
4. Integrar cierre y consulta común; probar medianoche, concurrencia, corte y recuperación tras apagado antes de exponer resultados cerrados.
5. Componente compartido y Mi trabajo, incluyendo retorno de entrega; Supervisión y detalle; Tablero personal y exclusión de quiosco.
6. Pruebas con formularios/servicios reales y navegador de aplicación, recoger decisiones nuevas en tiempo real. Mantener preguntas pequeñas, continuar escenarios independientes y no tratar silencio como acuerdo.
7. Documentar API futura, revisar diff y evidencia. Identificar versión desplegada y destino solo si se autoriza posteriormente dejar implementación disponible; este plan no realiza commit, push ni despliegue.

Protocolo obligatorio para el implementador: ante un caso ambiguo, detener solo su expectativa dependiente, conservar fixture/reproducción, explicar opciones al usuario y preguntarle en el mismo trabajo. Nunca escoger una regla de negocio para que la prueba pase. Al responder, actualizar decisiones, prueba y matriz juntas. Si el usuario no responde, marcar **no ejecutado por decisión pendiente**, sin declarar validación completa. Los casos acordados no se vuelven a preguntar.

## 9. Matriz de escenarios y evidencia futura

Todos los escenarios siguientes están **propuestos, no ejecutados**. U=calculador con reloj controlado; I=base aislada `sgol_task_checks_*` y servicios/formularios HTTP reales; N=navegador de aplicación real sobre I. C=consulta futura de contrato, sin endpoint externo implementado. Cada ejecución deberá añadir comando, fixture, resultado observado y evidencia local sin secretos.

| ID | Precondición y acción | Resultado esperado | Prueba/entorno | Estado |
|---|---|---|---|---|
| B01 | Seis repeticiones Importantes generadas | A=30, una obligación por repetición | U/I generador | Propuesto |
| B02 | A=50, D=5; 85/95 exactos y fracciones inferiores | 90→bono50; fronteras exactas sin redondeo engañoso | U/N | Propuesto |
| B03 | Futuras confirmadas, ninguna entrega/descuento | 100 provisional, sin marcar tareas realizadas | I/N | Propuesto |
| B04 | Entregar en día asignado aunque vencida por hora | Sin descuento por atraso horario; entrega no obliga a mover aguja | I/N formulario | Propuesto |
| B05 | Flexible sin envío al acabar día; enviar siguiente día | D=0.5; envío mantiene D, no suma/resta otra vez | U/I/N | Propuesto |
| B06 | Flexible no entrega siguiente día; Importante/Crítica incumple día | D=1/5/10 respectivamente, máximo por obligación | U/I | Propuesto |
| B07 | Flexible domingo pendiente; sábado enviada domingo | Domingo pierde todo; sábado conserva descuento mitad | U/I cortes | Propuesto |
| B08 | Entrega domingo; aprobación lunes 11:59 o sin revisar 12:00 | Fecha de envío manda; pendiente revisión se fija provisional como definitivo económico | I | Propuesto |
| B09 | No satisfecha antes/después de corte | Antes descuenta; después no altera cierre | I | Propuesto |
| B10 | Devuelta que sigue sin corregir al lunes 12:00; variante con corrección enviada lunes | Descuento completo, sustituyendo cualquier parcial sin duplicarlo; corrección del lunes no recupera puntos de la semana anterior. Cierre permanece inmutable | I/N | Propuesto |
| B11 | Correcciones múltiples y arrastre cruzan semana | Una obligación; día original/descuento intactos, sin base repetida | I | Propuesto |
| B12 | Transferir sin incumplir dentro de semana | Sale de anterior, pasa a receptor y plazo autorizado | I/N | Propuesto |
| B13 | Transferir después de incumplir o cruzando semana | Conservar incumplimiento original; consultar atribución/cruce antes de fijar esperado restante | I + pregunta | Propuesto |
| B14 | Cancelar antes de incumplir; después sin excepción | Antes sale de base; después conserva base/descuento | I/N | Propuesto |
| B15 | Dirección excepciona antes del corte / intenta después | Motivo/auditoría; decisión sobre base pendiente; después cierre inmutable | I + pregunta | Propuesto |
| B16 | Todas las tareas terminadas, bandeja sin pendientes | Base conserva completadas y calificación semanal correcta | I/N | Propuesto |
| B17 | Sin trabajo ni base; con trabajo pero sin registros | Primero calificación nula/bono0; segundo incidencia cerrada sin porcentaje | U/I/N | Propuesto |
| B18 | Progreso incompleto/completo; borrador/archivo sin envío; NINGUNO enviado | Meta al vencer; adjuntos/borradores no acreditan; NINGUNO conserva requisito actual | I formularios | Propuesto |
| B19 | Etapa bloqueada/reservada que luego se habilita | Sin castigo antes de exigibilidad; al habilitar capturar una obligación; no sumar proceso completo | I procesos | Propuesto |
| B20 | Compartida 10 puntos con minutos 60/40 | 6/4 conservados por persona; sin duplicación; verificar datos fuente | I + inventario | Propuesto |
| B21 | Cambio clasificación, ausencia y cancelación simultáneos | Snapshot/decisión aplicable trazables; sin perdonar atraso por arrastre | I + consulta si falta regla | Propuesto |
| B22 | Backfill 2–8 nov con entregadas e históricas previas | Incluye actuales completas; anteriores fuera; sábado7 no vigente, sábado14 primer periodo | I copia aislada/N | Propuesto |
| B23 | Repetir backfill/entrega/cierre/consultas | Sin duplicación; misma obligación y cierre | I | Propuesto |
| B24 | Envío/revisión/excepción concurrentes con cierre, exactamente 12:00 | Un orden transaccional, corte consistente, sin aplicación parcial | I reloj/barreras | Propuesto |
| B25 | Aplicación apagada domingo/lunes; reinicio tras revisión tardía | Reconstruye al corte, ignora hechos posteriores; resultado estable | I | Propuesto |
| B26 | Retorno real de entrega; porcentaje cambia/no cambia | Animación visible solo cuando cambia, valor final correcto, foco/scroll preservados | N Chromium/WebKit | Propuesto |
| B27 | Otro colaborador entrega mientras Supervisión sigue abierta | Refresco por lote y detalle coherente, sin recargar formulario en curso | I/N dos sesiones | Propuesto |
| B28 | Lunes actual/anterior; filtro de tarea/plan; paginar | Semanas separadas, bono semanal completo ajeno a filtro operativo | I/N | Propuesto |
| B29 | Quiosco, acceso ajeno, manipular persona/semana | Sin filtración de valores ni detalle; permisos servidor | I HTTP/N | Propuesto |
| B30 | Error/red lenta, respuesta fuera de orden, sesión expirada y reconexión | Estados explícitos; no cero falso ni sobrescritura con datos viejos | I/N | Propuesto |
| B31 | 320/375/768 y escritorio 1280/1920, temas vigentes, nombres largos | Número legible, sin saturar barras ni overflow; D1 define móvil Mi trabajo | N | Propuesto |
| B32 | Teclado, foco, zoom200%, lector de pantalla, movimiento reducido | Ayuda operable, valor/semana accesibles, sin transición obligatoria | N/manual | Propuesto |
| B33 | Medianoche/corte sin tareas nuevas; JS desactivado | Datos correctos al consultar/refrescar; SSR muestra resultado sin depender de animación | U/I/N | Propuesto |
| B34 | Contrato: cierre/incidencia/no vigente y lote repetido | Nulos distintos de cero, unidades claras y resultado estable | C, implementación externa futura | Propuesto |

Reutilizar fixtures y recorridos pertinentes de `tools/Sgol.TaskChecks`: `CorrectionSchedulingChecks.cs`, `CorrectionHttpChecks.cs`, `CarryoverChecks.cs`, `TransferChecks.cs`, `CurrentAssignmentTransferChecks.cs`, `BatchReviewChecks.cs`, `ChecklistChecks.cs` y `AssignmentRetirementChecks.cs`. Revisar requisitos antes de ejecutar; añadir suite específica de bono. Reutilizar herramientas de navegador en `tools/Sgol.MobileChecks` para páginas reales. Una suite detenida por capacidad no valida bono.

## 10. Criterio de aceptación y cierre del trabajo futuro

Calificación idéntica en las tres vistas para persona/semana/versión; número siempre entendible, fecha de efecto explícita, sin duplicar puntos ni modificar operación. Mi trabajo conserva entrega real y retorno; Supervisión explica causas; Tablero personal mantiene altura y quiosco excluye datos. Cierres estables y reconstruibles, históricos y completadas conservados.

Revisar diff, compilar proyectos afectados y ejecutar matriz proporcional al riesgo. Registrar aprobados/fallidos/no ejecutados/no aplicables con evidencia o razón. No afirmar validación completa mientras haya ambigüedades económicas o pruebas críticas pendientes. No confundir diseño estático, maqueta, prueba de componente y flujo real. Este documento es el entregable actual; no certifica implementación, base, pruebas ni despliegue.
