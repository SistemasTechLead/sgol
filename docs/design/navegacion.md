# Navegación del frontend SGOL

## Control

Esta fuente operativa materializa las decisiones aprobadas por la Adenda 46. Describe presentación y navegación; no concede autorización, no crea endpoints y no declara implementada una página. SGOL reconoce cuatro roles canónicos: `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION` y `PISO_VENTAS`.

## Registro de rutas

| ID | Ruta web | Unidad o grupo | Condición de presentación |
|---|---|---|---|
| `NAV-ENTRY` | `/` | Entrada | Pública; decide un destino seguro mediante sesión real |
| `NAV-AUTH-LOGIN` | `/acceso` | UI-A01 | Sin sesión plena |
| `NAV-AUTH-PASSWORD` | `/acceso/cambiar-contrasena` | UI-A02 | Sólo después de `nextStep=CHANGE_PASSWORD` |
| `NAV-AUTH-MFA-ENROLL` | `/acceso/mfa/enrolar` | UI-A03 | Sólo después de `nextStep=ENROLL_MFA` |
| `NAV-AUTH-MFA-VERIFY` | `/acceso/mfa/verificar` | UI-A04 | Sólo después de `nextStep=VERIFY_MFA` |
| `NAV-AUTH-RECOVERY` | `/acceso/codigos-recuperacion` | UI-A05 | Sólo para visualización o regeneración autorizada |
| `NAV-MY-WORK` | `/mi-trabajo` | Anfitrión del shell; UI-E01..E08 aún no implementadas | Los cuatro roles con sesión plena; ningún hijo se muestra hasta que su historia lo implemente |
| `NAV-VALIDATION` | `/validaciones` | UI-V01..V04 | Dirección, Administración y Subcoordinación; recurso reautorizado |
| `NAV-PLANNING` | `/planificacion` | UI-C02/C03 y UI-G01..G06 | Sólo hijos cuyo contrato permita presentarlos al rol actual |
| `NAV-IDENTITY` | `/personas-y-accesos` | UI-I01..I07 | Dirección; hijos con sus permisos específicos |
| `NAV-CONFIGURATION` | `/configuracion` | UI-C01/C04..C09 | Lectura o administración según el contrato del hijo |
| `NAV-INDICATORS` | `/indicadores` | UI-R01..R02 | UI-R01 por alcance; UI-R02 sólo Dirección |
| `NAV-AUDIT` | `/auditoria` | UI-U01..U02 | Actor con contrato de auditoría y alcance jerárquico |
| `NAV-CONTINUITY` | `/continuidad` | UI-K01..K03 | Sólo Dirección |

Una ruta queda disponible únicamente cuando la historia que la consume la implementa. El shell no muestra enlaces rotos ni usa rutas de mutación `/api/v1` como destinos. Los segmentos variables aceptan sólo el tipo cerrado por el contrato de la historia.

`FRONT-002` materializa `/mi-trabajo` sólo como GET protegido y anfitrión vacío del shell. No materializa bandeja, avisos, obligaciones, evidencia, conclusión ni unidades UI-E01..UI-E08. El enlace de anfitrión no se presenta como hijo funcional del menú; el grupo «Mi trabajo» permanece oculto mientras no tenga un hijo implementado y visible. `/` sigue siendo entrada pública que envía a `/acceso` sin sesión plena y a `/mi-trabajo` con sesión plena.

## Grupos y unidades

| Grupo | Unidades | Visibilidad gruesa |
|---|---|---|
| Mi trabajo | E01..E08 | Los cuatro roles; cada hijo y recurso decide por contrato y affordance |
| Validación y supervisión | V01..V04 | Dirección, Administración y Subcoordinación |
| Planificación y generación | C02/C03, G01..G06 | Sólo hijos con predicado contractual demostrable; Piso sólo cuando se autorice expresamente |
| Personas y accesos | I01..I07 | Dirección |
| Configuración | C01, C04..C09 | Administración o lectura según el contrato de cada hijo; mutaciones de Dirección |
| Indicadores | R01..R02 | R01 por alcance permitido; R02 sólo Dirección |
| Auditoría | U01..U02 | Según permiso y alcance jerárquico del contrato de auditoría |
| Continuidad | K01..K03 | Dirección |

A01..A05 viven fuera del shell. A06 y A07 pertenecen al encabezado de sesión. Un grupo sin al menos un hijo implementado y visible no aparece. La visibilidad nunca sustituye la autorización del servidor.

## Entrada, sesión y destino de retorno

- `/` consulta el contexto request-scoped. Un anónimo va a `/acceso`; una sesión plena de cualquier rol va a `/mi-trabajo` o a un destino protegido válido.
- Password y MFA sólo continúan desde el `nextStep` del backend. Una preautenticación huérfana reinicia acceso y nunca abre negocio.
- El destino solicitado debe ser GET, local, implementado, registrado y con query allowlisted. Se protege mediante Data Protection durante 30 minutos y se revalida tras cada paso de autenticación.
- Un destino alterado, vencido, externo, API, mutante, firmado o ya no autorizado cae en `/mi-trabajo`.
- La sesión se consulta como máximo una vez por petición Razor protegida. No se conserva entre peticiones ni en almacenamiento del navegador.

## Composición de recorridos

- Listado: descubrimiento, filtro, paginación y selección.
- Detalle GET: identidad, historia y secciones del recurso.
- Página de formulario: edición compleja o recorrido multipaso.
- Diálogo: confirmación contextual corta y motivada; nunca deep link, formulario complejo ni mutación GET.

Personas, TAR, obligaciones y validaciones siguen este patrón. Cuentas y roles no obtienen detalle navegable hasta que su historia cierre las brechas de API correspondientes.

## Foco, regreso y filtros

- Un diálogo devuelve foco al disparador; si desapareció, al encabezado de sección.
- Regresar desde detalle restaura filtros GET, cursor protegido, ancla de fila y foco del enlace de origen. Si el contexto es inválido, vuelve al listado y enfoca su `h1`.
- Sólo filtros reales de lectura, no sensibles y allowlisted, además del cursor opaco, permanecen en query string.
- `/acceso` admite exclusivamente un aviso transitorio protegido de resultado de sesión (`cerrada`, `terminada` o `no-confirmada`) para mostrar el mensaje aprobado tras redirección. El valor no contiene identidad ni secreto, tiene vigencia breve y no se reutiliza como autoridad o destino.
- Contraseñas, TOTP, recovery codes, motivos, ETag, idempotencia, CSRF, archivos, evidencia, mutaciones y estado de diálogo permanecen en formularios y nunca en la URL.

## Deep links y errores seguros

- Sólo páginas GET implementadas y registradas admiten deep link. Cada llegada vuelve a autenticar y autorizar.
- 401 lleva a acceso con destino protegido; un POST no conserva cuerpo ni secreto.
- 403 de capacidad de sección permanece 403 y no lleva a login.
- Para un identificador concreto, inexistencia y fuera de alcance muestran el mismo 404: “No existe o no está disponible en tu alcance”.
- No hay deep links a pasos preauth, diálogos, mutaciones, `/api/v1` ni URLs firmadas.

## Visibilidad y acciones

`roleCode` y `permissions` de `GET /api/v1/auth/session` sirven sólo para el shell. No se usa `IsInRole`, puesto o turno como autoridad. Cuando un recurso entrega affordances, se reconocen únicamente:

- acciones: `VIEW_TASK`, `CONTRIBUTE_EVIDENCE`, `CONCLUDE_TASK`, `MARK_NOTICE_READ`;
- relaciones: `self`, `eligibility`, `evidence`, `evidenceReview`, `validations`, `issueDecision`.

La ausencia o un valor desconocido oculta la acción. Un mapa cerrado traduce una relación conocida a ruta Razor; nunca renderiza un `href` API arbitrario. El backend vuelve a autorizar incluso una solicitud forzada.

## Sesión, cookies y CSRF

Razor recibe cookies `HttpOnly` desde el navegador y el cliente común reenvía por allowlist sólo `__Host-SGOL-Session`, `__Host-SGOL-PreAuth` y `__Host-SGOL-CSRF` cuando la ruta y el método las admiten. No copia la cabecera `Cookie` completa. Las páginas usan `IAntiforgery`; una mutación API recibe el `X-CSRF-TOKEN` validado y la cookie CSRF del mismo par. La propagación de `Set-Cookie` se limita a esos tres nombres y valida atributos seguros antes de aplicarla.

401, logout y cambio de principal descartan snapshot, navegación, affordances y CSRF. Un fallo remoto de logout permite contención local, pero la interfaz no afirma que el backend lo haya auditado.

## Estados y adaptación

- Normal: destino y grupo actuales se expresan con texto y `aria-current`.
- Foco: anillo visible según tokens; el orden sigue encabezado, navegación y contenido.
- Deshabilitado: sólo para una acción visible temporalmente no disponible, nunca para anunciar una sección no autorizada.
- Cargando: `aria-busy` en la región afectada, sin presumir rol ni acciones.
- Vacío: mensaje y siguiente acción autorizada; un grupo vacío no se muestra.
- Cuando no exista ninguna página `NAV-*` implementada y visible, el shell muestra «Aún no hay secciones disponibles» sin enlace de acción. Texto aprobado expresamente por el responsable durante `TECH-FRONT-003`; no habilita rutas futuras.
- Error: mensaje funcional aprobado, `correlationId` cuando exista y foco en el resumen; nunca JSON crudo ni secretos.

En teléfono, la apertura y cierre del panel conservan foco, bloqueo modal y `Escape`; en escritorio, el orden y los nombres accesibles son equivalentes. Todo comportamiento cumple `accesibilidad.md`, `componentes.md` y `estados-y-mensajes.md`.
