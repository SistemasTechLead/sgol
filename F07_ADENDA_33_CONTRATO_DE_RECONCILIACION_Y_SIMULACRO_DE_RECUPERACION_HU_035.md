# F07 Adenda 33 — Contrato de reconciliación y simulacro de recuperación de HU-035

## 1. Control de la propuesta

| Campo | Valor |
|---|---|
| Historia | `HU-035` — Dirección verifica recuperación con identidades e historia |
| Capacidad | `CAP-047` |
| Criterios | `CA-035`, `CP-035-P`, `CP-035-N` |
| Reglas | `RN-027`, `RN-030` |
| No funcionales | `NFR-005`, `NFR-008` |
| Permiso | `PER-CONTINUIDAD-VER` |
| Estado | PROPUESTA; sin eficacia hasta aprobación humana íntegra |
| Base técnica | `TECH-OPS-001`, PR `#54`, commit implementado `eddfdfcca0fa9b6b5a988184610bce6d90efbbb2`, pipeline `SUCCESS` run `34885345138` segundo intento, merge `5ad5192663b93594771df29fe90a086c4ea5c90b` |
| Alcance | Contrato ejecutable mínimo para capturar, restaurar, reconciliar y aceptar un simulacro sintético |

Esta adenda no modifica F00–F07 ni `Fuentes/`. Su aprobación autoriza sólo la implementación local y los gates de `HU-035`; no autoriza commit, push, PR, despliegue, uso de datos o secretos reales, ni merge.

## 2. Hechos documentados

1. `HU-035` exige comparar el conjunto anterior y posterior a una recuperación, conservar IDs, vínculos, versiones y conteos, y hacer visibles las diferencias.
2. `CP-035-N` exige que evidencia o auditoría faltante haga fallar la reconciliación; no se borra ni recrea información para obtener una falsa igualdad.
3. `RN-027` prohíbe eliminar auditoría, evidencia, versiones, asignaciones y validaciones; `RN-030` exige conservar identidades e historia.
4. `NFR-005` fija RPO menor o igual a una hora y RTO menor o igual a cuatro horas, demostrados trimestralmente. `NFR-008` exige reconstrucción desde imagen, manifiesto, secretos externos, respaldo PostgreSQL y objetos replicados.
5. `Continuity` posee respaldos, conciliación, restauración y evidencia de recuperación, sin escribir directamente tablas de otros módulos.
6. El runbook aprobado ordena restaurar PostgreSQL y objetos en destinos nuevos, conciliar IDs, vínculos, versiones, conteos, hashes, idempotencia y auditoría, y sólo después autorizar reapertura.
7. El operador técnico administra plataforma, secretos, respaldo y recuperación, pero no obtiene por ello rol funcional. Dirección funcional administra y acepta dentro de SGOL.
8. `TECH-OPS-001` ya entrega imagen, backup portable, réplica, manifiestos, restore técnico aislado, comandos, locking, no-overwrite y telemetría minimizada; excluyó expresamente reconciliación funcional, `PER-CONTINUIDAD-VER`, `CA-035` y medición integral RPO/RTO.

## 3. Ambigüedades que requieren decisión

Los documentos aprobados no fijan de forma inequívoca la superficie, identidad y ciclo de una reconciliación; el formato de los conjuntos anterior y posterior; el inventario exacto de datos; exclusiones sensibles; canonicalización; asociación con backup, réplica y restore; errores; persistencia; auditoría de solicitud, ejecución, resultado y consulta; idempotencia; límites; ni el reparto de responsabilidades y pruebas. Tampoco deciden si existe UI, cómo se excluyen hechos posteriores legítimos, cómo se miden RPO/RTO o cuándo un resultado puede aprobarse.

Las secciones siguientes son una propuesta indivisible para resolver esas ausencias. Nada de lo propuesto se considera aprobado por estar escrito aquí.

## 4. Entregables exactos

La implementación aprobada entregará únicamente:

1. Contratos y comparador puro `Continuity` para snapshots `SGOL-FUNCTIONAL-SNAPSHOT-1`, canonicalización `SGOL-CANON-1`, diferencias y reporte.
2. Persistencia expand-only de solicitud, eventos y diferencias de reconciliación, con protección append-only en PostgreSQL.
3. API funcional mínima bajo `/api/v1/continuity/reconciliations` para solicitar, consultar y aprobar una reconciliación.
4. Job `CAPTURE_RECOVERY_REFERENCE` y outbox para producir el conjunto anterior coordinado con un backup PostgreSQL portable.
5. Comandos de `Sgol.Operations` para completar la referencia técnica y reconciliar el restore aislado.
6. Manifiestos privados, inmutables y verificables de referencia, conjunto posterior y reporte.
7. Un simulacro sintético positivo y casos negativos de faltante, alteración, corrupción e inaccesibilidad.
8. Pruebas unitarias, API, arquitectura y pruebas de integración escritas; validador CI, orquestador externo y runbook específico.
9. Actualización de solución, composición Web/Worker/Operations, migración/model snapshot, locks sólo si cambian dependencias, y `docs/traceability/IMPLEMENTATION_STATUS.md` en el mismo cambio de implementación.

No se entrega UI. No se editan vistas, componentes, estilos ni documentos de diseño.

## 5. Actores, autorización y separación de funciones

1. Sólo una sesión autenticada cuya cuenta, persona, empleo y asignación de rol estén vigentes en `LOR-001`, con rol canónico `DIRECCION`, satisface `PER-CONTINUIDAD-VER`.
2. El permiso aislado, un claim, el puesto textual o cualquier otro rol no bastan. La comprobación se hace en servidor y reutiliza el patrón de autorización vigente de Dirección.
3. Dirección solicita el simulacro, consulta su resultado y lo aprueba. Puede ser la misma persona, pero cada acto conserva actor y momento.
4. El operador técnico ejecuta backup, réplica, restore y comandos de Operations con credenciales técnicas externas. Esa función no concede `PER-CONTINUIDAD-VER`, no permite aprobar y no expone datos de negocio.
5. Una consulta por ID inexistente, de otra sucursal o fuera del alcance autorizado devuelve el mismo `404 RECONCILIACION_NO_ENCONTRADA`. Falta de autenticación devuelve `401 AUTENTICACION_REQUERIDA`; actor autenticado sin permiso devuelve `403 ACCESO_DENEGADO` sin revelar recursos.
6. La aprobación sólo se permite para resultado `IGUAL`, RPO y RTO dentro de umbral y evidencia técnica completa. No existe rechazo editable ni aprobación forzada.

## 6. Superficie funcional y técnica

### 6.1 API

1. `POST /api/v1/continuity/reconciliations` exige una única `Idempotency-Key` UUID canónica. El cuerpo cerrado es `{ "reason": string }`, normalizado NFC, trim, de 1 a 500 caracteres. El servidor fija `branchId=LOR-001` y `requestedAt` con reloj UTC confiable y genera `reconciliationId` UUID. El cliente no propone el punto temporal.
2. Respuesta nueva: `201`; replay: mismo status, payload, `ETag` y `Location`. Misma clave con contenido distinto usa el protocolo común de `HU-034` y devuelve `409 IDEMPOTENCY_CONFLICT` con auditoría.
3. `GET /api/v1/continuity/reconciliations/{reconciliationId}` devuelve estado plegado, referencias técnicas minimizadas, conteos, hashes raíz, RPO/RTO, diferencias y aprobación. Usa `Cache-Control: private, no-store`, `ETag` de la última secuencia y no devuelve keys S3, URIs, nombres, payloads, secretos ni contenido de evidencia.
4. `POST /api/v1/continuity/reconciliations/{reconciliationId}/approval` exige `Idempotency-Key` y `If-Match`; cuerpo cerrado `{ "reason": string }` con la misma normalización y límite. Devuelve `200`, o replay estable; `412 VERSION_CONFLICT` si cambió la secuencia; `409 RECONCILIACION_NO_APROBABLE` si no cumple todas las condiciones.
5. No se incorpora listado, borrado, edición, reinicio, compensación, descarga de manifiestos ni endpoint de ejecución técnica.

### 6.2 Job y comandos

1. El outbox de la solicitud dispara `CAPTURE_RECOVERY_REFERENCE`. El job usa `ScheduledJobRunner`, `PreventOverlappingSlots=true` y advisory lock estable por nombre.
2. El job captura el conjunto anterior en una transacción PostgreSQL `REPEATABLE READ, READ ONLY`, obtiene `transaction_timestamp()` y un snapshot exportado. El `pg_dump` portable se ejecuta con ese mismo snapshot PostgreSQL; si no puede compartirlo, falla `REFERENCE_BACKUP_SNAPSHOT_MISMATCH` y no publica una referencia completa.
3. `Sgol.Operations complete-functional-reference --reconciliation-id <uuid> --reference <s3-uri> --backup-manifest <s3-uri> --replica-manifest <s3-uri>` verifica y asocia la referencia, backup y réplica. Sólo publica el manifiesto de referencia cuando todos son íntegros y la réplica contiene cada objeto exigido.
4. `Sgol.Operations reconcile-functional-restore --reconciliation-id <uuid> --reference-manifest <s3-uri> --restore-evidence <path>` sólo acepta la conexión `Restore__PostgreSql__ConnectionString`, diferente de la primaria, con marcador sintético y entorno aislado. Captura el conjunto posterior, verifica objetos, compara, persiste resultado y emite evidencia sanitizada.
5. Comandos repetidos con la misma identidad y hashes recuperan el resultado. Contenido distinto bajo la misma etapa falla `RECONCILIATION_IMMUTABLE_CONFLICT`; nunca sobrescribe.

## 7. Identidad, estados y terminalidad

1. `reconciliationId` es la identidad estable global. `branchId`, solicitante, `requestedAt` y motivo son inmutables. `targetRecoveryAt` queda fijado una sola vez por el evento `REFERENCE_CAPTURING`, con el timestamp del snapshot PostgreSQL exportado, y después es inmutable.
2. Los estados se derivan sólo de eventos append-only con secuencia creciente:
   - `REQUESTED`;
   - `REFERENCE_CAPTURING`;
   - `REFERENCE_READY`;
   - `RESTORE_STARTED`;
   - `RECONCILING`;
   - `MATCHED`;
   - `DIFFERENT`;
   - `FAILED`;
   - `APPROVED`.
3. Flujo válido: `REQUESTED -> REFERENCE_CAPTURING -> REFERENCE_READY -> RESTORE_STARTED -> RECONCILING -> MATCHED -> APPROVED`.
4. Desde cualquier etapa técnica puede llegarse una sola vez a `FAILED`. Desde `RECONCILING` puede llegarse a `DIFFERENT`. `DIFFERENT`, `FAILED` y `APPROVED` son terminales. `MATCHED` no es terminal hasta aprobación, pero no permite otra ejecución.
5. `MATCHED` significa igualdad funcional y evidencia completa; no implica aún aceptación humana. `APPROVED` registra esa aceptación y no altera datos reconciliados.
6. Una cancelación deja `FAILED` con `OPERATION_CANCELLED`; no existe estado silencioso, reinicio ni reutilización del mismo ID.

## 8. Persistencia append-only

La migración crea exclusivamente:

1. `recovery_reconciliation`: cabecera inmutable con ID, sucursal, solicitante, motivo y `requested_at`; el punto temporal inmutable se conserva en el evento de captura.
2. `recovery_reconciliation_event`: eventos append-only con ID, reconciliación, secuencia, tipo, actor funcional opcional, actor técnico de clase fija opcional, instante UTC, correlation ID, códigos, hashes y métricas permitidas.
3. `recovery_reconciliation_difference`: diferencias append-only con reconciliación, ordinal, grupo, tipo de recurso, clave estable, campo, clase de diferencia y hashes esperado/actual opcionales.

Restricciones únicas impiden dos secuencias iguales, dos eventos terminales y dos diferencias con el mismo ordinal. Triggers PostgreSQL rechazan `UPDATE` y `DELETE` con SQLSTATE `55000` en las tres tablas. No hay backfill, purga, TTL ni cascada destructiva. La cabecera, el evento de solicitud, el outbox y su `audit_event` se insertan en una transacción. Cada inicio, resultado y aprobación persiste evento y auditoría en la misma transacción.

## 9. Conjuntos `SGOL-FUNCTIONAL-SNAPSHOT-1`

### 9.1 Universo relacional exacto

El contrato v1 incluye todas las columnas persistidas en el baseline de migración `20260912213000_AddPortableDataProtectionKeyRing`, salvo las exclusiones expresas de 9.3, para estas 39 tablas:

- identidades y organización: `branch`, `person`, `employment_version`, `availability_day_version`, `app_user`, `identity_credential`, `role_assignment_version`, `direction_bootstrap`;
- configuración y versiones: `configuration_release`, `calendar_day_version`, `task_definition`, `task_definition_version`, `eligibility_policy_version`, `activation_rule_version`, `evidence_requirement_catalog`, `evidence_policy_version`, `evidence_requirement_version`, `validation_policy_version`;
- planificación, generación, asignación y ejecución: `week_period`, `work_plan`, `plan_version`, `plan_version_obligation`, `generation_request`, `work_obligation`, `eligibility_evaluation`, `eligibility_candidate`, `assignment_version`, `execution_result`, `validation_requirement`, `validation_decision_version`;
- evidencia: `file_object`, `evidence_item`, `evidence_version`, `evidence_review_snapshot`;
- historia y soporte funcional: `audit_event`, `idempotency_record`, `internal_notice`, `outbox_event`, `scheduled_job_run`.

La implementación usa proyecciones allowlist por tabla; no descubre columnas dinámicamente. Una nueva migración o columna no entra en v1 sin actualizar explícitamente el contrato y sus pruebas.

### 9.2 Identidades, vínculos, versiones, conteos, evidencia e historia

1. Identidades compara presencia y clave primaria de cada fila; `person.stable_code`, cuenta, credencial y sucursal se representan por hashes de campo, no por texto en el reporte.
2. Vínculos compara todas las FK y referencias lógicas persistidas, incluidos `supersedes_id`, IDs de versión/política/release, persona/usuario/sucursal, obligación/asignación/plan, evidencia/archivo/revisión/validación, generación, idempotencia, outbox y auditoría.
3. Versiones compara ID, número, estado, row version, vigencias, sustitución y los restantes campos allowlist de toda entidad versionada.
4. Conteos compara total por tabla y por estado canónico persistido. Los conteos son evidencia auxiliar; nunca compensan una fila diferente.
5. Evidencia compara metadatos persistidos, SHA-256, tamaño, bucket class, estado, enlaces, versiones, payload estructurado canónico y snapshots de revisión. Para cada `file_object` referenciado, recalcula bytes del objeto en el bucket aislado y exige tamaño/SHA-256 exactos.
6. Auditoría compara cada `audit_event` hasta el corte, incluidos ID, actor, acción, recurso, sucursal, instante, correlation/request, before/after, motivo, outcome y `source_ip_hash`, todos mediante hashes de campo. Ausencia o alteración tiene clase específica.
7. Idempotencia, outbox, avisos y corridas de job existentes al corte se comparan como historia. Los registros de control creados por la propia reconciliación después del corte se excluyen según 9.3.

### 9.3 Exclusiones justificadas

1. Se excluyen las tres tablas nuevas de reconciliación y sus eventos posteriores al corte para que observar el proceso no altere el objeto comparado.
2. En `audit_event`, `idempotency_record`, `outbox_event` y `scheduled_job_run` sólo se incluyen filas cuyo instante de creación/ocurrencia/inicio sea menor o igual a `referenceCapturedAt`. Una fila técnica posterior legítima no se presenta como diferencia.
3. No se serializan ni hashean `identity_credential.password_hash`, `app_user.security_stamp` ni `data_protection_key.xml`. Se verifica estructura, vínculo, no-vacío y smoke sintético de autenticación/cookie; esta exclusión evita convertir material de autenticación en manifiesto.
4. `data_protection_key`, `__EFMigrationsHistory`, extensiones, constraints e índices se validan técnicamente por presencia, migración esperada, integridad y smoke, no como filas funcionales.
5. No se incluyen URLs firmadas, connection strings, secretos, credenciales, contenido binario, stderr de herramientas ni valores de variables de entorno.
6. No existen exclusiones por “dato incómodo”, orden físico, diferencia de conteo o error. Toda exclusión está cerrada en esta sección.

## 10. Canonicalización, hashes y formatos

1. Cada snapshot privado contiene `schemaVersion=1`, `kind`, `reconciliationId`, `branchId`, `referenceCapturedAt`, revisión, digest OCI, migración, identidad de artefactos, tablas, conteos y `rootSha256`.
2. Cada fila se representa como `{ table, stableKey, fields }`; `fields` contiene nombre y SHA-256 del valor canónico. El valor en claro no viaja al reporte ni a telemetría.
3. `SGOL-CANON-1` usa UTF-8 sin BOM; propiedades de objeto por orden ordinal; tablas por el orden de 9.1; filas por clave estable ordinal; arrays de conjunto ordenados y arrays semánticos preservados; strings NFC; GUID lowercase `D`; booleanos `true/false`; enteros base diez sin ceros; `null` literal; fechas UTC RFC3339 con seis dígitos fraccionarios; `DateOnly` `yyyy-MM-dd`; JSON recursivamente canónico sin whitespace ni números no finitos.
4. `stableKey` es la PK en orden de columnas, con GUID lowercase y separador `|`. Una PK textual se representa por su SHA-256 para no revelar PII.
5. `rowSha256` se calcula sobre tabla, clave y fields canónicos; `tableSha256` sobre filas ordenadas; `rootSha256` sobre la lista ordenada de tablas y sus conteos/hashes.
6. Manifiestos usan el canonicalizador JCS existente de Operations cuando sea compatible; `SGOL-CANON-1` gobierna valores de dominio. Cualquier byte no canónico o hash distinto falla cerrado.
7. Keys inmutables: `continuity/v1/<reconciliationId>/reference.snapshot.json`, `reference.manifest.json`, `actual.snapshot.json` y `report.manifest.json`. Se crean condicionalmente, se releen y rehashean; nunca se sobrescriben.

## 11. Igualdad, diferencias y fallos observables

1. Existe igualdad funcional completa sólo si contrato, sucursal, corte, revisión/migración compatibles, inventario de tablas, conteos, claves, fields, root hash, objetos de evidencia y auditoría coinciden, y la evidencia técnica es íntegra.
2. Clases de diferencia: `IDENTITY_MISSING`, `IDENTITY_ADDITIONAL`, `LINK_MISSING`, `LINK_CHANGED`, `VERSION_CHANGED`, `COUNT_CHANGED`, `VALUE_CHANGED`, `EVIDENCE_MISSING`, `EVIDENCE_CORRUPT`, `EVIDENCE_INACCESSIBLE`, `AUDIT_MISSING`, `AUDIT_ALTERED` y `UNEXPECTED_POST_RECOVERY_RECORD`.
3. Códigos de fallo operativo: `REFERENCE_MISSING`, `REFERENCE_CORRUPT`, `REFERENCE_VERSION_UNSUPPORTED`, `REFERENCE_BACKUP_SNAPSHOT_MISMATCH`, `BACKUP_MANIFEST_INVALID`, `REPLICA_MANIFEST_INVALID`, `RESTORE_EVIDENCE_INVALID`, `ACTUAL_CAPTURE_FAILED`, `CARDINALITY_LIMIT_EXCEEDED`, `RECONCILIATION_IMMUTABLE_CONFLICT`, `RPO_EXCEEDED`, `RTO_EXCEEDED`, `POSTGRES_CONCURRENCY_EXHAUSTED` y `UNEXPECTED_RECONCILIATION_FAILURE`.
4. Una diferencia bien formada termina `DIFFERENT`; corrupción/inaccesibilidad que impida comparación completa, límite excedido o error técnico termina `FAILED`. Ambos resultados son fallo de reconciliación y no pueden aprobarse.
5. El reporte contiene todos los detalles hasta 100 000 diferencias. Si se supera el límite, conserva resumen por clase, marca `truncated=true`, termina `FAILED/CARDINALITY_LIMIT_EXCEEDED` y nunca se presenta como comparación completa.
6. Límites v1: máximo 1 000 000 filas por tabla, 512 MiB por snapshot canónico, 100 000 diferencias y 10 GiB por objeto. Se procesa en streaming; exceder un límite falla cerrado.

## 12. Registros posteriores al punto de recuperación

1. El conjunto anterior es inmutable y queda fijado por el snapshot PostgreSQL exportado. Actividad legítima del sistema fuente posterior no modifica esa referencia ni se consulta durante la reconciliación.
2. El destino restaurado permanece aislado, descartable y sin tráfico funcional hasta aprobación. Cualquier fila funcional adicional no presente en la referencia es diferencia; no puede justificarse como tráfico legítimo del restore.
3. Sólo los eventos de control de HU-035 y filas técnicas posteriores excluidos expresamente en 9.3 pueden existir sin diferencia.
4. Fusionar hechos creados después del punto, reabrir producción, compensar o capturar escrituras divergentes queda fuera de `HU-035` y requiere decisión humana posterior.

## 13. Asociación de artefactos y evidencia mínima

1. `reference.manifest.json` liga por URI privada y SHA-256: snapshot anterior, backup manifest, replica manifest, revisión Git, digest OCI, migración, sucursal, `reconciliationId`, `targetRecoveryAt` y `referenceCapturedAt`.
2. El backup fue creado con el snapshot PostgreSQL exportado de la referencia. La réplica elegida es `COMPLETE`, no posterior a `targetRecoveryAt`, y acredita todos los objetos referenciados.
3. La evidencia de restore técnico de `TECH-OPS-001` debe acreditar destino nuevo/vacío, rechazo de primario, backup/digest/migración, inicio, fin y resultado. Su hash queda ligado al reporte.
4. Evidencia mínima para `MATCHED`: cuatro manifiestos válidos, root hashes iguales, cero diferencias, todos los objetos verificados, auditoría íntegra, smoke de key ring/autenticación/autorización, RPO/RTO dentro de umbral y ausencia de secretos en salida.
5. El reporte es reproducible desde los dos snapshots y artefactos ligados. Persiste resumen en PostgreSQL y detalle privado append-only; no depende de logs efímeros.

## 14. RPO, RTO y reloj

1. `requestedAt` se toma del `IClock` del servidor Web respaldado por UTC. `targetRecoveryAt` y `referenceCapturedAt` son el mismo instante del snapshot de datos, tomado una sola vez de `transaction_timestamp()` PostgreSQL al iniciar la captura coordinada. No se aceptan timestamps del cliente.
2. `databaseRpo = targetRecoveryAt - backupSnapshotAt`; el backup coordinado debe usar exactamente el snapshot exportado y por ello el valor esperado es cero. `objectRpo = targetRecoveryAt - replicaManifest.scheduledFor`. Valores negativos, zona no UTC, snapshot de backup distinto o manifiesto posterior al objetivo son inconsistentes y fallan.
3. `observedRpo = max(databaseRpo, objectRpo)`. Pasa sólo si es menor o igual a 3 600 segundos.
4. `recoveryStartedAt` es el instante UTC emitido al iniciar el restore técnico; `reconciliationCompletedAt` lo emite Operations al persistir el resultado. `observedRto = reconciliationCompletedAt - recoveryStartedAt`; se contrasta con duración monotónica del proceso para detectar reloj regresivo.
5. RTO pasa sólo si es menor o igual a 14 400 segundos. Los valores exactos, fuentes y hashes de evidencia se guardan; superar un umbral termina `FAILED` aunque los conjuntos sean iguales.

## 15. Idempotencia, reintentos, concurrencia y no-efecto

1. Solicitud y aprobación reutilizan `HU-034`: autorización antes de lookup, scope por actor/operación/recurso, `IDEM-CANON-1`, PostgreSQL como autoridad y replay estable.
2. Job y comandos tienen unicidad PostgreSQL por `(reconciliation_id, stage)` y advisory lock estable `HU_035_RECOVERY_RECONCILIATION`. Sólo timeout, desconexión, throttling, 5xx y SQLSTATE de concurrencia aprobado admiten hasta tres intentos con backoff acotado y jitter.
3. Faltante, corrupción, autorización, hash, versión, 4xx no transitorio o diferencia funcional no se reintentan como éxito.
4. Un lock ocupado devuelve resultado observable `LOCK_BUSY`; no inicia una segunda ejecución. Agotar concurrencia termina `FAILED/POSTGRES_CONCURRENCY_EXHAUSTED`.
5. Antes de persistir resultado, la captura posterior y comparación son read-only. Al terminar sólo se añaden manifiestos, cabecera/eventos/diferencias de continuidad, idempotencia, outbox y auditoría propios; no se modifica ninguna fila reconciliada.
6. Un fallo conserva evidencia `FAILED`, revierte cualquier transacción parcial y nunca borra, compensa, sobrescribe, repara ni fabrica filas u objetos.

## 16. Auditoría y consulta

1. Acciones exactas: `RECOVERY_RECONCILIATION_REQUESTED`, `RECOVERY_REFERENCE_CAPTURE_STARTED`, `RECOVERY_REFERENCE_READY`, `RECOVERY_RESTORE_STARTED`, `RECOVERY_RECONCILIATION_COMPLETED`, `RECOVERY_RECONCILIATION_FAILED`, `RECOVERY_RECONCILIATION_VIEWED` y `RECOVERY_RECONCILIATION_APPROVED`.
2. Auditoría contiene sólo ID, sucursal, etapa, resultado/código, conteos, root hashes, RPO/RTO y referencias digest; no contiene motivos en logs, keys, URI, nombres, objetos, valores comparados ni evidencia.
3. Solicitud, inicio, resultado y aprobación son transaccionales con su evento. Cada consulta autorizada inserta `RECOVERY_RECONCILIATION_VIEWED` antes de responder; si falla esa auditoría, devuelve `500 RECONCILIATION_AUDIT_FAILED` y no entrega el reporte.
4. Consultar no modifica la cabecera, el resultado, diferencias, datos de negocio, ETag ni manifiestos. Sólo añade el evento de auditoría exigido.

## 17. Telemetría, privacidad y retención

1. Logs permitidos: timestamp UTC, servicio, operación, etapa, intento, resultado, errorClass, duración, revisión/digest y correlation ID técnico.
2. Métricas: corridas, duración, resultado, diferencias totales, RPO/RTO y antigüedad de referencia. Labels cerrados: `operation`, `stage`, `result`, `errorClass`; nunca ID, actor, recurso, key, bucket, hash, URI o código de persona.
3. Manifiestos y reportes privados conservan sólo claves estables y hashes de campos; el API minimiza aún más la salida. No se expone contenido de evidencia, payload estructurado, before/after, secretos ni material de autenticación.
4. Cabeceras, eventos, diferencias, snapshots y reportes se retienen sin purga en el MVP. No se implementa lifecycle destructivo; crecimiento posterior requiere contrato y autorización separados.

## 18. Simulacro sintético aislado

1. El responsable de SGOL autoriza el simulacro; Dirección lo solicita y aprueba funcionalmente; el operador técnico lo ejecuta conforme al runbook.
2. Usa exclusivamente personas, cuentas, historia laboral/roles, configuraciones versionadas, obligaciones, asignaciones, planes, evidencia de archivo y estructurada, sustituciones, validaciones, idempotencia, outbox y auditoría sintéticos.
3. Camino feliz: solicitud, referencia coordinada, backup, réplica, restore nuevo, captura posterior, igualdad, smoke, RPO/RTO y aprobación.
4. Casos negativos separados: identidad ausente/adicional, vínculo alterado, versión/conteo distinto, evidencia faltante, objeto corrupto/inaccesible, auditoría faltante/alterada y manifiesto corrupto.
5. Cada negativo demuestra estado terminal no aprobable, diferencia/código visible, cero compensación, cero fabricación y origen/resto sin modificación funcional.
6. Recursos de prueba son efímeros y descartables; la evidencia sanitizada se conserva fuera de `Fuentes/`. No se restaura sobre la única copia ni sobre producción.

## 19. Pruebas y gates

### 19.1 Dentro de la sesión

- unitarias del contrato, canonicalización, orden, hashes, igualdad, diferencias, límites, RPO/RTO e idempotencia pura;
- API positiva/negativa, permiso, rol vigente, anti-IDOR, replay, If-Match, consulta auditada y no-efecto;
- arquitectura para módulo, ausencia de SDK proveedor/datos sensibles, telemetría de baja cardinalidad, no UI y protección de fronteras;
- compilación y descubrimiento de integración PostgreSQL sin ejecutarla;
- validadores estáticos de manifiestos, comandos, restore aislado, scripts, secretos, migración expand-only, BOM y `Down()` bloqueado;
- gates finales .NET, vulnerabilidades, espejo/protección de `Fuentes/`, rutas de diseño y `git diff --check`.

### 19.2 A cargo del desarrollador fuera de la sesión

- PostgreSQL real/Testcontainers para snapshot exportado compartido con `pg_dump`, tablas append-only, auditoría transaccional, locking, concurrencia, replay y no-efecto;
- dos almacenamientos S3-compatible para referencia, réplica, objetos, no-overwrite, faltante, corrupción e inaccesibilidad;
- restore integral en destino nuevo con imagen `linux/amd64`, backup, objetos, key ring, smoke y comparación;
- simulacro sintético positivo y negativos, evidencia sanitizada y medición RPO/RTO;
- pipeline del SHA exacto con el gate integral aprobado.

Estos gates no se presentan como aprobados hasta ejecutarse. Las pruebas externas se solicitarán al desarrollador en un solo mensaje.

## 20. Archivos previstos después de la aprobación

- `src/Modules/Continuity/Sgol.Continuity.csproj` y `src/Modules/Continuity/Contracts/*.cs`;
- `src/Sgol.Web/Infrastructure/Persistence/Continuity/*.cs`, `SgolDbContext.cs`, una migración `AddRecoveryReconciliation` y `SgolDbContextModelSnapshot.cs`;
- `src/Sgol.Web/Interface/Endpoints/ContinuityApiEndpoints.cs`, composición de `Program.cs` y persistencia;
- `src/Sgol.Worker/Program.cs` y composición del job/outbox, sólo si la registración no puede quedar en extensiones existentes;
- `src/Sgol.Operations/FunctionalRecovery*.cs`, `OperationManifests.cs`, `PostgreSqlPortableBackup.cs`, `OperationsJobs.cs` y `Program.cs`;
- `tests/Sgol.UnitTests/Continuity*.cs`, `tests/Sgol.ArchitectureTests/ContinuityArchitectureTests.cs`, `tests/Sgol.IntegrationTests/ContinuityPersistenceTests.cs` y pruebas externas en `tests/Sgol.OperationsIntegrationTests/`;
- `scripts/ci/validate-hu-035.ps1`, `scripts/operations/verify-hu-035-external.ps1` y `docs/operations/functional-recovery-reconciliation.md`;
- `SGOL.slnx`, proyectos/lockfiles estrictamente necesarios y `docs/traceability/IMPLEMENTATION_STATUS.md`.

No se prevén archivos de UI ni cambios en `docs/design`. Si durante la implementación un archivo no listado fuera imprescindible, se detendrá esa parte y se solicitará decisión antes de añadirlo.

## 21. Gates finales previstos

```powershell
rtk dotnet restore --locked-mode
rtk dotnet build --no-restore --configuration Release
rtk dotnet test --no-build --configuration Release
rtk dotnet format --no-restore --verify-no-changes
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ci/Assert-NoVulnerablePackages.ps1
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ci/verify-fuentes-mirror.ps1
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ci/verify-fuentes-protection.ps1
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ci/validate-tech-ops.ps1
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ci/validate-hu-035.ps1
rtk git diff --check
```

Durante implementación se ejecutarán sólo pruebas enfocadas nuevas. La suite completa y formato se ejecutarán una sola vez al final. PostgreSQL real, Docker, S3-compatible y simulacro integral se ejecutarán fuera de la sesión por el desarrollador.

## 22. Exclusiones

- plataforma general de disaster recovery, producción, cloud real, staging real, secretos o datos reales;
- merge de hechos posteriores, reapertura automática, compensación, reparación, borrado, overwrite o fabricación;
- restore sobre origen, producción o única copia; `--clean`, drop, rollback o migración destructiva;
- edición manual de snapshots, diferencias, resultado o aprobación forzada;
- reconciliación parcial presentada como éxito;
- UI, exportación pública, listado general, endpoint técnico o descarga de manifiestos;
- Kubernetes, Redis, broker, microservicio o SDK de proveedor en dominio;
- firma/publicación de imagen, creación/modificación de infraestructura y purga/lifecycle;
- cambios funcionales a historias anteriores, `CV-05` y cualquier tarea posterior.

## 23. Riesgos y límites

1. Coordinar `pg_dump` y proyección funcional con un snapshot PostgreSQL exportado aumenta duración y presión temporal; el límite y el fallo cerrado evitan una referencia incoherente.
2. Los hashes de campos detectan diferencia sin revelar valores, pero el diagnóstico del valor exacto requerirá inspección operativa autorizada fuera del reporte.
3. El RPO portable se demuestra con un backup bajo demanda coordinado; esto no sustituye comprobar PITR administrado antes de producción.
4. El destino restaurado contiene eventos técnicos posteriores excluidos explícitamente; ampliar exclusiones reduciría cobertura y exige nueva aprobación.
5. Sin purga, manifiestos y diferencias crecerán; se acepta para preservar historia durante el MVP.
6. El simulacro local prueba portabilidad e integridad sintética, no latencia, red ni controles del proveedor real.

## 24. Eficacia, trazabilidad y cierre

1. La aprobación íntegra de esta adenda autoriza implementación local y gates de `HU-035`, no commit, push, PR ni merge.
2. La implementación actualizará `docs/traceability/IMPLEMENTATION_STATUS.md` en el mismo commit como propuesta, conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md`.
3. Antes de cada autorización Git se presentarán rama/base, status, rutas exactas, diff/stat, gates/límites y confirmación de rutas protegidas.
4. `HU-035` sólo queda Terminada después de contrato aprobado, implementación y trazabilidad en el mismo commit, simulacro integral, `CA-035`/CP satisfechos, pipeline verde del SHA exacto, aprobación humana, merge, ascendencia en `origin/master`, `Fuentes/` protegida y cero defectos bloqueantes.
5. Esta adenda no inicia ni habilita automáticamente `CV-05` u otra tarea.

## 25. Decisiones solicitadas

La aprobación íntegra decide como una unidad:

1. entregables, ausencia de UI y combinación API + Worker + Operations;
2. actor Dirección, aplicación exacta de `PER-CONTINUIDAD-VER`, anti-IDOR y separación del operador técnico;
3. identidad, estados, terminalidad y aprobación explícita;
4. tres tablas append-only, auditoría transaccional y retención sin purga;
5. universo exacto de 39 tablas, exclusiones sensibles y controles técnicos separados;
6. `SGOL-FUNCTIONAL-SNAPSHOT-1`, `SGOL-CANON-1`, orden, hashes y límites;
7. coordinación por snapshot PostgreSQL exportado y asociación de backup, réplica, restore y reporte;
8. igualdad completa, clases de diferencia, errores y fallo cerrado;
9. tratamiento de hechos posteriores, no-efecto y prohibición de reparación/fabricación;
10. medición de RPO/RTO, relojes y umbrales;
11. idempotencia, retries, locking, telemetría y privacidad;
12. simulacro sintético, reparto de gates, archivos previstos, exclusiones y riesgos.

No existe aprobación parcial implícita. Si una decisión no es aceptable, debe corregirse esta propuesta antes de escribir código funcional.

La pregunta de aprobación es: **¿se aprueba íntegramente `F07_ADENDA_33_CONTRATO_DE_RECONCILIACION_Y_SIMULACRO_DE_RECUPERACION_HU_035.md`, sin cambios, para autorizar la implementación local de `HU-035` bajo este contrato?**
