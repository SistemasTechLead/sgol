# FRONT-016 — Bandeja, avisos y consulta de tareas

## Estado y autorización

**Implementada localmente** en `codex/front-016`, desde `master` `6036f4f7be7e15d8bc01a261a068ffb652842cf5`. Commit implementado: el que contiene este informe. Fecha de trabajo: 2026-09-29, America/Mexico_City.

El responsable ordenó «Bueno sigue con la tarea, apruebo la adenda integramente». Se aprobó íntegramente `FRONT_016_PLAN_DE_IMPLEMENTACION.md` §§2–8, incorporado por `F07_ADENDA_54_CONTRATO_CONSUMIDOR_FRONT_016.md` y las extensiones operativas de docs/design. Publicación, merge y despliegue no están autorizados.

Se leyó primero IMPLEMENTATION_STATUS y su tabla de tareas insertadas; preflight fue la única comprobación inicial del entorno. Se acepta FRONT-013 integrada, FRONT-015 integrada por PR #82 y documento FRONT integrado por PR #83 conforme a la evidencia previa del responsable. Las menciones premerge o de FRONT-016 no iniciada en resúmenes antiguos son históricas. No se reconstruyeron análisis ni gates de esas tareas.

## Alcance y fuentes

FRONT-016, fila 94 de `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md`; HU-023/HU-030, CA-023/030 y CP-023-P/N/030-P/N; UI-E01/E02/E03. Adendas 15/22 conservan sus contratos backend. BR-D13 se consume, BR-N04 se aplica y BR-M10 se incorpora exclusivamente para esta historia.

Las lecturas se localizaron mediante docs/INDICE_IDS. Se leyeron los cinco documentos obligatorios: tokens, componentes, estados-y-mensajes, estados-de-dominio y accesibilidad. No se modifican Fuentes, documentos congelados, backend, permisos, migraciones, NuGet, global.json ni CI. FRONT-017..020 siguen no iniciadas.

## Resultado funcional y permisos

- `/mi-trabajo` compone Mis tareas, Mis avisos y Tareas que puedo consultar, conserva sesión y logout. Los cuatro puestos reciben sólo bandeja y avisos propios con PER-BANDEJA-PROPIA.
- Con PER-TAREA-VER, la consulta y `/mi-trabajo/tareas/{obligationId:guid}` usan el alcance vigente del servidor: Piso propio; Subcoordinación propio/Piso; Administración propio/Subcoordinación/Piso; Dirección todo LOR-001, incluidas tareas sin asignación. No se calcula jerarquía en navegador ni se ocultan filas después de consultar una colección amplia.
- Futura, Disponible, Vencida y Concluida son clasificaciones recibidas, sin estados persistidos nuevos. Pendiente conserva su badge junto al informativo. No se infieren vencimientos ausentes; Concluida no significa validada. Origen recurrente se muestra como Programada.
- Filtros cerrados de estado personal, lectura, TAR de las ocho permitidas, ejecución y condición. Período se selecciona desde una fila autorizada; los filtros adicionales contractuales sólo se admiten en enlaces validados. Semana actual retira el período explícito; ningún GET /weeks materializa períodos.
- Cursores personales de tareas/avisos, consulta e historia son independientes y protegidos por actor, sección y filtros. Anterior conserva el recorrido sin números de página inventados. Cambiar un filtro reinicia su cursor; marcar leído reinicia el de avisos porque cambia su orden.
- Aviso leído requiere CSRF validado por Razor y API. POST API sin cuerpo, If-Match ni Idempotency-Key. MARKED_READ/ALREADY_READ confirman Leído y el readAt recibido; no hay actualización optimista ni reintento automático. Aviso unavailable omite datos/vínculos de tarea.
- Detalle usa GET y sólo los cuatro hechos de historia permitidos; motivo sólo para ASIGNACION_CORREGIDA. Ajena e inexistente comparten el mismo mensaje 404 sin filas ni historia. Enlaces de retorno sólo locales con filtros permitidos y contexto protegido por actor.
- Normal, foco, deshabilitado, cargando, error y vacío están compuestos con primitivas aprobadas. Tablas semánticas, controles etiquetados, badges de texto e icono decorativo del parcial existente, alertas seguras y mensajes específicos; carga con esqueleto/aria-busy. Tras marcar, foco al estado Leído o al encabezado de avisos si desaparece la fila. No se imprimen mensajes internos, JSON ni cursores en alertas.

GET no marca avisos ni ejecuta generación, ensure, publicación o comandos de tarea. No se ofrecen aportación, descarga, revisión, conclusión, validación ni reapertura.

## Archivos cambiados

| Conjunto | Archivos |
|---|---|
| Contrato y diseño aprobado | Adenda 54; `docs/design/componentes.md`, `estados-de-dominio.md`, `estados-y-mensajes.md`, `navegacion.md` |
| Composición y detalle | `src/Sgol.Web/Pages/MyWork/Index.cshtml`, `Index.cshtml.cs`, `Index.Inbox.cs`, `_Inbox.cshtml`, `_Notices.cshtml`, `_Obligations.cshtml`, `_WorkPagination.cshtml`, `Details.cshtml`, `Details.cshtml.cs` |
| Presentación cerrada | `src/Sgol.Web/Interface/MyWork/MyWorkPresentation.cs`, `MyWorkQuery.cs`, `MyWorkCursor.cs`, `MyWorkReturnContext.cs` |
| Navegación y estados | `Interface/Navigation/NavigationItem.cs`, `Pages/Shared/_Layout.cshtml`, `wwwroot/css/components.css`, `wwwroot/js/my-work.js`; sólo espaciado mediante token existente |
| Unitarias y arquitectura | `Front016PresentationTests.cs`, `Front002Tests.cs`; `Front016ArchitectureTests.cs`, `InboxArchitectureTests.cs`, `ObligationQueryArchitectureTests.cs`, `InterfaceDesignRulesTests.cs` |
| Persistencia y navegador | `Front016NoEffectSnapshot.cs`, `AssignmentCorrectionPersistenceTests.cs`, `ObligationQueryPersistenceTests.cs`; `BrowserFixture.MyWork.cs`, `Front016BrowserTests.cs`, `Front002BrowserTests.cs`, `ShellBrowserSmokeTests.cs` |
| Trazabilidad | Plan aprobado, este informe, IMPLEMENTATION_STATUS y complemento de INDICE_IDS |

Los inventarios antiguos que prohibían estas vistas se acotan a las dos rutas consumidoras ahora aprobadas; conservan prohibiciones en otras superficies y comandos. No se elimina ni deshabilita una prueba.

## Criterios y evidencia

| Criterio | Evidencia local ejecutada | Evidencia preparada, ejecución diferida |
|---|---|---|
| CA-030 / CP-030-P | Consumo Item con dos colecciones; cuatro estados y badges; fecha nula sin inferencia; filtros cerrados; GET permitido para cuatro roles; vacío y respuesta inconsistente sin datos parciales | Recorrido real de cuatro roles con semilla propia Disponible/Vencida/Concluida/Futura en Chromium escritorio y WebKit móvil |
| CP-030-N | Permiso ausente no llama API; aviso unavailable con datos filtrados falla cerrado; CSRF inválido no envía POST; error no confirma lectura ni reintenta | Filas/avisos ajenos ausentes para cuatro roles; invariancia persistida de tarea/asignación/evidencia/plan y readAt original de la repetición |
| CA-023 / CP-023-P | Consulta GET con filtros canónicos, detalle tipado y nombres de historia cerrados; cursores ligados a actor/sección/filtros; contexto de retorno local | Backend real con jerarquía vigente, historia permitida y paginación; huellas completas antes/después de detalle/historia |
| CP-023-N | Respuesta 404 de detalle no produce recurso/historia; mensaje seguro ajena/inexistente; 401 retira datos y termina sesión | Matriz de superiores/pares/antiguos responsables ya cubierta por tests backend afectados, más rechazo de tarea de Dirección desde los otros tres puestos en navegador |
| GET sin escrituras | PageModels sin acceso a DB y solicitudes funcionales exclusivamente GET de inbox/obligations; arquitectura prohíbe /weeks, evidencia y conclusión | Comparación de filas persistidas de obligaciones, asignaciones, períodos, evidencia/versiones/snapshots, resultados, planes/versiones/vínculos, idempotencia, outbox, jobs, avisos y auditoría |
| Marcar leído no cambia tarea | Único POST aprobado, cuerpo/Intent/IfMatch nulos y CSRF presente; confirmación cerrada y hora original; error/CSRF no anuncia lectura | Prueba existente reforzada: primera/repetida/ajena, una auditoría y taskRows idénticas; navegador compara invariancia y foco tras filtrar Sin leer |

La cobertura local acredita el consumidor, no sustituye la ejecución transaccional o de navegador. Las pruebas PostgreSQL y navegador se compilaron; sus resultados no se presentan como aprobados.

## Validación local

SDK exigido **10.0.400**, `rollForward=disable`, mediante instalación aislada `C:\Users\siste\.codex\tmp\sgol-sdk-10.0.400\dotnet.exe`. Se antepuso su directorio al PATH de cada proceso de validación; no se cambió global.json ni se usó 10.0.401.

| Comando | Resultado |
|---|---|
| `dotnet build --no-restore --configuration Release` | PASS, 0 errores/advertencias; incluye compilación de pruebas PostgreSQL/navegador |
| `dotnet test tests/Sgol.UnitTests --no-build --configuration Release --filter 'FullyQualifiedName~Front016PresentationTests\|FullyQualifiedName~Front002Tests\|FullyQualifiedName~InboxApiEndpointTests\|FullyQualifiedName~ObligationQueryTests\|FullyQualifiedName~TechFront003Tests'` | PASS 118/118, 0 omitidas |
| `dotnet test tests/Sgol.ArchitectureTests --no-build --configuration Release --filter 'FullyQualifiedName~Front016ArchitectureTests\|FullyQualifiedName~InterfaceDesignRulesTests\|FullyQualifiedName~ObligationQueryArchitectureTests\|FullyQualifiedName~InboxArchitectureTests'` | PASS 14/14, 0 omitidas |
| `dotnet format whitespace SGOL.slnx --no-restore --include <archivos C# afectados>` | Aplicado y comprobado sobre archivos afectados; sin ejecutar formato integral |
| `git diff --check` y comprobación de paths afectados | PASS; sin cambios de Fuentes ni documentos congelados |

No se necesitó restore ni cambiaron locks/dependencias. Durante el desarrollo se corrigieron advertencias de analizadores en código/pruebas nuevas antes de la compilación verde; no se suprimieron analizadores. EF1003 del snapshot de navegador se resolvió construyendo SQL desde la lista fija de tablas del modelo en variable local; ninguna entrada de usuario participa.

## Validación diferida y siguiente punto de espera

PostgreSQL/Testcontainers y navegador hospedado/TLS se reservan para el hito de publicación conforme al §8 aprobado. No existe incompatibilidad AMD64 documentada ni se alega ausencia de Docker. Causa: agrupación de validaciones costosas autorizada, no bloqueo del entorno. Foco, reflow, contraste medido y transacciones reales quedan pendientes de ejecución.

Filtros enfocados previstos al publicar: `AssignmentCorrectionPersistenceTests|ObligationQueryPersistenceTests` en integración; `Front016BrowserTests|Front002BrowserTests|ShellBrowserSmokeTests` en navegador. Suites integrales, formato completo, escáneres y controles remotos permanecen en los gates del hito.

Se espera autorización de publicación antes de push/PR. No hay pipeline pendiente ni automatización remota por activar ahora. Esa autorización futura incluye corregir defectos de este hito y subirlos al mismo PR; cada pipeline debe corresponder a la cabeza actual. Las capturas sintéticas y explicación por puesto se entregarán mientras corra el pipeline. El seguimiento en este chat se configurará y verificará antes de terminar un turno con pipeline pendiente. Merge y despliegue requieren autorización expresa separada.
