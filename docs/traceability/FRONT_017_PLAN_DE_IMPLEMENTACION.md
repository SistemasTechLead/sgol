# FRONT-017 — Plan único de implementación

Fecha: 2026-09-30. Estado: **APROBADO ÍNTEGRAMENTE**, incorporado por Adenda 56 antes de código.

Aprobación literal: «Apruebo integramente el plan, recuerda seguir los documentos de diseño para crear las pantallas a como están diseñadas las anteriores». Cubre todas las secciones, BR-D08 y ampliación de lectura de §3.3; los términos propuesta/pendiente debajo conservan el momento de preparación y quedan superados por esta aprobación. Sólo implementación y commits locales, sin publicación.

## 1. Alcance y control

La aprobación solicitada cubre este plan, incluidas las propuestas expresas de §§3–7. No se consideran aprobadas por existir este archivo. Antes de código se conservará la decisión literal y se incorporará una adenda F07 consumidora en la raíz, con número libre comprobado en ese momento, que referencie las secciones aprobadas y precise la ampliación de lectura de §3.3. No se cambia F00–F07 congelado ni Fuentes/. Si la aprobación excluye esa ampliación, se detiene la composición que necesita requisitos capturados y se solicita una alternativa contractual concreta.

Resumen previo, limitado a ocho puntos:

1. Implementar sólo FRONT-017, fila 95 de Adenda 45: UI-E04/E05, carga privada, escaneo y primera aportación binaria o estructurada para las ocho TAR de LOR-001.
2. Reutilizar FRONT-012/016 y TECH-EVID-001/002, sin repetir sus gates. Diseño v2 de PR #85 integrado, según evidencia aceptada del responsable y merge disponible localmente.
3. BR-D07 tiene base aprobada; precisar su consumo. BR-D08 carece de resolución explícita local: proponer formularios cerrados, etiquetas, estados y mensajes, sin atribuirle cierre previo.
4. Resolver mediante adenda aprobada la lectura de requisitos congelados de la obligación, ausente del detalle actual; no usar política vigente de configuración como sustituto.
5. Añadir secciones al detalle existente, componentes compartidos y mejora JavaScript para PUT directo; conservar rutas, permisos, sesión, CSRF, idempotencia y contratos de escritura.
6. Modificar únicamente presentación/cliente y la proyección de lectura aprobada, pruebas afectadas, diseño operativo y trazabilidad; sin paquetes ni migraciones previstos.
7. Validar build, pruebas enfocadas, PostgreSQL/S3/ClamAV y navegador proporcional; cubrir seguridad, auditoría, no-efecto y revisión visual escritorio/móvil.
8. Commits locales pequeños después de validación. Publicación, PR, checks remotos, merge y despliegue requieren sus autorizaciones respectivas; este plan no los autoriza.

Excluidos: FRONT-018..020, sustitución de versiones, conclusión, nueva revisión agregada/faltantes, validación jerárquica, preview, descarga, exportación, borrado, avisos externos, otras TAR, JSON libre, editor general, nuevos permisos y despliegue. El GET de evidencia sólo se usa para reconocer primera aportación existente y el formulario vigente necesario para la foto condicional; no materializa la pantalla de historia/revisión de FRONT-018.

## 2. Hechos documentados, evidencia y dependencias

| Fuente consultada | Hecho y uso |
|---|---|
| AGENTS.md e INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md §§2–4 | Plan/aprobación previos, desarrollo local, protección documental y validación proporcional. |
| docs/INDICE_IDS.md; Adenda 45 filas 80, 94, 95 | FRONT-012 y FRONT-016 son dependencias; FRONT-017 usa UI-E04/E05, BR-D07/D08, CA/CP-025 y CAT. |
| IMPLEMENTATION_STATUS.md, tabla Tareas insertadas por adenda | TECH-EVID-001/002 y base frontend disponibles; TECH-FRONT-005 corresponde a después de FRONT-001..020 y no bloquea este trabajo. No se encontró dependencia de código pendiente anterior aplicable. |
| FRONT-012, registro integrado | PR #78, cabeza 8c396e97b8de7cddc54e5041921f1fa644da1079, merge e87c64c3713fe53f572a4d4f4d8c4088022bca4a; políticas de evidencia/validación. Evidencia aceptada sin nuevos gates. |
| FRONT-016, código y registro del hito visual | Bandeja y detalle existentes. DISENO_RENOVADO_V2_IMPLEMENTACION.md registra incorporación por ec53eed; el merge de #85 tiene como primer padre ec53eede4d10d7b97d7d3c8da97f54e83ea62cc6. Las filas antiguas Publicada/pendiente no bloquean el consumidor. No se inventan nuevos resultados de #84. |
| PR #85, evidencia aportada en este chat | Cabeza 14e4f99995082672b77d0cfca138c183d0999e02, run final 36797097416 correcto, merge 754029d7f3fce8f5eec9961824ad336930d235dc. Git local confirma objeto de merge y cabeza como segundo padre. No se consultó ni ejecutó un gate remoto. |
| F05, HU-025/CA-025/CP-025-P/N; CAT-001..008 | Historia/sustitución y reglas de cada TAR. FRONT-017 cubre primera aportación y rechazos pertinentes; no declara satisfechos los CP de sustitución posterior a conclusión, reservados a FRONT-018. CAT de conclusión/validación tampoco se declara completado por guardar evidencia. |
| F06 API §§2, 10; Arquitectura §6.3; Seguridad §8; ADR-006/007 | JSON camelCase, transacciones/auditoría, privado, cuarentena, tipo real, SHA-256, 15 MiB, antimalware y LIMPIO antes del vínculo. |
| Adenda 17 §§4–7; Adenda 18 §§4–10, 13, 17, 24–25 | Infraestructura, carga de 10 minutos, autorización reiterada, PUT firmado, complete, status, primera aportación, CSRF y límites reales de CORS SeaweedFS. |
| Adenda 20 §§5–12, 15 | 18 payloads cerrados y condición de foto de TAR-0092. Su evaluación aprobada supera el rechazo inicial de condición de Adenda 18; no es una contradicción nueva. |
| Ocho documentos de diseño solicitados y Adenda 55 | ESTILO_VISUAL_V2.md, referencia-renovada.md, tokens.md, componentes.md, estados-y-mensajes.md, estados-de-dominio.md, accesibilidad.md, navegacion.md. CSS propio, variables y componentes compartidos; Poppins local OFL, Georgia del sistema sólo bienvenida. The Seasons excluida, sin reintroducción ni archivos de Georgia. |

Base local de planificación: rama codex/diseno-renovado, HEAD 14e4f99995082672b77d0cfca138c183d0999e02; árbol inicialmente limpio. No se cambió de rama ni se movió HEAD. El checkout contiene el código validado del hito, aunque HEAD no sea su merge; no se afirma que esté situado en master. Antes de implementar se conservarán estos documentos en una rama local codex/front-017 basada en el merge aceptado disponible, preservando cualquier cambio ajeno aparecido entretanto.

## 3. Brechas y resoluciones propuestas para aprobación

### 3.1 BR-D07: base existente y consumo pendiente

**Hecho:** TECH_FRONT_001_AUDITORIA_BASE_UI.md fila BR-D07, componentes.md «Subida y análisis de evidencia», estados-y-mensajes.md «Estados seguros de subida» y _UploadPresentation.cshtml ya definen selección, progreso, cancelación y veredictos seguros. Esta base no ejecuta carga ni escaneo. La referencia renovada mantiene BR-D07 como resolución consumidora de FRONT-017.

**Propuesta:** reutilizar y completar ese parcial con tipos admitidos por requisito, label/ayuda asociada, progreso con nombre accesible, drag/drop equivalente al selector nativo y estados contractuales exactos. Los ejemplos históricos «Completa» y «quitar» no autorizan completitud agregada ni borrado. Sólo se permite retirar selección local antes de enviarla. Cancelar aborta la transferencia local y detiene la continuación; no borra file_object ni evidencia, ni garantiza que S3 no haya recibido bytes. No existe endpoint de cancelación. Después de complete sólo se puede detener la consulta de estado; el Worker continúa su contrato.

### 3.2 BR-D08: carencia exacta y propuesta

**Hecho:** no se localizó resolución explícita de BR-D08 en el índice, adendas, diseño operativo o trazabilidad consultados. Los entregables INVENTARIO_EXACTO_FRONTEND.md y BRECHAS_Y_DECISIONES_PENDIENTES_FRONTEND.md nombrados por Adenda 44 no existen en la raíz actual. No se buscó en Fuentes/ ni se reconstruyeron esos originales.

**Inferencia delimitada:** la fila FRONT-017 requiere evidencia estructurada; su formulario consumidor y checklist son necesarios. No se atribuye a BR-D08 una definición canónica más específica que la evidencia disponible.

**Propuesta de resolución consumidora:** aprobar los formularios y etiquetas de §5, composición de §4, estados/mensajes de §6 y controles de §7. Son presentación de los esquemas de Adenda 20, no cambios de sus payloads. Incorporar esta resolución por referencia en componentes.md y estados-y-mensajes.md después de aprobarla; registrar BR-D08 resuelta para FRONT-017 sólo entonces.

Primitivas existentes suficientes: texto, textarea, select, radio, decimal mediante campo nativo y fecha/hora local. Falta su composición específica para 18 esquemas, etiquetas, ayudas y mensajes. No se crea biblioteca ni renderer de esquema configurable. Cada booleano usa Sí/No sin selección inicial, para distinguir false de ausencia. schemaVersion y formCode fijos son datos del contrato, no campos editables. responsiblePersonId se captura con etiqueta «Identificador de la persona responsable» y ayuda «UUID de la persona»; no se inventa selector de personas ni permisos de consulta adicionales.

### 3.3 Carencia de lectura de la política capturada

**Hecho de código:** ObligationDetail no contiene política/requisitos de evidencia; contiene tarea, origen, período, fechas, asignación, links, generación e historia. GET /evidence sólo entrega ítems existentes. InboxEvidence entrega faltantes y resultado, no el catálogo íntegro capturado para un detalle independiente. GET /evidence-review persiste snapshots en EfEvidenceReviewService; no es una lectura inocua del catálogo. Leer la política vigente de UI-C08 exigiría además autoridad de configuración y no acreditaría el snapshot de creación.

**Propuesta contractual mínima, pendiente:** ampliar aditivamente GET /api/v1/obligations/{id} con `evidencePolicy` nullable: `{ evidencePolicyVersionId, requirements: [{ requirementVersionId, requirementCode, kind, conditionCode, ordinal }] }`. Las propiedades reutilizan identificadores existentes; este objeto nuevo es una propuesta, no un DTO aprobado vigente. `null` significa política capturada ausente, no una lista vacía inventada. La lista proviene de la versión almacenada en la obligación, ordenada por ordinal, sin usar la versión vigente actual. No añade evaluación, satisfacción, payload, hash, enlaces firmados, estado de escaneo, datos personales ni escrituras. Misma autorización PER-TAREA-VER/alcance y 404 convergente, ETag y rutas existentes. Un snapshot incoherente falla cerrado con el error existente de consulta, sin formulario.

Lectura mediante contrato interno explícito del módulo dueño, sin escritura cruzada ni acceso de Razor a DbContext. Pruebas de versión congelada tras publicar política nueva, no-efecto y anti-IDOR. La adenda consumidora aprobada deberá contener este cambio antes de modificar DTO/endpoints. No se necesita nuevo endpoint, tabla o migración. Si emerge cualquier dato adicional necesario, se presenta su carencia antes de implementarlo.

## 4. Pantallas, componentes y recorrido propuestos

UI-E04/E05 viven en secciones «Aportar evidencia» del detalle existente `/mi-trabajo/tareas/{obligationId:guid}`. No se crea grupo de navegación ni nueva ruta web. La bandeja puede ofrecer «Aportar evidencia» hacia ese detalle y su ancla sólo si recibe CONTRIBUTE_EVIDENCE. En acceso directo se reconsulta sesión y detalle: visibilidad de aporte exige permiso vigente, responsable actual propio y PENDIENTE; cada API vuelve a autorizar. La ausencia de autoridad oculta controles. Los links recibidos sólo se traducen por mapa cerrado, nunca se renderiza href arbitrario de API.

Orden: contexto de tarea existente; requisitos capturados en orden contractual; selector de un requisito aún sin primera aportación; formulario binario o estructurado; resultado; historia de tarea existente. Un requisito ya aportado se muestra informativamente y no permite sobrescribir. No se ofrece sustitución. Se conserva Volver a Mi trabajo, filtros/cursor protegidos y foco de origen. No se infiere cantidad total de ítems desde una página: lectura de evidencia pagina por cursor hasta verificar el requisito seleccionado, o bloquea la acción si la consulta falla.

UI-E04: seleccionar archivo → revisión de nombre/tipo/tamaño y cálculo SHA-256 en memoria temporal → «Cargar archivo» → intención → PUT directo → complete → consulta de estado. La respuesta firmada se mantiene sólo en memoria JS durante transporte; no se escribe en marcado/atributos/hidden inputs, enlaces, almacenamiento del navegador ni telemetría. No se muestra el hash completo. Un PUT con progreso 100 % sólo indica transferencia, nunca evidencia aportada.

UI-E05: LIMPIO confirmado habilita «Aportar evidencia», por decisión explícita del usuario. La API revalida todo y sólo una respuesta de aporte confirmada permite anunciar resultado. Para evidencia estructurada se muestra directamente el formulario cerrado y «Aportar evidencia», sin upload-intents, PUT, S3, complete ni escaneo.

Sin JavaScript, los formularios estructurados conservan POST Razor y validación servidor. Para el PUT directo, mostrar ayuda explícita de mejora necesaria sin simular carga ni transferir el binario a Web. No hay service worker, persistencia del archivo, recuperación automática del cuerpo tras login ni carga en segundo plano después de cerrar el detalle.

Paneles blancos, fondo cálido, h1 Poppins existente, h2 por sección, una columna, controles compartidos y secundarios neutros. Sólo variables oficiales para visuales. No se cambia tipografía, logo o shell aprobado. No se vuelve a diseñar toda la aplicación ni se usa la maqueta externa como dependencia.

## 5. Formularios cerrados propuestos para BR-D08

Todas las filas incluyen schemaVersion=1 fijo. Las etiquetas de esta tabla son **propuestas de presentación**; los campos y reglas son **contratos aprobados de Adenda 20 §8**.

| TAR / requisito | Campos exactos → etiquetas propuestas |
|---|---|
| TAR-0005 / CALCULO_AVANCE | expectedTarget → Meta esperada; actualSales → Venta real; sourceReference → Referencia de la fuente |
| TAR-0005 / ACCION_O_CONFORMIDAD | outcome → Resultado (Acción/Conformidad); actionDescription → Descripción de la acción; responsiblePersonId → Identificador de la persona responsable; startsAt → Inicio de la acción |
| TAR-0007 / LIBERACION | releasedAt → Fecha y hora de liberación; releaseReference → Referencia de liberación |
| TAR-0007 / MERCANCIA | merchandiseReference → Referencia de mercancía |
| TAR-0007 / FECHA_HORA | occurredAt → Fecha y hora del hecho |
| TAR-0007 / RETORNO_EXHIBICION | returnedAt → Fecha y hora de retorno; returnReference → Referencia de retorno |
| TAR-0008 / SECUENCIA | sequenceSummary → Secuencia de hechos |
| TAR-0008 / DECISION | decisionSummary → Decisión; decidedAt → Fecha y hora de decisión |
| TAR-0008 / FUNDAMENTO | foundationSummary → Fundamento |
| TAR-0008 / AVISO_INTERNO | noticeReference → Referencia del aviso interno; notifiedAt → Fecha y hora del aviso |
| TAR-0011 / EVALUACION | assessmentSummary → Evaluación; assessedAt → Fecha y hora de evaluación |
| TAR-0011 / REPARACION_O_CAMBIO | solutionType → Solución (Reparación/Cambio); solutionReference → Referencia de la solución; completedAt → Fecha y hora de terminación |
| TAR-0011 / ENTREGA | deliveryReference → Referencia de entrega; deliveredAt → Fecha y hora de entrega |
| TAR-0018 / CHECKLIST_COMPLETO | Diez respuestas Sí/No en el orden indicado abajo |
| TAR-0026 / FORM_ADM_02 | formCode=FORM-ADM-02 fijo; formReference → Referencia del formulario; completedAt → Fecha y hora de llenado |
| TAR-0092 / F_ENT_001 | formCode=F-ENT-001 fijo; formReference → Referencia del formulario; completedAt → Fecha y hora de llenado; hasDifference → ¿Hay diferencia?; hasDamage → ¿Hay daño? |
| TAR-0093 / ANOTACION_F_ENT_001 | formCode=F-ENT-001 fijo; formReference → Referencia del formulario; annotationReference → Referencia de la anotación; recordedAt → Fecha y hora de registro |
| TAR-0093 / CONSTANCIA_AVISO_INTERNO | noticeReference → Referencia del aviso interno; notifiedAt → Fecha y hora del aviso |

Checklist: productCorrect → Producto correcto; zoneAndFamilyCorrect → Zona y familia correctas; stableFormation → Formación estable; labelsVisible → Etiquetas visibles; alignmentConsistent → Alineación consistente; occupancyJustified → Ocupación justificada; clean → Limpio; intact → Íntegro; signageCorrect → Señalización correcta; matchesPlanogramOrList → Coincide con el planograma o lista. Cada grupo usa fieldset/legend, error asociado y área mínima. No se marca todo por defecto ni existe «seleccionar todo».

Reglas comunes: referencias 1–120 y textos 1–500 caracteres; NFC/trim y rechazo de controles, HTML, Markdown, URL/ruta/datos binarios conforme al validador existente. Decimales con máximo 15 enteros/2 decimales; meta >0, venta >=0. No usar cálculo floating point como autoridad. Instantes introducidos con datetime-local y zona operativa explícita, convertidos por servidor a UTC Z; nunca usar zona del navegador ni rellenar «ahora» sin decisión. UUID canónico D minúsculo. Campos adicionales/ausentes/tipo incorrecto/esquema distinto rechazados en servidor.

ACCION exige descripción/persona/inicio; CONFORMIDAD envía los tres null explícitos. No se calcula porcentaje en cliente como regla de aporte ni se bloquea un payload válido por relaciones de conclusión reservadas a HU-026. Checklist con un No se conserva como evidencia válida, sin anunciar cumplimiento. Foto condicional de TAR-0092 sólo se ofrece si F_ENT_001 vigente autorizado resuelve verdadero; ausente/incoherente bloquea y false no equivale a pendiente. Esta presentación no es autoridad: intención, complete y aporte revalidan el hecho en servidor.

Binarios contratados: fotografías JPEG/PNG; EXPEDIENTE, AUTORIZACION, COMPROBANTES, PLANOGRAMA_O_LISTA, COMPROBANTE_LOCALIZABLE y DOCUMENTO_RECEPCION en PDF; fotografía final/diferencia/incidencia según la política capturada. DOCUMENTO_RECEPCION exige subtipo NOTA/REMISION/FACTURA; los demás no añaden ese metadato. accept/extensión/MIME en navegador son ayudas, no acreditan tipo real ni firma estructural.

## 6. Estados y mensajes

**Ya aprobados:** normal/foco/deshabilitado/error/cargando/vacío; mensajes comunes de CSRF/401/403/404/conflicto; los cinco mensajes de escaneo de estados-y-mensajes.md. Se conservan literalmente y no se reutiliza COMPLETA como estado de transferencia.

**Propuesta consumidora pendiente:**

| Situación | Mensaje / acción propuesta |
|---|---|
| Vacío inicial | «Selecciona un requisito para aportar evidencia.» / selector autorizado |
| Sin selección binaria | «Selecciona un archivo del tipo permitido, de hasta 15 MiB.» |
| Política capturada ausente | «Esta tarea no tiene una política de evidencia disponible.» / sin aporte |
| Ningún requisito admite primera aportación | «No hay requisitos disponibles para una primera aportación.» / volver al detalle, sin prometer completitud |
| Falta evidencia existente | «Aún no hay evidencia aportada para este requisito.» |
| Calculando SHA-256 | «Preparando el archivo…» / ocupado, sin porcentaje ficticio |
| PUT | «Cargando archivo…» / progreso real y Cancelar carga |
| PENDIENTE_CARGA / complete | «La carga todavía no está confirmada.» / «Confirmando carga…» |
| PENDIENTE_ESCANEO | Mensaje aprobado de PENDIENTE: «Esperando análisis antimalware.» / Actualizar estado o Detener seguimiento |
| LIMPIO | «El archivo terminó el análisis y puede vincularse como evidencia.» / Aportar evidencia habilitado |
| INFECTADO | «El archivo fue rechazado por seguridad y no se vinculó.» |
| INVALIDO | «El archivo no cumple el tipo o formato permitido y no se vinculó.» |
| ERROR_ESCANEO | «No se pudo completar el análisis. El archivo permanece sin vincular.» |
| Aporte en curso | «Aportando evidencia…» / control inactivo |
| Aporte confirmado nuevo | «Se aportó la evidencia.» / «Se guardó su primera versión.» |
| Replay confirmado | «Se recuperó la aportación registrada.» / no anunciar otra creación |
| Cancelación local | «Se detuvo la carga en este navegador. No se aportó evidencia.» / no afirmar borrado remoto |
| Detener polling | «Se detuvo el seguimiento. El análisis puede continuar.» / Actualizar estado |
| PUT/complete/POST incierto | «No se pudo confirmar el resultado.» / consultar estado o recuperar la misma solicitud; nunca crear otra automáticamente |
| 413 / 415 | «El archivo supera 15 MiB.» / «El tipo de archivo no está permitido para este requisito.» |
| 410 | «La intención de carga venció. Prepara una nueva carga.» / decisión explícita, nueva clave |
| 409 CARGA_NO_ENCONTRADA | «No se encontró la carga para confirmarla.» / no aportar, consultar antes de decidir otro intento |
| 409 EVIDENCIA_YA_EXISTE / ARCHIVO_YA_VINCULADO | «La evidencia ya está registrada o el archivo ya fue vinculado.» / recarga; sin sobrescribir |
| 409 IDEMPOTENCY_CONFLICT | «La solicitud original no coincide con estos datos.» / sin reenvío ni cambio automático de clave |
| 422 PAYLOAD_EVIDENCIA_INVALIDO / REQUISITO_EVIDENCIA_INVALIDO | «Revisa los datos del requisito seleccionado.» / errores allowlisted asociados al campo, sin reflejar payload |
| 422 ARCHIVO_NO_LIMPIO | «El archivo todavía no puede aportarse como evidencia.» / consultar estado |
| Condición UNRESOLVED | «Primero aporta un formulario F-ENT-001 válido para determinar si corresponde la fotografía.» |
| Condición false | «La fotografía no aplica según el formulario F-ENT-001 vigente.» |
| Checklist con algún No guardado | «La evidencia se guardó. Una respuesta No no acredita la conformidad del checklist.» |
| 429 | «Se alcanzó el límite de intenciones de carga. Inténtalo más tarde.» / sin temporizador inventado |
| 503 / fallo de transporte | «No fue posible completar esta operación.» / correlationId seguro si existe, sin detalle del proveedor |
| Sin JavaScript para binario | «Para cargar un archivo, activa JavaScript y recarga esta página.» |

Nuevo intento después de INFECTADO/INVALIDO/ERROR_ESCANEO sólo por acción explícita «Preparar otra carga»; nunca rehabilita un archivo terminal ni reintenta el escáner desde UI. Errores desconocidos usan presentación común segura. Para cualquier mensaje adicional necesario no recogido aquí, detener esa parte y presentar su texto antes de implementarlo.

Propuesta de asociación visual exacta: PENDIENTE_CARGA → «Pendiente de carga», upload; PENDIENTE_ESCANEO → «Pendiente de análisis», reloj, par info; LIMPIO → «Archivo limpio», check, par éxito; INFECTADO → «Archivo rechazado por seguridad», escudo, par peligro; INVALIDO → «Archivo inválido», alerta, par peligro; ERROR_ESCANEO → «Error de análisis», alerta, par peligro. Progreso/selección son estados de presentación, no estados persistidos nuevos. Textos e iconos siempre juntos; se incorporará la precisión de pending en diseño después de aprobarla.

Foco: selector y campos en orden DOM; errores enfocan resumen y enlazan al control; progreso/status usa aria-live polite, sin mover foco en cada consulta. Si Aportar desaparece tras éxito, foco al resultado de sección. Cancelar/Detener devuelve foco a la acción disponible o encabezado. Vacío/error regional obligatorio, sin retirar contexto autorizado de tarea por fallo de otra región. Durante transferencia/cambio de requisito se bloquean acciones incompatibles; sin permiso se ocultan.

## 7. Contratos, transporte y seguridad

| Operación existente | Consumo y garantías |
|---|---|
| POST /api/v1/files/upload-intents | Cuerpo exacto: obligationId, requirementCode, originalFileName, declaredMediaType, sizeBytes, sha256, documentSubtype según contrato. Clave por intención y CSRF de sesión. 1..15,728,640 bytes; respuesta firmada sólo temporal. Ventana 10 min; rate limit persistente 30/h. |
| PUT firmado de S3 | Método/objeto/headers exactos recibidos; credenciales del navegador omitidas, sin cookies/token CSRF SGOL a S3, sin Referer y sin log de URL/response crudo. Content-Length lo emite el navegador para el File/Blob exacto, no se intenta fijar una cabecera prohibida. Probar que el valor real conserva la firma; no relajar la firma si el proveedor/navegador rechaza. |
| POST /api/v1/files/{id}/complete | `{}` exacto, clave propia distinta de intención/aporte y CSRF; sólo después de respuesta PUT satisfactoria confirmada. 202/PENDIENTE_ESCANEO no acredita limpio. No convertir error HTTP/abort/timeouts en éxito. |
| GET /api/v1/files/{id}/status | Reautorizado, sin escribir. Propuesta: polling secuencial cada 5 s únicamente tras complete confirmado, mientras visible y pendiente, sin consultas superpuestas; suspender con pestaña oculta, navegación, cancelación o error. Ofrecer Actualizar estado manual. Una actualización no reinicia ni acelera Worker. Intervalo técnico, sin ampliar reintentos de negocio. |
| POST /api/v1/obligations/{id}/evidence | XOR fileId/structuredPayload; primera aportación solamente, clave estable por cuerpo canónico y CSRF. Backend exige LIMPIO/mismo actor, obligación, política, requisito/estado y ausencia de ítem. 201 y ETag recibidos; no imponer If-Match a un endpoint que no lo exige. |
| GET /api/v1/obligations/{id}/evidence | Autorización PER-TAREA-VER/alcance, filtros/cursor cerrados y límite 25 por defecto; contenido estructurado sólo en contexto autorizado confidencial. No mostrar payload en errores/logs/capturas reales. |

La sustitución que exige If-Match no se consume; sus endpoints/contratos existentes se conservan. CSRF Razor y X-CSRF-TOKEN/API con el par vigente, usando el puente allowlisted existente. sesión request-scoped, cookies HttpOnly y no-store/no-referrer; 401 descarta formulario/URL temporal/estado de sesión y vuelve a acceso, sin recuperar cuerpos sensibles. 403/404 no revelan datos fuera de alcance. PUT firmado es la única excepción externa ya contratada; no activar CORS general de API ni reparar CORS desde runtime. La limitación de privilegios gruesos SeaweedFS de Adenda 18 §25 no se presenta como aislamiento de producción demostrado.

Cada mutación tiene una intención estable ligada a actor, recurso, operación y cuerpo. Propuesta: token protegido Data Protection para recuperación en POST de intención/complete/aporte, sin URL firmada ni archivo; para payload estructurado, nunca cuerpo en URL o browser storage, no log/ToString de DTO sensible. Vigencia del token limitada por sesión y, para carga/complete, por vencimiento firmado recibido. Solicitud incierta sólo se recupera con la misma clave/cuerpo tras decisión explícita. Una edición prepara otra intención explícita, sin reutilizar clave. No se reenvía PUT automáticamente sobre resultado incierto: consultar estado y complete con su intención; If-None-Match impide overwrite.

Tipo real/firma/estructura, SHA-256 real y ClamAV son autoridad del Worker existente. No se cambia pipeline, motor, catálogo ni reintentos. Transferencia, hash o estado cliente no autorizan vínculo. Auditoría/transacción/idempotencia/historia de backend se conservan; no escribir duplicados desde Razor. GET ordinarios y nueva proyección propuesta sin auditoría/idempotencia/outbox/snapshot.

## 8. Archivos previstos después de aprobación

| Grupo | Archivos/superficie directamente afectados |
|---|---|
| Adopción documental | Nueva adenda F07 consumidora en raíz (número libre por verificar); docs/design/componentes.md, estados-y-mensajes.md, estados-de-dominio.md, navegacion.md; INDICE_IDS.md para el nuevo contrato. Sin modificar congelados. |
| Presentación | src/Sgol.Web/Pages/MyWork/Details.cshtml y Details.cshtml.cs; nuevo partial _EvidenceContribution.cshtml y archivo parcial de handlers si conviene; _Inbox.cshtml sólo para acción recibida. |
| Composición cerrada | Nuevos helpers/DTO de presentación bajo src/Sgol.Web/Interface/MyWork/; _UploadPresentation.cshtml y ComponentModels.cs sólo extensión necesaria de accesibilidad/accept/estados. |
| Cliente y transporte | Interface/ApiClient existente sólo si necesita conservar DTO seguro/intenciones; nuevo wwwroot/js/evidence-contribution.js para hash/PUT/polling, components.css sólo variantes compartidas. Nada de CSS literal nuevo fuera de tokens; no cambios de tokens previstos. |
| Lectura propuesta §3.3 | Modules/Execution/Contracts/ObligationQueries.cs, lector/endpoints de detalle y contrato lector del dueño de política estrictamente necesario; pruebas antiguas de serialización/presentación adaptadas aditivamente. No negocio nuevo en Web. |
| Pruebas | Unitarias de FRONT-017/cliente/render; EvidenceApiEndpointTests, StructuredEvidencePayloadValidatorTests y Front016PresentationTests directamente afectados; integración de aporte/proyección/auditoría y harness navegador FRONT-017; corpus sintético existente. Nombres nuevos de tests son previstos, no IDs contractuales. |
| Trazabilidad | Este plan con aprobación literal; FRONT_017_CARGA_Y_APORTE.md al implementar; IMPLEMENTATION_STATUS.md y README.md. Capturas/manifiesto sanitizados en .artifacts o carpeta externa, sin Fuentes. |

No se prevén dependencias NuGet, cambios de CI, Dockerfile, migraciones ni alteración de tablas. Defecto necesario de contrato/seguridad descubierto se reproduce, se acota y, si cambia contrato, se presenta antes de editarlo. Mantener inventarios de arquitectura/serialización/pruebas antiguas directamente afectados, sin exenciones generales para hacer pasar gates.

## 9. Validación local y criterios de entrega

Tras aprobación y código: `dotnet build --no-restore --configuration Release`; `dotnet test --no-build --configuration Release --filter <pruebas nuevas/directamente afectadas>` por proyecto; `git diff --check`. Restore locked una sola vez sólo si es necesario. SDK fijado 10.0.400 sin cambiar global.json. Preflight de planificación informa que no se resuelve ese SDK en PATH; el informe anterior documenta instalación aislada, cuya utilización se verificará al implementar sin convertir la limitación en éxito.

| Conjunto enfocado | Casos y evidencia exigidos |
|---|---|
| Positivos | JPEG/PNG/PDF sintéticos admitidos y recorrido firmado real hasta LIMPIO + primera versión; los 18 payloads exactos; subtipo recepción; foto sólo con formulario vigente verdadero; mismo resultado en replay. |
| Negativos | Vacío, 15 MiB exactos/límite+1, MIME/extensión/firma/estructura/hash discordantes; INFECTADO/INVALIDO/ERROR_ESCANEO/timeout; expiración, rate limit, objeto ausente, condición false/UNRESOLVED, payload extra/ausente/repetido/HTML/URL/tipo/límite; checklist false guardado sin anunciar conformidad. |
| Autorización/CSRF | Cuatro roles sobre tarea propia pendiente según permiso; otro responsable, histórico, par/inferior/superior sobre pendiente, otra sucursal, empleo/rol/cuenta inválidos, tarea concluida, cambio de asignación/sesión durante flujo; 401 precede CSRF, autenticado sin par válido sin invocar negocio; 404 convergente. |
| Transacción/auditoría/no-efecto | Eventos de intención/complete/aporte con contenido minimizado; falla de auditoría revierte ítem/versión/vínculo/idempotencia/outbox pertinentes. No vínculo antes de LIMPIO, doble click/replay/concurrencia crea sólo primera versión. GET/status/proyección, cancelación y errores cliente no alteran ejecución, validación, avisos o historia funcional. Los estados técnicos de rechazo sí conservan sus efectos aprobados. |
| PostgreSQL real | Snapshot congelado vs política nueva, transacciones/unicidad/idempotencia/concurrencia y rollback pertinentes. No SQLite. Ejecutar sólo casos afectados, no suites integrales de todo el repositorio. |
| SeaweedFS/ClamAV | Recorrido binario real proporcional, firma/headers reales emitidos por navegador, CORS permitido/denegado, privado, antimalware seguro y Worker existente. No afirmar la limitación residual de CORS como garantía productiva. |
| Navegador hospedado | Chromium escritorio/WebKit móvil, carga→escaneo→aporte real con datos sintéticos, formulario/checklist, error/vacío/cancelación/incierto; URLs/headers sensibles excluidos de capturas, consola, trazas de red y reportes. Si se necesitan veredictos deterministas mediante doble de prueba, identificarlos como simulación y acreditar aparte el caso real; no afirmar escaneo real por un doble. |
| Visual/a11y | Contraste texto/UI, teclado, labels/resumen/aria-live, foco/retorno, 44×44, texto 200 %, reflow 320 CSS, escritorio/móvil y prefers-reduced-motion. Sin ocultar overflow global, sin sustituir zoom nativo con CSS como evidencia equivalente. |

Capturas sintéticas de selección/formulario, progreso, escaneo pendiente, limpio sin aporte, aporte confirmado, errores y vacío. Explicar en lenguaje cotidiano: elegir requisito, cargar archivo, esperar análisis, pulsar Aportar evidencia; para formulario completar campos y aportar. Solicitar revisión visual. Etiquetar cada captura como aplicación funcionando o previsualización Razor; una previsualización no acredita PUT/servidor/escaneo/persistencia. No guardar la URL firmada ni contenidos reales, incluso si temporalmente existen en memoria de transporte.

Validaciones diferidas actuales: zoom nativo, lector de pantalla y dispositivos físicos del hito anterior, porque no se ejecutaron en su recorrido; siguen pendientes. En este turno build, pruebas funcionales, servicios y revisión visual de FRONT-017 **no ejecutados porque sólo se solicita el plan**; no son éxitos ni fallos de implementación. La resolución SDK está pendiente para la compilación futura. Suites generales/gates costosos se reservan al hito autorizado. Cualquier incompatibilidad AMD64 demostrada se registra VALIDACION_DIFERIDA_POR_ARQUITECTURA con la prueba exacta; no se presume incompatibilidad de Docker disponible.

Entrega futura: Implementada localmente sólo con alcance y trazabilidad existentes y build/pruebas enfocadas compatibles correctos; comandos/resultados/límites reales, archivos y commits, capturas y petición de revisión visual. No declarar CP de sustitución ni conclusión/validación satisfechos. Publicación explícita futura incluye push/PR/checks/correcciones del mismo hito y heartbeat verificado en este chat si terminamos con checks pendientes. Sólo con cabeza/checks/requisitos vigentes correctos se solicita aprobación expresa de merge con PR/SHA/run. No merge ni despliegue por publicar.

## 10. Resultado de esta preparación y decisión solicitada

Preflight ejecutado como única comprobación inicial: árbol y Fuentes limpios, sesión gh autenticada, HEAD de cabeza validada; SDK 10.0.400 no resuelto, se informa 10.0.401 instalado. Git status/log y objeto de merge inspeccionados sin cambios ajenos iniciales. No gates previos repetidos, no red/publicación, no interfaces/código ejecutable editados.

Se corrige el registro vigente de diseño v2 en IMPLEMENTATION_STATUS.md y DISENO_RENOVADO_V2_IMPLEMENTACION.md con la evidencia aportada del PR #85 y su merge confirmado localmente. Los registros históricos se conservan contextualizados; no se abre otro PR administrativo ni se reabre el hito. FRONT-017 queda Plan preparado pendiente de aprobación, no Implementada localmente.

**Decisión solicitada:** aprobar íntegramente este plan, especialmente BR-D08 (§3.2 y §§4–6), la ampliación mínima de lectura de política capturada (§3.3) y su incorporación por adenda antes del código. La espera procede de INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md §3.7–8 y de la orden expresa del responsable de aprobar el plan y las carencias antes de implementar. Sin esa decisión sólo se ha preparado documentación.
