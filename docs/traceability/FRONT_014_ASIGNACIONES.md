# FRONT-014 — elegibilidad, carga activa y corrección motivada

Fecha: 2026-09-29, America/Mexico_City. Estado: **Implementada localmente**, commit local autorizado mediante «Autorizo el commit local» después de mostrar las pantallas. Commit implementado: el que contiene esta actualización. No equivale a Publicada, Integrada ni Terminada formalmente.

## Base y alcance

Proyecto Codex SGOL y directorio efectivo: `C:\Users\siste\Dev\SGOL-Migracion-20260929\extraido\SGOL-git-portable-v3\SGOL`.

La apertura encontró master en `0ab7dd3e977a3ec061476f5075bde8a5897d6bbe`, limpio. Se revisó exclusivamente la diferencia; la rama de continuidad y sus dos commits estaban conservados. Tras la instrucción del responsable de crear `codex/front-014`, se creó esa rama desde `codex/migration-continuity`, HEAD `4a9626b7a85e93ba0a0532c13a9883b1fea19f61`, para conservar AGENTS vigente y la reconciliación documental. Sin reset, limpieza, fetch, nuevo repositorio ni worktree. La referencia de migración se conserva.

Sólo fila 87 de Adenda 45, UI-G02/G03/G04, HU-004/HU-016/HU-019 y CA/CP correspondientes. Dependencias FRONT-013/005/007 y backend aceptadas como evidencia previa; no se repiten sus gates de apertura. No hay dependencia de código anterior pendiente. TECH-FRONT-005 corresponde al hito posterior.

## Lectura y punto de parada

Se leyeron AGENTS, estado, documentos de continuidad, informes históricos locales, rangos selectivos del índice, F05, F06 API, Adendas 06/09/46, contratos actuales y los cinco documentos de diseño obligatorios. BR-D04/D13 contienen primitivas aprobadas. Faltan composición y mensajes específicos para estas tres unidades.

En el primer corte, la propuesta mínima `FRONT_014_PROPUESTA_CONSUMIDOR.md`, §§2–7, esperaba aprobación y no se había escrito UI. El responsable aprobó expresamente mediante «La apruebo». Antes de editar vistas, las decisiones se incorporaron por referencia como fuente operativa en docs/design/componentes.md, estados-y-mensajes.md, estados-de-dominio.md y navegacion.md. La propuesta no modifica contratos funcionales ni crea endpoints.

## Cambio independiente implementado

GET /api/v1/obligations/{id} omitía el ETag exigido por F06_CONTRATO_DE_API.md §7.2 y Adenda 09 §4. Se transporta RowVersion en ObligationDetailPage (contrato interno) desde la fila de la misma lectura autorizada y transacción de consulta. El endpoint exitoso emite VersionEtag.Format(page.RowVersion). No agrega propiedad JSON pública, endpoint, consulta organizacional separada, migración ni escritura funcional.

Archivos de código y pruebas afectados:

- src/Modules/Execution/Contracts/ObligationQueries.cs.
- src/Sgol.Web/Infrastructure/Persistence/Execution/EfObligationQueryReader.cs.
- src/Sgol.Web/Interface/Endpoints/ObligationQueryApiEndpoints.cs.
- tests/Sgol.UnitTests/ObligationQueryTests.cs.
- tests/Sgol.IntegrationTests/ObligationQueryPersistenceTests.cs.

La prueba de detalle exige el ETag exacto recibido del contrato interno y ausencia de RowVersion en JSON. Tres casos nuevos verifican ausencia de ETag en 401/403/404; el 401 no llama al lector. La prueba PostgreSQL existente se amplió para contrastar RowVersion del detalle con la fila persistida y el mismo valor para dos actores autorizados; compilada, no ejecutada en este corte.

## Comprobaciones nuevas

Preflight inicial ejecutado una sola vez después de cargar Activar-SGOL.ps1: SDK 10.0.400, Git limpio, Fuentes limpio, sin rebaseline pendiente. Su rama/head master describían la apertura, no la rama creada posteriormente. Proyecto SGOL asociado al directorio exacto confirmado por list_projects.

Cada proceso .NET cargó el activador y comprobó exactamente 10.0.400 antes de continuar. No se cambió global.json ni configuración global.

```powershell
. 'C:\Users\siste\Dev\SGOL-Migracion-20260929\Activar-SGOL.ps1'
$taskSdkVersion = dotnet --version
if ($taskSdkVersion -ne '10.0.400') { throw "SDK distinto: $taskSdkVersion" }
dotnet build --no-restore --configuration Release
```

Resultado: salida 0, 0 errores, 0 advertencias. No fue necesario restore; artefactos disponibles. El proyecto de integración ampliado compila.

```powershell
dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --no-restore --configuration Release --filter 'FullyQualifiedName~ObligationQueryTests|FullyQualifiedName~AssignmentCorrectionTests|FullyQualifiedName~EligibilityEvaluationTests|FullyQualifiedName~ActiveLoadTests' --logger 'console;verbosity=normal'
git diff --check
```

Pruebas: **63 encontradas, 63 aprobadas, 0 fallidas, 0 omitidas**, salida 0. Incluyen tres casos nuevos negativos de ETag, el caso existente ampliado positivo y regresiones directamente relacionadas de contratos, jerarquía, motivos, idempotencia y conflicto. No acreditan rollback o concurrencia PostgreSQL ni UI todavía ausente. Diff-check: salida 0, sin errores.

## Evidencia histórica y límites

Build de migración, 147 pruebas enfocadas y navegador FRONT-013 4/4 son evidencia histórica del equipo nuevo, no pruebas de FRONT-014 ni reejecuciones en esta tarea. La excepción TLS de aquel fixture no se hereda.

Validación diferida: PostgreSQL/Testcontainers, API hospedada, concurrencia y auditoría/rollback reales, navegador/accesibilidad renderizada, suite completa, formato global y gates remotos. Causa: validación proporcional por tarea según AGENTS vigente; se agrupan en hito solicitado. El navegador además requiere definir su validación TLS específica antes de cualquier excepción. No se presentan como aprobados ni como incompatibilidad de arquitectura.

El primer corte terminó en desarrollo, esperando esa aprobación. Fue superado por la implementación y validación detalladas abajo. No hubo push, PR, merge, despliegue ni cambios en Fuentes/documentos congelados.

## Implementación posterior a la aprobación

UI-G02/G03/G04 viven en /planificacion. Selección UUID de obligación y enlace desde el resultado real de alta manual; sin bandeja ni otra historia. La ficha presenta identidad mínima e historia de asignaciones desde GET autorizado, que ahora emite ETag. Candidatos conservan código, orden, exclusiones y entradas del snapshot; null no equivale a carga cero. La tabla de carga usa sólo personId y cursor reales. Paginación anterior/siguiente conserva una cadena protegida de cursores de lectura ligada a actor, filtro y sección; el cliente no los interpreta.

La corrección se prepara con responsable marcado elegible, motivo normalizado por el contrato de dominio y comprobación contra la versión/evaluación mostradas. No calcula autoridad ni revalida elegibilidad viva. El POST real conserva los tres campos del cuerpo de Adenda 09, CSRF, If-Match y clave. Data Protection liga actor/obligación/cuerpo/versión/clave y expiración al máximo de ocho horas, limitada además por sesión absoluta. Ante transporte incierto, recuperar es acción explícita con la misma intención; 412 retira el formulario de envío hasta recarga y otra preparación explícita. El resultado CREADA/RECUPERADA sólo proviene de respuestas contractuales válidas y permanece distinguido si falla una lectura posterior.

RolePermissionProjection añade PER-TAREA-VER a los cuatro roles canónicos y PER-ASIGNACION-EXPLICAR/PER-CARGA-VER/PER-ASIGNACION-CORREGIR a Dirección, Administración y Subcoordinación exclusivamente. Es visibilidad; cada API sigue reautorizando en servidor. No se cambió dominio, persistencia de corrección, auditoría, ranking, autorización jerárquica ni transacciones de escritura.

Archivos adicionales modificados/creados tras el primer corte:

- docs/design/componentes.md, estados-y-mensajes.md, estados-de-dominio.md y navegacion.md: aprobación consumidora por referencia.
- src/Modules/Identity/Contracts/Roles.cs: proyección de permisos de presentación.
- src/Sgol.Web/Interface/ApiClient/ApiClientContracts.cs y SgolApiClient.cs: transporte y validación de historyNextCursor separado del cursor de colección.
- src/Sgol.Web/Pages/Planning/Index.cshtml.cs y Index.cshtml: composición con las secciones existentes.
- src/Sgol.Web/Pages/Planning/Index.Assignments.cs: lecturas, preparación, intención protegida, corrección y mensajes cerrados.
- src/Sgol.Web/Pages/Planning/_Assignments.cshtml: tablas, ficha, historia, errores, vacíos y diálogo.
- src/Sgol.Web/Pages/Planning/_ManualGeneration.cshtml: enlace desde obligationId confirmado.
- src/Sgol.Web/wwwroot/css/components.css y js/assignments.js: tokens, carga, foco y apertura del diálogo mediante componente existente.
- tests/Sgol.UnitTests/Front014PresentationTests.cs: 24 casos nuevos de presentación, intención, permiso, CSRF, no envío, conflicto, recuperación, resultado y cursor protegido.
- tests/Sgol.UnitTests/SgolApiClientTests.cs: cuatro casos nuevos de cursor de historia correcto/inválido.
- docs/traceability/IMPLEMENTATION_STATUS.md, FRONT_014_PROPUESTA_CONSUMIDOR.md y este informe: alcance, aprobación y evidencia.

## Validación final del cambio

Después del último cambio de código: dotnet build --no-restore --configuration Release, salida 0, 0 errores/advertencias. SDK 10.0.400 comprobado con activador en cada proceso. Sin restore adicional, suite completa, formato global ni gates remotos.

```powershell
dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --no-restore --configuration Release --filter 'FullyQualifiedName~Front014PresentationTests|FullyQualifiedName~ObligationQueryTests|FullyQualifiedName~AssignmentCorrectionTests|FullyQualifiedName~EligibilityEvaluationTests|FullyQualifiedName~ActiveLoadTests|FullyQualifiedName~RoleAdministrationTests|FullyQualifiedName~SgolApiClientTests|FullyQualifiedName~Front013IntentionTests' --logger 'console;verbosity=minimal'
dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --no-restore --configuration Release --filter 'FullyQualifiedName~InterfaceDesignRulesTests|FullyQualifiedName~ArchitectureBoundaryTests' --logger 'console;verbosity=minimal'
git diff --check
```

Resultados: **152/152** unitarias/presentación/contratos afectadas y **11/11** arquitectura; salida 0, ninguna fallida/omitida, filtros no vacíos. Diff-check sin errores (los avisos LF/CRLF reflejan core.autocrlf existente). No se suman los pases de diagnóstico como pruebas adicionales. Incluye 24 casos de la UI nueva, cuatro de metadatos y tres negativos de ETag añadidos en el primer corte, además de casos existentes ampliados y regresiones de los contratos consumidos. Criterios de presentación CA/CP-004/016/019 cubiertos en el nivel local disponible: conteos y ausencia de datos fieles, razones ordenadas, selección sólo elegible, motivo, permisos, clave/cuerpo/versión estables, rechazo y 412 sin reenvío. Auditoría, historia persistida, rollback y concurrencia PostgreSQL conservan contratos/backend aceptados; su verificación real nueva continúa diferida.

Diagnóstico conservado: el build inicial de las pruebas nuevas encontró cuatro reglas de analizador xUnit; se corrigió el uso de aserciones. Primer pase de 20 casos: 18/20 por falta de endpoint de routing en el render aislado; se corrigió el contexto de pruebas. Segundo pase: 19/20 por expectativa de acento literal que Razor codifica; la aserción conserva la exigencia de codificar script y comprueba el texto decodificado. Después, el grupo afectado pasó 144/144. Tras añadir metadatos/cursor y resultado confirmado con lectura fallida, el último pase pasó 152/152. El primer chequeo de foco de la previsualización leyó antes del evento close; se sustituyó la lectura inmediata por espera de la condición, sin alterar el comportamiento productivo.

## Pantallas para revisión humana

La variable opcional SGOL_FRONT014_PREVIEW_DIR exportó HTML de la vista Razor real con respuestas sintéticas en las pruebas de presentación. Antes de escribirlo se vacían valores de inputs ocultos: no contiene CSRF ni intención protegida. No se creó un modo sintético en producción ni un backend alternativo. Assets reales de tokens/CSS/JS se incorporan sólo al artefacto externo de revisión, sin descargas.

Ubicación: `C:\Users\siste\.codex\visualizations\2026\09\29\01a0ef04-9791-7752-a0b9-2733d05297b3\front014`.

capture.cjs abre archivos locales en Chromium disponible, viewport escritorio 1440×1000 y móvil 390×844, sin servidor ni excepción TLS. Cinco estados por viewport (normal, confirmación, conflicto, vacío, recuperada): **10/10** capturados y sin desbordamiento horizontal de página; Cancelar enfocado inicialmente, Escape cierra y retorna foco. Catorce PNG sanitizados: diez estados más recortes de carga y elegibilidad para cada viewport. Inspección visual de confirmación escritorio, normal móvil y carga escritorio realizada. preview-checks.json conserva las diez comprobaciones; no acredita integración hospedada, PostgreSQL ni accesibilidad exhaustiva. WebKit no se ejecutó; la previsualización móvil usó Chromium.

Commit local autorizado después de mostrar las pantallas según solicitud del responsable; sin publicación solicitada. PostgreSQL/API hospedada/concurrencia real/auditoría rollback, navegador con sesión y TLS, accesibilidad exhaustiva, WebKit y gates integrales siguen en Validación diferida por el alcance proporcional de AGENTS vigente, no por incompatibilidad demostrada del equipo. No se inició FRONT-015 ni otra tarea.
