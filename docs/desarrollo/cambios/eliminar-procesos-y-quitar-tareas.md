# Eliminar procesos y quitar tareas

Cambio mixto, 29/10/2026. Decisión del usuario: conservar el trabajo existente y devolver las tareas a General, donde recuperan su programación independiente.

## Recorrido e invariantes

- `/tareas/procesos`, `ProcessesModel`: Dirección edita; Administración consulta. Quitar en el diálogo de etapa modifica el formulario; Guardar aplica la retirada junto con los demás cambios. Publicar y activar conservan sus pasos explícitos.
- `OperationalProcessCatalogService.SaveAsync`: valida pertenencia, revisión, reglas y conteos; devuelve a General las tareas omitidas, desactiva nuevos inicios y guarda el borrador en una transacción bajo el bloqueo de asignaciones. Las posiciones y devoluciones se reenumeran en `process-rules.js`; los enlaces que apuntaban a una tarea retirada se limpian y se informa al usuario. No se cambia automáticamente el receptor de un conteo.
- `DeleteAsync`: eliminación lógica mediante `OperationalSequence.DeletedAt`, con confirmación HTTP, revisión, autorización, bloqueo, transacción y auditoría. Se oculta del catálogo y se impiden nuevas ediciones, publicaciones, activaciones e incorporaciones. General permanece reservado.
- Se conservan versiones publicadas, ejecuciones, etapas, intentos, asignaciones y evidencias. `OperationalProcessService` continúa las ejecuciones desde sus snapshots. Los snapshots pendientes antiguos se completan antes de cambiar pertenencia, siguiendo la protección existente de `AddTaskAsync`.
- `ProcessPlanningService` prepara reservas y activa mediante `StartAsync`. Las reservas de planes PREPARADO bloquean retirada/eliminación: se exige revisar y eliminar esos planes desde Planificación. No se liberan cargas silenciosamente. No afecta planes ya activados.
- Abandonar restaura las tareas publicadas si siguen activas en General; rechaza atómicamente si ya pertenecen a otro proceso, están inactivas o eliminadas. No roba tareas de otro proceso.
- `OperationalTaskService` limita el compromiso publicado a la pertenencia actual: una referencia histórica de un proceso eliminado no impide administrar una tarea que volvió a General.

## Matriz de validación

Pruebas destructivas exclusivamente en bases aisladas `sgol_task_checks_process_deletion_*_20261029`; ningún borrado de la simulación diaria.

| Escenario | Resultado esperado | Prueba y entorno | Estado |
|---|---|---|---|
| Quitar etapa, guardar y reabrir | Tarea en General, borrador sin etapa, posiciones consecutivas; nuevos inicios desactivados | HTTP Razor + PostgreSQL; navegador retirando única etapa e intermedia, devolución a la retirada y totales | Aprobado |
| Reemplazar tarea y publicar | Añadir desde General, ajustar acciones y publicar nueva versión | Servicios/HTTP aislados; comparación de reglas publicadas anteriores | Aprobado |
| Eliminar proceso publicado con historial | Desaparece del catálogo; versiones, asignaciones y evidencias idénticas | HTTP + comparación de asignaciones, ejecuciones, etapas, intentos, documentos, adjuntos y fotografías | Aprobado |
| Ejecución iniciada | Puede continuar con sus etapas originales | Motor real: completar tres etapas después de retirar, reemplazar y eliminar el catálogo | Aprobado |
| General, actor sin permiso, revisión vencida, falta de confirmación o CSRF | Rechazo sin cambios parciales | HTTP/servicios aislados | Aprobado |
| Plan preparado con reservas | Rechazo comprensible; reservas conservadas | Planificación aislada; HTTP verifica nombre y enlace al plan afectado | Aprobado |
| Abandonar tras retirar tarea | Recupera pertenencia si General; conflicto si otro proceso | Servicios aislados y comparación de pertenencia/revisión tras rechazo | Aprobado |
| Fallo de auditoría y reintento de eliminación | Rollback íntegro; reintento muestra 404 controlado | Restricción temporal de auditoría en base aislada y formulario HTTP real | Aprobado |
| Tarea creada todavía sin etapa y borrador sin publicar | Guardar no la expulsa; eliminar devuelve todos sus miembros | Servicios aislados | Aprobado |
| Escritorio y 320 px, claro/oscuro/especial, teclado/foco | Controles visibles, confirmación y retorno de foco | Navegador, pantalla Razor aislada. Verificados diálogo, vacío, guardado y expansión por teclado | Aprobado |
| Migración y compilación | Modelo alineado, columna nullable compatible con registros anteriores | Migración sobre fixtures previos, `HasPendingModelChanges` y build sin errores/advertencias | Aprobado |
| Navegador con autenticación real y acceso LAN compartido | Confirmar disponibilidad en la instancia habitual | No se desplegó ni migró la instancia compartida | No ejecutado |

## Evidencia

- `dotnet build tools/Sgol.TaskChecks --artifacts-path .local/process-deletion-build --no-restore -v minimal`: compilación correcta, cero errores y advertencias; `.local/process-deletion-build.log`.
- `--process-editor-only` en `sgol_task_checks_process_deletion_verified_20261029`: 186 comprobaciones PASS, incluidas las regresiones nuevas en `ProcessCatalogDeletionChecks.cs`, formularios Razor reales, evidencias mixtas y recorrido PDF previo. Registro `.local/process-deletion-verified-checks.log`.
- `--process-catalog-only` en `sgol_task_checks_process_deletion_catalog_final_20261029`: 122 comprobaciones PASS, incluyendo las reglas compartidas, publicación inmutable, pertenencia, permisos, revisión y abandono. Registro `.local/process-deletion-catalog-final-checks.log`. El primer intento se detuvo por `SCHEDULING_CRITICALITY_REQUIRED`: dos fixtures antiguos omitían criticidad. Se completaron con `FLEXIBLE` y la suite completa pasó sin modificar la regla del producto.
- El servidor de prueba usa sesión simulada de Dirección y base expresamente aislada, puertos 7258/7259; no equivale a autenticación real ni a la aplicación habitual.
- Capturas locales ignoradas: `artifacts/process-deletion/desktop-dark.png`, `mobile-light.png`, `quitar-tarea.png`. Se restauró la apariencia especial que tenía el navegador y su tamaño habitual.
- Verificación de la compilación final en navegador: añadir tres etapas, configurar devolución de la tercera a la segunda, retirar la segunda, comprobar índices 0/1 y posiciones 1/2, devolución vacía, conteo/carga 2/2 y foco en la etapa siguiente; guardar y reabrir. También se comprobó la retirada de la única etapa y el guardado vacío. El servidor aislado se detuvo al terminar; se conservan las bases y registros para reproducción.
- La migración `20261029215055_AddProcessCatalogDeletion` adapta el guard de publicación: comprueba pertenencia actual al publicar y activar, y permite divergencia con el catálogo de procesos desactivados. Conserva la inmutabilidad de versiones y etapas históricas. Su reversión rechaza descartar eliminaciones o retiradas ya usadas.
- La revisión del diff mantiene las modificaciones previas. La comprobación usa `git -c core.whitespace=cr-at-eol diff --check` porque varios archivos existentes usan CRLF.

Hay modificaciones previas ajenas al alcance; se conservan.

## Aplicación en el entorno compartido, 29/10/2026

El usuario autorizó expresamente aplicar la migración y cargar la versión.

- Destino verificado: PostgreSQL local en puerto 5440, base `sgol_local`; proceso web 29960 en `127.0.0.1:7241`, servido por el proxy LAN existente. No se ejecutaron inicializadores ni cambios en usuarios, tareas o asignaciones.
- Respaldo privado previo: `.local/backups/process-deletion-20261029-160141.dump`, formato PostgreSQL custom, 140127227 bytes y ACL privada.
- Migración `20261029215055_AddProcessCatalogDeletion` aplicada con `sgol_migrator`, verificando posteriormente el historial de migraciones y la columna `DeletedAt`. Cero procesos eliminados.
- Nueva versión publicada en `.local/runtime/process-deletion-20261029/Web`; SHA-256 de `Sgol.Web.dll`: `EBF5D1303A8731DA5BD3378B098DF4C88D389AC5A5BA61013C233BBD5522B55B`. Evidencia: `.local/process-deletion-publish.log` y `.local/process-deletion-migration.log`.
- Carga de versión **pendiente**: la revisión automática rechazó el comando que sustituía el proceso web con el motivo único `blocked by policy`; no se ejecutó. El proceso 29960 sigue cargando la publicación anterior y el acceso LAN continúa respondiendo `alive`.
- Se dejó `.local/cargar-version-procesos.ps1` para ejecución manual: valida binario y puerto antes de detener exclusivamente la instancia esperada, inicia la nueva con el entorno existente, actualiza el registro local y comprueba binario y salud local/LAN. Su sintaxis fue comprobada; no se ejecutó. Incluye recuperación de la versión anterior si el nuevo proceso no llega a escuchar.
