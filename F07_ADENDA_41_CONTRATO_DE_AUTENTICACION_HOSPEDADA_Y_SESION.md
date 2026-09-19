# F07 Adenda 41 — Contrato de autenticación hospedada y sesión

## 1. Control de la propuesta

| Campo | Valor |
|---|---|
| Identificador propuesto | `TECH-AUTH-001` — Autenticación hospedada, primer acceso, MFA TOTP y sesión segura |
| Estado | PROPUESTA INDIVISIBLE; `TECH-AUTH-001` no existe como tarea formal ni está vigente mientras no exista aprobación humana íntegra de esta adenda |
| Fecha | 2026-09-17 |
| Base local | Rama `codex/hu-035`, commit `84bfecf2d57723990303431108f21efee58aeaf5` |
| Trazabilidad principal | `CAP-006`, `HU-006`, `HU-007`, `RN-002`, `RN-006`, `CA-006`, `CA-007`, `TECH-ID-BOOT-001`, `ADR-004`, `ADR-012`, `NFR-001`, `NFR-004`, `NFR-010` |
| Alcance | Endpoints funcionales de autenticación local hospedada en `Sgol.Web`: cookie de mismo origen, contraseña temporal, MFA TOTP, recovery codes, bloqueo, sesión y logout |
| Exclusiones | Razor Pages, HTML, CSS, componentes o decisiones de diseño; OAuth/OIDC externo, SSO, JWT persistente, Redis, SPA separada, impersonación, administración general de sesiones y ampliación de permisos |

Esta adenda no modifica los documentos F00–F07 congelados ni `Fuentes/`. Su aprobación íntegra insertaría `TECH-AUTH-001` como tarea técnica local y autorizaría su implementación y validación local proporcional. No autoriza push, PR, merge, publicación ni despliegue.

## 2. Hechos documentados y observados

1. F06 exige credenciales locales, MFA TOTP obligatorio, recovery codes de un solo uso, cookie `Secure`, `HttpOnly`, `SameSite=Strict`, CSRF en mutaciones, inactividad de 30 minutos y máximo absoluto de ocho horas.
2. `TECH-ID-BOOT-001` crea una sola vez la primera persona, empleo, cuenta `DIRECCION`, credencial y rol en una transacción auditada. La cuenta queda con `MustChangePassword=true`, `MfaEnrolledAt=null` y contraseña hasheada mediante `IPasswordHasher<AppUser>`.
3. `HU-006` reutiliza `AppUser` e `IdentityCredential`; crea cuentas individuales activas con contraseña temporal, y al reactivar cambia el hash, marca `MustChangePassword=true` y rota `SecurityStamp`. Desactivar también rota el sello.
4. `HU-007` conserva un único `RoleAssignmentVersion` activo entre `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION` y `PISO_VENTAS`. Cambiar o revocar el rol vigente rota `SecurityStamp`; puesto y turno no conceden autoridad.
5. `Program.cs` registra antiforgery, pero no registra `AddAuthentication`, una cookie de autenticación, validación del principal, rate limiting, `UseAuthentication` ni `UseAuthorization`.
6. Los endpoints protegidos actuales comprueban una identidad autenticada y extraen `ClaimTypes.NameIdentifier`. Los servicios de negocio vuelven a consultar PostgreSQL para cuenta, rol, empleo, `LOR-001`, vigencia, jerarquía y recurso. El endpoint de catálogo de sucursal sólo comprueba que exista una identidad autenticada y depende, por tanto, de que la cookie sea validada contra el estado persistido.
7. La persistencia actual no contiene secreto TOTP, recovery codes, desafíos, contador de fallos, bloqueo ni prevención de reutilización de un paso TOTP. `MfaEnrolledAt` por sí solo no demuestra un enrolamiento real.
8. Los tests históricos que fabrican `ClaimsPrincipal` o asignan `MfaEnrolledAt` no demuestran login hospedado, MFA real ni emisión de cookie.

## 3. Ambigüedades y contradicciones que resuelve la propuesta

F06 enumera rutas generales y políticas, pero no fija DTO cerrados, estados del primer acceso, vida de desafíos, nombre y renovación de cookies, claims, validación de `SecurityStamp`, persistencia TOTP, consumo concurrente de recovery codes, semántica exacta del bloqueo, invalidación por empleo, códigos de error, auditoría, telemetría ni procedimiento sintético.

Además, existe una tensión que no puede resolverse silenciosamente: el enrolamiento TOTP requiere entregar al cliente un secreto u `otpauth://` y los recovery codes deben entregarse una vez, mientras la regla general prohíbe secretos en respuestas. Esta propuesta establece la única excepción: el secreto TOTP pendiente y los recovery codes nuevos pueden aparecer exclusivamente en la respuesta API autorizada y `no-store` que los crea, una sola vez y antes de persistirlos como credenciales activas o hashes. Nunca vuelven a aparecer en respuestas posteriores, logs, auditoría, métricas, excepciones, fixtures ni Git.

Las secciones siguientes son una decisión propuesta indivisible. Nada adquiere vigencia por estar redactado aquí.

## 4. Resultado exacto de `TECH-AUTH-001`

La tarea entregaría únicamente:

1. Servicios de autenticación dentro del módulo `Identity`, adaptadores ASP.NET Core en `Sgol.Web` y cookie de mismo origen.
2. Ciclo de login, contraseña temporal, enrolamiento y desafío TOTP, recovery codes, sesión y logout.
3. Reset MFA administrado por Dirección para recuperación ordinaria; el break-glass de la última cuenta Dirección permanece en el runbook técnico y no se convierte en endpoint público.
4. Persistencia expand-only, configuración EF y migración forward-only para MFA, recovery codes, desafíos y bloqueo.
5. Pruebas unitarias, de componente HTTP, PostgreSQL y smoke HTTPS real con los cuatro roles canónicos.
6. Procedimiento local reproducible que usa datos sintéticos y secretos externos al repositorio.
7. Actualización de trazabilidad y documentación local de ejecución de la API.

No se modifica la semántica funcional de los endpoints existentes ni se agrega permiso a ningún rol.

## 5. Superficie API exacta

Todas las entradas JSON son objetos cerrados: propiedades desconocidas, duplicadas, nulas donde no se admitan o fuera de longitud devuelven `400 DATOS_AUTENTICACION_INVALIDOS`. Toda respuesta lleva `correlationId`; todo error usa `application/problem+json`; todas las respuestas de autenticación llevan `Cache-Control: no-store` y `Pragma: no-cache`.

### 5.1 CSRF

1. `GET /api/v1/auth/csrf` no exige sesión. Devuelve `200` con `{ "data": { "requestToken": string }, "meta": { "correlationId": string } }` y establece la cookie antiforgery `__Host-SGOL-CSRF`.
2. El token de solicitud se envía en `X-CSRF-TOKEN`. No es credencial de autenticación y no se registra.
3. Toda operación HTTP insegura (`POST`, `PUT`, `PATCH`) basada en cookie de sesión o preautenticación exige antiforgery, incluidas login y logout. Faltante o inválido devuelve `400 CSRF_INVALID` sin ejecutar servicio ni producir cambios funcionales.

### 5.2 Login y primer acceso

1. `POST /api/v1/auth/login` recibe `{ "userName": string, "password": string }`.
   - Credenciales válidas crean un desafío persistido y la cookie corta de preautenticación; todavía no crean el principal de sesión.
   - Devuelve `200` con `nextStep` igual a `CHANGE_PASSWORD`, `ENROLL_MFA` o `VERIFY_MFA`, `challengeExpiresAt` UTC y la ruta siguiente.
   - Usuario inexistente, cuenta/persona/empleo inactivo, sucursal inválida, rol ausente/múltiple/no canónico o contraseña incorrecta devuelven exactamente `401 AUTHENTICATION_FAILED` con el mismo título y cuerpo observable.
   - Una cuenta bloqueada sólo devuelve `423 ACCOUNT_LOCKED` después de comprobar una contraseña correcta; una contraseña incorrecta conserva la respuesta genérica y no permite enumerar usuarios.
2. `POST /api/v1/auth/password/change` recibe `{ "currentPassword": string, "newPassword": string }`.
   - En primer acceso exige desafío vigente `CHANGE_PASSWORD` y vuelve a comprobar la contraseña temporal.
   - En una sesión normal exige MFA completado hace no más de cinco minutos.
   - Cambia el hash, pone `MustChangePassword=false`, rota `SecurityStamp`, consume los demás desafíos y elimina todas las cookies anteriores. Devuelve un nuevo desafío y `nextStep=ENROLL_MFA` cuando no existe TOTP activo, o `nextStep=VERIFY_MFA` cuando ya existe.
3. `POST /api/v1/auth/mfa/enroll` recibe `{}` y exige desafío vigente `ENROLL_MFA`.
   - Genera un secreto pendiente nuevo; nunca reutiliza uno vencido.
   - Devuelve una sola vez `{ "manualKey": string, "otpauthUri": string, "expiresAt": instant }`. La respuesta es la excepción limitada de la sección 3.
   - No cambia `MfaEnrolledAt` ni habilita sesión.
4. `POST /api/v1/auth/mfa/confirm` recibe `{ "totpCode": string }` y exige el mismo desafío de enrolamiento.
   - Confirma el secreto pendiente, revoca cualquier TOTP anterior, persiste la credencial protegida, fija `MfaEnrolledAt`, rota `SecurityStamp`, crea diez recovery codes y consume el desafío en una transacción.
   - Devuelve una sola vez `{ "recoveryCodes": [string], "nextStep": "RECOVERY_CODES" }` y emite la cookie de sesión. Nunca vuelve a poder consultarse el texto de esos códigos.
5. `POST /api/v1/auth/mfa/verify` recibe exactamente uno de `{ "totpCode": string }` o `{ "recoveryCode": string }` y exige desafío vigente `VERIFY_MFA`.
   - TOTP válido consume el desafío y emite sesión.
   - Recovery code válido se consume una sola vez bajo lock transaccional y devuelve `nextStep=REGENERATE_RECOVERY_CODES`; mantiene una preautenticación restringida que sólo permite regenerar códigos o cerrar el flujo. No permite endpoints de negocio todavía.
6. `POST /api/v1/auth/recovery-codes/regenerate` recibe `{ "currentPassword": string }`.
   - Exige contraseña correcta y MFA reciente de no más de cinco minutos, o el desafío restringido creado por un recovery code consumido.
   - Revoca los códigos anteriores, crea diez nuevos hashes, rota `SecurityStamp`, consume desafíos y devuelve una sola vez los nuevos códigos. Emite una sesión plena nueva.

### 5.3 Sesión y logout

1. `GET /api/v1/auth/session` exige cookie plena válida. Devuelve `200` con `userId`, `personId`, `userName`, `displayName`, `branchCode=LOR-001`, `roleCode`, `permissions`, `mfaAuthenticatedAt`, `idleExpiresAt` y `absoluteExpiresAt`; no devuelve sello, hash, secreto, desafío ni códigos.
2. `permissions` es una proyección informativa calculada por un lector compartido a partir de los contratos y resolutores vigentes. No se persiste, no se acepta desde el cliente y no sustituye las comprobaciones de los servicios. El servicio de autenticación no mantiene una matriz paralela.
3. `POST /api/v1/auth/logout` exige CSRF, elimina `__Host-SGOL-Session` y `__Host-SGOL-PreAuth` y devuelve `204`. Es idempotente para una cookie ausente o ya expirada.
4. No existe listado de sesiones, cierre remoto selectivo, “recordarme”, refresh token, access token ni JWT de navegador.

### 5.4 Recuperación administrada

1. `POST /api/v1/users/{userId}/mfa-reset` exige sesión Dirección vigente, `PER-USUARIO-ADMIN`, `Idempotency-Key` y cuerpo `{ "reason": string, "temporaryPassword": string }`.
2. El servidor vuelve a consultar cuenta, persona, empleo, rol y `LOR-001`; no usa el claim de rol como autorización.
3. En una transacción cambia el hash a la contraseña temporal, fija `MustChangePassword=true`, revoca TOTP y recovery codes activos, pone `MfaEnrolledAt=null`, rota `SecurityStamp`, consume desafíos y audita el motivo. Devuelve el `AccountSummary` existente sin contraseña ni secretos.
4. La operación no desbloquea mediante una vía separada: el reset administrado deja contador y bloqueo en cero; fuera de él, el desbloqueo es automático al vencer `lockoutEndUtc`.

## 6. Cookies, expiración y renovación

### 6.1 Cookie plena

- Nombre: `__Host-SGOL-Session`.
- `Secure=Always`, `HttpOnly=true`, `SameSite=Strict`, `Path=/`, sin `Domain`.
- Ticket cifrado y firmado por ASP.NET Core Data Protection con el key ring PostgreSQL existente.
- Inactividad: 30 minutos. Renovación deslizante sólo cuando hayan transcurrido al menos 15 minutos desde la última emisión válida.
- Máximo absoluto: ocho horas desde el MFA que creó la sesión. La renovación nunca mueve ese límite.
- No existe degradación a HTTP en local: el smoke usa HTTPS local.

### 6.2 Cookie de preautenticación

- Nombre: `__Host-SGOL-PreAuth`.
- Mismos atributos de seguridad; expiración absoluta de diez minutos, sin renovación.
- Contiene únicamente el ID opaco del desafío y protección de Data Protection. No contiene usuario, contraseña, TOTP, secreto pendiente ni recovery code.
- Nunca es un principal autorizado para endpoints de negocio.

### 6.3 Invalidación

En cada petición con cookie plena, antes de construir o conservar el principal, el servidor consulta PostgreSQL y exige simultáneamente:

1. `AppUser` existente y `ACTIVA`;
2. persona existente;
3. exactamente un empleo actual `ACTIVA` en `LOR-001`;
4. exactamente un `RoleAssignmentVersion` actual `ACTIVO`, canónico y en `LOR-001`;
5. `MustChangePassword=false`;
6. `MfaEnrolledAt` y una credencial TOTP activa coherentes;
7. `SecurityStamp` igual al snapshot protegido de la cookie;
8. MFA completado y máximo absoluto no vencido.

Fallar cualquier condición rechaza el principal, elimina cookies y devuelve `401 SESSION_INVALID` o redirige a login. Esta validación ocurre en cada petición, no en un intervalo, para que pérdida de empleo, desactivación o cambio de rol invaliden la siguiente solicitud. Cambio de contraseña, desactivación/reactivación, cambio/revocación de rol, reset MFA y enrolamiento/regeneración rotan además `SecurityStamp` dentro de su transacción.

## 7. Claims mínimos

El principal se crea únicamente después de contraseña válida, MFA completo y validación persistida. Contiene:

- `ClaimTypes.NameIdentifier`: UUID canónico `D` de `AppUser.Id`;
- `sgol:security_stamp`: snapshot protegido usado sólo por validación de cookie;
- `amr=mfa` y `sgol:mfa_at` UTC;
- `sgol:role`: snapshot informativo del rol canónico.

No se emite `ClaimTypes.Role`, lista de permisos, puesto, turno, contraseña, persona detallada ni secreto. `sgol:role` sirve únicamente para presentación y se actualiza al reemitir la cookie; nunca satisface por sí solo autorización. Los endpoints de negocio siguen recibiendo sólo `NameIdentifier` y sus servicios vuelven a consultar PostgreSQL.

## 8. Contraseña temporal y política

1. Contraseñas entre 14 y 128 caracteres; se permiten frases largas, pegar y gestores. No se recortan ni normalizan silenciosamente.
2. Se rechazan contraseñas iguales al usuario, iguales a la contraseña actual o presentes en una lista local versionada de contraseñas comunes/comprometidas. Ningún valor se envía a servicios externos.
3. El hash usa el `IPasswordHasher<AppUser>` ya registrado. `SuccessRehashNeeded` actualiza el hash dentro de una transacción auditada sin cambiar la contraseña lógica.
4. La contraseña temporal se recibe sólo por bootstrap o administración autorizada, mediante secreto externo. Nunca hay contraseña predeterminada ni activación pública.
5. El cambio de contraseña actualiza hash, `MustChangePassword`, `SecurityStamp`, desafíos y auditoría en una sola transacción. Un fallo de política no altera hash, sello, contadores, TOTP ni sesión.

## 9. TOTP y recovery codes

1. TOTP usa RFC 6238, HMAC-SHA1 interoperable, seis dígitos, período de 30 segundos y ventana máxima de un paso anterior o posterior, con reloj UTC inyectable.
2. El secreto tiene 160 bits aleatorios criptográficos, Base32 sin padding. El `otpauth://` usa issuer `SGOL Loretta` y el usuario normalizado como etiqueta visible sólo durante enrolamiento.
3. El secreto activo y el pendiente se protegen con `IDataProtector` y propósito versionado ligado al UUID del usuario. La clave de Data Protection no se guarda junto al valor ni se expone.
4. `lastAcceptedTimeStep` se actualiza bajo lock; un paso ya aceptado no puede reutilizarse aunque esté dentro de la ventana.
5. Se generan diez recovery codes independientes de 20 caracteres Base32, presentados en cinco grupos de cuatro. Cada texto tiene entropía criptográfica y se persiste únicamente mediante `IPasswordHasher<AppUser>`; el texto sólo vive en la respuesta de creación.
6. Consumir o revocar un código conserva la fila histórica con `consumedAt` o `revokedAt`; no se borra. Dos consumos concurrentes del mismo código producen un solo éxito.
7. Regenerar revoca todos los códigos aún vigentes. Un recovery code consumido obliga a regenerar antes de emitir una sesión plena.

## 10. Bloqueo, concurrencia y rate limiting

1. Cinco fallos acumulados de contraseña o MFA para una cuenta conocida producen bloqueo. Cada modificación bloquea la fila de cuenta o desafío en PostgreSQL; los reintentos concurrentes no pierden incrementos.
2. Primera reincidencia: 15 minutos; segunda consecutiva: 30 minutos; tercera y posteriores: 60 minutos. Un login completo con MFA restablece contador y nivel a cero. El mero acierto de contraseña no los restablece.
3. Al vencer `lockoutEndUtc`, la siguiente autenticación correcta puede continuar; no existe endpoint ordinario de desbloqueo. El reset MFA autorizado restablece el bloqueo como parte de la recuperación.
4. Login: máximo diez solicitudes por IP normalizada en 15 minutos y cinco fallos por cuenta. MFA: cinco fallos por desafío; después el desafío se consume y aplica el bloqueo de cuenta.
5. El rate limit se implementa con ASP.NET Core Rate Limiting en memoria, apropiado para la única instancia MVP; no introduce Redis. La partición por IP no se registra en claro y el límite no sustituye autorización.
6. `429 RATE_LIMITED` no crea desafíos, no cambia credenciales y sólo expone `Retry-After`. Los límites y duraciones se configuran con valores acotados, sin etiquetas por usuario o IP en métricas.

## 11. Persistencia y migración expand-only

La migración aditiva propuesta crea o amplía únicamente:

1. `app_user`: `access_failed_count` entero no negativo con valor seguro `0`, `lockout_level` entero no negativo con valor seguro `0`, `lockout_end_utc` nullable y `authentication_row_version` bigint de concurrencia.
2. `authentication_challenge`: UUID, usuario, propósito cerrado (`CHANGE_PASSWORD`, `ENROLL_MFA`, `VERIFY_MFA`, `REGENERATE_RECOVERY_CODES`), estado (`PENDING`, `CONSUMED`, `EXPIRED`), snapshot de sello, creación, expiración, consumo, fallos, secreto TOTP pendiente protegido nullable y row version. Índices acotados por usuario/estado/expiración.
3. `mfa_totp_credential`: ID, usuario, secreto protegido, versión de protección, creación, enrolamiento, revocación, último paso aceptado y row version; índice único parcial para una credencial activa por usuario.
4. `mfa_recovery_code`: ID, usuario, hash, creación, consumo, revocación y row version; índices por usuario y estado, nunca por texto o hash expuesto.

No se crea tabla de sesión: la cookie cifrada mantiene la sesión y la consulta persistida por petición invalida el estado obsoleto. No hay backfill de secretos ni recovery codes. Una fila histórica con `MfaEnrolledAt` pero sin credencial TOTP activa falla cerrada como `MFA_STATE_INCONSISTENT` y requiere reset autorizado; la migración no inventa un enrolamiento.

Todas las FK usan `Restrict`; no hay cascade destructivo, purga ni overwrite de historia. `Down()` lanza una excepción de reversión bloqueada y el archivo generado queda sin BOM.

## 12. Autorización de endpoints existentes

1. La capa HTTP sólo obtiene el UUID canónico de `ClaimTypes.NameIdentifier`; no toma decisiones funcionales desde `sgol:role`.
2. La validación de cookie garantiza el mínimo común de cuenta, persona, empleo, rol único, sucursal, MFA y sello para toda petición protegida.
3. Administración de personas, cuentas, roles, configuración, calendario y políticas sigue reconsultando `DIRECCION`, empleo y vigencia en sus servicios.
4. Inbox, obligaciones, supervisión, indicadores, evidencias, validaciones y demás recursos siguen reconsultando propiedad, jerarquía, rol vigente y recurso según su módulo.
5. `/api/v1/continuity/reconciliations` sigue reconsultando PostgreSQL y acepta sólo `DIRECCION`; un claim manipulado no concede `PER-CONTINUIDAD-VER`.
6. `/api/v1/branches/LOR-001` puede usar sólo el principal ya validado porque no revela otro alcance; no convierte el rol de la cookie en autorización adicional.

## 13. Auditoría, logs y métricas

1. Escrituras críticas de contraseña, bloqueo, enrolamiento, consumo/regeneración de códigos, reset MFA y rotación de sello insertan auditoría en la misma transacción.
2. Eventos mínimos: `AUTH_LOGIN_FAILED`, `AUTH_PASSWORD_ACCEPTED`, `AUTH_ACCOUNT_LOCKED`, `AUTH_PASSWORD_CHANGED`, `AUTH_MFA_ENROLLMENT_STARTED`, `AUTH_MFA_ENROLLED`, `AUTH_MFA_FAILED`, `AUTH_MFA_SUCCEEDED`, `AUTH_RECOVERY_CODE_USED`, `AUTH_RECOVERY_CODES_REGENERATED`, `AUTH_MFA_RESET`, `AUTH_SESSION_REJECTED` y `AUTH_LOGOUT`.
3. Un fallo previo a identificar de forma segura al usuario usa actor y recurso nulos y razón genérica. No audita el nombre presentado ni distingue inexistente, inactivo o contraseña incorrecta.
4. Auditoría, logs, Problem Details y métricas excluyen contraseña, hash, TOTP, secreto protegido, `otpauthUri`, recovery code/hash, cookie, token CSRF, `SecurityStamp`, desafío protegido y nombre de usuario fallido.
5. Logs JSON usan identificador de evento, operación, resultado, `correlationId` y causa acotada. No incluyen cuerpos de autenticación ni serializan DTO sensibles; todos sobrescriben `ToString()` con redacción.
6. Métricas: contadores de intentos, éxito, fallo, bloqueo, MFA, recovery, sesión rechazada y rate limit; histogramas de latencia por operación. Etiquetas permitidas: operación, resultado y causa allowlist. Nunca usuario, UUID, IP, correlation ID o excepción libre.

## 14. Provisión local sintética

1. La primera cuenta Dirección se crea exclusivamente mediante `Sgol.Admin`/`DirectionBootstrapService` ya existente y sus secretos efímeros externos.
2. Después de completar contraseña y MFA de Dirección en el host real, `scripts/dev/provision-auth-users.ps1` usa exclusivamente la API autenticada existente para crear personas, cuentas y roles restantes; no escribe tablas directamente ni evita autorización.
3. El script recibe nombres y contraseñas temporales desde variables de entorno o un archivo explícito externo al repositorio e ignorado. No contiene valores predeterminados, TOTP, recovery codes ni credenciales de ejemplo utilizables.
4. Crea datos sintéticos para una cuenta de cada rol canónico en `LOR-001`, conserva los UUID devueltos y no deriva autoridad del puesto.
5. Cada usuario completa de forma interactiva su cambio de contraseña y enrolamiento TOTP. El procedimiento documenta cómo borrar la base local sintética fuera del flujo funcional, pero no agrega borrado de cuentas al producto.

## 15. Casos mínimos de prueba

### 15.1 Positivos

- Login HTTPS real de cada rol con contraseña, desafío TOTP, cookie emitida y `GET /api/v1/auth/session` correcto.
- Primer acceso: contraseña temporal, cambio obligatorio, enrolamiento, confirmación, respuesta única con diez recovery codes y sesión plena.
- `GET /api/v1/me/inbox` devuelve `2xx` para los cuatro roles con datos sintéticos acordes.
- Dirección puede ejecutar el caso sintético de `POST /api/v1/continuity/reconciliations`; los otros tres roles reciben `403` por las reglas existentes.
- Logout elimina cookies y la siguiente petición protegida devuelve `401`.
- Recovery code válido se consume, obliga a regenerar y después permite sesión plena.

### 15.2 Negativos y no-efecto

- Usuario inexistente, contraseña incorrecta, cuenta inactiva, empleo no vigente y rol ausente/múltiple/no canónico producen respuestas no enumerables y ninguna cookie plena.
- MFA incorrecto, TOTP repetido, recovery code repetido, desafío vencido/consumido y cookie alterada fallan sin sesión.
- CSRF ausente o inválido rechaza cada mutación sin cambiar hash, sello, contadores funcionales, MFA, recovery codes ni auditoría de éxito.
- `PISO_VENTAS`, `SUBCOORDINACION` y `ADMINISTRACION` no acceden a cuentas, roles ni continuidad exclusivos de Dirección.
- Cambiar contraseña, desactivar/reactivar cuenta, cambiar/revocar rol, reset MFA, modificar sello o perder empleo invalida la cookie anterior en la siguiente petición.
- Respuestas y captura de logs no contienen contraseña, TOTP, secreto, recovery codes fuera de su respuesta única, cookies, hash ni sello.

### 15.3 Concurrencia

- Cinco fallos paralelos producen un solo estado de bloqueo coherente y contador exacto.
- Dos confirmaciones del mismo desafío producen un éxito.
- Dos TOTP del mismo time step producen un éxito.
- Dos consumos del mismo recovery code producen un éxito.
- Regeneración concurrente deja un solo conjunto activo y todos los anteriores revocados.
- Cambio de rol o contraseña concurrente con validación de cookie no permite una petición autorizada después del commit invalidante.

## 16. Validación local requerida

1. `dotnet build SGOL.slnx --no-restore --configuration Release`.
2. Pruebas unitarias y de componente filtradas para contratos, cookies, CSRF, contraseña, TOTP, recovery, bloqueo, claims, auditoría, no-efecto y autorización.
3. Pruebas PostgreSQL enfocadas para migración, locks, unicidad, transacciones, consumo único e invalidación.
4. Smoke contra Kestrel HTTPS real y PostgreSQL local: obtener CSRF, login, cambio de contraseña, enrolar/confirmar MFA, recibir cookie y alcanzar al menos un endpoint protegido `2xx`. Un `ClaimsPrincipal` construido manualmente no satisface este gate.
5. Smoke de los cuatro roles, incluidas las denegaciones de Dirección y continuidad.
6. `git diff --check`.

Docker/S3/ClamAV, suite completa, gate AMD64 de `HU-035`, pipeline remoto, publicación y despliegue no forman parte del gate ordinario de esta tarea. Una limitación real de infraestructura se registra como `Validación diferida` con causa exacta; no se presenta como éxito.

## 17. Trazabilidad y estado

Si se aprueba e implementa, el mismo cambio actualizará:

- `docs/traceability/IMPLEMENTATION_STATUS.md` con `TECH-AUTH-001` como `Implementada localmente`;
- documentación local de HTTPS, migración, bootstrap, variables externas, provisión sintética, consumo de endpoints y smoke;
- inventario de migraciones y model snapshot;
- solución, proyectos y lockfiles sólo si una dependencia nueva resulta imprescindible y queda justificada, fijada y revisada.

No se marcará `Terminada`, `Publicada` ni `Integrada` sin la evidencia correspondiente. `AGENTS.md`, `Fuentes/` y cambios ajenos quedan fuera de staging y commits.

## 18. Exclusiones expresas

Quedan fuera:

- proveedor externo, OAuth/OIDC, SSO, federación o SMS;
- JWT persistente, local storage, refresh token o API pública;
- Redis, broker, microservicio de identidad o segunda base;
- Razor Pages, HTML, CSS, componentes, navegación y cualquier decisión visual del frontend;
- SPA separada;
- impersonación, cambio de usuario, “recordarme”, listado/revocación individual de sesiones o panel administrativo general;
- autorización basada sólo en rol/permiso de cookie, puesto o turno;
- credenciales predeterminadas, bypass, header de autenticación, usuario mágico o secretos versionados;
- ampliación de permisos, cambio de jerarquía o modificación semántica de endpoints de negocio.

## 19. Criterio de aprobación y eficacia

La aprobación debe ser íntegra. Una aprobación parcial, condicionada o con cambios pendientes no crea `TECH-AUTH-001` y no autoriza código funcional. Después de la aprobación íntegra se podrá implementar localmente este contrato en el mismo chat, con los límites y validaciones aquí definidos.
