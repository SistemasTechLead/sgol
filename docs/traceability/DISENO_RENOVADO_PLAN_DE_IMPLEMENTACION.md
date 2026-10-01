# SGOL — Diseño renovado — Plan único de adaptación de pantallas existentes

## 1. Estado, autorización y evidencia de entrada

Fecha: 2026-09-30. **PLAN COMPLETO APROBADO** mediante «Si apruebo el plan completo». Autoriza implementación, validación y commits locales de este único hito; no publicación, merge ni despliegue. Este archivo es el único plan de implementación del hito. La aprobación no acredita por sí misma código renovado ni pruebas productivas.

**Decisión aprobada:** «Apruebo las secciones y la adenda» cubre §§3–9 de `docs/design/PLAN_DISENO_RENOVADO.md` y Adenda 55. Se acepta íntegramente, sin volver a someterla a aprobación. La implementación propuesta materializa esa referencia con CSS propio y variables oficiales.

**Hecho observado:** checkout `codex/diseno-renovado`, HEAD `03ae7eae690645ee1e3c51e3b7bca0278ab2c626`, árbol limpio. Se leyó primero `IMPLEMENTATION_STATUS.md`; preflight fue la única comprobación inicial del entorno. Informó `treeClean=true`, `fuentesClean=true` y `fuentesRebaselinePending=false`. También informó falta de resolución del SDK exacto 10.0.400 y disponibilidad de 10.0.401. No se diagnostica adicionalmente ni cambia `global.json`.

**Evidencia aceptada:** registros vigentes de las tareas implementadas, incluida FRONT-016. Las menciones históricas de anfitrión vacío o de historias no iniciadas no sustituyen la evidencia posterior. No se repiten gates para aprobar de nuevo esas tareas; las pruebas seleccionadas después serán regresión del cambio visual. El pipeline histórico de FRONT-016 no forma parte de este hito.

La tabla «Tareas insertadas por adenda» se consultó: no se identifica dependencia funcional o de código pendiente para adaptar las superficies existentes. TECH-FRONT-005 y FRONT-017..020 conservan su alcance y estado. Publicación, pipeline o cierre formal anterior no bloquean esta adaptación local.

## 2. Alcance, fuentes y límites

**Propuesta para aprobación:** renovar layout, variables y componentes existentes primero, y después las superficies inventariadas en §3 mediante los grupos de §5. Conservar identidad Loretta, textos funcionales y todos los datos y acciones autorizados. Un único hito con commits locales pequeños; ninguna publicación por pantalla o por historia anterior.

Fuentes operativas leídas: AGENTS.md, `INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md`, Adenda 55, `docs/design/referencia-renovada.md`, `PLAN_DISENO_RENOVADO.md`, `tokens.md`, `componentes.md`, `estados-y-mensajes.md`, `estados-de-dominio.md`, `accesibilidad.md` y `navegacion.md`.

Los IDs se localizaron primero en `docs/INDICE_IDS.md`. Se consultaron las filas canónicas de Adenda 45: 47–50 (base compartida), 59–60 (FRONT-001/002), 66–70 (FRONT-003..007), 76–80 (FRONT-008..012), 86–88 (FRONT-013..015) y 94 (FRONT-016). Se leyeron los criterios F05 relacionados; la selección aplicable por grupo consta en §5. Las composiciones y mensajes consumidores incorporados en docs/design siguen siendo autoridad; no se reconstruyen decisiones funcionales previas.

Decisiones F06 necesarias: arquitectura §8, ADR-002, ADR-005, ADR-006, ADR-009, ADR-012 y ADR-015; estrategia de pruebas §14. Determinan Razor/mejora progresiva, autorización por recurso, auditoría, idempotencia, cookies/CSRF y accesibilidad. NFR-003/004/006/007/010 se conservan. No hay cambio de API ni necesidad de volver a leer todos sus esquemas para una adaptación de presentación.

Fuera de alcance: FRONT-017..020, TECH-FRONT-005, capacidades pendientes, evidencia aportable/conclusión/decisiones nuevas, nuevas rutas, permisos, estados, KPI, consultas decorativas, bibliotecas, fuentes externas, cambios de dependencias, backend, migraciones, pipelines, reparación de historias anteriores, merge o despliegue. No se modifican Fuentes/, identidad congelada, F00–F07 congelados, AGENTS.md ni las instrucciones FRONT. No se abre la maqueta externa: la referencia operativa es autocontenida.

## 3. Inventario de superficies existentes

Inventario de código: nueve archivos Razor con `@page`, quince parciales en `Pages/Shared` incluido layout, once parciales consumidores y dos archivos de infraestructura Razor (`_ViewStart`/`_ViewImports`). Acceso reutiliza una vista para sus cinco pasos; los pasos no son cinco archivos nuevos. Las tablas consumidoras no usan todas `_DataTable`, por lo que renovar sólo ese parcial sería insuficiente.

Todas las rutas y condiciones de presentación quedan intactas; este inventario no concede acceso ni añade enlaces.

| Superficie | Archivos relativos a `src/Sgol.Web/Pages` | Contenido que se conserva |
|---|---|---|
| Shell, UI-A06/A07 | `Shared/_Layout.cshtml` | Marca, nombre/rol/expiración confirmados, logout POST, menú autorizado, activo, salto a contenido y navegación móvil modal. |
| Entrada `/` | `Entry.cshtml` | Redirección vigente y presentación del error de sesión cuando corresponde. |
| UI-A01..A05 | `Access/Index.cshtml` | `/acceso`, cambio obligatorio, enrolamiento/verificación MFA y recuperación; formularios por nextStep, preauth y visualización única. |
| UI-I01, I04..I07 | `People/Index.cshtml` | `/personas-y-accesos`: lista/alta de personas, cuentas, alta/reactivación/desactivación, roles e historia, reset MFA y bloque sensible efímero. |
| UI-I02/I03 y detalle I01 | `People/Details.cshtml` | Detalle `/personas-y-accesos/personas/{personId:guid}`, empleo/vigencia, disponibilidad binaria e historia laboral. |
| UI-C01/C04 | `Configuration/Index.cshtml` | `/configuracion`: ficha de LOR-001, releases e historia, creación de borrador y publicación motivada. |
| UI-C05..C09 | `Configuration/_TaskDefinitions.cshtml`, `_Policies.cshtml`, `_EvidenceValidation.cshtml` | Ocho TAR, versión/detalle/historia, editores cerrados, activación/elegibilidad, requisitos de evidencia y matriz de validación; secciones y errores independientes. |
| UI-C02/C03 | `Planning/Index.cshtml` | `/planificacion`: semana ISO, calendario vigente, días de release borrador y confirmación. |
| UI-G01 | `Planning/_ManualGeneration.cshtml`, `_ManualFilters.cshtml` | Seis formularios manuales aplicables, referencias/padres/reclamantes, revisión, confirmación, intención protegida y recuperación explícita. |
| UI-G02/G03/G04 | `Planning/_Assignments.cshtml` | Snapshot de asignación, historia, elegibilidad/razones, corrección motivada y carga activa. |
| UI-G05/G06 | `Planning/_Plans.cshtml` | Plan actual, publicaciones/snapshots, cursores y confirmación de publicación sin motivo nuevo. |
| UI-E01/E02/E03 | `MyWork/Index.cshtml`, `_Inbox.cshtml`, `_Notices.cshtml`, `_Obligations.cshtml`, `_WorkPagination.cshtml`, `Details.cshtml` | `/mi-trabajo` y detalle `/mi-trabajo/tareas/{obligationId:guid}`: bandeja propia, avisos, consulta separada, detalle/historia y cursores independientes. |
| Vista técnica existente de sucursal | `Branches/Details.cshtml` | `/branches/{branchCode}`: título, tabla, carga/error/vacío actuales; no se incorpora al menú, no se altera su protección o lectura. |

La vista técnica y Entry se incluyen para evitar dejar una superficie existente con tipografía o estados desalineados. No se convierten en capacidades nuevas ni modifican cómo se resuelve el shell.

## 4. Inventario compartido, decisiones de composición y carencias

| Base existente | Archivos | Adaptación propuesta |
|---|---|---|
| Variables y reglas | `wwwroot/css/tokens.css`, `components.css` | Sincronizar valores con bloque oficial, incorporar título compacto y primitivas aprobadas; consumir variables para borde/foco/área/iconos/esqueleto. Eliminar literales visuales consumidores del alcance. |
| Layout/encabezados/paneles | `_Layout.cshtml`; clases de encabezado ya presentes y secciones consumidoras | Trabajo cálido, encabezado/navegación/panel blanco, columna por `--ancho-navegacion`, ancho máximo y padding oficiales; patrón CSS compartido de cabecera/filtros/cuerpo. Extender la base existente, sin segunda biblioteca. |
| Campos | `_FormField`, `_CredentialField`, `_TextArea`, `_LocalDateField`, `_Checkbox`, `_RadioGroup` (`.cshtml`) | Nativos, etiquetas/ayudas/errores intactos, área y foco oficiales; formularios refluyen. Conservar protección de credenciales y controles mostrar/ocultar. |
| Tabla/cursor | `_DataTable.cshtml`, `_WorkPagination.cshtml` y tablas inline de consumidores | Cabecera cálida, celdas densas, caption y scope; controles Anterior/Siguiente intactos. Desplazamiento sólo de tabla. No imponer mínimo universal ni anchos nuevos por columna. |
| Estados | `_StatusBadge`, `_ProblemAlert`, `_SuccessAlert`, `_ValidationSummary`, `_EmptyState` (`.cshtml`) | Texto/icono y pares semánticos; grupos de badges con wrap, foco de errores y vacío autorizado; nunca colorear todo el panel por una tarea. |
| Diálogo/upload | `_MotivatedConfirmation.cshtml`, `_UploadPresentation.cshtml` y dialogs consumidores | Renovar presentación; Cancelar inicial, Escape, retorno y motivo contractual. Upload sigue siendo componente base: no se conecta ni habilita FRONT-017. |
| Mejora progresiva | `wwwroot/js/components.js`, `my-work.js`, `manual-generation.js`, `assignments.js`, `plans.js` | Preservar selectores/IDs y comportamiento. Cambiar sólo si la estructura renovada exige un ajuste de presentación, carga o foco y cubrirlo con prueba enfocada. |

**Decisiones propuestas de composición:** una columna por defecto; panel propio por sección funcional existente, subgrupos h3 dentro de su sección. No fusionar secciones con permisos/errores distintos. No trasladar campos a otra sección, eliminar columnas, cambiar títulos ni añadir contadores. Código y nombre podrán recibir distinta jerarquía dentro de la misma celda ya existente, conservando texto completo y asociación. Estado y bandera pueden envolver juntos con wrap, siempre separados semánticamente. Las celdas que ya contienen varios datos usan la densidad aprobada. Se conservan todas las tablas de avisos y consulta, sin colapsarlas ni convertir filas en tarjetas.

**Hecho:** existen `encabezado-pagina` y diversos contenedores específicos; falta materializar en código la composición renovada compartida de panel descrita en componentes.md. El token de título productivo aún tiene el valor anterior y no declara las primitivas renovadas. Esto es trabajo de implementación ya documentado, no una carencia de contrato.

**Carencias documentales identificadas:** ninguna para la composición acotada anterior. No se proponen mensajes ni componentes funcionales nuevos. Si una revisión de grupo revela una carencia o contradicción adicional, se presenta con fuente/sección y propuesta antes de implementar esa parte; continúa el trabajo independiente aprobado. La aprobación del plan no aprueba por anticipado una solución aún desconocida.

## 5. Grupos, orden y commits locales

Las etiquetas de grupo siguientes son organización del hito, no IDs nuevos de backlog. Cada grupo actualiza su evidencia en el informe común. Las regresiones por CSS compartido se revisan desde el primer grupo en todas las rutas, aunque su composición específica se adapte después.

| Orden | Grupo y archivos de código previstos | Criterios de conservación y validación enfocada |
|---|---|---|
| 1 | Base: ambas hojas CSS, `_Layout` y los catorce parciales compartidos restantes cuando necesiten ajuste | Tokens exactos, identidad, seis estados aplicables, landmarks, sesión/logout y modal móvil. `InterfaceDesignRulesTests`, `ComponentRenderTests`, smoke del shell y regresión dirigida de sesión. |
| 2 | Acceso y entrada: `Access/Index`, `Entry`, `Branches/Details` | Acceso fuera de shell, pasos/preauth/CSRF intactos según Adendas 41/46 y BR-D02/M01/M02; CA-005 y CP-005-P/N para sucursal. `AccessBrowserTests`, `Front002BrowserTests`, render de sucursal/entrada y revisión de estados, con secretos enmascarados. |
| 3 | Personas y accesos: `People/Index`, `Details` | CA-001/002/003/006/007 y sus CP-P/N: vigencia/historia, disponibilidad, permisos y secreto efímero intactos. Selección de `Front003..007BrowserTests`, render/componentes afectados; confirmaciones y conflicto 412. |
| 4 | Configuración: `Configuration/Index` y tres parciales | CA-005/008/011/012/017/024/027 y CP-P/N: ocho TAR, versiones/historia, catálogos cerrados, snapshot y autoridad. Selección de `Front009..012BrowserTests` y consulta de sucursal de Front008; vacío/denegación/conflicto por sección. |
| 5 | Planificación de fechas y alta: `Planning/Index`, `_ManualGeneration`, `_ManualFilters` | CA-009/010/014/015 y CP-P/N: ISO/zona, calendario/borrador, seis formularios, padres, intención y recuperación. Casos pertinentes de `Front008BrowserTests` y `Front013BrowserTests`; selects largos, reclamantes y foco. |
| 6 | Asignaciones, carga y plan: `Planning/_Assignments`, `_Plans` | CA-004/016/019/020/021 y CP-P/N: elegibilidad/jerarquía, snapshot, corrección, carga y publicación/historia. `Front014PresentationTests`, `Front015PresentationTests`; recorridos visuales dirigidos de ambas secciones usando fixture existente. |
| 7 | Mi trabajo: `MyWork/Index`, `Details` y cuatro parciales | CA-023/030 y CP-P/N: propio/alcance, estados, historia, filtros y cursores independientes; marcar leído no altera tarea. `Front016PresentationTests` y selección de `Front016BrowserTests`; regreso/foco y denegación. |
| 8 | Cierre transversal y entrega | Revisar escritorio/móvil/estados en todas las superficies, corregir sólo defectos de este hito, consolidar evidencia y capturas. Sin demo integral de capacidades pendientes. |

Un commit local coherente por grupo cuando pasen sus comprobaciones disponibles; dividir un grupo voluminoso si facilita revisión. No es necesario un commit vacío por un parcial que sólo consume CSS. Mantener el mismo hito y no abrir PR por grupo. Se conservarán los IDs/selectores/atributos de los recorridos; ajustes de pruebas obsoletas exigirán el contrato vigente, sin relajar autorización, auditoría o no-efecto.

## 6. Invariantes funcionales y de seguridad

Conservar rutas, query allowlisted y destino protegido; permisos/roles/jerarquía y reevaluación del servidor por recurso/estado. No deducir autoridad de puesto, ocultación, CSS o badge. Mantener sesión request-scoped, cookies permitidas, preauth separada, logout/invalidación y fechas operativas de Ciudad de México.

Preservar formularios, handlers, nombres/campos y valores, antiforgery/CSRF, intención idempotente, ETag/If-Match y cursores opacos. Una agrupación visual no moverá controles fuera de su formulario ni duplicará campos/id. No rotar claves, reintentar ni aceptar una versión nueva automáticamente ante 412. Recuperar una intención conserva cuerpo y clave originales.

Preservar acciones autorizadas y carga regional, lectura GET sin mutación de ejecución/aviso, historia/snapshots/auditoría y no-efecto ante rechazo. La renovación no añade llamadas de negocio, transacciones ni acceso a tablas. Revisión del diff y pruebas afectadas comprobarán estas invariantes; si un ajuste excede presentación se detiene y presenta la decisión necesaria.

Mantener los mensajes aprobados literalmente, `correlationId` seguro y asociación de errores. No renderizar respuestas crudas ni secretos. No mostrar una credencial efímera en capturas ni conservarla en evidencia; no capturar contenido real de evidencia.

## 7. Validación local proporcional y evidencia visual

**Antes de implementar:** no se ejecutan build, navegador ni gates previos para volver a aprobar tareas. La validación de este plan consiste en inventario, enlaces locales, alcance del diff y `git diff --check`.

**Durante implementación:** una compilación Release por cambio que lo justifique, pruebas enfocadas nuevas/directamente afectadas y diff-check. Comandos base:

```powershell
dotnet build --no-restore --configuration Release
dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter FullyQualifiedName~InterfaceDesignRulesTests
dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --configuration Release --filter FullyQualifiedName~ComponentRenderTests
git diff --check
```

Se seleccionarán además los casos de §5 con filtros concretos y se registrarán comandos, conteos, salida y commit evaluado. No repetir build/tests sin cambio, fallo o incertidumbre que lo justifique. Restore locked sólo una vez cuando dependencias/lock o artefactos ausentes lo hagan necesario. Sin cambiar el pin ni suponer que 10.0.401 acredita 10.0.400.

Se revisan las pruebas de arquitectura que inventarían markup/JS y las pruebas de render y navegador afectadas. Se amplían sólo para verificar riesgos reales: tokens exactos, estilos renderizados, paneles sin pérdida de datos, accesibilidad y preservación de interacción. No añadir pruebas que sólo reflejen cada regla CSS. No ejecutar suite completa, format integral, escáneres, operaciones o pipeline remoto durante cada grupo.

**Navegador enfocado de este hito:** usar el harness existente con Kestrel HTTPS/PostgreSQL desechable y cuentas sintéticas; Chromium escritorio y WebKit móvil cuando sean ejecutables. Agrupar escenarios relacionados y no repetir toda la suite por pantalla. Para asignaciones/plan, agregar recorridos enfocados al harness sólo si falta cobertura necesaria para la presentación modificada. PostgreSQL se usa para soportar el recorrido auténtico o una regresión transaccional directamente afectada; no se ejecuta toda la suite de persistencia por CSS ni se usa SQLite.

| Verificación | Evidencia y criterio |
|---|---|
| Escritorio/móvil | Vista amplia y teléfono por grupo; 320 px CSS para reflow, ambas orientaciones pertinentes. Sin overflow de página; sólo tabla desplazable, todos los datos/controles accesibles. |
| Teclado y retorno | Salto, tabulación DOM, foco sin recortes, labels/errores, navegación modal con Escape y foco contenido; Cancelar inicial y retorno al disparador o encabezado vigente. Regreso de detalle conserva filtros/cursor/foco. |
| Contraste | Pares oficiales y colores efectivos renderizados en fondos renovados, texto normal ≥4.5:1 y UI/foco ≥3:1 según contrato; deshabilitado se distingue de contenido necesario. Captura sola no prueba contraste. |
| Área mínima | Medir caja/área interactiva de controles y acciones ≥44×44 px CSS según contrato, incluidos menú, paginación, checkbox/radio y mostrar credencial. |
| Texto ampliado/zoom | Texto ampliado al 200% y zoom/reflow equivalente a 320 px CSS; sin pérdida de títulos, etiquetas, botones, errores o foco. No ocultar overflow global. |
| Movimiento reducido | Emular `prefers-reduced-motion: reduce`, comprobar esqueleto sin animación y mensaje conservado. |
| Seis estados | Matriz por superficie: normal, foco, deshabilitado, error, cargando y vacío con los No aplica documentados. Incluir vacío por filtro/historia, denegación y conflicto pertinentes sin inventar operaciones o mensajes. |
| Conservación | Recorridos positivos/negativos afectados verifican permisos, CSRF, intención/ETag/cursor y GET/no-efecto; auditoría del comando confirmado cuando el caso la exponga. No acreditar auditoría sólo por una captura. |

**Límite observado:** preflight no resuelve SDK 10.0.400. Build/tests no ejecutados todavía; no son éxito ni fallo de código. Si esa condición persiste al ejecutar, registrar `Validación diferida` con causa exacta y no declarar Implementada localmente hasta disponer de compilación y comprobaciones enfocadas requeridas. Una limitación de un motor/servicio se registra para esa comprobación y permite continuar trabajo verificable independiente. Si no hay soporte AMD64, usar `VALIDACION_DIFERIDA_POR_ARQUITECTURA`; no asumir incompatibilidad sin evidencia.

Una previsualización Razor sintética puede aportar revisión visual si el recorrido hospedado está temporalmente impedido, pero se etiqueta como previsualización y no acredita API, sesión, auditoría, transacción ni navegador hospedado/TLS. Chrome/Edge/Safari reales, versiones objetivo y cualquier comprobación no ejecutada conservarán su causa específica; Chromium/WebKit no se presentan como certificación de todos esos productos ni de WCAG integral.

## 8. Trazabilidad, capturas y estados de salida

Crear `docs/traceability/DISENO_RENOVADO_ADAPTACION.md` tras la aprobación, con registro de aprobación y matriz por grupo: superficie/archivos, referencia documentada, diseño implementado, seis estados, comandos/resultados, capturas, commit y validaciones diferidas. Actualizar `IMPLEMENTATION_STATUS.md` incrementalmente con el avance de este hito; preservar íntegra la evidencia vigente de FRONT-016 y anteriores.

Actualizar la sección de transición de `docs/design/referencia-renovada.md` sólo al existir evidencia de las superficies adaptadas; conservar procedencia y distinguir el cierre documental histórico del avance de código. No reescribir el plan documental aprobado, Adenda 55 ni criterios congelados. La referencia seguirá disponible para tareas futuras sin imponer esta adaptación completa como dependencia.

Guardar capturas sintéticas fuera de Fuentes/ y preferentemente fuera de Git, con un índice en el informe y rutas de entrega. Cubrir superficies principales por grupo en escritorio/móvil, vacío/error/confirmación cuando aporten comprensión. Enmascarar contraseña, TOTP, recuperación, secreto de enrolamiento y activación temporal antes de escribir la imagen; evitar volcados de DOM/log/red con secretos o evidencia real. No incluir motivos operativos reales, cookies, tokens, URLs firmadas ni cadenas de conexión. Revisar imágenes antes de entregarlas.

Entregar capturas de lo implementado con explicación cotidiana: para qué sirve cada pantalla, qué información conserva, cómo consultar y qué hacen sus botones autorizados. Identificar aplicación en funcionamiento o previsualización. En este hito se entregan también durante el cierre local, conforme a la solicitud actual; después de publicar se actualizan si hubo correcciones visibles.

Estados: **referencia documentada** ya aprobada; **diseño implementado** sólo por superficie con código y evidencia; **Implementada localmente** cuando cumple alcance y comprobaciones enfocadas disponibles; **Validación diferida** con causa, nunca éxito; **Publicada/Integrada** exclusivamente con evidencia remota. Una compilación o prueba enfocada compatible que falle bloquea Implementada localmente del grupo afectado.

## 9. Publicación única y puntos de aprobación

La aprobación solicitada ahora cubre §§2–8 para implementar, validar y crear commits locales dentro del único hito; no incluye push, PR, checks remotos, merge ni despliegue. Registrar literalmente la respuesta del responsable antes de modificar código.

Sólo ante autorización expresa de publicación: preparar la rama y base preservando cambios ajenos; aislar los commits de este hito respecto de FRONT-016 u otras historias. La base actual contiene trabajo anterior publicado: no incluirlo como implementación nueva ni crear PR para republicarlo. Si la base remota aún no lo contiene, establecer comparación/base coherente sin revertirlo ni condicionar el avance local a su merge; presentar cualquier decisión de integración real necesaria.

Agrupar el hito en un único PR y pipeline final; adjuntar el PR al chat. La autorización incluye corregir defectos de este hito, validar y subir correcciones al mismo PR sin reconfirmar publicación. No relajar checks ni cambiar contratos para obtener verde.

Seguir la cabeza real y su pipeline. Si el turno termina con checks pendientes, configurar o reutilizar y verificar heartbeat en este mismo chat, con PR/rama/SHA/run/plan/autorizaciones y silencio ante estado sin cambios. No crear seguimiento remoto antes de autorización ni reutilizar el de FRONT-016 como seguimiento de este hito.

Con cabeza vigente verde y requisitos previos cubiertos, solicitar aprobación expresa de merge indicando PR, SHA completo, run/evidencia y límites. No hacer merge o despliegue por autorización de publicación. La ejecución local se detiene para revisión al cerrar el hito sin pedir publicar cada grupo.

## Complemento v2 al único hito — revisión 2026-09-30

Base visual aceptada mediante «Todas las demas pantallas están correctas». Orden expresa de actualizar la referencia operativa; incorporada documentalmente en ESTILO_VISUAL_V2.md y Adenda 55 §5. Correcciones externas presentadas para revisión: Detalle de persona (padding, ancho y grid), ocho variantes del componente único TAR (gap selector/acción), Conflicto de plan y Resultado incierto (gap mensaje), nueve pasos de Acceso (marca centrada, versión del ensamblado, Delicias visible). No son nuevas historias o rutas.

Una vez revisadas y aprobadas las correcciones: G0, variables, fuentes locales, marca/iconos y layout compartido; G1, Acceso/Entry y personas/cuentas/roles; G2, Configuración y TAR reutilizable; G3, Planificación y plan; G4, Mi trabajo/detalle y sucursal existente. Son los mismos grupos y el mismo hito de adaptación, ampliados visualmente a la base v2 aceptada, no a capacidades pendientes.

Archivos previstos: wwwroot/css/tokens.css y components.css, fuentes/licencia y logo local, Pages/Shared/_Layout.cshtml y parciales compartidos; vistas actuales de Access, People, Configuration, Planning, MyWork, Entry y Branches sólo donde exija presentación. Mantener modelos, handlers, name/id/aria, rutas, contratos, mensajes, permisos, CSRF, If-Match, intenciones y cursores. No trasladar datos sintéticos ni enlaces externos de maquetas a producción. Versión de ensamblado mostrada sin sufijo técnico de commit. No cambiar la zona operativa.

Validar build/pruebas de presentación afectadas y navegador sintético proporcional por grupo, escritorio/móvil/seis estados, teclado/foco/retorno, contraste, 44px, texto ampliado/reflow y movimiento reducido. Reusar evidencia histórica sin reaprobar tareas. Crear commits pequeños locales; trazabilidad por grupo. Publicación/PR/pipeline final sólo por autorización expresa; merge y despliegue separados.

Pendiente antes del código: aprobación de estas vistas corregidas, como ordenó el responsable. Para publicación de The Seasons, obtener documento de derechos web verificable; prototipo privado no acredita licencia. No se declara una nueva implementación productiva en este complemento.

## Aprobación de implementación y límite vigente — 2026-09-30

Respuesta literal: «Apruebo las correcciones y el complemento del plan para implementar, ahora haz la implementacion en el repositorio siguiendo estrictamente el diseño que hicimos fuera del repositorio». Se autoriza ejecutar los grupos G0–G4 de este único hito. La misma instrucción pide revisión de capturas implementadas para dar después «mi aprobacion explicita para hacer el commit, push y PR de todas las pantallas y del documento de diseño». Esta condición más reciente prevalece: no crear nuevos commits, push ni PR antes de esa aprobación. Los commits previos permanecen intactos. No habilita FRONT-017..020, merge o despliegue.

Ejecución y concordancia documentadas en DISENO_RENOVADO_V2_IMPLEMENTACION.md. El SDK exacto se usa desde la ruta aislada previamente disponible; no se modifica global.json ni se ejecuta restore. Los límites iniciales y la secuencia histórica anteriores se conservan como evidencia de su momento.

## Autorización de publicación del único hito — 2026-09-30

«Apruebo las pantallas, ahora vamos a agregar esto al repositorio, apruebo commit, push y abrir el PR». Supera la espera de revisión anterior y autoriza commits, push y PR únicos del hito, seguimiento y correcciones del pipeline. Merge y despliegue siguen sujetos a autorización expresa propia. La licencia web de The Seasons conserva su requisito documental previo a publicación.

## Decisión de tipografía para publicación — 2026-09-30

«Autorizar Georgia como en la vista presentada y excluir The Seasons del repositorio y su historial de publicación». Georgia reemplaza la familia de marca sólo en bienvenida, con respaldo Times New Roman/serif; Poppins OFL conserva toda la operación. Se elimina @font-face y el archivo de The Seasons, también de los commits no publicados enviados. No se distribuye ningún archivo de Georgia. Esta decisión supera el requisito pendiente de derechos de The Seasons mediante su exclusión; los registros anteriores describen la secuencia histórica. No cambia alcance, contratos ni autorización de merge/despliegue.
