# Hora explícita al preparar un proceso ocasional

## Causa y alcance

Cambio mixto, 29/10/2026. Consulta de solo lectura a `sgol_local`: «Fotografías faltantes en Sizes & Colors», versión publicada 1, `StartKind=OCASIONAL`, `StartHourConfirmed=false`, habilitado. `ProcessPlanningService.PreviewCoreAsync` rechazaba cualquier versión sin hora confirmada con `PROCESS_RULES_INVALID`, antes de evaluar responsables o conteos. El editor deshabilita la hora para procesos ocasionales y permite publicarlos así: el catálogo y el planificador se contradecían.

Decisión del usuario: elegir fecha y hora al preparar cada plan. Se añade `ProcessPlanInput.OccasionTime` (HH:mm, Ciudad de México), obligatorio en `OCASION`. `CALENDARIO` conserva la hora publicada y tiene un diagnóstico específico cuando falta. Binding inválido se rechaza en ambos formularios POST, además de la validación del servicio. El campo se oculta y deshabilita en calendario; reaparece al elegir ocasión.

## Trazabilidad e invariantes

- Formulario: `Pages/Planning/Processes.cshtml`, `Processes.cshtml.cs`, `wwwroot/js/process-plan.js`, `wwwroot/css/processes.css`. Reutiliza campos, tokens, controles y mensajes existentes.
- Cálculo: `ProcessPlanningService.PreviewCoreAsync` convierte fecha y minutos locales a UTC usando `OperationalSchedule.Instant`. Los inicios efectivos participan en la huella existente y en la agenda, selección aleatoria y capacidad. El formulario de preparación conserva la hora explícita.
- Persistencia: `PrepareAsync` guarda el instante en `PreparedProcessStart.StartsAt`; no requiere migración. `ActivateAsync` pasa ese instante a `OperationalProcessService.StartAsync`, que captura `OperationalSequenceRun.PlannedStartsAt` y conserva la agenda preparada.
- Conteo: `ProcessItemRules.CaptureAsync` captura tarifas; `OperationalProcessService.Items.cs` ajusta la etapa receptora al reportar cantidades. No se modifican fórmula, tarifas, cero, devoluciones, revisión ni historial.
- Planes preparados y ejecuciones existentes se leen y activan desde sus inicios capturados. No hay actualización masiva ni cambios a responsables, reservas o evidencia existentes.
- Los fixtures que preparaban ocasiones ahora envían explícitamente la hora que antes tomaban de su versión de prueba.

## Matriz y evidencia

Pruebas aisladas: `sgol_task_checks_occasion_time_final_20261029` y `sgol_task_checks_occasion_auto_20261029`, localhost:5440. Host Razor con sesión exclusiva de prueba en 7258/7259. Sin escrituras en la simulación compartida.

| Escenario / acción | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Publicar proceso ocasional con conteo sin hora de calendario | Publicación válida | Servicios reales, suite `--process-items-only` | Aprobado |
| Vista previa sin hora, hora imposible y segundos | 422 con causa y acción comprensibles | Formularios HTTP reales, misma suite | Aprobado |
| Enviar 12:15 en ocasión | Vista previa válida y preparación con instante exacto UTC | Formularios HTTP, consultas de persistencia | Aprobado |
| Cambiar hora después de revisar | 409; sin plan parcial | POST Prepare con huella anterior | Aprobado |
| Activar y ejecutar conteos | Conserva inicio; 20×3=60, corrección 12×3=36, cero omite y conserva historial | Integración de servicios y BD, misma suite | Aprobado |
| Usar calendario sin hora publicada | Diagnóstico específico | Servicio real, misma suite | Aprobado |
| Aleatorio con manuales, incompatibles, ausencia y capacidad | Conserva manuales, excluye incompatibles; no duplica preparación | `--process-plan-auto-only` | Aprobado |
| Calendario semestral y publicación concurrente | Respeta calendario; rechaza propuesta obsoleta sin persistencia parcial | Misma suite de Aleatorio | Aprobado |
| Cambiar ocasión/calendario | Campo habilitado/oculto según modalidad | Navegador sobre Razor real aislado | Aprobado |
| Escritorio y 320 px, foco y texto explicativo | Campo legible; sin desbordamiento horizontal | Navegador real, tema claro; captura `.local/process-time-mobile.png` | Aprobado |
| Tema oscuro | Revisión de contraste con tokens existentes | No se verificó visualmente | No ejecutado |
| Viri / Aleatorio / Vale en simulación compartida | Capacidad depende de fecha/hora elegidas por el usuario | No se guardó ni activó el plan del usuario | No ejecutado |

Compilación: `dotnet build tools/Sgol.TaskChecks/Sgol.TaskChecks.csproj --artifacts-path .local/process-time-build`. Logs locales: `.local/process-time-items-final.log`, `.local/process-time-auto.log`. Se preservan los cambios previos del repositorio. Al terminar las pruebas, la aplicación compartida todavía cargaba la versión anterior; la carga autorizada se registra a continuación.


## Carga autorizada

El usuario autorizó cargar la versión el 29/10/2026. Se ejecutó .local/cargar-hora-procesos.ps1: proceso web 37336, binario .local/runtime/process-time-20261029/Web/Sgol.Web.dll. Salud local y LAN aprobadas; el recurso /js/process-plan.js servido contiene el control de hora actualizado. No se guardó ni activó el plan del usuario.
