# Mi trabajo: tareas no satisfechas al final

Cambio de interfaz y orden de consulta, 03/11/2026. `NO_SATISFECHA` es un cierre, no una solicitud de corrección. Se conserva el estado, evidencia, historial y la acción Ver evidencia. No modifica cargas, responsables, permisos ni persistencia; no necesita migración.

## Causa e implementación

`OperationalSupervisionService` guarda el resultado de revisión y completa la asignación si no tenía entrega. `OperationalTaskService.MineIncludingPendingProcessesAsync` ordenaba esos cierres con prioridad 0 antes de paginar; `MineQuery`, `OperationalWorkPlan.Priority` y `my-work-timing.js` reproducían esa prioridad. `IndexModel.OperationalColor` los pintaba urgentes y el vencimiento podía decir Vencida.

Ahora tienen prioridad 5, después de las entregadas, en consulta, presentación y temporizador. La agrupación histórica también los sitúa al final. La clase `closed` reutiliza los tokens neutros existentes en los tres temas y el texto temporal es «Cerrada · No satisfecha». Las correcciones pendientes conservan su urgencia y acciones. Los registros existentes reciben la presentación sin actualizar sus datos.

## Matriz de verificación

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Cierre con entrega, por supervisión y registro histórico sin CompletedAt | Último, neutro, texto cerrado, consulta de evidencia | `dotnet run --project tools/Sgol.TaskChecks --no-restore -- --my-work-closed`, reglas locales | Aprobado |
| Mezcla con vencidas, futuras y entregadas | Cierre después del resto y fuera del trabajo de hoy | Mismo comando, orden y agrupación | Aprobado |
| Temporizador, paso del vencimiento y del día; corrección pendiente | Cierre permanece último; corrección conserva prioridad operativa | `node tools/Sgol.MobileChecks/my-work-closed-checks.cjs`, script real con DOM simulado | Aprobado |
| Compilación Web | Sin errores ni advertencias | `dotnet build src/Sgol.Web/Sgol.Web.csproj --no-restore -v quiet` | Aprobado |
| SQL paginado, búsqueda y varias páginas | No satisfecha al final del conjunto filtrado | Consulta modificada e inspeccionada; falta ejecución con BD aislada | No ejecutado |
| Pantalla autenticada, 320 px/escritorio, tres temas, teclado y evidencia | Neutro legible, orden estable y evidencia accesible | Falta navegador sobre aplicación real; la prueba DOM no lo sustituye | No ejecutado |
| Persistencia, concurrencia y migración | Sin nuevas escrituras | Cambio exclusivo de lectura y presentación | No aplicable |

Repositorio en master con modificaciones previas conservadas, incluida `Index.Operational.cs`. No se escribió en base de datos ni se reinició/desplegó la aplicación compartida. Implementado con validación de integración y visual pendiente; no se afirma que la instancia local/LAN ya cargue este cambio.

## Despliegue autorizado

El usuario solicitó desplegar el cambio el 03/11/2026. Se publicó el checkout actual en `.local/runtime/my-work-closed-20261103` y se sustituyó únicamente el proceso web en 7241, conservando el proxy LAN y el binario previo para rollback. No se ejecutaron migraciones ni herramientas de preparación de datos.

Verificado: proceso cargado desde el nuevo binario, salud interna live/ready aprobada, salud LAN aprobada, `/acceso` devuelve 200 y los recursos servidos por LAN contienen la prioridad 5 y el estilo `closed`. Evidencia local: `.local/deploy-my-work-closed-20261103/deployment.json`. La revisión visual autenticada de Mi trabajo y la prueba SQL aislada siguen pendientes; las comprobaciones de disponibilidad no las sustituyen.
