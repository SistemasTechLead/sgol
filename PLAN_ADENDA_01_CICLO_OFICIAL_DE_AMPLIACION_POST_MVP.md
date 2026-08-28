# SGOL — Adenda 01 al plan maestro: ciclo oficial de ampliación post-MVP

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Adenda normativa al `PLAN_EJECUCION_SGOL_CHATGPT_CODEX.md` |
| Estado | APROBADA E INCORPORADA A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación del responsable | `Apruebo la Adenda 01 del plan maestro de SGOL` |
| Efecto | Añade una Fase 11 repetible después del cierre de Fase 10 |
| Conservación | No modifica ni sobrescribe FTE-001 ni los entregables aprobados anteriores |
| Regla de entrada en vigor | Aprobación expresa, incorporación de copia idéntica a `Fuentes` y verificación SHA-256 |

## 2. Motivo y vacío que resuelve

El plan rector ya establece:

1. 186 definiciones de tarea opcionales que no constituyen compromiso de versión;
2. promoción individual basada en necesidad, actor, disparador, resultado, dependencias, efectos y decisión humana;
3. aceptación del MVP en Fase 09;
4. piloto y `F10_CIERRE_Y_BACKLOG_POSTERIOR.md` en Fase 10;
5. control de cambios después del MVP.

Faltaba definir literalmente cómo una tarea pasa desde recomendación hasta una versión implementada y validada. Esta adenda crea ese procedimiento sin ampliar el MVP actual ni autorizar código anticipado.

## 3. Autoridad y relación con el plan rector

Una vez aprobada e incorporada, esta adenda:

- forma parte del plan obligatorio de SGOL para trabajo posterior al MVP;
- conserva sin cambios las Fases 00 a 10 y sus entregables;
- no reabre automáticamente una fase cerrada;
- no promueve ninguna tarea, capacidad o integración por sí misma;
- aplica a cada incremento post-MVP hasta que el responsable declare cerrado el desarrollo evolutivo.

En caso de conflicto, las decisiones aprobadas más recientes y específicas del incremento prevalecen únicamente dentro de su alcance, sin alterar silenciosamente hechos históricos.

## 4. Orden actualizado de puertas

```text
00 → 01 → 02 → 03 → 04 → 05 → 06 → 07 → 08 → 09 → 10
                                                    ↓
                          11.1 → 11.2 → 11.3 → 11.4 → 11.5 → 11.6
                            ↑                                  ↓
                            └──── siguiente incremento ───────┘
```

La Fase 11 es repetible. Cada recorrido usa un identificador `INC-###`, comenzando por `INC-001`. Ningún incremento posterior comparte aprobación, backlog, código o dictamen con otro.

## 5. Condiciones para abrir Fase 11

La ruta ordinaria requiere:

1. Fase 09 con dictamen explícito del MVP;
2. Fase 10 cerrada, con operación estable y backlog posterior separado;
3. fuentes y entregables finales incorporados;
4. ausencia de incidentes críticos abiertos que vuelvan inseguro ampliar el sistema;
5. responsable humano disponible para confirmar necesidad y aprobar alcance.

Si antes de Fase 10 aparece una tarea indispensable para que el MVP funcione, se trata como cambio extraordinario de alcance: debe volver a la puerta de F04, actualizar F05, revisar F06 y actualizar F07 antes de escribir código. La urgencia no permite saltarse esas puertas.

## 6. Tamaño y composición de los incrementos

- Tamaño recomendado: entre una y cinco tareas aprobadas por incremento.
- Más de cinco exige justificación explícita de cohesión, riesgo y capacidad de prueba.
- Las tareas se agrupan por resultado operativo y dependencias, no por numeración legacy.
- Un incremento puede contener cero tareas implementables si el análisis concluye que deben combinarse, reemplazarse o descartarse.
- Las 186 tareas opcionales no forman un compromiso de implementación total.
- TAR-0195 a TAR-0202 y T221 continúan fuera del catálogo futuro. Sólo una decisión formal que revoque DEC-047 podría cambiarlo.

## 7. Estados de una recomendación

| Estado | Significado | Autoriza código |
|---|---|---:|
| IDENTIFICADA | Existe una definición o solicitud que merece revisión. | No |
| EN_ANALISIS | Se están contrastando fuentes y operación actual. | No |
| RECOMENDADA_CONSERVAR | La obligación parece vigente y puede conservar su intención. | No |
| RECOMENDADA_SIMPLIFICAR | La necesidad existe, pero el comportamiento actual debe reducirse. | No |
| RECOMENDADA_COMBINAR | Varias entradas representan una sola obligación útil. | No |
| RECOMENDADA_REEMPLAZAR | La necesidad existe, pero requiere un flujo distinto. | No |
| RECOMENDADA_DESCARTAR | No existe necesidad actual, es duplicada, no es tarea o invade límites. | No |
| APROBADA_PARA_VERSION | El responsable aprobó tarea, alcance e incremento. | Todavía no; falta especificación y backlog |
| ESPECIFICADA | Tiene historia, reglas, permisos, estados, criterios y pruebas aprobados. | No; falta impacto técnico y backlog |
| LISTA_PARA_IMPLEMENTAR | Diseño y backlog aprobados; dependencias satisfechas. | Sí, sólo para historias incluidas |
| IMPLEMENTADA | Código y pruebas del incremento están completos. | No autoriza despliegue |
| VALIDADA | Dictamen de aceptación positivo. | Autoriza promoción según plan de liberación |
| RECHAZADA | No superó una puerta o perdió necesidad. | No |

Una recomendación nunca salta directamente de una definición legacy a `LISTA_PARA_IMPLEMENTAR`.

## 8. Fase 11 — Ciclo de ampliación controlada

### 11.1 — Selección y recomendaciones

**Formato del chat:** `11.1 — INC-### — Selección y recomendaciones`

**Objetivo:** revisar candidatas una por una y rescatar la necesidad operativa, no el diseño legacy.

**Entradas:** `F10_CIERRE_Y_BACKLOG_POSTERIOR.md`, datos del piloto, catálogo F02/F03, clasificación F04, fuentes originales y solicitudes actuales.

**Ficha obligatoria `REC-TAR-####`:**

1. definición y fuentes;
2. problema operativo actual;
3. evidencia de uso o necesidad vigente;
4. forma actual de trabajo y fallas observadas;
5. actor responsable y alcance;
6. frecuencia, disparador o caso de uso real;
7. resultado verificable;
8. datos, evidencia y validación plausibles;
9. dependencias con tareas y capacidades aprobadas;
10. riesgo de nómina, incentivos, integraciones o complejidad heredada;
11. recomendación: conservar, simplificar, combinar, reemplazar o descartar;
12. incertidumbres y preguntas que impiden aprobarla;
13. versión sugerida, sin convertirla todavía en compromiso.

**Entregables:**

- `INC-###_RECOMENDACIONES_Y_EVIDENCIA.md`
- `INC-###_CANDIDATAS_DESCARTADAS_O_COMBINADAS.md`

**Criterios de cierre:** todas las candidatas revisadas tienen fuente, recomendación y nivel de evidencia; ninguna suposición se presenta como hecho; no se ha autorizado código.

### 11.2 — Decisión de alcance del incremento

**Formato del chat:** `11.2 — INC-### — Alcance de ampliación`

**Objetivo:** decidir qué recomendaciones entran a una versión concreta.

Cada tarea sólo puede aprobarse si registra:

1. necesidad operativa actual confirmada;
2. actor y alcance;
3. disparador o frecuencia;
4. resultado objetivamente comprobable;
5. razón para gestionarla en SGOL;
6. dependencias;
7. efecto sobre evidencia, validación, estados, excepciones, indicadores y seguridad;
8. capacidades existentes que reutiliza;
9. capacidades posteriores que sería necesario promover;
10. prioridad frente al costo de no implementarla;
11. decisión humana y versión objetivo.

**Entregables:**

- `INC-###_ALCANCE_APROBADO.md`
- `INC-###_RIESGOS_Y_DEPENDENCIAS.md`

**Criterios de cierre:** lote máximo justificado; cada entrada está aprobada, diferida o rechazada; los límites están explícitos. Sólo las aprobadas pasan a `APROBADA_PARA_VERSION`.

### 11.3 — Especificación funcional del incremento

**Formato del chat:** `11.3 — INC-### — Especificación funcional`

**Objetivo:** especificar las tareas aprobadas con el mismo rigor de F05.

Para cada tarea se exige historia, actor, permisos, precondiciones, flujo, alternativas, errores, reglas, datos, validaciones, estados, criterios, pruebas, dependencias y trazabilidad. También se debe decidir expresamente si basta configuración o se requiere ampliar capacidades.

**Entregables:**

- `INC-###_ESPECIFICACION_FUNCIONAL.md`
- `INC-###_TRAZABILIDAD_Y_PRUEBAS.md`
- actualizaciones controladas a matrices de roles y estados cuando correspondan

**Criterios de cierre:** ninguna historia carece de prueba objetiva; no quedan vacíos funcionales bloqueantes; los documentos están aprobados e incorporados.

### 11.4 — Impacto técnico y backlog

**Formato del chat:** `11.4 — INC-### — Impacto técnico y backlog`

**Objetivo:** evitar código nuevo cuando la solución aprobada cabe en mecanismos existentes.

Cada historia se clasifica como:

- `SOLO_CONFIGURACION`: usa definición, política, evidencia, validación y estados existentes;
- `EXTENSION_PEQUENA`: exige una modificación técnica localizada;
- `CAPACIDAD_NUEVA`: necesita diseño, ADR, datos, API, seguridad y pruebas adicionales;
- `NO_IMPLEMENTABLE_AUN`: mantiene una dependencia o decisión bloqueante.

**Entregables:**

- `INC-###_IMPACTO_TECNICO_Y_ADR.md`
- `INC-###_BACKLOG_IMPLEMENTABLE.md`
- `INC-###_PLAN_DE_PRUEBAS.md`

**Criterios de cierre:** arquitectura y datos actualizados sólo donde exista necesidad; backlog ordenado; cada historia cumple definición de Listo; ninguna tarea `NO_IMPLEMENTABLE_AUN` pasa a código.

### 11.5 — Implementación del incremento

**Formato de cada chat:** `11.5 — INC-### — [HU/resultado concreto]`

**Objetivo:** implementar historias pequeñas del backlog aprobado.

Se aplican las mismas reglas de F08: fuentes autorizadas, criterios verificables, cambios pequeños, pruebas, trazabilidad y prohibición de ampliar alcance. Configuración y código deben distinguirse en el informe.

**Entregable acumulado:** `INC-###_INFORME_DE_IMPLEMENTACION.md`.

**Criterios de cierre:** todas las historias incluidas están implementadas o explícitamente retiradas mediante control de cambios; pruebas unitarias/integración pertinentes pasan; no hay código huérfano ni funciones no aprobadas.

### 11.6 — Validación, liberación y aprendizaje

**Formato del chat:** `11.6 — INC-### — Validación y dictamen`

**Objetivo:** validar el incremento, decidir su liberación y alimentar el siguiente lote.

**Entregables:**

- `INC-###_RESULTADOS_DE_VALIDACION.md`
- `INC-###_REGISTRO_DE_DEFECTOS.md`
- `INC-###_DICTAMEN_Y_LIBERACION.md`
- actualización de `CATALOGO_POST_MVP_ACUMULADO.md`
- actualización del backlog de candidatas restantes

**Criterios de cierre:** cada requisito tiene evidencia; pruebas críticas pasan; no quedan defectos bloqueantes; existe dictamen explícito de aceptar, corregir o rechazar; el siguiente incremento no se abre hasta incorporar los resultados aprobados.

## 9. Regla configuración antes que código

Una tarea nueva no justifica automáticamente un módulo, tabla, servicio, proceso, automatización o pantalla propios.

Orden obligatorio de evaluación:

1. ¿Puede representarse con una definición de tarea versionada?
2. ¿Puede usar alta manual o recurrencia ya disponible?
3. ¿Puede usar roles, asignación, plan, evidencia y validación existentes?
4. ¿Puede resolverse con configuración y pruebas sin código funcional nuevo?
5. Sólo si la respuesta anterior es no, ¿qué capacidad nueva y verificable falta?

No se crea un motor genérico para cubrir candidatas no aprobadas. La extensibilidad se diseña alrededor de variaciones comprobadas, no de posibilidades imaginadas.

## 10. Reglas contra herencia de código inservible

- Una macro, botón, hoja, procedimiento, adaptador, nombre `AUT-*` o estado legacy no demuestra necesidad.
- “Actualmente se hace así” documenta una práctica, no obliga a reproducir su solución.
- “Actualmente se hace mal” justifica investigar el problema, no copiar el flujo defectuoso.
- Texto de puesto no concede rol, autoridad o elegibilidad.
- KPI, checklist, paso, regla, dato o automatización no se convierte en tarea salvo que represente una obligación real con resultado propio.
- Duplicados semánticos deben combinarse antes de especificar.
- Las tareas que invadan incentivos, nómina o integraciones excluidas se rechazan o se limitan a coordinación interna mediante decisión expresa.
- Todo código debe trazar a una historia aprobada, criterio y prueba del incremento.
- Código sin historia aprobada se considera fuera de alcance y no puede integrarse.

## 11. Trazabilidad adicional

Se agregan identificadores permanentes:

- Incremento: `INC-001`.
- Recomendación: `REC-TAR-####`.
- Decisión de incremento: `DINC-###`.
- Historia post-MVP: `HU-INC-###-###`.
- Criterio: `CA-INC-###-###`.
- Prueba: `CP-INC-###-###`.
- Defecto: `DEF-INC-###-###`.
- Versión: `VER-###` conforme al plan rector.

Cadena obligatoria:

```text
Fuente + evidencia operativa → recomendación → decisión de alcance → requisito/regla → historia → criterio → prueba → configuración/código → versión → dictamen
```

## 12. Mensaje para abrir el primer ciclo, cuando corresponda

**Nombre del chat:** `11.1 — INC-001 — Selección y recomendaciones`

```text
Estamos ejecutando la Fase 11.1 del plan maestro de SGOL para el incremento INC-001.

Lee el dictamen del MVP, el cierre operativo de Fase 10, el backlog posterior y las fuentes aprobadas. No escribas código ni promociones tareas por su existencia legacy.

Selecciona un grupo inicial de entre una y cinco tareas relacionadas. Para cada candidata contrasta su definición con la operación actual y registra: necesidad, evidencia de uso, fallas del proceso vigente, actor, frecuencia o disparador, resultado verificable, datos, evidencia, validación, dependencias, riesgos y recomendación de conservar, simplificar, combinar, reemplazar o descartar.

Produce:
1. INC-001_RECOMENDACIONES_Y_EVIDENCIA.md
2. INC-001_CANDIDATAS_DESCARTADAS_O_COMBINADAS.md

No autorices implementación. Cierra únicamente cuando ninguna suposición se presente como hecho y todas las candidatas tengan una recomendación trazable. Después indica exactamente cuándo abrir 11.2 — INC-001 — Alcance de ampliación y proporciona el mensaje listo para copiar.
```

## 13. Aprobación y entrada en vigor

El responsable aprobó expresamente esta adenda el 2026-08-27 mediante el texto `Apruebo la Adenda 01 del plan maestro de SGOL`.

La versión final se conserva en la raíz documental y se incorporó como copia idéntica a `Fuentes`. La igualdad entre ambas copias se comprueba mediante SHA-256. Desde esa comprobación, la Fase 11 repetible forma parte del plan obligatorio para todo trabajo post-MVP.

Su vigencia deberá registrarse además en el próximo documento de control que corresponda, sin modificar ni renumerar FTE-001 a FTE-044.
