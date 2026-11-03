# Conflictos de pertenencia al publicar un proceso

## Alcance y diagnóstico

Cambio mixto: diagnóstico servidor y presentación del rechazo al publicar en `/tareas/procesos`. El mensaje anterior reunía pertenencia, actividad y unicidad sin identificar la tarea. También ocultaba miembros del proceso ausentes del borrador.

Consulta de solo lectura de la simulación el 03/11/2026: el borrador de «Recepcion de mercancia» tiene nueve etapas; TAR-0168 (etapa 6) y TAR-0405 (etapa 9) están inactivas. La captura del usuario tenía siete etapas; el estado consultado es posterior. No se activaron, retiraron ni editaron tareas de la simulación.

Rama `master`, con cambios previos conservados, incluidos Markdown en `Processes.cshtml` y ajustes del fixture en `ProcessEditorChecks.cs`. Runtime compartido observado: `.local/runtime/my-work-closed-20261103/`, separado del binario de pruebas. Este cambio no se desplegó allí.

## Implementación e invariantes

- `OperationalProcessCatalogService.PublishAsync` diagnostica dentro de la transacción y del bloqueo de catálogo, después de autorización y revisión de concurrencia, antes de escribir snapshots.
- `ProcessMembershipConflict.cs` identifica inactividad, eliminación/ausencia, pertenencia incorrecta, repetición y ausencia en el orden; entrega todos los conflictos con ID, código, nombre y acción sugerida.
- `ProcessesModel.Write` conserva esos detalles del intento rechazado. Razor muestra un resumen accesible y borde rojo, icono y explicación en cada tarjeta afectada. Una tarea ausente del orden aparece en el resumen, pues no existe tarjeta.
- Se reutiliza `--color-peligro` en los tres temas. Los mensajes quedan fuera del diálogo para ser visibles sin abrirlo y no dependen exclusivamente del color.
- Se conservan reglas de aceptación, autorización, revisión, asignación e historial. No cambia esquema ni requiere migración. No se producen asignaciones al guardar/publicar. Otros errores de reglas, guardar y activar conservan su comportamiento anterior; este alcance corresponde al rechazo de pertenencia al publicar mostrado por el usuario.
- Crear/editar tareas escribe pertenencia y actividad en el catálogo; guardar conserva el borrador mutable; publicar exige correspondencia completa antes de copiar los snapshots. Los consumidores de asignación, ejecución e historial siguen leyendo las mismas versiones y snapshots: no se reescriben registros históricos.

## Matriz de verificación

| Escenario / acción | Resultado esperado | Entorno y prueba | Estado |
|---|---|---|---|
| Publicar con dos tareas inactivas | Dos tarjetas y causas identificadas; 422 | Base aislada, formulario Razor real, `ProcessMembershipChecks` | Aprobado |
| Eliminar una tarea del catálogo y publicar el borrador previo | Identifica eliminación y propone retirar | Misma integración HTTP | Aprobado |
| Mover tarea a General tras guardar | Identifica pertenencia incorrecta | Misma integración HTTP | Aprobado |
| Crear miembro todavía ausente de las etapas | Identifica nombre/código y propone Añadir tarea | Misma integración HTTP | Aprobado |
| Intentos rechazados | Etapas idénticas y versión sin publicar | Comparación de filas serializadas y consulta de publicación | Aprobado |
| Corregir causas y repetir publicación | Publica y no genera asignaciones | Misma integración HTTP | Aprobado |
| Tarea duplicada | Diagnóstico de repetición | Regresión del diagnosticador en `ProcessMembershipChecks` | Aprobado |
| Dos conflictos visibles en 320/1280 px y claro/oscuro/especial | Texto legible, sin desbordamiento horizontal | Edge con Playwright, página Razor y POST reales; capturas inspeccionadas | Aprobado |
| Abrir detalles con Enter, cerrar con Escape | Diálogo abre y devuelve foco al botón | Mismo navegador y fixture | Aprobado |
| Compilar herramientas y Web | Sin errores ni advertencias | `dotnet build tools/Sgol.TaskChecks/Sgol.TaskChecks.csproj --no-restore -v quiet` | Aprobado |
| Lector de pantalla, dispositivo físico, WebKit, texto ampliado y nombres extremadamente largos | Verificar presentación y anuncios | No ejecutados en esta sesión | No ejecutado |
| Carrera concurrente, autorización por otros roles | Conservar bloqueo/revisión/autorización existentes | Inspección de ruta, sin prueba concurrente nueva | No ejecutado |
| Alterar asignaciones/historial o migrar simulación | No corresponde a este diagnóstico | No se modifican productores ni consumidores históricos | No aplicable |

Pruebas en localhost:5440, bases nuevas `sgol_task_checks_membership_20261103` y `sgol_task_checks_membership_final_20261103`. Host Razor aislado en 7258/7259; credenciales obtenidas del almacén local sin imprimirlas. Evidencia local en `.local/membership-checks-20261103/`, incluido `final-http.log` y capturas por ancho/tema. El fixture usa `SGOL_MEMBERSHIP_ONLY=1` con `--process-editor-only`; `SGOL_MEMBERSHIP_UI=1` mantiene la fase de conflicto para el navegador. `tools/Sgol.MobileChecks/process-membership-checks.cjs` recibe URL del fixture y carpeta de evidencia y requiere Playwright en NODE_PATH.

Sin commit ni push. Activar o retirar las tareas inactivas sigue siendo una decisión operativa del usuario.

## Despliegue autorizado

El usuario solicitó desplegar el 03/11/2026. Se compiló Web sin errores ni advertencias en `.local/runtime/process-membership-20261103/` y se reemplazó únicamente el proceso Web compartido, conservando proxy y dependencias. Se verificaron el binario cargado, salud local live/ready, salud LAN, acceso LAN `/acceso` HTTP 200 y CSS con el nuevo indicador en ambas direcciones. El acceso directo al backend HTTP 7241 `/acceso` respondió 500; no se presenta como acceso local validado. La dirección de usuario comprobada es HTTPS LAN 7440. Evidencia y hash en `.local/deploy-process-membership-20261103/deployment.json`. No se ejecutaron migraciones ni cambios de estado de tareas. El runtime anterior queda disponible para reversión.

## Ampliación: restantes errores del editor

Petición posterior del usuario: aplicar el mismo comportamiento a los demás conflictos. Alcance: editor `/tareas/procesos`, sus operaciones de catálogo y validaciones de formulario; no las pantallas de ejecución o planificación. Sustituye la limitación de alcance de la primera entrega.

`ProcessValidationIssue` transporta tarea y campo desde el punto de rechazo. Las reglas originales siguen siendo la autoridad: el diagnosticador se ejecuta al capturar su excepción y no permite guardar datos rechazados. Se ubican orden, acción, entrada, plazos, devoluciones, instrucciones, conteos, auditoría y límites del flujo. Los rechazos por especialización, parámetros de catálogo, criticidad, progreso y sellado conservan la tarea responsable. Crear identifica nombre/descripción; guardar, añadir, activar y restaurar incorporan contexto de tarea cuando existe. Las advertencias de planes, revisiones concurrentes y otras condiciones generales permanecen en el resumen sin inventar una tarea responsable.

El modelo Razor conserva los errores de binding únicamente en formularios que envían esos campos; publicar no marca como inválido un nombre que su formulario no envía. El resumen y las tarjetas usan texto visible; los campos localizables reciben mensaje asociado con `aria-describedby` y `aria-invalid`. En el navegador, las restricciones nativas y enlaces de conteo/devolución muestran el mismo borde y aviso fuera del diálogo. Una devolución inválida ya no fuerza la reapertura del diálogo al cerrarlo; al corregir una validación de cliente se retira su indicador. Los diagnósticos del servidor se renuevan al reenviar.

| Escenario adicional | Prueba y entorno | Resultado |
|---|---|---|
| Acción inicial incorrecta | POST Publish real, base aislada | Aprobado: tarjeta y campo Action |
| Tarea crítica sin inicio mínimo | POST Publish real, base aislada | Aprobado: tarea y MinimumStartOffsetHours |
| Calendario sin hora | POST Publish real, base aislada | Aprobado: StartHour, ninguna tarjeta culpada |
| Devolución, inicio mínimo fuera de límites, instrucciones largas, conteo y auditoría | Diagnosticador con varios errores simultáneos | Aprobado: campos y tareas correspondientes |
| Corregir causas, publicar de nuevo y comparar borrador | HTTP y comparación de etapas serializadas | Aprobado: rechazo sin cambios y reintento correcto |
| Devolución inválida, Escape y corrección | Edge, 320/1280, claro/oscuro/especial, Razor real | Fallo inicial por reapertura automática; corregido y aprobado en segunda ejecución |
| Otras rutas de añadir/restaurar, especialización y catálogo | Inspección de puntos de rechazo e integración de metadatos | No ejecutado: no se simularon individualmente todos los códigos |
| Activación con versión histórica y borrador diferente | Inspección: se diagnostica la versión publicada; tareas ausentes quedan en resumen | No ejecutado en esta ampliación |
| Permisos, CSRF y carreras de escritura | Se mantienen autorización, revisión y bloqueo existentes | No ejecutado: no hubo nueva suite específica de concurrencia/roles |

Bases aisladas nuevas `sgol_task_checks_all_conflicts_20261103` y `sgol_task_checks_all_conflicts_final_20261103`. Evidencia: `.local/membership-checks-20261103/all-conflicts-final.log` y `.local/all-conflicts-ui-final-20261103/`. Compilaciones Web y herramientas sin errores ni advertencias. Inspección visual de captura móvil oscura; pruebas automatizadas de desbordamiento y teclado en seis combinaciones. No se probaron dispositivos físicos ni lector de pantalla.

Desplegado como continuación autorizada en `.local/runtime/process-errors-20261103/`: binario cargado verificado, salud local live/ready y LAN aprobada, acceso LAN `/acceso` 200 y JavaScript actualizado comprobado. Evidencia en `.local/deploy-process-errors-20261103/deployment.json`. Se conservó la simulación; sin migración, commit ni push.
