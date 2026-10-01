# Navegación del frontend SGOL

## Precisión vigente del estilo v2

La base visual v2 está aceptada y documentada en [ESTILO_VISUAL_V2.md](ESTILO_VISUAL_V2.md), por solicitud expresa de actualización del responsable el 2026-09-30. Sus reglas precisan los ejemplos anteriores: secundario neutro sin borde rojo, fuentes locales, logo/iconos, sesión a la derecha y acceso con composición de marca/formulario. Las correcciones y el complemento fueron aprobados mediante «Apruebo las correcciones y el complemento del plan para implementar». La implementación local se registra por grupo en [DISENO_RENOVADO_V2_IMPLEMENTACION.md](../traceability/DISENO_RENOVADO_V2_IMPLEMENTACION.md); la aprobación del diseño no equivale a publicación o integración. Los contratos y mensajes funcionales se conservan.

## Diseño renovado — suplemento de presentación aprobado

[Adenda 55](../../F07_ADENDA_55_REFERENCIA_DE_DISENO_RENOVADO.md) y [referencia-renovada.md](referencia-renovada.md) complementan la presentación sin cambiar rutas, grupos, sesión, permisos, destinos ni filtros. Encabezado blanco, columna lateral blanca de `--ancho-navegacion`, área de trabajo cálida y main con `--ancho-contenido`. El orden DOM sigue salto al contenido, identidad/sesión/logout, navegación y main; no se cambia tabulación para replicar la maqueta.

Items normales conservan texto/icono, activo usa acento, superficie elevada, peso medio, borde y `aria-current`; foco y área mínima usan variables de tokens.md. Un grupo sin hijos implementados/visibles no aparece. No se muestran nuevas rutas por decoración.

En viewport estrecho se conserva el panel modal de navegación existente: disparador, foco contenido, Escape y retorno. Identidad, rol, expiración y logout refluyen y permanecen disponibles. La navegación abierta en línea y la ocultación del pie de la maqueta no son reglas oficiales. El encabezado de página sólo muestra contexto confirmado y acciones autorizadas; no añade breadcrumbs, períodos supuestos ni conteos globales a partir de una página con cursor.

La aprobación documental no migra las pantallas existentes. Tareas pendientes consumen esta referencia y materializan sólo lo necesario; la adaptación general tiene su propio hito, sin convertirse en dependencia artificial de FRONT-017..020. Los registros funcionales e históricos siguientes conservan su contexto.

## FRONT-016 — Mi trabajo

Adenda 54 incorpora §§4–6 del plan aprobado: `/mi-trabajo` materializa UI-E01/E02 y consulta HU-023 con PER-BANDEJA-PROPIA/PER-TAREA-VER respectivamente. Hijo visible «Mi trabajo» para cuatro roles con permiso vigente; detalle GET `/mi-trabajo/tareas/{obligationId:guid}` reautoriza por API. Los períodos se seleccionan desde filas autorizadas; no se consulta GET /weeks ni se materializa semana. La descripción de anfitrión vacío de FRONT-002 abajo es histórica. UI-E04..E08 permanecen pendientes. Marcación es POST separado del logout; abrir, filtrar o navegar no marca avisos ni cambia ejecución.

## UI-G05 y UI-G06

Adenda 53 §§6/7 aprobadas el 2026-09-29: ambas unidades viven en `/planificacion`. UI-G05 es visible a los cuatro roles con PER-PLAN-VER, incluido Piso expresamente. Publicar se presenta sólo con PER-PLAN-PUBLICAR proyectado; la API reautoriza. La historia selecciona publicaciones del mismo plan sin ruta web adicional ni enlaces a pantallas de FRONT-016. Año, semana y cursores son filtros GET; ninguna navegación publica o crea el plan.

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

UI-C05 se presenta dentro de `/configuracion`: el catálogo y el detalle de una TAR seleccionada comparten la ruta registrada. La selección usa únicamente un código de las ocho TAR canónicas en una query GET allowlisted; un valor distinto se rechaza sin consultar otra definición. Crear borrador, publicar y desactivar son POST de formularios, sin deep links a diálogos ni cambios de estado mediante GET. El enlace de UI-C05 sólo aparece cuando la sección está implementada y el permiso de presentación permite consultarla; el backend reautoriza cada solicitud.

UI-C06/C07 se presentan como secciones del mismo detalle TAR en `/configuracion?taskCode=...`. La selección conserva exclusivamente el código canónico allowlisted; no hay nueva ruta web ni enlaces `/api/v1`. La historia se consulta al abrir el detalle y al recargar explícitamente tras un conflicto. Guardar usa formularios POST con antiforgery e intención idempotente, sin mutación ni apertura de diálogo mediante GET. Cada política exige su propio permiso proyectado para presentación y autorización nueva en el servidor.

UI-C08/C09 siguen la misma ruta de detalle TAR y el mismo código allowlisted. Sus secciones sólo aparecen con `PER-EVIDENCIA-CONFIG` o `PER-VALIDACION-CONFIG` respectivamente; cada GET y PUT se reautoriza en servidor. La lectura de cada política entrega versión vigente, historia y ETag propio. Los formularios POST guardan mediante sus rutas API existentes; no hay enlace API de menú, deep link de diálogo ni mutación GET. Un conflicto exige recarga explícita de la sección afectada.

`FRONT-003` materializa `/personas-y-accesos` como lista y formulario de alta de UI-I01. El detalle de una persona usa exclusivamente GET `/personas-y-accesos/personas/{personId:guid}`; el identificador no concede acceso y cada solicitud vuelve a consultar la API autorizada. Ninguna UI-I02..I07 queda implementada por habilitar el grupo. La alta permanece en POST de la ruta de lista, con antiforgery e idempotencia fuera de la URL.

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

## UI-G01 — navegación consumidora aprobada

Aprobada expresamente durante FRONT-013: la función vive en una sección «Alta manual» de `/planificacion`, con enlace de ancla dentro de esa ruta sólo cuando esté implementada y PER-OBLIGACION-CREAR permita presentarla. Conserva las secciones actuales y oculta funciones posteriores.

La selección GET admite sólo una TAR canónica allowlisted mediante taskCode; el resultado admite generationRequestId UUID en la misma ruta. Los filtros ya aprobados de semana/calendario conservan su contrato. Referencias, datos del formulario, intención, clave y CSRF quedan fuera de URL. Una consulta vuelve a autorizar; recurso inexistente o invisible usa el 404 convergente. No hay deep link a confirmación, mutación GET ni enlace API de menú.

«Recuperar resultado» reenvía sólo la intención original conservada tras decisión explícita; no crea otra. «Preparar otra solicitud» no envía automáticamente. Un GET de resultado debe terminar antes de presentar el estado siguiente; el valor anterior al submit no acredita la nueva respuesta. La aprobación es de composición: los esquemas CAT y cambios funcionales pendientes requieren el contrato de Adenda 51.

La aprobación íntegra posterior «Si la apruebo» da eficacia a Adenda 51 y resuelve la limitación de aprobación parcial descrita arriba para UI-G01.

## UI-G02/G03/G04 — composición consumidora aprobada

El responsable aprobó íntegramente las secciones 2–7 de `docs/traceability/FRONT_014_PROPUESTA_CONSUMIDOR.md` mediante «La apruebo» el 2026-09-29. Esas secciones se incorporan por referencia como contrato operativo específico de estas unidades, exclusivamente para FRONT-014. La composición vive en `/planificacion`, conserva el snapshot confirmado, carga recibida y confirmación motivada con intención protegida. No incorpora bandeja ni asignación automática. Se reutilizan BR-D04/D13, tokens y estados existentes; el servidor decide elegibilidad y autoridad.

## FRONT-017 — composición consumidora aprobada

Adenda 56 incorpora íntegramente FRONT_017_PLAN_DE_IMPLEMENTACION.md, aprobado mediante «Apruebo integramente el plan, recuerda seguir los documentos de diseño para crear las pantallas a como están diseñadas las anteriores». §§3–7 definen BR-D07/D08, los 18 formularios cerrados, etiquetas, mensajes, estados y navegación dentro del detalle de tarea. Se conserva estilo v2, CSS propio/variables/componentes compartidos. Sólo primera aportación; LIMPIO permite aportar, no confirma evidencia. Sin preview/descarga/sustitución/conclusión. Los textos de §6 son catálogo operativo literal; PENDIENTE_CARGA y PENDIENTE_ESCANEO se distinguen. Cada booleano Sí/No sin selección inicial. No hay JSON libre ni biblioteca adicional.

## FRONT-018 — composición consumidora aprobada

Adenda 57 incorpora íntegramente el plan FRONT_018_PLAN_DE_IMPLEMENTACION.md, aprobado mediante «Si apruebo integramente el plan». Sus §§5–7 son contrato operativo literal de UI-E06/E07/E08: versiones paginadas, revisión explícita, faltantes exactos, sustitución con autoridad/motivo y conclusión sin cuerpo. Conserva estilo v2, seis estados, componentes compartidos, variables oficiales, accesibilidad y navegación en el detalle existente. Descarga/preview excluidos. Revisión tiene únicamente la excepción atómica de snapshot/auditoría de Adenda 19; no cambia ejecución ni decisiones anteriores.
## FRONT-019 — composición consumidora aprobada

Adenda 58 incorpora el plan único FRONT_019_PLAN_DE_IMPLEMENTACION.md aprobado mediante «Apruebo el plan». Sus §§4–8 son el contrato operativo de UI-V01..V04: pendientes/supervisión en /validaciones; emisión, sustitución e historia en el detalle existente, tres resultados, fundamento y motivos separados, confirmación y conflicto. BR-D09/M09 resueltas sólo para esta consumidora. Catálogo literal y seis estados en §§7–8; mapas cerrados de requisito, resultado y autoridad con pares/iconos existentes. CSS propio, variables oficiales y base v2 conservados. Proyección validationActions sólo de presentación, servidor reautoriza. Lecturas puras sin efectos; revisión explícita conserva snapshot/auditoría atómicos deduplicados. Descarga/preview excluidos sólo para FRONT-019; BR-API04 global permanece abierta. Sin FRONT-020/TECH-FRONT-005.
