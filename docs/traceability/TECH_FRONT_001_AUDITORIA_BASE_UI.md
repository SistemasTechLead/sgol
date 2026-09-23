# TECH-FRONT-001 — Auditoría ejecutable de la base de interfaz

## 1. Control

| Campo | Valor |
|---|---|
| Tarea | `TECH-FRONT-001` |
| Base auditada | `d311d967e925f4b9c1d1161bb86914d587d809af` (`origin/master`) |
| Rama local | `codex/tech-front-001` |
| Resultado actual | `Implementada localmente`; pendiente primera autorización de publicación |
| Alcance | Base `TECH-UI-001`: layout, tokens, CSS, parciales compartidos y `Pages/Branches/Details` |
| Exclusiones | Pantallas funcionales, rutas nuevas, API, DTO, dominio, persistencia, migraciones, Playwright, despliegue y recursos externos |

Este informe registra hechos observados y decisiones pendientes. No convierte una propuesta de componente en contrato aprobado.

## 2. Fuentes aplicadas

- `F07_ADENDA_02_TAREAS_DE_BASE_DE_INTERFAZ.md` (`TECH-UI-001`).
- `F07_ADENDA_04_CRITERIO_DE_INTERFAZ.md`.
- `F07_ADENDA_44_PLANIFICACION_CONTRACTUAL_DEFINITIVA_DEL_FRONTEND.md`.
- `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md`, fila `TECH-FRONT-001`.
- `F06_ARQUITECTURA.md`, sección 8; `F06_ESTRATEGIA_DE_PRUEBAS.md`, secciones 3, 14 y 15.
- `docs/design/tokens.md`, `componentes.md`, `estados-y-mensajes.md`, `estados-de-dominio.md` y `accesibilidad.md`.
- Inventario y registro de brechas frontend aprobados el 2026-09-22.
- Implementación y pruebas enumeradas en las secciones siguientes.

## 3. Método reproducible

1. Confirmar `HEAD`, rama, limpieza y coincidencia con el `origin/master` aceptado.
2. Ejecutar `scripts/ci/preflight.ps1` como única comprobación inicial.
3. Comparar la documentación de diseño con modelos, parciales, CSS y la página Razor existente.
4. Revisar normal, foco, deshabilitado, error, cargando y vacío sólo donde sean aplicables al componente.
5. Revisar teclado, área mínima, contraste, asociación de errores, semántica y responsive.
6. Separar una carencia contractual de un defecto contra un contrato ya aprobado.
7. Tras aprobar el paquete mínimo, ejecutar las pruebas de render/componentes, arquitectura y regresión indicadas en la sección 8.

## 4. Mapa auditable de la base existente

| Elemento | Evidencia conforme | Carencia o límite confirmado | Clasificación |
|---|---|---|---|
| `_Layout.cshtml` | `lang=es-MX`, salto a contenido, `nav` y `main`; navegación visible filtrada antes de renderizar | No contiene sesión, expiración o logout y recibe la navegación desde la página | BR-D03/BR-D15; corresponde a `TECH-FRONT-003`, no se resuelve aquí |
| `tokens.css` | Declara el mismo conjunto de variables documentado en `tokens.md` | Cualquier token nuevo exige primero contrato de diseño | Conforme para la base actual |
| `components.css` | Usa variables para colores, tipografía, espacios, radios y sombras; foco visible y área de 44 px derivada de tokens | No hay contrato responsive estrecho para layout/tabla; faltan estilos de varios componentes inventariados | BR-D13/BR-D14/BR-D16 |
| `_FormField.cshtml` | Texto/select; etiqueta; error asociado; estados disabled y loading del select | Fija `type=text`; no expresa credencial, textarea, radio, fecha, autocomplete o inputmode | BR-D01/BR-D05/BR-D14 |
| `_Checkbox.cshtml` | Normal, marcado, foco, deshabilitado y error textual asociado | Sin defecto contractual bloqueante observado en el alcance actual | Conforme para su contrato actual |
| `_DataTable.cshtml` | Caption, encabezados con `scope`, filas, esqueleto `aria-busy` y estado vacío | Sin filtros, cursor opaco, acciones por fila, foco interactivo ni comportamiento móvil contratado | BR-D13/BR-D16 |
| `_StatusBadge.cshtml` | Texto e icono visibles; no depende sólo de color | CSS sólo implementa variante `exito`; las restantes no deben añadirse sin estado consumidor aprobado | Cobertura parcial; no bloquea esta auditoría |
| `_ProblemAlert.cshtml` | Alerta semántica y `correlationId` en detalle técnico | Recibe una presentación ya traducida; el traductor exhaustivo pertenece a `TECH-FRONT-002` | Conforme al límite de esta tarea |
| `_EmptyState.cshtml` | Título, explicación opcional y acción opcional accesible | Sin defecto contractual bloqueante observado | Conforme para su contrato actual |
| `Branches/Details` | Demuestra normal, error, cargando y vacío; la autorización previa evita lectura sin sesión | Consume un servicio interno y no el cliente HTTP común, que aún no existe | Límite conocido de `TECH-UI-001`; corrección corresponde a `TECH-FRONT-002/003` |
| Contraste | Los 17 pares documentados se recalcularon y coinciden con la tabla; borde fuerte alcanza 3.04:1 | La cabecera todavía citaba WCAG 2.1 | BR-D17 corregida en esta rama |

## 5. Estados y accesibilidad

| Dimensión | Resultado de auditoría |
|---|---|
| Normal | Cubierto por los parciales existentes para texto/select, checkbox, tabla, badge, alerta y vacío |
| Foco | Cubierto para enlaces, botones, `summary`, campos y checkbox; los futuros controles requieren contrato propio |
| Deshabilitado | Cubierto para campos, checkbox y botones en CSS; no existe aún un parcial común de botón |
| Error | Cubierto para campo, checkbox y Problem Details presentado; faltan resumen de errores y mensajes de los futuros controles |
| Cargando | Cubierto para select y tabla; los futuros botones/upload requieren contrato y marcado propios |
| Vacío | Cubierto por `_EmptyState` y por `_DataTable` cuando recibe ese modelo |
| Teclado | Layout, controles nativos y salto a contenido conservan orden DOM; modal, cursor, filtros y acciones no pueden declararse cubiertos porque faltan sus contratos |
| Área mínima | Los elementos base implementados usan 44 px derivados de tokens donde son interactivos |
| Responsive | La tabla permite desplazamiento horizontal, pero no existe un contrato aprobado para tabla/navegación en viewport estrecho |
| Norma | WCAG 2.2 AA queda declarada como norma de aceptación; los pares de contraste se verificaron de nuevo |

## 6. Paquete mínimo aprobado e implementado

El responsable aprobó explícitamente este paquete durante la ejecución de `TECH-FRONT-001`. La aprobación se limitó a estas primitivas y no autorizó pantallas funcionales, publicación ni merge.

| Brecha | Decisión mínima aprobada | Resultado local |
|---|---|---|
| BR-D01 | Campo de credencial para password, TOTP y recovery code con `autocomplete`, `inputmode`, mostrar/ocultar accesible y prohibición de persistir o registrar el secreto | Modelo, parcial, mejora progresiva y prueba de render; no se creó login |
| BR-D04 | Confirmación motivada con resumen del recurso, textarea obligatorio, errores asociados, foco inicial en cancelar, trampa/retorno de foco y Escape | Contrato y `<dialog>` nativo reutilizable sin acción de dominio |
| BR-D05 | Fecha local ISO y rango, mostrando `America/Mexico_City` y sin derivar reglas de negocio de la zona del navegador | Primitiva de fecha/fecha-hora; no se creó calendario funcional |
| BR-D07 | Contrato visual de upload con progreso, cancelación y estados `PENDIENTE/LIMPIO/INFECTADO/INVALIDO/ERROR_ESCANEO`, sin mostrar URL firmada | Presentación y mensajes seguros; S3, SHA-256 y escáner siguen en la historia consumidora |
| BR-D13 | Tabla con filtros GET, acciones por fila, cursor anterior/siguiente opaco, estados disabled/loading/empty y áreas de 44×44 | Modelo/parcial de presentación; no consume endpoints ni interpreta cursores |
| BR-D14 | Textarea, grupo radio, fecha/fecha-hora, resumen de errores y aviso de éxito; grupo repetible diferido hasta su consumidor cerrado | Primitivas y pruebas de render; no se inventó esquema repetible |
| BR-D16 | Comportamiento estrecho: navegación refluye sin ocultar controles; tabla conserva semántica y lectura mediante scroll; foco visible | CSS responsive y reglas automatizadas de arquitectura |

No se propone resolver BR-D02/D03/D06/D08..D12/D15 ni brechas BR-API/BR-M/BR-N en esta tarea. Cada una continúa bloqueando sólo su consumidor.

## 7. Resolución de BR-D17

`docs/design/accesibilidad.md` declara WCAG 2.2 AA como norma de aceptación, conserva la fórmula de luminancia aplicable y exige volver a verificar tokens y componentes cuando cambien. Los 17 pares publicados se recalcularon contra sus valores actuales y coinciden con las relaciones documentadas.

## 8. Validación local ejecutada

El worktree nuevo no contenía `project.assets.json`; la primera compilación `--no-restore` terminó en `NETSDK1004` antes de compilar código. Se ejecutó una sola restauración bloqueada y después las comprobaciones enfocadas:

```powershell
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet build tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-restore --configuration Release
dotnet build tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-restore --configuration Release
dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-build --configuration Release --filter "FullyQualifiedName~ComponentRenderTests|FullyQualifiedName~BranchPageTests|FullyQualifiedName~HostSmokeTests.SharedInterfaceStyles_AreServed"
dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter "FullyQualifiedName~InterfaceDesignRulesTests"
git diff --check
```

Resultados:

- restore bloqueado: correcto;
- compilación completa: correcta, 0 errores; tres reintentos transitorios de copia por recursos del sistema terminaron correctamente;
- compilaciones dirigidas finales: correctas, 0 errores y 0 advertencias;
- render/componentes, activos compartidos y regresión de la página de sucursal: 12/12;
- arquitectura, tokens, contratos, teclado estático y contraste WCAG 2.2: 8/8;
- `git diff --check`: correcto; sólo avisos informativos de conversión LF/CRLF.

Playwright, PostgreSQL, S3-compatible y ClamAV no son proporcionales a `TECH-FRONT-001` y corresponden a tareas posteriores. No se presentan como aprobados.

## 9. Puerta de publicación

La implementación local y su validación enfocada están completas. Debe solicitarse la primera autorización antes de crear commit, hacer push o abrir PR. `TECH-FRONT-002` no puede comenzar hasta fusionar esta tarea y verificar que su commit sea ancestro de `origin/master`.
