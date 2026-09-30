# FRONT-015 — Plan semanal y publicaciones

Fecha: 2026-09-29, America/Mexico_City. **Implementada localmente** en `codex/front-015`, base `9b1c2f07d83b7c2370326b3e817d63a533900b4e`. Commit implementado: el que contiene esta actualización. Registro inicial local; publicación posterior acreditada en la sección siguiente. No se declara Integrada o Terminada.

## Contrato y autorización

La orden «Ahora vamos con esa implementación» autoriza trabajo local de FRONT-015. Antes de escribir lecturas o interfaz se preparó `F07_ADENDA_53_PROPUESTA_CONSUMIDOR_FRONT_015.md`, porque AGENTS.md prohíbe decidir silenciosamente sobre contratos y diseño ausentes. El responsable aprobó íntegramente sus secciones 2–8 mediante «Apruebo íntegramente las secciones 2–8». La Adenda 53 quedó incorporada localmente conservando el nombre revisado por el responsable; componentes, mensajes, estado PUBLICADO y navegación se incorporaron por referencia a docs/design.

Fuentes: fila 88 de Adenda 45; HU-020/HU-021 y CA/CP-020/021; F06 §5.5 y §7; Adendas 10/11/15/46/53. FRONT-008/013 integradas y FRONT-014 Implementada localmente constituyen las dependencias aceptadas. No se repitieron sus gates ni se implementó FRONT-016.

## Resultado

- GET `/api/v1/plans/{isoYear}/{isoWeek}` entrega identidad y estado persistidos con ETag, queriedAt y no-store; no crea plan ni período.
- GET `/api/v1/plans/{planId}/versions` lista historia autorizada paginada. Con publicationId consulta los pares históricos paginados, sin sustituir assignmentVersionId congelado por la asignación actual.
- El lector implementa IPlanQueryReader reutilizando dentro de una transacción read-only RepeatableRead el actor y universo vigente de HU-023. Filtra antes de paginar; oculta recursos ajenos y supersedesId no visible. El cursor protegido liga actor, rol, plan, variante, publicación y límite; cada petición reautoriza.
- UI-G05/G06 conviven en `/planificacion`. El contenido actual usa la consulta existente de obligaciones por período y se distingue del snapshot publicado. Hay filtros, vacíos distintos, errores, carga anunciada, controles deshabilitados, foco de errores y confirmación accesible sin textarea de motivo.
- Ensure conserva PER-PLAN-VER para los cuatro roles según Adenda 10. Publicar conserva autoridad del servidor y cuerpo vacío de Adenda 11. PER-PLAN-PUBLICAR se proyecta sólo para presentación de Dirección/Administración/Subcoordinación.
- CSRF, intención protegida ligada a actor/operación/semana/plan y ETag original se conservan en reenvío. Un 412 bloquea publicar hasta recarga y nueva decisión. La incertidumbre no se anuncia como éxito. La denegación posterior limpia resultados y oculta reenvío.
- No hay migraciones, paquetes, cambios de reglas de publicación, nuevas entidades, Worker, bandeja, evidencia o cambios en Fuentes.

## Archivos del cambio

| Área | Archivos |
|---|---|
| Contrato y trazabilidad | F07_ADENDA_53_PROPUESTA_CONSUMIDOR_FRONT_015.md; docs/traceability/FRONT_015_PLAN_SEMANAL.md; docs/traceability/IMPLEMENTATION_STATUS.md |
| Diseño aprobado | docs/design/componentes.md; estados-y-mensajes.md; estados-de-dominio.md; navegacion.md |
| Contratos internos y permisos de presentación | src/Modules/Planning/Contracts/PlanQueries.cs; src/Modules/Identity/Contracts/Roles.cs |
| Lecturas y composición Web | src/Sgol.Web/Infrastructure/Persistence/Execution/EfObligationQueryReader.cs y .Plans.cs; Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs; Interface/Endpoints/PlanQueryApiEndpoints.cs; Program.cs |
| Razor y mejora progresiva | src/Sgol.Web/Pages/Planning/Index.cshtml.cs; Index.cshtml; Index.Plans.cs; _Plans.cshtml; src/Sgol.Web/wwwroot/js/plans.js |
| Pruebas nuevas ejecutadas | tests/Sgol.UnitTests/PlanQueryApiEndpointTests.cs; Front015PresentationTests.cs |
| Pruebas de persistencia preparadas | tests/Sgol.IntegrationTests/PlanPublicationPersistenceTests.cs pasa a partial; PlanPublicationPersistenceTests.Front015.cs agrega siete casos PostgreSQL |

## Criterios y evidencia disponible

| Criterio | Evidencia local | Límite |
|---|---|---|
| CA-020 / CP-020-P/N | Render con dos TAR en un mismo plan; ensure con cuatro roles, identidad recuperada y misma intención; plan ausente y plan sin filas distinguibles; tests existentes de comando | Unicidad/restricciones PostgreSQL siguen respaldadas por implementación aceptada; nuevas lecturas relacionales no ejecutadas contra PostgreSQL en esta tarea |
| CA-021 / CP-021-P/N | Separación ver/publicar en los cuatro roles; Piso sin controles y POST Razor denegado; comando existente y manejo de errores; snapshot usa pares recibidos y ETag actual separado del histórico | V2, filtrado relacional, concurrencia y efectos transaccionales están preparados en casos PostgreSQL y pendientes del hito |
| Contrato y seguridad de presentación | Autenticación, filtros cerrados, cursor protegido/alterado, correlationId, no efecto de preparación, CSRF, intención alterada/actor distinto, replay, 412 sin retry y reenvío de clave/ETag originales | Reautorización hospedada, TLS y lecturas de base real no se acreditan por dobles sintéticos |
| UI-G05/G06 | Render Razor real con codificación de nombres, estados normal/vacío/error/conflicto/confirmación/recuperación, controles y marcado de carga/foco; arquitectura sin literales visuales | Render no mide teclado, focus trap, contraste o reflow reales en navegador |

## Comprobaciones locales ejecutadas

Preflight inicial: master, treeClean=true, fuentesClean=true, fuentesRebaselinePending=false. El SDK fijado 10.0.400 no estaba disponible y preflight informó 10.0.401 instalado. Se instaló 10.0.400 en `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400`, sin cambiar global.json. El instalador oficial descargó/extrajo un ZIP pese a la restricción del repositorio; el error de procedimiento se informó al responsable. No se abrió ni modificó un original de SGOL. No se diagnosticó el entorno más allá del preflight ni se repitió restore: los artefactos restaurados existentes bastaron.

Todos los comandos .NET finales usaron ese SDK aislado añadiendo su directorio al PATH del proceso.

```powershell
dotnet build --no-restore --configuration Release -verbosity quiet

dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~PlanQueryApiEndpointTests|FullyQualifiedName~Front015PresentationTests|FullyQualifiedName~WorkPlanTests|FullyQualifiedName~PlanPublicationTests|FullyQualifiedName~Front014PresentationTests|FullyQualifiedName~CalendarTests|FullyQualifiedName~WeekTests|FullyQualifiedName~Front002Tests' --logger 'console;verbosity=minimal'

dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~InterfaceDesignRulesTests|FullyQualifiedName~ObligationQueryArchitectureTests|FullyQualifiedName~ArchitectureBoundaryTests' --logger 'console;verbosity=minimal'

git diff --check
git diff -- Fuentes
```

Resultados finales: build Release PASS, cero errores/advertencias; unitarias/presentación afectadas **107/107**; arquitectura afectada **13/13**; diff-check PASS; diff de Fuentes vacío. Los fallos intermedios de compilación por nombres/constructor de contrato y analizadores se corrigieron. La primera ejecución de 77 pruebas tuvo cuatro fallos de aserción de codificación de la letra acentuada en HTML, no una falla de escape; se corrigió la comprobación para verificar por separado marcado escapado y texto decodificado. La ejecución final acredita el conjunto posterior de 107 casos.

Los tests de render generaron doce fragmentos HTML sintéticos con valores ocultos limpiados en `C:/Users/siste/.codex/tmp/sgol-front015-previews`; son auxiliares externos a Git, sin capturas ni acreditación de navegador.

## Validación diferida

La sección 8 de Adenda 53 aprobada y el flujo de validación local proporcional reservan estas comprobaciones al hito solicitado, por costo acumulado; no se atribuye incompatibilidad AMD64 ni indisponibilidad de Docker:

- Siete casos nuevos de PostgreSQL/Testcontainers: los cuatro roles sin escrituras GET, historia V2/V1 y paginación, filtro antes de paginar/anti-IDOR y cursor ligado a actor/límite/variante/rol. Están compilados, no ejecutados.
- Regresiones de unicidad, concurrencia, auditoría y no efecto contra PostgreSQL real de los comandos reutilizados; su evidencia aceptada anterior se conserva, pero no se declara revalidada en esta rama.
- Recorrido API/Razor hospedado con autenticación y TLS reales; navegador escritorio/móvil, teclado, focus trap, contraste y reflow medidos. El HTML sintético no los sustituye.
- Suite completa, dotnet format --verify-no-changes, escáneres, demo integral y gates remotos. No forman parte del cierre local por tarea.

Los pendientes no equivalen a éxito. Este estado local permite continuar dependencias conforme a AGENTS.md. No se realizó push, PR, merge, despliegue ni revisión remota.

## Publicación autorizada

El responsable autorizó la publicación mediante «Apruebo su publicacion». Se publicó la rama remota `codex/front-015` en `https://github.com/SistemasTechLead/sgol`, con la implementación `b18c75133cf1a82406f95733a30f0dca4f150c10`. La rama parte de master `9b1c2f07d83b7c2370326b3e817d63a533900b4e`, coincidente con origin/master al preparar el hito.

Estado: **Publicada**. PR: el que incorpora esta actualización. Cabeza final: la resuelta por dicho PR; cualquier check debe corresponder a esa cabeza exacta. Pipeline y revisión pendientes de verificación; no se atribuye éxito a las validaciones diferidas. La publicación activa los gates de pull_request existentes sin modificar el workflow. Merge y despliegue no autorizados ni realizados. El registro anterior conserva la evidencia del cierre local previo a esta publicación.
