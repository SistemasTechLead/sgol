# SGOL — Adaptación del diseño renovado

Fecha: 2026-09-30. Plan completo aprobado mediante «Si apruebo el plan completo»: [plan](DISENO_RENOVADO_PLAN_DE_IMPLEMENTACION.md). Referencia documental aceptada: §§3–9 y Adenda 55 mediante «Apruebo las secciones y la adenda».

**Implementada localmente**, con **Validación diferida** del recorrido hospedado. Un único hito; sin publicación, merge ni despliegue. FRONT-017..020 no iniciadas. Evidencia anterior, incluida FRONT-016, aceptada sin repetir sus gates de aprobación.

## Grupos y evidencia

| Grupo | Referencia documentada | Diseño implementado y evidencia local |
|---|---|---|
| Base compartida | Aprobada | Variables oficiales exactas, fondo cálido, encabezado/nav blancos, paneles, filtros y acciones; área mínima y foco. Navegación modal móvil con cierre inicial enfocado y botón sólo en estrecho. |
| Acceso, entrada y sucursal | Aprobada | Flujo y campos conservados. Alertas y estado técnico usan superficie/panel existentes. Capturas de acceso, error y esqueleto; render PASS. |
| Personas y accesos | Aprobada | Listado, cuenta, empleo, disponibilidad e historia agrupados sin perder controles. Vista poblada, vacía/error; diálogo de baja, Escape y retorno PASS en previsualización. |
| Configuración | Aprobada | Loretta, releases, TAR, activación, elegibilidad, evidencia y validación en paneles existentes. Vista con TAR seleccionado y vacío, sin modificar contratos. |
| Fechas y alta manual | Aprobada | Semana/calendario/borrador y formulario manual en grupos coherentes. Estados vacíos y formulario TAR-0008 sintético; controles deshabilitados conservados. |
| Asignaciones, carga y plan | Aprobada | Consulta, historia, elegibilidad, corrección motivada, carga, plan y publicaciones. Normal/vacío/conflicto/confirmación/resultado incierto/recuperación/denegación sintéticos. Modal del plan usa contenido/título oficiales. |
| Mi trabajo | Aprobada | Bandeja, avisos, consulta, detalle e historia conservan agrupaciones independientes. Normal/vacío/error, badges informativos y filtros conservados. |

Todos los grupos pasan compilación y comprobaciones locales enfocadas. La implementación corresponde al código productivo; las capturas y mediciones corresponden a Razor con estados sintéticos. No acreditan consulta real autenticada ni mutaciones hospedadas.

## Archivos afectados

- `src/Sgol.Web/wwwroot/css/tokens.css` y `components.css`: root oficial, primitivas y composición compartida; ningún paquete CSS nuevo.
- `src/Sgol.Web/Pages/Shared/_Layout.cshtml`: foco inicial del cierre de navegación móvil.
- `Pages/Access/Index.cshtml`, `Entry.cshtml`, `Branches/Details.cshtml`.
- `Pages/People/Index.cshtml`, `Details.cshtml`.
- `Pages/Configuration/Index.cshtml`, `_TaskDefinitions.cshtml`, `_Policies.cshtml`, `_EvidenceValidation.cshtml`.
- `Pages/Planning/Index.cshtml`, `_ManualGeneration.cshtml`, `_Assignments.cshtml`, `_Plans.cshtml`.
- `Pages/MyWork/Index.cshtml`, `Details.cshtml`, `_Inbox.cshtml`, `_Notices.cshtml`, `_Obligations.cshtml`.
- `tests/Sgol.ArchitectureTests/InterfaceDesignRulesTests.cs`: igualdad de nombres y valores oficiales.
- `tests/Sgol.UnitTests/RenewedDesignPreviewTests.cs`: 16 estados sintéticos de nueve páginas y layout real; exportación opcional con valores ocultos sanitizados.
- `tests/Sgol.FrontendBrowserTests/RenewedDesignBrowserTests.cs`: recorrido transversal autenticado, medidas, tablas, foco y no-efecto preparado para el harness existente.
- `tests/Sgol.FrontendBrowserTests/BrowserFixture.cs`: diagnóstico opcional limitado a nombres de etapas, sin credenciales, URLs ni contenido de evidencia.
- Plan, este informe, `IMPLEMENTATION_STATUS.md` y [capturas](DISENO_RENOVADO_CAPTURAS.md).

Las rutas de páginas de esta lista parten de `src/Sgol.Web/`. Sin cambios productivos de C#, JS, endpoints, dominio, permisos, sesión, contratos, mensajes, auditoría, CSRF, idempotencia, If-Match o cursores. Comparación estática de 18 vistas contra `03ae7eae690645ee1e3c51e3b7bca0278ab2c626`: **513 tags de controles/formularios/enlaces/partials idénticos**, ignorando sólo clases y espacios. Layout añade sólo `data-initial-focus`. Esta comparación acredita conservación de marcado, no comportamiento de servidor.

## Validación ejecutada

Preflight fue la única comprobación inicial de entorno. La resolución predeterminada no encuentra 10.0.400; se usó el SDK aislado ya documentado por FRONT-016: `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe`, sólo mediante PATH del proceso. Sin restore, instalaciones, cambios de global.json o dependencias.

| Comando o comprobación | Resultado y límite |
|---|---|
| `dotnet build --no-restore --configuration Release` | PASS, 0 errores/advertencias. Último build después de la corrección de modal y estados sintéticos. |
| Unitarias con `--no-build --configuration Release`, filtro `ComponentRenderTests` / `Front013IntentionTests` / `Front014PresentationTests` / `Front015PresentationTests` / `Front016PresentationTests` / `RenewedDesignPreviewTests` | 109/109 PASS. Render/presentación e intenciones afectados; no equivale a integración PostgreSQL. |
| Arquitectura con `--no-build --configuration Release`, filtro `InterfaceDesignRulesTests` / `Front016ArchitectureTests` | 10/10 PASS; root exacto, reglas de diseño y límites UI. |
| Playwright disponible, Chromium y WebKit | 198 combinaciones: 33 vistas/estados × dos motores × 1440/390/320 px. Sin overflow de página normal o texto efectivo al 200 %; tablas semánticas, caption/scope, IDs únicos y objetivos visibles ≥44×44. Contraste efectivo de badges/alertas/botones/controles/títulos PASS; esqueleto sin animación con movimiento reducido. |
| Teclado sintético | 34 casos en ambos motores a 1440/390/844, incluido teléfono horizontal. Apertura preparada, Cancelar/cierre inicial, Tab, Escape y retorno PASS. Ningún control exterior recibe foco mientras el dialog es modal; se admite el paso nativo a la interfaz del navegador, cuyo activeElement es body. |
| Pares oficiales de contraste | 11/11 PASS. Mínimo semántico de texto 4.587:1; borde fuerte/blanco 3.042:1; foco de acento/fondo cálido 4.969:1. Deshabilitados no se cuentan como texto operativo exigible. |
| Revisión de imágenes | Acceso móvil, persona escritorio, configuración, planificación, bandeja móvil, asignaciones, confirmación y navegación. Datos/textos sintéticos; no se copió evidencia real. |
| `git diff --check` | PASS. Fuentes y congelados sin cambios respecto de la base. |

Los filtros unitarios se combinan mediante `FullyQualifiedName~<clase>` y `|` en `dotnet test tests/Sgol.UnitTests`; arquitectura usa `tests/Sgol.ArchitectureTests`. Resultados y comandos externos se conservaron bajo `C:/Users/siste/.codex/tmp/sgol-diseno-renovado-results/`.

La revisión corrigió WebKit propagando el texto ampliado del select de responsable: recorte del campo ya aprobado, conservando `--espacio-4` para foco. El botón móvil aparecía en escritorio por especificidad de `.boton`; se corrigió la prioridad del selector. También se incorporó padding/contenido oficial al modal del plan. Sólo se repitieron comprobaciones por correcciones o nuevos estados de evidencia.

## Estados y conservación

Normal: persona/tarea pobladas, configuración seleccionada, formulario manual, asignaciones/carga/plan. Foco: salto/main, navegación y confirmaciones. Deshabilitado: controles existentes de reclamantes y publicación sin datos. Error: acceso, recurso no disponible, conflicto/denegación del plan y asignación. Cargando: esqueleto de sucursal y reglas de progreso existentes. Vacío: personas, catálogo, calendario, bandeja, avisos, consulta, historia, carga y publicaciones. Las páginas servidas tras responder conservan progreso de formulario; no se añadió carga asíncrona artificial.

Recuperación, rechazo y no-efecto de intenciones se verificaron en pruebas enfocadas existentes sobre la presentación afectada. No se repitieron gates históricos para volver a aprobar FRONT-016. No se ejecutaron comandos de negocio para completar las capturas sintéticas.

## Validaciones diferidas y límites exactos

**Recorrido hospedado HTTPS/PostgreSQL:** el intento agrupado terminó por inactividad; el diagnóstico de los dos casos nuevos terminó con timeout antes de Kestrel. Etapas confirmadas: PostgreSQL listo, migraciones, identidades sintéticas y certificado temporal creado. El bloque de acceso/incorporación al almacén Root del usuario Windows no terminó; no existe HOST_READY. No se atribuye a incompatibilidad AMD64 ni falta de Docker. No se relajó TLS. Quedan diferidos los asserts autenticados, snapshots de no-efecto, auditoría y capturas del recorrido real de este hito. **No son PASS**. Se retiró después el timeout interno para evitar que StartAsync continuara detrás de Dispose; próximos intentos deben resolver esa espera y usar el timeout del runner.

Tecnología asistiva real y zoom mediante interfaz del navegador: no ejecutados; se comprobaron DOM, teclado, texto efectivo al 200 % y viewport 320 como reflow. No se declara certificación WCAG integral. Suite completa, formato integral, escáneres y pipeline final reservados para integración/publicación autorizada. No hay pipeline autorizado ni heartbeat de este hito que mantener.

Diagnóstico externo: `C:/Users/siste/.codex/tmp/sgol-diseno-renovado-results/renewed-stages.trx` y `C:/Users/siste/.codex/tmp/sgol-diseno-renovado-capturas/mobile-stages.log`; sin logs de backend ni volcados incorporados. Scripts y HTML sanitizado: `C:/Users/siste/.codex/tmp/sgol-diseno-renovado-previews/` (`capture.cjs`, `interactions.cjs`, `contrast.cjs`), ejecutados con Node disponible en `C:/Users/siste/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe`. Medidas seguras junto a las capturas entregadas.

## Cierre local

Commits de grupos: `f295e06`, `913481f`, `34c83d8`, `70fa307`, `86b7c26`, `51a03e8`, `732391f`; correcciones visuales `a6155a6`; pruebas `d6c3478`. Todos en `codex/diseno-renovado`; el cierre documental conserva el mismo hito. La base contiene trabajo previo aceptado; no se republicó.

Se entrega para revisión local. Publicar requiere orden expresa y agrupará este hito en un único PR/pipeline. Esa orden habilitará seguimiento y correcciones al mismo PR; merge y despliegue requieren autorización propia. La adaptación no impone migración completa como dependencia de FRONT-017..020.
