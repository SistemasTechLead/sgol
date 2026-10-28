# Inspección de seguridad de SGOL — 28/10/2026

## Alcance y conclusión

Inspección de código y configuración de Web, módulos, Worker, Admin, Operations, proxy LAN, cliente de pantalla y utilidades. Dos agentes auxiliares revisaron autenticación/autorización e infraestructura; el agente principal revisó inyecciones, archivos y consumo de recursos, contrastó los hallazgos y ejecutó pruebas adicionales. Referencias de ataque: OWASP Top 10 2025, API Security Top 10 2023 y Cheat Sheet Series.

Es una inspección general con pruebas dirigidas, no una certificación ni cobertura exhaustiva de todas las rutas, estados o ataques. No se implementaron correcciones. El entorno habitual es desarrollo con simulaciones; las pruebas de escritura se hicieron exclusivamente en una base nueva `sgol_task_checks_security_auth_*` y en procesos de prueba. En la simulación compartida solo se observaron configuración, listeners y privilegios de base mediante lectura.

Se confirmaron una vulnerabilidad de revocación de sesión y tres debilidades de configuración/control de recursos. Además, hay avisos de seguridad de SeaweedFS aplicables por versión cuya explotación en esta instalación no quedó demostrada. No se encontró SQL injection explotable en los recorridos inspeccionados; esto no equivale a demostrar su ausencia en todo el proyecto.

## Hallazgos confirmados

| ID | Severidad contextual | Hallazgo | Evidencia |
|---|---|---|---|
| SEC-01 | Media; impacto según permisos de la sesión robada | Logout no revoca la sesión del servidor | Reutilización HTTP de cookie anterior: 200 después de logout 204 |
| SEC-02 | Media; impacto alto tras compromiso previo | Cuenta runtime de PostgreSQL es superusuario | Consulta real de roles: SUPERUSER, CREATEDB y CREATEROLE activos |
| SEC-03 | Media, requiere acceso local autorizado por ACL | Secretos DB/S3/TLS legibles por un grupo adicional | Archivos sin cifrar y ACL heredada con lectura para CodexSandboxUsers |
| SEC-04 | Media, exposición a abuso de disponibilidad; caída no demostrada | Readiness anónimo ejecuta comprobaciones internas sin cuota/caché | 64 solicitudes HTTP anónimas, concurrencia 8: 64 respuestas 200, ningún 429 |

### SEC-01 — Una cookie copiada conserva acceso después de cerrar sesión

**Archivos:** `src/Sgol.Web/Interface/Endpoints/AuthenticationApiEndpoints.cs:270`, en particular 284–285; `src/Sgol.Web/Infrastructure/Persistence/Identity/EfHostedAuthenticationService.cs:502`; validación de stamp en el mismo servicio, línea 475.

`LogoutAsync` registra auditoría y emite el borrado de cookie. `RecordLogoutAsync` no invalida el ticket ni cambia el security stamp. El servidor sigue aceptando el ticket anterior. Las cookies tienen HttpOnly, Secure y SameSite=Strict: no se ha demostrado aquí una forma de robarlas y no se confunde un token CSRF visible con el secreto de sesión.

**Prueba ejecutada:** login y cambio de contraseña reales; sesión 200; logout sin CSRF 400; logout válido 204; navegador lógico con cookie eliminada 401; cliente independiente reutilizando la cookie guardada antes de logout 200. Se persistió un evento `AUTH_LOGOUT`. La expiración absoluta sigue siendo de hasta 24 horas desde autenticación.

**Impacto y precondición:** quien ya haya obtenido el ticket conserva acceso pese al cierre de sesión. El logout no es un mecanismo efectivo para contener ese robo. No prueba lectura de cookies por JavaScript ni bypass de contraseña/MFA.

**Criterio para la corrección posterior:** invalidar la sesión en servidor y hacer fallar su replay. Decidir si logout cierra solo la sesión actual o todas las del usuario; una rotación de stamp sin modelo de sesiones afectaría a todas.

### SEC-02 — El proceso Web usa una cuenta PostgreSQL privilegiada

**Archivos:** `compose.local.yaml:7`, `scripts/local-env.ps1:13`.

La cuenta inicial del contenedor se reutiliza como cuenta de la aplicación. Consulta de lectura a `pg_roles` confirmó `rolsuper=true`, `rolcreatedb=true`, `rolcreaterole=true` para la cuenta runtime.

**Impacto y precondición:** un robo de credenciales, una ejecución en la aplicación o una futura SQL injection podría permitir controlar otras bases/roles y usar capacidades privilegiadas del servidor PostgreSQL. La ejecución potencial sería como usuario del servicio dentro del contenedor DB; no se ha demostrado escape al host Windows ni entrada remota anónima.

**Criterio posterior:** separar migración/administración de runtime y comprobar que la cuenta runtime sigue soportando los recorridos necesarios con privilegios mínimos.

### SEC-03 — Secretos locales sin protección suficiente frente a otros procesos

**Archivos:** `scripts/iniciar-local.ps1:18`, 19 y 27; `scripts/iniciar-red.ps1:19`, 29 y 32.

Se guardan contraseñas DB/S3 y credencial inicial en `.local/config.json`, `.local/compose.env` y `.local/acceso.txt`. La contraseña del PFX está en `.local/red.json`, junto a `.local/red.pfx`. Los scripts no fijan ACL mínima. La inspección de ACL de config.json, red.json y red.pfx mostró lectura heredada para el grupo local `CodexSandboxUsers`, además del propietario, SYSTEM y Administrators.

**Impacto y precondición:** un proceso/cuenta incluido en esos permisos puede leer secretos; el par PFX/contraseña permite extraer la clave TLS. No se ejecutó una prueba suplantando a otro usuario ni se demostró exposición por HTTP, acceso para todos los usuarios Windows o publicación de secretos en Git. El usuario propietario y los administradores siguen siendo entidades de confianza del host; no se propone proteger secretos frente a un administrador ya comprometido.

**Criterio posterior:** ACL explícita ajustada a las cuentas necesarias, protección de secretos y eliminación de duplicados/credenciales iniciales obsoletas. Ignorar `.local` en Git evita rastreo, no acceso local.

### SEC-04 — Endpoint público de readiness sin contención de solicitudes

**Archivos:** `src/Sgol.Web/Program.cs:64`, 65 y 76; `src/Sgol.Web/Infrastructure/Authentication/HostedAuthenticationInfrastructure.cs:284`; `src/Sgol.Web/Infrastructure/Http/PerformanceWork.cs`; `src/Sgol.Web/Infrastructure/Persistence/PostgreSqlReadinessHealthCheck.cs`; `src/Sgol.Web/Infrastructure/Evidence/EvidenceHealthChecks.cs`; `src/Sgol.Web/Infrastructure/Evidence/S3PrivateObjectStorage.cs:370`.

`/health/ready` no exige autorización ni una política de rate limiting. Las políticas existentes cubren login y pantalla; los gates cubren determinadas operaciones, pero no esta ruta. Cada llamada activa los checks etiquetados `ready`: conexión PostgreSQL, protección criptográfica y disponibilidad de almacenamiento. En la configuración con S3 operativo, el check de almacenamiento hace dos `ListObjectsV2` con las credenciales del servidor, uno por bucket.

**Prueba ejecutada:** host HTTPS aislado, mismos middlewares de autenticación/rate/admission y mapeo readiness equivalente, con PostgreSQL/DataProtection reales; 64 peticiones anónimas con concurrencia 8 recibieron 200 en 48 ms. No se registró S3 en ese host de prueba: las dos llamadas S3 por petición se sustentan en el código, no en una carga integrada medida. No fue el proceso completo de la simulación ni se ejecutó una prueba de derribo.

**Impacto y precondición:** quien alcance el endpoint puede provocar trabajo interno sin cuota. El caudal para causar indisponibilidad, la resistencia del proxy y el impacto con S3 degradado no se midieron. No se afirma que 64 peticiones hayan agotado ningún recurso.

**Criterio posterior:** limitar la exposición a los monitores que lo necesiten y contener/coalescer las verificaciones. Conservar probes útiles sin ofrecer trabajo interno ilimitado a clientes externos.

## Dependencias: avisos pendientes de alcance

SeaweedFS está fijado en versión 4.45. Se verificaron avisos oficiales:

- [GHSA-5fx4-c9qp-36jc](https://github.com/seaweedfs/seaweedfs/security/advisories/GHSA-5fx4-c9qp-36jc): el servicio gRPC de administración S3 acepta cambios de identidades sin autenticación en versiones afectadas. Compose no publica 18333 y el proxy apunta a HTTP 8333: no se demostró explotación desde la LAN. Falta comprobar acceso lateral entre contenedores y configuración efectiva del listener interno. Severidad del aviso: crítica; no trasladar automáticamente esa severidad a una entrada LAN no demostrada.
- [GHSA-qmx6-fpcw-q3xg](https://github.com/seaweedfs/seaweedfs/security/advisories/GHSA-qmx6-fpcw-q3xg): POST Object puede eludir políticas de bucket en <=4.45. Requiere identidad con Write y políticas pertinentes. SGOL entrega PUT firmados, no la clave S3; no se acreditaron las precondiciones de explotación con los permisos normales del navegador.

NuGet no reportó paquetes vulnerables en los 15 proyectos de la solución ni en las comprobaciones adicionales de proxy/cliente de pantalla. No es garantía de ausencia de vulnerabilidades desconocidas o nativas. Consulta directa al endpoint público de advisories de npm con los nombres/versiones del package-lock de avatar-assets: 32 paquetes consultados, respuesta sin avisos. No se ejecutaron scripts npm ni se enviaron secretos. jsPDF 4.2.1 vendorizado ya incorpora la corrección del [aviso revisado](https://github.com/parallax/jsPDF/security/advisories/GHSA-wfv2-pwc8-crg5), que afecta a <=4.2.0.

## Inyecciones y otras clases de ataque

### SQL injection

Se buscaron productores de SQL en `src`, `tools` y `scripts`: NpgsqlCommand, CommandText, ExecuteSqlRaw, SqlQueryRaw, FromSqlRaw y APIs interpoladas, incluyendo utilidades operativas y lectores antiguos. Se siguieron las rutas principales de login, catálogo/búsqueda, reportes, bandeja, locks y restauración.

- `EfInboxReader.ReadTaskBucketAsync` usa SQL fijo y `DbParameter` para persona, periodo, cursor, límites y códigos; no concatena el texto del usuario al SQL (`EfInboxReader.cs:157`, 199–208 y 230).
- Los usos runtime encontrados de `ExecuteSqlRawAsync` son sentencias fijas de lectura o locks. `FromSqlInterpolated`/`ExecuteSqlInterpolatedAsync` reciben directamente la cadena interpolada y parametrizan los valores.
- `PostgreSqlXmlRepository` usa placeholders y parámetros para insertar nombre/XML. `ScheduledJobRunner` parametriza los identificadores de advisory locks. Las verificaciones de Operations usan SQL constante y parámetro para migración.
- Búsqueda de tareas usa LINQ/EF y el filtro normalizado; el orden de reportes deriva de alternativas explícitas y columnas permitidas. No se encontró un ordenamiento SQL construido con texto arbitrario recibido.
- SQL dinámico de algunas herramientas de snapshot usa nombres obtenidos del catálogo DB y comillas escapadas; no es una entrada HTTP. Las migraciones contienen SQL de esquema generado por desarrolladores y no representan por sí mismas una entrada de usuario.
- **Ataque HTTP ejecutado:** nombre de login con tautología SQL, rechazado con 401; consulta posterior de sesión 401. Esto prueba ese caso de autenticación, no todos los campos y endpoints. No se ejecutaron payloads destructivos ni retardos SQL contra la simulación.

Resultado: **sin SQL injection confirmada**. El riesgo SEC-02 es independiente: parametrizar consultas no justifica usar un superusuario. Referencia: [prevención de SQL injection de OWASP](https://cheatsheetseries.owasp.org/cheatsheets/SQL_Injection_Prevention_Cheat_Sheet.html).

### Otros resultados

- **XSS:** revisión de salidas Razor, JS propio y puntos `innerHTML`. Los fragmentos dinámicos revisados proceden de páginas renderizadas por Razor; `Html.Raw` en `_Icon.cshtml` elige únicamente SVG constante de un switch. No se confirmó una cadena de XSS. No se ejecutó una campaña de payloads persistidos en cada campo con navegador real.
- **Sesiones en navegador:** no se encontró cookie/token de sesión guardado en localStorage/sessionStorage. Las preferencias visuales sí usan almacenamiento local. Los tokens CSRF y autorizaciones limitadas de captura/subida son deliberadamente accesibles al formulario; no son la contraseña ni una clave S3 permanente.
- **CSRF y fuerza bruta:** login/logout sin CSRF rechazados; login limitado con 429. Intentos de evadir esa cuota con headers de origen inventados y mayúsculas de ruta también dieron 429. El middleware protege solicitudes mutadoras pertinentes y Razor mantiene su protección. No se generaliza a todas las combinaciones legacy/MFA.
- **Autorización:** cookie emitida con Dirección y cuenta posteriormente degradada a PISO_VENTAS: users, people y direction-plans devolvieron 403. No se confirmó elevación por cookie con rol antiguo. Falta ejecutar una matriz completa de IDOR entre usuarios y entidades.
- **XXE/SSRF/archivos:** parsers Excel rechazan DTD, URL externa, referencia fuera del ZIP, VBA, hojas repetidas y expansión excesiva en los casos sintéticos ejecutados. No escriben los miembros ZIP en rutas del host. HTTP de API usa origen confiable validado y redirecciones desactivadas.
- **Comandos:** el pipeline de backup usa `UseShellExecute=false`, `ArgumentList` y entorno para credenciales; no se identificó concatenación de entrada HTTP a shell. Backup/restore no se ejecutó de extremo a extremo.
- **Red:** DB, scanner, S3 y backend observados en loopback; proxy TLS en LAN. Validación de Host y forwarded headers restringidos observada. No se hizo escaneo externo, auditoría completa de firewall, TLS o contenedores.

## Matriz de validación ejecutada y pendiente

| Escenario | Resultado esperado | Prueba/entorno | Estado |
|---|---|---|---|
| Cookie anterior a logout | Rechazar replay | HTTP HTTPS + EF/PostgreSQL aislados | Fallido: 200 |
| Logout/login sin CSRF | Rechazar | Mismo host aislado | Aprobado: 400 |
| Sesión anónima | Rechazar | Mismo host aislado | Aprobado: 401 |
| Login con tautología SQL | No iniciar sesión | Mismo host aislado | Aprobado: 401 y sesión 401 |
| Rol degradado con cookie antigua | Sin permiso administrativo | APIs users/people/direction-plans aisladas | Aprobado: tres 403 |
| Login repetido | Activar cuota | 12 intentos acotados, partición previamente utilizada | Aprobado: siete 401 y cinco 429 |
| Readiness público repetido | Contener trabajo interno | 64 peticiones, concurrencia 8, PG/DP reales sin S3 | Fallido para cuota: 64 respuestas 200; no caída |
| Privilegios runtime DB | Cuenta sin superusuario | Consulta lectura de roles del entorno actual | Fallido |
| Protección secretos locales | Lectura mínima necesaria | Scripts y ACL del host | Fallido: grupo adicional con lectura |
| XLSX mínimo válido | Aceptar | Parser real, memoria/proceso aislados | Aprobado |
| XXE con declaración de archivo | Rechazar | Parser real, referencia sintética inexistente | Aprobado |
| URL externa / recorrido fuera del ZIP | Rechazar | Parser real, dos casos sintéticos | Aprobado |
| Hoja repetida / VBA | Rechazar | Parser real, dos casos sintéticos | Aprobado |
| ZIP expandido de 61 MiB | Rechazar antes de parseo | Un miembro comprimido sintético de ~63 KB; sin extracción a disco | Aprobado |
| HTML como JPEG / SVG como PNG / extensión ejecutable | Rechazar | Validador técnico real, tres casos | Aprobado |
| JPEG, cancelación, colas, ticket interno | Reglas existentes | `--performance-regressions`, compilación aislada | Aprobado: diez comprobaciones |
| NuGet y lock npm | Sin avisos del catálogo consultado | Consultas de auditoría | Aprobado con límites del catálogo |
| XSS persistente y todos los IDOR | Sin ejecución/acceso ajeno | Código parcialmente inspeccionado | No ejecutado exhaustivo |
| MFA habilitado, recuperación y carreras de revocación | Sin bypass | Harness actual usa MFA=false, como desarrollo | No ejecutado integrado |
| DoS sostenido/distribuido/slow clients | Mantener disponibilidad | Pruebas de derribo fuera de esta tanda acotada | No ejecutado |
| Avisos Seaweed accesibles lateralmente | Sin bypass de identidad/políticas | Versión y publicación inspeccionadas | No ejecutado activo |
| SO, imágenes/SBOM, historial completo de secretos | Sin problemas alcanzables | Sin escáner dedicado en esta revisión | No ejecutado |

## Evidencias y reproducción

Archivos locales ignorados en `artifacts/security-audit/`:

- `auth-api.md`, `auth-http-results.json`, `auth-harness/`: revisión y reproducción HTTP con servicios reales y datos inventados.
- `infra.md`: observaciones y comandos de infraestructura, sin valores secretos.
- `media-probes/`, `media-results.json`: diez casos sintéticos de archivos.
- `performance-regressions.log`: diez comprobaciones de regresión existentes.
- `npm-advisories.json`: resultado del catálogo npm.

Comandos principales desde la raíz, con outputs de compilación separados del servicio activo:

```powershell
dotnet run --project artifacts/security-audit/media-probes -p:ArtifactsPath=C:/Users/siste/Dev/SistemaSGOL/artifacts/security-audit/media-build
dotnet run --project tools/Sgol.TaskChecks -p:ArtifactsPath=C:/Users/siste/Dev/SistemaSGOL/artifacts/security-audit/regression-build -- --performance-regressions
dotnet list SistemaSGOL.slnx package --vulnerable --include-transitive --format json
```

El harness HTTP crea su propia base de pruebas y conserva el caso para reproducción; no imprimir sus cookies o credenciales. La primera compilación convencional chocó con DLL del servicio activo; se repitió con rutas de artefactos aisladas sin detener ese servicio. No hubo correcciones de producto, migraciones de la simulación, commit, push ni despliegue.

Este informe es la entrada para el plan de corrección posterior. Las decisiones pendientes son el alcance de revocación de logout y la exposición necesaria de probes/servicios; la priorización debe considerar tanto la vía de entrada como el daño posible tras un compromiso.

## Referencias de evaluación

- [OWASP Top 10](https://owasp.org/www-project-top-ten/) y [API Security Top 10](https://owasp.org/API-Security/).
- [OWASP SQL Injection Prevention](https://cheatsheetseries.owasp.org/cheatsheets/SQL_Injection_Prevention_Cheat_Sheet.html).
- [OWASP File Upload](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html) y [XML Security](https://cheatsheetseries.owasp.org/cheatsheets/XML_Security_Cheat_Sheet.html).
