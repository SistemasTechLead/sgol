# FRONT-019 — Plan único de validación y supervisión

2026-10-01, America/Mexico_City. **APROBADO ÍNTEGRAMENTE** mediante «Apruebo el plan». Decisiones incorporadas por Adenda 58 antes del código; implementación local autorizada.

Este documento conserva el único plan presentado. La aprobación comprende la totalidad, incluidas las propuestas consumidoras y diferencias de §4. Las expresiones «proponer», «propuesta» y «requiere aprobación» de las secciones originales describen su presentación previa, ya aprobada; los hechos documentados siguen diferenciados de las decisiones consumidoras. Los resultados de ejecución se registran en FRONT_019_VALIDACION_Y_SUPERVISION.md. No hay autorización de publicación, merge o despliegue.

## 1. Resumen del plan (ocho puntos)

1. Implementar exclusivamente la fila 102, secuencia 23, de Adenda 45: UI-V01..V04 para pendientes, primera decisión, sustitución motivada/historia y supervisión de inferiores.
2. Consumir HU-028/HU-031, CA/CP-028/031 y CAT-001..008 mediante las políticas capturadas; aceptar FRONT-018/012/014 y las bases técnicas existentes sin repetir gates.
3. Resolver BR-D09/M09 sólo para esta consumidora mediante §§4–8; excluir descarga/preview expresamente para FRONT-019, sin cierre global de BR-API04.
4. Conservar permisos, jerarquía vigente, los tres resultados, fundamento y motivos separados; mantener CONCLUIDA, snapshots anteriores e historia y reautorizar todas las operaciones en servidor.
5. Reutilizar /validaciones y el detalle existente de tarea; proponer una proyección opcional de autoridad de recurso, consistencia de lectura histórica y corrección acotada del texto contractual, sin endpoint ni persistencia nuevos.
6. Mantener el diseño integrado de PR #85, CSS propio/variables, componentes compartidos y seis estados; probar escritorio/móvil, teclado, foco/retorno, contraste y reflow.
7. Validar código nuevo y pruebas/inventarios antiguos afectados, incluidos PostgreSQL real, auditoría, idempotencia, concurrencia y no-efecto; actualizar trazabilidad y entregar capturas sintéticas de funcionamiento para revisión visual.
8. Trabajar en codex/front-019 con commits locales pequeños. Publicación, seguimiento remoto y aprobación expresa de merge se activan únicamente cuando se autorice ese hito; nunca despliegue implícito.

## 2. Hechos documentados, evidencia y dependencias

### 2.1 Base aceptada y Git

- Se leyeron primero IMPLEMENTATION_STATUS.md y FRONT_018_SIGUIENTE_TAREA_Y_LECCIONES.md. Este último conserva una transición anterior a FRONT-018; se aplican sus medidas preventivas, no su antiguo estado de siguiente tarea.
- Evidencia aportada por el responsable: FRONT-018 **Integrada**, PR https://github.com/SistemasTechLead/sgol/pull/87; cabeza `fd452ed6f573234dd9972b3b5fb9e0ed48967636`; merge `f722e44747e2a136825c72ac8c8f00122c079a00`; pipeline https://github.com/SistemasTechLead/sgol/actions/runs/36923195911/attempts/1 correcto. Plan, revisión visual, publicación y merge expresamente aprobados; 96dfe4d incluido. Seguimiento pausado; sin despliegue. Las esperas históricas del estado/informe quedan superadas por esta evidencia.
- Git inicial: codex/front-018, HEAD igual a la cabeza validada, árbol limpio. El merge está disponible localmente, tiene esa cabeza como segundo padre y su ascendencia se comprobó con resultado 0. Se creó codex/front-019 desde ese merge, sin fetch, cambios ajenos ni gates remotos.
- Única comprobación inicial de entorno: `./scripts/ci/preflight.ps1`, salida 0, treeClean=true, fuentesClean=true, fuentesRebaselinePending=false. El dotnet del PATH no resuelve 10.0.400 y muestra 10.0.401 instalado. No se añadió diagnóstico; futuras validaciones usarán `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe`, sin cambiar global.json ni restaurar salvo necesidad concreta.

### 2.2 Fuentes consultadas y eficacia

| Fuente | Alcance consultado / aplicación |
|---|---|
| AGENTS.md e INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md | Flujo local, aprobación íntegra previa, protección de fuentes y publicación por hito. |
| docs/INDICE_IDS.md | Localización de FRONT-019, HU-028/031, CA/CP-028/031, CAT y ADR. |
| Adenda 45, línea 102 | Única fila de implementación; no se inicia FRONT-020 ni TECH-FRONT-005. |
| F05_ESPECIFICACION_FUNCIONAL_MVP.md, líneas 143 y 146 | HU-028/CAP-033 y HU-031/CAP-043; permisos, RN y dependencias. |
| F05_CRITERIOS_DE_ACEPTACION.md, líneas 58, 61, 71–78 | CA/CP-028/031, CAT-001..008 y CPT positivos/negativos correspondientes. |
| F06_CONTRATO_DE_API.md §§5.6–5.8 y 7; F06_ARQUITECTURA.md §8; ADR-006/007 | API/sesión/CSRF, separación ejecución-validación, concurrencia/idempotencia, auditoría y archivos privados. Las adendas precisan las rutas conceptuales. |
| Adendas 25 y 26 | Contratos ejecutables de decisión/historia y pendientes/supervisión. Adendas 18/19/21/24 por referencia sólo en lo necesario para evidencia, revisión, conclusión y política capturada. |
| Adendas 46/55/57; informe FRONT_018_VERSIONES_REVISION_SUSTITUCION_CONCLUSION.md | Navegación/sesión, diseño vigente, consumos que no se alteran y excepción de revisión. |
| Ocho documentos de docs/design solicitados | ESTILO_VISUAL_V2.md, referencia-renovada.md, tokens.md, componentes.md, estados-y-mensajes.md, estados-de-dominio.md, accesibilidad.md y navegacion.md. Leídos antes de cualquier interfaz. |
| Contratos, endpoints, Roles.cs, cliente común, detalle, lector EF y servicio de validación | Superficie real, autoridad, ausencia de proyección consumidora y diferencias acotadas de §4. |
| Pruebas de decisión, supervisión, arquitectura e informe/lecciones de FRONT-018 | Prevención de expectativas históricas obsoletas y validación proporcional. |

La tabla `Tareas insertadas por adenda` acredita TECH-UI-001, TECH-VER-001, TECH-JOBS-001, TECH-EVID-001/002, TECH-AUTH-001, TECH-E2E-CV-02/03/04/05 y TECH-UI-PLAN-001; TECH-FRONT-001..004 están integradas. No se detectó una dependencia real de código pendiente anterior a FRONT-019. TECH-FRONT-005 es posterior, no un gate de inicio.

FRONT-012 está integrada por PR #78, cabeza `8c396e97b8de7cddc54e5041921f1fa644da1079`, merge `e87c64c3713fe53f572a4d4f4d8c4088022bca4a`, pipeline 36495586807 aceptado. Se conserva su matriz congelada y sus GET de política. FRONT-014 tiene implementación y trazabilidad disponibles de elegibilidad/corrección y ETag de detalle; su cierre administrativo histórico no impide este consumo. No se repite su análisis ni sus gates. FRONT-018 conserva los consumos aprobados de evidencia y conclusión; no se vuelve a implementar carga, sustitución de evidencia o conclusión.

### 2.3 Inferencias explícitas

Las primitivas visuales y las intenciones protegidas existentes pueden reutilizarse; eso no aprueba por sí mismo composición, mensajes ni campos nuevos. La existencia de un enlace API tampoco concede autoridad. La ausencia de una pendiente en una página no demuestra falta de autoridad ni permite ofrecer sustitución. Un resultado humano nunca se calcula desde COMPLETA/INCOMPLETA estructural ni desde vencimiento.

## 3. Alcance y exclusiones

Incluido: pendientes inferiores accionables, filtros/cursor; revisión de contexto y evidencia autorizados; primera emisión ordinaria o escalada; excepción de autovalidación de Dirección únicamente desde detalle; sustitución explícita autorizada, cadena histórica; supervisión de inferiores con evidencia/decisión vigentes y navegación a sus consultas existentes. Confirmaciones, errores, vacíos, recuperación de intención y conflictos forman parte del alcance.

Excluido: FRONT-020, TECH-FRONT-005, indicadores/KPI/conteos totales, vista integral de Dirección, auditoría general, exportación, nuevos avisos o solicitudes persistidas de escalamiento, reasignación, reapertura/cancelación/tareas correctivas automáticas, nuevas TAR/estados/permisos/resultados, actualización in-place de decisiones, borrado, backfill, políticas actuales en lugar de capturadas, nuevas dependencias o migraciones. No se añade carga o sustitución de evidencia a la nueva sección de validación; las acciones existentes del detalle conservan exclusivamente su contrato anterior.

Descarga y preview se excluyen por la propuesta explícita de §4. Consultar evidencia binaria ofrece únicamente metadata autorizada; no afirmar haber leído sus bytes. Se conserva la consulta de registros estructurados cerrados y faltantes. No se presume que esta limitación impida emitir un resultado humano contratado; el fundamento debe expresar lo revisado sin declarar una revisión binaria inexistente.

## 4. Brechas, diferencias y resoluciones propuestas para aprobación

**Estas resoluciones no están aprobadas todavía. Esta parte permanece detenida hasta la aprobación íntegra.** No se altera una fuente congelada para esconder diferencias.

| Carencia / hecho comprobado | Propuesta concreta y límite |
|---|---|
| BR-D09: FRONT-012 cerró la composición UI-C08/C09. Adenda 45 y referencia renovada aún exigen resolver el consumo UI-V01..V04; no existe composición específica incorporada en componentes.md. | Aprobar §§5–8 como extensión consumidora de componentes, navegación y estados. No declarar cerrada otra consumidora por asociación del mismo identificador. |
| BR-M09: falta catálogo literal específico de decisión/supervisión. | Aprobar íntegramente §7, incluidas etiquetas, ayudas, confirmaciones, resultados, vacíos y errores por código. |
| BR-API04: F06 menciona descarga conceptual; Adenda 18 §4.2 la excluye de su implementación y los endpoints reales no la ofrecen. Adenda 57 la excluye sólo de FRONT-018. | Aprobar exclusión independiente de descarga/preview para FRONT-019. Sin endpoint nuevo, permiso supuesto, URL firmada de lectura ni miniatura. BR-API04 permanece abierta globalmente. Si el responsable exige bytes, detener esa parte y revisar este mismo plan con contrato independiente antes de código. |
| Proyección: pendientes tiene availableAuthority/decisionEtag; el histórico no informa autoridad para sustituir ni autovalidar desde detalle. La sesión omite PER-VALIDACION-ESCALAR/SUSTITUIR aunque ya existen en contratos/Roles. | Proyectar esos dos permisos existentes para los roles que GrantsValidationEscalation/Replacement autoriza, exclusivamente para presentación. Añadir opcionalmente al envelope data del GET de validaciones `validationActions: { issueAuthority: null\|ORDINARIA\|ESCALAMIENTO\|AUTOVALIDACION_DIRECCION, canReplace: boolean }`. No añadirlo a decisiones históricas ni cambiar POST. Ausencia/desconocido falla cerrado. issueAuthority sólo en concluida con política exacta, sin decisión vigente y autoridad actual; canReplace sólo para la vigente con autoridad de original/superior. Compartir evaluador puro con las mutaciones, sin copiar reglas en Razor. Reautorizar siempre al confirmar. |
| Consistencia: EfValidationDecisionService.GetAsync hace lecturas AsNoTracking, sin transacción explícita que abarque ProjectAsync. Una sustitución concurrente podría separar requisito y cadena. | Encapsular GET y la proyección anterior en REPEATABLE READ, READ ONLY con instante único y AsNoTracking; no crear requisito/snapshot/auditoría. Probar cadena y ETag consistentes antes/después de una sustitución. No cambiar aislamiento SERIALIZABLE de comandos. |
| Diferencia con Adenda 25 §6: ValidationText normaliza/limita y rechaza controles, saltos y ángulos, pero no rechaza URLs; tampoco es un detector semántico de secretos o evidencia. | Corregir sólo el texto consumido por HU-028: mantener NFC, trim, 1–1000/1–500, tabulación permitida y códigos existentes; rechazar tokens de URL con esquema URI o prefijo www., rutas de archivo/objeto inequívocas, nombres con extensiones binarias permitidas JPEG/JPG/PNG/PDF y tokens de SHA-256 de 64 hexadecimales. Usar la misma validación en API/dominio y preparación UI; añadir negativos sin logs/capturas del valor. La ayuda prohíbe secretos y transcripción de evidencia; ninguna expresión regular acredita detección semántica universal. Esta precisión operativa requiere aprobación expresa con el plan; si se exige reconocimiento adicional no definido, solicitar la regla concreta y detener esa ampliación, sin introducir un motor general ni rebajar la prohibición documental. |
| Estados visuales: faltan resultados humanos/requisito y modos de autoridad específicos en estados-de-dominio.md. | Aprobar §8: mapas cerrados de presentación con iconos/pares existentes. No derivar el resultado ni confundir INCOMPLETA humana con evidencia incompleta. |

Después de aprobar: incorporar estas decisiones por una adenda F07 consumidora nueva en la raíz, comprobando el número libre entonces (no modificar F00–F07 ni Adenda 57). La adenda referenciará este único plan, no creará otro plan paralelo. Incorporar §§5–8 por referencia y las precisiones necesarias en docs/design **antes** de escribir vistas. La aprobación no autoriza capacidades excluidas ni cambia autoridad funcional.

## 5. Contratos y garantías que se conservan

### 5.1 Lecturas

| Operación real | Permiso / autoridad | Filtros, proyección y concurrencia |
|---|---|---|
| GET /api/v1/validations/pending | PER-VALIDACION-EMITIR o PER-VALIDACION-ESCALAR aplicable; inferior estricto, persona distinta, política congelada coherente y autoridad actual. | level, responsiblePersonId UUID, isoYear+isoWeek, cursor, limit 1–100/default 25; sin executionStatus. Orden pendingSince ascendente/obligationId ascendente. availableAuthority y decisionEtag exactos, MATERIALIZED/DERIVED; pendingSince=concludedAt. No total ni ETag de colección. |
| GET /api/v1/supervision/obligations | PER-SUPERVISION-VER; inferior estricto. | Mismos filtros más executionStatus PENDIENTE/CONCLUIDA. Orden dueAt ascendente/nulos al final, período descendente, taskCode ordinal/obligationId ascendente. Evidencia y decisión sólo vigentes; no duplica file/payload. |
| GET /api/v1/obligations/{id}/validations | PER-TAREA-VER y alcance HU-023: propia, inferior estricto o Dirección en LOR-001. | Sin query, cuerpo, If-Match ni Idempotency-Key. Historia completa **sin paginación**: versionNo descendente/ID ascendente. ETag de requisito si existe, de obligación si no; requisito null/decisions=[] no materializa. Proyección opcional de §4 requiere aprobación. |
| Detalle / evidencia / revisión existentes | Sus propios permisos y alcance; se reautorizan independientemente. | Detalle e historia de tarea, evidencia paginada y filtros VIGENTE/SUSTITUIDA conforme FRONT-018. Revisión sólo explícita, no automática al navegar. |

Pendiente significa CONCLUIDA, política capturada no nula, ninguna decisión vigente y autoridad actual. DERIVED sin requisito no lo crea por consultar. Política nula, ejecución pendiente o decisión vigente no entran. Una cadena imposible falla cerrada, no se presenta como vacío. Colecciones: filtros válidos fuera de alcance devuelven vacío indistinguible, inválidos 400. Cursor opaco ligado a ruta/filtros; cada página reevalúa autoridad, no promete snapshot entre páginas. No N+1 ni consulta previa de recursos fuera de alcance. meta.count cuenta sólo la página, nunca un indicador.

Pendientes/supervisión usan snapshot REPEATABLE READ, READ ONLY y AsNoTracking, no materializan, escriben, auditan ni alteran rowVersion. Histórico mantiene no-efecto y consistencia propuesta. Cache private,no-store; colecciones Pragma:no-cache. GET evidence-review es la excepción explícita de Adenda 19/57: snapshot inmutable y auditoría atómicos, deduplicación por huella/entrada canónica; misma huella no agrega filas. No confundirlo con lectura pura ni alterar ejecución, evidencia, asignaciones, validación, idempotencia u outbox.

### 5.2 Permisos, jerarquía y autoridad

| Actor vigente | Inferiores visibles en supervisión/pendientes |
|---|---|
| DIRECCION | ADMINISTRACION, SUBCOORDINACION, PISO_VENTAS |
| ADMINISTRACION | SUBCOORDINACION, PISO_VENTAS |
| SUBCOORDINACION | PISO_VENTAS |
| PISO_VENTAS | Ninguno; las colecciones superiores se deniegan. |

No hay propia, par, superior ni sin asignación vigente en esas colecciones, incluida Dirección. Rol/empleo/cuenta/MFA y LOR-001 vigentes al instante único; exactamente un rol canónico, nunca puesto textual. El mismo permiso aislado no basta. La autovalidación de Dirección sólo se ofrece desde el detalle autorizado, no desde pendientes ni supervisión; los demás roles no la obtienen, incluso forzando POST.

Matriz capturada: `TAR-0005`, `TAR-0008`, `TAR-0011`, `TAR-0092` y `TAR-0093`, SUBCOORDINACION→ADMINISTRACION; `TAR-0007` y `TAR-0018`, PISO_VENTAS→SUBCOORDINACION; `TAR-0026`, ADMINISTRACION→DIRECCION. No consultar política actualmente vigente como sustituto.

ORDINARIA requiere PER-VALIDACION-EMITIR, rol exacto del validador capturado, ejecutor actual exacto y superioridad estricta, persona distinta, escalationReason=null. ESCALAMIENTO requiere PER-VALIDACION-ESCALAR, superior estricto al validador de política y al responsable actual, persona distinta, escalationReason obligatorio. No crea solicitud/estado de escalamiento: es primera decisión por nivel posterior. AUTOVALIDACION_DIRECCION exige misma persona, DIRECCION vigente y PER-VALIDACION-EMITIR, motivo de escalamiento null; auditoría específica.

Sustitución requiere PER-VALIDACION-SUSTITUIR y decisión VIGENTE. SUSTITUCION_ORIGINAL: mismo validatorUserId, rol capturado aún vigente/canónico y alcance conservado; SUSTITUCION_SUPERIOR: superior estricto al validatorRole capturado y responsable vigente, persona distinta. No autoriza a un par por ocultar el botón. No basta haber sido validador si perdió rol/alcance. reason siempre obligatorio, incluso original o resultado igual.

### 5.3 Mutaciones, intención, historia y auditoría

| Operación | Cuerpo cerrado | Precondición / respuesta |
|---|---|---|
| POST /api/v1/obligations/{id}/validation-decisions | result, foundation, escalationReason (null ordinaria/autovalidación; texto en escalamiento). | If-Match fuerte del requisito o de obligación DERIVED; Idempotency-Key; CSRF/sesión. 201, Location al histórico y ETag de requisito. |
| POST /api/v1/validation-decisions/{id}/replacements | result, foundation, reason. | ID vigente, If-Match del requisito, misma protección. 201 crea sucesora y conserva anterior SUSTITUIDA, requisito RESUELTA. |

Sólo CUMPLIDA, INCOMPLETA, NO_CUMPLIDA, sin selección por defecto ni cuarto resultado. foundation obligatorio 1–1000 y motivos 1–500, normalización y prohibiciones de §4/Adenda 25. Los motivos no se intercambian. Fundamento libre permitido no es JSON ni catálogo nuevo. No copiar contenido de evidencia al fundamento.

Cada decisión conserva actor/rol/autoridad/fecha, fundamento, reason contractual (en emisión escalada refleja motivo de escalamiento), predecesora, evidenceReviewSnapshotId y evidenceVersionIds exactos. Mostrar los UUID autorizados sin resolver nombres ajenos mediante endpoints administrativos. Historia inmutable; no recalcular snapshots/decisiones anteriores con evidencia actual. Cada comando reevalúa evidencia VIGENTE atómicamente; estructura completa/incompleta no determina resultado humano. NO_CUMPLIDA/INCOMPLETA no reabren ni alteran CONCLUIDA, execution_result, conclusión, evidencia, decisiones previas ni crean obligaciones.

Servidor SERIALIZABLE, unicidad, bloqueos y hasta tres intentos totales del contrato. Scopes `validation:emit:{actorUserId:D}:{obligationId:D}` y `validation:replace:{actorUserId:D}:{currentDecisionVersionId:D}`. Misma clave/scope/hash recupera identidad/cuerpo/status/Location/ETag originales sin nuevo éxito/snapshot; hash distinto 409 IDEMPOTENCY_CONFLICT. Segunda emisión secuencial con otra clave 409 DECISION_VALIDACION_YA_EXISTE. Carreras con mismo ETag: una vigente/sucesora y perdedor 412; equivalencia de resultado no evita motivo ni nueva versión si fue sustitución explícita.

La API de decisión no entrega meta.replayed: no inventarlo ni inferir replay por 201. Primer envío confirmado dice «Validación registrada»; recuperación confirmada de la misma intención dice «Se confirmó el resultado de la misma solicitud» sin prometer otra versión. Intención Data Protection ligada a actor, método/ruta, recurso/ID vigente, cuerpo normalizado, clave y ETag originales, ocho horas como base consumidora existente. Fuera de URL/logs/browser storage. Preparar/confirmar/cancelar no mutan antes del POST final. Sin JavaScript existe confirmación equivalente. Incierto: sólo recuperación explícita con la intención intacta; nunca rotación/reintento automático. Intención vencida exige consultar historia antes de otra decisión.

412 bloquea el comando hasta recarga explícita de histórico/autoridad; se descarta la intención antigua y se exige nueva elección/confirmación. 409 segundo resultado conduce a consultar historia, no a sustituir automáticamente. Fallo de auditoría revierte requisito, decisiones, snapshot nuevo, idempotencia y negocio. Eventos existentes: VALIDATION_DECISION_ISSUED/ESCALATED, VALIDATION_DIRECTION_SELF_VALIDATED, VALIDATION_DECISION_REPLACED; rechazos DENIED/REJECTED donde corresponda, nunca éxito falso. Lecturas puras no insertan auditoría, incluso denegadas.

## 6. Composición propuesta UI-V01..V04 y componentes

El grupo ya registrado «Validación y supervisión» materializa /validaciones sólo con hijos implementados y permisos de sesión existentes. Propuesta de composición cerrada, pendiente de aprobación:

| Unidad | Superficie / información | Acciones y límites |
|---|---|---|
| UI-V01 | Panel «Pendientes de validar» en /validaciones. Tabla con tarea/código/origen/período, responsable autorizado/nivel, conclusión/pendiente desde, autoridad y acción. No duplica evidencia. | Consultar, quitar filtros, anterior/siguiente, «Ver tarea». No emitir desde una fila sin revisión contextual. ORD/ESC mostrados con texto; DERIVED explica pendencia sin crear requisito. |
| UI-V02 | Panel «Emitir validación» en /mi-trabajo/tareas/{obligationId:guid}, después de contexto/evidencia, con autoridad de recurso propuesta. | Tres radios sin preselección, fundamento, motivo de escalamiento sólo ESC; preparar y confirmar con resumen de tarea, resultado, fundamento y motivo aplicable. Autovalidación Dirección claramente excepcional. Sólo primera decisión, no editor sobre una vigente. |
| UI-V03 | Panel «Historial de validaciones» y editor «Sustituir validación» en el mismo detalle. Cadena completa de GET /validations, separada de historia de tarea/evidencia. | Ver requisito/decisión vigente e historial (versión, estado, resultado, fundamento, validador UUID/rol, autoridad, fecha, motivo etiquetado según autoridad, predecesora y snapshot/IDs). Sustituir sólo vigente con proyección verdadera, razón/fundamento/resultado nuevos y confirmación que conserva anterior. |
| UI-V04 | Panel «Supervisión de inferiores» en /validaciones, separado de pendientes. Tabla con tarea/contexto, responsable/nivel, ejecución, evidencia vigente resumida y decisión vigente/fundamento. | Filtros permitidos, cursor independiente y «Ver tarea» hacia detalle/historia. Sin propios/pares/superiores, KPI, total global ni controles de mutación masiva. |

Filtros de cada colección: «Nivel», «Persona responsable (identificador)», «Año ISO», «Semana ISO», «Registros por página»; supervisión además «Estado de ejecución». Persona usa UUID contractual, no selector poblado desde /people administrativo, búsqueda de nombres ni endpoints nuevos. Opciones de nivel derivan del mapa canónico de inferiores; cada API sigue denegando/filtrando solicitudes forzadas. Campos vacíos se omiten; año/semana se presentan juntos. Página inicial sin filtros no usa /weeks ni inventa semana. Instantes convertidos por servidor a America/Mexico_City y etiquetados «Hora de Ciudad de México»; fechas/períodos conservan su tipo.

Cursores de pendientes y supervisión protegidos por separado, ligados a actor/ruta/filtros, sin reutilización cruzada. Cambiar filtro reinicia sólo su cursor. Contexto de regreso protegido restaura colección, filtros, cursor y ancla/foco de la fila; inválido vuelve a /validaciones y su h1. Proponer adaptar el contexto de retorno del detalle existente para ambos orígenes, manteniendo los retornos previos de Mi trabajo. Query sólo filtros allowlisted y tokens opacos; fundamento, motivos, ETag, clave, CSRF e intención nunca en URL. Las relaciones API se traducen mediante mapa cerrado; no se renderiza href API arbitrario.

Reutilización: shell/layout/navegación/iconos, paneles y cabeceras, _EmptyState, _Alert, _ValidationSummary, _StatusBadge, _CursorPagination, campos/select/textarea, radio fieldset/legend y dialog de confirmación. Detalle/evidencia/_EvidenceVersions/_EvidenceReview existentes sin alterar sus contratos. Los nombres de nuevos parciales serán consumidores, no una segunda biblioteca. Falta composición de decisión/confirmación: la resuelve esta propuesta antes de vistas; no se requiere token, color o librería nuevos.

Confirmación de emisión/sustitución: formulario complejo fuera del dialog; éste resume intención protegida y consecuencia, Cancelar inicialmente enfocado, Escape/Tab/Shift+Tab, retorno al disparador o encabezado si desaparece. Acción crítica no destructiva usa primario; resultado NO_CUMPLIDA no crea botón de borrado. Cancelar no escribe. Preparación mediante POST Razor con antiforgery puede hacer lecturas autorizadas pero no comando/snapshot de revisión implícito.

## 7. Catálogo literal propuesto (BR-M09)

Los textos siguientes son propuesta para incorporar como catálogo operativo; no se renderizan title/detail/instance/errors crudos. correlationId seguro cuando existe. Datos recibidos se codifican como texto, nunca HTML. Los mensajes consumidores existentes de evidencia/sesión/CSRF conservan su literal.

| Situación | Texto literal / acción |
|---|---|
| Título de página / secciones | «Validación y supervisión»; «Pendientes de validar»; «Supervisión de inferiores»; «Emitir validación»; «Historial de validaciones»; «Sustituir validación». |
| Etiquetas de decisión | «Resultado»; «Cumplida»; «Incompleta»; «No cumplida»; «Fundamento»; «Motivo de escalamiento»; «Motivo de sustitución». |
| Ayuda de separación | «La validación es una decisión separada. La ejecución permanece concluida.» |
| Ayuda de fundamento/motivos | «Explica tu decisión sin copiar evidencia, nombres de archivo, hashes, rutas, enlaces ni secretos.»; «Fundamento: de 1 a 1000 caracteres.»; «Motivo: de 1 a 500 caracteres.» |
| Sin pendientes / filtros | «No hay validaciones pendientes en tu alcance»; «No hay coincidencias con estos filtros», acción «Quitar filtros». No total cero global ni afirmación sobre sucursal completa. |
| Sin supervisión / filtros | «No hay tareas de inferiores disponibles en tu alcance»; «No hay coincidencias con estos filtros», acción «Quitar filtros». |
| Sin historial | «Aún no hay decisiones de validación»; emisión sólo si autoridad proyectada. |
| Sin política / ejecución pendiente | «Esta tarea no tiene una política de validación disponible»; «La tarea debe estar concluida antes de validarla». Sin controles de decisión. |
| DERIVED | «Pendiente de validación; el requisito se registrará al emitir la primera decisión». |
| Sin evidencia vigente en supervisión | «No hay evidencia vigente disponible». Sin enlace de descarga o afirmación sobre evidencia histórica. |
| Carga | «Consultando pendientes…»; «Consultando supervisión…»; «Consultando validaciones…»; «Enviando validación…»; «Enviando sustitución…». |
| Acciones | «Consultar pendientes»; «Consultar supervisión»; «Ver tarea»; «Preparar validación»; «Preparar sustitución»; «Emitir validación»; «Sustituir validación»; «Cancelar»; «Recargar validaciones»; «Recuperar resultado»; «Preparar otra decisión». |
| Autoridad | «Superior inmediato»; «Escalamiento»; «Autovalidación excepcional de Dirección»; «Sustitución por validador original»; «Sustitución por superior». |
| Confirmar emisión | «Emitir esta validación»; «Se registrará el resultado elegido con su fundamento. La ejecución permanecerá concluida.» |
| Confirmar escalamiento | «Emitir validación por escalamiento»; «Actúas como un nivel posterior. Se registrará el motivo de escalamiento y la ejecución permanecerá concluida.» |
| Confirmar autovalidación Dirección | «Autovalidar esta tarea como Dirección»; «Esta excepción se registrará expresamente. La ejecución permanecerá concluida.» |
| Confirmar sustitución | «Sustituir esta validación»; «La decisión vigente pasará a Sustituida y seguirá en el historial. Se registrarán la nueva decisión y el motivo; la ejecución permanecerá concluida.» |
| 201 envío confirmado | «Validación registrada. La ejecución permanece concluida.»; «Validación sustituida. La decisión anterior permanece en el historial.» |
| Recuperación 201 de intención original | «Se confirmó el resultado de la misma solicitud. Consulta el historial antes de preparar otra decisión.» No inferir número de nuevas filas. |
| Incierto / vencido | «No se pudo confirmar el resultado de la solicitud. Recupera la misma intención antes de preparar otra decisión.»; «La intención venció y no se pudo confirmar su resultado. Consulta el historial antes de preparar otra decisión.» |
| 400 FILTRO_SUPERVISION_INVALIDO / FILTRO_VALIDACIONES_PENDIENTES_INVALIDO | «Revisa los filtros de supervisión» / «Revisa los filtros de pendientes»; «Usa un nivel, identificador y semana válidos». Asociar al filtro comprobado; no adivinar campo desde detail. |
| 400 SOLICITUD_VALIDACION_INVALIDA / IDs inválidos | «No se pudo preparar la validación. Revisa los datos y recarga la tarea si el problema continúa.» |
| 400 IDEMPOTENCY_KEY_INVALIDA | «La intención de la solicitud no es válida. Consulta el historial antes de preparar otra decisión.» |
| 401 | Mensaje aprobado: «Tu sesión terminó. Inicia sesión nuevamente.» Limpia sesión/cookies permitidas; no conserva cuerpo de POST como destino. |
| 403 por región | «No tienes permiso para consultar pendientes de validación»; «No tienes permiso para supervisar estas tareas»; «No tienes permiso para realizar esta validación». Retirar datos/controles correspondientes. |
| 404 obligación/decisión | Literal aprobado «No existe o no está disponible en tu alcance». Sin indicar si era par, histórico, ajeno o inexistente. |
| 409 OBLIGACION_NO_CONCLUIDA | «La tarea debe estar concluida antes de validarla». |
| 409 POLITICA_VALIDACION_NO_DISPONIBLE | «Esta tarea no tiene una política de validación disponible». |
| 409 EVIDENCIA_VALIDACION_NO_DISPONIBLE | «No se pudo verificar la evidencia que sustenta esta decisión. Recarga la tarea antes de continuar.» |
| 409 DECISION_VALIDACION_YA_EXISTE | «Esta tarea ya tiene una decisión vigente. Consulta el historial; una sustitución exige autorización y motivo.» |
| 409 IDEMPOTENCY_CONFLICT | «Esta intención ya se utilizó con otros datos. Consulta el historial antes de preparar otra decisión.» No recuperar con cuerpo modificado. |
| 409 VALIDACION_CONCURRENCIA_CONFLICTO | «No se pudo confirmar la decisión por un conflicto de concurrencia. Consulta el historial antes de continuar.» Sin reintento automático. |
| 412 VERSION_CONFLICT / 400 IF_MATCH_* | «Esta validación cambió mientras la preparabas. Recarga las validaciones y decide de nuevo.» Acción «Recargar validaciones», bloquear comando. |
| 422 RESULTADO_VALIDACION_INVALIDO | «Elige Cumplida, Incompleta o No cumplida», asociado al grupo. |
| 422 FUNDAMENTO_INVALIDO | «Ingresa un fundamento válido de 1 a 1000 caracteres, sin datos prohibidos», asociado al campo. |
| 422 MOTIVO_REQUERIDO | «Ingresa un motivo válido de 1 a 500 caracteres, sin datos prohibidos», asociado al motivo aplicable. |
| 422 AUTOVALIDACION_NO_PERMITIDA | «Sólo Dirección puede autovalidar su propia tarea». No habilitar excepción a otro rol. |
| 500 CONSULTA_SUPERVISION_INCONSISTENTE / CONSULTA_VALIDACION_INCONSISTENTE; desconocido/503 de protocolo | «No se pudo obtener información consistente. Recarga la sección; si el problema continúa, informa el identificador de seguimiento.» Nunca convertir inconsistencia en vacío/éxito ni mostrar datos parciales. |
| CSRF inválido | Catálogo común aprobado: «No se pudo verificar la solicitud. Recarga la página antes de volver a enviarla.» |

Validación previa local del formulario: «Selecciona un resultado»; «Ingresa el fundamento antes de continuar»; «Ingresa el motivo antes de continuar»; «Revisa este campo». Conservar valores no sensibles de la intención, sin reflejar respuestas técnicas. Confirmación muestra texto normalizado para que el usuario apruebe lo que se enviará.

## 8. Estados y diseño propuestos

| Estado de pantalla | Cobertura |
|---|---|
| Normal | Datos/autoridad confirmados, ejecución separada, historia vigente/sustituida; no ofrecer emisión junto a decisión vigente. |
| Foco | Anillo oficial, orden DOM, resumen enfocable, errores asociados; retorno de dialog y de detalle a colección/fila. |
| Deshabilitado | Sólo temporal/incompleto/en envío o conflicto. Sin permiso/autoridad se oculta acción. No atenuar datos necesarios de lectura. |
| Error | Regional y textual; 401/403/404 convergentes; conflicto exige decisión explícita; sin presentación parcial inconsistente. |
| Cargando | aria-busy regional, esqueleto de tabla y mensaje correspondiente; botones previenen doble envío. |
| Vacío | Separar ausencia de pendientes, supervisión, filtro, historia y evidencia; sólo recarga/filtro/acción autorizada. |

Estados de dominio **recibidos**, no nuevos persistidos: requisito PENDIENTE→«Pendiente de validación», reloj/advertencia; RESUELTA→«Resuelta», check/info; decisión VIGENTE→«Vigente», escudo/exito y SUSTITUIDA→«Sustituida», historial/info existentes. Resultado humano CUMPLIDA→«Cumplida», check/exito; INCOMPLETA→«Incompleta», triángulo/advertencia; NO_CUMPLIDA→«No cumplida», equis/peligro. Mantener badge separado «Concluida». Ausencia de requisito/decisión no equivale a resultado NO_CUMPLIDA. Autoridades y MATERIALIZED/DERIVED son texto descriptivo, no transición de negocio. No copiar el badge estructural como resultado humano.

Conservar base aprobada/integrada PR #85: marca/logo/iconos propios, sesión y navegación, paneles blancos sobre fondo cálido, h1/h2/h3, tablas semánticas con caption/th/scope y scroll exclusivamente interno. Poppins local OFL operativo y Georgia sistema sólo usos aprobados, The Seasons excluida. Primario acento, secundario neutro, auxiliares textuales y navegación con flecha. CSS propio, variables oficiales para color, tipografía, radios, sombras, espaciados, foco y área mínima. Sin librerías nuevas, fuentes remotas ni literales fuera de docs/design/hoja de variables y excepción de media query ya documentada.

En móvil filtros/editor/dialog refluyen a una columna; no transformar tablas en tarjetas ni ocultar información esencial. Medir contraste efectivo, área mínima --alto-control-minimo, teclado completo, Escape/retorno/trap de foco, texto 200 %, página a 320px CSS y prefers-reduced-motion. No declarar WCAG integral por capturas o emulación.

## 9. Archivos previstos y secuencia posterior a aprobación

No se modifican ahora los archivos siguientes. Cada cambio se limita al consumo necesario:

| Grupo | Archivos / propósito |
|---|---|
| Contrato consumidor/documentación | Adenda F07 nueva de contrato FRONT-019, referencias consumidoras en docs/design/componentes.md, estados-y-mensajes.md, estados-de-dominio.md, navegacion.md y referencia-renovada.md; este plan conserva su identidad. IMPLEMENTATION_STATUS.md e informe FRONT_019_VALIDACION_Y_SUPERVISION.md en el hito implementado. |
| Contrato/evaluación | src/Modules/Execution/Contracts/ValidationDecisions.cs y nuevo evaluador puro ValidationDecisionAuthorization.cs si se necesita separar la regla existente; src/Modules/Identity/Contracts/Roles.cs para proyección de permisos existentes. No EF/ASP.NET en dominio. |
| Servicio/GET | src/Sgol.Web/Infrastructure/Persistence/Validation/EfValidationDecisionService.cs; src/Sgol.Web/Interface/Endpoints/ValidationDecisionApiEndpoints.cs: proyección opcional y lectura consistente; misma autoridad compartida en comandos. HierarchySupervisionApiEndpoints.cs y EfObligationQueryReader.cs se reutilizan; sólo correcciones indispensables comprobadas, nunca nuevas rutas. |
| Cliente/presentación | Interface/ApiClient/ApiClientContracts.cs y SgolApiClient.cs sólo para transportar/validar meta.queriedAt en colecciones de este consumidor sin debilitar otros envelopes; presentación nueva Interface/Validation/* para DTO/protocolo, mensajes, consulta, cursor, retorno e intención protegida. Validar result, status, IDs/ETag y autoridad desconocida cerradamente. |
| Navegación | Interface/Navigation/NavigationItem.cs, registro de destinos protegidos y shell donde corresponda: /validaciones con hijos por permiso; destino GET y queries allowlisted. Conservar los destinos existentes. |
| Razor | Nuevos Pages/Validations/Index.cshtml(.cs), _PendingValidations.cshtml, _Supervision.cshtml; Pages/MyWork/Details.Validation.cs y parciales _ValidationHistory.cshtml/_ValidationDecision.cshtml; adaptar Details.cshtml(.cs) para carga histórica/proyección y retorno desde ambos listados. Reutilizar evidencia/revisión anteriores. |
| CSS/JS | wwwroot/css/components.css únicamente si composición lo requiere; JS existente de confirmación/carga/foco o validations.js mínimo si hace falta. Sin regla de negocio cliente ni almacenamiento local. |
| Pruebas | Front019PresentationTests, Front019ArchitectureTests, Front019BrowserTests y fixture parcial Front019; ampliación enfocada de ValidationDecisionTests/ApiEndpointTests, HierarchySupervisionApiEndpointTests, RoleAdministrationTests, SgolApiClientTests, navegación/retorno, ObligationConclusionPersistenceTests y/o parcial Front019. Inventarios antiguos de evidencia/diseño/frontend directamente afectados. |
| Evidencia sintética | .artifacts/front-019, manifiesto FRONT_019_CAPTURAS.md e informe; proteger datos ocultos/CSRF/cookies, no URLs firmadas ni evidencia real. |

Secuencia: (1) aprobar plan e incorporar decisiones en adenda/diseño; (2) corregir diferencias acotadas de §4 y probar contratos/no-efecto; (3) materializar colecciones/historia; (4) emisión/sustitución con intención y confirmación; (5) validación enfocada y capturas de funcionamiento; (6) trazabilidad y commits locales pequeños, revisión visual expresa. No alterar el SDK/locks/dependencias; no generar migraciones por esta historia.

## 10. Validación y criterios de aceptación previstos

Al presentar este plan no se ejecutaron build/pruebas funcionales ni se modificó código. La comprobación documental fue git diff --check y revisión de alcance/Git. Los resultados posteriores a la aprobación constan en el informe de implementación. Fuentes/originales/Excel/ZIP no se abren ni modifican.

Tras aprobar, usar SDK exacto indicado: `dotnet build --no-restore --configuration Release`; `dotnet test <proyecto> --no-build --configuration Release --filter <casos afectados>`; `git diff --check`. No restore salvo artefactos ausentes/cambio justificado; no suite completa por edición. Seleccionar casos reales tras definir pruebas, verificar que el filtro tenga coincidencias y registrar números/resultados, no atribuir éxito a un filtro vacío.

| Criterio / riesgo | Verificación proporcional |
|---|---|
| CA-028 / CP-028-P | Superior exacto emite los tres resultados, incluidas CUMPLIDA y NO_CUMPLIDA; ejecución/snapshot de conclusión inalterados. Sustitución con fundamento/motivo, ambas versiones, una vigente y snapshot exacto. |
| CA-028 / CP-028-N | Autovalidación no Dirección, par/inferior/rol obsoleto, resultado desconocido, segunda emisión, sustitución histórica o sin motivo, escalamiento sin motivo y fuera de alcance: rechazo esperado y ausencia de efecto funcional/idempotencia/éxito. Excepción Dirección auditada sólo donde contrato la admite. |
| CA-031 / CP-031-P/N | Administración ve Subcoordinación/Piso y evidencia vigente, jamás Dirección/Administración par/propia/sin asignación. Dirección y Subcoordinación conforme matriz. Cambio de rol/empleo/responsable y cursores antiguos no amplían alcance. |
| CAT-001..008 / CPT correspondientes | Matriz capturada de ocho TAR, requisito condicional F-ENT-001, padre/contexto donde corresponda, evidencia/fundamentos autorizados. CAT-003/004/006 permiten desfavorable temporal con ejecución concluida; CAT-001/002/005/007/008 no cambian reglas previas de conclusión. No calcular resultado humano automáticamente ni repetir gates completos de conclusión aceptados. |
| Permisos/sesión/CSRF/anti-IDOR | Positivos/negativos de endpoints y Razor, POST forzado, 401/403/404 y autoridad perdida antes de confirmar/replay; proyección falsa/ausente nunca concede acción. |
| Auditoría/idempotencia | PostgreSQL real: fallo de auditoría revierte todo; mismo cuerpo/clave recupera IDs sin otra auditoría de éxito/snapshot; cambio de cuerpo/ETag con misma clave 409; no fundamento/motivo/payload sensible en auditoría/log. |
| Concurrencia | PostgreSQL real: ordinaria contra escalada, dos primeras emisiones, dos sustituciones, sustitución de evidencia contra decisión, histórico durante reemplazo. ETag fuerte vigente, perdedor 412, no mezcla de versiones. |
| No-efecto | Comparar persistencia antes/después de pendientes, supervisión e historia, incluidos rechazados: requisitos, decisiones, evidencia, ejecución/conclusión, idempotencia, audit_event, avisos/outbox. Revisión explícita: primera huella sólo snapshot+auditoría atómicos; repetida sin nuevos registros. |
| Texto/protocolo | Límites 1/1000/1001 y 1/500/501, NFC/trim/tab, saltos/controles/HTML y tokens prohibidos de §4; JSON cerrado, ETag inválido, autoridad/resultado desconocidos, inconsistent projections: fallo cerrado sin expectativas relajadas. |
| UI / rutas / componentes | Pruebas nuevas y antiguas afectadas: ValidationDecisionArchitectureTests, HierarchySupervisionArchitectureTests, EvidenceInfrastructureArchitectureTests, InterfaceDesignRules, Front016/017/018, navegación, retorno/session/cliente. Ampliar listas cerradas sólo por archivos aprobados, conservar prohibiciones y aserciones correctas. |
| Navegador | Harness existente HTTPS/PostgreSQL real con sintéticos: cuatro roles, escritorio/móvil, filtros sin resultados/cursor/historia, emisión/escalamiento/sustitución/conflicto/recuperación/no-JS, los seis estados, reflow/foco/contraste/texto ampliado/movimiento reducido. Los fallos se investigan leyendo logs antes de reintento. |

Pruebas PostgreSQL enfocadas directamente relacionadas con HU-028/HU-031, presentes en ObligationConclusionPersistenceTests, se reutilizan y amplían; no suponer que la suite de política cubre decisiones o supervisión. Si se reutiliza carga desde las pantallas anteriores, conservar 15 MiB, firma/tipo real/SHA-256/cuarentena/antimalware, nunca vincular antes de LIMPIO; este plan no crea otro flujo de carga. Si servicios reales faltan temporalmente, registrar causa precisa y avanzar sólo donde la validación sea posible; un build/test enfocado compatible fallido impide Implementada localmente.

**Validación diferida conservada:** zoom nativo, lector de pantalla y dispositivos físicos por no contar con esas verificaciones; WebKit Windows PUT/S3 local HTTP por ausencia de respuesta HTTP documentada (no relajar firma); aislamiento productivo completo SeaweedFS por no acreditarlo el entorno local. Emulación, capturas/revisión visual o uso de Chromium no resuelven esos límites. Suites integrales/costosas y formato integral quedan para publicación autorizada; no repetir gates de FRONT-018 aceptada.

## 11. Entrega, trazabilidad y autorizaciones

Estado al presentar el plan: **preparado para aprobación**, con este plan como único archivo producido. Las decisiones previas eran §§2/5 y estilo integrado; §§4/6/7/8 fueron propuestas consumidoras. La aprobación íntegra posterior «Apruebo el plan» habilitó su incorporación local por Adenda 58 e implementación; el informe registra el estado ejecutado.

Tras implementar: actualizar IMPLEMENTATION_STATUS y el informe en el mismo hito con aprobación literal/alcance, base, archivos, criterios, comandos/resultados y límites. Registrar la integración aceptada de FRONT-018 al inicio de esa trazabilidad sin reabrir sus gates ni crear commit administrativo para números resolubles. Capturas de aplicación **funcionando** con datos sintéticos y manifiesto: colecciones de cada superior, estados vacío/error/carga, evidencia/historia, tres resultados, escalamiento, autovalidación Dirección si el fixture contractual la admite, confirmaciones, sustitución/412/recuperación y móvil. Explicar en lenguaje cotidiano qué ve cada puesto, qué hace cada acción y la diferencia entre tarea concluida y decisión; solicitar revisión visual expresa. No entregar maquetas como funcionamiento.

No push/PR/checks remotos ni automatización ahora. Al autorizar publicación de FRONT-019: formato `dotnet format --verify-no-changes --no-restore` con SDK fijado **antes** del push, validación afectada, un PR adjunto a este chat y seguimiento/correcciones del mismo hito sin nuevas confirmaciones. Si quedan checks pendientes al terminar, configurar/verificar heartbeat en este chat, reutilizado sin duplicados ni chats nuevos, silencioso sin cambio accionable. Leer logs y separar instalación/ejecución/causa primaria antes de reintentar. Validar siempre la cabeza vigente; cancelación/omisión requerida no es éxito.

Con todos los checks requeridos correctos y requisitos cubiertos, verificar PR abierto, SHA completo exacto, ausencia de conflictos y revisiones/requisitos pendientes; solicitar aprobación expresa de merge con PR/SHA/pipeline/límites y pausar seguimiento. Publicación no autoriza merge ni despliegue.

**Decisión solicitada al presentar y ya recibida:** aprobar íntegramente este único plan, incluidas las resoluciones de §4, composición, mensajes y estados de §§6–8, para habilitar exclusivamente implementación local de FRONT-019. Respuesta del responsable: «Apruebo el plan». No hay autorización de publicación, merge ni despliegue.
