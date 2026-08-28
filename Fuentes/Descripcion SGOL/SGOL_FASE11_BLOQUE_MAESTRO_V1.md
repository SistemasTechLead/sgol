# SGOL — Fase 11 · Evidencias

## Propósito del dominio

El dominio de evidencias conserva las pruebas que demuestran que una ejecución produjo el resultado esperado o que existe un soporte verificable asociado a ella. Su responsabilidad es determinar qué evidencia es obligatoria, qué modalidades están permitidas, registrar una o varias evidencias sin perder trazabilidad y comprobar su integridad estructural antes de permitir el avance del ciclo operativo.

La presencia de una evidencia no equivale a aprobación. F11 comprueba existencia, tipo, asociación e integridad estructural; F12 determina si la evidencia demuestra cumplimiento y emite la decisión de validación.

## Principios obligatorios

- Una evidencia es un hecho persistente independiente de la ejecución; no se almacena como un conjunto fijo de columnas dentro de `EjecucionTarea`.
- Toda evidencia debe estar asociada a una ejecución identificable.
- Una ejecución puede tener cero, una o múltiples evidencias según sus requisitos efectivos.
- La obligatoriedad se representa mediante requisitos de evidencia; la ausencia de requisitos significa que no existe evidencia obligatoria.
- `NO_APLICA` no es un tipo de evidencia. Es ausencia de requisito y no debe formar parte del catálogo objetivo de tipos.
- Un requisito que admite o exige varios tipos debe expresar de forma explícita su lógica de cumplimiento: todos, cualquiera o una cardinalidad mínima. Nunca se infiere la semántica a partir de un texto concatenado.
- Los tipos de evidencia son catálogos administrados por el negocio y definen qué formas de referencia son compatibles.
- Un registro de evidencia conserva quién lo registró y cuándo se registró.
- Una evidencia no se sobrescribe silenciosamente. Cuando deba sustituirse, la relación de reemplazo conserva trazabilidad.
- La evidencia puede referenciar un recurso, liga, folio u observación según la modalidad permitida por su tipo.
- El requisito de evidencia aplicable debe quedar determinado por la versión de la definición de tarea y el contexto operativo vigente de la obligación.
- Los criterios de suficiencia o cumplimiento pertenecen a F12. F11 sólo evalúa estructura, presencia, asociación y tipo permitido.
- La conclusión de una ejecución que exige evidencia no puede avanzar al siguiente gate si los requisitos obligatorios de F11 no están satisfechos.

## Mapa del dominio

1. **Tipo de evidencia**: catálogo de modalidades permitidas.
2. **Requisito de evidencia**: configuración que establece qué pruebas exige una definición de tarea y bajo qué cardinalidad.
3. **Tipos permitidos por requisito**: relación entre requisito y modalidades aceptadas.
4. **Evidencia registrada**: hecho concreto aportado para una ejecución.
5. **Referencia de evidencia**: referencia verificable asociada a una evidencia, cuando corresponda.
6. **Evaluación estructural de evidencia**: resultado derivado que determina si se cumplen los requisitos mínimos para avanzar.
7. **Vistas de evidencia**: proyecciones para responsables, supervisores, validadores, reportes y auditoría.

## Entidades persistentes

### TipoEvidencia

Catálogo de modalidades válidas de prueba.

**Tipos iniciales**
- `FOTO`;
- `CHECKLIST`;
- `FORMATO_FISICO`;
- `FORMATO_DIGITAL`;
- `DOCUMENTO`;
- `POS_SISTEMA`;
- `FOLIO_JOTFORM`;
- `OBSERVACION_SUPERVISOR`;
- `WHATSAPP_AUTORIZADO`.

**Atributos relevantes**
- `id_tipo_evidencia` o código estable;
- nombre;
- descripción;
- permite referencia tipo link;
- permite referencia tipo folio;
- permite observación;
- estado activo/inactivo;
- vigencia cuando sea necesaria.

Un tipo inactivado no puede utilizarse en nuevos registros, pero las evidencias históricas que lo utilizaron conservan su referencia al catálogo.

### RequisitoEvidencia

Configuración persistente asociada a una versión de definición de tarea o a una regla contextual explícita.

**Atributos relevantes**
- `id_requisito_evidencia`;
- `id_version_tarea` o definición aplicable;
- descripción de la evidencia esperada;
- `modo_cumplimiento`: `TODAS`, `CUALQUIERA` o `AL_MENOS_N`;
- `cantidad_minima` cuando aplique;
- vigencia;
- prioridad/orden cuando existan varios grupos de requisito.

La existencia de al menos un requisito obligatorio vigente hace que la evidencia sea obligatoria para la ejecución correspondiente. No se duplica un booleano `requiere_evidencia` como fuente independiente de verdad.

### RequisitoEvidenciaTipo

Relación entre un requisito y uno o más `TipoEvidencia` permitidos o exigidos.

**Atributos relevantes**
- `id_requisito_evidencia`;
- `id_tipo_evidencia`;
- cantidad mínima específica cuando la regla lo requiera;
- condición contextual opcional si está formalmente definida.

### Evidencia

Hecho persistente registrado para una ejecución.

**Atributos relevantes**
- `id_evidencia` opaco y único;
- `id_ejecucion`;
- `id_tipo_evidencia`;
- descripción u observación del aportante cuando corresponda;
- `registrada_en`;
- `registrada_por`;
- `id_evidencia_reemplazada`, nullable cuando sea una sustitución;
- estado de integridad estructural cuando se materialice para auditoría.

Una ejecución puede conservar múltiples evidencias del mismo tipo o de tipos distintos. La multiplicidad se controla mediante los requisitos, no mediante columnas repetidas.

### ReferenciaEvidencia

Referencia verificable asociada a una evidencia cuando el tipo la permite.

**Atributos relevantes**
- `id_referencia_evidencia`;
- `id_evidencia`;
- clase de referencia permitida por el tipo;
- valor de referencia;
- metadatos mínimos de integridad o localización cuando existan.

Una referencia no debe mezclar semánticamente link, ruta, folio y observación en un único valor sin indicar su clase.

## Objetos derivados; no tablas por defecto

### RequiereEvidencia

Valor derivado de los requisitos vigentes aplicables a la ejecución.

### CumplimientoEstructuralEvidencia

Resultado calculado que indica si la ejecución posee la cantidad y los tipos de evidencia requeridos, con referencias compatibles con cada modalidad.

### VistaEvidenciasEjecucion

Proyección de ejecución, requisitos, evidencias, tipos, referencias y datos de captura para UX, validación y auditoría.

### PendientesEvidencia

Vista de ejecuciones concluidas o próximas a concluir cuyos requisitos obligatorios todavía no están satisfechos.

## Relaciones

- una versión de `DefinicionTarea` puede tener cero o varios `RequisitoEvidencia`;
- un `RequisitoEvidencia` se relaciona con uno o varios `TipoEvidencia`;
- una `EjecucionTarea` puede tener múltiples `Evidencia`;
- una `Evidencia` pertenece exactamente a una `EjecucionTarea`;
- una `Evidencia` usa exactamente un `TipoEvidencia`;
- una `Evidencia` puede tener cero o varias `ReferenciaEvidencia` según su tipo;
- F12 consume las evidencias de F11 para emitir validaciones, sin apropiarse de sus registros.

## Reglas de negocio

1. No se puede registrar evidencia contra una ejecución inexistente.
2. El tipo de evidencia debe existir, estar permitido por el requisito aplicable y ser válido en el contexto temporal de captura.
3. Si no existen requisitos de evidencia, la ejecución puede concluir sin evidencia salvo otra regla explícita.
4. Si existen requisitos obligatorios, `concluir_ejecucion` debe consultar F11 y bloquear el avance cuando el resultado estructural no sea satisfactorio.
5. Cuando un requisito define varios tipos, debe especificar `TODAS`, `CUALQUIERA` o `AL_MENOS_N`; un conjunto de tipos sin modo declarado es una configuración inválida.
6. Un tipo no puede aceptar una clase de referencia que su catálogo prohíbe.
7. La existencia de evidencia no implica que ésta sea suficiente, correcta o aprobada; esa decisión pertenece a F12.
8. Una misma evidencia no puede quedar asociada simultáneamente a ejecuciones distintas.
9. La sustitución de evidencia conserva la anterior y registra la relación de reemplazo; no se destruye trazabilidad.
10. Los recursos externos deben conservar una referencia estable suficiente para volver a localizarlos mientras la política de retención lo requiera.
11. El registro de evidencia debe conservar actor y timestamp del sistema.
12. Las evidencias históricas permanecen interpretables aunque un tipo sea inactivado posteriormente.
13. Las reglas de permisos de F2 determinan quién puede registrar, consultar o sustituir evidencia; la autoridad de validación de F12 no concede por sí sola permiso de edición.

## Funciones y consultas

- `obtener_requisitos_evidencia(id_ejecucion)`;
- `requiere_evidencia(id_ejecucion)`;
- `obtener_tipos_evidencia_permitidos(id_ejecucion)`;
- `validar_tipo_evidencia(id_ejecucion, id_tipo_evidencia)`;
- `validar_referencia_evidencia(id_tipo_evidencia, clase_referencia, valor)`;
- `listar_evidencias_ejecucion(id_ejecucion)`;
- `evaluar_cumplimiento_estructural_evidencia(id_ejecucion)`;
- `explicar_faltantes_evidencia(id_ejecucion)`;
- `obtener_evidencia_vigente(id_evidencia)` cuando existan sustituciones.

## Comandos transaccionales

### registrar_evidencia

Debe:
1. validar existencia de la ejecución;
2. validar permiso del actor;
3. resolver el requisito aplicable;
4. validar que el tipo esté permitido;
5. validar que las referencias suministradas sean compatibles con el tipo;
6. crear la evidencia y sus referencias de forma atómica;
7. registrar actor y timestamp;
8. emitir `EvidenciaRegistrada` después del commit.

### sustituir_evidencia

Debe:
1. validar que la evidencia original exista y pertenezca a la ejecución indicada;
2. validar permiso y mutabilidad del período;
3. crear una nueva evidencia;
4. vincularla con la evidencia sustituida;
5. conservar la evidencia anterior;
6. emitir `EvidenciaSustituida` después del commit.

## Eventos

- `EvidenciaRegistrada`;
- `EvidenciaSustituida`;
- `RequisitosEvidenciaSatisfechos` cuando sea útil para orquestar F10/F12.

Los eventos no sustituyen las entidades persistentes.

## Permisos

- El responsable de ejecución puede registrar evidencia dentro de su ejecución cuando F2 lo autorice.
- La consulta por supervisores y validadores respeta alcance organizativo y de rol.
- Sustituir evidencia después de una conclusión o validación requiere permiso explícito y trazabilidad reforzada.
- El cierre de F14 bloquea nuevas altas o sustituciones salvo una ruta administrativa extraordinaria formalmente autorizada.

## Dependencias

- **F2**: identidad, roles, alcance y permisos.
- **F4**: versión de definición de tarea a la que pertenece el requisito.
- **F6**: instancia de obligación y contexto operativo.
- **F9**: partida publicada que conduce a la ejecución.
- **F10**: ejecución a la que se adjunta la evidencia y gate de conclusión.
- **F12**: criterio de suficiencia, aprobación o rechazo de la evidencia.
- **F14**: bloqueo postcierre y retención.
- **F17**: auditoría de altas, sustituciones y accesos críticos.
- **F19**: adaptadores de repositorios o plataformas externas cuando correspondan.

## Entradas y salidas

**Entradas**
- ejecución objetivo;
- versión de definición de tarea;
- requisitos vigentes;
- tipo de evidencia;
- referencia o referencias;
- observación cuando corresponda;
- actor y contexto de autorización.

**Salidas**
- evidencia registrada;
- referencias asociadas;
- resultado estructural de cumplimiento;
- faltantes explicables;
- eventos de dominio posteriores al commit.

## Errores de dominio

- `EJECUCION_NO_ENCONTRADA_PARA_EVIDENCIA`;
- `REQUISITO_EVIDENCIA_NO_CONFIGURADO`;
- `MODO_REQUISITO_EVIDENCIA_INVALIDO`;
- `TIPO_EVIDENCIA_NO_PERMITIDO`;
- `TIPO_EVIDENCIA_INACTIVO`;
- `REFERENCIA_EVIDENCIA_INCOMPATIBLE`;
- `REFERENCIA_EVIDENCIA_INVALIDA`;
- `EVIDENCIA_REQUERIDA_FALTANTE`;
- `CARDINALIDAD_EVIDENCIA_INCOMPLETA`;
- `EVIDENCIA_NO_ENCONTRADA`;
- `EVIDENCIA_NO_PERTENECE_A_EJECUCION`;
- `EVIDENCIA_BLOQUEADA_POSTCIERRE`;
- `ACTOR_NO_AUTORIZADO_PARA_EVIDENCIA`.
