# FRONT-018 — Plan único de versiones, revisión, sustitución y conclusión

2026-10-01. **APROBADO ÍNTEGRAMENTE** mediante «Si apruebo integramente el plan»; incorporado por Adenda 57. El texto siguiente conserva la propuesta revisada como contrato aprobado; sus menciones de decisiones pendientes describen el momento previo a aprobación. Este documento reúne el plan y las decisiones consumidoras pendientes. No acredita implementación, publicación ni integración de FRONT-018. Una aprobación parcial habilita únicamente lo expresamente aprobado; no resuelve por inferencia las demás decisiones.

## 1. Resumen de alcance y base

1. Implementar únicamente la fila 96 de Adenda 45: UI-E06/E07/E08, HU-022/025/026 y CA/CP-022/025/026, con los CAT aplicables a las ocho TAR de LOR-001.
2. Mantener el detalle existente `/mi-trabajo/tareas/{obligationId:guid}`: versiones e historia de evidencia, revisión y faltantes, sustitución autorizada y conclusión por responsable vigente. Sin FRONT-019/020, reapertura, dispensa, decisión de validación, borrado, preview ni descarga.
3. Aceptar FRONT-017 integrada por PR #86, cabeza `1913347a3c6e27649b2fb3af881ec4c06c55c1f2`, merge `5dfd5557b948d742fa61790a346466acb29ba0fb`, pipeline `36905844511/attempts/2` correcto. No repetir gates aceptados. Diseño de PR #85 y Adendas 55/56 conservados.
4. Conservar `96dfe4dbd4aa26207b68fd3a723dd0b49cebd218` en `codex/front-018`, junto a este hito; sin PR documental separado. Árbol inicial limpio y Fuentes limpio según preflight/Git.
5. Resolver por aprobación de §§3–7 las carencias consumidoras: BR-API04 sin descarga, composición específica BR-D04/M08, proyección de permisos existentes y autoridad de recurso; decidir expresamente la excepción de snapshot de revisión.
6. Archivos previstos: detalle Razor y parciales, presentación/intenciones protegidas, proyección de sesión y detalle, pruebas afectadas y documentación. Sin dependencia nueva, migración prevista ni modificación de contratos de dominio sustantivos.
7. Validar build y pruebas enfocadas de presentación/API/arquitectura; PostgreSQL real para atomicidad, carreras y no-efecto; carga segura y navegador del recorrido cuando sea proporcional. Capturas de aplicación funcionando con datos sintéticos.
8. Esperar aprobación antes de incorporar decisiones o editar código. Implementación y commits locales después; push/PR/checks sólo con autorización de publicación. Merge/despliegue requieren autorización independiente.

## 2. Fuentes y dependencias verificadas

Se leyeron primero IMPLEMENTATION_STATUS.md y FRONT_018_SIGUIENTE_TAREA_Y_LECCIONES.md, el prompt completo de transición y INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md. Se utilizó INDICE_IDS.md para FRONT-018, CA/CP y HU/RN; se consultaron la fila 96 de Adenda 45, F05 criterios líneas 52/55/56 y 71–78, especificación líneas 96/101/137/140/141 y la matriz de permisos correspondiente. No se reanalizaron íntegramente F00–F07.

Contratos necesarios: Adenda 18 §§5–13 y seguridad/errores, Adenda 20 §§5–7 y §§10–15 (precedencia estructurada), Adenda 19 §§3–14 y errores, Adenda 21 §§6–13; contratos C# EvidenceContributions, EvidenceReviews, ObligationConclusions y ObligationQueries; endpoints de evidencia, consulta y conclusión; consumidores FRONT-016/017. F06 API: reglas comunes, ejecución/evidencia y concurrencia; ADR-003/005/006/007/009/012/014/015 y estrategia de pruebas por riesgo. Las precisiones aprobadas de las adendas prevalecen sobre ejemplos generales históricos.

Diseño operativo consultado: ESTILO_VISUAL_V2.md, referencia-renovada.md, tokens.md, componentes.md, estados-y-mensajes.md, estados-de-dominio.md, accesibilidad.md y navegacion.md, además de Adenda 55. Adenda 56 mantiene las decisiones de FRONT-017. No se abrieron identidad congelada, originales, Excel ni ZIP.

La tabla **Tareas insertadas por adenda** registra TECH-EVID-001/002 Terminadas y TECH-FRONT-001..004 Integradas, con evidencia aceptada. HU-025/026/022 están disponibles en el código de la base aceptada; TECH-FRONT-005 corresponde después de FRONT-001..020. No se encontró una dependencia real de código pendiente anterior. La espera de esta tarea es por decisiones consumidoras y aprobación de este plan, no por publicación histórica.

Preflight ejecutado como única comprobación inicial de entorno: `head=96dfe4d…`, `branch=codex/front-018`, `treeClean=true`, `fuentesClean=true`, `fuentesRebaselinePending=false`, sesión gh autenticada. Informa que el dotnet del PATH no resuelve SDK 10.0.400 y enumera 10.0.401. No se amplió el diagnóstico. Para validar implementación se usará el ejecutable aislado ya documentado `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe`; su disponibilidad no se acredita con este plan. global.json permanece intacto.

## 3. Brechas y decisiones expresas solicitadas

| Brecha o diferencia | Hecho documentado / código observado | Propuesta concreta para aprobación |
|---|---|---|
| BR-API04 | Adenda 18 §3.2 excluye descarga y `GET /api/v1/files/{id}/download`. EvidenceApiEndpoints no la implementa; ADR-007 fija seguridad, pero no constituye contrato de descarga. La fila FRONT-018 la condiciona a contrato existente. | Resolver **sólo para esta consumidora mediante exclusión explícita**: consulta de metadata/historia y registros estructurados autorizados, sin abrir/descargar binarios ni preview, enlace o URL de lectura. BR-API04 no se declara cerrada globalmente. Una descarga futura necesita contrato independiente aprobado. |
| BR-D04 | Existe dialog nativo motivado, Cancelar inicial, Escape, retorno y variante contextual sin motivo en otros consumidores. No existe composición específica UI-E06/E07/E08 aprobada. | Aprobar §5: formulario de sustitución fuera del diálogo; confirmación corta con motivo opcional en pendiente y obligatorio después de conclusión; conclusión sin motivo/cuerpo. Incorporar extensión consumidora en docs/design tras aprobar. |
| BR-M08 | Los textos de carga/escaneo y errores comunes existen; faltan catálogo específico de versiones, revisión, sustitución y conclusión y sus vacíos. | Aprobar literalmente §6 y semántica de §7, sin tomar textos o funciones de maquetas. |
| Permisos de sesión | F05 matriz líneas 56–58 permite ejecutar propia a cuatro roles y sustituir después a Dirección/Administración/Subcoordinación sobre inferiores. RolePermissionProjection.ForRole en `Roles.cs` aún no proyecta esos dos permisos. | Añadir sólo `PER-TAREA-EJECUTAR` a los cuatro roles y `PER-EVIDENCIA-SUSTITUIR` a los tres superiores. Es proyección de permisos existentes, no permiso nuevo ni autoridad cliente. |
| Autoridad por recurso | ObligationDetail incluye responsable/personId y estado, pero no rol vigente del responsable ni affordance de sustitución. Dirección puede consultar obligaciones de Dirección sin ser superior estricto. Inferir autoridad por visibilidad produciría un botón incorrecto. | Extender la lectura de detalle con una proyección opcional `evidenceActions` de dos booleanos: `canReplace` y `canConclude`. Se calculan en servidor con las mismas guardas de recurso/estado/autoridad ya aprobadas, sin nuevas reglas. Ausencia, incoherencia o desconocido oculta acciones. No añadir un token de acción al mapa de navegación ni endpoint de permisos. |
| No-efecto frente a revisión | Adenda 19 §§5/11/14 dice «materializará o reutilizará un snapshot» y «La creación de un snapshot registra EVIDENCE_REVIEW_SNAPSHOT_CREATED / SUCCESS dentro de la misma transacción». EfEvidenceReviewService añade snapshot y auditoría cuando la huella no existe. Por tanto el GET de revisión no es una lectura sin escrituras en toda la base. | **Decisión requerida:** conservar íntegramente esa excepción contractual. No-efecto significa no cambiar ejecución, asignación, evidencia, validación, idempotencia ni outbox. Primera huella permite exactamente snapshot+auditoría atómicos; huella repetida no añade filas. Si se exige cero escrituras absoluto, detener sólo UI-E07 y su consumo hasta nueva adenda de contrato; no alterar backend ni gates silenciosamente. |

Los nombres de campos nuevos de proyección son **propuesta**, no hechos aprobados. Su aceptación autoriza únicamente esa representación de lectura mínima y sus pruebas. Las reglas de elegibilidad pertenecen a los módulos: reutilizar/extraer evaluadores existentes sin reglas en Razor/JS ni escritura entre módulos; el lector de detalle compone resultados de contratos internos explícitos. No divulgar rol del responsable para sustituir esta proyección. Mantener respuesta y ETag existentes para los demás consumidores; propiedad opcional para compatibilidad. `canConclude` refleja autoridad/estado, no acredita evidencia completa: UI combina con revisión actual y el comando siempre reevalúa.

Después de aprobación íntegra, incorporar estas decisiones mediante la siguiente adenda F07 libre en raíz y referencias consumidoras de docs/design, en el mismo hito. Verificar el número libre antes de crearla; no editar congelados ni asignar identificador definitivo ahora. La excepción de revisión y la exclusión de descarga deben quedar explícitas en esa incorporación. No hay adenda aprobada creada por este plan.

## 4. Contratos que se consumirán

| Operación | Contrato y datos | Autoridad / concurrencia |
|---|---|---|
| Detalle GET existente | `/api/v1/obligations/{id}`; política capturada, responsable, estado, fechas, historia limitada y ETag de obligación. Conservar historyCursor independiente. | PER-TAREA-VER y alcance autorizado. GET sin escrituras. Conservar ETag recibido fuera de URL; no fabricarlo desde ítem o snapshot. Proyección propuesta de §3. |
| Versiones GET | `/api/v1/obligations/{id}/evidence`; filtros exclusivos requirementCode/status/limit/cursor; status VIGENTE o SUSTITUIDA, ausencia incluye ambas; 25 por defecto, máximo 100; ordinal ascendente, versión descendente, ID ascendente. | PER-TAREA-VER: propia vigente, superiores estrictos y Dirección en LOR-001. 404 convergente; sin writes, auditoría funcional, idempotencia, outbox ni S3/escaneo. |
| Representación de versión | evidenceItemId/itemRowVersion, requirement, version (número, estado, actor ID, instante, motivo, predecesora); exactamente file o structuredPayload según Adenda 20. | Metadata de binario sin contenido ni URL; payload estructurado cerrado sólo en detalle autorizado, con etiquetas FRONT-017 y escape HTML. No inventar nombre del actor: el GET sólo da submittedByUserId. No mostrar JSON crudo ni SHA completo por arrastre. |
| Revisión GET | `/api/v1/obligations/{id}/evidence-review`, sin query/cuerpo/If-Match/CSRF/idempotencia ni ETag. snapshotId, policy ID, resultado, evaluatedAt, requirements y missingRequirements exactos. | PER-TAREA-VER y mismo alcance. Excepción de persistencia de §3 pendiente de decisión. No confirma conclusión ni validación. |
| Sustitución POST | `/api/v1/obligations/{id}/evidence/{itemId}/replacements`; exactamente uno de fileId/structuredPayload, más reason según Adendas 18/20. | Pendiente: sólo responsable vigente con PER-EVIDENCIA-APORTAR; motivo opcional. Concluida: sólo superior estricto con PER-EVIDENCIA-SUSTITUIR y motivo. Idempotency-Key UUID, CSRF y If-Match fuerte del **ítem**, obtenido de itemRowVersion. |
| Carga segura reutilizada | upload-intents → PUT temporal → complete `{}` → status → reemplazo únicamente LIMPIO. Binarios JPEG/PNG/PDF según requisito, 1–15,728,640 bytes inclusive, tipo real/firma/SHA-256/ClamAV. | Intención ligada a obligación/política/requisito/actor; reautorizar en cada paso y volver a evaluar F_ENT_001 para foto condicional. URL de PUT sólo en transferencia técnica transitoria, jamás visible, log, captura o almacenamiento permanente. Claves separadas por operación. |
| Conclusión POST | `/api/v1/obligations/{id}/conclusion`, **cero bytes de cuerpo**, sin query. No enviar `{}`, motivo, snapshot, actor, estado o resultado. | Sólo responsable vigente de propia pendiente, sesión/MFA/cuenta/empleo/rol vigentes y PER-TAREA-EJECUTAR. CSRF, Idempotency-Key UUID e If-Match de **obligación**. `200` y ETag nuevo. El servidor reevalúa evidencia dentro de transacción SERIALIZABLE. |

Revisión: sólo VIGENTE del mismo requisito/política/obligación cuenta. `COMPLETA` requiere todos los aplicables satisfechos y ninguna condición NO_RESUELTA. `NO_APLICABLE` no aparece como faltante; `NO_RESUELTA` no equivale a falso. Ausencia de F_ENT_001 deja INCOMPLETA y ese formulario faltante; foto permanece sin resolverse. Reutilizar exactamente las tres relaciones nominales: avance/acción o conformidad, diez ítems verdaderos y F_ENT_001. No implementar motor de reglas ni nuevos cálculos cliente.

Conclusión: evidencia incompleta produce `422 EVIDENCIA_FALTANTE` y deja pendiente; política/historia incoherente falla cerrada. Vencimiento solo no concluye ni dispensa evidencia. Se permite anticipar según Adenda 21 §9; no añadir guarda temporal de cliente. Los CAT se prueban mediante los contratos existentes de materialización/evidencia/conclusión; no se inventa otra regla para compensar un desacuerdo.

Sustitución crea sucesora única y conserva anterior, actor/fecha/motivo/cadena. Motivo NFC, recorte, 1–500 caracteres si se proporciona, sin controles/HTML/saltos de línea según Adenda 18; no reutilizar mínimo 10 de asignaciones. No incluir secretos, URLs o contenido de evidencia en motivo. Cambiar F_ENT_001 no borra foto ni cambia conclusión/validación. Después de concluir no hay nueva primera aportación, cambio de responsable ni reapertura; sólo sustitución de ítem existente por superior autorizado. Nadie es superior estricto de Dirección.

Una revisión actual posterior no sustituye la fotografía histórica del cierre ni una decisión previa. No añadir historia general de auditoría, validaciones o resultado completo que el GET actual no expone. Concluida sigue siendo distinta de validada.

## 5. Composición y componentes propuestos

Una sola ruta Razor existente y shell aprobado. Mantener retorno protegido a lista, filtros originales, ancla/foco de origen y sesión request-scoped. Orden: contexto de tarea, UI-E06 versiones, UI-E07 revisión, aportación existente, editor de sustitución cuando se prepara por POST y UI-E08 conclusión, historia existente. Paneles blancos separados con cabecera/cuerpo; Poppins local OFL, Georgia sólo donde ya corresponde, sin The Seasons. CSS propio, únicamente variables oficiales; no biblioteca, tipografía, iconería externa ni valor visual nuevo.

**UI-E06.** Tabla «Versiones de evidencia», caption «Versiones conservadas de esta tarea». Columnas Requisito y tipo, Versión y estado, Fecha y aportante, Evidencia, Motivo, Acción. Fecha local con `<time>` UTC; aportante muestra «Identificador del aportante» y UUID autorizado, sin resolver nombres desde API ajena. file muestra nombre escapado, tipo/tamaño y subtipo cuando procede; no botón de abrir. structured muestra campos cerrados con etiquetas/valores ya aprobados, sin campos extras. Motivo sólo el autorizado por este GET. Cadena/predecesora permite identificar versión anterior por ID recibido, sin inferir datos no presentes en página.

Filtros «Requisito», «Estado de versión», «Consultar versiones», «Quitar filtros»; opciones de requisito de política capturada y «Todas», estado «Todas», «Vigente», «Sustituida». Dos cursores independientes para evidencia e historia; nombres Razor propuestos `evidenceRequirement`, `evidenceStatus`, `evidenceCursor`. Son aliases web allowlisted hacia filtros API reales, no nuevos filtros API. Proteger cursor con Data Protection ligado a actor/recurso/filtros, reset al cambiar filtro; count sólo de página, Anterior/Siguiente, sin totales ni ordenar columnas. No enumerar toda la historia para fabricar tabla sin paginación.

Acción «Preparar sustitución» sólo sobre versión vigente de ítem existente y proyección canReplace verdadera. Es POST Razor protegido que prepara el editor; no ejecuta sustitución ni produce una escritura funcional. Editor cerrado reutiliza formularios FRONT-017 vacíos para nuevo aporte estructurado; conserva lectura de versión anterior aparte. No copiar inadvertidamente contenido a una nueva versión. Binario reutiliza upload seguro, sin formularios multipart que creen vínculos. Requisito/ítem/política/versión original quedan ligados a intención protegida, no editables.

**Confirmación de sustitución.** Dialog corto «Sustituir evidencia», contexto TAR/requisito/versión actual y texto «La versión vigente quedará en historia como sustituida. La nueva versión no cambiará una conclusión ni una decisión de validación anterior». «Motivo (opcional)» en pendiente; «Motivo» obligatorio después de conclusión. Ayuda «Hasta 500 caracteres. No incluyas secretos, URLs ni contenido de evidencia». Botones «Cancelar» y «Sustituir evidencia», foco inicial Cancelar, Escape, Tab/Shift+Tab y retorno. Formulario complejo queda fuera de modal. Sin JS se presenta confirmación equivalente en la misma página mediante POST preparatorio, no una mutación GET.

**UI-E07.** Panel «Revisión de evidencia» con badge completa/incompleta, «Evaluada el» y fecha del servidor; «Consultar revisión» para petición explícita. Propuesta: la llegada al detalle no invoca revisión automáticamente; evita crear snapshots por navegar o filtrar historia/versiones. El POST Razor de consulta usa antiforgery y llama al GET existente, sin crear nuevo comando API. Tabla «Requisitos evaluados» con código/tipo, aplicabilidad, satisfacción y versión utilizada; lista «Evidencia faltante» con códigos exactos devueltos. No construir faltantes desde conteos de archivos. Renovar revisión después de aporte/sustitución por decisión explícita; invalidar el resultado visible previo como habilitación de conclusión.

**UI-E08.** Panel «Conclusión de tarea». Con autoridad proyectada y PENDIENTE, botón «Concluir tarea» sólo habilitado con revisión actual COMPLETA confirmada; ausencia de revisión o error deja inactivo y explica qué consultar. El modal «Concluir tarea» resume TAR/recurso/responsable y dice «La tarea quedará concluida. Esto no significa que esté validada». Sin campo de motivo ni checkbox contractual inventado. Cancelar/Concluir tarea, foco/escape/retorno iguales al dialog base. La conclusión reevalúa en servidor incluso si la revisión visible quedó obsoleta.

Componentes: shell/panel/encabezado compartidos, tabla/cursor, campos/select/textarea/radio, upload, `_StatusBadge`, `_ProblemAlert`, `_EmptyState`, `_MotivatedConfirmation` adaptado al motivo opcional o ausente conforme al consumidor, `_WorkPagination`. No cambiar mensajes/contratos de otros consumidores por reutilización. Resumen de error enfocable y avisos role=status. Sin facultad proyectada, ocultar acciones; no anunciarlas como deshabilitadas.

## 6. Catálogo literal propuesto para BR-M08

Conservar catálogo común HTTP/code y FRONT-017; estos textos son **propuestas pendientes**, no mensajes ya aprobados.

| Situación | Texto propuesto / acción |
|---|---|
| Sin versiones | «Aún no hay versiones de evidencia registradas». Primera aportación existente sólo si autorizada. |
| Filtro vacío | «No hay versiones que coincidan con estos filtros». «Quitar filtros». |
| Consulta de versiones en curso | «Consultando versiones…». |
| Filtro inválido | «Revisa los filtros de versiones». Asociar al campo conocido; no mostrar valores crudos. |
| Revisión no solicitada | «Consulta la revisión para conocer la evidencia faltante». «Consultar revisión». |
| Revisión en curso | «Consultando revisión…». |
| COMPLETA | «La evidencia está completa para esta revisión. El servidor volverá a comprobarla al concluir». |
| INCOMPLETA | «Falta evidencia para concluir. Revisa los requisitos indicados». |
| Sin faltantes, completa | «No hay requisitos de evidencia faltantes en esta revisión». |
| Revisión no disponible | «No se pudo comprobar la evidencia de esta tarea. No puede concluirse desde esta consulta». «Consultar revisión». |
| Aplicabilidad | «Aplicable», «No aplicable», «Sin resolver». Son traducciones de los tres valores existentes, no estados nuevos. |
| Satisfacción | «Satisfecho», «Faltante», «No requerido», «Sin resolver», según combinación válida recibida; nunca deducir satisfecho de ausencia. |
| Razones de faltante | EVIDENCIA_VIGENTE_AUSENTE: «Sin evidencia vigente»; EVIDENCIA_VIGENTE_NO_SATISFACE: «La evidencia vigente no satisface el requisito». |
| Preparación sin ítem vigente | «No hay una versión vigente disponible para sustituir». «Recargar detalle». |
| Motivo requerido/inválido | «Escribe un motivo válido de hasta 500 caracteres». Asociado a Motivo; respetar servidor canónico. |
| Sustitución en curso | «Sustituyendo evidencia…». |
| Sustitución confirmada | «Sustitución confirmada. La versión anterior permanece en historia». Mismo texto para replay; no afirmar otra creación. |
| Sustitución no permitida | «No puedes sustituir esta evidencia con tu autorización actual». Retirar acción, recarga explícita. |
| Falta revisión completa | «Consulta una revisión completa de la evidencia antes de concluir». |
| Conclusión en curso | «Concluyendo tarea…». |
| Conclusión confirmada | «Conclusión confirmada. La tarea está concluida; esto no significa que esté validada». Mismo texto para replay. |
| Ya concluida | «La tarea ya está concluida». «Recargar detalle»; no crear otra intención automática. |
| EVIDENCIA_FALTANTE al concluir | «La tarea sigue pendiente porque falta evidencia». Mostrar sólo referencias allowlisted del catálogo capturado recibidas en errors/MISSING; no JSON ni detail crudos. «Consultar revisión». |
| Conflicto 412 / IF_MATCH_* | Texto común aprobado y «Recargar detalle». Bloquear envío hasta recarga y nueva decisión explícita. |
| CONCLUSION_CONCURRENCIA_CONFLICTO | «No se pudo confirmar la conclusión por un cambio simultáneo. Consulta el estado de la tarea antes de continuar». «Recargar detalle». |
| CONCLUSION_INCONSISTENTE | «No se pudo comprobar la conclusión de esta tarea». «Recargar detalle». No presumir éxito. |
| Resultado de mutación incierto | «No se pudo confirmar el resultado. Puedes recuperar la solicitud original sin crear otra». «Recuperar resultado» o «Recargar detalle». |
| Nueva decisión | «Preparar otra solicitud». Advertir «La solicitud anterior puede haberse procesado. Consulta el estado antes de preparar otra» si resultado incierto. |
| Lectura/error inesperado | «No fue posible completar esta operación». Recarga/consulta explícita según región; correlationId seguro. |
| Revisión desactualizada tras aporte/sustitución | «La evidencia cambió. Consulta una nueva revisión antes de concluir». |
| CONCLUIDA persistida | «La tarea está concluida. Su historia se conserva». Responsable sin aporte/sustitución/conclusión; superior sólo sustitución autorizada. |

401 descarta sesión/cookies permitidas/intención y vuelve a acceso; 403 retira sección/acción correspondiente; 404 usa «No existe o no está disponible en tu alcance» sin datos de recurso. Conservar textos de CSRF/idempotencia/carga/tipo/límite/condición aprobados. Códigos desconocidos fallan cerrado. Si el cliente común sólo conserva path/code de errors y pierde reference de EVIDENCIA_FALTANTE, extenderlo con reference opcional validada por el consumidor; sin renderizar errors crudos ni alterar otros mensajes.

## 7. Estados, seguridad e intenciones

| Estado | Cobertura de las tres unidades |
|---|---|
| Normal | Datos confirmados, badges texto/icono, requisito y versión exactos; conclusión separada de validación. |
| Foco | Orden DOM estable, foco visible, filtros/cursor/enlaces alcanzables; Cancelar inicial y retorno a disparador o h2 si desaparece; errores enfocados. |
| Deshabilitado | Envío en curso, revisión pendiente/incompleta/error o conflicto; datos legibles. Falta de autoridad oculta acciones. |
| Error | Alerta regional y resumen/errores asociados; desconocido seguro; nunca reutilizar datos fallidos para habilitar acción. |
| Cargando | aria-busy regional, esqueleto de tabla y mensajes §6, progreso upload; doble envío bloqueado. |
| Vacío | Versiones ausentes, filtros sin coincidencias, revisión aún no solicitada y faltantes vacíos diferenciados; sin ítem no hay sustitución. |

VIGENTE/SUSTITUIDA y COMPLETA/INCOMPLETA reutilizan pares/iconos oficiales; aplicabilidad/satisfacción se presenta como texto recibido/traducido, sin badge semántico nuevo. PENDIENTE/CONCLUIDA y Vencida separados. El estado LIMPIO del archivo habilita vínculo, no significa evidencia completa ni conclusión.

Preparación Razor siempre POST con antiforgery, formulario allowlisted sin campos duplicados/extra. Intenciones Data Protection ligan actor, obligación, ítem, requisito, operación, ETag original, clave, cuerpo exacto y vencimiento. Reutilizar el límite de FRONT-017: el menor de IdleExpiresAt y AbsoluteExpiresAt de la sesión al preparar, también para conclusión; no prorrogar sesión, intención de carga ni autoridad. Token protegido fuera de URL; no localStorage/sessionStorage ni log/ToString de cuerpo. Archivo, motivo y payload nunca en query string.

Recuperar respuesta perdida reenvía sólo intención exacta original, previa reautorización, incluso después de concluir. No cambiar ETag/clave/cuerpo al reintentar. 412 exige recarga y nueva confirmación; no automática. GET posterior que muestra CONCLUIDA acredita estado pero no demuestra que una solicitud incierta concreta ganó. Cada operación tiene scope/clave separados; POST de conclusión usa Body=null con transferencia real cero bytes, verificable en prueba HTTP. Reutilizar ISgolApiClient y puente allowlisted de cookies/CSRF; no acceder a DbContext desde Razor.

## 8. Archivos previstos después de aprobar

| Grupo | Archivos / cambio mínimo previsto |
|---|---|
| Detalle y aporte | `src/Sgol.Web/Pages/MyWork/Details.cshtml`, `Details.cshtml.cs`, `Details.Evidence.cs`, `_EvidenceContribution.cshtml`: conservar FRONT-016/017, ETag de obligación y carga; regiones independientes y filtros allowlisted. |
| Nuevos consumidores | En ese directorio: `Details.EvidenceReview.cs`, `Details.EvidenceReplacement.cs`, `Details.Conclusion.cs`, `_EvidenceVersions.cshtml`, `_EvidenceReview.cshtml`, `_EvidenceReplacement.cshtml`, `_Conclusion.cshtml`. Lista exacta propuesta, no creación presente. |
| Presentación/intención | `src/Sgol.Web/Interface/MyWork/EvidenceContributionPresentation.cs`/`EvidenceIntention.cs` reutilizados; nuevos `EvidenceReviewPresentation.cs`, `EvidenceReplacementIntention.cs`, `ConclusionIntention.cs`; cursor/contexto existente extendido únicamente para evidencia. |
| Sesión/lectura de autoridad | `src/Modules/Identity/Contracts/Roles.cs`; `src/Modules/Execution/Contracts/ObligationQueries.cs`; `src/Sgol.Web/Infrastructure/Persistence/Execution/EfObligationQueryReader.cs`; contratos internos mínimos de lectura en Evidence/Execution y adaptadores de sus módulos para §3. Sin lógica de negocio en Web composition/Razor y sin exponer nuevos comandos. |
| Cliente/compartidos | `src/Sgol.Web/Interface/ApiClient/ApiClientContracts.cs`/`SgolApiClient.cs` sólo si falta conservar reference de error; `_MotivatedConfirmation.cshtml` y modelo sólo si requiere variante compatible; estilos compartidos ya existentes y `wwwroot/js/evidence-contribution.js`/`my-work.js` para nuevo modo/intención/foco. Sin nuevas hojas de valores ni biblioteca. |
| Pruebas nuevas | `Front018PresentationTests.cs`, `Front018ArchitectureTests.cs`, `Front018BrowserTests.cs` en proyectos existentes; casos enfocados PostgreSQL de evidencia/conclusión/consulta, fixture sintética y snapshot de no-efecto. |
| Pruebas existentes | EvidenceInfrastructureArchitectureTests, ObligationConclusionArchitectureTests, Front016ArchitectureTests, Front017ArchitectureTests, SgolApiClientTests, pruebas de sesión/roles, EvidenceApiEndpointTests, ObligationConclusionApiEndpointTests, Front016/017PresentationTests y BrowserTests, persistencia directamente afectada. |
| Documentación | Siguiente adenda libre, extensión consumidora de componentes/estados/mensajes/navegación, IMPLEMENTATION_STATUS.md, informe `FRONT_018_VERSIONES_REVISION_Y_CONCLUSION.md`, manifiesto `FRONT_018_CAPTURAS.md` y evidencia sintética fuera de Fuentes. Conservar transición de 96dfe4d. |

No se prevé cambiar rutas API, payloads estructurados, infraestructura, NuGet/locks, workflows, migraciones históricas, fuentes tipográficas, congelados ni Fuentes. Si aparece necesidad fuera de las proyecciones descritas, documentar diferencia antes de ampliar el plan.

## 9. Validación y criterios de aceptación

Ahora sólo planificación: no build, restore, pruebas, navegador, formato ni checks remotos. Se ejecutaron preflight y lecturas/inspección de Git; verificar diff documental al guardar. Después de aprobación:

- SDK aislado fijado: `dotnet build --no-restore --configuration Release`; restore locked sólo por ausencia real de artefactos o cambio autorizado de dependencias, una vez. `dotnet test --no-build --configuration Release --filter <pruebas afectadas>` por proyecto y `git diff --check`. Registrar comandos exactos/filtros/resultados en informe, sin afirmar pruebas aún no ejecutadas.
- CA-025/CP-025-P/N: ambos estados, propietario/superior/par/inferior/histórico/otra sucursal, ausencia de motivo, normalización y longitud, ítem inexistente y F_ENT_001/foto condicional; versiones consultables, una VIGENTE, cadena y actor/fecha/motivo. No primera aportación posterior a conclusión ni sustitución por superior de pendiente.
- CA-026/CP-026-P/N: conjunto exacto de requisitos, falta uno de varios, evidencia sustituida no satisface, diez SI, acción/conformidad correspondiente, condición verdadera/falsa/sin resolver, política nula/corrupta y payload inválido. Snapshot nuevo atómico; repetido sin nuevas filas si §3 se aprueba.
- CA-022/CP-022-P/N: propia completa PENDIENTE→CONCLUIDA; faltante/actor distinto no cambia; expiración por sí sola permanece pendiente. Sin cuerpo/query ni motivo; ETag de obligación independiente del ítem. Permisos/relaciones vigentes revalidados tras preparar y después del replay.
- CAT-001..008 / CPT respectivos: ocho TAR con política capturada y casos positivos/negativos de evidencia aprobados. TAR-0005 umbral y fuente; TAR-0007 origen/liberación/referencia; TAR-0008 expediente y reclamantes; TAR-0011 autorización/documentos/entrega; TAR-0018 diez SI/foto/planograma; TAR-0026 comprobante/FORM-ADM-02; TAR-0092 F_ENT_001/documento/foto condicional; TAR-0093 padre/foto/anotación/aviso interno. Resultados CUMPLIDA/NO_CUMPLIDA son validación posterior y no se implementan ni simulan aquí. Un desacuerdo contractual específico se presenta, no se cubre con regla UI.
- PostgreSQL real enfocado: dos reemplazos con mismo ETag, dos conclusiones/replay, reemplazo versus conclusión y cambio de asignación, versión/huella consistentes, rollback por auditoría, constraints/idempotencia y respuesta perdida. Snapshots antes/después: listado/detalle/filtros/estado sin escrituras funcionales; revisión permite sólo excepción §3; fallos de mutación sin éxito parcial. Decisión previa y snapshot de cierre permanecen literales tras sustitución, usando fixture interna existente sin UI FRONT-019.
- Carga S3/ClamAV enfocada del modo sustitución: límite/firma/tipo/hash, rechazo/infección/error nunca vinculan, archivo limpio sólo de actor/obligación/requisito correctos, autoridad/condición revalidada y recuperación sin otra versión. Reutilizar fixtures, datos sintéticos y servicios reales disponibles; no relajar firmado ni presentar emulación como producción.
- Playwright enfocado de aplicación HTTPS/PostgreSQL: propietario y superiores, consulta/versiones/cursores, formulario cerrado, revisión/faltantes, ambos reemplazos y conclusión, error/vacío/carga/conflicto/401/CSRF; teclado/foco/retorno, contraste renderizado, controles 44×44 por tokens, texto 200 %, reflow 320 CSS px, escritorio/móvil y reduced-motion. Conservar pruebas FRONT-016/017 directamente afectadas. No suite integral por cada edición.

**Prevención de fallos históricos.** EvidenceInfrastructureArchitectureTests.Hu025UsesExactlyTheApprovedSurfaceAndThreePersistentAggregates debe admitir sólo los parciales/JS concretos aprobados y exigir existencia, conservar cuatro POST/tres GET y prohibición de descarga. ObligationConclusionArchitectureTests actualmente exige cero UI `*Conclusion*`: sustituir únicamente esa expectativa por inventario cerrado para FRONT-018, manteniendo independencia, transacción y ausencia de validación/outbox/S3. Front016ArchitectureTests excluye sólo Details.Evidence.cs y prohíbe toda llamada evidence/conclusion: delimitar sus archivos exactos FRONT-016 y comprobar separadamente los consumidores nuevos. Front017ArchitectureTests mantiene restricciones de primera aportación; no ampliar sus listas a cualquier archivo. Revisar también inventarios de rutas/cliente/idempotencia/sesión y fixtures antes de correr pruebas; corregir expectativa obsoleta únicamente contra contrato aprobado, nunca eliminar una guarda válida.

Diferidos conservados: zoom nativo y lector de pantalla requieren comprobación manual específica, dispositivos físicos no disponibles en emulación, WebKit Windows PUT a S3 local HTTP sin respuesta HTTP acreditada, y límites de aislamiento productivo SeaweedFS no demostrados por fixtures locales. No se convierten en PASS por capturas. Arquitectura incompatible se registra VALIDACION_DIFERIDA_POR_ARQUITECTURA; este entorno declarado es AMD64 y no se presume incompatibilidad de PostgreSQL/Testcontainers. Cualquier fallo de build o prueba enfocada compatible impide marcar Implementada localmente.

## 10. Entrega local y publicación futura

Tras aprobar, incorporar decisiones antes de UI, implementar en entregables pequeños, validar afectados y crear commits locales coherentes. Actualizar trazabilidad en el mismo hito; Implementada localmente sólo con código, alcance y comprobaciones disponibles pasadas. Capturas sintéticas de aplicación funcionando, con manifiesto de escenario/estado/escritorio-móvil y explicación sencilla de consulta, sustitución y conclusión; solicitar revisión visual antes de cierre humano. No presentar preview como integración ni incluir secretos/URLs firmadas/evidencia real.

No push/PR/checks/heartbeat ahora. Una autorización futura de publicación incluye preparar commits, comprobar **formato antes del push** con `dotnet format --verify-no-changes --no-restore` y SDK fijado, corregir sólo el hito, validar afectados, abrir un único PR con 96dfe4d incluido, adjuntarlo al chat y seguir/corregir su pipeline. Mantener gates sin rebajar. Cada cabeza nueva requiere su propia evidencia; leer logs antes de reintentar y distinguir instalación de navegador de tiempo de pruebas. Si termina un turno con checks pendientes, configurar y verificar heartbeat en este chat, silencioso sin cambios accionables. Con todos los requeridos correctos y revisión/requisitos cubiertos, pausar seguimiento y solicitar merge con PR/SHA vigente/run y límites. No merge ni despliegue por autorización de publicación.

## 11. Aprobación solicitada

Se solicita aprobar el plan completo, incluyendo expresamente: exclusión de descarga de §3; excepción contractual snapshot+auditoría de revisión; proyección de permisos existentes y evidenceActions; composición UI-E06/E07/E08 y catálogo literal de §§5–7; intenciones, archivos y validaciones previstos. Esta aprobación habilita su incorporación documental y ejecución local de FRONT-018, no publicación, merge ni despliegue.

Si se exige cero escrituras absoluto en revisión o se solicita descarga, esa parte necesita decisión contractual nueva antes de implementación. El resto del plan conserva su alcance; no se elige silenciosamente una interpretación.
