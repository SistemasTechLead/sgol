# F07 Adenda 30 — Contrato de idempotencia integral y conflictos de HU-034

## 1. Control de la propuesta

| Campo | Valor |
|---|---|
| Identificador | `F07_ADENDA_30` |
| Historia | `HU-034` — SGOL recupera mutaciones repetidas y registra conflictos |
| Estado | Propuesta; requiere aprobación íntegra antes de código productivo |
| Fecha | 2026-09-12 |
| Alcance | Idempotencia integral de las mutaciones ya implementadas que consumen una clave idempotente, recuperación de la respuesta confirmada y auditoría consultable de conflictos |
| Exclusiones rectoras | Sin nuevas funciones de negocio, UI, backfill inventado, purga, continuidad, respaldo, restauración, compensación o reconciliación de `HU-035` |

Esta adenda no modifica los documentos F00–F07 aprobados. Cierra únicamente las decisiones contractuales que éstos dejaron abiertas para implementar `HU-034` sin cambiar la autoridad, las guardas ni el resultado funcional propio de cada mutación.

## 2. Hechos documentados

1. El backlog ubica `HU-034` en el orden 34, inmediatamente después de `HU-033`, y la hace depender de `HU-014` a `HU-033` según la operación. `HU-035` ocupa el orden 35 y conserva continuidad y recuperación.
2. No existe una tarea insertada por adenda pendiente que deba ejecutarse antes de `HU-034`. `F07_ADENDA_30` es el siguiente identificador libre.
3. `RN-010` exige como máximo una obligación por regla, alcance, período y hecho originador; un reintento recupera la existente.
4. `RN-027` conserva auditoría, evidencia, versiones, asignaciones y validaciones. `RN-028` exige auditoría de cambios críticos.
5. `CA-034` exige que repetir una operación idempotente recupere el resultado y que un conflicto se registre sin duplicar. `CP-034-P` exige mismo ID y una sola obligación para dos solicitudes idénticas. `CP-034-N` exige `RECHAZADA`, auditoría y ausencia de segundo recurso para la misma clave con contenido distinto.
6. F06 exige `Idempotency-Key` UUID en mutaciones idempotentes, define el alcance conceptual usuario + operación + recurso, exige `409 IDEMPOTENCY_CONFLICT` para la misma clave con otro cuerpo y no permite que la idempotencia sustituya la identidad funcional permanente de generación.
7. `ADR-009` / `DEC-063` exige clave normalizada, hash de solicitud, restricción única y recuperación transaccional. PostgreSQL, no una consulta previa en memoria, es la autoridad final ante carreras.
8. `idempotency_record` ya existe con PK `(scope,key)` y los campos `request_hash`, `status`, `resource_type`, `resource_id`, `response_code`, `created_at` y `expires_at`.
9. La implementación vigente persiste mayoritariamente `COMPLETED`, mientras evidencia persiste `COMPLETADA`. No existen respuestas persistidas completas ni un catálogo DB que materialice `EN_PROCESO → COMPLETADA | FALLIDA`.
10. Los productores actuales tienen scopes, hashes, replays, validadores de cabecera y horizontes heterogéneos. Algunos recuperan desde una entidad inmutable; otros podrían reconstruir desde estado posterior y ya modificado.
11. Generación manual ya usa idempotencia persistente y unicidad funcional permanente. Generación recurrente, evaluación de elegibilidad y asignación automática usan identidades técnicas deterministas propias.
12. `TECH-AUD-001` aporta `audit_event` append-only y `AuditTransaction`. `HU-033` aporta `GET /api/v1/audit-events` con filtro exacto por `action`, autorización jerárquica y minimización; no autoriza una ruta de conflictos separada.
13. F06 exige métricas de conflictos idempotentes, pero no fija dimensiones, retención, límites de cardinalidad ni datos permitidos.

## 3. Contradicciones y carencias que impiden implementar sin decisión

1. F06 enumera categorías de mutación idempotente, pero no decide si `HU-034` cubre sólo esas categorías o todos los consumidores ya implementados de `Idempotency-Key` y `idempotency_record`.
2. Usuario + operación + recurso no define el recurso de una creación cuyo ID todavía no existe ni el actor de una operación técnica.
3. No existe una canonicalización transversal aprobada. Cambiar silenciosamente los hashes actuales rompería replays históricos.
4. `idempotency_record` conserva ID y status HTTP, pero no necesariamente el DTO, ETag o `Location` originales.
5. F02 conceptualiza estados que no coinciden con los valores actualmente persistidos y no decide si una reserva incompleta debe quedar visible.
6. No están fijados el orden entre autorización, replay, `If-Match` y guardas funcionales, ni el comportamiento de un replay cuyo ETag original ya quedó obsoleto.
7. No se distingue contractualmente el rollback previo al commit de una respuesta perdida después de un commit confirmado.
8. `CP-034-N` no define la forma del evento, si se audita cada intento o cómo se consulta sin crear recursión.
9. El horizonte de reintento sólo está concretado para algunas operaciones. No se define si una clave expirada puede reutilizarse ni si existe purga.
10. No está decidido si jobs, outbox, operaciones naturalmente idempotentes o recurrencia deben adoptar el protocolo HTTP.
11. No existe una decisión sobre columnas adicionales, compatibilidad de filas históricas o tratamiento de snapshots que no pueden almacenar secretos ni URLs firmadas.

Estas carencias cambian persistencia, seguridad y comportamiento observable. Por ello no se autoriza código productivo antes de aprobar íntegramente esta adenda.

## 4. Inferencias limitadas

1. “Integral” debe cubrir todo consumidor HTTP ya implementado que exige `Idempotency-Key`; excluir consumidores existentes dejaría el mismo riesgo que `HU-034` pretende cerrar.
2. Los procesos internos con identidad determinista deben demostrar las mismas propiedades de unicidad, rollback y recuperación, pero no deben fingir una cabecera HTTP ni un usuario humano.
3. La respuesta confirmada debe congelarse en forma minimizada. Reconsultar un recurso mutable no demuestra `TEC-IDEM-004` y puede devolver una versión que la solicitud original nunca confirmó.
4. Una reserva dentro de la misma transacción que la mutación elimina la necesidad de abandonar una fila `EN_PROCESO`: antes del commit nada es visible y, después del commit, el resultado ya es terminal.
5. La consulta general de `HU-033` puede satisfacer “autorizados consultan” mediante el filtro `action`; una ruta adicional duplicaría autorización, cursor y minimización sin respaldo documental.
6. Una clave expirada debe seguir siendo un tombstone de no reutilización. Permitir su reutilización convertiría expiración en reejecución arbitraria.

Estas inferencias se convierten en decisiones sólo si la adenda se aprueba íntegramente.

## 5. Frontera exacta

### 5.1 Incluido

- las 29 mutaciones HTTP de la sección 6 que ya exigen `Idempotency-Key` por su implementación y contrato de historia;
- generación recurrente, evaluación de elegibilidad y asignación automática únicamente bajo sus identidades técnicas existentes;
- validación estructural única de `Idempotency-Key` para las 29 mutaciones HTTP;
- scope versionado, canonicalización, SHA-256 y compatibilidad con filas históricas;
- snapshot minimizado de la respuesta confirmada, recuperación sin reejecución y precedencia frente a `If-Match`;
- atomicidad entre mutación, historia/versiones, idempotencia, auditoría, outbox o aviso que ya pertenezca a la operación;
- resolución PostgreSQL de concurrencia y reintentos acotados;
- evento append-only por cada intento conflictivo autorizado;
- consulta de conflictos exclusivamente mediante `HU-033`;
- métricas y logs minimizados;
- migración aditiva mínima de `idempotency_record` necesaria para `TEC-IDEM-004`.

### 5.2 Excluido

- cualquier mutación que no consume clave idempotente y ya tiene convergencia natural, como marcar un aviso leído;
- login, MFA, logout, descarga, consultas y demás operaciones de seguridad o lectura;
- convertir `PUT` o `If-Match` por sí solos en consumidores de `Idempotency-Key` cuando su contrato vigente no la exige;
- cambiar permisos, jerarquía, estados funcionales, validaciones, DTO de entrada o resultado de negocio de una historia cerrada;
- crear recursos funcionales de error, corrida, reconciliación o recuperación;
- cambiar la identidad funcional permanente de generación o reemplazarla por `(scope,key)`;
- adoptar el protocolo HTTP en el Worker, outbox o scheduler;
- reejecutar efectos posteriores ya confirmados, reenviar eventos o reparar filas históricas;
- una API, tabla o pantalla específica de conflictos;
- UI o pruebas de navegador;
- purga física, job de limpieza, partición, cache, broker o integración;
- continuidad, respaldo, restauración, compensación o reconciliación de `HU-035`.

## 6. Inventario exacto de mutaciones cubiertas

Cada fila conserva el permiso, DTO, guarda, status de éxito y resultado funcional de su historia aprobada. `HU-034` sólo transversaliza la recuperación y el conflicto.

| Operación estable | Mutación |
|---|---|
| `PERSON_CREATE` | `POST /api/v1/people` |
| `PERSON_EMPLOYMENT_PATCH` | `PATCH /api/v1/people/{personId}/employment` |
| `PERSON_DEACTIVATE` | `POST /api/v1/people/{personId}/deactivate` |
| `PERSON_REACTIVATE` | `POST /api/v1/people/{personId}/reactivate` |
| `AVAILABILITY_PUT` | `PUT /api/v1/people/{personId}/availability/{date}` |
| `ACCOUNT_CREATE` | `POST /api/v1/users` |
| `ACCOUNT_DEACTIVATE` | `POST /api/v1/users/{userId}/deactivate` |
| `ACCOUNT_REACTIVATE` | `POST /api/v1/users/{userId}/reactivate` |
| `ROLE_ASSIGNMENT_CHANGE` | `POST /api/v1/users/{userId}/role-assignments` |
| `CONFIGURATION_RELEASE_CREATE` | `POST /api/v1/configuration/releases` |
| `CONFIGURATION_RELEASE_PUBLISH` | `POST /api/v1/configuration/releases/{releaseId}/publish` |
| `TASK_DEFINITION_VERSION_CREATE` | `POST /api/v1/task-definitions/{taskCode}/versions` |
| `TASK_DEFINITION_VERSION_PUBLISH` | `POST /api/v1/task-definitions/{taskCode}/versions/{versionId}/publish` |
| `TASK_DEFINITION_DEACTIVATE_NEW` | `POST /api/v1/task-definitions/{taskCode}/deactivate-new` |
| `ACTIVATION_POLICY_PUT` | `PUT /api/v1/task-definitions/{taskCode}/activation-policy` |
| `ELIGIBILITY_POLICY_PUT` | `PUT /api/v1/task-definitions/{taskCode}/eligibility-policy` |
| `EVIDENCE_POLICY_PUT` | `PUT /api/v1/task-definitions/{taskCode}/evidence-policy` |
| `VALIDATION_POLICY_PUT` | `PUT /api/v1/task-definitions/{taskCode}/validation-policy` |
| `GENERATION_REQUEST_CREATE` | `POST /api/v1/generation-requests` |
| `ASSIGNMENT_CORRECTION_CREATE` | `POST /api/v1/obligations/{id}/assignment-corrections` |
| `WORK_PLAN_ENSURE` | `POST /api/v1/plans/{isoYear}/{isoWeek}/ensure` |
| `PLAN_PUBLICATION_CREATE` | `POST /api/v1/plans/{planId}/publications` |
| `FILE_UPLOAD_INTENT_CREATE` | `POST /api/v1/files/upload-intents` |
| `FILE_UPLOAD_COMPLETE` | `POST /api/v1/files/{id}/complete` |
| `EVIDENCE_CONTRIBUTE` | `POST /api/v1/obligations/{id}/evidence` |
| `EVIDENCE_REPLACE` | `POST /api/v1/obligations/{id}/evidence/{itemId}/replacements` |
| `OBLIGATION_CONCLUDE` | `POST /api/v1/obligations/{id}/conclusion` |
| `VALIDATION_DECISION_CREATE` | `POST /api/v1/obligations/{id}/validation-decisions` |
| `VALIDATION_DECISION_REPLACE` | `POST /api/v1/validation-decisions/{id}/replacements` |

Las operaciones técnicas cubiertas son `RECURRING_OCCURRENCE_PROCESS`, `ELIGIBILITY_EVALUATION_CREATE` y `AUTOMATIC_ASSIGNMENT_CREATE`. Conservan el productor, request ID determinista, clave funcional, payload y resultado ya aprobados. No exponen cabecera ni ruta nueva.

Una mutación implementada que no figure en esta sección queda fuera. Incorporarla exigiría corregir y volver a aprobar esta adenda; una coincidencia de patrón técnico no amplía el inventario.

## 7. Contrato de `Idempotency-Key`

Para las 29 mutaciones HTTP:

1. La cabecera es obligatoria y debe aparecer exactamente una vez.
2. El valor debe estar en formato UUID textual `D` (`8-4-4-4-12`), sin espacios iniciales/finales ni internos, y no puede ser `00000000-0000-0000-0000-000000000000`.
3. Los dígitos hexadecimales aceptan mayúsculas o minúsculas; el servidor normaliza con `Guid.ToString("D")` en minúsculas antes de persistir o correlacionar.
4. El nombre de cabecera conserva la regla HTTP case-insensitive. El casing del nombre no crea una clave distinta.
5. Cabecera ausente devuelve `400 IDEMPOTENCY_KEY_REQUERIDA`. Múltiple, vacía, con espacios, no UUID, UUID no canónico o UUID vacío devuelve `400 IDEMPOTENCY_KEY_INVALIDA`.
6. El rechazo estructural no reserva la clave, no consulta recursos y no crea auditoría funcional. La telemetría técnica no incluye el valor.

Las operaciones técnicas no simulan esta cabecera: usan exclusivamente el request ID o clave determinista ya aprobado por su productor.

## 8. Scope exacto

El scope nuevo usa la forma lógica versionada:

```text
idem:v1|api:v1|actor:{actor}|operation:{operation}|resource:{resource}
```

- `actor` es el `actorUserId` canónico `D` para HTTP. Para actor sistema es el código estable `system:{producer}`.
- `operation` es exactamente uno de los códigos de la sección 6.
- `resource` es el ID canónico de la ruta cuando existe; para una versión usa su agregado padre; para disponibilidad usa `personId:date`; para plan usa `LOR-001:isoYear:isoWeek`; para políticas/definiciones usa el `taskCode` canónico y el release o versión padre cuando aplique.
- En una creación sin ID de ruta, `resource` es su padre o identidad funcional aprobada. Si tampoco existe padre, vale `new:LOR-001`. El contenido solicitado nunca se concatena al scope: queda protegido por `request_hash`.

Consecuencias observables:

- la misma clave puede usarse por actores distintos sin colisión ni descubrimiento cruzado;
- la misma clave puede usarse en operaciones, endpoints o recursos distintos porque pertenece a scopes distintos;
- la versión `/api/v1` forma parte del scope; otra versión de API no hereda ni colisiona silenciosamente;
- una operación de creación sin ID no puede reutilizar la misma clave para crear dos recursos distintos dentro del mismo actor y operación: el segundo contenido produce conflicto;
- una clave nunca amplía autoridad, sucursal, jerarquía o visibilidad.

Los scopes históricos no se reescriben. La implementación busca primero el scope vigente de la operación y conserva un adaptador explícito sólo para los scopes históricos realmente producidos por esa misma operación. No se prueba un scope perteneciente a otro actor, recurso u operación.

## 9. Canonicalización y `request_hash`

El hash nuevo es SHA-256 de UTF-8, expresado como 64 caracteres hexadecimales en minúsculas. Su entrada es un envelope semántico `IDEM-CANON-1`:

```json
{
  "schemaVersion": 1,
  "apiVersion": "v1",
  "operation": "OPERATION_CODE",
  "actor": "uuid-o-system-producer",
  "resource": "selector-estable",
  "ifMatch": 7,
  "body": {}
}
```

Reglas de canonicalización:

1. Se valida primero el DTO allowlist propio de la operación. Los campos desconocidos se rechazan; nunca se ignoran para calcular el hash.
2. Los nombres de propiedad se ordenan por comparación ordinal de su representación UTF-16 y se serializan una sola vez sin indentación.
3. El orden de propiedades recibido no importa. El orden de arrays sí importa, salvo que el contrato aprobado de la operación declare expresamente el conjunto sin orden; en ese caso se ordena por su clave canónica antes de serializar.
4. Strings se conservan después de la normalización funcional ya aprobada y se normalizan a Unicode NFC. Casing, espacios internos y puntuación siguen siendo significativos salvo regla funcional expresa.
5. UUID se serializa en `D` minúscula; fechas locales en `yyyy-MM-dd`; instantes como UTC RFC 3339 con siete fracciones; enums y códigos en su forma canónica aprobada.
6. Enteros usan decimal base diez sin signo positivo ni ceros iniciales. Otros números usan el tipo .NET cerrado por el DTO y formato invariante de ida y vuelta; `NaN` e infinitos se rechazan.
7. `true`, `false` y `null` usan literales JSON. Campo omitido y campo `null` son distintos, salvo que el contrato de la operación transforme ambos al mismo valor predeterminado antes del envelope.
8. Path y route values se incluyen mediante `resource`; los demás valores de ruta que alteren intención se incluyen también en `body` normalizado.
9. `If-Match` se incluye como `ifMatch` numérico cuando aplica. Un ETag débil, comodín o inválido se rechaza antes del hash.
10. Se excluyen `correlationId`, cookies, CSRF, cabeceras de transporte, `Content-Type` después de validarlo, IP, User-Agent, timestamps del servidor y valores efímeros generados por SGOL.
11. Para archivos se incluyen sólo metadatos de intención aprobados, IDs, tamaño, tipo y SHA-256 cuando formen parte del comando; nunca binario, secreto, object key, cookie ni URL firmada.

Los hashes históricos se comparan con el algoritmo histórico de su operación. No se recalculan ni migran. Una fila nueva usa exclusivamente `IDEM-CANON-1`; no se intenta comparar un hash nuevo con otro algoritmo sin identificar antes la versión del registro.

## 10. Orden de autorización, replay, guardas e `If-Match`

El orden obligatorio es:

1. autenticar sesión, MFA y CSRF aplicables;
2. validar método, route, media type, cabecera idempotente y sintaxis del DTO;
3. revalidar cuenta, persona, empleo, rol, permiso, sucursal, relación jerárquica y visibilidad actuales;
4. resolver el selector de recurso sin revelar recursos ajenos y construir scope/hash;
5. iniciar la transacción de la operación y buscar/reservar `(scope,key)` bajo autoridad PostgreSQL;
6. si existe resultado terminal con el mismo hash, devolver replay sin reevaluar `If-Match`, estado ni guardas funcionales que ya fueron confirmadas;
7. si existe con otro hash, rechazar y auditar el conflicto;
8. sólo para una clave nueva, evaluar `If-Match`, locks, estado y guardas funcionales de la mutación;
9. confirmar atómicamente mutación, historia, idempotencia, auditoría y efectos transaccionales ya aprobados.

Una solicitud no autenticada, no autorizada o fuera de alcance no reserva, consulta ni descubre una clave. Recurso inexistente, ajeno y fuera de alcance conserva la convergencia anti-IDOR de su historia.

Un replay auténtico debe enviar el mismo `If-Match` original porque éste forma parte del hash. Aunque ese ETag ya sea obsoleto por una mutación posterior, el replay devuelve la respuesta originalmente confirmada. Cambiar u omitir `If-Match` con la misma clave produce `409 IDEMPOTENCY_CONFLICT`, no `412` ni replay.

Una clave nueva con `If-Match` obsoleto devuelve `412 VERSION_CONFLICT`, no se consume y no crea conflicto idempotente. Corregido el ETag, puede reintentarse con la misma clave porque no existió commit terminal.

## 11. Estados y representación observable

Los estados funcionales de `HU-034` significan:

- `ACEPTADA`: la mutación ganó y confirmó exactamente un resultado;
- `RECUPERADA`: la petición autorizada encontró el mismo scope, clave y hash terminales y devolvió el resultado confirmado sin reejecutarlo;
- `RECHAZADA`: la petición autorizada reutilizó el mismo scope y clave con otro hash; devuelve `409 IDEMPOTENCY_CONFLICT`, genera auditoría y no crea un segundo recurso.

Estos valores aparecen sólo donde el DTO aprobado de la operación ya tiene un campo `result`. `HU-034` no agrega ese campo a los demás DTO. Para ellos, aceptación y recuperación se observan por el mismo status, payload, ID y headers semánticos; el conflicto se observa mediante Problem Details y auditoría.

El status interno persistido nuevo es `COMPLETED`, equivalente técnico de `COMPLETADA`. `COMPLETADA` permanece como alias terminal histórico aceptado. No se reescriben filas existentes.

`EN_PROCESO` es un estado lógico dentro de la transacción y nunca se confirma aisladamente. `FALLIDA` describe el intento técnico fallido en telemetría o corrida ya existente, pero no se confirma como `idempotency_record`: una falla previa al commit revierte la reserva. Por ello no existe fila abandonada, propietario de takeover ni timeout de apropiación en `HU-034`.

Errores `400`, `401`, `403`, `404`, `409` funcional distinto, `412`, `413`, `415`, `422`, `423`, `429`, `500` o `503` no consumen una clave salvo que el contrato previo de una operación ya haya aprobado expresamente un resultado funcional terminal recuperable. Esta adenda no convierte fallos transitorios en éxitos almacenados.

## 12. Respuesta original y replay

Cada commit nuevo guarda una proyección de replay minimizada con:

- status HTTP original;
- `data` semántico original necesario para reproducir ID, versión, estado y valores confirmados;
- ETag original cuando aplica;
- `Location` original cuando aplica;
- código de operación y versión del protocolo.

No se guarda el envelope completo ni `meta.correlationId`. Cada replay usa el `correlationId` de la petición actual. Tampoco se guardan `Date`, cookies, CSRF, secretos, credenciales, TOTP, códigos de recuperación, URLs firmadas, object keys, binarios ni contenido íntegro de evidencia.

El replay devuelve el mismo status HTTP, DTO semántico, ID, versión, ETag y `Location` confirmados. Si el DTO aprobado posee un marcador de recuperación, sólo ese marcador cambia a `RECUPERADA`; el resto proviene del snapshot. No se consulta el estado mutable actual para reconstruir la respuesta.

`FILE_UPLOAD_INTENT_CREATE` es la única excepción efímera: se congela la identidad y metadatos seguros, pero no la URL firmada. Antes de `uploadExpiresAt`, un replay puede emitir una autorización equivalente para el mismo objeto exacto. Después devuelve `410 INTENCION_CARGA_EXPIRADA`; nunca crea otro objeto con la misma clave.

Una fila histórica sin snapshot conserva el replay previamente aprobado de su historia y se marca como protocolo legado. No se inventa payload ni se hace backfill. Si sus vínculos ya no permiten una reconstrucción segura, falla cerrado con `409 IDEMPOTENCY_REPLAY_NO_DISPONIBLE`, sin reejecutar ni alterar la fila; el caso se registra sólo en telemetría técnica minimizada.

## 13. Persistencia adicional mínima

Se autoriza una migración aditiva de `idempotency_record` con columnas nullable:

| Columna | Tipo | Uso |
|---|---|---|
| `protocol_version` | `smallint` | `NULL` para legado; `1` para `IDEM-CANON-1` |
| `response_payload` | `jsonb` | Proyección segura de `data` necesaria para replay |
| `response_etag` | `text` | ETag fuerte original, si aplica |
| `response_location` | `text` | Location relativa original, si aplica |

Para filas `protocol_version = 1`, `response_payload` es obligatorio, `request_hash` conserva 64 hex minúsculos y `status = COMPLETED`. `response_location`, cuando existe, debe ser una ruta relativa local `/api/v1/...`; no admite host, esquema ni URL firmada.

La migración no actualiza filas históricas, no cambia su status, hash, expiración, scope o recurso y no crea índice adicional: la PK `(scope,key)` sigue siendo la autoridad. `Down()` queda bloqueado conforme a la regla fija de migraciones del proyecto y el archivo generado no conserva BOM.

## 14. Atomicidad y fallos

Para una clave nueva, la misma transacción PostgreSQL contiene, según la operación:

- reserva lógica y confirmación terminal de idempotencia;
- cambio funcional;
- versiones sucesoras o historia;
- auditoría de éxito;
- outbox o aviso interno que el contrato previo ya exija;
- snapshot de replay.

Si falla cualquier parte antes del commit, se revierten todas. No quedan recurso, sucesora, cambio de estado, idempotencia, auditoría de éxito, outbox, aviso ni snapshot parciales. La misma clave puede reintentarse porque no existe resultado confirmado.

Si PostgreSQL confirma el commit y la respuesta se pierde, cancela o agota timeout, el resultado ya es terminal. El servidor no intenta compensarlo ni borrarlo. El siguiente intento autorizado con el mismo scope/clave/hash devuelve el snapshot: éste es el caso obligatorio de `TEC-IDEM-004`.

La cancelación del cliente sólo cancela mientras el commit no haya sido enviado. Desde el inicio del commit se usa un token interno acotado para determinar su resultado; si queda incierto, no se reejecuta fuera del protocolo y el cliente reintenta con la misma clave.

## 15. Concurrencia y retry

1. La PK `(scope,key)` decide el ganador. Consultar antes de insertar es sólo una optimización y nunca la protección final.
2. Misma clave y mismo hash concurrentes: exactamente un intento confirma; los demás esperan o capturan la violación única, limpian su ChangeTracker, releen el ganador y recuperan el mismo resultado.
3. Misma clave y hashes distintos: el primer commit válido gana. Cada perdedor autorizado devuelve `409 IDEMPOTENCY_CONFLICT` y registra su propio evento; ninguno sobrescribe la fila ganadora.
4. Claves distintas contra la misma unicidad funcional: la restricción funcional permanente decide. Sólo se recupera el recurso ganador cuando el contrato funcional de la operación declara equivalencia exacta; en otro caso se devuelve su conflicto funcional, no un falso replay idempotente.
5. Son reintentables SQLSTATE `40001`, `40P01` y `23505` exclusivamente cuando la restricción concreta corresponde a idempotencia o a una unicidad funcional aprobada y el ganador se revalida exactamente.
6. El máximo es tres intentos totales con backoff acotado y jitter. Agotado el máximo se conserva el error de concurrencia aprobado por la operación o `409 IDEMPOTENCY_CONCURRENCY_CONFLICT` si el conflicto es exclusivamente transversal.
7. Una colisión de PK de auditoría, otra FK/check/unique o un error desconocido nunca se interpreta como replay.

Las pruebas concurrentes usan PostgreSQL real. Ninguna prueba con proveedor en memoria o SQLite demuestra estas propiedades.

## 16. Expiración y retención

- Generación manual y recurrente conserva permanentemente su clave funcional y su idempotencia.
- Todas las mutaciones confirmadas que crean o modifican identidad, configuración, plan, asignación, conclusión, evidencia o validación conservan el tombstone idempotente hasta el fin de retención de la historia funcional; en el MVP se representa con `DateTimeOffset.MaxValue`.
- `FILE_UPLOAD_INTENT_CREATE` conserva `expires_at = created_at + 24 horas`, que es su horizonte de replay. Expirar invalida la autorización de carga, no libera la clave.
- Las demás operaciones de archivo confirmadas son permanentes en el MVP.
- Todos los cálculos usan UTC.
- `expires_at` no autoriza eliminación física, reutilización, scheduler, purga o partición. No se implementa limpieza en `HU-034`.

Una clave expirada vuelve a la respuesta terminal segura de su operación, como `410` para intención de carga, o falla cerrado; nunca reejecuta la mutación ni crea otro recurso.

## 17. Auditoría del conflicto

Cada intento HTTP autenticado, autorizado y conflictivo crea exactamente un `audit_event` en una transacción corta separada de la mutación ya rechazada:

| Campo | Valor |
|---|---|
| `action` | `IDEMPOTENCY_CONFLICT_REJECTED` |
| `resourceType` | Tipo del recurso ganador o agregado objetivo aprobado |
| `resourceId` | ID del resultado ganador cuando existe; en otro caso, ID del agregado padre |
| `actorUserId` / `actorType` | Actor vigente / `USER`; para productor técnico, `NULL` / `SYSTEM` |
| `branchId` | `LOR-001` |
| `correlationId` | Correlación de la petición conflictiva actual |
| `requestId` | UUID idempotente canónico conflictivo |
| `beforeData` | `null` |
| `afterData` | Sólo `operation`, `protocolVersion` y `reasonCode` |
| `reason` | `null` |
| `outcome` | `REJECTED` |

`reasonCode` vale `IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_CONTENT`. No se guarda cuerpo, hash de solicitud, ETag, secreto, cookie, token, IP, URL, binario ni contenido de evidencia. La cabecera completa sólo ocupa `requestId`, que `HU-033` no expone en su DTO minimizado.

Cada intento conflictivo crea un evento; no se deduplica porque representa una petición distinta. La auditoría no consume `Idempotency-Key`, no llama al coordinador idempotente y no genera auditoría de sí misma, evitando recursión.

Si el evento no puede confirmarse, la respuesta es `500 IDEMPOTENCY_CONFLICT_AUDIT_FAILED`; la mutación sigue sin ejecutarse y la fila ganadora no cambia. No se devuelve `409` afirmando que el conflicto quedó registrado cuando no fue así.

## 18. Consulta autorizada de conflictos

No se crea ruta, DTO, permiso, índice ni lector nuevo. Los conflictos se consultan mediante:

```text
GET /api/v1/audit-events?from={utc}&to={utc}&action=IDEMPOTENCY_CONFLICT_REJECTED
```

Aplican exactamente `PER-AUDITORIA-VER`, jerarquía, anti-IDOR, período, cursor, snapshot, minimización, `404` de detalle y no efectos de `F07_ADENDA_29`. Dirección ve eventos globales de actor sistema conforme a `HU-033`; los demás roles sólo ven conflictos cuyo sujeto funcional esté dentro de su alcance.

`HU-034` no modifica el contrato de lista o detalle de auditoría. “Autorizados consultan” queda satisfecho por el filtro ya aprobado de `HU-033`.

## 19. Jobs, outbox y recurrencia

El Worker, `scheduled_job_run` y `outbox_event` conservan sus IDs, locks, checkpoints, número de intento y reglas de entrega ya aprobados. No reciben scope HTTP, response snapshot ni cabecera.

La generación recurrente sí participa como productor técnico porque ya posee clave determinista y unicidad funcional permanente. Debe demostrar mismo resultado para la misma ocurrencia y conflicto para contenido incompatible según su contrato vigente; no cambia horario, calendario, catálogo TAR, omisión de día inhábil ni materialización.

Evaluación de elegibilidad y asignación automática conservan sus request IDs y vínculos exactos. `HU-034` sólo exige que una repetición no duplique snapshot o asignación y que una incompatibilidad no se trate como éxito.

Procesar dos veces el mismo `outbox_event` o retomar un job no queda convertido en una nueva mutación funcional ni autoriza reenviar un efecto ya confirmado. Reparación, takeover, replay administrativo y reconciliación pertenecen a operación o `HU-035`.

## 20. Telemetría, minimización y abuso

Se emiten métricas de baja cardinalidad:

- `idempotency_requests_total{operation,outcome}` con `outcome = accepted|recovered|rejected|failed`;
- `idempotency_retries_total{operation,sqlstate_class}`;
- `idempotency_replay_unavailable_total{operation,protocol}`;
- duración del camino accepted/recovered/rejected por operación.

No son labels ni logs: key, scope completo, actor ID, resource ID, request hash, ETag, correlation ID, ruta con IDs, body, nombre de archivo o dato personal. Los logs contienen como máximo operación, outcome, protocolo, intento, clase SQLSTATE y `correlationId` conforme a la política general.

`HU-034` no agrega un bucket ilimitado para rechazos: sólo una aceptación confirmada crea la fila. Conflictos crean auditoría porque `CP-034-N` lo exige y quedan sujetos al rate limit vigente de la mutación y a los controles de sesión. No se inventa una cuota funcional o permiso nuevo.

El hash no se presenta como anonimización: permanece dato técnico interno, no se expone por API ni telemetría y nunca sustituye la minimización del snapshot.

## 21. Compatibilidad y estrategia de implementación

1. Se reutilizan `IdempotencyRecord`, su PK, `SgolDbContext.IdempotencyRecords`, `AuditTransaction`, restricciones funcionales, transacciones y retries existentes.
2. Puede extraerse un coordinador transversal en Infrastructure para validar scope/hash, persistir snapshot y recuperar ganador, pero cada módulo conserva autorización, guardas, locks, respuesta y autoridad sobre sus tablas.
3. El dominio no depende de ASP.NET Core, EF Core ni del coordinador. Los endpoints sólo adaptan cabecera, ETag, DTO y Problem Details.
4. Los productores se migran de forma explícita operación por operación. No se usa reflexión, middleware que capture cuerpos o respuestas indiscriminadamente ni serialización de secretos.
5. Cada adaptador histórico queda probado con filas sintéticas reales de su propio formato. No existe fallback que pruebe scopes arbitrarios.
6. No se cambia un error funcional existente a `IDEMPOTENCY_CONFLICT` salvo reutilización real de la misma clave y scope con otro hash.

## 22. Fronteras con historias y núcleos cerrados

- `HU-014`/`HU-015` y `RN-010`: generación manual es el caso transversal de `CP-034-P`; conserva tanto clave idempotente como unicidad funcional permanente. Ninguna sustituye a la otra.
- `HU-013`: recurrencia conserva su UUID determinista, día inhábil, resultado y job existentes; no adopta cabecera HTTP.
- `HU-019` a `HU-028`: cada mutación conserva autoridad, ETag, jerarquía, snapshot, versión, estado, motivo y errores aprobados. `HU-034` sólo fija replay/conflicto transversal.
- `TECH-AUD-001`: no cambian entidad, configuración, índices o trigger append-only. Sólo se añade el productor de conflicto usando el núcleo existente.
- `HU-033`: no cambian rutas, permiso, DTO, filtros, cursor, alcance o minimización. Se reutiliza `action` para consultar conflictos.
- `TECH-JOBS-001`: no cambian Worker, outbox, locks, scheduler, retry operativo ni checkpoint.
- `HU-035`: no se implementan recuperación, continuidad, respaldo, restauración, compensación, reparación ni reconciliación.

## 23. Pruebas obligatorias

La implementación deberá demostrar, al menos:

1. las 29 rutas rechazan cabecera ausente, múltiple, vacía, con espacios, no UUID, UUID no canónico y UUID vacío con el código exacto;
2. casing del nombre de cabecera y casing hexadecimal del UUID convergen a la misma clave;
3. actor, operación, versión API y recurso pertenecen al scope exacto y no conceden acceso cruzado;
4. orden distinto de propiedades produce el mismo hash; número, Unicode, null, omitido, unknown field, route e `If-Match` siguen las reglas de la sección 9;
5. cada productor nuevo escribe `protocol_version = 1`, snapshot minimizado, status, ETag y Location aplicables;
6. replay devuelve status, DTO semántico, ID, versión, ETag y Location originales con `correlationId` actual y sin segunda mutación o auditoría de éxito;
7. un recurso modificado después no altera el snapshot recuperado;
8. una URL firmada nunca se persiste y la intención de carga recupera autorización equivalente sólo dentro de su vigencia;
9. filas históricas `COMPLETED` y `COMPLETADA` se recuperan sólo mediante el adaptador propio; ausencia insegura falla cerrada sin backfill ni reejecución;
10. autorización se revalida antes del lookup y una clave no permite IDOR, cruce de actor, jerarquía o sucursal;
11. replay con el `If-Match` original funciona aunque sea hoy obsoleto; otro ETag con la misma clave da `409`; clave nueva obsoleta da `412` sin consumirla;
12. `TEC-IDEM-001`: 20 solicitudes concurrentes misma clave/cuerpo producen un recurso y todas el mismo ID;
13. `TEC-IDEM-002`: misma clave/cuerpos distintos produce un ganador, `409` en perdedores, un evento por intento y ningún segundo recurso;
14. `TEC-IDEM-003`: falla después de autorización/validación/reserva y antes de commit deja cero cambios visibles;
15. `TEC-IDEM-004`: commit confirmado con respuesta perdida se recupera sin reejecutar;
16. `40001`, `40P01` y la `23505` exacta reintentan como máximo tres veces; otras restricciones no se confunden con replay;
17. claves distintas contra unicidad funcional conservan el resultado propio del dominio y una sola obligación bajo `RN-010`;
18. generación manual conserva una sola `generation_request` y `work_obligation`; recurrencia conserva su identidad permanente;
19. conflicto auditado contiene exclusivamente los campos de la sección 17 y una falla de auditoría devuelve `500` sin mutación;
20. `GET /api/v1/audit-events` con la acción exacta devuelve sólo conflictos autorizados conforme a `HU-033` y la lectura no produce efectos;
21. ninguna respuesta, snapshot, auditoría, log o métrica contiene secretos, cookies, TOTP, códigos, URL firmada, object key, binario o contenido íntegro de evidencia;
22. expiración de upload intent produce `410` y nunca libera la clave; no existe purga o scheduler nuevo;
23. jobs y outbox existentes no adoptan el protocolo HTTP ni reenvían efectos confirmados;
24. migración aditiva conserva filas históricas, PK y datos; `Down()` está bloqueado y el archivo no tiene BOM;
25. arquitectura confirma límites modulares, Domain sin dependencias de infraestructura, inventario cerrado y ausencia de UI/HU-035;
26. cada prueba negativa verifica ausencia de recurso, versión, transición, idempotencia parcial, auditoría de éxito, outbox o aviso no autorizado;
27. PostgreSQL real ejecuta `TEC-IDEM-001` a `TEC-IDEM-004` y las carreras representativas de cada familia de productor.

Las pruebas enfocadas locales se ejecutan durante el desarrollo. La suite PostgreSQL consolidada la ejecuta el desarrollador fuera de la sesión al llegar al gate, conforme a las reglas del proyecto.

## 24. Gates, trazabilidad y eficacia

Después de la aprobación íntegra se ejecutarán, durante el desarrollo, sólo pruebas enfocadas. Al final se ejecutarán una sola vez:

```text
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release
dotnet format --verify-no-changes
```

También se comprobarán arquitectura, API/contrato, seguridad, migración, vulnerabilidades NuGet, espejo y protección de `Fuentes/`, secretos, `git diff --check` y archivos nuevos. Cuando los gates locales estén listos se solicitará una sola corrida externa consolidada de PostgreSQL.

La implementación actualizará `docs/traceability/IMPLEMENTATION_STATUS.md` en el mismo cambio como `Propuesta implementada`, nunca como `Terminada`. Presentar esta adenda no exige actualizar ese registro: aún no existe implementación ni evidencia de cierre.

Esta propuesta no autoriza commit, push, publicación de rama, pull request o merge. Cada autorización se solicitará por separado.

`HU-034` sólo será `Terminada` después de commit exacto, pipeline requerido exitoso sobre ese commit, aprobación humana, merge, ascendencia verificada en `origin/master`, PostgreSQL externo satisfactorio, protección de `Fuentes/` y cero defectos bloqueantes.

## 25. Preguntas cerradas por la propuesta

La aprobación íntegra responderá conjuntamente:

1. que el inventario exacto son las 29 mutaciones HTTP y tres productores técnicos de la sección 6;
2. que jobs, outbox, lecturas y mutaciones naturalmente idempotentes quedan fuera del protocolo HTTP;
3. que generación manual participa en los cuatro `TEC-IDEM` y conserva su unicidad funcional; recurrencia conserva su identidad propia;
4. que la cabecera usa una ocurrencia, UUID `D` no vacío, sin espacios y normalización a minúsculas;
5. que scope, actores, recursos aún no creados, cruce de actor/recurso/operación y versión API siguen la sección 8;
6. que `IDEM-CANON-1`, SHA-256, Unicode, números, null, omitidos, unknown fields, path, actor, `If-Match` y metadatos siguen la sección 9;
7. que autorización precede al lookup y un replay auténtico precede a la reevaluación de ETag/estado ya confirmados;
8. que status, DTO, ID, versión, ETag y Location originales se congelan; sólo `correlationId` y el marcador aprobado de recuperación cambian;
9. que no se persisten secretos, binarios ni URLs firmadas;
10. que `COMPLETED` es el terminal nuevo, `COMPLETADA` sigue como alias legado y no se confirma `EN_PROCESO` o `FALLIDA` aislado;
11. que una falla antes del commit revierte todo y un commit con respuesta perdida se recupera por snapshot;
12. que PostgreSQL elige ganador, los retries son tres y sólo aplican a SQLSTATE/restricción exactos;
13. que cada intento conflictivo autorizado produce un evento `IDEMPOTENCY_CONFLICT_REJECTED` y una falla de auditoría impide afirmar `409` registrado;
14. que los conflictos se consultan exclusivamente por `HU-033`, sin ruta, DTO o permiso nuevo;
15. que generación y mutaciones históricas son permanentes, upload intent usa 24 horas como horizonte y ninguna expiración libera la clave;
16. que se agregan sólo cuatro columnas nullable, sin backfill ni índice nuevo;
17. que métricas y logs son de baja cardinalidad y minimizados;
18. que no existe UI ni se adelanta `HU-035`.

## 26. Decisiones solicitadas y aprobación íntegra

Se solicita aprobar o rechazar `F07_ADENDA_30` como una unidad. No existe aprobación parcial implícita. Si cualquier decisión no es aceptable, debe corregirse esta propuesta antes de producir código.

La pregunta de aprobación es: **¿se aprueba íntegramente `F07_ADENDA_30_CONTRATO_DE_IDEMPOTENCIA_INTEGRAL_Y_CONFLICTOS_HU_034.md`, sin cambios, para autorizar la implementación local de `HU-034` bajo este contrato?**
