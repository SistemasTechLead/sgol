# FRONT-020 — Plan único de implementación

## 1. Estado, autorización y base

2026-10-01, America/Mexico_City. **APROBADO ÍNTEGRAMENTE** por el responsable mediante «Apruebo integramente el plan». Se autoriza exclusivamente la implementación local de FRONT-020 conforme a este documento; publicación, merge y despliegue requieren autorización separada. Los apartados que describen decisiones propuestas conservan la secuencia documental y quedan aprobados por esta declaración. La aprobación de este documento será necesaria antes de modificar código, pruebas productivas o diseño operativo. Este plan comprende un único hito con tres incrementos internos; no son tres planes ni tres publicaciones.

Solicitud: UI-R01/R02/U01/U02/K01..K03, HU-029/HU-032/HU-033/HU-035, CA/CP-029/032/033/035. Fila efectiva: Adenda 45, línea 103, secuencia 24, localizada mediante docs/INDICE_IDS.md. Filas backend de referencia: F07_BACKLOG_DE_IMPLEMENTACION.md 106–110; no se reinicia su implementación.

FRONT-019 se acepta **Integrada** por evidencia expresa del responsable: [PR #88](https://github.com/SistemasTechLead/sgol/pull/88), cabeza `577d7d53914fe85b4db383b0a0f2f3f0e85b3b3a`, merge `c5097010d75d90db40cfc5e1146974ace5617a3a`, [pipeline final correcto, intento 1](https://github.com/SistemasTechLead/sgol/actions/runs/36935208368/attempts/1). Aprobaciones de plan, capturas, commits, publicación y merge aceptadas; unitarias 973/973, arquitectura 68/68, integración 273/273, navegador 33/33; todos los checks requeridos correctos. Experimento opcional HU-035 isolated network SKIPPED. Seguimiento pausado y verificado según evidencia aportada. Sin despliegue. Los registros anteriores de espera son historia superada, no dependencias pendientes.

Git inicial: árbol limpio, HEAD en la cabeza anterior al merge, rama codex/front-019. Se verificó el objeto local del merge y se creó `codex/front-020` directamente en ese merge, sin fetch, push ni cambios ajenos. La planificación parte de una base que incluye la integración aceptada.

Preflight fue la única comprobación inicial de entorno. Informó árbol/Fuentes limpios, sesión gh autenticada y SDK de PATH 10.0.401 incompatible con global.json 10.0.400. No se diagnosticó más el entorno. Las validaciones de implementación usarán exclusivamente `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe`, sin cambiar global.json ni restore salvo necesidad concreta.

## 2. Fuentes y precedencia

Leídos primero IMPLEMENTATION_STATUS y FRONT_019_VALIDACION_Y_SUPERVISION; se conservan sus lecciones de publicación y sus límites. Fuentes funcionales: F05_ESPECIFICACION_FUNCIONAL_MVP 144/147/148/150; F05_CRITERIOS_DE_ACEPTACION 59/62/63/65; RN-025..030 y matriz de permisos, filas PER-INDICADOR-VER/PER-DIRECCION-VER/PER-AUDITORIA-VER/PER-CONTINUIDAD-VER. F06_CONTRATO_DE_API §§2/3/5.8/7; F06_SEGURIDAD_Y_OPERACION §10; ADR-009/010/011/012. Adendas 27 §§7–14, 28 §§7–14, 29 §§6–17, 33 §§5–17 precisan los contratos existentes y prevalecen sobre rutas conceptuales antiguas.

Diseño leído: ESTILO_VISUAL_V2, referencia-renovada, tokens, componentes, estados-y-mensajes, estados-de-dominio, accesibilidad, navegacion y Adenda 55. Se acepta la base PR #85, Poppins local OFL y Georgia de sistema; The Seasons excluida. Las referencias históricas no reabren sus aprobaciones.

Contraste con código: Indicators.cs, AuditQueries.cs, RecoveryReconciliation.cs; IndicatorApiEndpoints, DirectionOverviewApiEndpoints, AuditApiEndpoints y ContinuityApiEndpoints; Roles.cs, NavigationItem.cs, _Layout.cshtml, cliente común y EfRecoveryReconciliationService. No se reconstruyen historias terminadas ni se releen integralmente F00–F07.

Tabla Tareas insertadas por adenda: TECH-E2E-CV-05 Terminada, PR #59, merge `32c3961ff67f79670d0824da71d4f70a06d1dc01`, pipeline `35797088036` correcto; cierre aceptado sin repetir gates. FRONT-019 integrada satisface precedencia. TECH-FRONT-005 No iniciada va después de FRONT-020 y queda excluida. Las filas históricas FRONT-019..020 No iniciadas no invalidan la evidencia vigente. No se identificó dependencia real de código pendiente; sí decisiones consumidoras de §4.

## 3. Alcance y exclusiones

Incluido: consultar exactamente los cinco indicadores operativos; panorama exclusivo de Dirección con los mismos cinco; lista/detalle y traza completa autorizada de auditoría; solicitar, consultar por ID y aprobar reconciliación únicamente mediante el contrato existente de Dirección. Componentes y mensajes consumidores, cliente tipado, navegación, validaciones enfocadas y trazabilidad del mismo hito.

Excluido: sexto indicador, porcentajes derivados, KPI monetario, monto/incentivo/nómina, exportación, integración externa, listado general de reconciliaciones, descarga de manifiestos, backup/restore desde UI, reparación, reinicio, cancelación operativa, borrado, aprobación forzada, cambios a evidencia/validaciones anteriores, ampliación de descarga/preview, TECH-FRONT-005, merge y despliegue. BR-API04 permanece abierta globalmente.

Las lecturas de indicadores/panorama/auditoría son sin efectos funcionales. **Consultar continuidad sí inserta RECOVERY_RECONCILIATION_VIEWED**; si falla la auditoría no se entrega reporte. Crear continuidad añade solicitud/evento/outbox/idempotencia/auditoría atómicos: puede disparar captura técnica mediante el Worker existente. Aprobar añade aceptación y auditoría transaccionales; no repara ni despliega. Preparar/cancelar confirmaciones no llama comandos ni consultas auditadas implícitas.

## 4. Brechas y decisiones sometidas a aprobación

Hechos: Adenda 45 declara BR-API05, BR-D10..D12 y BR-M11/M12 como condiciones; referencia-renovada las mantiene por resolver. No existe composición específica de estas unidades ni catálogo completo de sus mensajes en docs/design. Los documentos históricos INVENTARIO_EXACTO_FRONTEND.md y BRECHAS_Y_DECISIONES_PENDIENTES_FRONTEND.md nombrados por Adenda 44 no están disponibles en la raíz ni en archivos Git; Adenda 45 declara autocontenidas sus condiciones. No se presume conocer definiciones adicionales ausentes ni se consulta Fuentes para recuperarlas.

| Brecha | Hecho o carencia | Propuesta concreta, pendiente de aprobación |
|---|---|---|
| BR-API05 | F06 conceptual recovery-runs no coincide con superficie eficaz de Adenda 33 y código. No hay listado general ni cursor de diferencias; DTO no contiene código de fallo técnico ni historial completo de eventos de continuidad. | Consumir sólo POST reconciliations, GET por ID y POST approval. K01 consulta por ID y solicita; K02 presenta el reporte minimizado existente; K03 confirma aprobación elegible. Mostrar FAILED sin inventar causa; no prometer historial/eventos no devueltos. No nuevo endpoint, DTO de dominio ni listado. |
| BR-D10 | Faltan composiciones R01/R02. | Dos secciones independientes en /indicadores: Operación y, sólo Dirección, Panorama de Dirección. Cuatro filas de conteo más una tabla de carga constituyen cinco indicadores; base y denominadores son contexto, no un sexto indicador. §6. |
| BR-D11 | Faltan composiciones U01/U02. | /auditoria con filtros cerrados y modo traza; detalle /auditoria/eventos/{eventId:guid}. Tabla cronológica en traza, general descendente, cambios minimizados en tabla campo/antes/después, §7. |
| BR-D12 | Faltan composición K01..K03 y representación de estados. | /continuidad y detalle /continuidad/reconciliaciones/{reconciliationId:guid}; solicitud y aprobación motivadas mediante confirmación; reporte de diferencias sin esconder mismatch. Mapas de §8/9. |
| BR-M11 | Faltan textos de indicadores/auditoría. | Catálogo literal §9, con ceros verdaderos, ausencia de consulta distinta de vacío y traza incompleta explícita. |
| BR-M12 | Faltan textos de continuidad, efectos y conflictos. | Catálogo literal §9, consulta auditada explícita, solicitud distinta de recuperación concluida, igualdad distinta de aprobación y 412 sin reenvío. |

Carencia técnica de presentación: Roles.cs no proyecta PER-INDICADOR-VER ni PER-AUDITORIA-VER para los cuatro roles ni PER-DIRECCION-VER para Dirección; sí proyecta PER-CONTINUIDAD-VER para Dirección. Propuesta: añadir exclusivamente esos permisos ya aprobados a la proyección de sesión, sin crear permisos ni convertir la sesión en autoridad de dominio. Cada API reautoriza.

Diferencia acotada de forma: Adenda 27 §7 dice isoYear de cuatro dígitos; endpoint integrado admite enteros 1..9999. Propuesta consumidora: campo de año con cuatro dígitos y envío formateado a cuatro dígitos para R01 (0001..9999); R02 conserva su rango 1..9999. No se cambia API ni se presenta su aceptación de formatos adicionales como resolución global. Si no se aprueba esta forma, se detiene esa parte antes de código.

Inferencia técnica: componentes compartidos actuales bastan para materializar las composiciones propuestas sin biblioteca ni token nuevo. No es aprobación de nuevas pantallas. Decisión aprobada previa: arquitectura/diseño base y contratos backend. **Todas las resoluciones consumidoras de este documento son propuestas hasta aprobación íntegra.** Después de aprobar, incorporar referencia normativa en una nueva adenda F07 de raíz y docs/design antes de escribir interfaz; no modificar F00–F07 congelados. Numeración de adenda se verifica al incorporarla.

## 5. Autorización, jerarquía y navegación

Sesión individual/MFA completo, cuenta/persona/empleo/rol único canónico vigentes en LOR-001. Se comprueba permiso, recurso, jerarquía y estado en servidor en cada petición, incluido replay. Un claim, puesto textual, UUID, filtro o control oculto no concede acceso.

| Consulta | Dirección | Administración | Subcoordinación | Piso |
|---|---|---|---|---|
| R01 PER-INDICADOR-VER | Propia + Administración/Subcoordinación/Piso | Propia + Subcoordinación/Piso | Propia + Piso | Propia |
| R02 PER-DIRECCION-VER | Toda LOR-001, incluidos pares Dirección y obligaciones sin asignación sin filtros de persona/nivel | Denegada | Denegada | Denegada |
| U01/U02 PER-AUDITORIA-VER | Todo LOR-001 y globales permitidos | Sujeto histórico propio + niveles inferiores | Sujeto histórico propio + Piso | Sujeto histórico propio |
| K01..K03 PER-CONTINUIDAD-VER | Solicitar/consultar; aprobar sólo MATCHED elegible | Denegada | Denegada | Denegada |

Auditoría determina sujeto/nivel mediante relaciones y versiones persistidas al hecho, no rol actual del actor histórico. BRANCH_GOVERNANCE/SYSTEM_GLOBAL/UNRESOLVED sólo Dirección en general; dependencias concretas de configuración pueden aparecer en traza autorizada. Traza no Dirección con un eslabón de par/superior o irresoluble converge completa a 404, sin suprimirlo. Dirección no adquiere permiso de borrar.

401 inicia acceso con destino GET local protegido, sin conservar cuerpos. 403 retira datos y acciones de la región; 404 de evento/reconciliación/traza convergente. Filtros válidos de indicadores fuera de alcance devuelven ceros/vacío, sin confirmar identidad; scope devuelto gobierna lo mostrado.

Navegación propuesta: Indicadores y Auditoría para cuatro roles con sus permisos; Continuidad sólo Dirección + permiso. R02 es sección, no un cuarto enlace. Registrar rutas GET/deep links y query allowlisted en SafeReturnDestination, shell y navegación; ningún enlace API arbitrario. No autocompletar personas mediante PER-PERSONA-ADMIN: filtros por ID canónico con ayuda, nombres sólo de respuestas autorizadas.

Inventario exacto esperado en fixtures canónicas con todos sus permisos: Dirección 8 enlaces (mi-trabajo, validaciones, personas-y-accesos, configuracion, planificacion, indicadores, auditoria, continuidad); Administración/Subcoordinación 6 (sin personas-y-accesos ni continuidad); Piso 5 (sin validaciones/personas-y-accesos/continuidad). Mantener cada enlace exactamente una vez, negativos y permisos faltantes. Revisar FRONT-002/005/006, navegación unitaria y demás inventarios directamente afectados antes de publicación. No ajustar expectativas correctas para aceptar respuestas inesperadas.

## 6. R01/R02: contratos, fórmulas y pantalla

GET /api/v1/indicators y GET /api/v1/direction/overview. Query cerrada: isoYear, isoWeek obligatorios; level canónico, responsiblePersonId UUID D minúsculo no vacío, cursor, limit opcionales. Limit 1..100, defecto 25. Repetidos/desconocidos/vacíos/inválidos se rechazan; filtros AND. Semana ISO lunes–domingo America/Mexico_City, membresía por week_period persistido; nunca por vencimiento/conclusión/validación. La UI no crea semana ni elige silenciosamente un período: inicio con formulario pendiente de consulta. Filtros visibles «Año ISO», «Semana ISO», «Nivel», «ID de persona responsable», «Filas por página»; acción «Consultar indicadores» o «Consultar panorama», «Quitar filtros» conserva período y reinicia cursor. Nivel vacío no restringe, opciones canónicas etiquetadas por SessionPresentation.

Una respuesta independiente por sección: período/rango/zona recibidos, scope efectivo, queriedAt, baseObligationsCount; tabla «Conteos operativos» con columnas «Indicador», «Conteo», «Denominador» y exactamente cuatro filas. Tabla «Carga activa por persona» con código/nombre, nivel y carga recibidos y denominador común pending.count. No porcentajes, suma de página como total ni comparación entre snapshots distintos. Las ayudas siguientes explican las fórmulas y son parte del catálogo propuesto.

| Indicador | Numerador contractual | Denominador |
|---|---|---|
| Pendientes | IDs distintos del universo con executionStatus PENDIENTE; no son validaciones pendientes | baseObligationsCount |
| Concluidas | IDs distintos con executionStatus CONCLUIDA, exclusivamente ejecución | baseObligationsCount |
| Validadas | Una decisión VIGENTE de CUMPLIDA, INCOMPLETA o NO_CUMPLIDA, una vez por obligación | baseObligationsCount |
| Incumplidas | Única decisión VIGENTE NO_CUMPLIDA; vencida sin esa decisión no entra | baseObligationsCount |
| Carga activa por persona | PENDIENTE con única asignación VIGENTE a esa persona, una vez por obligación | pending.count |

R01 universo: obligación LOR-001 del período, única asignación vigente y responsable vigente autorizado. Propia/inferiores, nunca pares; personas visibles sin obligación sí tienen fila cero. R02 sin filtros de nivel/persona: todas las obligaciones del período, incluso sin asignación; con esos filtros sólo asignaciones vigentes coincidentes. Responsable asignado incoherente falla completo, no se oculta como sin asignar.

Invariantes: pending + concluded = base; nonCompliant <= validated. Suma de cargas de **toda la colección**, no una página: R01 = pending; R02 <= pending, diferencia contractual por pendientes sin asignar. No mostrar esa diferencia como KPI ni fila ficticia. Ayuda R02 explica que las pendientes sin asignación integran el denominador y no generan persona de carga.

Sin obligaciones: cuatro ceros y denominadores cero; carga puede contener personas con cero. Sin personas visibles: tabla vacía. Nunca dividir entre cero ni sustituirlo por «sin información». Política nula, requisito ausente/PENDIENTE o sin decisión vigente dan cero validadas/incumplidas; no materializar requisitos. Asignación/decisión SUSTITUIDA no suma; no derivar validación de ejecución CONCLUIDA. Anomalías de cadena/multiplicidad producen error completo, nunca ceros ni conteos parciales.

Cursor API pagina sólo activeLoadByPerson.items. Mantener agregados de la respuesta; meta.count es filas de carga recibidas. Orden person.stableCode ordinal ascendente y person.id ascendente del servidor, sin ordenación cliente. Rutas/actor/filtros/contextos separados, anterior mediante contexto protegido existente, siguiente sólo con nextCursor; cambio de filtro reinicia consulta. Cada página reautoriza y tiene su queriedAt; no afirmar snapshot único entre peticiones. GET sin SaveChanges/auditoría/snapshots/outbox, no ETag/If-Match, private no-store/no-cache.

Mapa de errores exacto: 400 FILTRO_INDICADORES_INVALIDO / FILTRO_DIRECCION_INVALIDO usa el mensaje de filtros; 500 CONSULTA_INDICADORES_INCONSISTENTE / CONSULTA_DIRECCION_INCONSISTENTE usa el de conciliación, sin datos parciales. 401/403 y ERROR_INTERNO usan los estados seguros de §9. No hay enlaces de detalle de obligación en estos DTO ni se fabrican a partir de conteos.

## 7. U01/U02: auditoría e historia

GET /api/v1/audit-events: from inclusivo, to exclusivo obligatorios en RFC3339 Z; máximo 31 días de 24 horas, sin predeterminado. Propuesta de controles: campos de texto etiquetados «Desde (UTC, incluido)» y «Hasta (UTC, excluido)», ejemplo sintético RFC3339 en ayuda; no usar datetime-local que sugiera la zona operativa de tareas. Filtros adicionales actorUserId, resourceType, resourceId (exige type), action, outcome, correlationId, level, traceObligationId, cursor, limit. branchCode se omite y equivale a LOR-001. UUID canónico; type/action/outcome ASCII [A-Z0-9_.:-], 1..128. No catálogos inventados, nombres ni texto de motivo como filtros.

Dos modos explícitos de formulario: «Eventos» y «Traza de obligación». Traza requiere traceObligationId; incompatible con actorUserId/resourceType/resourceId/correlationId/level, admite action/outcome. Se validan incompatibilidades en servidor y formulario; no enviar filtros ocultos ni descartarlos silenciosamente. Ambos requieren fechas incluso con cursor. Sólo consultar tras decisión explícita.

Lista general descendente occurredAt/id; traza ascendente. Columnas «Fecha y hora (UTC)», «Actor», «Acción», «Recurso», «Alcance», «Resultado», «Detalle». Mostrar IDs/tipos y null como «No informado», sin resolver nombres ajenos. Actor histórico inactivo conserva ID; tipo/acción/outcome históricos desconocidos se muestran como texto escapado exacto, sin convertirlos en estado nuevo.

Traza conserva configuración congelada/publicación → asignaciones → versiones de evidencia/revisión → decisiones; no enlaza por correlación o texto. meta.completeness se muestra en cuatro filas «Configuración», «Asignación», «Evidencia», «Validación»: «Con hechos registrados» / «Sin hechos registrados en la consulta». Booleanos de colección lógica completa y período/filtros, no sólo página, ni garantía de vigencia/éxito. No llamar completa a una página ni fabricar eslabones faltantes.

Detalle GET /api/v1/audit-events/{id}, sin query API. Encabezado de identidad/instante/correlación, actor/acción/recurso, scope, resultado, «Motivo registrado» Sí/No (reason.provided); tabla de change.before/after sólo minimizados por servidor. Mostrar omittedFieldCount y no inferir claves omitidas. Diferenciar clave ausente («No informado») de valor JSON null («Nulo»). Renderizado codificado, escalares permitidos conservados; sin JSON crudo, motivos/fundamentos históricos ni contenido de evidencia. Resource archivado/sustituido conserva ID original, no enlazarlo a un sucesor ni consultar su estado actual.

Cursor: 25 defecto, 100 máximo, cerca meta.snapshot de primera consulta, contexto exacto repetido; expira 15 minutos. Inserciones posteriores a la cerca se ven en consulta nueva; inserción retroactiva anterior puede aparecer, no prometer snapshot PostgreSQL entre páginas. Cambios de identidad/alcance/filtros/límite invalidan cursor. Regreso de detalle conserva contexto protegido y foco de origen; fallback al h1. No totales inferidos, borrar/exportar ni nuevas acciones. GET READ ONLY/REPEATABLE READ sin auditoría de lectura ni cambios.

## 8. K01..K03: continuidad restringida

K01: /continuidad, panel «Consultar reconciliación» con «ID de reconciliación» y «Consultar reconciliación»; panel independiente «Solicitar reconciliación» con «Motivo» y «Preparar solicitud». No listado/sugerencias de IDs ni consulta automática. K02: detalle autorizado con ID, estado, requestedAt/targetRecoveryAt UTC, sequence, hashes raíz, differenceCount/differencesTruncated, RPO/RTO en segundos cuando presentes, approvedAt/approvedBy. Ausentes «No informado», nunca cero. No inventar fecha de terminación, código de fallo ni historial que el DTO no devuelve.

Tabla «Diferencias» conserva todos los elementos devueltos y orden ordinal: grupo, resourceType, stableKey, field, kind, expectedSha256, actualSha256. Es proyección minimizada, no payload de evidencia. Sin cursor API ni total calculado de una porción. Propuesta inicial: tabla completa recibida, sin paginación ficticia, exportación o recorte silencioso. Validar respuesta extensa y coste de render; si el límite contractual de 100 000 exige otra composición, detener esa optimización y presentar decisión antes de introducir paginación o almacenamiento. Si differencesTruncated=true, aviso persistente de comparación incompleta; nunca PASS. Un reporte DIFFERENT sin filas recibidas conserva mismatch y su conteo; ausencia de detalle no significa igualdad.

K03: preparar aprobación sólo si respuesta MATCHED, cero diferencias, no truncada, RPO/RTO presentes dentro de 3600/14400 y ETag válido recibido. Es predicado de presentación conservador; servidor sigue decidiendo evidencia técnica completa y estado. APPROVED no ofrece otra aprobación; DIFFERENT/FAILED/etapas no ofrecen aprobación/reparación. Estado desconocido se muestra escapado como «Estado no reconocido: {status}», sin acciones ni éxito.

Mapas de estados propuestos, sin crear transiciones: REQUESTED «Solicitada» (info/documento), REFERENCE_CAPTURING «Capturando referencia» (info/reloj), REFERENCE_READY «Referencia preparada» (info/documento), RESTORE_STARTED «Restauración iniciada» (info/reloj), RECONCILING «Comparando» (info/reloj), MATCHED «Coincide; pendiente de aprobación» (info/check), DIFFERENT «Diferencias detectadas» (peligro/advertencia), FAILED «Falló la reconciliación» (peligro/advertencia), APPROVED «Aprobada» (éxito/check). Iconos compartidos/texto y pares existentes; MATCHED nunca aceptación humana.

POST crear: único cuerpo {reason}, Idempotency-Key UUID canónica, sin If-Match; 201 y Location propios validados, ETag de sequence. POST approval: mismo cuerpo, clave y If-Match exacto del reporte; 200. NFC/trim, 1..500 caracteres, sin <, >, CR/LF ni controles prohibidos por servicio vigente; tab admite contrato. No enviar sucursal, objetivo temporal ni campos técnicos.

Confirmaciones reutilizan intención protegida por actor/recurso/operación/cuerpo/clave/ETag, sin secretos en URL ni browser storage; vigencia técnica de ocho horas conforme al patrón existente. Preparar/cancelar no manda API; foco Cancelar, Escape/retorno y fallback sin JavaScript. Confirmar exige antiforgery Razor y puente CSRF a API. Recuperación explícita sólo misma intención ante resultado incierto, nunca nueva clave/cuerpo/version automáticos. Replay conserva resultado y reautoriza. 409 idempotencia no se presenta como éxito; 412/428 bloquean aprobación y exigen recarga explícita, que será nueva consulta auditada; otra intención sólo después de revisión del reporte actual.

Consulta GET genera VIEWED antes de entregar; si falla devuelve 500 RECONCILIATION_AUDIT_FAILED sin reporte. Recargar es explícito, sin polling automático ni consulta extra para decidir un botón. Respuesta de comando confirmada permite mostrar resultado sin GET añadido inadvertidamente. Ninguna ruta UI ejecuta Operations, instala herramientas ni restaura producción.

## 9. Catálogo literal propuesto y estados de pantalla

Los textos de este apartado, etiquetas y ayudas literales de §§6–8 requieren aprobación e incorporación en estados-y-mensajes/componentes/navegacion/estados-de-dominio. Los títulos e IDs técnicos históricos de auditoría no forman un catálogo nuevo.

| Situación | Texto literal | Acción |
|---|---|---|
| R01 inicial | Selecciona un año y una semana ISO para consultar los indicadores. | Consultar indicadores |
| R02 inicial | Selecciona un año y una semana ISO para consultar el panorama de Dirección. | Consultar panorama |
| Carga R01/R02 | Consultando indicadores… / Consultando panorama… | Controles ocupados |
| Base cero | No hay obligaciones en el período y alcance consultados. Los conteos y sus denominadores son cero. | Quitar filtros, conservando período |
| Carga sin filas | No hay personas en el alcance consultado. | Quitar filtros |
| Ayuda pendientes | Pendientes cuenta tareas con ejecución pendiente; no cuenta validaciones pendientes. | Sin acción |
| Ayuda concluidas | Concluida describe la ejecución; no significa validada. | Sin acción |
| Ayuda validadas | Validadas incluye cualquier resultado de la decisión vigente. Las decisiones sustituidas no se suman. | Sin acción |
| Ayuda incumplidas | Incumplidas exige una decisión vigente No cumplida. Una tarea vencida no basta. | Sin acción |
| Ayuda carga R02 | El denominador incluye pendientes sin asignación; esas tareas no generan una fila de persona. | Sin acción |
| Filtro R01/R02 inválido | Revisa el año, la semana y los filtros de la consulta. | Corregir campos |
| Inconsistencia R01/R02 | No se pudieron conciliar los datos de esta consulta. No se muestran conteos parciales. | Nueva consulta explícita |
| U01 inicial | Indica un intervalo UTC de hasta 31 días para consultar auditoría. | Consultar auditoría |
| U01 carga | Consultando auditoría… | Controles ocupados |
| U02 carga | Consultando evento… | Controles ocupados |
| U01 vacío | No hay eventos en el intervalo y alcance consultados. | Quitar filtros, conserva fechas |
| Traza incompleta | La consulta no contiene hechos de todas las etapas. No se han creado registros para completar la historia. | Consultar otro intervalo explícitamente |
| Cambios vacíos | Este evento no contiene cambios visibles en la proyección autorizada. | Sin acción |
| Campos omitidos | Campos omitidos por protección de datos: {omittedFieldCount}. | Sin acción |
| AUDIT_FILTER_INVALID | Revisa el intervalo UTC y la combinación de filtros. | Corregir campos |
| AUDIT_CURSOR_INVALID | La continuación de la consulta ya no es válida. Inicia una nueva consulta. | Nueva consulta |
| AUDIT_SCOPE_INCONSISTENT | No se pudo reconstruir esta traza de forma íntegra. No se muestran eslabones parciales. | Nueva consulta explícita |
| K01 inicial | Indica el ID de una reconciliación para consultar su resultado. | Consultar reconciliación |
| Aviso de lectura K | Cada consulta de una reconciliación queda registrada en auditoría. | Sin acción |
| Carga K | Consultando reconciliación… / Enviando solicitud… / Registrando aprobación… | Región ocupada |
| Sin diferencias en MATCHED | No se detectaron diferencias. La aceptación de Dirección sigue pendiente. | Preparar aprobación si elegible |
| Sin diferencias en APPROVED | No se detectaron diferencias. La aceptación de Dirección está registrada. | Recargar reconciliación |
| Etapa sin diferencias | La comparación aún no tiene un resultado confirmado. | Recargar reconciliación |
| DIFFERENT | Se detectaron diferencias. Esta reconciliación no puede aprobarse. | Recargar reconciliación |
| FAILED | La reconciliación falló. Este resultado no acredita una recuperación correcta. | Recargar reconciliación |
| Truncada | El reporte de diferencias está incompleto. No acredita una comparación completa y no puede aprobarse. | Sin reparación |
| Confirmación crear | Solicitar reconciliación | Cancelar / Solicitar reconciliación |
| Consecuencia crear | Se registrará la solicitud y se iniciará la captura de referencia mediante el proceso existente. Esto no confirma una recuperación ni autoriza un despliegue. | Motivo obligatorio |
| Confirmación aprobar | Aprobar reconciliación | Cancelar / Aprobar reconciliación |
| Consecuencia aprobar | Se registrará tu aceptación del resultado mostrado. Los datos reconciliados y su historia no se modificarán. | Identifica ID/estado/secuencia/motivo |
| Ayuda motivo | Explica el motivo sin incluir contraseñas, credenciales, enlaces privados ni contenido de evidencia. | Sin acción |
| MOTIVO_INVALIDO | Escribe un motivo de 1 a 500 caracteres en una sola línea, sin los signos < y >. | Error asociado |
| Crear confirmado | Solicitud de reconciliación registrada. La recuperación aún no está confirmada. | Ver resultado propio |
| Aprobar confirmado | Aceptación de la reconciliación registrada. | Ver resultado propio |
| Replay confirmado | Se recuperó la misma operación; no se registró otra solicitud o aprobación. | Ver resultado |
| Resultado incierto | No se pudo confirmar el resultado de la operación. | Recuperar resultado, sólo con intención original |
| Intención vencida | La intención venció y no se pudo confirmar su resultado. | Volver a consultar; sin reenvío |
| IDEMPOTENCY_CONFLICT | Esta intención ya se utilizó con otros datos. No se ha creado otra operación. | Sin rotación automática |
| 412 VERSION_CONFLICT | Esta reconciliación cambió. Recarga su resultado antes de preparar otra aprobación. | Recargar reconciliación |
| 428 IF_MATCH_REQUERIDO | Falta la versión de la reconciliación | Recarga la reconciliación antes de aprobarla. |
| RECONCILIACION_NO_APROBABLE | Este resultado no reúne las condiciones para aprobarse. | Recargar reconciliación |
| RECONCILIATION_AUDIT_FAILED | No se pudo registrar la consulta en auditoría. No se entrega el reporte. | Consultar nuevamente por decisión explícita |
| Error de cuerpo crear/aprobar | Revisa el motivo y prepara de nuevo la solicitud o aprobación. | Corregir, sin reenvío automático |
| 403 R/U | No tienes permiso vigente para consultar esta sección. | Sin datos/controles de sección |
| 403 K | La continuidad está disponible únicamente para Dirección con autorización vigente. | Sin reporte/acciones |
| 404 evento/traza/reconciliación | No existe o no está disponible en tu alcance | Regresar |
| Desconocido/RECONCILIACION_FALLO | No se pudo completar la operación. Conserva el identificador de correlación para solicitar revisión. | Sin detalle crudo |

CSRF_INVALID y aliases aprobados usan catálogo común: «No se pudo verificar la solicitud» / «Recarga la página antes de volver a enviarla». 401 usa flujo existente; no refleja cookies/body. correlationId visible cuando recibido y seguro, nunca secretos ni motivo en logs/capturas. «Anterior», «Siguiente», «Regresar», «Cancelar», «Recargar reconciliación», «Preparar aprobación», «Recuperar resultado» conservan semántica real.

Seis estados en cada región: normal sólo datos confirmados; foco por variables y orden DOM; deshabilitado temporal durante envío o cursor inexistente, acciones no autorizadas ocultas; error regional/resumen enfocable y campos asociados; carga aria-busy/esqueleto/botón ocupado sin éxito anticipado; vacío distingue inicial sin consulta, resultado cero, ausencia de filas, cambios ausentes y reporte no disponible. Un error nunca reutiliza un reporte anterior como actual ni sustituye datos por ceros. En consulta/confirmación cancelada no se anuncia éxito.

## 10. Composición visual y accesibilidad

Reutilizar shell v2, panel blanco/cabecera/cuerpo, tabla-filtros, campo/select/textarea/radio, _EmptyState, alertas, badge, _Icon, cursor y dialog nativo. R01/R02 no estrenan tarjeta KPI: tabla de conteos y carga. Filtros dentro de banda, etiqueta próxima/asociada, botones separados y alineados, orden DOM estable; no cambiar pendientes/supervisión ni versiones incidentalmente. Sin ordenar cabeceras ni botones ilustrativos antiguos ajenos al contrato.

CSS propio, variables oficiales para todo color/tipografía/espaciado/radio/sombra. No nuevos literales visuales ni dependencias. Poppins operativo, Georgia sólo uso de marca ya aprobado. Nuevos iconos de navegación, si necesarios, SVG propios por helper vigente y trazo de token, decorativos aria-hidden con etiqueta visible.

Tablas con caption/th/scope, scroll sólo contenedor, sin tarjetas ni truncado de IDs/valores esenciales. Móvil una columna, controles mínimos --alto-control-minimo, pares accesibles existentes, anillo completo, texto 200%, reflow 320, movimiento reducido sin quitar mensaje. Teclado completo, h1/landmarks, errores enlazados, Cancelar inicial/Escape/retorno, sin tabindex positivo; confirmar/cancelar sin JS funciona mediante POST Razor sin efecto al preparar. Navegador espera condiciones reales de respuesta, navegación, diálogo oculto y retorno antes de operar foco. Capturas/pausas arbitrarias no sincronizan pruebas.

## 11. Archivos previstos después de aprobación

| Grupo | Archivos o destinos acotados |
|---|---|
| Documentación normativa | Nueva adenda F07 consumidora en raíz; docs/design/componentes.md, estados-y-mensajes.md, estados-de-dominio.md, navegacion.md, referencia-renovada.md; este plan con texto de aprobación |
| Cliente | src/Sgol.Web/Interface/ApiClient/ApiClientContracts.cs y SgolApiClient.cs: DTO/envelopes tipados existentes, ETag/replay/minimización, métodos de rutas cerradas |
| Presentación | Nuevos Interface/Reporting/IndicatorPresentation.cs e IndicatorQuery.cs; Interface/Auditing/AuditPresentation.cs, AuditQuery.cs y AuditReturnContext.cs; Interface/Continuity/ContinuityPresentation.cs y ContinuityIntention.cs (nombres técnicos propuestos, sin IDs funcionales nuevos) |
| Páginas | Pages/Indicators/Index.cshtml(.cs), parciales de conteos/carga; Pages/Audit/Index.cshtml(.cs), Details.cshtml(.cs); Pages/Continuity/Index.cshtml(.cs), Details.cshtml(.cs) y parcial de confirmación |
| Compartidos | Pages/Shared/_Layout.cshtml, _Icon.cshtml y parciales de estados/confirmación sólo si necesarios; Interface/Navigation/NavigationItem.cs, SafeReturnDestination.cs; Modules/Identity/Contracts/Roles.cs, proyección de permisos de presentación |
| Mejora progresiva | wwwroot/js/continuity.js y scripts de carga de consultas necesarios; wwwroot/css/components.css sólo extensión mínima por variables; Program.cs únicamente registros DI de helpers nuevos si requeridos |
| Pruebas | Front020PresentationTests.cs, Front020ArchitectureTests.cs, Front020BrowserTests.cs y BrowserFixture.Front020.cs; Front020PersistenceTests.cs o parciales enfocados en suites existentes. Revisar SgolApiClient/RoleAdministration/Navigation, IndicatorApiEndpointTests, DirectionOverviewApiEndpointTests, AuditApiEndpointTests, ContinuityApiEndpointTests, AuditQueryPersistenceTests, ContinuityPersistenceTests y pruebas de indicadores en suites de ejecución; FRONT-002/005/006 e inventarios restantes afectados |
| Evidencia | IMPLEMENTATION_STATUS.md; FRONT_020_INDICADORES_AUDITORIA_CONTINUIDAD.md y FRONT_020_CAPTURAS.md; capturas sintéticas locales en .artifacts/front-020, fuera de Fuentes y de Git según patrón vigente |

No cambios previstos en fórmulas/API/dominio/persistencia/Worker/Operations/migraciones/global.json/lockfiles. Un defecto real dentro del hito se investiga; si exige alterar un contrato, se presenta decisión antes de esa parte. No se crean carpetas o archivos en Fuentes.

## 12. Incrementos internos y validación proporcional

1. Incorporar decisiones y docs/design aprobados; cliente/proyección/shell; R01/R02 y pruebas de fórmulas/ceros/autorización/no-efecto.
2. U01/U02, filtros/cursor/traza minimizada y regreso; positivos/negativos completos sin reconstruir auditoría.
3. K01..K03, consulta auditada/confirmaciones/recuperación/412 y datos sintéticos; revisión integrada de navegación y accesibilidad/capturas del único hito.

Build y test siempre secuenciales al compartir artefactos. Leer causa y logs antes de reintentar. Sin restore salvo dependencia/lock/artefacto ausente comprobado; sin suite integral ni formato global como diagnóstico por edición. Cada filtro se define por pruebas nuevas y directamente afectadas, se registra su resultado/omisiones; no presentar comandos previstos como ejecutados.

Comandos previstos con SDK fijado: build --no-restore --configuration Release; test de Sgol.UnitTests/ArchitectureTests --no-build --configuration Release con filtros Front020, cliente/sesión/navegación y contratos directamente afectados; Sgol.IntegrationTests con Front020, indicadores/dirección/auditoría/continuidad pertinentes; Sgol.FrontendBrowserTests con Front020 y FRONT-002/005/006 afectados; git diff --check. PostgreSQL real/Docker Linux AMD64, jamás SQLite. Formato --verify-no-changes --no-restore antes del futuro push autorizado, conservando gates requeridos sin rebajarlos.

| Criterio | Evidencia prevista positiva y negativa |
|---|---|
| CA/CP-029 | Dataset conocido: 8 obligaciones, 3 pendientes/5 concluidas, decisiones vigentes de los tres resultados y sustituidas que no duplican, vencida sin NO_CUMPLIDA, carga/denominador completos y personas cero. Sin sexto indicador/monto; filtros fuera de alcance ceros; anomalía falla completa; GET sin escrituras. |
| CA/CP-032 | Toda LOR-001 y pares Dirección; obligación pendiente sin asignación aumenta denominador y no fila; filtros excluyen no asignadas, jerarquía R01 distinta. Otros roles/permiso aislado/cuenta o rol vencidos rechazados; ninguna métrica prohibida. |
| CA/CP-033 | Configuración→asignación→evidencia→validación, completas y faltantes declaradas, sustituciones/actores históricos, cerca/cursor/15 min/contexto/revocation, cadena ajena converge 404 completa, secretos/campos anidados omitidos. GET sin efecto. Intento DELETE por Dirección rechazado/auditado con mecanismo existente, rollback si falla auditoría; nunca botón de borrado. |
| CA/CP-035 | Datos sintéticos MATCHED, DIFFERENT (evidencia/auditoría faltantes), FAILED/truncado/etapas/APPROVED; diferencias nunca ocultas ni reparadas. GET añade sólo VIEWED, fallo de auditoría no entrega reporte; solicitud/outbox/aceptación atómicos, rollback; clave igual replay, cuerpo distinto 409, ETag obsoleto 412, ausencia/mal formato 428, concurrencia sin doble aceptación, CSRF/anti-IDOR/rol actual. |
| UI y navegación | Escritorio/móvil HTTPS, Chromium y WebKit de consultas sin PUT/S3; exactitud de inventarios antiguos; diálogo/no JS, foco/retorno, error/vacío/carga, 200% texto/reflow/contraste/área/movimiento reducido. Capturas exclusivamente aplicación funcionando y sintética. |

Continuidad sintética se valida mediante servicios/fixtures reales de PostgreSQL y estados contractuales en entorno efímero; no se inicia recuperación operativa real o demo integral. No se declara revalidado el simulacro integral backend por ver un reporte UI. Las consultas sin efecto se comprueban contra historia/auditoría/requisitos/idempotencia/outbox; continuidad tiene la excepción VIEWED y escrituras propias expresas.

## 13. Límites, evidencia y publicación

Conservar Validación diferida de zoom nativo, lector de pantalla y dispositivos físicos (no sustituidos por emulación); WebKit Windows PUT/S3 HTTP local (ausencia de respuesta HTTP en recorrido previo); aislamiento productivo completo SeaweedFS (entorno local no lo acredita). La carrera específica sustitución de evidencia contra emisión no reensayada en FRONT-019 sigue sin nuevo PASS; este hito no la revalida con capturas ni aceptación visual. El experimento opcional isolated network SKIPPED no pasa a PASS. No son dependencias bloqueantes de consultas disponibles.

En implementación, adjuntar informe con comandos/resultados/causas y manifiesto de capturas: R01 por puestos, R02 Dirección, ceros y carga cero; auditoría general/traza/detalle y eslabón ausente; continuidad etapas, MATCHED/DIFFERENT/FAILED/APPROVED, confirmación/cancelación/412/vacío/error/carga, escritorio/móvil. Explicar pantallas, botones, puestos y estados con palabras cotidianas y solicitar revisión visual expresa. No presentar maquetas ni render aislado como funcionamiento integrado.

Compilación/prueba enfocada compatible fallida impide Implementada localmente. Limitación específica documentada no se convierte en bloqueo general ni en éxito. Sólo marcar Implementada localmente tras código/alcance/trazabilidad y checks disponibles correctos; commits pequeños coherentes en codex/front-020. La aprobación del plan no autoriza push/PR/checks remotos/heartbeat/merge/despliegue. No pedir publicar al cerrar cada incremento.

Futura publicación expresa autorizará único PR del hito, adjuntarlo a este chat, seguimiento/correcciones limitadas sin nuevas confirmaciones; formato e inventarios afectados antes del push, validar cabeza exacta. Si checks pendientes al finalizar, configurar/verificar heartbeat en este chat, sin duplicados/chats nuevos, quieto sin cambio accionable. Antes de pedir merge: PR abierto, SHA exacto verde, todos los requeridos correctos, sin conflictos/revisiones/hilos/requisitos obligatorios pendientes, límites incluidos; pausar seguimiento al solicitar aprobación expresa. Merge/despliegue requieren sus autorizaciones separadas. No commits administrativos para hashes/PR/run resolubles.

## 14. Aprobación solicitada y continuidad

La decisión requerida cubre el plan completo, en especial §4 (brechas/proyección/formato del año), §§6–8 (composición/contratos), §9 (textos y estados) y §§10–13 (archivos/validación/límites). Si una propuesta cambia, se corrige este mismo plan antes de implementar; no aprobación parcial implícita. Esperar la aprobación íntegra del responsable.

Comprobación documental de preparación: git diff --check sobre el estado registrado y git diff --no-index --check -- NUL sobre este archivo nuevo, ambos sin errores de whitespace. No se ejecutaron build ni pruebas de código: esta fase sólo prepara decisiones y no modifica implementación. Esas comprobaciones siguen previstas para después de aprobar, no están acreditadas por el plan.

Continuar preferentemente en este mismo chat. Si se abre uno nuevo, nombre exacto sugerido: **SGOL — FRONT-020 — Implementación del plan aprobado**. Mensaje listo para copiar, únicamente después de aprobar: «Aplica AGENTS.md e INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md. Apruebo íntegramente docs/traceability/FRONT_020_PLAN_DE_IMPLEMENTACION.md y ordeno implementar exclusivamente FRONT-020, incorporando primero sus decisiones consumidoras. Conserva contratos/diseño v2, Fuentes, validación enfocada, trazabilidad y límites documentados. Entrega capturas de aplicación funcionando para revisión visual. Trabajo local con commits coherentes; sin push, PR, checks remotos, merge ni despliegue hasta autorización expresa.»
