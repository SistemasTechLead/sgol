# SGOL — Adenda 19 a F07: contrato de evaluación estructural de evidencia para `HU-026`

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Propuesta de adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE; IMPLEMENTACIÓN EN CURSO` |
| Fecha | 2026-09-08 |
| Historia | `HU-026 — SGOL evalúa evidencia completa e informa faltantes` |
| Corte y épica | `CV-03` / `EP-07` |
| Criterios | `CA-026`, `CP-026-P`, `CP-026-N`, `RN-017`, `RN-018`, `RN-019` |
| Efecto pretendido | Precisar el contrato ejecutable de `HU-026` sin modificar ni renumerar el backlog aprobado |
| Precedencia verificada | `HU-025` reconocida como `Terminada` efectiva: PR `#41`, commit `9f754ea68569786054bee63261ebdb3ee2565d4f`, check `TECH-BASE-003 / PR gates` `SUCCESS` run `34171002437`, aprobación humana, PostgreSQL y SeaweedFS/ClamAV satisfactorios, merge `e74c2e8d077d39ebf1605cd6e6f5b44ad1ff1d1f`, commit y merge ancestros de `origin/master` y cero defectos bloqueantes conocidos. `TECH-EVID-002` reconocida como `Terminada` efectiva por la misma regla condicional: Adenda 20 aprobada, PR `#42`, commit `66248692f2577a95384db62882e8081dec6fdd9e`, check `TECH-BASE-003 / PR gates` `SUCCESS` run `34264672922`, aprobación humana, PostgreSQL externo `7/7`, merge `bd12592660121762cdf315fb5cfc497540b6892c`, `origin/master` verificado en ese merge, commit y merge ancestros de `origin/master` y cero defectos bloqueantes conocidos |
| Conservación | Mantiene sin cambios F00–F07, `Fuentes/`, `docs/design/logo.svg`, `docs/design/mapa-pantallas.md` y los tres Designer ajenos identificados para esta tarea |
| Aprobación | Aprobación íntegra recibida del responsable el 2026-09-08 |

Esta revisión incorpora la resolución aprobada e implementada por `F07_ADENDA_20_CONTRATO_DE_EVIDENCIA_ESTRUCTURADA_TECH_EVID_002.md`. La aprobación íntegra autoriza exclusivamente la implementación aquí descrita. No autoriza commit, publicación, pull request ni merge.

## 2. Fuentes y orden efectivo

La propuesta se limita a la fila 25 de `F07_BACKLOG_DE_IMPLEMENTACION.md`, `HU-026`, `CAP-031`, `CA-026`, `CP-026-P`, `CP-026-N`, `RN-017` a `RN-019`, `DEC-022`, `CPE-001`, `CPE-003`, el límite futuro `TEC-CONC-003`, los contratos F06 directamente aplicables, las adendas 15 a 20 y la implementación y pruebas fusionadas de obligaciones, política congelada, evidencia binaria y estructurada, autorización, auditoría, PostgreSQL, consultas y observabilidad.

La tabla `Tareas insertadas por adenda` incorpora `TECH-EVID-002` inmediatamente antes de `HU-026`; su cierre efectivo satisface esa precedencia. La tabla `Dependencias de infraestructura por momento` no agrega otro bloqueo para este tramo. No existe una adenda posterior a `F07_ADENDA_20` en la base verificada. El orden efectivo es `HU-025 → TECH-EVID-002 → HU-026 → HU-022`.

No se adelantan `HU-022`, `HU-030`, `HU-027`, `HU-028` ni otra historia posterior. `TEC-CONC-003` sólo se conserva como límite: la conclusión concurrente con sustitución se implementará en `HU-022`; `HU-026` únicamente deja un snapshot consumible y una disciplina de bloqueo compatible.

## 3. Hechos aprobados que no se reabren

1. La obligación conserva una FK inmutable `work_obligation.evidence_policy_version_id` hacia la política exacta capturada al materializarse.
2. Una política publicada contiene el catálogo completo y ordenado de su TAR; todo requisito configurado y aplicable es obligatorio.
3. Las únicas condiciones son `SIEMPRE` y `DIFERENCIA_O_DANO`; esta última pertenece exclusivamente a `TAR-0092 / FOTO_DIFERENCIA_DANO / FOTOGRAFIA`.
4. `evidence_item` corresponde de forma única a obligación y requisito de la política capturada.
5. Una versión funcional de evidencia es `VIGENTE` o `SUSTITUIDA`; sólo una puede estar `VIGENTE` por ítem y cada `evidence_version` contiene exactamente uno de `file_object_id` o `structured_payload`.
6. Para la porción binaria, una `evidence_version` sólo se inserta si su `file_object` está `LIMPIO`, en el bucket lógico `CLEAN` y ligado irreversiblemente al mismo ítem.
7. `FORMULARIO_REFERENCIADO`, `REGISTRO_DIGITAL`, `DATO_ESTRUCTURADO` y `CHECKLIST_ESTRUCTURADO` son aportables mediante 18 contratos cerrados; aplicación y PostgreSQL validan requisito, clase, `schemaVersion` y payload.
8. La versión `VIGENTE` y estructuralmente válida de `F_ENT_001` de la misma obligación y política es el único hecho canónico de `DIFERENCIA_O_DANO`.
9. La historia de `evidence_item` y `evidence_version` es inmutable; una sustitución conserva la versión anterior como `SUSTITUIDA` y crea una única sucesora `VIGENTE`.
10. `HU-025` no vuelve a inspeccionar contenido durante una consulta y `HU-026` no invoca S3, SeaweedFS, ClamAV, promoción ni descarga.
11. Evaluación estructural, conclusión de obligación y decisión de validación son hechos separados.

## 4. Resolución aprobada de la contradicción histórica

La contradicción que contenía la versión inicial de esta propuesta quedó resuelta por la Adenda 20 aprobada y por el cierre efectivo de `TECH-EVID-002`. La implementación fusionada acredita conjuntamente:

1. las cuatro clases `FORMULARIO_REFERENCIADO`, `REGISTRO_DIGITAL`, `DATO_ESTRUCTURADO` y `CHECKLIST_ESTRUCTURADO` ya son aportables y sustituibles por las rutas existentes;
2. existen exactamente 18 contratos estructurados cerrados por `taskCode + requirementCode + kind + schemaVersion`;
3. `evidence_version` admite exactamente archivo o `structured_payload`, nunca ambos ni ninguno;
4. la versión `VIGENTE` de `F_ENT_001` de la misma obligación y política es la autoridad exclusiva de `DIFERENCIA_O_DANO`; ambos booleanos falsos resuelven falso, cualquiera verdadero resuelve verdadero y la ausencia no se interpreta como falso;
5. el validador de aplicación y `sgol_evidence_structured_payload_valid` en PostgreSQL comprueban los contratos cerrados;
6. `evidence_item` y `evidence_version` conservan cadena lineal, única `VIGENTE`, sucesoras y versiones `SUSTITUIDA`; y
7. `CP-026-P` es alcanzable sin ignorar requisitos, inventar evidencia, inferir `input_payload` ni ampliar `HU-026` para aportar o sustituir.

La revalidación contra el corte fusionado no abre otra clase, requisito, endpoint ni representación. `HU-026` debe reutilizar los 18 contratos y las tres relaciones nominales de la Adenda 20: coherencia `CALCULO_AVANCE`/`ACCION_O_CONFORMIDAD`, diez valores verdaderos de `CHECKLIST_COMPLETO` y el hecho `F_ENT_001` para `DIFERENCIA_O_DANO`. No se introduce un motor de reglas.

La aprobación de la Adenda 20 no aprueba esta Adenda 19. El contrato de evaluación, snapshot, concurrencia y persistencia de las secciones siguientes requiere una decisión íntegra independiente antes de editar producción.

## 5. Resultado cerrado propuesto

`HU-026` agregará exclusivamente:

```http
GET /api/v1/obligations/{id}/evidence-review
```

La operación resolverá la política congelada de la obligación, evaluará todos sus requisitos en orden canónico, considerará sólo evidencia funcional `VIGENTE`, materializará o reutilizará un snapshot coherente e inmutable y devolverá exclusivamente `COMPLETA` o `INCOMPLETA`. No concluirá, validará, aportará, sustituirá ni descargará evidencia.

## 6. Actor sistema y solicitante

El cálculo lo ejecuta el actor técnico `SYSTEM`; no existe cuenta técnica, permiso nuevo ni identidad funcional para ese actor. El usuario autenticado sólo solicita y consulta el resultado. El snapshot registra ambos hechos de forma separada:

- `evaluated_by = SYSTEM`, constante protegida por check; y
- `requested_by_user_id`, usuario autorizado que provocó la primera materialización de esa huella.

La reutilización del mismo snapshot por otro usuario autorizado no cambia el solicitante histórico, no crea otra fila y no transfiere autoridad. El usuario actual se vuelve a autorizar en cada GET.

## 7. Autorización, alcance y anti-IDOR

Se reutiliza exclusivamente `PER-TAREA-VER`; no se crea permiso de revisión. En un único `evaluatedAt`, el actor debe tener sesión individual con MFA completo, cuenta y empleo vigentes en `LOR-001`, exactamente un rol canónico vigente y el permiso efectivo.

La visibilidad reutiliza literalmente la expresión de alcance de `HU-023` y `F07_ADENDA_15`:

- responsable vigente: su propia obligación;
- superior canónico: obligaciones cuyo responsable vigente tiene nivel estrictamente inferior; y
- `DIRECCION`: toda obligación de `LOR-001`.

Responsabilidad histórica, par, inferior, puesto textual, turno, publicación o dato enviado por el cliente no conceden acceso. Sesión ausente o MFA incompleto devuelve `401 AUTENTICACION_REQUERIDA`. Actor autenticado sin autoridad base devuelve `403 ACCESO_DENEGADO` antes de resolver el recurso. UUID inexistente, obligación de otra sucursal o fuera del alcance converge en `404 OBLIGACION_NO_ENCONTRADA`.

La consulta de obligación se restringe en SQL antes de cargar política, requisitos, ítems, versiones o snapshot. Una solicitud rechazada no crea snapshot, auditoría de éxito ni otro efecto.

## 8. Contrato HTTP y payload propuesto

La ruta no admite query string, cuerpo, `Idempotency-Key`, `If-Match` ni CSRF. Parámetro desconocido o repetido devuelve `400 SOLICITUD_REVISION_INVALIDA`; UUID de ruta mal formado devuelve `400 OBLIGACION_ID_INVALIDO` antes de invocar el servicio.

No se usa ETag: la representación es una evaluación de la evidencia actualmente vigente y un ETag podría inducir a consumir un resultado obsoleto. `snapshotId` identifica el corte inmutable y `inputFingerprint` se conserva sólo internamente; cada consumidor debe volver a solicitar la revisión cuando necesite conocer el estado actual.

Respuesta `200`:

```json
{
  "data": {
    "snapshotId": "019...",
    "obligationId": "019...",
    "evidencePolicyVersionId": "019...",
    "result": "COMPLETA|INCOMPLETA",
    "evaluatedAt": "2026-09-07T23:00:00Z",
    "requirements": [
      {
        "requirementVersionId": "019...",
        "requirementCode": "FOTOGRAFIA_FINAL",
        "kind": "FOTOGRAFIA",
        "conditionCode": "SIEMPRE",
        "ordinal": 2,
        "applicability": "APLICABLE",
        "satisfied": true,
        "evidenceVersionId": "019...",
        "missingReason": null
      }
    ],
    "missingRequirements": []
  },
  "meta": {
    "correlationId": "019..."
  }
}
```

`requirements` contiene todos los requisitos de la política congelada, incluidos los no aplicables o todavía no resolubles. `applicability` usa exclusivamente `APLICABLE`, `NO_APLICABLE` o `NO_RESUELTA`; nunca convierte ausencia de `F_ENT_001` en falso. `satisfied` sólo puede ser verdadero para `APLICABLE` con evidencia vigente funcionalmente satisfactoria, o para `NO_APLICABLE`, que no exige evidencia. `evidenceVersionId` es el ID vigente usado o `null`. `missingReason` es `EVIDENCIA_VIGENTE_AUSENTE`, `EVIDENCIA_VIGENTE_NO_SATISFACE` o `null`.

`missingRequirements` contiene exactamente los requisitos `APLICABLE` que no están satisfechos, con `requirementVersionId`, `requirementCode`, `kind`, `conditionCode`, `ordinal` y `missingReason`. Un requisito condicional `NO_RESUELTA` no se presenta falsamente como aplicable ni como no aplicable; la autoridad `F_ENT_001` ausente aparece por sí misma como faltante obligatorio y el resultado es `INCOMPLETA`. Ambas colecciones usan `ordinal` ascendente y `requirementVersionId` ascendente como desempate defensivo.

No se devuelve nombre original, media type, tamaño, hash, subtipo documental, actor de aporte, motivo, contenido, `structured_payload`, clave, bucket, URL, escáner, auditoría, validación ni datos internos de condición. El código y la clase del requisito son visibles; no se inventa una descripción localizada que no forme parte del catálogo persistido aprobado.

## 9. Algoritmo determinista propuesto

Dentro de la transacción de la sección 12:

1. capturar una sola vez `evaluatedAt = IClock.UtcNow`;
2. autorizar al solicitante y cargar la obligación exclusivamente mediante el filtro SQL de alcance;
3. exigir una `evidence_policy_version_id` no nula y resolver exactamente esa versión, nunca la política actualmente `VIGENTE` de la TAR;
4. cargar el conjunto completo de `evidence_requirement_version` de esa política y verificar su coherencia contra la política congelada y el catálogo cerrado de la TAR;
5. evaluar `SIEMPRE` como aplicable;
6. evaluar `DIFERENCIA_O_DANO` exclusivamente desde la única versión `VIGENTE` de `F_ENT_001` de la misma obligación y política: cualquiera de `hasDifference` o `hasDamage` verdadero produce `APLICABLE`, ambos falsos producen `NO_APLICABLE`, y la ausencia ordinaria de ítem o versión produce `NO_RESUELTA`; duplicidad, payload inválido o vínculo incoherente falla cerrado;
7. para cada requisito aplicable, buscar el único `evidence_item` del mismo par obligación/requisito y su única `evidence_version` `VIGENTE`;
8. considerar satisfecho un requisito binario sólo si la versión `VIGENTE` referencia el archivo del mismo ítem y requisito en `LIMPIO/CLEAN`; considerar satisfecho un requisito estructurado sólo si la versión `VIGENTE` cumple el contrato cerrado de aplicación y PostgreSQL para su TAR, código, clase y esquema;
9. aplicar además únicamente las relaciones nominales aprobadas: `ACCION_O_CONFORMIDAD` satisface si su resultado corresponde al porcentaje de `CALCULO_AVANCE`; `CHECKLIST_COMPLETO` satisface sólo con sus diez booleanos verdaderos; `F_ENT_001` estructuralmente válido satisface con independencia del valor de sus booleanos y ese valor sólo decide la foto condicional;
10. tratar una versión `SUSTITUIDA`, un archivo técnico no vinculado o una fila ajena como inexistentes para satisfacción; si hay una versión `VIGENTE` estructuralmente corrupta o más de una vigente, fallar cerrado en vez de degradarla a faltante ordinario;
11. producir `COMPLETA` sólo si todos los requisitos `SIEMPRE` y `APLICABLE` están satisfechos y no existe aplicabilidad `NO_RESUELTA`; en otro caso producir `INCOMPLETA` y la lista exacta de requisitos aplicables no satisfechos; y
12. calcular la huella canónica, materializar o reutilizar el snapshot y devolverlo.

Requisitos `NO_APLICABLE` y el requisito condicional `NO_RESUELTA` no aparecen en faltantes. La ausencia ordinaria de evidencia vigente sí es un faltante; no es inconsistencia de configuración. Un requisito configurado inexistente, duplicado, ajeno a la política, con clase/condición/ordinal incoherente, sin representación funcional aprobada, o una condición que no pueda resolverse pese a existir su supuesta autoridad, falla cerrado y no crea snapshot.

Con las mismas entradas canónicas se obtiene el mismo resultado, el mismo orden, la misma huella y el mismo `snapshotId`; `evaluatedAt` conserva el instante de la primera materialización de esa huella.

## 10. Política ausente, histórica o inconsistente

Aunque `F07_ADENDA_16` conserva obligaciones históricas con `evidence_policy_version_id = NULL` y no infiere política retroactiva, esta ausencia no permite afirmar `COMPLETA`. El GET devuelve `409 REVISION_EVIDENCIA_NO_DISPONIBLE`, no consulta la política actual, no crea snapshot y no modifica la obligación.

La misma respuesta pública se usa para política/requisitos inexistentes, incompletos, duplicados, de otra TAR, con condición desconocida, sin representación funcional aprobada, con más de una versión vigente o con vínculos incoherentes. La ausencia ordinaria de `evidence_item`, `evidence_version` o `F_ENT_001` vigente se evalúa como `INCOMPLETA`, no como `409`. Internamente la métrica distingue `missing_policy`, `invalid_policy`, `unsupported_requirement`, `invalid_evidence_history` y `unresolvable_condition`; logs y respuesta no revelan estructura interna.

## 11. Snapshot, obsolescencia y reutilización

Cada conjunto nuevo de entradas canónicas crea un `evidence_review_snapshot`. Un conjunto ya materializado reutiliza la misma fila. No existe expiración, actualización ni borrado funcional.

La entrada canónica se conserva como JSON cerrado, ordenado y sin contenido sensible. Su huella SHA-256 minúscula se calcula sobre la representación UTF-8 canónica e incluye, en orden estable:

- versión de esquema de snapshot;
- `obligation_id` y `evidence_policy_version_id`;
- cada requisito configurado con ID, código, clase, condición, ordinal, `applicability`, `satisfied` y `missingReason`;
- para cada requisito aplicable, el `evidence_item.id` y `evidence_version.id` `VIGENTE`, o marcadores explícitos de ausencia; y
- para `DIFERENCIA_O_DANO`, `TRUE`, `FALSE` o `UNRESOLVED` y el `evidence_version.id` de `F_ENT_001` usado, si existe; y
- los resultados funcionales mínimos derivados para acreditar las tres relaciones nominales, sin incluir `structured_payload`, contenido ni metadatos secretos.

`evaluatedAt`, solicitante, actor técnico y `correlationId` no forman parte de la entrada ni de la huella. PostgreSQL conserva la entrada canónica y aplica unicidad sobre ella y sobre la huella; una colisión o desacuerdo entre ambas falla cerrado. La huella se prueba con vectores conocidos, pero no se agrega `pgcrypto` ni otra extensión para recalcular SHA-256 en la base.

Un snapshot queda obsoleto cuando una evaluación actual produce otra huella. La sustitución posterior no modifica snapshots anteriores: crea o permite reutilizar un snapshot correspondiente a la nueva versión vigente. Un cambio posterior de política no afecta la obligación porque su FK congelada no cambia.

No existe endpoint para listar u obtener snapshots históricos en `HU-026`; la historia queda persistida para consumo transaccional futuro de `HU-022` y trazabilidad posterior, sin adelantar sus superficies.

## 12. Transacción y concurrencia

La operación usa una transacción PostgreSQL `SERIALIZABLE` y una única fotografía MVCC. El orden interno de lectura y bloqueo de `HU-026` es:

1. obligación ya filtrada por autorización `FOR UPDATE`;
2. asignación `VIGENTE` necesaria para el alcance;
3. política congelada y requisitos, que son inmutables;
4. `evidence_item` en ordinal estable;
5. `evidence_version` `VIGENTE` de cada ítem; y
6. snapshot existente por entrada canónica o huella.

Los aportes y sustituciones fusionados de `HU-025`/`TECH-EVID-002` también usan `SERIALIZABLE`, pero no se presume que bloqueen primero la obligación. La coherencia se apoya en la fotografía MVCC, los bloqueos de predicado de PostgreSQL y la serialización: una evaluación concurrente se ordena lógicamente antes o después del aporte o sustitución y nunca mezcla versiones. `40001`, `40P01` y la carrera `23505` sobre entrada/huella usan recuperación acotada: tras una carrera de inserción se lee la fila ganadora sólo si coinciden exactamente entrada canónica, huella y proyección; en otro caso se reintenta la transacción completa dentro del límite común. No se modifica la semántica funcional de aporte o sustitución.

`HU-026` no bloquea ni cambia `work_obligation.execution_status`. `HU-022` deberá adquirir primero la obligación y consumir o volver a calcular una huella dentro de su propia transacción; esa transición queda fuera de esta historia.

## 13. Persistencia propuesta: `evidence_review_snapshot`

El módulo `Evidence` es propietario de la única tabla nueva. `Execution` podrá consumir su contrato en `HU-022`, pero no escribirla directamente.

| Columna | Tipo PostgreSQL | Regla |
|---|---|---|
| `id` | `uuid` | PK, UUID v7 generado por aplicación |
| `obligation_id` | `uuid` | FK `RESTRICT` a `work_obligation.id` |
| `evidence_policy_version_id` | `uuid` | FK `RESTRICT` a `evidence_policy_version.id`; debe coincidir con la FK congelada de la obligación |
| `result` | `varchar(16)` | Sólo `COMPLETA` o `INCOMPLETA` |
| `schema_version` | `smallint` | Exactamente `1` |
| `input_fingerprint` | `char(64)` | SHA-256 hexadecimal minúsculo de las entradas canónicas |
| `canonical_input` | `jsonb` | Entrada cerrada y ordenada de IDs, ausencia/presencia, aplicabilidad y resultados derivados; sin payload ni metadatos sensibles |
| `requirements_snapshot` | `jsonb` | Arreglo cerrado y ordenado de todos los requisitos evaluados |
| `missing_requirements` | `jsonb` | Arreglo cerrado y ordenado de los aplicables no satisfechos |
| `evidence_version_ids` | `uuid[]` | IDs exactos, ordenados y sin duplicados de versiones `VIGENTE` usadas |
| `evaluated_by` | `varchar(16)` | Exactamente `SYSTEM` |
| `requested_by_user_id` | `uuid` | FK `RESTRICT` a `app_user.id`, solicitante de la primera materialización |
| `evaluated_at` | `timestamptz` | Instante UTC único de la primera materialización |
| `correlation_id` | `uuid` | Correlación de la primera materialización; no se expone como dato histórico |

Restricciones e índices:

- unicidad `(obligation_id, input_fingerprint)` y `(obligation_id, canonical_input)` para reutilización determinista y defensa ante desacuerdo;
- índice `(obligation_id, evaluated_at desc, id desc)` para consumo histórico interno futuro;
- índice `(evidence_policy_version_id, evaluated_at desc)`;
- checks de UUID no nulo, resultado, esquema, huella, formas JSON cerradas, valores permitidos de aplicabilidad y motivo, ordinal positivo, orden, ausencia de duplicados y `evidence_version_ids` sin nulos;
- check `result='COMPLETA'` si y sólo si `missing_requirements` es `[]` y ningún requisito tiene `applicability='NO_RESUELTA'`;
- trigger `BEFORE INSERT` defensivo que verifica obligación/política, catálogo completo, vínculo exacto de cada ítem/versión vigente y coincidencia entre `canonical_input`, JSON proyectado y array de versiones. Reutiliza `sgol_evidence_structured_payload_valid` para defender los 18 contratos, pero no decide la evaluación ni aloja un motor de reglas;
- trigger que rechaza todo `UPDATE` y `DELETE`; y
- `ON DELETE RESTRICT` en todas las FK.

Los IDs dentro del JSON y del array no sustituyen FKs ordinarias; se validan al insertar por el trigger, y las guardas append-only ya aprobadas impiden borrar `evidence_version`. No se agrega tabla puente porque esta adenda limita la persistencia a `evidence_review_snapshot`.

La migración propuesta, sólo después de aprobar íntegramente esta adenda revisada, se llamará `AddEvidenceReviewSnapshots`. Será forward-only, sin BOM, con LF y `Down()` bloqueado mediante excepción. No modificará migraciones históricas.

## 14. Auditoría y atomicidad

La creación de un snapshot registra `EVIDENCE_REVIEW_SNAPSHOT_CREATED / SUCCESS` dentro de la misma transacción. El evento contiene sólo versión de esquema, snapshot, obligación, política, resultado, conteos de aplicables/faltantes, solicitante, instante y `correlationId`; no contiene listas completas, nombres, hashes, archivos, payloads ni contenido.

La reutilización de una huella existente es una lectura y no crea auditoría de éxito. Una denegación autenticada reutiliza la auditoría sensible transversal sólo cuando el patrón existente la exige; nunca crea snapshot ni auditoría de éxito. La falta de sesión no inventa actor funcional.

Si falla auditoría, trigger, persistencia o commit, no queda snapshot ni evento de éxito. No hay outbox: no existe efecto externo ni procesamiento asíncrono.

## 15. Errores normalizados

Todos los errores usan `application/problem+json`, propiedades `status`, `code`, `title`, `instance` y `correlationId`, y detalle genérico.

| HTTP | `code` | Uso |
|---:|---|---|
| 400 | `OBLIGACION_ID_INVALIDO` | UUID de ruta inválido |
| 400 | `SOLICITUD_REVISION_INVALIDA` | Query, cuerpo o cabecera funcional no admitida |
| 401 | `AUTENTICACION_REQUERIDA` | Sesión ausente, expirada o MFA incompleto |
| 403 | `ACCESO_DENEGADO` | Actor autenticado sin autoridad base antes de resolver recurso |
| 404 | `OBLIGACION_NO_ENCONTRADA` | Inexistente, otra sucursal o fuera de alcance |
| 409 | `REVISION_EVIDENCIA_NO_DISPONIBLE` | Política ausente/inconsistente, requisito no evaluable o autoridad de condición presente pero corrupta/incoherente |
| 500 | `ERROR_INTERNO` | Falla no controlada o transaccional agotada, sin efecto parcial |

`INCOMPLETA` es un resultado `200`, no un error. Nunca se responde `422 EVIDENCIA_FALTANTE` desde este GET. No se usan `Idempotency-Key`, `If-Match`, `412`, `413`, `415` ni `503` porque no hay mutación cliente, transferencia ni dependencia de archivos.

## 16. Seguridad, privacidad y observabilidad

La autorización se aplica en servidor por permiso, rol vigente, recurso, jerarquía y `LOR-001`. Ninguna decisión cliente, visibilidad UI o snapshot previo concede acceso.

Logs JSON pueden incluir `correlationId`, operación, resultado de baja cardinalidad, duración y código de falla allowlist. Se prohíben ID de usuario/obligación/snapshot/política/evidencia, código de requisito, nombre, metadata, contenido, `structured_payload`, hash, clave S3, bucket, URL, secreto, SQL, stack y detalle de proveedor.

Métricas propuestas:

- `sgol.evidence.reviews` con `result=complete|incomplete|inconsistent|failed`;
- `sgol.evidence.review_snapshots` con `result=created|reused|failed`; y
- histograma `sgol.evidence.review_duration` con `result`.

No se etiquetan TAR, requisito, usuario, obligación, política, snapshot ni versión de evidencia. Configuración inconsistente falla cerrada y cuenta como `inconsistent`; error técnico inesperado cuenta como `failed`.

## 17. Pruebas requeridas después de una aprobación válida

### 17.1 Dominio y API sin Docker

- todos satisfechos produce `COMPLETA`; uno o varios faltantes producen `INCOMPLETA` y lista exacta ordenada;
- condicional no aplicable no bloquea y aplicable faltante sí bloquea; `F_ENT_001` ausente produce `INCOMPLETA`, autoridad faltante y foto `NO_RESUELTA`, nunca falso implícito;
- `CALCULO_AVANCE`/`ACCION_O_CONFORMIDAD` incoherentes marcan no satisfecho el segundo requisito; checklist con cualquier `false` conserva evidencia pero no satisface;
- los 18 contratos cerrados, el XOR archivo/payload y `F_ENT_001` vigente se reutilizan sin aceptar otra clase o esquema;
- política congelada es autoridad y una sucesora no altera la obligación;
- sólo una versión `VIGENTE` funcionalmente válida satisface; `SUSTITUIDA`, ajena, inexistente o técnica no vinculada no satisface;
- política ausente/inconsistente y autoridad condicional presente pero corrupta o incoherente fallan cerradas sin snapshot;
- mismo conjunto reutiliza snapshot; conjunto nuevo crea otro sin alterar anteriores;
- payload exacto, ausencia de ETag/Idempotency-Key/CSRF y errores correlacionados;
- autorizado dentro de alcance consulta; par, superior no aplicable y ajeno no obtienen datos; inexistente y fuera de alcance convergen;
- rechazo no crea snapshot ni auditoría de éxito; fallo de auditoría revierte snapshot;
- no cambia ejecución, no crea `execution_result`, no valida y no invoca S3/ClamAV; y
- no existe UI ni endpoint adicional.

### 17.2 PostgreSQL real externa

- migración desde cero y desde la base vigente;
- PK, FK `RESTRICT`, checks, índices, unicidad por huella y triggers son autoridad final;
- trigger rechaza política, requisitos, JSON, huella, evidencia o resultado incoherentes;
- `UPDATE` y `DELETE` de snapshot se rechazan;
- evaluación simultánea idéntica produce una fila; sustitución concurrente nunca mezcla versiones;
- sustitución posterior produce otra huella y conserva ambos snapshots;
- auditoría y snapshot confirman o revierten juntos; y
- usuario de aplicación no puede fabricar, mutar ni borrar historia incoherente.

No se usa SQLite. El desarrollador ejecutará externamente las suites afectadas con PostgreSQL/Docker/Testcontainers y comunicará comando, total, errores, omitidas y duración. `HU-026` no requiere SeaweedFS/ClamAV; tocar sus adaptadores bloquea por ampliación de alcance.

## 18. Archivos previstos tras aprobar

Sólo después de aprobar íntegramente esta adenda podrán modificarse:

- `src/Modules/Evidence/` para evaluación y contratos;
- API y persistencia directamente necesarias en `src/Sgol.Web/`;
- una migración nueva, su Designer y el snapshot EF;
- pruebas unitarias, arquitectura, API y PostgreSQL directamente afectadas;
- OpenAPI/documentación técnica mínima e inventarios de migraciones/tablas;
- `docs/traceability/README.md` y `docs/traceability/IMPLEMENTATION_STATUS.md`; y
- esta adenda para registrar la decisión y aprobación íntegra.

No se modificarán proyectos, paquetes, configuración, CI, Worker ni adaptadores S3/ClamAV salvo que la adenda corregida lo autorice expresamente. Se preservan los cinco cambios ajenos identificados al inicio.

## 19. Gates de salida posteriores a aprobación e implementación

Tras revisar el diff completo, los gates se ejecutarán una sola vez y en este orden:

1. `dotnet restore SGOL.slnx --locked-mode` fuera del aislamiento;
2. `dotnet build SGOL.slnx --no-restore --configuration Release`;
3. suite unitaria completa;
4. suite de arquitectura completa;
5. pruebas enfocadas de `HU-026` sin Docker;
6. una solicitud al desarrollador para ejecutar externamente las suites PostgreSQL afectadas y espera de su resultado;
7. `dotnet format SGOL.slnx --verify-no-changes --no-restore`;
8. `./scripts/ci/Assert-NoVulnerablePackages.ps1`;
9. modelo EF sin cambios pendientes;
10. `./scripts/ci/verify-fuentes-protection.ps1`;
11. `./scripts/ci/verify-fuentes-mirror.ps1`, después y no en paralelo con el anterior; y
12. `git diff --check`.

Un gate omitido, compuesto, pendiente o ejecutado sobre otro SHA se registra como no verificado. No se ejecutan Docker ni Testcontainers dentro de la sesión y no se diagnostica Docker.

## 20. Trazabilidad y cierre condicional

La rama registrará `HU-026` como propuesta. El mismo registro sólo adquirirá eficacia `Terminada` en `master`, sin commit administrativo posterior, cuando el commit exacto contenga implementación y trazabilidad, el pipeline requerido esté verde, exista aprobación humana, merge y ascendencia verificada en `origin/master`, PostgreSQL sea satisfactorio, `Fuentes/` permanezca protegido y no haya defectos bloqueantes conocidos.

La adenda aprobada, `docs/traceability/README.md`, `docs/traceability/IMPLEMENTATION_STATUS.md`, la documentación API mínima, el inventario de migraciones/tablas y la documentación de la suite PostgreSQL se actualizarán en el mismo cambio de implementación.

Edición, commit, publicación de rama, apertura de PR y merge son autorizaciones independientes. Esta propuesta sólo autoriza su propia preparación documental solicitada.

## 21. Fuera de alcance

No se implementan ni simulan:

- `HU-022`, `POST /api/v1/obligations/{id}/conclusion` o `PENDIENTE→CONCLUIDA`;
- `HU-027`, `HU-028`, `HU-030`, validación, bandeja o avisos;
- UI, carga, descarga, sustitución, inspección, promoción o modificación de archivos;
- S3, SeaweedFS, ClamAV, URL firmada, hash o contenido binario;
- cambios automáticos de evidencia o validaciones;
- excepción o dispensa de evidencia;
- motor general de reglas o expresión configurable;
- borrado funcional, permiso o autenticación nuevos;
- `execution_result`, `validation_requirement`, `validation_decision_version` u otras tablas futuras;
- outbox, broker, Redis, microservicios, scheduler, otro Worker;
- réplica, respaldo, restauración o despliegue productivo; y
- cambios en F00–F07 o `Fuentes/`.

## 22. Decisión íntegra solicitada

Aprobar esta adenda significa aprobar conjuntamente:

1. la única ruta `GET /api/v1/obligations/{id}/evidence-review`, su payload, errores y ausencia de ETag, `Idempotency-Key`, CSRF y cuerpo;
2. reutilización exclusiva de `PER-TAREA-VER`, alcance de responsable vigente, superiores estrictos y Dirección, y convergencia `404` contra IDOR;
3. política congelada como autoridad, catálogo completo, obligatoriedad por defecto y aplicabilidad `SIEMPRE`/`DIFERENCIA_O_DANO`;
4. uso exclusivo de versiones `VIGENTE`, exclusión de `SUSTITUIDA` y las tres relaciones nominales ya aprobadas en la Adenda 20;
5. resultado exclusivo `COMPLETA/INCOMPLETA`, aplicabilidad trivaluada y lista completa, exacta y ordenada de requisitos aplicables no satisfechos;
6. materialización o reutilización de un snapshot inmutable con entrada canónica, huella, política, requisitos, versiones, faltantes, actor técnico, solicitante e instante UTC;
7. transacción `SERIALIZABLE`, fotografía MVCC, orden de bloqueo, recuperación de carreras y compatibilidad con aporte/sustitución concurrentes;
8. única tabla `evidence_review_snapshot`, sus columnas, FK `RESTRICT`, checks, índices, unicidades, guardas PostgreSQL e inmutabilidad;
9. auditoría atómica sólo al crear, sin outbox, y ausencia de snapshot/auditoría de éxito ante rechazo;
10. privacidad, métricas de baja cardinalidad, fallo cerrado, pruebas, gates, trazabilidad y cierre condicional; y
11. exclusión de conclusión, `execution_result`, validación, UI, operaciones de archivo, infraestructura externa y cualquier historia posterior.

No se interpreta como aprobación íntegra la aprobación de la Adenda 20, la existencia o nombre de este archivo, una aprobación parcial, una autorización de edición o la memoria del chat. Cualquier cambio material posterior exige una nueva decisión antes de implementar.

**¿Apruebas íntegramente la Adenda 19 revisada?**
