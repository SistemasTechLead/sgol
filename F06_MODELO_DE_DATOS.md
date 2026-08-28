# F06 — Modelo de datos del MVP de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación literal del responsable | `Apruebo los seis entregables de Fase 06 y autorizo su incorporación a Fuentes` |
| Motor | PostgreSQL administrado |
| Alcance | Datos necesarios para HU-001 a HU-035 y ocho tareas MVP |
| Fuentes | F05 especificación, estados, permisos, trazabilidad y criterios; ADR-003, ADR-006, ADR-009 y ADR-014 |
| Exclusiones | Migración legacy, excepciones formales, cierre/reapertura, eventos/condiciones internas, nómina, incentivos e integraciones |

## 2. Principios

1. La base relacional es la autoridad de identidad, relaciones, estados, vigencias, versiones, idempotencia y auditoría.
2. Los binarios viven en almacenamiento de objetos; PostgreSQL conserva metadatos, hash, estado de escaneo y vínculos.
3. Un cambio histórico crea una fila nueva o un evento; no sobrescribe ni elimina el hecho anterior.
4. Todas las claves y guardas críticas se refuerzan con restricciones de base, no sólo con código.
5. Las relaciones funcionales usan identificadores internos UUID v7 y códigos estables (`LOR-001`, `TAR-####`, código de persona).
6. Instantes en `timestamptz`; fechas locales en `date`; zona operativa registrada explícitamente.
7. Estado, resultado y condición calculada permanecen en campos/dominos separados.
8. Todo objeto mutable incorpora `row_version` para concurrencia optimista.
9. No existe borrado en cascada desde maestros hacia hechos operativos.

## 3. Dominios PostgreSQL

| Tipo lógico | Representación |
|---|---|
| ID público | `uuid`, generado como UUID v7 en aplicación |
| Código estable | `varchar` con formato validado y unicidad |
| Instante | `timestamptz`, normalizado a UTC |
| Fecha operativa | `date`, interpretada con `America/Mexico_City` |
| Dinero | No existe en el MVP |
| Estado/resultado | `varchar` con `check`; no enum nativo para permitir migración compatible |
| Hash | `char(64)` SHA-256 hexadecimal minúsculo |
| Datos variables acotados | `jsonb` sólo para payload de tarea, auditoría y explicaciones, con versión de esquema |
| Concurrencia | `bigint row_version`, incrementado en actualización válida |

## 4. Vista conceptual

```text
Branch ─┬─ Person ─ EmploymentVersion
        ├─ UserAccount ─ RoleAssignmentVersion
        ├─ AvailabilityDayVersion
        ├─ CalendarDayVersion
        └─ WeekPeriod ─ WorkPlan ─ PlanVersion

TaskDefinition ─ TaskDefinitionVersion
       ├─ ActivationRuleVersion
       ├─ EligibilityPolicyVersion
       ├─ EvidenceRequirementVersion
       └─ ValidationPolicyVersion

GenerationRequest ─ WorkObligation ─ AssignmentVersion
                              ├─ EvidenceItem ─ EvidenceVersion ─ FileObject
                              ├─ ValidationDecisionVersion
                              ├─ InternalNotice
                              └─ AuditEvent

IdempotencyRecord / OutboxEvent / ScheduledJobRun / RecoveryRun
```

## 5. Catálogo de tablas

### 5.1 Organización e identidad

| Tabla | Propósito | Campos esenciales | Restricciones principales |
|---|---|---|---|
| `branch` | Sucursal canónica. | `id`, `code`, `name`, `status`, `timezone` | Sólo semilla `LOR-001`; `code` único; `timezone='America/Mexico_City'` en MVP. |
| `person` | Identidad laboral estable. | `id`, `stable_code`, `display_name`, `created_at` | `stable_code` único, no vacío. |
| `employment_version` | Vigencia, puesto, turno y sucursal versionados. | `id`, `person_id`, `branch_id`, `status`, `position_text`, `shift_text`, `valid_from`, `valid_to`, `supersedes_id`, `row_version` | Máximo una versión vigente por persona/sucursal; intervalos no solapados. Puesto/turno no referencian rol. |
| `app_user` | Extensión funcional de ASP.NET Core Identity. | `id`, `person_id`, `status`, `must_change_password`, `mfa_enrolled_at`, `security_stamp` | Una cuenta activa por persona en MVP; nunca compartida. Credenciales permanecen en tablas Identity. |
| `role_assignment_version` | Rol canónico vigente/histórico. | `id`, `user_id`, `branch_id`, `role_code`, `status`, `valid_from`, `valid_to`, `supersedes_id` | Índice único parcial: un `ACTIVO` por usuario/sucursal; roles permitidos: cuatro canónicos. |
| `availability_day_version` | Disponibilidad binaria por fecha. | `id`, `person_id`, `branch_id`, `local_date`, `is_available`, `status`, `supersedes_id`, `changed_by` | Una versión vigente por persona/fecha; valor booleano; cambio por Dirección. |

La jerarquía se deriva del `role_code` vigente mediante orden fijo, no de `position_text`.

### 5.2 Calendario y configuración

| Tabla | Propósito | Campos esenciales | Restricciones |
|---|---|---|---|
| `configuration_release` | Agrupa una publicación de configuración. | `id`, `version_no`, `status`, `effective_from`, `reason`, `published_by`, `published_at`, `supersedes_id` | Una versión `VIGENTE`; sólo Dirección publica. |
| `calendar_day_version` | Laborable, festivo o cierre extraordinario. | `id`, `branch_id`, `local_date`, `day_type`, `is_working_day`, `release_id`, `status`, `supersedes_id` | Una versión vigente por sucursal/fecha; zona de sucursal. |
| `task_definition` | Identidad estable de una TAR. | `id`, `task_code`, `name` | Sólo ocho códigos aprobados; bloqueo de aplicación y check/tabla semilla. |
| `task_definition_version` | Snapshot operativo de definición. | `id`, `task_definition_id`, `version_no`, `status`, `effective_from`, `schema_version`, `task_payload`, `release_id`, `supersedes_id` | Una `VIGENTE` o `INACTIVA_NUEVAS`; obligaciones referencian una versión exacta. |
| `activation_rule_version` | Alta manual o recurrencia. | `id`, `task_definition_version_id`, `mode`, `schedule`, `origin_key_schema`, `status`, `supersedes_id` | `mode` sólo `MANUAL`/`RECURRENTE`; horario local explícito. |
| `eligibility_policy_version` | Rol, disponibilidad y turno opcional. | `id`, `task_definition_version_id`, `required_role`, `requires_availability`, `required_shift`, `status`, `supersedes_id` | Rol exacto canónico; turno nulo salvo restricción aprobada. |
| `evidence_requirement_version` | Requisito de evidencia por TAR. | `id`, `task_definition_version_id`, `requirement_code`, `kind`, `conditions`, `is_required`, `status` | Todos los requisitos vigentes aplicables son obligatorios. |
| `validation_policy_version` | Necesidad y autoridad de validación. | `id`, `task_definition_version_id`, `is_required`, `validator_relation`, `allowed_results`, `status` | Ocho TAR requieren `SUPERIOR_INMEDIATO`; tres resultados permitidos. |

Los payloads específicos de las ocho tareas se validan con un `schema_version` y esquemas de aplicación versionados. No se crea un motor de formularios o reglas general para las 186 tareas opcionales.

### 5.3 Período, generación, asignación y plan

| Tabla | Propósito | Campos esenciales | Restricciones |
|---|---|---|---|
| `week_period` | Semana ISO única. | `id`, `branch_id`, `iso_year`, `iso_week`, `starts_on`, `ends_on`, `derived_status` | Único `(branch_id, iso_year, iso_week)`; fechas lunes-domingo. |
| `generation_request` | Evaluación idempotente manual/recurrente. | `id`, `idempotency_key`, `request_hash`, `rule_version_id`, `branch_id`, `period_id`, `origin_type`, `origin_reference`, `result`, `requested_by`, `requested_at`, `obligation_id`, `error_code` | `idempotency_key` única; misma clave con hash distinto es conflicto. |
| `work_obligation` | Identidad estable del trabajo. | `id`, `task_definition_version_id`, `branch_id`, `period_id`, `generation_request_id`, `origin_reference`, `input_payload`, `due_at`, `execution_status`, `concluded_at`, `concluded_by`, `row_version` | Una por solicitud aceptada; estado sólo `PENDIENTE/CONCLUIDA`; no delete. |
| `assignment_version` | Responsable vigente e historial. | `id`, `obligation_id`, `person_id`, `status`, `assignment_type`, `explanation`, `reason`, `assigned_by`, `assigned_at`, `supersedes_id` | Única `VIGENTE` por obligación; responsable elegible; corrección con motivo. |
| `eligibility_evaluation` | Snapshot explicable de candidatos. | `id`, `obligation_id`, `evaluated_at`, `policy_version_id`, `input_snapshot`, `winner_person_id` | Inmutable; una evaluación puede tener múltiples candidatos. |
| `eligibility_candidate` | Inclusión/exclusión y ranking. | `evaluation_id`, `person_id`, `is_eligible`, `reasons`, `active_load`, `last_auto_assignment_at`, `stable_code`, `rank` | Razones completas; ranking determinista. |
| `work_plan` | Plan único por sucursal/semana. | `id`, `branch_id`, `period_id`, `status`, `row_version` | Único `(branch_id, period_id)`; `BORRADOR/PUBLICADO`. |
| `plan_version` | Publicación incremental. | `id`, `plan_id`, `version_no`, `status`, `scope_role`, `published_by`, `published_at`, `supersedes_id` | Una vigente por alcance publicado; número creciente. |
| `plan_version_obligation` | Contenido de cada publicación. | `plan_version_id`, `obligation_id`, `assignment_version_id` | Snapshot; no mueve obligación a otro plan. |

`VENCIDA`, `PROGRAMADA` y `DISPONIBLE` se calculan; no son valores de `execution_status`.

### 5.4 Evidencia y archivos

| Tabla | Propósito | Campos esenciales | Restricciones |
|---|---|---|---|
| `file_object` | Ciclo técnico de un binario. | `id`, `bucket_class`, `object_key`, `original_name`, `declared_media_type`, `detected_media_type`, `size_bytes`, `sha256`, `scan_status`, `scan_engine`, `scanned_at`, `uploaded_by`, `created_at`, `replicated_at` | 1–15 MiB; tipos finales JPEG/PNG/PDF; clave única; objeto privado; hash único no implica deduplicación funcional. |
| `evidence_item` | Requisito lógico de una obligación. | `id`, `obligation_id`, `requirement_version_id`, `requirement_code` | Único por obligación/requisito aplicable. |
| `evidence_version` | Aporte o sustitución histórica. | `id`, `evidence_item_id`, `file_object_id`, `structured_payload`, `status`, `submitted_by`, `submitted_at`, `reason`, `supersedes_id` | Una `VIGENTE` por ítem; archivo `LIMPIO`; después de conclusión sólo superior y motivo. |
| `evidence_review_snapshot` | Integridad calculada en un instante. | `id`, `obligation_id`, `result`, `missing_requirements`, `evaluated_at` | `COMPLETA/INCOMPLETA`; inmutable, recalculable. |

`file_object.scan_status`: `PENDIENTE`, `LIMPIO`, `INFECTADO`, `INVALIDO`, `ERROR_ESCANEO`. Un archivo rechazado puede conservarse en cuarentena el tiempo técnico mínimo definido por operación, pero nunca se vuelve evidencia.

### 5.5 Ejecución, validación y avisos

| Tabla | Propósito | Campos esenciales | Restricciones |
|---|---|---|---|
| `execution_result` | Resultado estructurado al concluir. | `id`, `obligation_id`, `result_code`, `result_payload`, `evidence_review_id`, `recorded_by`, `recorded_at` | Uno por obligación en MVP; requiere evidencia completa y responsable vigente. |
| `validation_requirement` | Estado pendiente de validar. | `id`, `obligation_id`, `policy_version_id`, `status`, `created_at` | Uno por obligación concluida validable; `PENDIENTE/RESUELTA`. |
| `validation_decision_version` | Decisión vigente e histórica. | `id`, `requirement_id`, `result`, `foundation`, `status`, `validator_user_id`, `decided_at`, `reason`, `supersedes_id`, `evidence_snapshot` | Una `VIGENTE`; resultado permitido; autoridad y no autovalidación salvo Dirección; sustitución con motivo. |
| `internal_notice` | Aviso dentro de SGOL. | `id`, `recipient_user_id`, `notice_type`, `resource_type`, `resource_id`, `created_at`, `read_at` | Sin canal externo; acceso sólo destinatario. |

Una validación nunca cambia `work_obligation.execution_status`.

### 5.6 Auditoría, idempotencia y operación

| Tabla | Propósito | Campos esenciales | Restricciones |
|---|---|---|---|
| `audit_event` | Hecho funcional append-only. | `id`, `occurred_at`, `actor_user_id`, `actor_type`, `action`, `resource_type`, `resource_id`, `branch_id`, `correlation_id`, `request_id`, `before_data`, `after_data`, `reason`, `outcome`, `source_ip_hash` | Sólo INSERT para rol de aplicación; trigger rechaza UPDATE/DELETE; sin secretos ni contenido binario. |
| `idempotency_record` | Respuesta estable a mutaciones API. | `scope`, `key`, `request_hash`, `status`, `resource_type`, `resource_id`, `response_code`, `created_at`, `expires_at` | Único `(scope,key)`; no expira antes del horizonte de reintento del dominio. Generación conserva clave permanentemente. |
| `outbox_event` | Trabajo posterior a commit. | `id`, `event_type`, `aggregate_id`, `payload`, `created_at`, `available_at`, `processed_at`, `attempt_count`, `last_error` | Insertado en transacción; procesamiento con bloqueo y reintento. |
| `scheduled_job_run` | Ejecución de recurrencia, réplica, respaldo o conciliación. | `id`, `job_name`, `scheduled_for`, `started_at`, `ended_at`, `status`, `checkpoint`, `error` | Único `(job_name, scheduled_for)`; estados auditables. |
| `recovery_run` | Evidencia de restauración. | `id`, `started_at`, `ended_at`, `target`, `backup_reference`, `rpo_observed`, `rto_observed`, `reconciliation_result`, `approved_by` | No se marca exitoso sin conciliación funcional. |

## 6. Restricciones críticas

| ID | Restricción física |
|---|---|
| DB-G-001 | Índice único parcial para una versión vigente de cada objeto versionado y alcance. |
| DB-G-002 | Índice único parcial para una asignación vigente por obligación. |
| DB-G-003 | Índice único para plan por sucursal/período. |
| DB-G-004 | Índice único para generación por clave idempotente. |
| DB-G-005 | Conclusión se ejecuta por servicio transaccional que bloquea obligación y asignación vigentes. |
| DB-G-006 | Una decisión vigente por requisito de validación. |
| DB-G-007 | Evidencia vigente referencia exclusivamente archivo `LIMPIO`. |
| DB-G-008 | Restricciones históricas usan `ON DELETE RESTRICT`; no `CASCADE`. |
| DB-G-009 | `audit_event` no admite actualización o borrado a la cuenta de aplicación. |
| DB-G-010 | `row_version`/ETag evita actualizaciones perdidas. |
| DB-G-011 | Códigos de tarea fuera de las ocho aprobadas no pueden adquirir versión generadora vigente. |
| DB-G-012 | Estado de ejecución no acepta `VENCIDA`, `INICIADA`, `CANCELADA`, `TRASLADADA` ni otros estados fuera del MVP. |

Las restricciones que cruzan varias tablas se aplican en el servicio de dominio y se vuelven a comprobar dentro de la transacción con filas bloqueadas. Los triggers se reservan para defensa append-only y guardas simples; no alojan lógica funcional completa.

## 7. Concurrencia y aislamiento

- Aislamiento ordinario `READ COMMITTED` con bloqueos explícitos sobre agregados escritos.
- Generación usa `INSERT ... ON CONFLICT`/manejo de violación única y compara `request_hash`.
- Asignación automática bloquea la obligación y vuelve a leer cargas antes de confirmar.
- Sustituciones bloquean la versión vigente y crean sucesora de forma atómica.
- Publicación bloquea el plan y asigna el siguiente `version_no`.
- `If-Match` se traduce a `row_version`; discrepancia devuelve `412 VERSION_CONFLICT`.
- Deadlocks se reintentan un máximo acotado con la misma clave idempotente y telemetría.

## 8. Índices mínimos

- `person(stable_code)` único.
- versiones vigentes por claves de negocio con índices parciales.
- `work_obligation(branch_id, execution_status, due_at)`.
- `assignment_version(person_id, status, assigned_at)`.
- `validation_requirement(status, created_at)`.
- `audit_event(resource_type, resource_id, occurred_at desc)`.
- `audit_event(actor_user_id, occurred_at desc)`.
- `audit_event(branch_id, occurred_at desc)`.
- `internal_notice(recipient_user_id, read_at, created_at desc)`.
- `file_object(scan_status, created_at)` y `file_object(replicated_at)`.
- `outbox_event(processed_at, available_at)`.

No se particiona en el MVP por la escala aprobada. Se monitorea tamaño y latencia; particionar auditoría requiere ADR posterior y prueba de restauración.

## 9. Datos específicos de las ocho tareas

| TAR | `input_payload` mínimo | Resultado/evidencia estructurada clave |
|---|---|---|
| TAR-0005 | meta, venta real, fuente, ventana | porcentaje calculado; acción/responsable/inicio o conformidad; soporte digital |
| TAR-0007 | separado, mercancía, inicio, vencimiento, referencia | liberación, mercancía, fecha/hora, retorno a exhibición |
| TAR-0008 | operación, detección, reclamantes ≥2 | expediente, secuencia, decisión, fundamento, aviso interno |
| TAR-0011 | expediente, autorización previa, producto, solución, fecha | evaluación, autorización, reparación/cambio, comprobantes, entrega, aviso |
| TAR-0018 | evento, zona, planograma/lista | diez ítems, foto final y planograma/lista en PDF/JPEG/PNG |
| TAR-0026 | servicio, vencimiento real, referencia | FORM-ADM-02 estructurado/PDF y comprobante |
| TAR-0092 | recepción, proveedor, nota, inicio, mercancía | F-ENT-001, documentos; foto condicional por daño/diferencia |
| TAR-0093 | recepción padre, tipo, descripción, momento | foto, anotación y constancia de aviso interno |

Los esquemas concretos se implementarán sólo con los campos aprobados en F05. Esta tabla no autoriza campos comerciales nuevos.

## 10. Auditoría y privacidad

Se auditan, como mínimo: autenticación sensible, administración de cuenta/MFA, configuración, persona, rol, calendario, generación, asignación, publicación, conclusión, evidencia, validación, acceso denegado, conflicto idempotente, respaldo y recuperación.

`before_data`/`after_data` contiene únicamente los campos necesarios para reconstruir el cambio. Se excluyen contraseña, hash de contraseña, semilla TOTP, códigos de recuperación, cookie, URL firmada, secreto, binario y texto completo innecesario. Datos personales se minimizan y la consulta aplica la jerarquía F05.

## 11. Retención, respaldo y restauración

- Piloto: datos, auditoría y evidencias sin purga automática.
- Producción: bloqueada hasta política legal/operativa aprobada.
- PostgreSQL: PITR administrado más exportación portable diaria cifrada.
- Objetos: réplica incremental al menos horaria a segunda región y conciliación SHA-256.
- Una restauración válida compara IDs, relaciones, versiones, conteos, hashes y eventos conforme a CA-035.

## 12. Migraciones de esquema

1. Todas las migraciones están versionadas y revisadas.
2. No se ejecuta edición manual de producción salvo runbook de incidente.
3. Migraciones compatibles hacia adelante; sin `DROP`, renombre destructivo ni conversión irreversible en la misma liberación.
4. Datos semilla canónicos —sucursal, roles, permisos y ocho códigos TAR— son idempotentes y auditables.
5. Staging prueba migración desde una copia sintética de la versión anterior y rollback de aplicación.
6. El pipeline falla si el modelo permite estado, rol o tarea fuera del alcance aprobado.

## 13. Trazabilidad de capacidades a agregados

| Capacidades | Agregados/tablas principales |
|---|---|
| CAP-001 a CAP-007 | `person`, `employment_version`, `app_user`, `role_assignment_version`, `availability_day_version`, `branch` |
| CAP-009 a CAP-015 | configuración, calendario, TAR y reglas versionadas |
| CAP-018 a CAP-025 | `generation_request`, `work_obligation`, elegibilidad, asignación y plan |
| CAP-027 a CAP-033 | obligación, evidencia, ejecución y validación |
| CAP-039, CAP-042 a CAP-044 | consultas derivadas, `internal_notice` y vistas autorizadas |
| CAP-045 a CAP-047 | `audit_event`, idempotencia, trabajos, respaldos y `recovery_run` |

## 14. Criterios de aceptación del modelo

1. Las 35 capacidades encuentran persistencia o derivación explícita.
2. Las guardas G-001 a G-007 de F05 tienen restricción o transacción definida.
3. Ninguna tabla introduce funciones posteriores o excluidas.
4. Toda versión conserva predecesora y vigencia.
5. No existe ruta de borrado funcional para auditoría, evidencia o historia.
6. El modelo soporta los positivos y negativos CA-001 a CA-035 y CAT-001 a CAT-008.
