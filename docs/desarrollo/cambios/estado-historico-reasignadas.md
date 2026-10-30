# Etiqueta temporal de asignaciones reasignadas — 30/10/2026

Cambio de presentación con proyección de un dato existente; no modifica estados persistidos.

## Diagnóstico

Consulta de solo lectura en `sgol_local`: diez asignaciones no retiradas con vencimiento
anterior al 30/10/2026 y `ReviewStatus=REASIGNADA`. Todas conservan su entrega
(`CompletionKind=ENTREGA`), y ninguna está vinculada a un intento de proceso.
El servidor activo utiliza `.local/runtime/process-my-work-20261030/Web/Sgol.Web.dll`.

`IndexModel.OperationalTiming` convertía el color gris de REASIGNADA en «Por activar».
El temporizador cliente conserva el texto de estas filas, por lo que la contradicción
persistía aunque el inicio y el vencimiento ya hubieran pasado.

## Corrección e invariantes

`OperationalAssignmentDetails.CompletionKind` proyecta el dato de la entrega mediante
`OperationalTaskService.MineQuery`, utilizado por listados, paginación y detalle de Mi trabajo.
La etiqueta temporal de una reasignada muestra «Entregada» si existe una entrega identificada
como ENTREGA; para cierres de supervisión o históricos sin ese dato muestra «Reasignada».
La insignia de revisión conserva «Reasignada». No se restablece PENDIENTE_REVISION:
eso eliminaría visualmente una decisión posterior que sí ocurrió.

En esta primera corrección no se alteraron fechas, responsables, evidencia, orden,
capacidad, colores ni estados de reserva. El ajuste posterior de color y orden se detalla abajo.
La corrección aplica también a registros existentes. No requiere migración ni reparación de datos.

## Escenarios

| Caso | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Entrega histórica reasignada | Entregada junto al vencimiento; insignia Reasignada | Razor HTTP, base aislada `sgol_task_checks_reassigned_timing_r2_20261030` | Aprobado |
| Cierre por supervisión reasignado | Reasignada; sin afirmar que hubo entrega | Mismo recorrido HTTP | Aprobado |
| Etapa reservada | En espera; acceso al proceso sin entrega habilitada | Suite `--process-mywork-only` | Aprobado |
| Activación y entrega del proceso | Conserva sustitución de reserva y validaciones de formulario | Misma suite | Aprobado |
| Compilación | Cero errores | `dotnet build` con `--artifacts-path artifacts/reassigned-timing-build` | Aprobado |
| Navegador, temas y teclado | Texto correcto con componentes actuales | Aplicación real | No ejecutado; sin cambios estructurales ni de controles |
| Entorno compartido | Nueva versión cargada | Servidor local/LAN | Aprobado: publicación y proceso comprobados; readiness directo y liveness local/LAN |

Evidencia local: `artifacts/reassigned-timing-build.log` y
`artifacts/reassigned-timing-checks-r2.log`. La simulación compartida solo se consultó.
La primera ejecución pasó los escenarios corregidos pero falló después por dejar los dos
registros de regresión en el fixture. Se añadió su limpieza antes de continuar el recorrido;
la segunda ejecución terminó con `Process My work integration checks passed.`

## Carga autorizada en la aplicación

El usuario pidió «Cargala a la aplicacion». Se publicó en
`.local/runtime/reassigned-timing-20261030/Web` y se sustituyó únicamente el servidor
web anterior mediante `.local/cargar-estado-reasignadas.ps1`, conservando el proxy.
El registro `.local/processes.json` apunta al nuevo lanzador. Se comprobaron el proceso
del puerto 7241 y los hashes de Web y Planning contra la compilación validada.
Readiness directo devuelve 200; liveness vía localhost y LAN devuelve 200.
Readiness vía proxy devuelve 403 conforme a su restricción a monitores locales.
No se ejecutaron migraciones ni reparaciones de datos. La sesión del usuario en navegador
no se inspeccionó; debe recargar Mi trabajo para recibir la página actualizada.

## Corrección de color y orden solicitada después

El usuario aclaró que las entregas reasignadas deben ser azules y ordenarse con las otras
azules, sin importar su fecha. El ajuste inicial de texto no resolvía ese objetivo.

Para `REASIGNADA` con `CompletedAt` y `CompletionKind=ENTREGA`, se usa el color
`delivered` y prioridad 4 en `IndexModel.OperationalColor`, `OperationalWorkPlan.Priority`,
la consulta SQL paginada de `MineIncludingPendingProcessesAsync` y `MineQuery`.
`my-work-timing.js` conserva esa prioridad en cada actualización. Los cierres de
supervisión sin entrega conservan su tratamiento anterior; la insignia Reasignada,
las evidencias y las decisiones persistidas no cambian.

| Escenario | Prueba / entorno | Estado |
|---|---|---|
| Entrega reasignada azul; cierre de supervisión diferenciado | Tabla Razor HTTP, base `sgol_task_checks_reassigned_color_20261030` | Aprobado |
| Reserva gris antes de la entrega reasignada | Tabla real, consulta SQL paginada y prioridad de presentación | Aprobado |
| Temporizador conserva azul y orden cronológico del grupo azul | DOM con script real, tres actualizaciones; fixture local `.local/check-reassigned-color.cjs` | Aprobado |
| Activación, entrega, reintentos y permisos | Suite `--process-mywork-only`, misma base aislada | Aprobado |
| Compilación y publicación | `artifacts/reassigned-color-build.log`, `artifacts/reassigned-color-publish.log` | Aprobado |
| Carga local/LAN y temporizador actualizado | `.local/runtime/reassigned-color-20261030/Web`, hashes y endpoints reales | Aprobado |
| Sesión de Viri en Brave | Computer Use | No ejecutado: el control se detuvo al no poder verificar con certeza la URL de Windows |

Evidencia de integración: `artifacts/reassigned-color-checks.log`.
Se cargó mediante `.local/cargar-color-reasignadas.ps1`; readiness directo y liveness
LAN aprobados. Se comprobó además el contenido del temporizador servido por la aplicación.
No se modificaron datos operativos ni se aplicaron migraciones.
