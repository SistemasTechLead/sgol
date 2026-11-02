# Bono de tareas: decisiones para continuar frontend y API

Fecha: 02/11/2026. Documento de continuidad entre chats. Estado: reglas acordadas y diseño pendiente; no representa una implementación.

## 1. Objetivo y límite de alcance

Agregar el indicador «Bono de tareas», con un velocímetro de calificación semanal, y posteriormente permitir consultar los resultados mediante una API para otro sistema.

**Las decisiones de este documento afectan exclusivamente el cálculo y la presentación de la evaluación. No cambian el funcionamiento operativo actual de SGOL.**

Conservar asignaciones, horarios, vencimientos operativos, arrastres, supervisores por rol, especializaciones, requisitos de entrega, evidencias, correcciones, reasignaciones, planificación y seguimiento. No crear nuevos trámites ni cambiar esos flujos para acomodar el bono. Si falta información para calcular, identificarla y resolver cómo registrarla para evaluación sin alterar implícitamente la operación.

SGOL evaluará únicamente el componente de tareas. No calculará pesos, ventas ni el bono total; esos aspectos corresponden al sistema consumidor y a las demás condiciones del bono.

## 2. Calificación y puntos

Usar la clasificación existente, modificable por Dirección:

| Clasificación | Puntos por repetición | Recuperación para el bono |
|---|---:|---|
| Flexible | 1 | Hasta finalizar el día siguiente, limitada por el domingo |
| Importante | 5 | No tiene recuperación al día siguiente |
| Crítica | 10 | No tiene recuperación al día siguiente |

Cada repetición cuenta por separado. No agregar un campo independiente de recuperabilidad.

`Calificación = 100 × (puntos evaluables − puntos descontados) / puntos evaluables`

| Calificación real, sin redondeo previo | Porcentaje de bono conservado por tareas |
|---|---:|
| Desde 95% hasta 100% | 100% |
| Desde 85% y menos de 95% | 50% |
| Menos de 85% | 0% |

Ejemplo: 50 puntos evaluables y 5 descontados producen 90% de calificación y 50% del componente de bono.

No confundir calificación con proporción de tareas terminadas, cantidad de archivos o porcentaje simple de entregas puntuales. Una Flexible entregada durante recuperación conserva la mitad de sus puntos.

## 3. Día evaluado y descuentos

El bono utiliza el **día originalmente asignado**, separado del vencimiento operativo con hora. Conservar la zona operativa actual `America/Mexico_City`.

- Envío a revisión dentro del día asignado: sin descuento provisional por atraso.
- Flexible sin envío al terminar ese día: descuento del 50% de su valor.
- Flexible enviada durante el día siguiente: conserva el descuento del 50%.
- Flexible sin cumplimiento al terminar el día siguiente: descuento total del 100%.
- Importante o Crítica sin cumplimiento dentro del día asignado: descuento del 100%.
- El descuento máximo es el valor de la tarea; no sumar 50% y 100%.
- No aplicar descuentos por horarios dentro del mismo día. Una tarea puede estar operativamente «Vencida» y todavía no tener descuento del bono.

**Todo arrastre autorizado permite ejecutar después, pero mantiene los descuentos y el día original para el bono.** Aplica también a Flexibles. El arrastre no traslada puntos a otra semana, no reinicia la recuperación y no borra descuentos. La tarea conserva seguimiento operativo sin volver a descontarse por el mismo incumplimiento en semanas posteriores.

## 4. Envío, revisión y cierre definitivo

El hecho que fija la entrega para la evaluación es **enviar la tarea a revisión**, con persona, fecha y hora. No se calcula contando evidencias. Se conservan los requisitos actuales: «Ninguno» sigue sin exigir nuevos adjuntos y los demás requisitos siguen funcionando.

- El envío acredita provisionalmente conforme a su fecha: puntual o con el descuento de recuperación que corresponda.
- Aprobar confirma ese resultado. La demora del supervisor no cambia la fecha de envío.
- Declarar «No satisfecha» antes del cierre descuenta los puntos de esa tarea.
- Devoluciones/correcciones deben utilizar su propia fecha de envío para evaluar el cumplimiento, sin crear puntos adicionales por el mismo trabajo. Falta precisar casos de devolución todavía sin corregir al corte; ver sección 9.

Calendario acordado:

1. Ejecución: lunes a domingo.
2. Fin del domingo: límite de ejecución de la semana. Una tarea pendiente pierde el 100%, incluso si es Flexible asignada ese domingo. Las enviadas el domingo conservan los descuentos previos que correspondan.
3. Lunes siguiente hasta las 12:00: revisión de lo enviado hasta el domingo. **Ampliar la revisión no amplía la ejecución.** Entregar o corregir el lunes no recupera puntos de la semana anterior.
4. Lunes a las 12:00: cierre definitivo, aunque existan entregas sin revisar.
5. Sábado posterior: pago en el sistema que corresponda.

Si al cierre una entrega sigue **En revisión**, se fija para el bono el resultado provisional correspondiente a su fecha de envío. No equivale a aprobación operativa ni garantiza todos los puntos: una Flexible enviada en recuperación mantiene el 50% descontado.

Las decisiones operativas posteriores al cierre no modifican el bono cerrado. Consecuencia aceptada: una entrega sin revisar al corte puede conservar puntos aunque después se declare no satisfactoria.

Durante la semana el velocímetro refleja descuentos y cambios provisionalmente; el lunes al mediodía el resultado se vuelve definitivo. La nueva semana se evalúa por separado, sin mezclarla con la revisión de la anterior.

## 5. Base evaluable y atribución

Se conservan las respuestas del dueño que no fueron sustituidas por aclaraciones posteriores:

- Incluir asignaciones efectivamente exigibles en la semana: recurrentes, ocasionales, por progreso y etapas de procesos.
- No penalizar etapas aún bloqueadas o no disponibles. Una etapa participa cuando está disponible, tiene responsable y vence en la semana; el atraso de la etapa anterior no se atribuye automáticamente a quien no podía ejecutar la siguiente.
- En progreso, evaluar la meta al vencer; compromisos parciales solo si ya están registrados.
- No duplicar puntos por etapa y proceso completo, ni por correcciones o recuperación del mismo trabajo.
- La reasignación no transfiere el atraso a la nueva responsable ni borra automáticamente el incumplimiento original. La recuperación queda vinculada, con responsable y plazo operativos propios. El trabajo adicional distinto requiere asignación expresa.
- Para tareas compartidas planificadas, el dueño indicó repartir puntos proporcionalmente a minutos asignados: 10 puntos con participación de 60 y 40 minutos corresponden a 6 y 4 puntos. Esta es una regla de evaluación, no autorización para rediseñar el reparto operativo. Verificar si los datos actuales permiten aplicarla.
- Considerar descansos y ausencias autorizadas para determinar qué tareas corresponden; un arrastre posterior no perdona atraso. Queda por concretar el tratamiento evaluativo de cancelaciones, exclusiones e incidencias distintas del arrastre.

Las tareas futuras de la semana no son incumplidas ni realizadas. Al incluirlas en la base, puede aparecer 100% provisional sin ninguna tarea terminada si todavía no hay descuentos. Es consecuencia de la fórmula, no una medida de avance.

Semanas sin puntos:

- Si no trabajó durante toda la semana y no tiene puntos: mostrar «Sin tareas evaluables» y bono de tareas 0%, sin inventar una calificación de incumplimiento ni dividir entre cero.
- Si trabajó pero no tiene tareas evaluables: señalar a Dirección; no aplicar automáticamente la regla anterior. La respuesta del dueño pide resolverlo antes del cierre; falta concretar el resultado si llega sin resolver a las 12:00.

## 6. Frontend: decidido y pendiente

Decidido:

- Nombre: **Bono de tareas**.
- Velocímetro con calificación semanal y, por separado, porcentaje de bono conservado: 100%, 50% o 0%.
- Identificar que solo evalúa tareas; mostrar semana y distinguir provisional de cerrado.
- Conservar pendientes y vencidas en la pantalla habitual y reutilizar sus estados operativos.
- Sin pesos, integración de ventas ni desglose extenso en la vista principal. Conservar trazabilidad para Coordinación y Dirección.

Estados actuales relevantes, verificados en el diagnóstico: Por activar, Activa, Último 30% del tiempo, Último 10% del tiempo, Vencida, Entregada/En revisión, Concluida, No satisfecha y Reasignada. En procesos también existen Programada, En espera de la etapa anterior y Pendiente de asignación. No son estados nuevos del bono ni equivalen directamente a un descuento.

Para el siguiente chat: definir ubicación/composición del indicador, selección de semana, convivencia de semana actual y anterior los lunes, presentación móvil/escritorio, actualización, accesibilidad y estados sin base/error/carga. Reutilizar estilo y componentes actuales. No diseñar otra pantalla de ejecución ni cambiar acciones existentes.

## 7. API: objetivo, sin contrato aprobado todavía

El usuario quiere consultar posteriormente estos datos desde otro sistema. **No hay endpoint, esquema, autenticación ni contrato final aprobados.** La propuesta previa de un factor `1 / 0.5 / 0` es una alternativa técnica, no una decisión cerrada sobre la API.

Definir en el próximo chat:

- Si devuelve calificación, porcentaje de bono conservado, factor o los tres; evitar confundir sus unidades.
- Identificación estable de persona, semana y sucursal/ámbito aplicable.
- Consulta individual o por lote y acceso a semanas históricas.
- Si expone provisionales y cerrados; distinguir el estado económico del estado operativo de revisión.
- Representación de «Sin tareas evaluables» y casos sin resultado resoluble; no confundir ausencia de dato con factor cero.
- Fecha de cálculo/cierre, precisión, versión de reglas y estabilidad del resultado cerrado.
- Autenticación, permisos de solo lectura y errores; no exponer adjuntos ni información innecesaria.
- Cómo recupera el cierre si la aplicación estuvo apagada y cómo evita cambios por revisiones posteriores al corte.

El consumidor calculará el bono final con sus demás condiciones. Consultar repetidamente no debe duplicar descuentos ni producir resultados históricos distintos. El cierre semanal debe persistirse o reconstruirse de forma verificable con hechos al corte; el mecanismo técnico queda por diseñar.

## 8. Decisiones que sustituyen documentos anteriores

Prevalecen las aclaraciones del usuario en este chat sobre la respuesta inicial del dueño y sobre el diagnóstico anterior:

| Propuesta anterior | Decisión vigente |
|---|---|
| Recuperabilidad independiente de importancia | Solo Flexibles recuperables; usar criticidad existente |
| Arrastre cambia vencimiento evaluable o semana | Solo cambia ejecución operativa; mantiene día original y descuentos |
| Aclaraciones hasta el siguiente día laborable tras devolución | No amplían ejecución ni cierre del lunes a las 12:00 |
| Mantener abierto el bono por revisiones pendientes | Cerrar a las 12:00 conservando el resultado de envíos aún en revisión |
| Revisores por especialidad por aclarar | Conservar supervisores actuales por rol |
| Agregar requisitos de evidencia para el bono | Usar envío a revisión y requisitos actuales, incluido Ninguno |

## 9. Bordes pendientes: no inventar reglas durante implementación

No reabrir las decisiones anteriores. Sí resolver, con impacto limitado a evaluación:

1. Fecha de inicio del cálculo y tratamiento de registros anteriores, cambios de clasificación y pendientes históricos. Evitar recalcular el pasado con parámetros actuales.
2. Tarea devuelta antes del corte, sin nuevo envío ni cierre «No satisfecha»: definir su resultado frente a una entrega que simplemente sigue sin revisar. El usuario definió expresamente estos últimos dos estados, no todas las combinaciones de devolución.
3. Cancelación/exclusión autorizada y transferencia antes del vencimiento: determinar base y responsable evaluado. Un arrastre por sí solo no autoriza exclusión ni perdón.
4. Persona que trabajó sin base evaluable y llega al cierre sin resolución: definir representación final sin contradicción con el corte obligatorio.
5. Identificación del día original y de la obligación única en correcciones, transferencias, procesos y compartidas; validar que los datos existentes bastan sin modificar la operación.
6. Delimitar técnicamente los eventos exactamente a las 12:00 y la concurrencia entre revisión y cierre; usar una regla consistente en consulta, persistencia y API.

## 10. Comprobaciones mínimas futuras

Estado de todos los escenarios: **propuesto, no ejecutado**. Entorno futuro: calculador con reloj controlado y base aislada; navegador real para frontend y HTTP para API.

| Escenario | Resultado esperado |
|---|---|
| Seis repeticiones Importantes | Base de 30 puntos, sin duplicación |
| 85%, 95% y valores inmediatamente inferiores | Rangos exactos sin redondeo previo |
| Flexible enviada al día siguiente | Descuento de 0.5 puntos |
| Importante arrastrada y enviada al día siguiente | Descuento de 5 puntos sobre día original |
| Flexible arrastrada más allá de recuperación | Descuento de 1 punto, sin base nueva en otra semana |
| Flexible del domingo sin envío al finalizar domingo | Descuento del 100% |
| Envío domingo, aprobación lunes antes del corte | Puntualidad por envío; aprobación no genera atraso |
| Envío pendiente de revisión al corte | Resultado provisional fijado como resultado económico definitivo |
| No satisfecha antes del corte / después del corte | Antes descuenta; después no altera bono cerrado |
| Corrección enviada el lunes | No recupera puntos de la semana anterior |
| Arrastre/reasignación y consultas repetidas | Sin descuentos ni puntos duplicados; operación intacta |
| Semana sin base | Estado explícito conforme al caso; sin división por cero |
| Lunes revisando semana anterior y ejecutando nueva | Cálculos separados por semana |
| API repetida, cierre concurrente y recuperación tras apagado | Resultado coherente al corte, sin incorporar revisiones tardías |
| Frontend móvil/escritorio y estados de carga/error/sin base | Lectura accesible, unidades y semana inequívocas |

## 11. Referencias y continuación

- [Diagnóstico inicial de código](analisis-bono-tareas-2026-11-02.md): útil para localizar fuentes, pero sus preguntas y propuestas quedan sustituidas donde este documento indica decisiones posteriores.
- Leer `AGENTS.md` y `docs/desarrollo/PROTOCOLO_IMPLEMENTACION.md` antes de diseñar/implementar; para interfaz, `docs/design/ESTILO_VISUAL_V2.md` y componentes vigentes.
- Puntos de partida técnicos: `OperationalTasks.cs`, `OperationalWorkPlan.cs`, `OperationalTaskService.cs`, `OperationalSupervisionService.cs`, `OperationalCarryover.cs` y sus parciales, en `src/Sgol.Web/Infrastructure/Persistence/Planning/`; `Pages/MyWork/Index.Operational.cs` y `_OperationalTaskTable.cshtml` para presentación.
- Existen modificaciones locales ajenas a este documento. Inspeccionar el estado actual antes de continuar; no atribuirlas a este trabajo ni sobrescribirlas.

**Solicitud sugerida para el siguiente chat:** «Lee este documento como decisiones vigentes. Diseñemos el frontend de Bono de tareas y el contrato de consulta de la API. Conserva toda la operación actual; los cambios solo deben afectar el cálculo, su persistencia y su presentación/consulta. Distingue lo acordado de los bordes pendientes y verifica el código actual antes de proponer implementación».
