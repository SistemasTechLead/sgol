# FRONT-013 — UI-G01 y generación manual

## Estado

Publicada en la rama remota `codex/front-013` el 2026-09-29, tras su implementación y validación local. Commit de implementación: `1ed660ce00dedbd3769c65509a81976a5da48518`. El PR que incorpora esta actualización identifica la publicación; el check requerido debe validar su cabeza final y el merge requiere segunda autorización. La Adenda 51 fue aprobada íntegramente mediante «Si la apruebo». BR-API06 se resuelve por ese contrato consumidor; su única mención previa en la fila 86 no acreditaba una resolución efectiva. La categoría completa se ejecutó y sus casos afectados pasaron en reproducción enfocada; la verificación final de FRONT-013 terminó 4/4. Se conserva el límite de no disponer de un pase único verde de toda la categoría. En el cierre local no había commit, push, PR ni merge. El 2026-09-29 el usuario autorizó commit local, push y PR mediante «Si autorizo»; el merge sigue pendiente de una segunda autorización específica.

Sólo UI-G01 en `/planificacion#alta-manual` y sus contratos consumidores mínimos. Se conservan semana/calendario y `/configuracion`. No se iniciaron FRONT-014..020 ni TECH-FRONT-005; no hay asignación, publicación de plan, captura/carga de evidencia, conclusión, emisión de validaciones ni recurrencia manual.

## Base, aislamiento e historia

- Checkout principal `C:\Users\josej\Dev\SGOL`: HEAD/master `bed96907d26170502d09f4530495c3fd5894bdcc`; cambios preexistentes en AGENTS.md, scripts/demo/run-cv03.ps1 y tests/Sgol.ArchitectureTests/Cv03DemoArchitectureTests.cs conservados, sin abrir sus diffs.
- Único preflight inicial: SDK 10.0.400, fuentesClean=true, fuentesRebaselinePending=false. `rtk` no disponible: comandos directos. `ghSession=unauthenticated` dentro del aislamiento; consultas ampliadas funcionaron sin cambiar cuenta.
- `git worktree list --porcelain` resolvió las rutas. Ningún worktree FRONT-001..012 se reutilizó o reseteó.
- Verificación remota del 2026-09-28: PR #78 MERGED; cabeza `8c396e97b8de7cddc54e5041921f1fa644da1079`; `TECH-BASE-003 / PR gates` SUCCESS en run `36495586807` para ese SHA; merge `e87c64c3713fe53f572a4d4f4d8c4088022bca4a`. Fetch limitado a master, ls-remote y ambas ascendencias devolvieron evidencia concordante. FRONT-012 se reconcilió a Integrada conservando sus pruebas locales, los dos pases 16/17 por cleanup y el primer run fallido `36494438980`.
- Worktree nuevo inicialmente limpio: `C:\Users\josej\.codex\worktrees\front-013\SGOL`; rama `codex/front-013`, HEAD/base igual al merge verificado.
- Antes de aprobar íntegramente Adenda 51 se validó sólo la proyección independiente: build correcto, 32/32 unitarias, 12/12 PostgreSQL, formato dirigido y diff-check. Es evidencia histórica del contrato anterior; no acredita la UI ni el POST v2.
- «Lo autorizo» permitió preparar la propuesta y composición; «Si la apruebo» dio eficacia a todas las secciones de Adenda 51. La propuesta anterior se conserva como historia de las brechas.

Fuentes selectivas: fila 86/reglas comunes de Adenda 45; tareas insertadas; Adendas 44/46; HU-014/HU-015, CA/CP-014/015, CAT/CPT-001..008, filas TAR autorizadas y F05-JP aplicables; F06 API/modelo/arquitectura/pruebas; seis documentos completos de diseño. No se reconstruyeron F00–F07 ni se leyeron fuentes congeladas o binarios Excel.

## Contrato técnico implementado

| Superficie | Comportamiento |
|---|---|
| GET `/api/v1/generation-requests/options` | Opciones vigentes, manuales, visibles y permitidas por nivel; metadato count. Omite TAR con guarda legacy. No acepta parámetros |
| GET `/api/v1/generation-requests/receipt-origins` | Padres TAR-0092 v2 pendientes, creador/superior visible; referencia exacta, cursor protegido por actor/filtro/tamaño de 30 minutos; sólo IDs, referencia, inicio y período |
| POST `/api/v1/generation-requests` v2 | Seis propiedades cerradas: schemaVersion, ruleVersionId, branchId, periodId, originType, inputPayload. Máximo 32 KiB; no acepta originReference suministrada por cliente |
| GET `/api/v1/generation-requests/{id}` | Resultado persistido autorizado e input v2 de sólo lectura; no reconstruye políticas ni materializa historia |
| Razor `/planificacion` | Selección taskCode allowlisted y consulta generationRequestId UUID; formularios y búsqueda de referencia usan POST, fuera de URL |

Seis formularios cerrados para TAR-0007/0008/0011/0018/0092/0093. TAR-0005/TAR-0026 permanecen recurrentes. Sucursal, regla, origen y propiedades canónicas no editables. Referencias declaradas externas sin URL, NFC y límites por escalares Unicode; se aclara que SGOL no verifica documentos externos. Instantes de entrada en America/Mexico_City convertidos a UTC. TAR-0093 hereda período del padre; no permite inventar un UUID de recepción desde la pantalla.

Origen funcional: tupla CAT normalizada, SHA-256 y prefijo CAT2; la tupla persistida se compara ante coincidencia del digest. Índices únicos de origen permanente y de origen pendiente, conservando RN-010. Bloqueos transaccionales por intención y sucursal/TAR; padre bloqueado frente a conclusión. Snapshot de input, tarea, regla, evidencia, validación, calendario y vencimiento. Separado usa vencimiento documentado, controversia detección+30 minutos, garantía siete días laborables publicados; no se añade SLA a las otras TAR.

Un solo commit de base de datos crea solicitud, obligación, vínculo, snapshot, dos eventos y resultado idempotente. Los SaveChanges intermedios sólo satisfacen las FK. Fallos inyectados antes de cada escritura final/commit dejan cero recursos parciales. Replay terminal reautoriza y conserva ambos IDs, status y ubicación; no vuelve a exigir regla activa ni vuelve a auditar creación. Conflicto conserva la intención y se audita según HU-034; no hay reintento automático ni rotación automática de clave.

La variante legacy sólo admite replay previamente confirmado y GET histórico. No hay backfill. La guarda por TAR rechaza nuevas altas v2 potencialmente competidoras mediante `GENERACION_LEGACY_REQUIERE_REVISION`; los registros históricos no se borran ni reparan. La migración `20260929005540_AddManualGenerationSnapshots` es aditiva, sin BOM, con Down bloqueado. EF verificó ausencia de cambios de modelo pendientes.

PER-OBLIGACION-CREAR se proyecta sólo para DIRECCION/ADMINISTRACION/SUBCOORDINACION, exclusivamente para presentación. El servidor comprueba cuenta, empleo, rol vigente, alcance, jerarquía y nivel. Se niegan rol/empleo fechados a futuro; Piso de ventas sigue excluido. No se infiere autoridad de puestos o nombres traducidos.

Razor reutiliza cliente HTTP común, sesión por petición, CSRF e Idempotency-Key por intención. Data Protection liga actor/cuerpo/clave durante ocho horas; no hay almacenamiento de navegador. Una intención vencida con ID conocido sólo hace GET; sin ID muestra incertidumbre y no vuelve a crear. Confirmación con foco inicial en Cancelar, Escape y retorno de foco; errores asociados y resumen enfocable. No se añade ETag/If-Match. Mensajes cerrados según sección 11 de Adenda 51; desconocidos conservan mensaje seguro/correlationId. El cliente común no registra URI con referencias privadas.

## Archivos y mapa de pruebas

| Grupo de archivos | Criterios y pruebas |
|---|---|
| Modules/Generation/Contracts/ManualGeneration.cs y GenerationRequests.cs | Esquemas, referencias, fechas, campos cerrados y vectores de digest: ManualGenerationInputTests; transporte: GenerationRequestTests |
| Modules/Identity/Contracts/Roles.cs | Proyección canónica positiva/negativa: GenerationRequestTests y RoleAdministrationTests |
| Persistence/Generation/EfGenerationRequestService*.cs y GenerationAuthorizationQuery.cs | Autorización, alcance/jerarquía, seis CAT, deadlines, padre y paginación: GenerationRequestPersistenceTests y parciales Manual/Cat |
| EfWorkObligationMaterializer, WorkObligationConfiguration, migración/snapshot | Atomicidad, creación única, concurrencia, identidad/versiones, no efecto y legacy: GenerationRequestPersistenceTests.Atomic y restantes casos de servidor |
| GenerationRequestApiEndpoints, API client/contracts, Program | POST/GET reales, CSRF, validación, perfil denegado, replay y conflicto: Front013BrowserTests.Api, GenerationRequestTests y SgolApiClientTests |
| Planning/Index*, _ManualFilters, _ManualGeneration, manual-generation.js, components.css | Seis formularios, teclado/foco, confirmación, resultado y replay en Chromium/escritorio y WebKit/móvil: Front013BrowserTests; vacíos/errores/privacidad: parcial Errors |
| Front013IntentionTests | Expiración con/sin ID, vínculo al actor, token alterado y falta de CSRF: ninguna escritura HTTP |
| BrowserFixture.Manual y declaración partial | Calendario sintético y conflicto visual inyectado; no cambia confianza, tiempos ni cleanup histórico |
| Cv02Seed y dos llamadas existentes | Adaptación mínima de consumidores internos al cuerpo v2 y políticas sintéticas; compilada, sin cambiar Worker ni ejecutar la demo integral |
| docs/design, Adenda 51 y trazabilidad | Contrato aprobado, límites, evidencia y reconciliación de FRONT-012 |

La prueba visual de conflicto inyecta explícitamente un hash en PostgreSQL desechable, porque la UI protege cuerpo/clave. Se restaura el hash y se comprueba recuperación del mismo ID. Separadamente, el POST HTTPS envía realmente la misma clave con un cuerpo distinto y comprueba 409. No se confunde la inyección visual con la validación de idempotencia del servidor.

Hu034IdempotencyArchitectureTests conserva su inventario de 19 consumidores: este POST ya estaba inventariado. No se cambian conteos por añadir la UI. Las pruebas de servidor no se sustituyen por Playwright.

## Comandos ejecutados y resultados

Desde el worktree nuevo; permisos ampliados para escribir allí y ejecutar herramientas cuando el aislamiento lo requiere:

```powershell
dotnet restore --locked-mode
dotnet tool restore
dotnet build --no-restore --configuration Release
dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-restore --configuration Release -p:BuildProjectReferences=false --filter 'FullyQualifiedName~Front013IntentionTests|FullyQualifiedName~GenerationRequestTests|FullyQualifiedName~ManualGenerationInputTests|FullyQualifiedName~SgolApiClientTests|FullyQualifiedName~RoleAdministrationTests'
dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~GenerationRequestPersistenceTests'
dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~Hu034IdempotencyArchitectureTests|FullyQualifiedName~InterfaceDesignRulesTests|FullyQualifiedName~ArchitectureBoundaryTests|FullyQualifiedName~Audit'
dotnet ef migrations has-pending-model-changes --no-build --configuration Release --project src/Sgol.Web --startup-project src/Sgol.Web
dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~Front013BrowserTests' --logger 'console;verbosity=minimal'
dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter 'Category=FRONT_BROWSER' --logger 'console;verbosity=normal'
dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-restore --configuration Release -p:BuildProjectReferences=false --filter 'FullyQualifiedName~SixClosedFormsCreateAndRecoverStableResultsOnDesktopAndMobile|FullyQualifiedName~Front009BrowserTests|FullyQualifiedName~Front007BrowserTests|FullyQualifiedName~Front006BrowserTests|FullyQualifiedName~FirstAccess_RecurrentTotp_AndRecoveryReachFullSessionOnlyAfterMfa'
dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-restore --configuration Release -p:BuildProjectReferences=false --filter 'FullyQualifiedName~Front013BrowserTests'
$changed = @((git diff --name-only) + (git ls-files --others --exclude-standard)) | Where-Object { $_.EndsWith('.cs') } | Sort-Object -Unique
dotnet format --no-restore --verify-no-changes --include $changed
git diff --check
```

- Restore locked de paquetes: una pasada inicial, 25 proyectos. Tool restore restauró la herramienta EF fijada; no agrega dependencias del producto.
- Build Release: 0 errores/advertencias. Unitarias enfocadas: 95/95. PostgreSQL real: 30/30, 2 m 43 s. Arquitectura afectada: 16/16. Modelo EF: sin cambios pendientes.
- Categoría completa FRONT_BROWSER ejecutada: 16/21 en 26.1031 minutos. Un fallo de foco inmediato en FRONT-013 escritorio; tres cancelaciones de X509Store.Add (FRONT-009/007/006); un timeout de acceso histórico con navegación chrome-error. Reproducción enfocada de los afectados más ambos viewports: 6/6 en 7.7414 minutos. El código de producto, la fixture, certificados, confianza y tiempos no cambiaron. Se conserva 16/21 como resultado de esa corrida completa; no se presenta como un pase único verde. Verificación final de FRONT-013 tras sincronizar su aserción de foco: 4/4 en 8.5229 minutos, con cleanup correcto. Los 21 casos tienen un resultado correcto en la categoría o en su reproducción posterior; no se repitió toda la categoría tras un cambio exclusivo de sincronización del test.
- Formato dirigido: correcto; se corrigió y verificó el único ENDOFLINE introducido al cambiar la aserción. Diff-check de archivos seguidos y revisión de espacios de los 22 archivos nuevos: correctos.

## Fallos reproducidos durante implementación

1. Secuencia sintética de UUID insuficiente antes de la colisión de auditoría prevista: corregida sólo la secuencia y pasada la prueba enfocada.
2. Semilla de calendario anterior a la release vigente: corregido el instante sintético tras demostrar solapamiento, sin cambiar reglas de versión.
3. Padre concluido: la fixture de conclusión debía declarar la condición de diferencia/daño que exige su evidencia. Se agregó parámetro opcional sintético conservando el comportamiento histórico predeterminado y todas las restricciones SQL.
4. WebKit: html/body 453 o 491 para viewport 390 aunque el control select cabía. Reproducción identificó la etiqueta propagando el texto nativo. Se recorta sólo el contenedor de select con margen de foco aprobado; no se oculta overflow global.
5. Corrida enfocada detenida antes de Kestrel: diagnóstico temporal demostró bloqueo en X509Store.Add, con ventana de seguridad de Windows. Se interrumpió esa corrida; la reproducción móvil pasó y el caso escritorio siguiente falló por cancelación explícita de Windows. La categoría completa también espera confirmaciones de retirada en X509Store.Remove. El usuario confirma esos diálogos. Se retiró la instrumentación temporal; no se cambiaron certificado, confianza, tiempos ni cleanup. Estos resultados no se declaran regresión funcional.

Una corrida completa anterior fue interrumpida; no se contabiliza como aprobada.

6. Captura de carga: detener la navegación con una ruta interceptada hacía que los localizadores esperaran esa misma navegación. Se reprodujo en ambos navegadores; ahora se retiene una única entrega nativa del formulario antes de navegar, se observa el estado real del listener de progreso y luego se envía el POST real. Sin cambiar timeouts. Reproducción corregida: 2/2 (seis formularios por viewport).

7. La categoría completa detectó una aserción de foco inmediato tras Escape en escritorio. El diálogo ya figuraba cerrado. La repetición real de ambos viewports pasó; un diagnóstico aislado con components.js real confirmó que el foco seguía en Cancelar inmediatamente después de close y pasaba al disparador al procesarse su evento. La hipótesis inicial del diagnóstico esperaba BODY y fue corregida al observar BUTTON/Cancelar; reproducción precisa 1/1. Se retiró toda instrumentación y se sustituyó sólo la aserción inmediata por ToBeFocusedAsync, sin ampliar timeouts ni cambiar producto o fixture.

8. FRONT-009/007/006 devolvieron cancelación de Windows en X509Store.Add antes de iniciar sus escenarios; los tres pasaron en la repetición enfocada con confirmaciones manuales. El caso histórico de acceso llegó a chrome-error y agotó navegación a MFA; pasó aislado en la misma repetición, sin cambiar esperas, certificados ni confianza. La causa exacta de esa navegación fallida no quedó demostrada; no se atribuye a una regresión funcional.

## Evidencia y límites

Capturas sintéticas sanitizadas fuera del checkout: `C:\Users\josej\.codex\worktrees\front-013\front-013-evidence`. Se enmascara todo el encabezado. Hay 70 capturas: 35 de escritorio y 35 móviles, reunidas en FRONT-013-pantallas.html y manifest.json. Cada captura verifica html/body y ancho PNG de 1440 escritorio o 390 móvil. Se esperan respuestas nuevas de POST/GET; DOMContentLoaded se registra antes del clic. No se guardan cookies, claves, CSRF, contraseñas, TOTP ni otra identidad de sesión.

Cierre local: build, pruebas enfocadas, categoría de navegador con reproducciones documentadas, revisión visual, dimensiones PNG, formato y diff revisados. Diff funcional y documental de 58 archivos, sin diagnósticos temporales; checkout principal y sus tres cambios preexistentes conservados. Commit y push autorizados y ejecutados con SistemasTechLead, autor local confirmado sistemas@lorettazapateria.com, sin cambiar configuración global. Pipeline remoto pendiente de comprobar para la cabeza final del PR; no se presume verde. Merge no ejecutado, sujeto a la segunda autorización original. La cuenta activa original javierjose201648-cmyk debe restaurarse al terminar la publicación. No se ejecutó suite integral ni demo CV-02 integral, por alcance proporcional. No se presume que datos legacy de una base real sean compatibles: la guarda se acredita con datos sintéticos, sin inspeccionar ni reparar datos reales.
## Primer pipeline y corrección enfocada

El run remoto `36590513659`, cabeza `eac3912f834648a214d22cff19709772d6faf751`, falló en `PostgreSqlPersistenceTests.Migrations_CreateOnlyTheApprovedTables`: el inventario histórico omitía `20260929005540_AddManualGenerationSnapshots`. El log mostró arquitectura 58/58, unitarias 744/744 e integración PostgreSQL 257/258; el navegador y la demo integral posterior no se ejecutaron. El error de subida de artefactos CV-04 fue derivado de esa demo omitida, no una causa independiente.

Se reprodujo localmente el mismo fallo 1/1 antes de editar. Se añadió únicamente la constante y su entrada en el inventario de migraciones; la lista de tablas y restantes aserciones se conservaron. Reproducción corregida con PostgreSQL real: 1/1, 8 segundos. Se verificaron formato dirigido y diff-check. No cambió el producto, la migración, la UI, los timeouts ni la fixture; no se repitió el navegador por esta corrección exclusiva de inventario.

```powershell
dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~PostgreSqlPersistenceTests.Migrations_CreateOnlyTheApprovedTables'
dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-restore --configuration Release -p:BuildProjectReferences=false --filter 'FullyQualifiedName~PostgreSqlPersistenceTests.Migrations_CreateOnlyTheApprovedTables'
dotnet format --no-restore --verify-no-changes --include tests/Sgol.IntegrationTests/PostgreSqlPersistenceTests.cs
git diff --check
```

El nuevo commit exige un pipeline válido para su SHA final; el run fallido se conserva como evidencia histórica.
## Segundo pipeline y referencias de esquema vigente

El run `36592334394`, cabeza `57ba94041acd2086fa86b870fe64cd6ce9b7d428`, pasó unitarias 744/744, arquitectura 58/58, PostgreSQL 258/258 y **FRONT_BROWSER completo 21/21** (6 m 48 s). Falló después en la demo integral CV-04, antes de iniciar sus escenarios. No se descargaron ni abrieron artefactos ZIP.

La reproducción local produjo `CV04_DATABASE_CONTRACT_FAILED` en POSTGRESQL: DemoContract.LatestMigration seguía apuntando a la migración anterior de autenticación. La búsqueda selectiva encontró la misma expectativa de esquema vigente en CV-05 y en el descriptor/gate HU-035. Una prueba enfocada nueva reprodujo las cuatro discrepancias antes de editar (0/4). Se actualizaron sólo cinco literales en cuatro archivos; no se tocaron referencias históricas que exigen la presencia de una migración anterior.

Archivos adicionales: `tests/Sgol.Cv04Demo/DemoContract.cs`, `tests/Sgol.Cv05Demo/DemoContract.cs`, `scripts/operations/invoke-hu-035-amd64-gate.ps1`, `tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryAmd64GateTests.cs` y `tests/Sgol.ArchitectureTests/MigrationConsumerInventoryTests.cs`. La prueba compara los consumidores de esquema vigente con la última migración fuente, para impedir que este inventario vuelva a quedar desfasado.

Validación posterior: build Release 0 errores/advertencias; arquitectura afectada 8/8; contrato CV-04 6/6; contrato puro CV-05 5/5; consumidor HU-035 condicionado compilado con SGOL_HU035_AMD64_TESTS=true; formato dirigido y diff-check correctos. El primer filtro NoDockerContractTests en CV-05 no encontró casos y no se contabiliza; se ejecutó después Cv05PureContractTests, su clase real.

Demo CV-04 real con Kestrel HTTPS y PostgreSQL: **PASSED**, dos ciclos, cero evidencias fallidas, cleanup PASSED, 65.824 segundos. Informes sanitizados en `.artifacts/cv04/latest` y fallo previo conservado en `previous-failure`, fuera del commit. No cambian producto, UI, confianza, tiempos ni fixture. No se repite navegador local por estos literales de inventario; se conserva el pase completo remoto del producto. CV-05 integral y HU-035 integral no se ejecutaron localmente: esta corrección verifica sus referencias y compilación; el gate HU-035 exige su ejecución nativa Linux AMD64 en CI.

```powershell
./scripts/demo/run-cv04.ps1 -Mode Automated
dotnet build --no-restore --configuration Release
dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~MigrationConsumerInventoryTests|FullyQualifiedName~Cv04|FullyQualifiedName~Cv05|FullyQualifiedName~Hu035'
dotnet build tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj --no-restore --configuration Release -p:SGOL_HU035_AMD64_TESTS=true -p:BuildProjectReferences=false
dotnet test tests/Sgol.Cv04Demo/Sgol.Cv04Demo.csproj --no-build --configuration Release --filter 'FullyQualifiedName~NoDockerContractTests'
dotnet test tests/Sgol.Cv05Demo/Sgol.Cv05Demo.csproj --no-build --configuration Release --filter 'FullyQualifiedName~Cv05PureContractTests'
dotnet format --no-restore --verify-no-changes --include tests/Sgol.ArchitectureTests/MigrationConsumerInventoryTests.cs tests/Sgol.Cv04Demo/DemoContract.cs tests/Sgol.Cv05Demo/DemoContract.cs tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryAmd64GateTests.cs
git diff --check
```

El nuevo commit requiere su propio check completo verde. Los pases parciales del segundo run no se presentan como éxito global del pipeline.
