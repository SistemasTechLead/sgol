# FRONT-016 — Plan de implementación para aprobación

## 1. Control y resumen previo

Estado: **Aprobado íntegramente; implementación autorizada**. Fecha: 2026-09-29, America/Mexico_City. Base comprobada por preflight: `master`, `6036f4f7be7e15d8bc01a261a068ffb652842cf5`.

Aprobación y orden literal: «Bueno sigue con la tarea, apruebo la adenda integramente». Se refiere al plan presentado en este chat; cubre íntegramente §§2–8, incluidas las decisiones consumidoras §§4–6. Las expresiones de propuesta y espera conservadas debajo documentan el texto sometido a aprobación, no una decisión todavía pendiente. Incorporación operativa por Adenda 54 y docs/design. El SDK aislado 10.0.400 quedó comprobado antes de implementar, sin cambiar global.json. Publicación, merge y despliegue siguen pendientes de autorización.

1. Alcance exclusivo: fila 94 de Adenda 45; HU-023/HU-030, CA-023/030 y CP-023-P/N/030-P/N; UI-E01/E02/E03.
2. Dependencias: FRONT-013 integrada y FRONT-015 integrada por PR #82; instrucciones FRONT integradas por PR #83, según evidencia aceptada del responsable. No repetir sus gates.
3. Fuentes: AGENTS.md, instrucciones FRONT, índice de IDs, rangos F05 indicados, Adendas 15/22/45/46 y diseño operativo. Contratos productivos contrastados en los archivos directamente afectados.
4. Resultado: bandeja propia, avisos propios, listado autorizado y detalle con historia paginada; cuatro estados temporales, filtros, vacío, error y aviso leído.
5. Contratos: reutilizar cuatro endpoints existentes; no crear endpoint, permiso, estado persistido, migración o dependencia NuGet.
6. Carencias: composición UI-E01/E02/E03, mensajes BR-M10 y presentación temporal/avisos requieren aprobación de §§4–6; BR-D13 y BR-N04 ya tienen primitivas y patrón aprobados.
7. Archivos previstos: Razor MyWork, presentación consumidora, navegación, diseño operativo, pruebas afectadas y trazabilidad; detalle en §7.
8. Validación: build Release, pruebas enfocadas de presentación/contrato/arquitectura y diff-check; pruebas PostgreSQL y navegador enfocadas durante el hito de publicación. No hay autorización de implementación, commit, publicación, merge ni despliegue en este turno.

## 2. Hechos documentados, estado vigente y precedencia

Se leyó primero `IMPLEMENTATION_STATUS.md`, incluida la tabla «Tareas insertadas por adenda». No aparece una dependencia funcional o técnica de código pendiente que deba ejecutarse antes de FRONT-016. TECH-FRONT-005 corresponde después de FRONT-001..020. Los estados administrativos históricos no bloquean el avance local.

FRONT-013 consta integrada por PR #79; FRONT-015 consta integrada por PR #82. La declaración vigente del responsable acredita también PR #83 en esta base. Las frases de incorporación documental «aún no publicado», FRONT-015 «Publicada» premerge y FRONT-014..020 «no iniciadas» en resúmenes antiguos son registros históricos; no reemplazan la evidencia vigente. No se modifican esos documentos congelados ni se vuelven a verificar GitHub, pipelines o análisis de tareas anteriores.

Fuentes localizadas primero mediante `docs/INDICE_IDS.md`:

| Fuente | Rango aplicado |
|---|---|
| `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md` | Fila 94; reglas comunes que gobiernan esa fila |
| `F05_ESPECIFICACION_FUNCIONAL_MVP.md` | 138, HU-023; 145, HU-030; 103/105/107, RN-025/027/029 |
| `F05_CRITERIOS_DE_ACEPTACION.md` | 53, CA/CP-023; 60, CA/CP-030 |
| `F07_ADENDA_46_MAPA_DE_NAVEGACION_Y_SESION_FRONTEND.md` | 63–69, BR-N04 |
| `F06_REGISTRO_ADR.md` | ADR-004/005/006, 75–103; ADR-012/014/015, 153–160 y 171–187 |
| `F06_ARQUITECTURA.md` | §8, Razor/MVC, mismo origen y mejora progresiva |
| `F06_CONTRATO_DE_API.md` | §§2/3, filas obligations de §5.4 y bandeja/avisos de §5.8 |

Las Adendas 15 y 22 precisan DTO, alcance, filtros, historia y efectos. Sus exclusiones históricas de UI corresponden a las tareas backend; FRONT-016 autoriza preparar su consumidor, condicionado al diseño aprobado. La excepción específica de Adenda 22 para marcar leído prevalece sobre la regla genérica de cabeceras de mutación: CSRF obligatorio, **sin** Idempotency-Key ni If-Match.

Se leyeron `docs/design/tokens.md`, `componentes.md`, `estados-y-mensajes.md`, `estados-de-dominio.md` y `accesibilidad.md`; además, la navegación operativa y la resolución BR-D13 en TECH-FRONT-001. No se consultó `Fuentes/IdentidadMarca/` ni Excel ni ZIP.

Preflight: `treeClean=true`, `fuentesClean=true`, `fuentesRebaselinePending=false`, sesión gh autenticada. El SDK solicitado 10.0.400 no se resuelve por la configuración actual; la salida informa 10.0.401 instalado. No se hizo otro diagnóstico de entorno. Este plan no ejecuta build ni pruebas de producto y no acredita su éxito.

## 3. Contratos disponibles y permisos

| Contrato existente | Uso y límites |
|---|---|
| `GET /api/v1/me/inbox` | Tareas con asignación propia VIGENTE y avisos del destinatario. Los cuatro roles requieren PER-BANDEJA-PROPIA. Jerarquía nunca amplía `/me`. Dos colecciones con cursores independientes, count de página e isEmpty; private/no-store, sin ETag. |
| `POST /api/v1/me/notices/{id}/read` | Sólo destinatario vigente. Sin query ni cuerpo en la petición API, con CSRF. MARKED_READ o ALREADY_READ; repetición conserva readAt y no duplica auditoría. Ajeno/inexistente converge en 404. |
| `GET /api/v1/obligations` | PER-TAREA-VER y alcance vigente aplicado por el servidor antes de filas, conteos o vínculos. No requiere publicación. Cursor opaco, 25 por defecto, máximo 100. |
| `GET /api/v1/obligations/{id}` | Mismo alcance, UUID canónico, historia de la obligación con historyCursor/historyLimit. Inexistente/ajena/otra sucursal converge en 404 OBLIGACION_NO_ENCONTRADA. El código vigente entrega ETag; no se usa para mutaciones en FRONT-016. |

Los permisos de bandeja y consulta ya se proyectan en `src/Modules/Identity/Contracts/Roles.cs`; no se añade permiso ni se cambia la matriz. Cookie y snapshot de sesión request-scoped existentes; cada API reautoriza cuenta, MFA, empleo, rol, recurso y estado.

| Puesto | Bandeja y avisos | Listado/detalle HU-023 |
|---|---|---|
| Piso de ventas | Exclusivamente propios | Asignación vigente propia |
| Subcoordinación | Exclusivamente propios | Propias y responsables vigentes de Piso de ventas |
| Administración | Exclusivamente propios | Propias y responsables vigentes de Subcoordinación/Piso de ventas |
| Dirección | Exclusivamente propios | Todas las obligaciones de LOR-001, incluidas las no asignadas |

«Tarea ajena ausente» se exige en la bandeja de todos los puestos y en listados fuera del alcance correspondiente. No se usa ese criterio para ocultar a un superior la tarea inferior que CP-023-P permite consultar.

Los GET mantienen lectores existentes read-only/AsNoTracking y no crean auditoría, semana, aviso, snapshot, idempotencia ni outbox. La única escritura de esta historia es read_at y su auditoría atómica INTERNAL_NOTICE_READ. No modifica obligación, asignación, evidencia, conclusión ni publicación.

Historia: GENERACION_SOLICITADA, ASIGNACION_AUTOMATICA, ASIGNACION_CORREGIDA y PUBLICACION_INCLUIDA; sólo hechos tipados ya devueltos. Mostrar motivo autorizado de corrección como texto codificado. No consultar auditoría general ni agregar eventos de evidencia, ejecución o validación.

## 4. Carencias y propuestas concretas para decisión

**Hecho:** BR-D13 tiene tabla semántica, filtros GET, cursor y estados base aprobados; BR-N04 tiene listado y detalle GET. Los cinco documentos obligatorios no contienen la composición específica UI-E01/E02/E03 ni catálogo BR-M10, UNREAD/READ o Futura/Disponible. No se considera resuelta BR-M10 por disponer de un error genérico.

**Propuesta de incorporación tras aprobación:** añadir §§5/6 como extensión consumidora a `docs/design/componentes.md`, `estados-y-mensajes.md`, `estados-de-dominio.md` y `navegacion.md`; conservar tokens y accesibilidad sin alterar valores. Registrar BR-D13 consumida, BR-M10 resuelta para FRONT-016 y BR-N04 aplicada. Si se requiere adenda F07 para formalizar la extensión, crearla en la raíz con el siguiente número libre comprobado al implementar, sin cambiar Adendas 15/22/45/46 ni originales F00–F07. Este plan no asigna un identificador estable nuevo.

**Hecho de código:** `EfWeekPeriodService.GetAsync` puede materializar week_period o refrescar DerivedStatus y escribir auditoría. Por ello no se llamará GET /weeks desde consultas FRONT-016, ni ensure de plan, ni generación para resolver filtros.

**Propuesta de período sin contrato nuevo:** bandeja inicial usa la semana corriente que devuelve inbox. La consulta HU-023 inicial lista todos los períodos dentro de alcance, sin periodId. Cada fila permite «Consultar este período», llevando exclusivamente su periodId real a un filtro de consulta, o «Ver este período en mi bandeja». El período elegido se presenta por año/semana/rango recibido; «Semana actual» elimina periodId de inbox y «Todos los períodos» lo elimina del listado. No se presenta un catálogo completo a partir de una página, un UUID editable ni un selector de semanas inexistentes. Así se alcanza una tarea futura navegando por filas autorizadas y luego filtrando la bandeja propia. No se escribe para crear una semana ausente.

**Inferencia técnica:** los contratos actuales y cliente compartido bastan para el consumidor. Inbox requiere DTO de envoltura consumidora con count/isEmpty por sección, en lugar de deserializar como colección plana; detalle ya dispone de HistoryNextCursor en ApiResponse. Validar esa envoltura y sus acciones cerradas en presentación; respuesta inconsistente falla con error seguro, sin completar campos inventados. No se reconstruyen estados temporales con el reloj del navegador.

**Decisión pendiente:** aprobar la composición, rutas web, mensajes y variantes de badges de §§5/6 y el mecanismo de período anterior. Si no se aprueba, queda detenida esa UI; no se improvisan componentes ni se agregan endpoints para eludir la carencia.

## 5. Composición propuesta de UI-E01/E02/E03

`/mi-trabajo` conserva el shell y el logout existentes y habilita «Mi trabajo» en navegación sólo con PER-BANDEJA-PROPIA. Un enlace interno «Consultar tareas» exige PER-TAREA-VER. Propuesta de detalle navegable: GET `/mi-trabajo/tareas/{obligationId:guid}`. La aprobación de este plan debe incluir esta ruta web; no es un endpoint REST adicional.

### UI-E01 — Mi bandeja y consulta autorizada

Primer bloque «Mis tareas»: período confirmado, select Estado (Todos/Futura/Disponible/Vencida/Concluida), Aplicar filtros y Limpiar filtros. Tabla: código/nombre, período, procedencia Manual/Programada, ejecución, situación temporal, vencimiento, evidencia Completa/Incompleta y códigos de requisitos faltantes con motivo legible. No traducir códigos funcionales a nombres inventados. VIEW_TASK habilita «Ver tarea»; no renderizar acciones CONTRIBUTE_EVIDENCE o CONCLUDE_TASK aún pendientes de FRONT-017/018.

Bloque separado «Tareas que puedo consultar»: API obligations, tabla con tarea, período, responsable actual o «Sin asignación vigente», origen, ejecución, bandera vencida y «Ver tarea». Filtros TAR del catálogo de ocho, ejecución y condición; período únicamente desde fila autorizada como §4. Se pueden admitir filtros contractuales adicionales en deep links validados, sin convertir IDs en controles para el usuario. No crear un selector de personas fuera de alcance ni consumir `/supervision`.

Cada listado conserva su propio cursor y filtros; cambiar un filtro reinicia su continuidad. Anterior se basa en cursores previamente recibidos dentro de navegación validada, Siguiente sólo en nextCursor; nunca números de página ni totales globales. Una nueva página reautoriza. No filtrar en memoria las filas de un listado amplio para fabricar la bandeja propia.

### UI-E02 — Avisos internos

Segundo bloque personal «Mis avisos», con select Todos/Sin leer/Leídos, tabla de aviso, fecha, lectura, tarea relacionada disponible y acción. Sólo OBLIGATION_ASSIGNED se presenta como «Tarea asignada». Sólo MARK_NOTICE_READ y UNREAD muestran «Marcar como leído».

Si resource.available=false, mostrar «La tarea relacionada ya no está disponible en tu bandeja», sin nombre/ID/vínculo funcional inferido. El aviso propio puede marcarse leído aunque la tarea ya no esté disponible. Un enlace a detalle requiere available, obligationId real y PER-TAREA-VER; el detalle vuelve a autorizar.

POST Razor usa handler específico para marcar, independiente del OnPost de logout, valida antiforgery y envía al API cuerpo nulo, Intent nulo e IfMatch nulo. No usa confirmación motivada, porque no es una acción crítica de tarea y el contrato no admite motivo. GET, abrir detalle o navegar nunca marca leído. No actualizar optimistamente: mostrar Leído sólo tras respuesta confirmada; después recargar el bloque y reiniciar cursor de avisos porque el orden cambia. Resultado incierto ofrece consultar avisos y, si sigue sin leer, repetir explícitamente la misma marcación naturalmente idempotente. Sin reenvío automático.

### UI-E03 — Detalle e historia permitida

Encabezado con código/nombre; secciones de procedencia y referencia sanitizada, versión TAR, período y fechas, ejecución/bandera y asignación vigente. Fecha ausente: «Sin fecha registrada»; no calcular dueAt. Hora de Ciudad de México con datetime UTC en elementos time.

Tabla «Historia de esta tarea»: fecha, hecho, responsable/publicación cuando aplique y motivo de corrección autorizado. Tipos y catálogos desconocidos fallan cerrado; no mostrar JSON ni explicaciones internas. Cursores independientes de la lista y ligados al mismo obligationId. Volver a Mi trabajo conserva sólo contexto local allowlisted; no redirección arbitraria.

links.self sirve para enlazar la página web tipada; nunca ejecutar un destino arbitrario devuelto por la API. Si existe links.eligibility autorizado, puede enlazar a la sección ya implementada de FRONT-014 en planificación con ID validado; no se abre una pantalla futura ni se ejecuta corrección desde aquí. No ofrecer aportación, descarga, revisión, conclusión, validación o reapertura.

### Estados y accesibilidad de las tres unidades

Normal: sólo datos confirmados. Foco: orden DOM, foco visible, enlaces y botones con área mínima definida; error enfoca resumen y regreso mantiene contexto. Deshabilitado: extremos de cursor y botón de marcación durante envío. Cargando: región aria-busy, esqueleto de tabla y «Marcando aviso…» en botón inerte. Error: mensaje cerrado, correlationId seguro, sin datos previos de recurso denegado. Vacío: texto específico por bloque y acción de consulta/limpieza autorizada.

Reutilizar parciales de tabla, badge, alerta, vacío y resumen; ningún color, radio, fuente, sombra o espaciado literal consumidor. Móvil: una columna y desplazamiento sólo dentro de tabla semántica; etiquetas asociadas, landmarks, teclado, reflow, texto e icono para estados. Éxito role=status/aria-live=polite. Tras marcar, si desaparece la fila por filtro Sin leer, foco al encabezado de avisos; de lo contrario, a su estado Leído. No abrir diálogo ni cambiar tarea.

## 6. Presentación y catálogo de mensajes propuestos

No se crean estados de dominio: executionStatus sigue PENDIENTE/CONCLUIDA; taskState es clasificación informativa recibida. Vencida conserva badge separado junto a Pendiente. Programada describe procedencia recurrente. Concluida no implica validada.

| Valor recibido | Texto/icono | Tokens existentes |
|---|---|---|
| FUTURA | Futura / calendario | --color-info y --color-info-fondo |
| DISPONIBLE | Disponible / círculo informativo | --color-info y --color-info-fondo |
| VENCIDA | Pendiente + Vencida / reloj y reloj-exclamación | Pares advertencia y peligro ya aprobados |
| CONCLUIDA | Concluida / check | Par exito ya aprobado |
| UNREAD | Sin leer / sobre cerrado | Par info |
| READ | Leído / sobre abierto | --color-texto-secundario sobre --color-superficie-elevada |

Futura y Disponible aparecen junto a Pendiente, no lo reemplazan. Completa/Incompleta reutilizan los badges existentes de evidencia. Texto y SVG decorativo aria-hidden, sin depender sólo de color.

| Situación / respuesta | Mensaje y acción |
|---|---|
| Bandeja corriente sin filas | «No hay tareas propias en este período». Consultar tareas o Semana actual según contexto; no prometer creación/asignación. |
| Filtros sin filas | «No hay tareas que coincidan con estos filtros». Limpiar filtros. |
| Avisos sin filas | «No hay avisos en esta consulta». Quitar filtro de lectura si lo hay. |
| Listado autorizado vacío | «No hay tareas disponibles en esta consulta». Limpiar filtros. No afirmar inexistencia global. |
| Historia vacía | «No hay eventos disponibles en esta consulta». Volver al detalle sin cursor. |
| MARKED_READ | «Aviso marcado como leído. La tarea no cambió». |
| ALREADY_READ | «Este aviso ya estaba leído. La tarea no cambió». Mostrar readAt original. |
| 400 FILTRO_BANDEJA_INVALIDO / FILTRO_OBLIGACIONES_INVALIDO / FILTRO_HISTORIA_INVALIDO | «Revisa los filtros de esta consulta». Asociar campo identificable y ofrecer Limpiar filtros; no reflejar cursor. |
| 400 AVISO_ID_INVALIDO / SOLICITUD_LECTURA_AVISO_INVALIDA / OBLIGACION_ID_INVALIDO | «La solicitud no es válida. Vuelve a la consulta e inténtalo de nuevo». Sin anunciar lectura confirmada. |
| 400 CSRF_INVALIDO | Mensaje común aprobado: recargar antes de volver a enviar. |
| 401 | Flujo existente de sesión terminada y acceso, sin datos funcionales. |
| 403 ACCESO_DENEGADO | «No tienes permiso para consultar tu bandeja» o «No tienes permiso para consultar tareas», según operación; retirar datos/acciones de esa sección. |
| 404 AVISO_NO_ENCONTRADO / OBLIGACION_NO_ENCONTRADA | «No existe o no está disponible en tu alcance». Mismo texto para inexistente/ajeno; volver a consulta. |
| 409 BANDEJA_INCONSISTENTE | «No se pudo consultar tu bandeja. Recárgala antes de continuar». Sin respuesta parcial. |
| 409 LECTURA_AVISO_CONCURRENCIA_CONFLICTO | «No se pudo confirmar la lectura del aviso. Consulta tus avisos antes de volver a marcarlo». |
| 500 CONSULTA_OBLIGACION_INCONSISTENTE o error desconocido/red | Mensaje seguro común y correlationId si existe; Recargar consulta. No mostrar title/detail/SQL/payload. |

## 7. Archivos previstos y trazabilidad posterior

Rutas previstas; los archivos nuevos son propuesta y aún no existen:

| Archivo o conjunto | Cambio mínimo previsto |
|---|---|
| `src/Sgol.Web/Pages/MyWork/Index.cshtml(.cs)` | Conservar sesión/logout, añadir consultas y handler específico de lectura |
| `src/Sgol.Web/Pages/MyWork/Index.Inbox.cs`, `_Inbox.cshtml`, `_Notices.cshtml`, `_Obligations.cshtml` | Separación de composición, filtros, DTO/estado consumidor y dos paginaciones personales |
| `src/Sgol.Web/Pages/MyWork/Details.cshtml(.cs)` | Detalle e historia GET en ruta propuesta |
| `src/Sgol.Web/Interface/MyWork/` | Modelos y presentación cerrada, normalización allowlisted y enlaces locales |
| `src/Sgol.Web/Interface/Navigation/NavigationItem.cs`, `Pages/Shared/_Layout.cshtml` | Habilitar Mi trabajo y preservar controles por permiso y logout |
| `src/Sgol.Web/wwwroot/css/components.css`, `wwwroot/js/components.js` | Sólo si las primitivas requieren composición/progreso/foco; únicamente tokens existentes |
| `docs/design/componentes.md`, `estados-y-mensajes.md`, `estados-de-dominio.md`, `navegacion.md` | Incorporar únicamente extensión aprobada §§4–6 |
| `tests/Sgol.UnitTests/Front016PresentationTests.cs` | Contrato consumidor, filtros, permisos, estados y marcación |
| `tests/Sgol.UnitTests/Front002Tests.cs`, pruebas comunes cliente/navegación afectadas | Actualizar expectativas del anfitrión vacío sin rebajar seguridad/logout |
| `tests/Sgol.ArchitectureTests/` | Cobertura de nuevos PageModels/rutas en inventarios y reglas afectadas |
| `tests/Sgol.IntegrationTests/ObligationQueryPersistenceTests.cs`, `AssignmentCorrectionPersistenceTests.cs` o archivo Front016 específico | Regresión enfocada de alcance, lectura e invariancia persistida |
| `tests/Sgol.FrontendBrowserTests/Front016BrowserTests.cs`, extensión acotada de BrowserFixture | Semilla sintética y recorridos de cuatro roles |
| `tests/Sgol.FrontendBrowserTests/Front002BrowserTests.cs`, smoke y consumidores del menú afectados | Actualizar conteos/enlaces y vacío histórico que ya cambian con esta historia |
| `docs/traceability/FRONT_016_BANDEJA_Y_CONSULTA.md`, `IMPLEMENTATION_STATUS.md`, `docs/INDICE_IDS.md` | Aprobación literal, mapa HU/CA/CP, brechas, comandos, resultados y límites; FRONT-017..020 siguen no iniciadas |

No se prevé modificar endpoints ni lectores productivos, contratos de módulo, Roles.cs, migraciones, CI, Worker o NuGet. Si se detecta un defecto del contrato necesario para esta historia, documentar la diferencia y resolver dentro del plan aprobado sólo cuando no exija una nueva decisión; una contradicción funcional detiene esa parte.

## 8. Pruebas, validación proporcional y flujo autorizado

| Criterio | Evidencia prevista |
|---|---|
| CA-030 / CP-030-P | Semilla propia futura/disponible/vencida/concluida y evidencia faltante; fecha nula no inferida, igualdad de vencimiento y Concluida sin Vencida; períodos seleccionados sin materialización |
| CP-030-N | Tarea ajena ausente de bandeja para cuatro roles, avisos ajenos ocultos/404, aviso histórico unavailable sin vínculos; sin correo/SMS |
| CA-023 / CP-023-P | Superior consulta inferior actual, procedencia, fechas, asignación y cuatro tipos de historia; filtros/cursor/detalle conservan identidad y alcance |
| CP-023-N | Par/superior fuera de alcance y antiguo responsable ocultos según matriz; detalle ajeno e inexistente convergen; no filtración en filas/count/links/cursor |
| GET sin efecto | Comparar huellas y conteos persistidos antes/después de bandeja, lista, detalle, historia y filtros repetidos, incluidos avisos/readAt, obligaciones, asignaciones, evidencia, auditoría, idempotencia, outbox, jobs y week_period; interceptor rechaza SaveChanges |
| Marcar leído no cambia tarea | Primera marcación sólo readAt + una auditoría; repetición mismo readAt sin auditoría adicional; obligación/asignación/evidencia/plan idénticos. CSRF inválido/ajeno/fallo no cambia aviso ni tarea; rollback de auditoría y concurrencia reutilizan pruebas existentes enfocadas |
| Contrato consumidor | Inbox como Item con dos colecciones, acciones allowlisted, cursores de cada sección; POST API sin cuerpo, If-Match ni Idempotency-Key; no marcado por GET, sin doble envío automático |
| Navegador/a11y | Cuatro roles; escritorio Chromium y móvil WebKit según harness existente; teclado/foco, etiquetas/errores, loading/disabled/empty, retorno de foco al marcar, reflow y contraste. No efectos mediante navegación; GET /weeks no invocado |

Después de aprobar el plan **y ordenar su implementación**: incorporar diseño aprobado primero, implementar, actualizar pruebas anteriores directamente afectadas, registrar trazabilidad y crear commit local coherente. Compilar con `dotnet build --no-restore --configuration Release`; ejecutar `dotnet test --no-build --configuration Release --filter <clases o casos directamente afectados>` por proyecto y `git diff --check`. Restore locked sólo si faltan artefactos o cambian lock/dependencias. Resolver el SDK exacto usando la instalación aislada ya documentada si está disponible durante implementación; no alterar global.json ni diagnosticar más el entorno en este turno.

Las pruebas PostgreSQL y navegador anteriores se preparan en el cambio y se ejecutan de forma enfocada al publicar el hito, con backend real y datos sintéticos, conservando los gates remotos. No correr suites integrales por endpoint. Si una comprobación compatible enfocada falla, no marcar Implementada localmente. Registrar cualquier omisión con causa exacta; no asumir que Docker o AMD64 son incompatibles. Navegador y transacciones no se acreditan mediante previsualizaciones.

Este turno sólo ejecutó preflight y lecturas de planificación. Se verificará diff-check del documento. **Validación diferida de producto:** build/pruebas no ejecutados porque aún no existe orden de implementación; además, preflight no resuelve SDK 10.0.400. No se presenta ninguna CA/CP como satisfecha por la redacción del plan.

Tras implementación validada: estado Implementada localmente, archivos/resultados/commit/límites concretos; esperar autorización de publicación. Sin push ni PR por aprobación del plan. Al publicar, un único PR adjunto al chat y pipeline asociado a cada cabeza actual. La autorización incluye corregir defectos del mismo hito, validar y subir correcciones sin repetir confirmación de publicación. Mostrar capturas sintéticas y explicación cotidiana por puesto mientras corre el pipeline, identificando cualquier previsualización. Antes de terminar un turno pendiente, configurar y verificar heartbeat en este chat; silencio sin novedades accionables. No activar seguimiento en esta fase de plan. Con checks requeridos verdes de la cabeza actual y requisitos comprobados, solicitar aprobación expresa de merge; no hacer merge ni despliegue por publicación.

## 9. Aprobación solicitada

Se solicita aprobar íntegramente el plan, especialmente §§4–6 (composición, ruta de detalle, filtros de período sin escrituras, badges y BR-M10) y §§7/8 (archivos, pruebas y flujo). La aprobación se conservará literalmente en trazabilidad. La implementación requiere además la orden expresa de ejecutarlo; hasta entonces sólo existe esta propuesta fuera de Fuentes.
