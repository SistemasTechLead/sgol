# SGOL — Adenda 45 a F07: backlog frontend definitivo

## 1. Control

| Campo | Valor |
|---|---|
| Estado | `APROBADA ÍNTEGRAMENTE POR EL RESPONSABLE EL 2026-09-22 — INCORPORADA LOCALMENTE` |
| Fuente de derivación aprobada | Inventario exacto y registro de brechas reconciliados el 2026-09-22; esta adenda cierra dentro de sí el orden, dependencias y condiciones de cada tarea |
| Arquitectura | Razor Pages/MVC en `Sgol.Web`, mejora progresiva, mismo origen |
| Base backend verificada | `origin/master = 32c3961ff67f79670d0824da71d4f70a06d1dc01`; CV-04 y CV-05 integrados con checks requeridos verdes y ascendencia comprobada |
| Efecto futuro | Insertar puertas técnicas e historias frontend después de aprobar e incorporar `TECH-UI-PLAN-001` |
| Numeración oficial de adenda | `45` |

El responsable aprobó íntegramente esta adenda y ordenó su incorporación el 2026-09-22. Los IDs `TECH-FRONT-*` y `FRONT-*` quedan insertados al integrarse este documento; no renumeran HU-001 a HU-035 ni modifican sus contratos. Ninguna tarea queda iniciada por esta aprobación.

## 2. Puerta de entrada

Ninguna fila puede comenzar hasta que:

1. se satisfaga la puerta completa de `F07_ADENDA_44_PLANIFICACION_CONTRACTUAL_DEFINITIVA_DEL_FRONTEND.md`;
2. el inventario, brechas y este backlog estén aprobados e incorporados;
3. la trazabilidad incorporada registre los estados finales de HU-029, HU-031..035, TECH-AUTH-001 y TECH-E2E-CV-04/05, incluidos SHA, checks y merges;
4. exista orden explícita del usuario para iniciar la primera tarea.

Las brechas BR-API01..BR-API13 y BR-D/M/N no son una barrera global: cada una bloquea únicamente la fila que la declara como dependencia. `TECH-FRONT-001` puede comenzar precisamente para resolver el paquete de diseño aprobado; ninguna pantalla funcional puede adelantarse.

## 3. Reglas comunes heredadas por todas las filas

Cada tarea incluye, aunque no se repita en la tabla:

- sólo campos y respuestas reales enumerados en el inventario;
- autorización y guardas en servidor; ocultar navegación es presentación, no autoridad;
- CSRF en mutaciones; `Idempotency-Key` por intención; ETag/`If-Match` sin retry automático;
- estados normal, foco, deshabilitado, error, cargando y vacío cuando sean aplicables;
- WCAG 2.2 AA según F06, teclado completo, área 44×44, etiquetas/errores asociados y estado no sólo por color; BR-D17 debe aclarar la referencia WCAG 2.1 de `docs/design/accesibilidad.md` antes de cerrar `TECH-FRONT-001`;
- caso positivo, negativo, autorización/IDOR, auditoría cuando corresponda y no-efecto;
- datos sintéticos; ningún secreto ni evidencia real;
- trazabilidad en `IMPLEMENTATION_STATUS`, mapa HU/CA/CP/CAT/RN, pruebas y brechas resueltas;
- exclusión permanente de SPA, CORS, repositorio separado, reglas de dominio en cliente, despliegue/cloud/cuentas externas/observabilidad productiva.

Una historia con brecha de diseño no comienza hasta incorporar la mínima extensión de `docs/design`. Una prueba Playwright no sustituye API/PostgreSQL para autorización, transacción o auditoría.

## 4. Puertas técnicas

| Orden / ID | Resultado demostrable | Dependencias exactas | Alcance y exclusiones | Estados/contratos/autorización | Criterios y prueba local enfocada | Habilita / trazabilidad |
|---|---|---|---|---|---|---|
| 1 — `TECH-FRONT-001` | Informe ejecutable de auditoría de la base `TECH-UI-001` y paquete aprobado de brechas de diseño | Puerta de sección 2; BR-D17 incluida | Audita layout, tokens, CSS, parciales y única Razor de sucursal; agrega sólo componentes/documentación aprobados. Sin pantalla funcional nueva | Verifica todos los estados, responsive, teclado, cursor, credencial, textarea/radio/fecha/modal/upload/mensajes y norma WCAG aplicable | Pruebas de render/componentes y arquitectura; contraste/teclado; cero literal visual consumidor | Habilita `TECH-FRONT-002`; actualiza diseño, mapa de componentes y estado de tarea |
| 2 — `TECH-FRONT-002` | Cliente Razor/HTTP común consume `/api/v1`, conserva cookies y traduce Problem Details por `code` | `TECH-FRONT-001`; BR-API11/12 resueltas | Cliente tipado, envelopes, cursor, ETag, idempotencia por intención y errores. Sin regla de negocio ni pantalla funcional | 400/401/403/404/409/412/413/415/422/423/428/429/500/503; `correlationId`; servidor sigue autorizando | Componentes con handler sintético: éxito/error, cuerpo cerrado, ETag, replay, no retry 412, no fuga de secreto | Habilita `TECH-FRONT-003`; registra matriz código→mensaje y pruebas |
| 3 — `TECH-FRONT-003` | Infraestructura de sesión, CSRF, expiración y navegación condicionada consume autenticación real | `TECH-FRONT-002`; mapa BR-N01..N08 aprobado | Estado de sesión por request, obtención CSRF, invalidación y shell. Sin login visual definitivo ni permisos inventados | Cookie segura/mismo origen; preauth no es principal; expiración limpia estado; navegación no autoriza | Kestrel HTTPS + PostgreSQL enfocado: cookie/CSRF/sesión válida-expirada-invalidada; teclado del shell | Habilita `TECH-FRONT-004` y `FRONT-001`; actualiza seguridad/frontend |
| 4 — `TECH-FRONT-004` | Harness Playwright común inicia SGOL real, datos sintéticos y axe/equivalente sin secretos | `TECH-FRONT-003` | Fixtures por cuatro roles, captura sanitizada, viewport escritorio/móvil, teclado; sin suite funcional completa | Login real sólo cuando `FRONT-001` exista; antes prueba shell/401 con fixture autorizado separado | Smoke de navegador, foco, títulos, landmarks, no errores críticos de accesibilidad; cleanup | Habilita historias con navegador; registra comandos, navegadores y evidencia |
| 25 — `TECH-FRONT-005` | Demo integral final recorre frontend completo con cuatro roles y backend real | `FRONT-001`..`FRONT-020`, cero brechas bloqueantes | Harness final, no comportamiento productivo nuevo; sin deploy/cloud | Sesión/CSRF/jerarquía/IDOR/auditoría/no-efecto/concurrencia, 9 recorridos | Playwright HTTPS + PostgreSQL/S3/ClamAV sólo donde el recorrido de evidencia lo exige; dos ciclos, cleanup y reporte sanitizado | Habilita cierre frontend; actualiza trazabilidad final y mapa de cobertura |

## 5. Historias frontend ordenadas

### 5.1 Acceso y shell

| Orden / ID | Resultado visible demostrable | Dependencias | Alcance, pantallas y estados; exclusiones | Backend reutilizado y autorización | Criterios positivos/negativos/a11y/no-efecto y prueba | Habilita / trazabilidad |
|---|---|---|---|---|---|---|
| 5 — `FRONT-001` | Usuario completa login, cambio obligatorio, TOTP/recovery y llega a sesión plena | `TECH-FRONT-003/004`; BR-D01/D02, BR-M01/M02 resueltas | UI-A01..A05; normal/foco/error/carga/bloqueo/desafío expirado/única visualización. Sin SSO/JWT/recordarme | Nueve rutas `auth`; CSRF; rate limit; preauth restringida; secretos efímeros | Positivo primer acceso y login recurrente; negativo no enumerable, TOTP/recovery inválido, CSRF y bloqueo; teclado, autocomplete; no cookie plena antes de MFA. Prueba Playwright HTTPS enfocada | Habilita toda historia funcional; traza TECH-AUTH-001, seguridad y pruebas |
| 6 — `FRONT-002` | Usuario ve identidad/rol/expiración, navega sólo opciones visibles y cierra sesión | `FRONT-001`; BR-D03/D15/D16, BR-N01..N08 resueltas | UI-A06/A07 y shell; estados de sesión válida/expirada/invalidada y navegación responsive. Sin autorización cliente | `GET auth/session`, `POST logout`; permisos sólo para presentación; servidor revalida | Cuatro roles ven menú previsto; deep link denegado no filtra; logout/expiración limpian datos; teclado móvil/escritorio. Playwright por rol | Habilita recorridos; actualiza mapa de pantallas/navegación y sesión |

### 5.2 Identidad y organización

| Orden / ID | Resultado visible demostrable | Dependencias | Alcance, pantallas y estados; exclusiones | Backend reutilizado y autorización | Criterios positivos/negativos/a11y/no-efecto y prueba | Habilita / trazabilidad |
|---|---|---|---|---|---|---|
| 7 — `FRONT-003` | Dirección lista, crea y consulta persona con historial laboral | `FRONT-002`; componentes tabla/formulario | UI-I01; vacío/error/carga; sin empleo editable todavía | `GET/POST people`, `GET people/{id}`; `PER-PERSONA-ADMIN`; idempotencia | CA/CP-001 alta/duplicado; otros roles 403; auditoría/no-efecto; teclado tabla/form. API + Playwright | Habilita `FRONT-004`; traza HU-001 |
| 8 — `FRONT-004` | Dirección cambia empleo/puesto/turno y baja/reactiva conservando historia | `FRONT-003`; BR-D04 resuelta | UI-I02; diálogo motivado y conflicto; sin rol/cuenta | PATCH y POST empleo; ETag/idempotencia; `PER-PERSONA-ADMIN` | CA/CP-001/002; puesto “Director” no eleva; 412 no sobrescribe; auditoría. API concurrencia + Playwright | Habilita `FRONT-005/006`; traza HU-001/HU-002 |
| 9 — `FRONT-005` | Dirección administra disponibilidad binaria por día y rango | `FRONT-004`; BR-D05 resuelta | UI-I03; rango vacío, valor existente/nuevo, fecha inválida | GET/PUT availability; `PER-DISPONIBILIDAD-ADMIN`; If-Match al corregir | CA/CP-003; sólo booleano; zona local; conflicto/no-efecto; teclado calendario. API + Playwright | Habilita generación/elegibilidad; traza HU-003/RN-004/005 |
| 10 — `FRONT-006` | Dirección lista, crea, desactiva y reactiva cuentas individuales | `FRONT-004`; BR-API08 y canal temporal resueltos | UI-I04/I05; contraseña temporal protegida; sin rol/reset MFA | users list/create/status; `PER-USUARIO-ADMIN`; idempotencia; invalida sesiones | CA/CP-006; cuenta compartida/duplicada y otros roles denegados; no secreto en DOM/log/captura. API + Playwright | Habilita `FRONT-007`; traza HU-006/seguridad |
| 11 — `FRONT-007` | Dirección consulta/cambia/revoca rol y ejecuta reset MFA motivado | `FRONT-006`; BR-API07, BR-D04 resueltas | UI-I06/I07; historia, ETag, confirmación de impacto. Sin break-glass | role-assignments y mfa-reset; `PER-ROL-ADMIN`/`PER-USUARIO-ADMIN`; MFA reciente | CA/CP-007; rol único, no autoridad por puesto, conflicto, invalidación inmediata; reset no accesible a otros roles. API + Playwright | Habilita toda matriz por rol; traza HU-007/TECH-AUTH-001 |

### 5.3 Gobierno de configuración

| Orden / ID | Resultado visible demostrable | Dependencias | Alcance, pantallas y estados; exclusiones | Backend reutilizado y autorización | Criterios positivos/negativos/a11y/no-efecto y prueba | Habilita / trazabilidad |
|---|---|---|---|---|---|---|
| 12 — `FRONT-008` | Usuario consulta `LOR-001`, semana ISO y calendario; Dirección versiona días | `FRONT-002/007`; BR-API01, BR-D05/M06 resueltas | UI-C01/C02/C03; consulta sucursal, rango calendario, edición motivada. Edición sucursal sólo si BR-API01 la autoriza | branch GET, calendar GET/PUT, weeks GET; permisos de F05; ETag/idempotencia | CA/CP-005/009/010; otro código/zona/fecha; otros roles no editan; 412/no-efecto; teclado fecha. API + Playwright | Habilita configuración/plan; traza HU-005/009/010 |
| 13 — `FRONT-009` | Dirección crea borrador y publica release versionado | `FRONT-008`; BR-D04/M13 resueltas | UI-C04; lista/historia/publicación; sin editor de TAR | configuration/releases; `PER-CONFIG-ADMIN`; idempotencia + If-Match | CA/CP-008; solapamiento/no Dirección/conflicto; historia intacta/auditoría; Playwright | Habilita `FRONT-010..012`; traza HU-008 |
| 14 — `FRONT-010` | Dirección consulta ocho TAR, crea/publica versión y desactiva nuevas generaciones | `FRONT-009`; BR-D06 y esquema de cada TAR aprobado | UI-C05; catálogo/detalle/historia/editor cerrado. Sin TAR novena ni JSON libre | task-definitions endpoints; `PER-DEFINICION-ADMIN`; ETag/idempotencia | CA/CP-011 y CAT schema; TAR no MVP/campo extraño; obligaciones previas inmutables; API + Playwright | Habilita políticas/generación; traza HU-011/CAT |
| 15 — `FRONT-011` | Dirección versiona activación y elegibilidad por TAR | `FRONT-010`; BR-API09 y editor schedule resueltos | UI-C06/C07; modo/manual/recurrencia y rol/disponibilidad/turno; sin evento/exterior | PUT activation/eligibility policies; permisos respectivos; If-Match/idempotencia | CA/CP-012/017; rol exacto, schedule cerrado, solapamiento y no-efecto; API + Playwright | Habilita alta/elegibilidad; traza HU-012/HU-017 |
| 16 — `FRONT-012` | Dirección versiona políticas de evidencia y validación para ocho TAR | `FRONT-010`; BR-D06/D09 resueltas | UI-C08/C09; requisitos ordenados y matriz ejecutor/validador/resultados; sin inventar requisito | PUT evidence/validation policies; permisos; If-Match/idempotencia | CA/CP-024/027; snapshot no cambia obligación; puesto/par no autoridad; API + Playwright | Habilita evidencia/validación; traza HU-024/HU-027 |

### 5.4 Generación, asignación y plan

| Orden / ID | Resultado visible demostrable | Dependencias | Alcance, pantallas y estados; exclusiones | Backend reutilizado y autorización | Criterios positivos/negativos/a11y/no-efecto y prueba | Habilita / trazabilidad |
|---|---|---|---|---|---|---|
| 17 — `FRONT-013` | Actor autorizado crea alta manual completa y observa resultado estable | `FRONT-011`; BR-API06 resuelta; formularios CAT aprobados | UI-G01; ocho formularios cerrados cuando aplique, replay/conflicto; sin recurrencia manual del Worker | generation-requests POST/GET; `PER-OBLIGACION-CREAR`; idempotencia | CA/CP-014/015 y CAT; origen incompleto/fuera de alcance; replay mismo ID; no parcial. API PostgreSQL + Playwright por formulario aprobado | Habilita obligación/plan; traza HU-014/HU-015/CAT |
| 18 — `FRONT-014` | Superior explica elegibilidad, ve carga y corrige asignación motivada | `FRONT-013/005/007`; BR-D04/D13 resueltas | UI-G02/G03/G04; candidatos/razones/carga/corrección; sin asignación automática cliente | eligibility, loads, assignment-corrections; permisos, jerarquía, ETag/idempotencia | CA/CP-004/016/019; par/inferior/inelegible/motivo vacío; 412/concurrencia/auditoría; API + Playwright | Habilita plan/supervisión; traza HU-004/016/019 |
| 19 — `FRONT-015` | Planificador crea/recupera plan y superior publica su alcance con historia visible | `FRONT-008/013/014`; BR-API02/03 resueltas | UI-G05/G06; vacío, borrador/publicado/versiones; sin segundo plan | plan GET/ensure/publications/versions según resolución; permisos, If-Match/idempotencia | CA/CP-020/021; Piso/superior indebido; obligación tardía V2; concurrencia/no-efecto. API + Playwright | Habilita recorrido operativo completo; traza HU-020/HU-021 |

### 5.5 Trabajo y evidencia

| Orden / ID | Resultado visible demostrable | Dependencias | Alcance, pantallas y estados; exclusiones | Backend reutilizado y autorización | Criterios positivos/negativos/a11y/no-efecto y prueba | Habilita / trazabilidad |
|---|---|---|---|---|---|---|
| 20 — `FRONT-016` | Usuario opera bandeja, avisos y consulta detalle/historia permitida | `FRONT-013/015`; BR-D13/M10/N04 resueltas | UI-E01/E02/E03; futura/disponible/vencida/concluida, vacío/filtros, aviso leído | inbox, notice read, obligations list/detail; permisos/links y anti-IDOR | CA/CP-023/030; tarea ajena ausente; marcar leído no cambia tarea; GET sin efecto; Playwright cuatro roles | Habilita evidencia/conclusión; traza HU-023/HU-030 |
| 21 — `FRONT-017` | Responsable carga archivo privado, espera escaneo y aporta evidencia limpia o estructurada válida | `FRONT-012/016`; BR-D07/D08 resueltas | UI-E04/E05; progreso/escaneo/errores; sólo tipos/payloads contratados; sin preview/descarga | upload-intents, PUT firmado, complete, status, evidence POST; permisos, CSRF/idempotencia | CA/CP-025 y CAT; 15 MiB, firma/tipo, infectado/error, no vínculo antes de limpio, URL no filtrada; API S3/ClamAV + Playwright móvil | Habilita revisión/conclusión; traza HU-025/TECH-EVID |
| 22 — `FRONT-018` | Actor consulta versiones/revisión, sustituye con autoridad y responsable concluye sólo completa | `FRONT-017`; BR-API04 y BR-D04/M08 resueltas | UI-E06/E07/E08; vigente/sustituida, faltantes, conflicto, confirmación; descarga sólo si existe contrato | evidence list/review/replacement, conclusion, descarga resuelta; permisos, If-Match/idempotencia | CA/CP-022/025/026 y CAT; falta evidencia/actor distinto/after-conclusion; decisión previa no cambia; concurrencia/no-efecto. API + Playwright | Habilita validación; traza HU-022/025/026 |

### 5.6 Validación, control y continuidad

| Orden / ID | Resultado visible demostrable | Dependencias | Alcance, pantallas y estados; exclusiones | Backend reutilizado y autorización | Criterios positivos/negativos/a11y/no-efecto y prueba | Habilita / trazabilidad |
|---|---|---|---|---|---|---|
| 23 — `FRONT-019` | Superior atiende pendientes, emite/sustituye validación y supervisa sólo inferiores | `FRONT-018/012/014`; BR-D09/M09 resueltas | UI-V01..V04; tres resultados, fundamento, escalamiento, historia, vacío | pending/supervision/validation endpoints; permisos, jerarquía, CSRF, ETag/idempotencia | CA/CP-028/031; autovalidación, par, segundo resultado, sustitución sin motivo; ejecución sigue concluida; API concurrencia + Playwright | Habilita indicadores/auditoría; traza HU-028/HU-031 |
| 24 — `FRONT-020` | Roles autorizados consultan cinco indicadores; Dirección ve panorama, auditoría y continuidad restringida | `FRONT-019`; BR-API05, BR-D10..D12, BR-M11/M12 resueltas; `TECH-E2E-CV-05` ya cerrado | UI-R01/R02/U01/U02/K01..K03; se implementa en incrementos internos: indicadores, auditoría, continuidad. Sin KPI, exportación, reparación o deploy | indicators/direction/audit/reconciliations; permisos/jerarquía/anti-IDOR; continuidad CSRF/idempotencia/If-Match | CA/CP-029/032/033/035; ceros/denominador, sin sexto indicador, cadena completa, no borrado, sólo Dirección, mismatch no se oculta. API PostgreSQL/continuidad sintética + Playwright | Habilita `TECH-FRONT-005`; traza HU-029/032/033/035 |

## 6. Condición para habilitar la siguiente fila

Cada fila habilita la siguiente sólo cuando:

1. no quedan brechas propias pendientes;
2. código y documentación existen;
3. pruebas locales enfocadas compatibles pasan;
4. fallos negativos demuestran no-efecto y auditoría correcta;
5. accesibilidad del flujo Playwright no tiene defecto crítico/bloqueante;
6. trazabilidad se actualiza como `Implementada localmente` sin afirmar publicación/integración;
7. no se amplió contrato, API, componente o mensaje sin aprobación.

La publicación se agrupa por hitos cuando el usuario la ordene. No se requiere PR por fila para continuar localmente, pero el cierre final exige integración real.

## 7. Hitos sugeridos de publicación, no autorizados por esta adenda

| Hito | Contenido |
|---|---|
| H1 | `TECH-FRONT-001..004`, `FRONT-001..002` |
| H2 | `FRONT-003..012` |
| H3 | `FRONT-013..018` |
| H4 | `FRONT-019..020`, `TECH-FRONT-005` |

La agrupación es una propuesta de gestión, no autorización de push, PR, merge o despliegue.

## 8. Eficacia y punto de parada

Esta Adenda 45 fue aprobada íntegramente y autorizada para incorporación el 2026-09-22. Su presencia en una rama local constituye incorporación documental local; sólo será `Integrada` cuando el commit que la contiene sea ancestro de `origin/master`.

La aprobación e incorporación no autorizan iniciar `TECH-FRONT-001` ni otra tarea, crear pantallas, instalar paquetes, ejecutar navegador, publicar o desplegar. El inicio de la primera tarea técnica requiere una orden futura explícita y una nueva verificación de base, trazabilidad y brechas propias.
