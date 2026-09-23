# SGOL — Adenda 46: mapa de navegación y sesión frontend

## 1. Control

| Campo | Valor |
|---|---|
| Estado contractual | `APROBADA POR EL RESPONSABLE — INCORPORADA LOCALMENTE` |
| Fecha de aprobación | 2026-09-23 |
| Base verificada antes de numerar | `origin/master = c5313aff32a62118d01e4bde6f139cbfcc1c23f9` |
| Tipo | Corrección contractual mínima de la dependencia documental previa a `TECH-FRONT-003` |
| Efecto | Resuelve `BR-N01..BR-N08`, fija el contrato de sesión/CSRF para Razor y crea la fuente operativa de navegación |
| No autoriza | Iniciar o reanudar `TECH-FRONT-003`, implementar vistas o comportamiento, publicar, fusionar o desplegar |

La numeración 46 fue asignada después de comprobar que la última adenda incorporada en la base era la Adenda 45. La aprobación del responsable convierte en decisiones aprobadas las propuestas documentadas en la matriz externa y autoriza exclusivamente esta incorporación documental.

## 2. Omisión corregida y precedencia

La Adenda 45 exige para `TECH-FRONT-003` un “mapa BR-N01..N08 aprobado”, pero esas brechas sólo existían como preguntas externas. Además, `docs/design/componentes.md` describía tres perfiles aunque SGOL tiene cuatro roles canónicos, la navegación base dependía de `IsInRole` sin un `ClaimTypes.Role` emitido por la autenticación hospedada, y el cliente común todavía no define por sí solo el puente completo de cookies y CSRF.

La dependencia de `TECH-FRONT-003` queda complementada así:

| Elemento | Condición corregida |
|---|---|
| Dependencia de código | `TECH-FRONT-002` integrada en `origin/master` mediante PR `#63`, cabeza `a464bdac745d6dc38b4e72fe40f5af824287b38a`, pipeline `35912549181` `SUCCESS`, merge `c5313aff32a62118d01e4bde6f139cbfcc1c23f9` |
| Dependencia documental | Esta adenda, `docs/design/navegacion.md` y la corrección de cuatro roles en `componentes.md` deben estar incorporadas |
| Orden de inicio | Una orden explícita posterior debe iniciar o reanudar `TECH-FRONT-003` |
| Límite | No adelanta `FRONT-001`, no crea login funcional y no modifica API, DTO, autorización o cookies del backend |

## 3. Reglas transversales aprobadas

1. La presentación conserva Razor Pages/MVC, mismo origen, cookie segura y CSRF; no introduce SPA, CORS ni repositorio separado.
2. Los roles canónicos son `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION` y `PISO_VENTAS`. Puesto y turno no autorizan.
3. La navegación visible es presentación. La autorización efectiva sigue en el servidor por principal vigente, permiso, rol, jerarquía, recurso y estado.
4. La preautenticación es opaca, no constituye principal y nunca consume endpoints de negocio.
5. `GET /api/v1/auth/session` es la fuente del snapshot request-scoped de identidad, rol, permisos y expiraciones del shell.
6. `allowedActions` y `links` gobiernan acciones de recurso cuando el contrato los entrega. Un control visible u oculto no concede autoridad.
7. Toda mutación autenticada por cookie conserva CSRF. Cookies, tokens, contraseñas, TOTP y recovery codes no se registran ni se exponen.
8. Las rutas web de esta adenda no alteran rutas `/api/v1`, no inventan DTO y no convierten una relación API en un `href` arbitrario.

## 4. Decisiones aprobadas BR-N01..BR-N08

### BR-N01 — entrada y destino inicial

- `/` es la entrada pública y `/acceso` el acceso.
- Cambio obligatorio de contraseña, enrolamiento MFA, verificación MFA y códigos de recuperación usan rutas Razor separadas registradas en `docs/design/navegacion.md`.
- Sin sesión plena, la entrada termina en acceso. Con sesión plena, los cuatro roles usan `/mi-trabajo` como destino común; no se crean tableros distintos por rol.
- Un paso de contraseña o MFA sólo se continúa desde el `nextStep` contractual de su respuesta. Una preautenticación huérfana al entrar por `/` se descarta de forma segura y reinicia acceso; no se inventa un endpoint de reanudación.
- Mientras se resuelve sesión, el shell está cargando y no presupone rol ni acciones. Un rol desconocido falla cerrado.

### BR-N02 — conservación del destino solicitado

- Sólo puede conservarse un destino GET local, implementado, incluido en el registro `NAV-*` y con claves de query aprobadas.
- El destino normalizado se protege con Data Protection en un valor opaco de vigencia absoluta de 30 minutos. Se revalida después de login, cambio obligatorio de contraseña y MFA.
- Se rechazan esquema o host, `//`, barras invertidas, caracteres de control, rutas API, logout, mutaciones, URLs firmadas, rutas no registradas y recursos que el principal ya no puede consultar.
- Un valor ausente, alterado, vencido o no autorizado cae en `/mi-trabajo` sin probar destinos alternativos ni revelar existencia.

### BR-N03 — agrupación de las 48 unidades

El shell usa ocho grupos: Mi trabajo; Validación y supervisión; Planificación y generación; Personas y accesos; Configuración; Indicadores; Auditoría; Continuidad. Las unidades A01..A05 permanecen fuera del shell y A06/A07 viven en el encabezado de sesión. Un grupo sin hijo implementado y visible no aparece; nunca se publica un enlace a una página inexistente.

La correspondencia completa de unidades, grupos y visibilidad gruesa por rol reside en `docs/design/navegacion.md`. La visibilidad de grupo no autoriza al recurso ni anticipa contratos de historias posteriores.

### BR-N04 — página principal, listado, detalle y diálogo

- Un listado sirve para descubrimiento, filtro y selección.
- Un detalle GET conserva identidad, historia y secciones del recurso.
- Un formulario complejo o recorrido multipaso usa página separada.
- Un diálogo se reserva para confirmación contextual corta y motivada; nunca es destino de deep link ni ejecuta una mutación por GET.
- Personas, TAR, obligaciones y validaciones aplican este patrón. Cuentas y roles no obtienen detalle navegable hasta que sus brechas de API estén cerradas por su historia consumidora.

### BR-N05 — restauración de foco y contexto

- Al cerrar un diálogo, el foco vuelve al disparador que lo abrió; si ya no existe, vuelve al encabezado de la sección.
- Al regresar desde detalle, se restaura el listado con filtros GET, cursor protegido cuando corresponda, ancla estable de fila y foco en el enlace que abrió el detalle.
- Si el contexto está ausente, vencido o inválido, se vuelve al listado y el foco se coloca en su `h1`.
- El historial del navegador no debe repetir una mutación ni conservar secretos.

### BR-N06 — filtros y formularios

- Permanecen en query string sólo filtros reales de lectura, no sensibles y allowlisted, además del cursor opaco contractual.
- Permanecen exclusivamente en el formulario: contraseñas, TOTP, recovery codes, motivos, ETag, `Idempotency-Key`, CSRF, archivos, contenido de evidencia, estado de diálogo y valores de mutación.
- Las query desconocidas se rechazan o se normalizan de forma cerrada; no se reflejan como HTML sin codificación.

### BR-N07 — deep links y respuesta segura

- Sólo son deep links las páginas GET implementadas y registradas; cada petición vuelve a autenticar y autorizar.
- Un 401 redirige a acceso con el destino seguro de BR-N02. Un 403 de capacidad de sección conserva 403 y no se convierte en login.
- Para un identificador concreto, recurso inexistente y recurso fuera de alcance usan una presentación 404 convergente: “No existe o no está disponible en tu alcance”. No se prueban identificadores alternativos.
- No existen deep links a pasos preauth, diálogos, mutaciones, `/api/v1` ni URLs firmadas.

### BR-N08 — visibilidad y autoridad

- El shell usa `roleCode` y `permissions` del snapshot de sesión únicamente para visibilidad gruesa. No usa `ClaimsPrincipal.IsInRole`, puesto ni turno para decidir autoridad.
- Las acciones de un recurso se muestran sólo desde `allowedActions` o `links` reconocidos cuando la respuesta los contiene. Ausencia o código desconocido significa no mostrar.
- Las acciones conocidas son `VIEW_TASK`, `CONTRIBUTE_EVIDENCE`, `CONCLUDE_TASK` y `MARK_NOTICE_READ`. Las relaciones conocidas son `self`, `eligibility`, `evidence`, `evidenceReview`, `validations` e `issueDecision`.
- Cada relación se traduce mediante un mapa cerrado a una ruta Razor; no se renderiza un `href` API arbitrario. Una llamada forzada sigue sujeta a autorización y no-efecto del servidor.

## 5. Contrato aprobado de sesión y CSRF en Razor

### 5.1 Consulta y vida del snapshot

Un servicio Razor request-scoped consulta `GET /api/v1/auth/session` como máximo una vez por petición protegida cuando layout o PageModel requieren sesión. El resultado puede compartirse sólo dentro de esa petición. No hay caché entre peticiones, cookie paralela de frontend, `localStorage`, `sessionStorage` ni estado global por usuario.

Un 200 reemplaza por completo el snapshot. Un 401, respuesta inválida, logout o cambio de principal lo descarta. El backend conserva la revalidación de cuenta, persona, empleo, rol, MFA y `SecurityStamp` en cada petición autenticada.

### 5.2 Cookies permitidas

El navegador envía las cookies `HttpOnly` al proceso Razor por HTTPS de mismo origen. El cliente común nunca copia la cabecera `Cookie` completa. Extrae por nombre exacto, rechaza duplicados y reenvía sólo:

| Cookie | Uso interno permitido |
|---|---|
| `__Host-SGOL-Session` | Sesión, logout y endpoints de negocio que requieren sesión plena |
| `__Host-SGOL-PreAuth` | Sólo password, MFA y recovery que aceptan el desafío; nunca negocio |
| `__Host-SGOL-CSRF` | Sólo una mutación que lleve el `X-CSRF-TOKEN` correspondiente |

Las llamadas permanecen en el mismo origen, bajo `/api/v1`, sin `CookieContainer` compartido y sin redirecciones automáticas. Ningún valor de `Cookie`, `Set-Cookie` o token aparece en logs, excepciones, auditoría, trazas, métricas, salida de pruebas o capturas.

### 5.3 Obtención y remisión de CSRF

Las páginas Razor usan el mismo `IAntiforgery` registrado por SGOL. El GET del formulario emite o renueva `__Host-SGOL-CSRF` en la respuesta al navegador y renderiza el request token como campo antiforgery. Razor valida el formulario en la frontera web y entrega explícitamente el token validado al cliente común, que remite `X-CSRF-TOKEN` junto con la cookie CSRF exacta a la API. La API vuelve a validar el par.

Las páginas Razor server-rendered no usan una llamada interna a `GET /api/v1/auth/csrf` para crear el par, porque su `Set-Cookie` no llegaría automáticamente al navegador. El endpoint permanece intacto para clientes de navegador y pruebas de API.

Tras login, cambio de contraseña, confirmación MFA, regeneración, logout o cambio de usuario, el par anterior se invalida y un nuevo GET genera otro. Un fallo CSRF no se reintenta automáticamente y no produce efecto funcional.

### 5.4 Propagación de `Set-Cookie`

El adaptador de autenticación Razor puede propagar a la respuesta externa únicamente `Set-Cookie` de `__Host-SGOL-Session`, `__Host-SGOL-PreAuth` y `__Host-SGOL-CSRF`. Antes verifica nombre exacto, `Secure`, `HttpOnly`, `SameSite=Strict`, `Path=/`, ausencia de `Domain` y CR/LF, y que la ruta invocada pueda emitir o eliminar esa cookie. Encabezados adicionales, duplicados incompatibles o atributos menos restrictivos fallan cerrados sin aplicación parcial.

No se transforma el valor ni se crea otra cookie de sesión. Una respuesta que cambia el principal invalida snapshot, CSRF, navegación y affordances anteriores.

### 5.5 401, 403, expiración, logout y cambio de usuario

- 401 o `SESSION_INVALID`: descartar snapshot y cookies allowlisted; un GET protegido conserva sólo el destino seguro, mientras un POST descarta cuerpo y secretos y no se reintenta.
- Preauth inválida o vencida: eliminar preauth, descartar campos sensibles y volver a acceso sin confirmar existencia de cuenta.
- 403: conservar sesión, mostrar denegación segura y no probar roles o identificadores.
- 404: usar la presentación convergente de BR-N07.
- Logout confirmado por 204: aplicar las eliminaciones allowlisted, eliminar antiforgery externo y volver a acceso.
- Si el backend no confirma el logout, Razor puede eliminar localmente las cookies para contener la sesión, pero informa que el cierre remoto no pudo confirmarse y nunca afirma auditoría remota.
- Un nuevo login limpia estado de principal anterior; no reutiliza destino que ya no esté autorizado, snapshot, menú, CSRF ni acción del usuario anterior.

## 6. Fuente de diseño y correcciones asociadas

Se crea `docs/design/navegacion.md` como fuente operativa para rutas, grupos, cuatro roles, retorno, filtros, deep links, estados 401/403/404 y separación entre visibilidad y autoridad. `docs/design/componentes.md` conserva la descripción visual del ítem lateral, corrige “tres perfiles” por cuatro roles canónicos y remite al nuevo documento.

Las historias posteriores sólo pueden materializar una ruta cuando su propio contrato y backend estén listos. Esta adenda no amplía `TECH-FRONT-004/005` ni `FRONT-001..020`.

## 7. Aceptación futura de `TECH-FRONT-003`

Cuando exista una orden separada de implementación, `TECH-FRONT-003` debe comprobar al menos:

1. una consulta de sesión por petición Razor protegida y ninguna caché entre peticiones;
2. cuatro roles, preauth sin acceso a negocio y navegación sin `ClaimTypes.Role`;
3. allowlist de cookies por ruta/método y rechazo de cookie o `Set-Cookie` inesperada;
4. par CSRF válido de navegador a Razor y API, con no-efecto en ausencia, mezcla o expiración;
5. rotación/limpieza tras login, password, MFA, logout, 401 y cambio de usuario;
6. vectores de open redirect rechazados y destino válido conservado durante toda la cadena;
7. traducción segura de 401/403/404 y Problem Details sin JSON crudo ni secretos;
8. foco, teclado, estados cargando/vacío/error y comportamiento responsivo del shell.

Estas pruebas requieren Kestrel HTTPS y PostgreSQL real para la sesión persistida; handlers sintéticos sólo pueden verificar el protocolo del adaptador.

## 8. Punto de parada

La incorporación de esta adenda resuelve la omisión contractual, pero no inicia ni reanuda `TECH-FRONT-003`. No se autoriza código, pruebas de implementación, publicación, PR, merge ni despliegue. El siguiente acto permitido es una orden explícita e independiente para comenzar la tarea sobre una base que contenga esta documentación.
