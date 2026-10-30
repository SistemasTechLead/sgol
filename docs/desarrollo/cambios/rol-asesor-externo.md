# Sustitución del rol Administración por Asesor Externo

Fecha: 29/10/2026. Estado: implementado y aplicado a la simulación compartida; acceso LAN verificado.

## Objetivo y alcance

Cambio mixto: renombrar el rol utilizado por la cuenta Admin, crear los perfiles Vale y Dafne con las contraseñas indicadas por el usuario, eliminar la cuenta Admin mediante el mecanismo del sistema y cambiar las tareas cuyo rol requerido sea Administración a Subcoordinación con especialidad Administración. Las contraseñas no forman parte de este documento ni deben incorporarse al repositorio.

## Entorno observado

- Base consultada: `sgol_local`, PostgreSQL del contenedor `sgol-local-postgres-1`, publicado en localhost:5440. Solo se realizaron consultas.
- Aplicación observada: proceso dotnet 9296, ensamblado `.local/runtime/process-search-20261029/Web/Sgol.Web.dll`; proxy LAN también ejecutándose. La compilación del proyecto no sustituye automáticamente ese ensamblado publicado.
- Hay modificaciones previas en planificación, actualización de asignaciones, supervisión, estilos y herramientas de comprobación. No se modificaron durante esta investigación.

## Hechos comprobados

- El código interno del rol es `ADMINISTRACION`; el nombre mostrado se resuelve en `SessionPresentation.RoleName`.
- Existe una sola cuenta con ese rol activo: usuario y nombre Admin. No existen usuarios con nombre de acceso Vale ni Dafne.
- El rol tiene rango superior a Subcoordinación en `RoleHierarchy`, capacidades de supervisión, validación y corrección, y autorización de administración de personas, cuentas y roles en servicios. La pantalla de personas sigue siendo exclusiva de Dirección. Un cambio de etiqueta conserva estos permisos.
- La especialidad Administración ya existe y está activa. Viri es la única persona vinculada a ella en la consulta realizada.
- Hay tres tareas actuales con `AssignedRole = ADMINISTRACION`: registrar cargos recurrentes mensuales en Cash Planner; identificar y corregir fotografías erróneas; auditar cumplimiento de fotografías de artículos. Las dos últimas requieren actualmente Contaduría y Auditoría a Procesos respectivamente; la petición implica sustituir dichas especialidades.
- No existen asignaciones con `AssignedRole = ADMINISTRACION`, ni etapas con `Role = ADMINISTRACION`, ni versiones de planes publicadas en ese ámbito. Tampoco existen asignaciones actuales de las tres tareas anteriores.
- Existen 24 snapshots de definición de asignación con `AssignedRole = ADMINISTRACION`; pertenecen a otras tareas y responsables actuales. No deben confundirse con 24 asignaciones a Admin.
- Existe un snapshot publicado de la tercera tarea en el proceso «Articulos pendientes de fotografia». La base protege las versiones y pasos publicados mediante triggers de inmutabilidad. Actualizar solo el catálogo no actualiza esa versión.
- No existen políticas de elegibilidad, políticas de validación ni decisiones de validación con ese rol en la base consultada. Sus contratos predeterminados sí lo mencionan en código.
- `CapacityWorker.EligibleFor` utiliza la especialidad como criterio alternativo al rol: cuando hay especialidad, exige tenerla y no estar a prueba. No exige además ser Subcoordinación. Es coherente con permitir personal de otros roles que tenga Administración.

## Fuentes propietarias y recorridos

| Recorrido | Fuente principal | Implicación |
|---|---|---|
| Identidad, jerarquía y permisos | `src/Modules/Identity/Contracts/Roles.cs` | Cambiar el nombre no cambia autoridad; resolver permisos antes de crear accesos. |
| Presentación, sesión y navegación | `SessionPresentation`, `RoleScreenAccess`, `RazorSessionState` | Revisar etiqueta, valores reconocidos y acceso directo y POST. |
| Alta, baja y especialidades | `Infrastructure/Persistence/Identity/UserProfileService.cs` | Usar baja lógica con cierre de rol y empleo e invalidación de acceso; conservar referencias históricas. |
| Autenticación | `EfHostedAuthenticationService` | Hash de contraseña, estado de la cuenta y cambio inicial requerido. |
| Catálogo de tareas | `OperationalTaskService.SaveAsync` y `OperationalTask` | Cambiar rol y especialidad, incrementar revisión y auditar sin alterar carga, calendario ni requisitos de evidencia. |
| Elegibilidad y planificación | `CapacityWorker.EligibleFor`, `OperationalPlanGenerator`, `ProcessPlanningService` | Comprobar personal especializado, ausencia de asignaciones automáticas al nuevo asesor y selección compatible. |
| Copias y actualización | `AssignmentDefinition`, `AssignmentRefreshService` | Distinguir rol del responsable de rol requerido en snapshot; no replantear horarios por una sustitución nominal sin una decisión expresa. |
| Procesos publicados y ejecución | `OperationalProcessCatalogService.PublishAsync`, `OperationalProcessService` | Publicar una nueva versión válida cuando cambien las tareas; conservar versiones históricas y no deshabilitar triggers. |
| Administración por API | `EfAccountAdministrationService`, `EfPersonAdministrationService`, `EfRoleAssignmentService` | Si se reducen permisos, cambiar la autorización del servidor además del menú. |

## Decisiones confirmadas por el usuario

1. Conservar todos los permisos actuales del rol, cambiando su nombre visible a Asesor Externo. Se conserva `ADMINISTRACION` como identificador interno por compatibilidad; no se cambia la especialidad Administración ni la jerarquía.
2. Cambiar requisitos y roles conservando responsables, fechas, evidencias y auditoría. Las versiones publicadas anteriores se conservan como historial; las futuras planificaciones utilizan la nueva versión.

## Alcance de implementación

1. Resolver la definición de autoridad y su código canónico. Si cambia el código persistido, cubrir restricciones, parsers, permisos y compatibilidad de registros históricos mediante migración; no hacer un reemplazo global de la palabra Administración, que también identifica una especialidad válida.
2. Cambiar las tres tareas con servicios reales y especialidad Administración. Conservar los demás parámetros.
3. Crear una nueva versión del proceso afectado para que futuras planificaciones no recuperen el requisito anterior. Mantener historial inmutable y revisar el estado habilitado y las reservas antes de publicar.
4. Aplicar a snapshots pendientes el alcance que confirme el usuario, con comprobación de responsables y sin alterar tiempos ni evidencias inadvertidamente. Conservar autoría y auditoría.
5. Crear Vale y Dafne con rol resuelto y hash de las contraseñas proporcionadas. No otorgar especialidades, horarios ni cargas no solicitados. Comprobar autenticación sin registrar contraseñas.
6. Eliminar lógicamente Admin con `UserProfileService`, verificando referencias y acceso revocado.
7. Probar en una copia aislada antes de aplicar a la simulación compartida. Verificar después el ensamblado realmente servido y el acceso local/LAN pertinente, sin reinicios automáticos no solicitados.

## Matriz de escenarios

| Escenario y precondición | Acción y resultado esperado | Entorno y prueba | Estado |
|---|---|---|---|
| Identidad y destino efectivos | Identificar cuenta única, tareas, especialidad y snapshots sin escrituras | Simulación compartida, consultas SQL de solo lectura y lectura de código | Aprobado: resultados arriba |
| Permisos del nuevo asesor | Acceso por menú, URL, POST y servicios coincide con autoridad decidida; no heredar privilegios por omisión | Base aislada, autenticación y formularios HTTP reales | No ejecutado: pendiente de definición de permisos |
| Tres tareas vigentes | Guardar y reabrir con Subcoordinación y especialidad Administración; conservar carga, calendario y evidencia requerida | Copia aislada, servicio y formulario real | Propuesto |
| Elegibilidad con especialidad | Personal con Administración es elegible; sin ella o a prueba queda excluido; el asesor no adquiere especialidad automáticamente | Base aislada, generador y asignación directa | Propuesto |
| Proceso publicado | Nueva planificación usa requisitos corregidos; versión anterior permanece íntegra | Copia aislada, publicación y planificación reales | Propuesto |
| Snapshots anteriores, pendientes y cerrados | Aplicar alcance acordado y comparar responsables, fechas, cargas, avance, evidencia y auditoría antes/después | Copia aislada, consultas y servicios | No ejecutado: pendiente de alcance histórico |
| Vale y Dafne | Crear y autenticar los dos perfiles con credenciales indicadas; verificar rol y cambio inicial de contraseña conforme a política | Copia aislada y después simulación, servicio y acceso HTTP | Propuesto |
| Baja de Admin | Perfil deja de aparecer en lista activa, acceso y sesiones revocados, autoría histórica conservada | Copia aislada, servicio de perfiles y autenticación | Propuesto |
| Reintento, conflicto y atomicidad | Repetir no duplica usuarios ni cambios; una revisión obsoleta identifica la entidad y no produce escrituras parciales | Base aislada, servicios reales | Propuesto |
| Etiqueta y presentación | Asesor Externo visible sin truncamiento en sesión y selectores, en escritorio/móvil y temas aplicables | Aplicación real, navegador | Propuesto |
| Compilación y versión servida | Compilación aprobada y aplicación utilizada cargando el cambio | Compilación y comprobación de proceso/HTTP | No ejecutado: no hay cambios de producto |

## Resultado y evidencia ejecutada

Esta sección sustituye el estado propuesto de la matriz de investigación anterior.

| Escenario | Entorno y evidencia | Resultado |
|---|---|---|
| Compilación final | `dotnet build tools/Sgol.RoleRename/Sgol.RoleRename.csproj --no-restore --nologo -v:q -p:UseSharedCompilation=false` | Aprobado: cero errores y advertencias en la ejecución final. |
| Aplicación completa | Copias `sgol_task_checks_role_20261029` y `sgol_task_checks_role_final_20261029` restauradas desde la simulación; herramienta `Sgol.RoleRename` con servicios reales | Aprobado: 3 tareas, 1 nueva versión de proceso, 24 snapshots, 2 altas y baja lógica de Admin. |
| Reintento | Segunda ejecución sobre la copia final | Aprobado: cero tareas, procesos y snapshots adicionales; no duplica usuarios. |
| Conservación | Comparación serializada de todas las asignaciones antes/después, normalizando únicamente el rol requerido en el snapshot y la revisión técnica | Aprobado: responsables, tiempos, cargas, estado operativo y demás parámetros de las asignaciones intactos. No se escribe en tablas de evidencia. |
| Acceso por formulario real | Instancia HTTPS aislada en localhost:7252, GET/POST con cookies y CSRF de Vale y Dafne | Aprobado: contraseñas proporcionadas aceptadas, cambio inicial requerido. |
| Cambio inicial y sesión | Solo en la primera copia aislada: formulario de cambio inicial de Vale y posterior acceso a Mi trabajo | Aprobado: sesión muestra Asesor Externo. La contraseña de la simulación compartida no se cambió durante esta prueba. |
| Navegador real | Mi trabajo en copia aislada, escritorio 1440 px y móvil 320 px, menú de cuenta | Aprobado: etiqueta completa y navegación correspondiente al rol anterior. Capturas locales `.local/role-asesor-desktop.png` y `.local/role-asesor-mobile.png`. |
| Aplicación compartida | `Sgol.RoleRename --apply-local`, conexión runtime a `sgol_local` | Aprobado: mismos resultados que en las copias. Admin queda `ELIMINADA`, sin rol activo; Vale y Dafne quedan activas y con cambio inicial requerido. |
| Proceso vigente | Consulta posterior a la aplicación | Aprobado: «Articulos pendientes de fotografia» versión 2, habilitado; etapa 3 con Subcoordinación y especialidad Administración. Versión 1 histórica íntegra. |
| Snapshots actuales | Consulta `DefinitionSnapshotJson->>'AssignedRole' = 'ADMINISTRACION'` | Aprobado: cero resultados. Los otros requisitos/especialidades históricos de esas 24 copias no se sustituyen indiscriminadamente. |
| Versión servida | Publicación en `.local/runtime/asesor-externo-20261029/Web`; sustitución del proceso que escuchaba en 127.0.0.1:7241, conservando proxy y base | Aprobado: proceso 29960 comprobado con el binario publicado; registro local de proceso web actualizado. |
| Acceso LAN compartido | Formularios reales de Vale y Dafne a través del proxy HTTPS, cookies y CSRF | Aprobado: ambas contraseñas iniciales válidas, sin completar su cambio inicial. |
| Elegibilidad especializada, generación nueva, concurrencia y rollback negativo | No se cambió la regla `CapacityWorker.EligibleFor` ni las autorizaciones | No ejecutado como suite nueva. Inspección de código y persistencia real; no se asignaron tareas a Vale/Dafne ni se replanificaron pendientes. |
| Tema oscuro y teclado completo | Cambio únicamente de texto sobre componentes existentes | No ejecutado; no se modificaron estilos ni interacción. |

Se conservaron respaldo local privado y copias aisladas para reproducir la intervención. No se publicaron credenciales ni datos privados en el repositorio. Las credenciales se entregan a la herramienta mediante variables del proceso y se almacenan con el hasher existente; no se incluyen en el código. Se mantienen las políticas de cambio inicial y los permisos anteriores. No se ejecutaron commit ni push.

La herramienta realiza operaciones auditadas por etapas mediante los servicios existentes; no es una transacción global de todas las etapas. Rechaza borradores preexistentes de procesos y evita restablecer una contraseña inicial que ya haya sido sustituida. No debe usarse como una herramienta general de gestión de cuentas.
