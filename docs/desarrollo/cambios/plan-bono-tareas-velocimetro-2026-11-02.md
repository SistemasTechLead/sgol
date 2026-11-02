# Plan de implementación: Bono de tareas y velocímetro compartido

Fecha: 02/11/2026 · zona operativa America/Mexico_City.

Estado: cambio mixto implementado en el checkout; ejecución y límites registrados en la sección 11. Sin push ni migración manual del entorno compartido. Complementa [las decisiones de cálculo](bono-tareas-decisiones-frontend-api-2026-11-02.md). Las respuestas del usuario recogidas aquí prevalecen en los puntos que precisan. No aplicar propuestas contradictorias del diagnóstico inicial.

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

Esta tabla conserva la matriz originalmente propuesta. Su estado de ejecución se registra en la sección 11, que prevalece sobre la columna histórica «Propuesto». U=calculador con reloj controlado; I=base aislada `sgol_task_checks_*` y servicios/formularios HTTP reales; N=navegador de aplicación real sobre I. C=consulta futura de contrato, sin endpoint externo implementado.

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

Revisar diff, compilar proyectos afectados y ejecutar matriz proporcional al riesgo. Registrar aprobados/fallidos/no ejecutados/no aplicables con evidencia o razón. No afirmar validación completa mientras haya ambigüedades económicas o pruebas críticas pendientes. No confundir diseño estático, maqueta, prueba de componente y flujo real. El registro siguiente distingue implementación, pruebas y activación.

## 11. Implementación y ejecución del 02/11/2026

### Respuestas del usuario durante la implementación

| Decisión | Resolución vigente |
|---|---|
| D1 móvil | **Resumen desplegable**, elegido después de presentar tres composiciones en contexto. Tablero conserva el indicador a la derecha. |
| Plazo multidiario/progreso | Evaluar al finalizar el plazo autorizado. Precisión posterior: si vence el miércoles 4 a las 14:00, el descuento comienza **al terminar ese día**, no a las 14:01. TAR-0021, 2/11 09:00–9/11 00:00, se evalúa al terminar el domingo 8. |
| Transferencia puntual entre semanas | La obligación cambia a la semana del nuevo plazo autorizado. |
| Transferencia después de incumplir | El anterior conserva su incumplimiento; el receptor realiza recuperación sin puntos adicionales. |
| Excepción de Dirección | Retirar la obligación completa de base y descuento. Con A=50/D=10 y obligación de 5, queda A=45/D=5. |
| Permisos del Tablero | Solo bono propio y de personas que puede supervisar. Las otras barras operativas conservan su visibilidad habitual. |
| Trabajo sin base | Usar horarios y ausencias como criterio. No se añade formulario de asistencia efectiva. |
| Conclusión administrativa | `CompletionKind=SUPERVISION` acredita cumplimiento con su fecha; se identifica como conclusión administrativa, no como envío ficticio. |
| Git | Integrar primero los cambios previos en un commit y dejar el bono en otro. El commit previo es `3756c44`. No se autorizó push. |
| Watcher existente | El usuario decidió **dejar activo** el `dotnet watch` externo y aceptar sus recargas. No se aplican las migraciones a la base compartida ni se ejecuta despliegue manual. |

### Código y conservación operativa

- `TaskBonusCalculator.cs`: periodos, puntos, descuentos, umbrales decimales, incidencias y cierre. La cifra visible se formatea en servidor para no redondear hacia un tramo superior.
- `TaskBonusPersistence.cs`, `TaskBonusCommitInterceptor.cs` y `SgolDbContext.cs`: hechos independientes capturados al guardar y confirmados en la misma transacción. Devoluciones, transferencias y eliminaciones conservan identidad económica. El interceptor fecha la visibilidad al confirmar, y rechaza una excepción de Dirección que cruza el corte.
- `TaskBonusService.cs`: consulta por lote, permisos existentes, materialización de obligaciones, cierre inmutable y worker recuperable. La clasificación original se conserva; reservas de procesos no generan puntos y sus intentos comparten la identidad de etapa.
- `_TaskBonusGauge.cshtml`, `task-bonus.css/js`: componente compartido con porcentaje prioritario, ayuda, movimiento reducido y resumen móvil desplegable. Mi trabajo conserva sus formularios y retorno; Supervisión añade resumen/detalle y excepción auditada; Tablero consulta solo en sesión personal y preserva la altura.
- API externa: [propuesta separada](bono-tareas-contrato-externo-propuesto-2026-11-02.md); sin endpoint externo, consumidor, pagos ni credenciales de integración.

La consulta no cambia cargas, responsables, revisión ni plazos. El inventario inicial encontró 780 asignaciones, 246 con inicio en 2–8/11, y 10 retiradas en total. Las completadas forman parte del backfill. Se conservan snapshots anteriores como soporte de linaje, pero no entran como obligaciones nuevas desde noviembre. `DifferentPeople` corresponde a distribución de repeticiones; no acredita participación compartida por minutos y no se usa para dividir puntos.

### Entornos y evidencia reproducible

PostgreSQL comprobado en `localhost:5440`. Base compartida: `sgol_local`, consultada sin aplicar las migraciones del bono. Las bases usadas para regresión comienzan con `sgol_task_checks_bonus_`; el ejecutable rechaza otros destinos. El host HTTP de pruebas usa `https://localhost:7296`, sesiones sintéticas aisladas y las páginas, binding, antiforgery, permisos y servicios reales. No certifica el proveedor de autenticación real ni un despliegue LAN. Los archivos locales pueden contener datos simulados: no se publican ni se incluyen en Git.

Comandos de prueba, después de cargar la conexión dedicada desde el almacén local de secretos sin imprimirla:

```powershell
dotnet build tools/Sgol.TaskChecks --artifacts-path .local/bonus-build-v3 -m:1
dotnet .local/bonus-build-v3/bin/Sgol.TaskChecks/debug/Sgol.TaskChecks.dll --bonus-rules
dotnet .local/bonus-build-v3/bin/Sgol.TaskChecks/debug/Sgol.TaskChecks.dll --bonus-migration
dotnet .local/bonus-build-v3/bin/Sgol.TaskChecks/debug/Sgol.TaskChecks.dll --bonus-http
dotnet .local/bonus-build-v3/bin/Sgol.TaskChecks/debug/Sgol.TaskChecks.dll --bonus-process
dotnet .local/bonus-build-v3/bin/Sgol.TaskChecks/debug/Sgol.TaskChecks.dll --bonus-http --ui
node tools/Sgol.MobileChecks/task-bonus-flow-checks.cjs .local/bonus-20261102
node tools/Sgol.MobileChecks/task-bonus-correction-checks.cjs .local/bonus-20261102
node tools/Sgol.MobileChecks/task-bonus-checks.cjs .local/bonus-20261102
node tools/Sgol.MobileChecks/task-bonus-resilience-checks.cjs .local/bonus-20261102
```

`--bonus-process` requiere una base vacía; `--bonus-migration`, una copia previa a las migraciones; `--ui` deja el host encendido y escribe `fixture.json`. Las pruebas de navegador de entrega y corrección se ejecutan en ese orden sobre fixtures nuevos. Playwright se resuelve mediante el `NODE_PATH` del runtime local. La prueba de layouts no debe ejecutarse al mismo tiempo que las que cambian el reloj.

### Matriz ejecutada

| ID | Estado y evidencia concreta |
|---|---|
| B01 | Calculador aprobado: seis Importantes = 30. Creación persistida y generador real cubiertos en `TaskBonusCoverageChecks`. |
| B02 | Aprobado en calculador: 90→50, 85/95 exactos y decimales inferiores; formato del servidor evita salto visual de tramo. |
| B03–B04 | Aprobado I/N: pendientes futuros dan 100 provisional; formulario NINGUNO envía sin requisito nuevo; envío puntual conserva valor y no anima. |
| B05–B07 | Aprobado U/I: mitad/total, sustitución sin acumulación, domingo y recuperación sábado-domingo; progreso completo enviado en recuperación mantiene medio punto. |
| B08–B09 | Aprobado U/I: pendiente de revisión al corte, rechazo anterior y posterior, consulta de cierre persistido no cambia. |
| B10 | Aprobado I/N: devolución real, corrección real del lunes sin recuperar puntos; corrección dentro del día sí recupera el resultado real y anima. |
| B11 | Aprobado I en corrección enlazada e intentos de proceso; arrastre autorizado comprobado por suite adicional. |
| B12–B13 | Aprobado I: transferencia puntual y posterior al incumplimiento con entrega real del receptor; cruce a semana del nuevo plazo aprobado en materialización con reloj. No se ejecutó ese cruce concreto mediante navegador. |
| B14 | Aprobado U/I: retirada persistida anterior, cancelación tardía en reglas y eliminación real con vista previa/confirmación; sobreviven hechos, transferencia y cierre. No se repitió toda la edición del catálogo y retiro masivo en navegador. |
| B15 | Aprobado I: formulario real de excepción, motivo, auditoría, CSRF, reintento y cierre inmutable. |
| B16 | Aprobado I/N: entrega real sale de pendientes y permanece en detalle/base; el proceso concluido conserva sus cuatro obligaciones. |
| B17 | Aprobado U/I: horarios, rango de vigencia, ausencias, sin base calculable y sin trabajo; no inventa 0%/100% de calificación. |
| B18 | Aprobado I: meta incompleta rechaza entrega, avance no es envío, plazo multidiario y envío final real; NINGUNO por formulario HTTP y navegador. PDF real de proceso en B19. No se ejecutaron aquí todos los tipos de cámara/checklist/borrador. |
| B19 | Aprobado I: plan de proceso, reserva, activación, PDF por formulario, devolución, nuevo intento, aprobación y conclusión; cuatro obligaciones, sin puntos por reservas ni por proceso completo. |
| B20 | Calculador aprobado con participaciones explícitas 6/4. **No aplicable I**: no se encontró productor operativo de participación compartida 60/40; no se inventa a partir de `DifferentPeople`. |
| B21 | Aprobado I: clasificación, ausencia y cancelación en una misma transacción conservan la base y descuento originales. También se verifican vigencia de horario y ausencia por separado. Es prueba de persistencia; no se presentan esos cambios como un formulario operativo único. |
| B22–B23 | Aprobado I: migración de 780 asignaciones completas/retiradas sin modificar sus datos, repetición idempotente y modelo coincidente; primera semana/sábado, consultas, exclusión y cierre sin duplicación. |
| B24 | Aprobado I: rollback; cuatro cierres concurrentes con misma versión; revisión guardada 11:59 y confirmada 12:00 permanece operativa pero no entra en cierre económico; una excepción que cruza ese límite se revierte íntegramente. No se ejecutó una barrera concurrente de todos los tipos de formularios a la vez. |
| B25 | Aprobado I: reconstrucción sin cierre previo ignora revisión tardía; worker real arrancado después del corte cubierto por suite adicional. |
| B26–B27 | Aprobado N Chromium/WebKit: entrega real sin cambio, devolución/corrección con cambio y animación visible, aviso de retorno, refresco remoto de Supervisión que conserva el motivo en edición. |
| B28 | Aprobado N: actual/anterior y búsqueda sin resultados con paginación conservan el bono semanal completo. El filtro de plan del Tablero se inspeccionó en código, sin recorrer todas sus combinaciones en navegador. |
| B29 | Aprobado I/N: propia/supervisión, CSRF, quiosco HTML/refresh y handlers denegados. Las sesiones de prueba son sintéticas; autenticación externa real no cubierta. |
| B30 | Aprobado N: error/reconexión, sesión revocada, petición retenida más de 10 segundos, una sola consulta en vuelo y respuesta antigua liberada después de cambiar de semana. Conserva último valor y elimina datos al perder acceso. Se simulan eventos de página restaurada; no certifica BFCache físico de Safari. |
| B31 | Chromium/WebKit, temas light/dark/special y 320/375/768/1280/1920; capturas locales. Prueba compara la altura real de cada barra con/sin indicador. |
| B32 | Teclado, Escape/foco, texto raíz al 200% y movimiento reducido en navegador. **No ejecutados** lector de pantalla ni Safari/iPhone físico. |
| B33 | Medianoche/corte aprobados U/I; refresco temporal y SSR sin JavaScript cubiertos N. |
| B34 | Documentado; **no aplicable como API ejecutada**, por alcance expresamente futuro. |

Los estados parciales anteriores no equivalen a certificar todas las combinaciones de la matriz. Los comandos y logs de la carpeta `.local/bonus-20261102` distinguen cada ejecución fallida y su reejecución.

Resultados consolidados: compilación sin advertencias/errores (`build-final2.log`); **34** comprobaciones del calculador (`rules-final-reviewed.log`); **53** de integración (`http-final-combinations.log`); migración idempotente con **780** asignaciones conservadas (`migration-final.log`); proceso completo (`process-v5.log`); **98** casos de navegador (`browser-reviewed-v2.log`), más **4** recorridos de entrega y corrección (`browser-flow-clean.log`, `browser-correction-clean.log`) y **4** casos de resiliencia (`browser-resilience-final.log`). Los JSON `browser-*-results.json` y capturas correspondientes contienen el detalle por motor y tamaño. Los fallos de layout anteriores permanecen en sus logs; la ejecución aprobada usa una copia de simulación con fixtures nuevos, sin la acumulación de personas de las regresiones destructivas.

### Fallos encontrados y correcciones

- El indicador compacto aumentaba la altura de la barra móvil. Se corrigieron columnas/filas y altura tipográfica; la regresión compara geometría renderizada.
- La prueba de proceso largo esperaba que una devolución de seis horas cupiera al final del día. Se conservaron las validaciones y el fixture fallido; el fixture de bono usa cuatro etapas de una hora, espera las reservas autorizadas y verifica el recorrido completo. También se actualizó su expectativa: capacidad conserva bloques terminados cuyo plazo aún no termina.
- La captura inicial al guardar permitía considerar revisión previa al corte aunque la transacción se confirmara después. Se añadió el interceptor y la regresión con reloj cruzando 11:59→12:00.
- La prueba inicial de desconexión interceptaba un orden de query string distinto del real. Se cambió el matcher a `URL.searchParams`; no se alteró el producto para hacerla pasar.
- La prueba de horario futuro violó inicialmente la restricción de solapamiento. Se corrigió el rango del fixture, conservando la restricción de base.
- El fixture inicial de arrastre no tenía política de agenda y fue rechazado por la regla operativa. Se captura una política real en el fixture; no se omite la validación.
- Una petición retenida podía detener el refresco indefinidamente. Se limita a 10 segundos, se conserva el último dato y se reintenta; respuestas/errores de una navegación anterior no reemplazan el periodo actual.
- La alineación superior de escritorio entró en conflicto con el modo de texto al 200%. Se usa el mismo container query de la interfaz existente; la matriz vuelve a comprobar reflow y posición junto al título.

### Activación posterior

No se aplicaron a `sgol_local`. Antes de activar esta versión, respaldar y verificar servidor/base efectivos, ejecutar las migraciones con las herramientas habituales y comprobar las tres pantallas en la sesión real:

1. `20261102214221_AddTaskBonusHistory`: hechos y cierres; snapshot inicial de asignaciones y transferencias.
2. `20261102214431_AddTaskBonusDecisions`: excepciones de Dirección.
3. `20261102214845_AddTaskBonusAvailabilityHistory`: historial económico de disponibilidad.

El worker cierra periodos vencidos; no borra ni reinicia simulaciones. Una instalación inicial posterior al primer corte que no pueda acreditar el historial produce incidencia, no un porcentaje inventado. El watcher externo queda activo por decisión expresa del usuario; esa recarga automática no sustituye aplicar y verificar las migraciones en el destino autorizado.
