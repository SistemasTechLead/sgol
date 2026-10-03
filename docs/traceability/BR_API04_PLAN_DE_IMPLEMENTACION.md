# SGOL — BR-API04: definición y plan de implementación de descarga

**Estado vigente: TECH-EVID-003 Implementada localmente; corrección Dirección autorizada mediante «Lo autorizo».** §12 registra ejecución y resultados posteriores; las paradas de §§9–11 son antecedentes superados por las órdenes correspondientes. BR-API04 permanece ABIERTA globalmente.

2026-10-03. **ID, DEFINICIÓN Y PLAN APROBADOS E INCORPORADOS LOCALMENTE POR ADENDA 64**, mediante «Lo apruebo». La solicitud inicial permitía preparar únicamente definición y plan, incluida la propuesta de verificación separada de Dirección. La aprobación posterior autoriza su incorporación documental, sin orden de implementación/ensayo ni autorización separada de verificar Dirección. El contrato de Adenda 63 ya está aprobado: no se somete de nuevo a aprobación. La redacción propositiva de §§1–9 conserva el antecedente revisado; §10 registra el estado vigente y supera sus pendientes de ID/plan.

## 1. Base, fuentes y precedencia

**Hechos documentados:** IMPLEMENTATION_STATUS registra TECH-FRONT-005 Integrada sobre merge `e6355332ea09542c2b38895945c61a1efb83d586`. HEAD inicial `58b12b6d201497671f0da39b30c42105a152f203` contiene ese merge: comprobación local de ascendencia exit 0. Se acepta la evidencia previa sin fetch, consultas remotas o repetición de gates. Los dos ciclos/770 resultados siguen atribuidos exclusivamente a `3693239e181782386d5038d8be4232341fa53a89`.

Se leyeron AGENTS.md, INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md y el estado operativo inicial; se comprobaron Tareas insertadas por adenda, con prevalencia de los registros vigentes sobre las filas históricas. No hay sucesora de código aprobada ni dependencia de publicación que habilite o bloquee este plan. La dependencia documental del contrato está satisfecha; la autoridad de Dirección tiene una diferencia estática pendiente de verificación/resolución separada.

| Fuente operativa | Rango pertinente / uso |
|---|---|
| docs/INDICE_IDS.md | Entradas HU-023/025, CA/CP-023/025, ADR-007 y complemento BR-API04: localización previa |
| F07_BACKLOG_DE_IMPLEMENTACION.md | Fila HU-025, línea 99, y TECH-EVID-001, línea 120: antecedentes; no contienen esta tarea nueva |
| F05_ESPECIFICACION_FUNCIONAL_MVP.md | Líneas 138/140: HU-023/CAP-028 y HU-025/CAP-030 |
| F05_CRITERIOS_DE_ACEPTACION.md | Líneas 53/55: CA-023/025, CP-023-P/N y CP-025-P/N; lectura con alcance e historia conservada |
| F06_CONTRATO_DE_API.md | §5.6, líneas 189–201: ruta y expiración máxima |
| F06_REGISTRO_ADR.md | ADR-007, líneas 105–113: S3 privado, cuarentena, integridad y limpieza |
| F07_ADENDA_15_CONTRATO_DE_CONSULTA_DE_TRABAJO_E_HISTORIA_PERMITIDA_HU_023.md | §§3–6: actor vigente, Dirección sin asignación, alcance PostgreSQL y ocultación |
| F07_ADENDA_62_RESOLUCION_GLOBAL_BR_API04.md | §§1–3: resolución global, exclusiones consumidoras, BR-API04 ABIERTA |
| F07_ADENDA_63_CONTRATO_MINIMO_DE_DESCARGA_BR_API04.md | §§1–3: incorporación exclusiva de contrato, límites y parada |
| PROPUESTA_POST_TECH_FRONT_005.md | §10.2: diferencia Dirección; §§15.1–15.8: contrato aprobado por referencia; §§3/5/6: límites y diferidos heredados |

INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md limita su flujo a FRONT-*. Se aplica su preparación y espera por la invocación expresa del responsable, sin convertir esta tarea backend en FRONT-* ni activar pantallas, capturas, publicación o seguimiento remoto. No se escribe interfaz; las lecturas de diseño previas a escribir UI no se activan en este alcance.

## 2. Definición de tarea sometida a decisión

**Identificador propuesto: `TECH-EVID-003`. No asignado ni aprobado.** La búsqueda acotada de Markdown operativos, excluyendo Fuentes/bin/obj/.vs, no encontró uso previo; TECH-EVID-001/002 son antecedentes existentes. La disponibilidad observada no sustituye una decisión del responsable. No se crea una fila canónica ni se modifica el backlog congelado.

| Campo propuesto | Definición |
|---|---|
| Nombre | Descarga privada del binario original de evidencia mediante autorización temporal |
| Relación | Entrega limitada de la descarga pendiente de BR-API04; no sucesora frontend automática |
| Objetivo | Implementar exclusivamente GET /api/v1/files/{id}/download conforme a Adenda 63 / §§15.1–15.8 |
| Dependencias disponibles | HU-023, HU-025, TECH-EVID-001/002 y base integrada aceptada; contratos de identidad, vínculo, versiones y storage existentes |
| Dependencia pendiente real | Verificación/resolución separada de Dirección de §4 antes de reutilizar CanViewAsync como autoridad de descarga |
| Entrada a ejecución | ID/definición/plan aprobados e incorporados expresamente; orden de implementar; autorización separada de la verificación Dirección |
| Salida local | Código mínimo y trazabilidad, build y pruebas enfocadas compatibles correctas; criterios de §6 cubiertos con resultados reales y diferidos exactos |
| Estado actual | Sólo propuesta preparada; no Implementada localmente, Publicada, Integrada ni Terminada |

Tras aprobar ID y plan, se propone incorporar su definición/precedencia por una nueva adenda F07 en raíz con número libre comprobado en ese momento. No se reserva ni redacta como aprobada una Adenda 64 ahora. Dirección no recibe un segundo ID inventado: su verificación es un entregable separado, con autorización y resultados propios. Si el responsable exige una tarea independiente para la corrección, su ID deberá decidirse entonces.

## 3. Alcance contractual que se conserva

La autoridad normativa completa sigue en §§15.1–15.8 incorporados por Adenda 63. Este resumen no los reemplaza ni añade variantes:

- GET singular, UUID D no vacío, sin cuerpo/query ni Idempotency-Key/If-Match funcionales; cookie segura, sesión/MFA completos. Sin CSRF sólo para este GET puro; no ETag/304 ni paginación de autorización.
- PER-TAREA-VER y autoridad vigente de cuenta/empleo/rol/permiso en LOR-001. Piso propia; Subcoordinación propia/Piso; Administración propia/Subcoordinación/Piso; Dirección LOR-001 completa incluso sin asignación y sin depender del responsable. No hereda permisos de carga ni autoridad histórica.
- Binario LIMPIO/CLEAN de VIGENTE o SUSTITUIDA, vinculado exactamente por file_object → evidence_version → evidence_item → work_obligation y linked_evidence_item_id recíproco. PENDIENTE/CONCLUIDA no bloquean por sí mismos. Sin inferir vínculos ni descargar evidencia estructurada.
- Alcance antes de metadata/S3; cotejo técnico tamaño/tipo/SHA-256/existencia; lectura final PostgreSQL READ ONLY/REPEATABLE READ, authorizedAt UTC único y revaluación completa; firma sólo tras comprobar la misma identidad/cadena/estado. Ninguna URL parcial si falla firma/lectura o ya expiró al responder.
- Firma GET del objeto CLEAN exacto. Cinco minutos por defecto, configuración mayor que cero y como máximo cinco minutos; expiresAt = authorizedAt + duración efectiva. No TTL cliente, renovación automática o recuperación por idempotencia.
- 200 application/json: sólo data.fileId, data.download.url, data.download.expiresAt y meta.correlationId. Sin 302, proxy ni binario en Web. Éxito y Problem Details llevan Cache-Control: private, no-store; Pragma: no-cache; Referrer-Policy: no-referrer; X-Content-Type-Options: nosniff.
- S3 entrega MIME real, Content-Disposition: attachment con evidence-{fileId:D}.jpg/.png/.pdf y Cache-Control: private, no-store cubiertos por firma, bytes originales. No nombre original, PII, inline, transformación o CORS CLEAN nuevo.
- GET puro: sin escritura PostgreSQL/S3, auditoría de descarga/lectura, idempotencia, outbox, jobs, historia ni row_version. Emitir token no demuestra lectura. Logs/diagnóstico sin URL, firma, contenido, nombre, hash, credenciales o conexiones.

| HTTP / code aprobado | Condición, según §15.6 |
|---|---|
| 400 SOLICITUD_DESCARGA_INVALIDA | UUID inválido/vacío, cuerpo/query/cabeceras funcionales inadmisibles |
| 401 AUTENTICACION_REQUERIDA | Sesión ausente/expirada o MFA incompleto |
| 403 ACCESO_DENEGADO | Actor inválido antes de resolver recurso |
| 404 ARCHIVO_NO_ENCONTRADO | Inexistente, ajeno, otra sucursal o sin vínculo funcional |
| 422 ARCHIVO_NO_LIMPIO | Cadena autorizada, archivo no LIMPIO/CLEAN |
| 500 CADENA_EVIDENCIA_INCONSISTENTE | Cadena contradictoria/ambigua/no recíproca tras reconocer alcance |
| 503 INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE | Storage/configuración/firma, objeto/metadata o garantías no disponibles |
| 500 ERROR_FUNCIONAL_REGISTRADO | Fallo inesperado, sin detalles nativos |

Precedencia: autenticación/actor → sintaxis → lookup con alcance → cadena/vínculo → limpieza → storage/metadata → reautorización final → firma/respuesta. Problem Details con code/correlationId, sin datos parciales. Condición HTTP de cache no evita reautorizar. No trasladar 409/412/428 de mutaciones.

**Límites ya aceptados:** URL al portador, sin revocación inmediata garantizada durante su vigencia, sin detener transferencias iniciadas ni retirar copias. Una pérdida posterior de autoridad impide nuevas emisiones, no garantiza invalidar tokens anteriores. La URI revela componentes técnicos al receptor autorizado; nunca se persiste ni registra. No se vuelve a pedir aprobar estos límites. Si el proveedor no cumple expiración/cabeceras/privacidad, falla cerrado y se presenta la decisión concreta.

## 4. Propuesta separada de verificación de Dirección

**Hecho estático:** EfEvidenceContributionService.ListAsync invoca CanViewAsync; éste retorna false sin asignación vigente y depende del responsable antes de RoleHierarchy.CanAccess. Adenda 15 §4, remitida por Adenda 18 §12 y Adenda 63, permite a Dirección consultar LOR-001 sin asignación. No es un ensayo fallido ni se atribuye a los ciclos previos. Tampoco prueba todos los efectos de runtime.

**Propuesta pendiente de autorización separada, previa a reutilizar esa autoridad:**

1. Preparar fixture PostgreSQL real sintético de obligación LOR-001 con historia/versión de evidencia persistida válida y sin asignación vigente. Si requiere antecedente de asignación para aportar, conservar la historia y representar la pérdida de asignación vigente sin inventar productor TAR-0026/CAT-006. No sembrar una asignación vigente para hacer pasar la consulta.
2. Invocar la consulta existente GET /api/v1/obligations/{id}/evidence como Dirección válida. Oráculo: lectura permitida con metadata/historia según el contrato existente, sin binario/URL ni nueva descarga. Contrastar detalle HU-023 como referencia de alcance, sin ampliar el análisis a otras consultas.
3. Negativos: otros tres roles sin asignación, Dirección de actor inválido, otra sucursal/inexistente; controles con asignación propia/inferior, par/superior y responsable no vigente. Exigir ocultación/códigos existentes y cero filtración según cada endpoint; no imponer los códigos de descarga a metadata.
4. Comparar conteos/huellas de tablas afectadas, auditoría, idempotencia/outbox/jobs y row_version antes/después; verificar cero consultas/firma/mutaciones S3 para esta consulta de metadata. Informar comando/filtro, SHA, resultado real y cleanup propio.
5. Presentar informe separado dentro de la trazabilidad de este plan: reproducción confirmada/no confirmada, causa y propuesta mínima de lectura. Si no se confirma, explicar evidencia antes de reutilizar el helper. Si se confirma, presentar archivos/casos de regresión y esperar orden expresa de corregir; aprobar verificación no autoriza corrección automática.

La corrección eventual se limita a lectura y alcance documentado, con guardas de sucursal y actor; no cambia RequireMutationAccessAsync, permisos de aporte/sustitución, historia, responsables o auditoría. No copiar el helper defectuoso, dar acceso global sin sucursal, atribuir sujeto futuro ni introducir la excepción Dirección en mutaciones por inferencia.

**Parada:** descarga no comienza dependiendo del helper mientras esta diferencia siga sin resolver/verificar. Si hay contradicción o fixture imposible bajo contratos vigentes, presentar la carencia; no alterar datos/reglas para superarla. Estimación heredada 2–4 h para verificación/informe, no medido ni autorizado; una corrección necesita alcance/estimación y orden propios.

## 5. Secuencia de implementación futura propuesta

Los pasos siguientes no se ejecutan en este chat.

1. Registrar aprobación literal de ID/definición/plan y su incorporación por adenda, conservando Adendas 62/63. Registrar por separado qué autorización recibió Dirección. Trabajar localmente sobre base aceptada preservando cambios ajenos; no publicación.
2. Completar §4 y la resolución que se autorice. Conservar resultado y regresiones de lectura como evidencia distinta de descarga. Sólo tras resolver la dependencia y recibir orden de implementar, continuar.
3. Añadir contrato interno de lectura de descarga en Evidence y su servicio de persistencia/composición; endpoint delgado. Consultas con alcance en PostgreSQL antes de materializar datos; ninguna regla de negocio en endpoint/dominio dependiente de EF/S3.
4. Añadir firma GET CLEAN a la abstracción/adaptador storage existente y adaptador no disponible; validar duración configurable. Reutilizar SDK S3 fijado, reloj y mecanismos técnicos actuales; sin dependencias nuevas, migración prevista o almacenamiento de tokens. Adaptar dobles afectados por la ampliación de interfaz.
5. Implementar cotejo S3 y lectura final READ ONLY/REPEATABLE READ con instantánea/reautorización, identidad exacta, fallos cerrados y pureza. Firmar localmente tras guardas y evitar devolver token si lectura final falla o caducó.
6. Registrar ruta con validación UUID explícita que permita devolver 400 contractual para ruta inválida; no copiar sin revisión el patrón {id:guid} que podría producir 404 de routing. Integrar autenticación/errores/envelope/cabeceras existentes sin alterar otras rutas.
7. Añadir pruebas de §6 y ajustar inventarios/dobles/regresiones directamente afectados. Validar build y filtros enfocados por capa; usar PostgreSQL/S3 reales sólo para casos del contrato. Registrar fallos y cleanup antes de declarar avance local.
8. Actualizar informe/estado/índice con resultados reales. Implementada localmente sólo cuando código, trazabilidad y comprobaciones enfocadas compatibles cubran el alcance; si falta prueba ejecutable por causa específica, registrarla sin PASS y explicar el límite de aceptación. Ningún cierre global automático de BR-API04.

## 6. Aceptación y validación futura proporcional

Se adopta íntegramente la matriz contractual §15.7. Las filas siguientes agrupan su ejecución propuesta; no son nuevos IDs CP, pruebas ejecutadas ni sustitutos de CA/CP existentes.

| Grupo | Verificación futura |
|---|---|
| API/contrato | Ruta/método; UUID/cuerpo/query/headers negativos; autenticación/MFA; DTO y headers exactos en éxito/error; cero 302/304/ETag/URL parcial/campos extra |
| Autoridad PostgreSQL/API | Cuatro roles, propia/inferior/par/superior, otra sucursal, asignación/actor/responsable vigentes, pérdida de autoridad, actor histórico; Dirección conforme al resultado separado |
| Historia/cadena/limpieza | Ambas versiones y estados de ejecución; vínculo exacto/recíproco y política capturada; limpio sin vínculo 404, no limpio 422, cadena incoherente 500; ajenos sin metadata/firma S3 |
| Emisión y concurrencia | Barreras reales, sin sleeps/retries: cambio de autoridad/cadena antes de instantánea final deniega; authorizedAt/TTL internos sanitizados; reemplazo conserva archivo anterior y cada descarga resuelve su versión exacta |
| S3 real | Bytes/SHA-256, MIME/attachment/nombre seguro/no-store; firma alterada, objeto distinto, PUT, anónimo/listado denegados; objeto/metadata ausentes/incongruentes o signer no conforme 503 |
| Expiración real | TTL corto válido del fixture con reloj/condición real del proveedor; acceso previo y rechazo posterior; nueva emisión denegada tras baja/rol/asignación/logout; límite residual del token previo documentado aparte |
| No-efecto/minimización | Huellas/conteos PostgreSQL antes/después de éxito/denegación/fallo, sin escrituras/auditoría/outbox/jobs/row_version; S3 sólo metadata/firma/lectura esperadas, cero promoción/borrado/CORS; diagnóstico sanitizado |
| Arquitectura/regresión | Sin DbContext/SDK en dominio/endpoint; inventarios y dobles afectados; rutas/listado/status/carga/reemplazo existentes conservan contrato y restricciones |

Build propuesto: `dotnet build --no-restore --configuration Release`, usando SDK exacto 10.0.400 conocido en `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe` si está disponible al autorizar ejecución. Pruebas: `dotnet test --no-build --configuration Release --filter <clases afectadas>`, por proyecto/capa y filtros concretados al crear las pruebas. Restore locked sólo por necesidad real, una vez; sin cambiar global.json para acomodar PATH. Final: `git diff --check`.

No suite completa, formato global, ciclos integrales, Playwright, preview, UI, escáneres generales o gates remotos por endpoint. PostgreSQL/S3 enfocados constituyen aceptación del contrato, no la repetición del hito integral. Reutilizar infraestructura privada/corpus sintético existentes, no otro antivirus ni evidencia real. No prometer PASS de proveedor mediante sólo mocks o reloj de aplicación. Incompatibilidad real AMD64: VALIDACION_DIFERIDA_POR_ARQUITECTURA; indisponibilidad/costo con causa propia, no bloqueo general ni aprobación ficticia.

## 7. Archivos previstos y costo

**En esta preparación:** este Markdown, docs/traceability/IMPLEMENTATION_STATUS.md y docs/INDICE_IDS.md. Sin cambio a código, pruebas, scripts, diseño, documentos congelados o Fuentes/.

**Propuesta para ejecución posterior:**

| Área | Archivos previstos / motivo |
|---|---|
| Contratos Evidence | src/Modules/Evidence/Contracts/EvidenceInfrastructureContracts.cs y nuevo EvidenceDownloads.cs: firma GET/contrato interno cerrado, sin DTO extra externo |
| Persistencia | Nuevo src/Sgol.Web/Infrastructure/Persistence/Evidence/EfEvidenceDownloadService.cs; composición DI existente directamente afectada |
| Storage/configuración | src/Sgol.Web/Infrastructure/Evidence/S3PrivateObjectStorage.cs, UnavailableEvidenceAdapters.cs, EvidenceInfrastructureOptions.cs y EvidenceInfrastructureServiceCollectionExtensions.cs |
| API | src/Sgol.Web/Interface/Endpoints/EvidenceApiEndpoints.cs: ruta, sintaxis, respuesta y errores |
| Pruebas | EvidenceApiEndpointTests.cs, EvidenceInfrastructureTests.cs, EvidenceExternalInfrastructureTests.cs; nuevas pruebas enfocadas de persistencia/descarga/arquitectura y dobles que implementen IPrivateObjectStorage |
| Dirección separada | EfEvidenceContributionService.cs sólo si se autoriza corrección; pruebas de metadata en persistencia/API y ObligationQueryPersistenceTests.cs como contraste de alcance |
| Trazabilidad | Adenda nueva para definición aprobada; este plan/informe, IMPLEMENTATION_STATUS e INDICE_IDS, sin editar Adendas 62/63 o congelados |

Nombres nuevos son propuestas de archivos, no contratos públicos ni IDs estables adicionales. No se prevé migración; cualquier necesidad de esquema/dependencia fuera de esta propuesta se presenta antes de ampliar alcance.

**Estimación, no presupuesto ni resultado:** 12–20 h de implementación y preparación de pruebas, más 1–2 h de ejecución/provisión enfocada API/PostgreSQL/S3 de §15.7; Dirección separada 2–4 h, sin incluir corrección todavía no dimensionada. Depende de resolución de autoridad y conformidad del proveedor; ajustar sólo con evidencia, sin extender el hito automáticamente.

Recursos futuros desechables e inventariados por ensayo; cleanup en finally y ausencia propia comprobada. No prune, borrado de evidencia funcional o recursos ajenos. Rutas absolutas verificadas para eliminación recursiva. No registrar URLs/secretos en reportes, comandos o aserciones; oráculos internos sanitizados. Esta preparación no creó contenedores, volúmenes, certificados ni tokens; no requiere cleanup de ejecución.

## 8. Exclusiones y diferidos conservados

BR-API04 permanece **ABIERTA globalmente**; entregar descarga no cierra preview ni resuelve la brecha por implicación. Preview, transformación y UI de lectura siguen diferidas. Sin proxy, CDN, URL permanente, nuevo permiso, productores, indicadores, fórmulas, cloud/cuentas, reparación CORS, observabilidad productiva o despliegue. D4 mantiene TAR-0026 sólo configuración/políticas/negativos y excluye fuente/cadena CAT-006. D5/Adenda 61 conserva su alcance limitado de traza, sin sujetos futuros ni productores.

Se conservan las causas de §5 del documento antecedente: zoom nativo y lector reales no ensayados; dispositivos físicos no ensayados; PUT/S3 Windows WebKit previo sin HTTP con causa no confirmada; aislamiento productivo completo SeaweedFS no acreditado; carrera sustitución de evidencia contra emisión no reensayada; navegador con 100000 diferencias diferido por costo; isolated network histórico opcional SKIPPED. Este plan API/S3 no ejecuta ni acredita esos casos con otra plataforma. La carrera de reemplazo/descarga no sustituye la carrera contra emisión de validación. Exclusiones consumidoras 57..60 y condiciones TLS/Development/CI de Adendas 17/18 permanecen.

## 9. Revisión documental y decisión solicitada

Preflight única comprobación inicial de entorno: exit 0, treeClean=true, fuentesClean=true, fuentesRebaselinePending=false. SDK de PATH 10.0.401 no satisface global.json 10.0.400; registrado sin diagnosticar ni modificar entorno. Git status inicial limpio, rama codex/propuesta-post-tech-front-005. Revisión de fuentes/IDs/precedencia y ascendencia realizada; validación de entrega limitada a revisión documental, enlaces locales, alcance del diff y whitespace. No build/restore/tests/ensayos o resultados nuevos de aplicación: no autorizados en esta preparación.

Resultado documental: referencias operativas comprobadas existentes y alcance limitado a los tres Markdown de §7. `git diff --check` exit 0 para archivos seguidos; archivo nuevo comprobado con `git diff --no-index --check -- NUL docs/traceability/BR_API04_PLAN_DE_IMPLEMENTACION.md`, sin diagnósticos de whitespace (exit 1 significa diferencia en modo no-index). Avisos LF→CRLF corresponden a la configuración existente, sin alterarla. Las verificaciones de aplicación son futuras y no autorizadas aún; los diferidos heredados conservan individualmente sus causas en §8.

**Decisiones pendientes, independientes del contrato ya aprobado:** aceptar o cambiar el ID propuesto TECH-EVID-003 y su definición; aprobar e incorporar este plan; decidir autorización separada para la verificación Dirección de §4. Una aprobación del plan sin orden expresa no inicia implementación ni ensayos. Una orden de verificar Dirección no autoriza corregir automáticamente. Publicación/merge/despliegue no forman parte de ninguna de estas decisiones.

Punto de parada actual: esperar aprobación del plan/identificador y orden expresa aplicable. No se inició implementación ni verificación Dirección. No se asigna tarea canónica por esta propuesta.

Nombre exacto sugerido del siguiente chat, sólo si el responsable decide abrirlo: **SGOL — BR-API04 — Decisión del plan de descarga y verificación Dirección**.

Mensaje listo para copiar:

> Aplica AGENTS.md e INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md. Revisa docs/traceability/BR_API04_PLAN_DE_IMPLEMENTACION.md: TECH-EVID-003 es un identificador propuesto, no aprobado. Conserva Adendas 62/63 y el contrato aprobado §§15.1–15.8, BR-API04 ABIERTA y todos sus límites/diferidos. Registra mi decisión sobre ID/definición/plan y espera mi orden expresa para implementar o ensayar. La verificación Dirección de §4 requiere autorización separada y no autoriza corrección automática. Sin publicación, merge, despliegue, preview/UI ni otro hito automático.

## 10. Aprobación posterior y estado vigente

2026-10-03. Aprobación literal: «Lo apruebo», en respuesta a la solicitud de aprobación del identificador, definición y plan para incorporación documental. **TECH-EVID-003 queda asignado/aprobado y el plan incorporado localmente por F07_ADENDA_64_DEFINICION_Y_PLAN_DE_DESCARGA_TECH_EVID_003.md.** Las expresiones de propuesta/pending previas se conservan como antecedente revisado, superadas exclusivamente en materia de ID/definición/plan e incorporación. No cambian contratos, estimaciones ni la naturaleza futura de aceptación/pruebas.

Sigue pendiente la orden expresa de implementar y la autorización separada de verificación Dirección. Aprobar la propuesta de §4 no ordena ejecutarla ni corregir automáticamente un resultado. No hay código, ensayo, build/restore/tests de aplicación ni PASS nuevo; no Implementada localmente, Publicada, Integrada o Terminada esta tarea. BR-API04 ABIERTA y todos los límites/diferidos de §8 se conservan.

Preflight única comprobación inicial de este turno: exit 0, HEAD 58b12b6d201497671f0da39b30c42105a152f203, rama codex/propuesta-post-tech-front-005; treeClean=false corresponde a los tres Markdown de preparación del turno anterior, conservados. Fuentes limpia/sin rebaseline; incompatibilidad SDK PATH 10.0.401 frente a 10.0.400 registrada sin diagnóstico adicional. Adenda 64 libre comprobada en raíz. Cambios exclusivamente documentales: adenda nueva, aprobación en este plan, estado e índice. Validación proporcional: referencias, clasificación de autorizaciones y whitespace; sin recursos de ejecución que limpiar.

Validación de incorporación: `git diff --check` exit 0; adenda y plan nuevos comprobados individualmente mediante `git diff --no-index --check -- NUL <archivo>`, sin diagnósticos de whitespace (exit 1 por diferencia en modo no-index). Rangos de la Adenda 64 en INDICE_IDS contrastados con sus líneas reales. Avisos LF→CRLF de configuración existente, sin modificarla. Sin verificaciones de aplicación: no autorizadas aún, y sin alterar las causas de los diferidos heredados.

Nombre exacto sugerido del siguiente chat, sólo si el responsable decide abrirlo: **SGOL — TECH-EVID-003 — Autorización de verificación Dirección y ejecución**.

Mensaje listo para copiar, que no constituye orden de ejecutar:

> Aplica AGENTS.md e INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md. Consulta IMPLEMENTATION_STATUS, INDICE_IDS y Adendas 62/63/64. TECH-EVID-003 y docs/traceability/BR_API04_PLAN_DE_IMPLEMENTACION.md están aprobados e incorporados localmente. Espera mi autorización separada antes de verificar Dirección y mi orden expresa antes de implementar. La verificación no autoriza corrección automática; presenta resultado/propuesta y resuelve esa dependencia antes de reutilizar CanViewAsync. Conserva contrato, BR-API04 ABIERTA y todos los diferidos. Sin publicación, merge, despliegue, preview/UI ni otro hito automático.

## 11. Orden de implementación y verificación Dirección — diferencia confirmada

2026-10-03. Orden literal: «Autorizo implementar y tambien autorizo verificar direccion». Autoriza implementar TECH-EVID-003 y ejecutar la verificación separada de §4, sin publicación/merge/despliegue/preview/UI. Los pendientes anteriores de esas dos órdenes quedan superados. La corrección eventual de la diferencia mantiene su autorización propia conforme a §4.5 y Adenda 64; se presenta aquí una propuesta concreta antes de modificar producción.

Preflight única comprobación inicial: exit 0; HEAD/branch conservados, cambios propios de los cuatro Markdown anteriores preservados, Fuentes limpia/sin rebaseline. SDK PATH incompatible sin diagnóstico adicional; para build/test se utilizó el SDK aislado 10.0.400 ya conocido. No restore, dependencias o migraciones.

**Verificación ejecutada:** `tests/Sgol.IntegrationTests/ObligationQueryPersistenceTests.TechEvid003.cs`, método `TechEvid003DirectionMustReadPersistedEvidenceAfterCurrentAssignmentEndsWithoutEffects`. Reutiliza PostgreSQL real desechable y fixture sintético TAR-0008 ya existente: persiste conclusión/evidencia limpia vinculada mientras hay asignación vigente, luego conserva la asignación como SUSTITUIDA, sin crear sucesora vigente. No TAR-0026/CAT-006 ni siembra para ocultar el defecto.

Invoca el handler público de GET metadata `EvidenceApiEndpoints.ListAsync` con HttpContext GET/actor autenticado y servicio EF real; contrasta `EfObligationQueryReader.GetAsync`. No es una llamada de red a Kestrel ni acredita middleware de cookie/MFA, serialización HTTP hospedada o UI. Las restricciones de sucursal del esquema impiden sembrar una segunda sucursal válida; no se afirma haber ensayado ese caso. Este alcance basta para reproducir la discrepancia del lector; los casos adicionales del §4 siguen pendientes antes de aceptar una corrección completa.

Resultados alcanzados antes de la aserción contractual final:

- Con asignación vigente: Piso propio, Subcoordinación, Administración y Dirección obtienen 200; metadata sin ObjectKey/download/bucket. Par Administración y Piso sobre superior obtienen 404.
- Tras perder asignación vigente: los otros tres roles obtienen 404; actor inexistente 403; recurso inexistente 404.
- Dirección obtiene detalle HU-023 200 sobre la misma obligación, pero metadata **404**, frente al oráculo aprobado **200**. Diferencia confirmada en ejecución, no error de entorno ni PASS.
- Huellas completas de tablas funcionales, historial, auditoría, idempotencia/outbox/jobs/row_version y file_object idénticas antes/después de consultas; ChangeTracker vacío; storage estricto registra cero llamadas. La preparación del fixture queda fuera de los intervalos comparados.

Comandos locales, con `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe`:

```text
build tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-restore --configuration Release
test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter FullyQualifiedName~TechEvid003
```

Build final PASS, 0 errores/advertencias; dos fallos previos de preparación de prueba corregidos (namespace real Presentation.Endpoints y retorno concreto requerido por CA1859), sin cambios productivos. Test final FAIL 1/1, cero omitidos, exit 1; mensaje «Direction metadata expected 200, actual 404; HU-023 detail 200; assigned four-role controls, unassigned non-Direction negatives, invalid actor, missing resource, no-effect and zero S3 verified». Se conserva el oráculo 200 y la prueba roja; no se cambia para aceptar 404. Lifecycle del fixture existente dispone el contenedor en DisposeAsync; no se afirma comprobación externa de ausencia ni cleanup de recursos ajenos.

**Corrección mínima propuesta, pendiente de orden:** en `EfEvidenceContributionService.CanViewAsync`, conservar validación de actor; exigir existencia de obligación en LOR-001 antes de conceder lectura; para Dirección vigente permitir antes de consultar asignación/responsable; para no Dirección conservar alcance jerárquico/propiedad y las guardas de vigencia documentadas, sin ampliar permisos. No tocar RequireMutationAccessAsync o comandos de aporte/sustitución. Ampliar regresión para Dirección con responsable sin vigencia, sesión/actor inválido y cadena existente; ajustar sólo guardas de lectura que requiera el contrato. Estimación adicional 2–4 h de corrección/regresión enfocada, no resultado ni autorización.

**Parada de esta dependencia:** autorización de implementación de descarga vigente, pero no reutilizar autoridad aún defectuosa. Esperar orden de corrección, luego ejecutar regresión y continuar descarga sin pedir otra autorización de implementación. BR-API04 ABIERTA, no Implementada localmente esta tarea, todos los diferidos conservados. Sin código productivo de descarga/corrección todavía.

## 12. Corrección autorizada e implementación local de TECH-EVID-003

2026-10-03. Orden literal posterior: **«Lo autorizo»**, referida a la corrección mínima de Dirección presentada en §11. Autoriza corregir esa lectura y continuar la implementación de descarga ya ordenada, sin otra aprobación del contrato. La autorización separada de verificar Dirección y su resultado rojo permanecen documentados; la corrección no se atribuye a la primera orden de verificar. No autoriza publicación, merge, despliegue, preview/UI ni una tarea posterior.

### 12.1 Resultado y criterios cubiertos

**TECH-EVID-003 Implementada localmente.** GET `/api/v1/files/{id}/download` entrega el envelope cerrado aprobado y autorización temporal del binario original CLEAN; duración predeterminada 300 segundos, configuración `Evidence:Download:ValiditySeconds` entera entre 1 y 300. No migraciones, paquetes, permisos, productores ni cambios de contratos congelados. Configuración inválida, metadata/objeto/firma no conformes fallan cerrado. Composición en Web, contratos internos en Evidence; no reglas de descarga en el handler ni dependencia de proveedor en el contrato.

Actor vigente y alcance PostgreSQL se resuelven antes de materializar archivo/cadena o consultar S3. Dirección cubre LOR-001 sin asignación; los otros roles conservan responsabilidad actual/jerarquía, denegación y ocultación. Se verifica vínculo recíproco y exacto, política/requisito capturados, VIGENTE/SUSTITUIDA y LIMPIO/CLEAN. Estados PENDIENTE y CONCLUIDA permiten lectura; evidencia estructurada no recibe binario inferido. Se cotejan existencia, tamaño, SHA-256 de metadata, tipo almacenado y Content-Type nativo por el mecanismo técnico existente, sin leer/rehash/reescanear el objeto por cada GET.

Tras metadata S3 se abre PostgreSQL `REPEATABLE READ`, `SET TRANSACTION READ ONLY`; se captura una sola lectura de reloj para la autorización final. `authorizedAt` usa segundos UTC completos, precisión representable por la firma S3; además se comprueba autoridad/cadena en el instante capturado con fracciones para impedir extender autoridad que venció dentro de ese segundo. Guardas en ambos instantes deben coincidir; falla cerrado si no puede asegurarse esa correspondencia. `expiresAt = authorizedAt + duración configurada`, sin mover el plazo aprobado, y se comprueba vigencia antes y después del commit de lectura. Firma local sólo tras guardas finales; no retorno parcial si falla o caduca.

El SDK fijado puede redondear su duración a un segundo distinto; el adaptador comprueba `X-Amz-Date + X-Amz-Expires`, realiza como máximo un ajuste local de precisión y exige coincidencia exacta con el deadline original y duración firmada entre 1 y 300. Ningún token intermedio sale del método. No es retry de una carrera ni renovación de TTL. El cálculo del SDK se contrastó con [fuente primaria de AWS, GetSecondsUntilExpiration](https://raw.githubusercontent.com/aws/aws-sdk-net/master/sdk/src/Services/S3/Custom/AmazonS3Client.Extensions.cs); la aceptación se acredita con el paquete fijado y SeaweedFS real, no por esa referencia a master.

API conserva códigos/precedencia de §3, `application/problem+json`, correlationId, cuatro cabeceras de privacidad en éxito/error y UTC Z; acepta If-None-Match sin 304, no ETag/Location, sin body/query/cabeceras funcionales extra. Cookies/sesión/MFA se verifican también en hospedaje real de aplicación. S3 firma GET, objeto CLEAN exacto, MIME, attachment/nombre técnico seguro y private/no-store. Comparaciones de contenido y metadata de ensayo usan diagnósticos sanitizados; tipos de autorización no imprimen URI mediante ToString.

GET no inserta auditoría ni escribe historia, versiones, jobs, outbox, idempotencia o row_version; tampoco promueve/borra/escribe S3 ni modifica CORS CLEAN. Huellas de filas completas se comparan como hash para evitar imprimir credenciales/contenidos en fallos. Los intervalos de preparación y cambios externos del fixture quedan fuera de la comparación de no-efecto de la lectura.

### 12.2 Dirección — regresión separada de descarga

Corrección exclusiva en `EfEvidenceContributionService.CanViewAsync`: actor válido, obligación existente LOR-001, Dirección antes de exigir asignación/responsable. `RequireMutationAccessAsync` y comandos de aporte/sustitución permanecen sin cambios. La prueba roja de §11 mantiene su oráculo 200 y ahora pasa. Controles: cuatro roles con asignación, par/superior ocultos, responsable inactivo (Dirección 200, Administración 404), pérdida de asignación (Dirección metadata y HU-023 200, otros roles 404), actor/recurso inexistentes y no-efecto con cero llamadas storage. Prueba PostgreSQL/handler real; no se presenta como llamada HTTP hospedada de metadata ni evidencia UI.

### 12.3 Archivos cambiados

| Área | Archivos |
|---|---|
| Contratos internos | `src/Modules/Evidence/Contracts/EvidenceDownloads.cs` (nuevo); `EvidenceInfrastructureContracts.cs` (firma GET CLEAN) |
| Lectura persistida y DI | `src/Sgol.Web/Infrastructure/Persistence/Evidence/EfEvidenceDownloadService.cs` (nuevo), `EfEvidenceContributionService.cs` (corrección separada), `Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs` |
| S3/configuración | `src/Sgol.Web/Infrastructure/Evidence/S3PrivateObjectStorage.cs`, `EvidenceInfrastructureOptions.cs`, `EvidenceInfrastructureServiceCollectionExtensions.cs`, `UnavailableEvidenceAdapters.cs` |
| Endpoint | `src/Sgol.Web/Interface/Endpoints/EvidenceApiEndpoints.cs` |
| Unitarias | `tests/Sgol.UnitTests/EvidenceDownloadTests.cs` (nuevo), `EvidenceInfrastructureTests.cs` (doble actualizado) |
| PostgreSQL/hosting | Nuevos `tests/Sgol.IntegrationTests/ObligationQueryPersistenceTests.TechEvid003.cs`, `ObligationQueryPersistenceTests.Downloads.cs`, `HostedAuthenticationPostgreSqlTests.Downloads.cs`; `HostedAuthenticationPostgreSqlTests.cs` pasa a partial |
| S3 real | Nuevo `tests/Sgol.EvidenceIntegrationTests/EvidenceExternalInfrastructureTests.Downloads.cs`; `EvidenceExternalInfrastructureTests.cs` pasa a partial |
| Arquitectura/doble demo | Nuevo `tests/Sgol.ArchitectureTests/EvidenceDownloadArchitectureTests.cs`; `EvidenceInfrastructureArchitectureTests.cs` actualiza inventario GET; `tests/Sgol.Cv04Demo/Cv04Seed.cs` adapta interfaz |
| Documentación | Adenda 64 de aprobación previa, este plan/informe, `docs/traceability/IMPLEMENTATION_STATUS.md` y `docs/INDICE_IDS.md` |

### 12.4 Comprobaciones locales y límites de evidencia

Preflight de este turno exit 0, única comprobación inicial de entorno. Base aceptada HEAD 58b12b6d201497671f0da39b30c42105a152f203; cambios propios previos preservados; Fuentes limpia/sin rebaseline. SDK PATH 10.0.401 no satisface global.json; se usa el SDK aislado 10.0.400 conocido, sin diagnóstico adicional ni cambiar global.json. Sin restore. Todos los comandos siguientes usan `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe`, Release.

Builds con `build <proyecto> --no-restore --configuration Release`: Sgol.UnitTests, Sgol.IntegrationTests, Sgol.EvidenceIntegrationTests, Sgol.ArchitectureTests y Sgol.Cv04Demo, en sus carpetas `tests/` y archivos `.csproj` homónimos. Resultado final: PASS, cero errores/advertencias; incluyen compilación del Web afectado. Sgol.Cv04Demo sólo se compila para comprobar el doble adaptado; no se ejecuta demo.

| Comando test (`--no-build --configuration Release --filter`) / proyecto | Resultado final y alcance |
|---|---|
| `FullyQualifiedName~EvidenceDownloadTests\|FullyQualifiedName~EvidenceApiEndpointTests\|FullyQualifiedName~EvidenceInfrastructureTests` — Sgol.UnitTests | PASS 66/66, cero omitidos: sintaxis, envelope, errores, no-cache, SDK y regresión API/infraestructura afectada |
| `FullyQualifiedName~TechEvid003` — Sgol.IntegrationTests | PASS 14/14, cero omitidos: Dirección separada, descarga jerárquica/versiones/ocultación, cadena y fallos, cambios externos de cuenta/rol/empleo/asignación antes del snapshot, no-efecto y cookie/MFA/logout hospedados |
| `FullyQualifiedName~TechEvid003DownloadPendingBinary\|FullyQualifiedName~TechEvid003DownloadKeepsExactOldFile` — Sgol.IntegrationTests | Reemplazo PASS 1/1; pendiente requirió corregir fixture y retest específico `FullyQualifiedName~TechEvid003DownloadPendingBinary` PASS 1/1. Dos casos adicionales distintos; 16 casos PostgreSQL/hosting en total, no un ciclo único de 16 |
| `FullyQualifiedName~TechEvid003` — Sgol.EvidenceIntegrationTests | PASS 1/1, cero omitidos: objeto ausente y metadata corrupta detectados por S3 real, PNG/JPEG/PDF originales, MIME/nombre/attachment/no-store, tokens a 300 s, adulteración de firma/header/objeto, PUT, anónimo/listado denegados, metadata conservada; token corto válido antes y rechazado tras frontera real de expiración |
| `FullyQualifiedName~EvidenceDownloadArchitectureTests\|FullyQualifiedName~EvidenceInfrastructureArchitectureTests` — Sgol.ArchitectureTests | PASS 5/5, cero omitidos: contrato sin proveedor, readonly, operaciones prohibidas y endpoint/inventario afectados |

Son **88 casos distintos con resultado final PASS**, obtenidos por filtros y ejecuciones enfocadas separadas; no suite completa ni evidencia de un ciclo integral. Coordinación PostgreSQL mediante callback esperado y commit de un contexto independiente antes de iniciar snapshot, sin sleeps/retries. Reemplazo concurrente conserva versión SUSTITUIDA y fileId anterior exactos; no sustituye la carrera histórica contra emisión de validación. Expiración S3 usa una frontera de reloj real mediante timer; no coordina carrera de base de datos ni simula rechazo del proveedor.

Incidencias corregidas, no éxitos: fixture de reemplazo inicialmente incumplía restricción diferida (transacción explícita agregada); inyección de corrupción inicialmente detenida por guardas (sólo en DB desechable se deshabilita/restaura trigger y, para no limpio vinculado, se retira constraint de fixture); diferencias de un segundo en firma corregidas con deadline verificado; errores de compilación en pruebas por namespace, import EmploymentStatus, referencia no disponible en Architecture y SQL de allowlist corregidos. Un test de arquitectura lanzado tras build fallido usó assembly anterior y falló con inventario obsoleto; no constituye validación de la cabeza actual. Build y filtro correctos posteriores pasan. Fixture pendiente buscó fotografía inexistente para TAR-0008; se eligió su requisito binario real sin inventar productor ni modificar oráculo. Fallos contractuales se conservaron hasta corregir causa; no se relajaron esperados ni guardas productivas.

Lifecycle existente DisposeAsync dispone PostgreSQL, hospedaje, clientes y SeaweedFS/ClamAV y elimina su temporal generado. No prune, borrado funcional, recursos ajenos ni certificados nuevos. No se afirma inspección externa de ausencia de contenedores/volúmenes/temporales; el fixture no emite inventario persistido para esa comprobación y ese control adicional queda pendiente para un hito de infraestructura, no como PASS ficticio. Tampoco se acredita aislamiento productivo por usar el proveedor local. Las guardas de sucursal están en SQL; esquema singleton LOR-001 impide crear otra sucursal válida, por lo que ese negativo no se presenta como ensayo con fixture de segunda sucursal.

### 12.5 Trazabilidad, diferidos y cierre local

Estado/índice actualizados con ID aprobado, las tres órdenes y resultados reales. Adendas 62/63 y §§1–9 aprobados se conservan; se agrega esta ejecución sin reescribir historia. `git diff --check` exit 0; archivos nuevos incluidos en el control staged final. Punto recuperable local en rama `codex/tech-evid-003`; el hash del commit que contiene el informe se entrega al responsable, sin autorreferencia dentro del propio commit. BR-API04 **ABIERTA globalmente**, TECH-EVID-003 **Implementada localmente**, nunca Publicada/Integrada/Terminada por inferencia. Sin publicación, merge, despliegue, preview/UI, suite completa, formato global o checks remotos.

**Validación diferida conservada con causa exacta:** §8 mantiene zoom nativo/lector y dispositivos físicos no ensayados; PUT/S3 Windows WebKit histórico sin HTTP y causa no confirmada; aislamiento productivo SeaweedFS no acreditado; carrera sustitución contra emisión de validación no reensayada; navegador 100000 diferencias por costo; isolated network histórico opcional SKIPPED. Preview/transformación/UI siguen fuera de alcance. Ningún PASS nuevo de descarga acredita esos casos. No se detectó incompatibilidad AMD64 en las pruebas enfocadas realizadas y no se les asigna VALIDACION_DIFERIDA_POR_ARQUITECTURA.
