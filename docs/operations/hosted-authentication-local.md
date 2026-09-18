# Autenticación hospedada local de SGOL

`TECH-AUTH-001` expone únicamente API HTTP dentro de `Sgol.Web`. No incorpora Razor, HTML, SPA, proveedor externo, JWT persistente, bypass ni usuario predeterminado. Toda autorización funcional sigue resolviéndose en los servicios existentes contra PostgreSQL.

## Superficie HTTP

| Método y ruta | Resultado |
|---|---|
| `GET /api/v1/auth/csrf` | Emite cookie antiforgery y devuelve el token para `X-CSRF-TOKEN` |
| `POST /api/v1/auth/login` | Valida usuario/contraseña y crea un desafío previo, nunca una sesión plena |
| `POST /api/v1/auth/password/change` | Cambia la contraseña temporal o una contraseña con MFA reciente |
| `POST /api/v1/auth/mfa/enroll` | Devuelve una vez el secreto manual y el URI `otpauth` del desafío |
| `POST /api/v1/auth/mfa/confirm` | Confirma TOTP, devuelve diez recovery codes una vez y emite la cookie de sesión |
| `POST /api/v1/auth/mfa/verify` | Completa MFA con TOTP o consume un recovery code |
| `POST /api/v1/auth/recovery-codes/regenerate` | Revoca los códigos restantes y devuelve un juego nuevo una vez |
| `GET /api/v1/auth/session` | Devuelve la identidad persistida y su proyección informativa vigente |
| `POST /api/v1/auth/logout` | Elimina la cookie de sesión |
| `POST /api/v1/users/{userId}/mfa-reset` | Dirección revoca MFA, impone contraseña temporal y primer acceso nuevo |

Todas las operaciones `POST`, `PUT`, `PATCH` y `DELETE` bajo `/api/v1` exigen el token obtenido de `/api/v1/auth/csrf`. Debe solicitarse otro token cuando cambia el principal, especialmente después de completar MFA. Las cookies `__Host-SGOL-Session`, `__Host-SGOL-PreAuth` y `__Host-SGOL-CSRF` requieren HTTPS, son `HttpOnly`, `Secure`, `SameSite=Strict` y usan `Path=/`.

## Preparación sin secretos en Git

1. Use un PostgreSQL local desechable con la migración `20260918001719_AddHostedAuthentication` aplicada. Inyecte la conexión sólo mediante `ConnectionStrings__Sgol`.
2. Cree fuera del repositorio un archivo de PowerShell o variables de proceso con estos valores:

   - `SGOL_BOOTSTRAP_PERSON_CODE`, `SGOL_BOOTSTRAP_PERSON_DISPLAY_NAME`, `SGOL_BOOTSTRAP_USER_NAME` y `SGOL_BOOTSTRAP_INITIAL_PASSWORD` para la primera Dirección;
   - para cada rol, usuario, contraseña temporal y contraseña definitiva distintos;
   - un destino externo para conservar el secreto TOTP y los recovery codes si se necesitan en ejecuciones posteriores.

3. No coloque el archivo bajo control de versiones. Por defensa adicional, `.sgol-auth.local.ps1` y `.sgol-auth-secrets.json` están ignorados si se crean accidentalmente en la raíz; se recomienda una ruta fuera del checkout y ACL sólo para el usuario local.
4. Aplique la migración y ejecute el bootstrap de un solo uso:

   ```powershell
   dotnet ef database update --project src/Sgol.Web --startup-project src/Sgol.Web
   dotnet run --no-build --configuration Release --project src/Sgol.Admin
   ```

5. Inicie el host exclusivamente por HTTPS:

   ```powershell
   $env:ASPNETCORE_URLS = 'https://localhost:7443'
   dotnet run --no-build --configuration Release --project src/Sgol.Web
   ```

El certificado de desarrollo debe ser de confianza local. No cambie `Secure` ni use HTTP para facilitar la prueba.

## Primer acceso real

Use una sesión HTTP que conserve cookies. El siguiente esqueleto evita imprimir contraseñas, secreto TOTP, recovery codes y cookies:

```powershell
$baseUri = 'https://localhost:7443'
$web = [Microsoft.PowerShell.Commands.WebRequestSession]::new()

function Get-Csrf {
    (Invoke-RestMethod -Method Get -Uri "$baseUri/api/v1/auth/csrf" -WebSession $web).data.requestToken
}

function Invoke-SgolPost([string]$Path, [object]$Body, [string]$Csrf, [hashtable]$Headers = @{}) {
    $Headers['X-CSRF-TOKEN'] = $Csrf
    Invoke-RestMethod -Method Post -Uri "$baseUri$Path" -WebSession $web `
        -Headers $Headers -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Compress)
}

$csrf = Get-Csrf
$login = Invoke-SgolPost '/api/v1/auth/login' @{
    userName = $env:SGOL_BOOTSTRAP_USER_NAME
    password = $env:SGOL_BOOTSTRAP_INITIAL_PASSWORD
} $csrf

$changed = Invoke-SgolPost '/api/v1/auth/password/change' @{
    currentPassword = $env:SGOL_BOOTSTRAP_INITIAL_PASSWORD
    newPassword = $env:SGOL_AUTH_DIRECCION_PASSWORD
} $csrf

$enrollment = Invoke-SgolPost '/api/v1/auth/mfa/enroll' @{} $csrf
# Registre enrollment.data.manualKey en un autenticador TOTP y obtenga el código actual.
$confirmed = Invoke-SgolPost '/api/v1/auth/mfa/confirm' @{
    totpCode = $totpCode
} $csrf
```

`confirmed.data.recoveryCodes` se muestra una sola vez. Guárdelo únicamente en el destino externo protegido o descártelo y use el reset MFA administrado cuando corresponda. Después de `mfa/confirm`, vuelva a llamar `Get-Csrf` porque el token anterior pertenecía al principal anónimo.

En accesos posteriores, `login` devuelve `VERIFY_MFA`; continúe con `POST /api/v1/auth/mfa/verify`. Si se usa `recoveryCode`, la respuesta obliga `REGENERATE_RECOVERY_CODES` y no emite sesión plena hasta ejecutar `POST /api/v1/auth/recovery-codes/regenerate` con la contraseña vigente.

## Provisión sintética de los cuatro roles

Con la sesión Dirección ya completada y un CSRF nuevo:

1. Mantenga la cuenta bootstrap como `DIRECCION`.
2. Para cada uno de `ADMINISTRACION`, `SUBCOORDINACION` y `PISO_VENTAS`:
   1. cree una persona sintética mediante `POST /api/v1/people`;
   2. cree su cuenta mediante `POST /api/v1/users` con contraseña temporal externa;
   3. asigne exactamente un rol mediante `POST /api/v1/users/{userId}/role-assignments`.
3. En cada escritura envíe una `Idempotency-Key` UUID nueva. La primera asignación no necesita `If-Match`; sustituciones posteriores sí usan el ETag devuelto.
4. Abra una sesión HTTP separada para cada cuenta y complete contraseña temporal, enrolamiento TOTP y confirmación. Nunca derive el rol del puesto o turno.

Ejemplo de creación, omitiendo deliberadamente los valores secretos:

```powershell
$csrf = Get-Csrf
$person = Invoke-SgolPost '/api/v1/people' @{
    stableCode = $stableCode
    displayName = $displayName
} $csrf @{ 'Idempotency-Key' = [guid]::NewGuid().ToString('D') }

$account = Invoke-SgolPost '/api/v1/users' @{
    personId = $person.data.id
    userName = $userName
    temporaryPassword = $temporaryPassword
} $csrf @{ 'Idempotency-Key' = [guid]::NewGuid().ToString('D') }

Invoke-SgolPost "/api/v1/users/$($account.data.id)/role-assignments" @{
    roleCode = $roleCode
    reason = 'Provisión local sintética TECH-AUTH-001'
} $csrf @{ 'Idempotency-Key' = [guid]::NewGuid().ToString('D') }
```

## Matriz mínima de smoke

Ejecute cada caso desde el host HTTPS real y PostgreSQL local. Conserve sólo estados HTTP, códigos de problema, correlaciones y conteos; no capture cuerpos que contengan secretos.

| Caso | Evidencia esperada |
|---|---|
| Contraseña incorrecta, usuario existente o inexistente | `401 AUTHENTICATION_FAILED`, mismo contrato |
| Primer acceso | `CHANGE_PASSWORD`, después `ENROLL_MFA`, confirmación y cookie plena |
| Cookie plena de cada rol | `GET /api/v1/auth/session` y `GET /api/v1/branches/LOR-001` responden `2xx`; recursos propios adicionales dependen de sus datos funcionales |
| Administración Dirección | `GET /api/v1/users` responde `2xx` sólo para Dirección y `403` para los otros roles |
| Continuidad | una solicitud válida a `POST /api/v1/continuity/reconciliations` supera autorización sólo como Dirección; los otros roles reciben `403` |
| Cambio de rol, estado o sello | la cookie emitida antes del cambio recibe `401` en la siguiente petición |
| Empleo no vigente o rol ausente/múltiple/no canónico | login o siguiente validación de cookie falla cerrada |
| Cinco fallos acumulados | la cuenta queda bloqueada; contraseña correcta devuelve `423 ACCOUNT_LOCKED` hasta `retryAt` |
| Recovery code | se consume una vez, obliga regeneración y el segundo uso falla |
| CSRF ausente o incorrecto | `400 CSRF_INVALID` sin ejecutar la mutación |
| Logout | `204`; la cookie anterior deja de autenticar |
| Logs y respuestas | no contienen contraseña, TOTP, recovery code, cookie, secreto protegido ni cadena de conexión |

El reset MFA ordinario requiere sesión Dirección vigente, `Idempotency-Key`, motivo y una contraseña temporal externa. Revoca TOTP y recovery codes, consume desafíos pendientes, pone en cero el bloqueo, rota `SecurityStamp` y obliga cambio de contraseña y enrolamiento nuevos en una sola transacción auditada.
