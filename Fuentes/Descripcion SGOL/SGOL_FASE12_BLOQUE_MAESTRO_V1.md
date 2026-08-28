# SGOL — Fase 12 · Validación, supervisión y autorizaciones

## Propósito del dominio

El dominio de validación determina si una ejecución concluida satisface los criterios de negocio aplicables, identifica qué autoridad puede emitir esa decisión, conserva la decisión y sus fundamentos y exige autorizaciones adicionales cuando una política sensible lo requiera.

La validación no sustituye la ejecución, la evidencia, la excepción operativa ni el cálculo económico. Consume información de esos dominios y produce una decisión auditable de cumplimiento.

## Principios obligatorios

- Una validación es una decisión persistente e independiente de `EjecucionTarea`; no se representa sobrescribiendo el estado de ejecución.
- La ejecución debe estar concluida antes de emitir una decisión ordinaria de validación.
- Toda definición de tarea que requiera validación debe tener una política de validación vigente y un criterio verificable.
- La autoridad para validar se resuelve desde una única política vigente y desde los permisos de F2. Etiquetas de puestos o nombres de personas no constituyen autoridad por sí mismos.
- La ausencia de una política de autoridad inequívoca bloquea la validación.
- `CUMPLIDA`, `INCOMPLETA` y `NO_CUMPLIDA` son resultados de validación.
- Arrastre, posposición, cancelación, reasignación y otras excepciones de ciclo de vida pertenecen a F13 y no son resultados de validación.
- Una ejecución que exige evidencia no puede recibir resultado `CUMPLIDA` mientras F11 reporte requisitos de evidencia incompletos.
- La falta de evidencia puede fundamentar `INCOMPLETA` o `NO_CUMPLIDA` cuando la política y el criterio aplicable así lo determinen.
- El impacto económico no se escribe durante la validación. F15 calcula bono o KPI a partir de la decisión de validación, las políticas económicas y las excepciones aplicables.
- Las decisiones no se sobrescriben silenciosamente. Cualquier corrección conserva la decisión anterior, el actor, la fecha y el motivo de la sustitución o revocación.
- Una validación masiva debe ejecutar exactamente los mismos gates que una validación individual y debe conservar una correlación de lote sin perder el resultado individual de cada ejecución.
- La política debe declarar si la autovalidación está permitida. Si no existe una regla explícita, la operación se considera no autorizada.
- El cierre de período de F14 bloquea nuevas validaciones ordinarias y cualquier corrección posterior requiere una ruta extraordinaria formalmente autorizada.
- Una autorización excepcional es un hecho distinto de un permiso permanente y distinto de la validación que eventualmente habilite.

## Mapa del dominio

1. **Política de validación**: define para una tarea o versión qué autoridad, criterios y gates aplican.
2. **Criterio de validación**: condición de negocio que debe evaluarse para determinar cumplimiento.
3. **Decisión de validación**: hecho auditable emitido sobre una ejecución concluida.
4. **Evaluación de criterio**: detalle opcional y auditable de cada criterio evaluado.
5. **Autoridad efectiva**: resultado derivado de política, usuario, roles, permisos, alcance y separación de funciones.
6. **Autorización**: decisión adicional requerida para operaciones sensibles, reutilizando el dominio de seguridad de F2.
7. **Colas y vistas de supervisión**: proyecciones derivadas de ejecuciones concluidas que todavía no tienen una decisión vigente.

## Entidades persistentes

### PoliticaValidacion

Configuración persistente y versionada que define cómo se valida una obligación.

**Atributos relevantes**
- `id_politica_validacion`;
- `id_version_tarea` o referencia equivalente a la definición vigente;
- `requiere_validacion`;
- `codigo_permiso_validador` o regla de autoridad;
- `tipo_alcance_validador`;
- `permite_autovalidacion`;
- `requiere_autorizacion_adicional`;
- `tipo_autorizacion_requerida` nullable;
- `vigente_desde`;
- `vigente_hasta` nullable;
- `activa`.

**Reglas**
- Debe existir como máximo una política efectiva no ambigua para el mismo contexto de validación.
- La política no debe identificar al validador por nombre visible.
- Cuando la autoridad dependa de un rol, puesto o nivel organizacional, la equivalencia se resuelve mediante configuración de F2.
- Una política incompleta no puede habilitar validación productiva.

### CriterioValidacion

Regla verificable que define qué significa cumplimiento para una versión de tarea.

**Atributos relevantes**
- `id_criterio_validacion`;
- `id_politica_validacion`;
- `codigo_criterio`;
- `descripcion`;
- `orden_evaluacion`;
- `obligatorio`;
- `activo`.

**Reglas**
- Los criterios múltiples deben conservar identidad y orden propios; no se depende de un texto concatenado para auditar qué condición falló.
- Un criterio puede requerir evidencia de F11, datos operativos o una comprobación humana.
- La ausencia de criterios en una política que exige validación es un error de configuración.

### Validacion

Decisión persistente sobre una ejecución.

**Atributos relevantes**
- `id_validacion`;
- `id_ejecucion`;
- `id_politica_validacion`;
- `id_usuario_validador`;
- `fecha_hora_validacion`;
- `resultado`;
- `observaciones`;
- `motivo_no_cumplimiento` nullable;
- `id_validacion_anterior` nullable cuando sustituya una decisión;
- `estado_decision` para distinguir una decisión vigente de una revocada o sustituida;
- `id_correlacion_lote` nullable.

**Resultados permitidos**
- `CUMPLIDA`;
- `INCOMPLETA`;
- `NO_CUMPLIDA`.

**Reglas**
- Una validación debe identificar al usuario real que tomó la decisión.
- Una ejecución no puede tener dos decisiones vigentes incompatibles.
- `NO_CUMPLIDA` requiere motivo explícito.
- `INCOMPLETA` debe identificar al menos los criterios no satisfechos o una observación equivalente y trazable.
- `CUMPLIDA` requiere todos los gates obligatorios satisfechos.
- La validación no modifica directamente el bono ni el KPI.

### EvaluacionCriterio

Detalle persistente cuando la política requiere demostrar cómo se obtuvo la decisión.

**Atributos relevantes**
- `id_evaluacion_criterio`;
- `id_validacion`;
- `id_criterio_validacion`;
- `resultado_criterio`;
- `observacion` nullable;
- referencias a evidencias o datos de soporte cuando corresponda.

**Reglas**
- Debe conservar el criterio exacto y su versión aplicable al momento de la decisión.
- No modifica la evidencia de F11; sólo la referencia como fundamento.

### Autorizacion

Se reutiliza la entidad definida en F2 cuando una operación requiere autoridad adicional.

**Reglas adicionales de F12**
- Una autorización previa puede ser gate de una validación, pero no reemplaza la decisión de validación.
- Debe referenciar la operación concreta, el usuario autorizador, el motivo, el alcance y el resultado.
- Las autorizaciones de arrastre, posposición, cancelación y reasignación se especifican en F13.
- Las autorizaciones de cierre se especifican en F14.

## Datos derivados y vistas

### PendientesValidacion

Vista de ejecuciones concluidas que requieren validación y no tienen una decisión vigente.

Debe exponer al menos:
- ejecución;
- tarea y contexto;
- responsable de ejecución;
- política de validación aplicable;
- autoridad requerida;
- estado estructural de evidencia de F11;
- vencimiento o prioridad de supervisión cuando corresponda.

### AutoridadValidacionEfectiva

Resultado derivado de:
- usuario autenticado;
- roles y permisos vigentes de F2;
- alcance organizacional;
- política de validación;
- identidad del ejecutor;
- regla de autovalidación;
- estado del período.

No debe persistirse como tabla de combinaciones.

### EstadoValidacionEjecucion

Derivado de la existencia y vigencia de decisiones:
- `PENDIENTE`;
- `CUMPLIDA`;
- `INCOMPLETA`;
- `NO_CUMPLIDA`.

No modifica el estado operativo de F10.

## Gates de validación

Antes de confirmar una decisión, el sistema debe comprobar:

1. la ejecución existe;
2. la ejecución está concluida;
3. el período no está cerrado o existe una ruta extraordinaria válida;
4. existe una política de validación vigente;
5. existen criterios de validación suficientes;
6. el usuario autenticado tiene el permiso y alcance requeridos;
7. se cumple la regla de separación de funciones o autovalidación;
8. no existe una decisión final vigente incompatible;
9. para `CUMPLIDA`, F11 confirma todos los requisitos de evidencia obligatorios;
10. cualquier autorización adicional requerida está vigente y corresponde a la misma operación;
11. el resultado pertenece al catálogo permitido;
12. los motivos y evaluaciones obligatorios están presentes.

## Funciones

### `obtener_politica_validacion(id_ejecucion, fecha)`
Resuelve una única política vigente para la definición y contexto de la ejecución.

### `obtener_criterios_validacion(id_politica_validacion)`
Devuelve los criterios vigentes y su orden de evaluación.

### `resolver_autoridad_validacion(id_ejecucion, id_usuario, fecha)`
Evalúa política, roles, permisos, alcance y separación de funciones. Devuelve autorización efectiva o causa estructurada de denegación.

### `puede_validar(id_ejecucion, id_usuario, resultado_propuesto)`
Ejecuta los gates de autoridad, estado, evidencia y configuración sin cambiar datos.

### `evaluar_gate_evidencia_para_validacion(id_ejecucion, resultado_propuesto)`
Consulta F11. `CUMPLIDA` exige cumplimiento estructural completo cuando la evidencia es obligatoria.

### `obtener_decision_validacion_vigente(id_ejecucion)`
Devuelve la decisión vigente o indica que la ejecución está pendiente.

### `explicar_bloqueo_validacion(id_ejecucion, id_usuario, resultado_propuesto)`
Devuelve causas como falta de permiso, criterio, evidencia, autorización o mutabilidad.

## Comandos y transacciones

### `validar_ejecucion`

Debe:
1. resolver ejecución, política y criterios;
2. validar autoridad del actor;
3. validar mutabilidad del período;
4. validar evidencia según el resultado propuesto;
5. validar motivo y evaluaciones requeridas;
6. impedir duplicidad de decisión vigente;
7. crear `Validacion` y `EvaluacionCriterio` de forma atómica;
8. registrar correlación y actor;
9. emitir `EjecucionValidada` después del commit.

### `validar_lote`

Debe:
- recibir un conjunto explícito de ejecuciones y el resultado propuesto;
- ejecutar los mismos gates de `validar_ejecucion` por cada elemento;
- no convertir una selección en cumplimiento sin evaluar cada ejecución;
- conservar un identificador de correlación común;
- aplicar la política transaccional definida para el lote y devolver detalle por elemento.

### `sustituir_decision_validacion`

Ruta extraordinaria para corregir una decisión sin destruir histórico. Debe exigir permiso específico, motivo y correlación con la decisión anterior.

## Eventos

- `EjecucionValidada`;
- `DecisionValidacionSustituida`;
- `ValidacionBloqueada` cuando su auditoría aporte valor operativo;
- `AutorizacionRegistrada`, reutilizado desde F2.

Los eventos se emiten después del commit y no sustituyen la persistencia de la decisión.

## Permisos y autoridad

La matriz inicial de F2 se mantiene como base:

- `SUBCOORDINACION` puede validar tareas de `PISO_VENTAS` cuando la política de la tarea así lo permita.
- `DIRECCION` puede validar tareas de `SUBCOORDINACION` y `ADMINISTRACION` cuando la política aplicable así lo establezca.
- `PISO_VENTAS` no emite validaciones ordinarias.
- La validación de tareas ejecutadas por `DIRECCION` requiere una política explícita antes de existir en operación; no se infiere una cadena superior inexistente.
- `AUTORIZAR_EXCEPCIONES` no equivale a `VALIDAR_*` y no permite saltar una política ordinaria de validación.

## Relaciones

- `DefinicionTareaVersion 1:N PoliticaValidacion` a lo largo del tiempo.
- `PoliticaValidacion 1:N CriterioValidacion`.
- `EjecucionTarea 1:N Validacion` históricas, con máximo una decisión vigente según política.
- `Validacion 1:N EvaluacionCriterio`.
- `Validacion N:1 Usuario` como actor validador.
- `Validacion` puede referenciar una `Autorizacion` cuando la política la exige.
- `EvaluacionCriterio` puede referenciar evidencias de F11 sin apropiarse de ellas.
- F15 consume la decisión de F12 para calcular impacto económico.

## Dependencias

- **F2**: usuarios, roles, permisos, alcance y autorizaciones.
- **F4**: definición y versión de tarea; criterios y políticas configurables.
- **F10**: ejecución concluida que se valida.
- **F11**: evidencia y gate estructural de suficiencia.
- **F13**: arrastre, posposición, cancelación, reasignación y sus autorizaciones.
- **F14**: bloqueo postcierre y rutas extraordinarias.
- **F15**: impacto económico, KPI y bono.
- **F16**: colas y UX de supervisión.
- **F17**: auditoría, correlación de lotes, retención y trazabilidad.

## Entradas y salidas

**Entradas**
- ejecución concluida;
- política y criterios vigentes;
- evidencias de F11;
- usuario autenticado y permisos de F2;
- resultado propuesto;
- evaluaciones, motivo y observaciones;
- autorización adicional cuando corresponda.

**Salidas**
- decisión de validación persistida;
- detalle de criterios evaluados;
- estado derivado de validación;
- eventos posteriores al commit;
- causas estructuradas cuando la operación sea bloqueada.

## Errores de dominio

- `EJECUCION_NO_CONCLUIDA_PARA_VALIDAR`;
- `POLITICA_VALIDACION_NO_CONFIGURADA`;
- `POLITICA_VALIDACION_AMBIGUA`;
- `CRITERIO_VALIDACION_NO_CONFIGURADO`;
- `RESULTADO_VALIDACION_INVALIDO`;
- `PERMISO_VALIDACION_DENEGADO`;
- `ALCANCE_VALIDACION_DENEGADO`;
- `AUTOVALIDACION_NO_AUTORIZADA`;
- `EVIDENCIA_INSUFICIENTE_PARA_CUMPLIDA`;
- `MOTIVO_NO_CUMPLIMIENTO_REQUERIDO`;
- `DETALLE_INCOMPLETA_REQUERIDO`;
- `VALIDACION_VIGENTE_DUPLICADA`;
- `AUTORIZACION_ADICIONAL_REQUERIDA`;
- `PERIODO_CERRADO_PARA_VALIDACION`;
- `DECISION_VALIDACION_NO_ENCONTRADA`;
- `SUSTITUCION_VALIDACION_NO_AUTORIZADA`.
