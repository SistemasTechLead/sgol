# FRONT-019 — Validación y supervisión

2026-10-01, America/Mexico_City. Implementación local del plan aprobado mediante «Apruebo el plan», incorporado por Adenda 58 antes de editar código. Rama `codex/front-019`. Resultado final y comprobaciones abajo. Revisión visual del responsable pendiente; sin autorización de publicación, PR, checks remotos, merge o despliegue.

## Evidencia aceptada y alcance

Base: merge FRONT-018 `f722e44747e2a136825c72ac8c8f00122c079a00`, con cabeza aceptada `fd452ed6f573234dd9972b3b5fb9e0ed48967636`; [PR #87](https://github.com/SistemasTechLead/sgol/pull/87), [pipeline final correcto](https://github.com/SistemasTechLead/sgol/actions/runs/36923195911/attempts/1). El commit documental 96dfe4d quedó en ese hito. La evidencia y aprobaciones expresas del responsable superan los registros anteriores de publicación/merge pendientes. No se repitieron sus gates ni se extendió su autorización.

FRONT-019 cubre UI-V01..V04: pendientes, primera decisión, sustitución motivada/historia y supervisión de inferiores. BR-D09/M09 resueltas exclusivamente para esta consumidora por el plan/Adenda 58. Descarga y preview excluidos de FRONT-019; BR-API04 permanece abierto globalmente. FRONT-020, TECH-FRONT-005, indicadores y capacidades nuevas excluidos.

No hay migraciones, dependencias nuevas, restore, cambio de global.json ni modificaciones en Fuentes o documentos congelados. Se conservan CSS y componentes existentes, variables oficiales, Poppins local OFL y Georgia sistema. No se consultaron ZIP, Excel ni originales de IdentidadMarca.

## Resultado y decisiones aprobadas

- /validaciones reúne dos colecciones independientes, filtros contractuales, cursores protegidos por actor/sección/filtros, fecha de consulta y regreso al contexto de origen. Sin totales ni ETag de colección. La proyección real ubica links en la fila; el cliente valida esa estructura y traduce sólo rutas propias.
- El detalle existente presenta historia completa sin paginación y la acción disponible para el recurso. GET proyecta validationActions opcional mediante el mismo evaluador puro de autoridad de comandos; READ ONLY / REPEATABLE READ impide mezclar requisito, cadena y ETag o materializar requisitos/snapshots/auditoría.
- Las decisiones son humanas y explícitas: CUMPLIDA, INCOMPLETA o NO_CUMPLIDA; no hay selección por defecto ni cálculo automático. Fundamento obligatorio; motivo separado para escalamiento o sustitución. El texto mantiene normalización NFC/trim, límites 1000/500 y rechaza los tokens prohibidos aprobados. La ayuda prohíbe copiar evidencia/secretos; no se promete detección semántica universal.
- Preparar, confirmar y cancelar no escriben. La intención protegida conserva actor/recurso/ruta/cuerpo/ETag/clave y vence a ocho horas. Recuperación explícita conserva exactamente la intención; no hay reintentos ni rotación automática. La API no entrega meta.replayed: el mensaje de recuperación confirma la misma solicitud sin afirmar otra versión.
- Se preservan sesión, CSRF, autorización en servidor, alcance Loretta, cuenta/empleo/rol vigentes, jerarquía estricta y separación de personas. Dirección ve Administración/Subcoordinación/Piso; Administración ve Subcoordinación/Piso; Subcoordinación ve Piso; Piso no tiene esta colección. Propias/pares quedan excluidas de supervisión. La excepción de autovalidación de Dirección sólo se ofrece en el detalle autorizado.
- Una emisión inicial no es una sustitución. Segunda emisión secuencial sigue en 409; sustitución exige permiso, autoridad actual, decisión y ETag vigentes y motivo, aun con resultado equivalente. Una carrera real responde 412 al perdedor; un recurso fuera de alcance sigue convergiendo en 404. Replay reautoriza también la autoridad original y el rol capturado.
- Ejecución CONCLUIDA, evidencia, snapshots y decisiones previas permanecen. Sustitución crea sucesora y conserva SUSTITUIDA; una sola VIGENTE. Lecturas de pendientes/supervisión/historia no escriben. Evidence-review conserva su excepción previa: snapshot inmutable y auditoría atómicos, deduplicados por huella. No se interpreta como lectura pura.
- Confirmación accesible con retorno de foco, funcionamiento sin JavaScript, estados normal/foco/deshabilitado/error/cargando/vacío, conflictos con recarga explícita y errores regionales seguros. No se exponen respuestas crudas, cookies, claves ni URLs firmadas.

## Archivos del hito

| Grupo | Archivos |
|---|---|
| Contratos y servidor | Modules/Execution/Contracts/ValidationDecisions.cs; Modules/Identity/Contracts/Roles.cs y ValidationDecisionAuthorization.cs; Infrastructure/Persistence/Validation/EfValidationDecisionService.cs; Interface/Endpoints/ValidationDecisionApiEndpoints.cs |
| Cliente y presentación | Interface/ApiClient/ApiClientContracts.cs y SgolApiClient.cs; Interface/Validation/ValidationPresentation.cs, ValidationQuery.cs, ValidationIntention.cs, ValidationReturnContext.cs |
| Pantallas y navegación | Pages/Validations/Index.cshtml(.cs); Pages/MyWork/Details.Validation.cs, Details.cshtml(.cs), _ValidationDecision.cshtml, _ValidationHistory.cshtml; Pages/Shared/_Layout.cshtml; Interface/Navigation/NavigationItem.cs y SafeReturnDestination.cs; wwwroot/js/validations.js |
| Pruebas | Front019PresentationTests.cs; Front019ArchitectureTests.cs; ObligationConclusionPersistenceTests.Front019.cs; Front019BrowserTests.cs y BrowserFixture.Front019.cs; ajustes acotados de expectativas de conflicto existentes, fixture MyWork y reutilización del comprobador de accesibilidad FRONT-018 |
| Documentación | Adenda 58; plan único con aprobación conservada; referencias consumidoras en componentes/estados/mensajes/navegación/referencia renovada; IMPLEMENTATION_STATUS; este informe y manifiesto de capturas; .gitignore para capturas locales |

## Criterios y comprobaciones

CA/CP-028-P/N: los tres resultados, fundamento, motivo de sustitución/escalamiento, autoría vigente, rechazo de autovalidación no Dirección/pares, segunda emisión, conflicto y conservación de ejecución/historia. CA/CP-031-P/N: alcance de inferiores, filtros/cursor, pendientes derivados sin materialización y lecturas sin efecto. CAT-001..008: se consume la matriz congelada; no se modifica política ni se repiten gates completos aceptados de conclusión/carga.

SDK usado: C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe. Preflight fue la única comprobación inicial durante el plan; señaló SDK de PATH diferente al fijado. Se usó el SDK indicado, sin diagnósticos iniciales adicionales.

```powershell
& C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe build --no-restore --configuration Release
& C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe test tests/Sgol.UnitTests --no-build --configuration Release --filter 'FullyQualifiedName~Front019|FullyQualifiedName~ValidationDecision|FullyQualifiedName~HierarchySupervision|FullyQualifiedName~Front016Presentation|FullyQualifiedName~Front018Presentation|FullyQualifiedName~SgolApiClient|FullyQualifiedName~RoleAdministration|FullyQualifiedName~Navigation'
& C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe test tests/Sgol.ArchitectureTests --no-build --configuration Release --filter 'FullyQualifiedName~Front019|FullyQualifiedName~ValidationDecision|FullyQualifiedName~HierarchySupervision|FullyQualifiedName~Front016|FullyQualifiedName~Front018|FullyQualifiedName~EvidenceInfrastructure|FullyQualifiedName~InterfaceDesignRules'
& C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe test tests/Sgol.IntegrationTests --no-build --configuration Release --filter 'FullyQualifiedName~Front019|FullyQualifiedName~ConcurrentIssueAndReplacement|FullyQualifiedName~ValidValidator|FullyQualifiedName~Hu031|FullyQualifiedName~EscalationAndDirection' --logger 'trx;LogFileName=front019-contracts.trx'
$env:SGOL_FRONT019_CAPTURE_DIR = Join-Path (Get-Location) '.artifacts/front-019'
& C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe test tests/Sgol.FrontendBrowserTests --no-build --configuration Release --filter 'FullyQualifiedName~Front019BrowserTests' --logger 'trx;LogFileName=front019-browser.trx'
git diff --check
```

Resultado: **Implementada localmente**. Build Release PASS, 0 errores/advertencias; unitarias enfocadas **193/193**; arquitectura enfocada **22/22**; PostgreSQL real **10/10**; navegador Chromium HTTPS/PostgreSQL **2/2**, escritorio y móvil; git diff --check PASS. Ningún caso omitido en esos filtros. No hubo restore. Los resultados finales posteriores a las correcciones reemplazan los intentos fallidos/interrumpidos; no se presenta una cancelación como éxito. Capturas finales: **36**, fuera de Git/Fuentes; manifiesto FRONT_019_CAPTURAS.md.

Se revisaron directamente pruebas antiguas de endpoints, permisos, jerarquía, cliente, diseño y persistencia, además de las nuevas. La expectativa antigua que permitía 409/404 en carreras se corrigió hacia el 412 exigido por Adenda 25; no se relajó la expectativa nueva para aceptar respuestas inesperadas. Fallos iniciales: referencia modular incorrecta corregida sin dependencia nueva; analizadores EF1003/CA1822 corregidos; diferencias de concurrencia corregidas con autoridad antes de revelar conflicto; estructura de links alineada al envelope existente. Un intento de captura de carga reteniendo navegación se interrumpió por bloqueo de la propia prueba; no es PASS ni fallo de instalación. Se sustituyó por pausa del evento de navegación después de los listeners reales y se volvió a ejecutar.

## Revisión visual y límites

La aplicación Kestrel HTTPS y PostgreSQL real funcionan con cuatro cuentas y evidencia exclusivamente sintéticas. Chromium verifica escritorio y móvil, texto 200%, reflow 320, contraste, área mínima de controles, teclado, foco de confirmación/cancelación, Escape y movimiento reducido. Se prueba paginación, vacío/error/carga, denegación Piso, recuperación de la misma intención, tres resultados/historia, 412, escalamiento, autovalidación excepcional y envío sin JavaScript. El manifiesto identifica las capturas; la carga detiene sólo navegación tras ejecutar el listener real para conservar el estado transitorio.

Validaciones diferidas: zoom nativo, lector de pantalla, dispositivos físicos; WebKit Windows PUT/S3 HTTP local; aislamiento productivo completo de SeaweedFS. Son límites previos conservados y no resueltos por emulación, capturas ni aprobación visual. No se ejecutaron suite integral, formato global ni checks remotos: reservados para publicación expresamente autorizada. La carrera específica sustitución de evidencia contra emisión no se volvió a ensayar en este hito; el bloqueo común y revisión transaccional existentes se conservan, sin declararla nueva validación aprobada.

Para el usuario: «Pendientes de validar» muestra tareas concluidas que puedes revisar. «Supervisión de inferiores» permite consultar el trabajo de los puestos de tu alcance. «Ver tarea» abre evidencia disponible e historia. Elige Cumplida, Incompleta o No cumplida y explica el fundamento; preparar muestra la confirmación antes de registrar. Sustituir exige explicar el motivo y conserva la decisión anterior. Un escalamiento también exige motivo. Dirección puede autovalidar su propia tarea únicamente en la excepción autorizada. Concluida describe ejecución; Vigente/Sustituida describen decisiones. Validar no reabre la tarea.

Revisión visual expresa solicitada en este chat. No activa publicación ni seguimiento remoto.

## Corrección visual solicitada por el responsable

Precisión posterior: el responsable solicita conservar el estilo de la banda, restaurando el orden vertical de campos en pendientes y supervisión. Se añade sólo la variante tabla-filtros--vertical al formulario de /validaciones: campos en una columna, mismos anchos máximos y espacios oficiales, botón al final. Estado de ejecución queda como último campo de supervisión; las versiones de evidencia conservan la corrección anterior. Se vuelve a entregar únicamente escritorio-administracion-pendientes.png. Verificación: build Release PASS, 0 errores/advertencias; arquitectura enfocada 11/11 PASS; navegador real de escritorio 1/1 PASS (front019-vertical-filters.trx), diff PASS y captura revisada. Al omitir capturas intermedias se detectó que el harness dependía accidentalmente del tiempo de screenshot antes de enviar Escape: Prepare ahora espera explícitamente el formulario de confirmación y la carga, conservando la aserción de foco sin relajarla. Un comando de build con el nombre del proyecto mal escrito produjo MSB1009; corregido el nombre, build enfocado PASS.

Se autorizaron cambios concretos sobre las capturas escritorio-administracion-pendientes y escritorio-historia-vacia: separación del botón, asociación/alineación de etiquetas y controles en ambas colecciones, título «Supervisión», alineación de Consultar versiones/Quitar filtros y número de versión centrado sobre el badge. Se reutiliza tabla-filtros compartido y --espacio-8; sin valores visuales nuevos, mensajes funcionales, endpoints o permisos. Archivos: Index.cshtml, _EvidenceVersions.cshtml, components.css y referencia en componentes.md. Capturas entregadas: únicamente las dos de escritorio señaladas, sintéticas de aplicación funcionando.

Comprobación de esta corrección: build Release final PASS, 0 errores/advertencias; navegador real escritorio/móvil 2/2 PASS (front019-visual-corrections.trx); revisión directa de las dos capturas; git diff --check PASS. Una compilación solapada con testhost encontró MSB3021/MSB3027 por DLL ocupadas; es una incidencia de ejecución local, no instalación. Se leyó su causa y se esperó a terminar pruebas antes de recompilar secuencialmente. No se declara ese intento correcto. El selector de salida del harness permite entregar sólo las capturas corregidas sin alterar el recorrido funcional.
