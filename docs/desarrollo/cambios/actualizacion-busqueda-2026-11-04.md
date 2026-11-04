# Actualización global: capacidad histórica y búsqueda

Estado: publicado en la app compartida el 04/11/2026. Cambio funcional con ajustes de texto en la interfaz existente; sin migración propia, commit ni push. No se aplicó la actualización a las asignaciones compartidas: se conserva la revisión y confirmación del usuario.

## Problema y decisión

La captura se reprodujo en una copia de la simulación. El buscador incorporaba 137 bloques, incluidos vecinos sin cambios de catálogo. El cálculo operativo cargaba las vencidas sobre hoy: Dennis acumulaba 255 minutos frente a 240 disponibles, incluidos 105 minutos vencidos. El límite ocultaba esa combinación bajo «Actualización general»; los intentos fallidos podían además quedar representados como movimientos de la vista previa.

El usuario aprobó explícitamente el 04/11/2026 excluir del cálculo de actualización las vencidas que se conservan sin reprogramar, manteniéndolas pendientes y con fechas e historial intactos. No se extiende la excepción a compromisos no vencidos, futuros ni a vencidas que se reprograman. No se modificó la carga de las consultas operativas.

## Implementación y recorrido

`AssignmentRefreshService.BuildAsync`: catálogo → diferencias de snapshots → adopción propuesta → vecinos y reservas → capacidad de la agenda → vista previa. Excluye de demanda las reservas existentes con `CompletedAt == null`, `DueAt <= now` y que no se liberan para reprogramar o retirar. Conserva exclusiones al construir el ledger. Informa el número de vencidas conservadas e incorpora el conjunto a la huella (algoritmo 5).

`Choices` parte de copias estables y restablece el candidato al cambiar de responsable. `Search` descarta ubicaciones equivalentes. Mantiene el orden de preferencia y el límite original de 20 000 pasos; no se incorporaron aumentos de presupuesto ni las estrategias más amplias exploradas durante el diagnóstico. Si falla o se interrumpe, restaura todos los candidatos; el diagnóstico de límite identifica tarea, responsable y fecha como punto de interrupción, no como causa demostrada de imposibilidad.

`ApplyAsync` conserva autorización, bloqueo, huella, transacción, auditoría e idempotencia existentes. Ejecución y revisión siguen consumiendo las asignaciones persistidas. No se alteran entregas, cierres, evidencia, bono ni reglas de procesos para forzar una solución.

`RefreshCaseText` reemplaza la instrucción sin contexto de reducir conflictos. La notificación utiliza los estilos existentes de `Refresh.cshtml`; no hay nuevos controles, CSS ni rutas.

## Validación

Base de reproducción: `sgol_task_checks_refresh_search_20261104`, restaurada desde un pg_dump de lectura de `sgol_local`. Suites sintéticas en bases `sgol_task_checks_refresh_*` distintas. Evidencias privadas: `.local/refresh-search-20261104/`.

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Captura con algoritmo anterior | Reproducir límite sin aplicar | Copia, baseline.log y baseline-morning.log | Aprobado |
| Mismos datos con corrección | Completar cálculo conservando decisiones | Copia, fixed.log: 0,55 s, sin conflicto de búsqueda | Aprobado |
| Opciones explícitas, confirmar y reintentar | Aplicación e idempotencia | Formulario Razor real, copia; replay-apply.log, 33 aserciones | Aprobado |
| Datos ajenos a propuesta, entregas y vencidas conservadas | Igualdad completa antes/después | Comparación JSON en replay | Aprobado |
| Frontera de vencimiento, evidencia y avance históricos | Excluir sin escribir; conservar evidencia | RefreshOverdueCapacityChecks, overdue5.log, 10 aserciones | Aprobado |
| Capacidad real, futuros y vencida reprogramada | Bloquear sin eximir demanda nueva | Misma suite | Aprobado |
| Fallo de búsqueda y concurrencia | Sin movimientos residuales ni escrituras; propuesta antigua rechazada | Misma suite | Aprobado |
| Actualización global y metadatos | Conservar agenda y decisiones | GlobalAssignmentRefreshChecks, 24 aserciones | Aprobado |
| Vencidas modificadas | Mantener excepción histórica y protecciones | ExpiredAssignmentRefreshChecks, 15 aserciones | Aprobado |
| Recurrencia, plazos y proceso activo | Confirmaciones y snapshots coherentes | AssignmentRefreshChecks, 37 aserciones | Aprobado |
| Pertenencia y reservas de procesos | Dependencias e historial preservados | ProcessAssignmentRefreshChecks, formulario real, 30 aserciones | Aprobado |
| CSRF y rol colaborador | Rechazar petición sin token y actor sin permiso | Replay HTTP | Aprobado |
| 320/768/1440 px, claro/oscuro/especial, teclado y detalles | Sin desbordamiento ni error JS | Chromium/Edge y WebKit, 18 casos de pantalla real aislada | Aprobado |
| Compilación Web y herramientas | Sin errores ni advertencias | Build y publish | Aprobado |
| Despliegue | Web nueva, proxy intacto, HTTP disponible | Readiness interno y acceso local/LAN 200, TLS normal | Aprobado |
| Asignaciones compartidas al publicar | Sin modificaciones | 789 filas con huella íntegra idéntica | Aprobado |
| Confirmación autenticada sobre host compartido | Usuario revisa y confirma su propuesta | No se aplicó sobre la simulación compartida | No ejecutado: se conserva confirmación operativa |
| Migración nueva | No necesaria | Sin cambio de esquema | No aplicable |

Se corrigió una fixture preexistente de `AssignmentRefreshChecks`: registraba estado APROBADA sin resultado reconocido. La prueba falló también con el binario anterior (baseline-assignment.log); ahora crea `CarryoverResult` y vuelve a validar el consentimiento de plazo. Los intentos iniciales de la nueva fixture se corrigieron por tipo de adjunto no permitido, datos ajenos de una copia y revisión de bono repetida. Los resultados finales anteriores corresponden a ejecuciones completas, no a esos intentos fallidos.

149 aserciones dirigidas de integración/recorrido y 18 escenarios de navegador aprobados. Los navegadores usaron sesión sintética exclusivamente en el host aislado; no equivalen a autenticación real del host compartido. Capturas inspeccionadas y registro en browser-results.json. El shell inicial no resolvía Playwright; se usó NODE_PATH del runtime suministrado, sin instalar dependencias del producto.

## Publicación y documentos

Recorrido consultado: Inicio → infraestructura → instalación local → base documental; acceso LAN. Procesos y listeners verificados antes de actuar. Web anterior: monthly-carryover-20261103, lanzador 26764/hijo 28192. Nueva: `.local/runtime/refresh-search-20261104/Sgol.Web.dll`, lanzador 20984/hijo 24192, puerto 127.0.0.1:7241. Proxy 20652/lanzador 23392 conservados en localhost:7240 y LAN 192.168.1.92:7440/7443. Publicación anterior disponible para recuperación. Huella SHA256 de la nueva Web: A5FA24FAE2CD8B617E45A087B0698F1A9C022F92DEDBCDF7C459E370319BB01A.

Actualizadas las notas existentes de la bóveda: actualización de asignaciones, patrones y estados, instalación local y base documental. Índice Graphify actualizado; sin nuevas notas ni modificaciones de estructura de mapas/inventario. La copia versionada conserva su estado anterior hasta preparación de un commit documental, conforme al procedimiento; no se sincronizó masivamente.

Los cambios previos de comparación de conflictos, arrastres mensuales y otras pantallas estaban en el árbol al comenzar y se conservaron. La copia inicial del servicio en `.local/refresh-search-20261104/AssignmentRefreshService.before.cs` permite separar esta corrección del diff previo.
