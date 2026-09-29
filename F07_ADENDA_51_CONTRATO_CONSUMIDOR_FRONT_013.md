# SGOL — Adenda 51 a F07: contrato consumidor y alta manual completa de FRONT-013

## 1. Control y eficacia

| Campo | Valor |
|---|---|
| Estado | **APROBADA ÍNTEGRAMENTE; PUBLICADA EN RAMA, PENDIENTE DE MERGE** |
| Historia exclusiva | FRONT-013 / UI-G01; HU-014/HU-015 y CAT-001..008 |
| Base comprobada | `e87c64c3713fe53f572a4d4f4d8c4088022bca4a`, PR #78 integrado |
| Numeración | 51, siguiente a la Adenda 50; disponibilidad confirmada con master remoto en la base verificada antes de publicación |
| Aprobación recibida | «Lo autorizo»: lectura y diseño de FRONT_013_PROPUESTA_CONSUMIDOR.md y preparación de esta adenda |
| Aprobación íntegra | El usuario confirmó «Si la apruebo» para todas las decisiones de esta adenda; autoriza implementar FRONT-013, sin publicar ni integrar |
| Protección | Sin cambios a F00–F07 congelados ni Fuentes; commit/push/PR autorizados posteriormente mediante «Si autorizo»; merge requiere segunda autorización |

Preparar esta adenda no equivale a aprobar sus decisiones nuevas. La lectura básica de opciones y la composición de UI-G01 ya aprobadas se conservan; no se vuelve a pedir su aprobación por separado. La aprobación íntegra posterior «Si la apruebo» da eficacia a todas las secciones; las palabras propuesta/propuesto conservadas abajo documentan su origen, no un bloqueo pendiente.

## 2. Hechos y resolución propuesta

Fuentes: F05 filas TAR 46–53 y F05-JP-001..009; CA/CP-014/015; CAT/CPT-001..008; F06 API, modelo de datos sección 9 y arquitectura; Adendas 20, 21, 30, 31, 44, 45 y 46. Adenda 45 fila 86 exige BR-API06 resuelta; el identificador sólo aparece allí en el Markdown versionado de la base. No es evidencia de resolución.

El POST vigente admite exactamente cinco campos y sólo valida referencia no vacía bajo MANUAL_REFERENCE_V1. No recibe input_payload ni conecta el materializador manual. AcceptedReplayAndConflictAreAuditedWithoutCreatingAnObligation exige obligationId nulo después de CreateAsync. El materializador interno existe y sus garantías PostgreSQL están probadas separadamente.

Propuesta: conservar replay y consultas históricos, agregar una variante versionada al mismo POST y confirmar solicitud/obligación/input/versiones/auditoría/idempotencia en una sola transacción. MANUAL_REFERENCE_V1 permanece como tipo; la versión del cuerpo identifica el esquema de alta. No se convierte una recurrencia en manual.

Las referencias de separado, operación, autorización, proveedor y planograma son identificadores documentales externos a SGOL, sin URL. No existe catálogo interno para comprobar su verdad material. Esta propuesta acepta una declaración trazable del creador; no verifica documentos ni autoriza garantías. El padre interno TAR-0092 sí exige existencia y visibilidad comprobadas en PostgreSQL. Esta diferencia debe ser visible en la UI y aceptada expresamente al aprobar la adenda.

## 3. Frontera

Incluye seis formularios, lectura de opciones y padres, período/regla, resultado, replay, no efecto y extensión consumidora mínima. Reutiliza cliente HTTP, sesión por petición, CSRF, catálogos y jerarquía.

Excluye asignación, incorporación/publicación de plan, carga/captura de evidencia, conclusión, emisión de validación, avisos/integraciones externas, catálogos comerciales, reparación histórica, recurrencia manual, FRONT-014..020 y TECH-FRONT-005. Crear PENDIENTE no afirma asignación, publicación o validación. Los CPT de conclusión conservan sus contratos; FRONT-013 acredita sólo sus precondiciones de alta.

## 4. Lecturas consumidoras

### 4.1 Opciones — ya aprobada

`GET /api/v1/generation-requests/options`, sin query: `data: [...]`, `meta.count`, `meta.correlationId`. Elementos exactos: taskCode, name, taskDefinitionVersionId, ruleVersionId, branchId, originType. Orden por taskCode; máximo seis manuales, sin cursor por universo cerrado.

Cuenta/persona/empleo/rol vigentes en LOR-001; creadores DIRECCION, ADMINISTRACION y SUBCOORDINACION. Cada fila requiere TAR activa, regla MANUAL vigente y política de elegibilidad de la misma versión con nivel propio o inferior. No entrega borradores/historia/recurrencia. 401 sin sesión, 403 sin autoridad, 200 vacío sin opciones. No amplía el GET administrativo de políticas.

Reutiliza `GET /api/v1/branches/LOR-001` y `GET /api/v1/weeks/{isoYear}/{isoWeek}`. Los IDs salen de respuestas autorizadas, no de campos UUID editables. No agrega ETag/If-Match a generation-requests.

### 4.2 Recepciones padre — propuesta adicional

`GET /api/v1/generation-requests/receipt-origins`: sólo receiptReference opcional (igualdad normalizada), cursor opcional, pageSize opcional (25 predeterminado, 1–100). Repetidos/desconocidos/inválidos: 400 CONSULTA_ORIGEN_INVALIDA. El filtro no entra en URL Razor, historial visible ni logs; se remite sólo al API del mismo origen.

`data`: obligationId, generationRequestId, receiptReference, startedAt, periodId. `meta`: count, nextCursor, correlationId. Sólo TAR-0092 versión 2 válida, PENDIENTE, LOR-001, visible al creador o superior de esa solicitud. El consultante debe ser creador de nivel suficiente para TAR-0093. 200 vacío sin resultados; 403 sin autoridad de sección. No muestra proveedor, mercancía ni nota.

Orden obligationId ascendente; cursor opaco protegido ligado al actor/filtro/pageSize, vigencia 30 minutos. Paginación viva reautorizada por página; POST revalida el padre bajo lock. No promete disponibilidad congelada. Los módulos propietarios exponen lecturas internas; Razor no consulta tablas.

## 5. Solicitud HTTP nueva — propuesta

Misma ruta POST `/api/v1/generation-requests`, sesión plena, CSRF e Idempotency-Key UUID canónico por intención. application/json con exactamente seis propiedades, sin duplicadas/desconocidas:

```json
{
  "schemaVersion": 2,
  "ruleVersionId": "UUID recibido de options",
  "branchId": "UUID recibido de branches/LOR-001",
  "periodId": "UUID recibido de weeks",
  "originType": "MANUAL_REFERENCE_V1",
  "inputPayload": {
    "taskCode": "TAR-0018",
    "eventReference": "EVENTO-SINTETICO-01",
    "zoneReference": "ZONA-SINTETICA-01",
    "planogramReference": "PLANOGRAMA-SINTETICO-V1"
  }
}
```

Los textos UUID del ejemplo son marcadores, no valores válidos. schemaVersion es entero 2. inputPayload es objeto cerrado por taskCode, coincidente con la regla resuelta. No acepta originReference, responsable, plan, estado, resultado, política ni motivo genérico adicionales. El servidor calcula originReference según sección 8; no JS ni concatenación Razor.

Cota propuesta: 32 KiB UTF-8, suficiente para los seis esquemas; exceso: 413 GENERATION_REQUEST_DEMASIADO_GRANDE. Sin archivos/base64/HTML. Sintaxis inválida, duplicadas/desconocidas, tipo o versión incorrectos: 400 GENERATION_REQUEST_INVALIDA. Forma correcta con valor funcional inválido: 422 ORIGEN_INVALIDO.

### 5.1 Tipos comunes propuestos

- Reference: string 1–120 valores escalares Unicode, Trim y NFC, sin controles ni URI absoluta. Identificador documental sin interpretación como enlace/HTML. Igualdad ordinal sensible a mayúsculas; no colapsar espacios internos.
- Summary: string 1–500 valores escalares Unicode, Trim/NFC, sin controles; texto codificado, no evidencia adjunta.
- Instant: string RFC 3339 UTC terminado en Z, máximo microsegundos. Razor convierte datetime-local usando America/Mexico_City. Un instante local ambiguo/inexistente se rechaza; no se usa zona del navegador.
- UUID: formato D no vacío. Enum: valor exacto. Todos los campos listados son obligatorios y no null.
- Hash idempotente: normalización HU-034 más la de este DTO. Cambiar orden de propiedades no cambia intención. No truncar ni corregir valores automáticamente.

## 6. Formularios cerrados — propuesta

Cada inputPayload lleva taskCode y exactamente los campos de su fila:

| CAT / TAR | Campo JSON: etiqueta — tipo |
|---|---|
| CAT-002 / TAR-0007 | reservationReference: Separado — Reference; merchandiseReference: Mercancía — Reference; startedAt: Inicio del separado — Instant; expiresAt: Vencimiento documentado — Instant; sourceReference: Documento de origen — Reference |
| CAT-003 / TAR-0008 | operationReference: Operación — Reference; detectedAt: Detección — Instant; claimantReferences: Referencias de reclamantes — lista de Reference |
| CAT-004 / TAR-0011 | caseReference: Expediente — Reference; authorizationReference: Autorización previa — Reference; productReference: Producto — Reference; solutionType: Solución autorizada — REPARACION o CAMBIO; authorizedAt: Fecha y hora de autorización — Instant |
| CAT-005 / TAR-0018 | eventReference: Evento de exhibición — Reference; zoneReference: Zona — Reference; planogramReference: Planograma o lista vigente — Reference |
| CAT-007 / TAR-0092 | receiptReference: ID de recepción — Reference; supplierReference: Proveedor — Reference; documentReference: Nota, remisión o factura — Reference; startedAt: Inicio de recepción — Instant; merchandiseReference: Mercancía — Reference |
| CAT-008 / TAR-0093 | parentObligationId: Recepción padre — UUID seleccionado; incidentReference: Referencia del incidente — Reference; incidentType: Tipo de incidencia — DIFERENCIA, DANO o DIFERENCIA_Y_DANO; description: Descripción — Summary; occurredAt: Momento de la incidencia — Instant |

Nombres, límites, identificación de reclamantes, tipos de incidencia y referencia estable del incidente son decisiones nuevas pendientes de aprobación. REPARACION/CAMBIO reutiliza vocabulario de evidencia; los tres tipos propuestos derivan de diferencia/daño. incidentReference concreta «recepción+incidente» de F05; no se genera un código diferente en cada reintento.

TAR-0005/CAT-001 y TAR-0026/CAT-006 no tienen formulario manual; su regla recurrente devuelve 409 REGLA_MANUAL_REQUERIDA, sin efecto. Worker conserva horarios, origen y productor.

### 6.1 Reglas por formulario

- TAR-0007: startedAt no futuro; expiresAt > startedAt. Ayuda «Plazo ordinario: 24 horas». Un plazo distinto se registra sólo como plazo ya documentado por sourceReference; SGOL no concede ampliaciones. Puede crearse antes de vencer, sin autorizar conclusión temprana. dueAt = expiresAt.
- TAR-0008: detectedAt no futuro; entre 2 y 20 claimantReferences distintas normalizadas, ordenadas ordinalmente para hash/equivalencia. No son roles ni autoridades. dueAt = detectedAt + 30 minutos. Un alta tardía conserva el objetivo vencido, no lo desplaza. Expediente, evidencia, decisión, fundamento y aviso se aportan después.
- TAR-0011: authorizedAt no futuro; la referencia declara autorización externa previa y no la otorga SGOL. No se infiere autoridad del nombre ni se aprueba garantía. Solución fija; dueAt según 6.2. AUTORIZACION/COMPROBANTES binarios se aportan después y no quedan satisfechos por esta referencia.
- TAR-0018: tres referencias obligatorias; planograma identifica versión documental vigente declarada, sin catálogo sincronizado. Sin SLA: dueAt=null. Checklist/foto/documento posteriores.
- TAR-0092: startedAt no futuro, recepción estable obligatoria. Sin SLA: dueAt=null; no se convierte el KPI de 45 minutos en obligación. Documento/F-ENT-001/foto condicional posteriores.
- TAR-0093: padre visible TAR-0092, LOR-001, PENDIENTE y versión 2 válida; occurredAt no futuro ni anterior al inicio del padre. Recepción heredada, no cadena duplicada editable. Período igual al del padre, mostrado como sólo lectura. Sin SLA: dueAt=null. Sin crear aviso ni satisfacer foto/anotación/constancia.

No se obliga a que un instante de origen caiga en la semana elegida, salvo igualdad de período con el padre. No crea cierre/reapertura semanal. Estas reglas temporales son propuestas expresas, no inferencias desde fixtures.

### 6.2 Siete hábiles — propuesta expresa

Para TAR-0011, contar desde la fecha siguiente a authorizedAt en America/Mexico_City, excluyendo el día de autorización. Contar siete fechas laborables del calendario publicado vigente al aceptar. Conservar hora local de authorizedAt en la séptima y convertir a UTC como dueAt.

Consultar máximo 366 días posteriores. Un hueco intermedio, solapamiento, menos de siete laborables o instante local no resoluble produce 422 CALENDARIO_GENERACION_INCOMPLETO. No sustituir por lunes–viernes. Persistir IDs de CalendarDayVersion usados hasta el séptimo y dueAt, inmutables ante publicaciones posteriores. Inicio/exclusión/hora y cota de consulta requieren aprobación porque F05 sólo fija «siete días hábiles».

## 7. Seguridad y revalidación

La proyección PER-OBLIGACION-CREAR sólo presenta controles. Servidor revalida cuenta/persona/empleo/rol, LOR-001, permiso y nivel TAR. Recurso inexistente/invisible devuelve 404 convergente sin datos parciales. Piso no adquiere creación por ser ejecutor.

Antes del replay se verifica autoridad actual y visibilidad del resultado original. No se vuelve a exigir regla activa ni padre PENDIENTE. Una intención nueva revalida reglas/políticas/período; TAR-0093 bloquea el padre de forma que la conclusión se serializa antes o después del alta, nunca entre validación y commit.

Referencias externas no se resuelven por red. «Fuera de alcance» se comprueba para sucursal/período/versión/padre interno; no se simula validación de catálogos externos inexistentes.

## 8. Identidad, equivalencia y unicidad

Conservar RN-010: regla+sucursal+período+tipo+origen. Para versión 2, la identidad nominal se calcula en servidor:

| TAR | Tupla en orden fijo | Protección adicional CAT en LOR-001 |
|---|---|---|
| TAR-0007 | reservationReference, expiresAt | Una por separado+vencimiento, permanente |
| TAR-0008 | operationReference | Una PENDIENTE por operación |
| TAR-0011 | authorizationReference | Una por autorización, permanente |
| TAR-0018 | eventReference, planogramReference | Una por evento+planograma, permanente |
| TAR-0092 | receiptReference | Una PENDIENTE por recepción |
| TAR-0093 | parentObligationId, incidentReference | Una por padre+incidente, permanente |

Origen canónico: JSON array `[2, taskCode, ...tupla]`, sin whitespace, strings normalizados, instantes UTC con seis decimales, UUID minúsculo D. La codificación de strings/arrays reutiliza el canonicalizador JSON de HU-034, sin incluir actor ni clave idempotente; se fija con un vector de prueba por TAR. SHA-256 de UTF-8; originReference persistido = `CAT2:` + 64 hexadecimales minúsculos. Es identidad técnica, no etiqueta de usuario ni hash del payload completo: cambiar descripción no elude unicidad. UI muestra la referencia humana autorizada.

El snapshot conserva la tupla para verificar igualdad exacta ante un digest coincidente. Tuplas diferentes con igual digest fallan cerrado con 409 ORIGEN_IDENTIDAD_INCONSISTENTE, sin entregar recurso ni tratarlo como replay.

Misma clave y mismo cuerpo: mismo status/Location/IDs/snapshot, sólo result=RECUPERADA. Misma clave y cuerpo distinto: 409 IDEMPOTENCY_CONFLICT. Otra clave sólo recupera ganador funcional si regla/sucursal/período/input completo son iguales y visibles al actor. Misma identidad CAT con datos/versiones/período distintos: 409 ORIGEN_YA_REGISTRADO, sin otros IDs. Ganador no visible: 404 convergente. No permite corregir input por sobrescritura.

Conclusión libera sólo la restricción activa de TAR-0008/TAR-0092; RN-010 permanente permanece. Concluir no permite repetir la misma identidad con la misma regla/período. Las protecciones permanentes CAT no se liberan.

## 9. Persistencia y arquitectura propuestas

Reutilizar generation_request, work_obligation, input_payload, due_at, índices funcionales e IdempotencyRecord. Adiciones nullable a work_obligation:

- manual_task_code: una de las seis manuales;
- manual_origin_key: digest de la tupla;
- parent_obligation_id: FK restrict a work_obligation, sólo TAR-0093 versión 2.

Snapshot inmutable en input_payload: `{ schemaVersion: 2, input: <inputPayload normalizado>, originIdentity: <array canónico>, calendarDayVersionIds: [...] }`. Calendario vacío salvo TAR-0011. Legacy conserva todos sus valores incluidos null; sin backfill/borrado/reinterpretación.

Dos índices únicos parciales por branch_id/manual_task_code/manual_origin_key: permanente para TAR-0007/0011/0018/0093; WHERE execution_status='PENDIENTE' para TAR-0008/0092. Coexisten con UX_generation_request_functional_key y UX_work_obligation_generation_request. La transición existente a CONCLUIDA libera el índice parcial automáticamente.

Checks distinguen legacy de versión 2. En versión 2, código/digest/snapshot son obligatorios y coherentes; padre obligatorio sólo para TAR-0093, sin self-reference. FK y locks verifican sucursal/período/TAR del padre. No agrega estado de obligación. Migración forward-only sin BOM y Down bloqueado; no altera recurrencia ni repara datos.

Generation posee origen/solicitud/obligación. Configuration expone versiones/calendario por contrato interno; Identity resuelve autoridad. Endpoint adapta transporte. Razor sólo consume API. Materializador ofrece operación componible sin commit propio y conserva su envoltorio aprobado para otros consumidores; no se simula atomicidad con dos transacciones sucesivas.

## 10. Transacción y fallos

1. Autenticación/CSRF, DTO y cabecera; autoridad y resolución segura.
2. Scope existente GENERATION_REQUEST_CREATE por actor y new:LOR-001; schemaVersion e input normalizado en hash. No cambiar scope para evadir clave histórica.
3. Transacción y reserva scope/key bajo autoridad PostgreSQL. Replay exacto devuelve snapshot antes de reevaluar guardas funcionales.
4. Para alta nueva, bloquear versiones TAR/regla/eligibilidad/evidencia/validación publicadas y compatibles, período y padre. Snapshot de una misma taskDefinitionVersionId; política faltante: 409 CONFIGURACION_GENERACION_INCOMPLETA.
5. Validar datos/calendario/guarda legacy. Crear solicitud ACEPTADA, obligación PENDIENTE y vínculo, input/dueAt/versiones, auditoría y snapshot idempotente en la misma transacción. Se permite SaveChanges intermedio para satisfacer las FK existentes (solicitud sin vínculo, obligación, vínculo final), siempre sin commit intermedio ni visibilidad de éxito antes del commit final.
6. Un commit; éxito HTTP sólo después. Fallo de validación/vínculo/auditoría/snapshot o commit confirmado como fallido revierte todas las escrituras del intento.

Respuesta perdida después del commit exige recuperar con la misma clave/cuerpo. Resultado incierto no habilita otra intención. Concurrencia usa unique/locks; comprobación previa no sustituye restricción. Retry transaccional limitado a HU-034; sin reenvío automático de UI ni retry 412.

Conservar GENERATION_REQUEST_ACCEPTED y WORK_OBLIGATION_CREATED en el mismo commit nuevo, más eventos de recuperación funcional aplicables. Replay terminal no vuelve a materializar/auditar creación. Conflicto usa IDEMPOTENCY_CONFLICT_REJECTED en transacción separada conforme a HU-034. Auditoría nueva de generación sólo incluye IDs, TAR, esquema, resultado y correlación; input/reclamantes/descripción/referencias externas nunca en auditoría/logs/métricas.

## 11. Respuestas y errores

POST versión 2: 201 alta nueva; 200 recuperación funcional con otra clave. Replay de igual clave conserva status/Location originales (incluso 201), result=RECUPERADA y correlationId actual. El status por sí solo no acredita creación nueva.

`data`: schemaVersion=2, taskCode, generationRequestId, ruleVersionId, taskDefinitionVersionId, branchId, periodId, originType, originReference canónica, result, requestedBy, requestedAt, obligationId, evidencePolicyVersionId, validationPolicyVersionId, dueAt, errorCode=null. IDs de obligación/políticas no nulos en nuevo éxito. No incluye inputPayload en POST/snapshot idempotente.

GET `/api/v1/generation-requests/{id:guid}` conserva creador/superior visible. Versión 2 devuelve esos datos persistidos más inputPayload normalizado en sólo lectura. No consulta políticas actuales para reconstruir snapshot, no materializa ni convierte ACEPTADA a RECUPERADA. Sin ETag: solicitud no editable. Cache-Control no-store en API consumidora y páginas.

| HTTP/code | Condición y mensaje |
|---|---|
| 400 GENERATION_REQUEST_INVALIDA | Forma/versión inválida: «Revisa los datos de la solicitud» |
| 400 CONSULTA_ORIGEN_INVALIDA | Query/cursor inválido: «Revisa la búsqueda de recepciones» |
| 401 AUTENTICACION_REQUERIDA | Flujo de sesión existente |
| 403 ACCESO_DENEGADO | «No tienes permiso para crear esta solicitud», sin datos de sección |
| 404 GENERATION_REQUEST_NO_ENCONTRADA | Regla/solicitud/padre/ganador inexistente o invisible: «No existe o no está disponible en tu alcance» |
| 409 REGLA_MANUAL_REQUERIDA | «La regla vigente no permite alta manual» |
| 409 TAR_INACTIVA | «Esta tarea no admite nuevas generaciones» |
| 409 CONFIGURACION_GENERACION_INCOMPLETA | «La configuración de esta tarea está incompleta» |
| 409 ORIGEN_YA_REGISTRADO | «Este origen ya tiene una solicitud incompatible con los datos ingresados», sin otros IDs |
| 409 ORIGEN_IDENTIDAD_INCONSISTENTE | «No se pudo verificar la identidad del origen», sin recuperación automática |
| 409 ORIGEN_PADRE_NO_DISPONIBLE | Padre visible concluido/incompatible: «La recepción ya no admite esta incidencia» |
| 409 GENERACION_LEGACY_REQUIERE_REVISION | «Hay orígenes históricos que requieren revisión antes de nuevas altas» |
| 409 IDEMPOTENCY_CONFLICT | Mensaje/decisión explícita aprobados; no cambiar clave automáticamente |
| 409 IDEMPOTENCY_REPLAY_NO_DISPONIBLE | «No se puede recuperar esta solicitud de forma segura» |
| 422 PERIODO_INVALIDO | «Selecciona un período válido»; incluye discrepancia con período del padre visible |
| 422 ORIGEN_INVALIDO | «Revisa los campos de origen» |
| 422 CALENDARIO_GENERACION_INCOMPLETO | «El calendario publicado no permite calcular el plazo» |
| 422 GENERATION_REQUEST_SCHEMA_REQUERIDO | Alta nueva legacy: «Recarga el formulario para usar el contrato de alta vigente» |
| 413 GENERATION_REQUEST_DEMASIADO_GRANDE | «La solicitud excede el tamaño permitido» |
| 500 IDEMPOTENCY_CONFLICT_AUDIT_FAILED | Mensaje seguro común; no afirmar conflicto auditado |

Sólo errores de campo agregan a Problem Details `fieldErrors`, lista de pares path/code cerrados. path: campos de sección 6 con prefijo inputPayload, o periodId/ruleVersionId; code: REQUIRED, INVALID, DUPLICATE, OUT_OF_RANGE, MISMATCH. No contiene valores, detalle técnico ni excepción. UI traduce por mapa cerrado; resumen enfocable y asociación al campo. Desconocidos conservan mensaje común/correlationId.

RECHAZADA describe el intento fallido sin crear solicitud persistida ni ID ficticio. ACEPTADA/RECUPERADA sólo desde respuestas confirmadas. Rechazo no consume clave, salvo resultado terminal expresamente aprobado por contrato previo.

## 12. Compatibilidad y activación

Cuerpo histórico de cinco campos: reconocido exclusivamente para recuperar una intención confirmada. Igual scope/key/hash devuelve snapshot/status históricos, incluso obligationId=null; no materializa ni completa desde estado nuevo. Otra versión/cuerpo con clave ocupada: conflicto. Intención nueva en formato antiguo: 422 GENERATION_REQUEST_SCHEMA_REQUERIDO, sin reserva.

Fila histórica sin snapshot conserva adaptador legacy aprobado; reconstrucción insegura: IDEMPOTENCY_REPLAY_NO_DISPONIBLE. GET histórico conserva DTO anterior. Sin obligación, mostrar «Solicitud histórica sin obligación vinculada»; no completar/migrar/generar otra automáticamente. UI-G01 sólo emite cuerpos nuevos versión 2.

No puede probarse equivalencia entre referencia legacy arbitraria y origen CAT estructurado. Propuesta conservadora: bloquear altas versión 2 por TAR en LOR-001 mientras exista origen manual legacy potencialmente competidor: cualquier registro legacy para TAR-0007/0011/0018/0093; para TAR-0008/0092, solicitud sin obligación u obligación PENDIENTE. Recurrencias no cuentan. Guarda/creación serializadas por sucursal/TAR; consumidores manuales internos nuevos deben usar contrato completo.

Options omite esas TAR; catálogo sin opciones usa vacío aprobado. POST directo autorizado: GENERACION_LEGACY_REQUIERE_REVISION, sin inventario histórico. Revisar/migrar orígenes legacy, si los hay, requiere tarea/autorización específica; no se borran ni reparan en FRONT-013. Probar guarda con datos sintéticos y reportar condición de activación sin inspeccionar datos reales por iniciativa propia. Esta guarda es una decisión nueva y limita disponibilidad de altas en bases con historia incompleta.

## 13. UI-G01 y ampliaciones al diseño aprobado

Conservar `/planificacion`, semana/calendario y el enlace «Alta manual» a `/planificacion#alta-manual` sólo con permiso proyectado. taskCode allowlisted y generationRequestId UUID en query, más filtros anteriores permitidos. Referencias/cuerpo/claves/CSRF/datos personales fuera de URL. Sin ruta web adicional ni enlace API de menú.

Formularios de sección 6, sin editor JSON. TAR/regla/sucursal/tipo de origen sólo lectura. Ayuda para referencias externas: «Registra la referencia del documento; SGOL no verifica su contenido». Reclamantes: lista accesible con Añadir/Quitar, mínimo dos campos; quitar devuelve foco al anterior o a Añadir. TAR-0093 busca padre mediante POST Razor sin mutación API, selector paginado y período heredado sólo lectura.

Confirmación «Crear solicitud manual» resume TAR/semana/origen, Cancelar como foco inicial y retorno al cerrar. Mensaje adicional propuesto: «Se creará una obligación pendiente; aún no estará asignada ni publicada», sólo tras aprobar/verificar conexión atómica. Sin motivo nuevo.

Resultado: IDs estables de solicitud/obligación, TAR/versiones, período y origen autorizado; «Solicitud aceptada» o «Se recuperó la misma solicitud; no se creó otra». «Consultar resultado» hace GET nuevo. No enlazar pantalla de obligación inexistente.

Intención propuesta: Data Protection ligada a usuario/operación, ocho horas, cuerpo normalizado y clave en campo oculto cifrado de formulario no-store; nunca cookie/URL/localStorage/sessionStorage. Misma intención tras respuesta incierta. Si vence sin ID conocido: «La intención venció y no se pudo confirmar su resultado», sin ofrecer nueva creación automática. Con ID conocido permite GET autorizado. El servidor conserva unicidad funcional. No capturar tokens ni cuerpos.

Carga: aria-busy y «Enviando solicitud…», acción inactiva. Conflicto: sin retry; Recuperar resultado sólo con intención original, Preparar otra solicitud exige decisión explícita sin enviar. 403 retira datos de sección. Errores asociados y resumen enfocable. Padre vacío: «No hay recepciones disponibles para vincular» y «Actualizar recepciones», sin crear padre desde el selector.

Móvil una columna, tablas con scroll interno, html/body dentro del viewport, overflow/text-overflow en select, foco y área 44×44 según tokens. Componentes/textos nuevos aquí (reclamantes, selector padre, intención vencida, mensaje de obligación) siguen propuestos; la aprobación de composición básica no los aprueba por arrastre.

## 14. Matriz de aceptación futura

| Grupo | Pruebas requeridas |
|---|---|
| Lecturas | Tres creadores y Piso; versión vigente/nivel/vacío; padre paginado, cursor alterado/otro actor y padre inexistente/ajeno/concluido sin filtración |
| Formularios | Seis altas completas, faltantes/extras/tipos/límites, referencias normalizadas; rechazo manual de TAR-0005/TAR-0026 |
| Temporalidad | 24 h y plazo documentado distinto; detección+30 min; siete hábiles con festivo/cierre/hueco/límite/cambio de calendario; ausencia de SLA donde corresponde |
| CAT/RN-010 | Carrera con claves distintas e igual identidad, recuperación equivalente/conflicto, unicidad permanente/activa al concluir, distinta regla/período |
| CA/CP-014 | CSRF/cabecera/autoridad/alcance/nivel; replay mismo ID; 409 auditado sin segundo recurso; rol/cuenta revocados no descubren resultado |
| CA/CP-015 | POST real crea solicitud/obligación con snapshot; 20 peticiones concurrentes; fallos inyectados antes de auditoría/vínculo/idempotencia/commit revierten todo; respuesta perdida recupera ambos IDs |
| Compatibilidad | Snapshot legacy con/sin obligación; adaptador anterior; clave legacy y cuerpo v2; alta vieja nueva rechazada; guardas por TAR; ningún backfill/materialización en GET/replay |
| Versiones | Cambiar política/calendario no modifica input/IDs/dueAt; digest contrastado por tupla; replay no reevalúa guardas funcionales ya confirmadas |
| Privacidad | Creación y auditoría mismo commit; input/reclamantes/referencias/descripción fuera de logs/auditoría/capturas; sin secretos |
| Navegador | Kestrel HTTPS/PostgreSQL; seis formularios, normal/foco/deshabilitado/error/carga/vacío, confirmación/replay/conflicto, deep link, teclado y móvil/escritorio |
| Regresión | Category=FRONT_BROWSER completa serializada, panel visible, espera de nueva respuesta POST/GET, html/body y dimensiones PNG; inventarios sólo ante discrepancia real |

Playwright no sustituye autoridad/transacción/auditoría de servidor. Capturas sanitizan todo el encabezado y permanecen fuera del checkout. Los CPT de evidencia/conclusión/validación son referencias de no regresión; no amplían UI-G01.

## 15. Archivos previstos y punto de aprobación

Tras aprobación: contratos/servicios/endpoints Generation, lecturas propietarias mínimas, EF/migración, cliente y sección Razor UI-G01, diseño, pruebas unitarias/API/PostgreSQL/navegador e inventarios afectados. El POST ya inventariado HU-034 no suma otro consumidor por cambiar DTO. Sin dependencias NuGet nuevas ni cambios de Worker.

Este paso sólo prepara documentos: adenda, cuatro documentos de diseño con contenido ya aprobado y trazabilidad. Proyección y pruebas previas permanecen sin nuevos cambios ni commit. Los 32/32 unitarios, 12/12 PostgreSQL y build Release previos no validan este contrato nuevo.

Se solicita aprobación íntegra de las decisiones nuevas: formato v2/retiro de altas nuevas legacy; referencias externas declaradas; seis esquemas/límites/enums; temporalidad; padres; unicidad/guarda legacy; persistencia; atomicidad y replay. Esa aprobación permite implementar exclusivamente FRONT-013 y sus extensiones mínimas. Commit/push/PR y merge conservan las dos autorizaciones originales.
