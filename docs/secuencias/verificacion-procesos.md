# Verificación de procesos y ejemplo de inventario

Registro: 13 de octubre de 2026, 17:18 -06:00. El registro distingue pruebas técnicas, revisión visual y operación humana.

## Alcance comprobado

Se reutilizan `OperationalSequence`, asignaciones y evidencias existentes. Cada tarea tiene un único `ProcessId`; General conserva la planificación independiente. Publicar captura tareas y reglas, sin generar trabajo. La activación de la definición habilita nuevos planes; guardar un plan reserva capacidad y la activación de ese plan crea las ejecuciones. Solo la etapa disponible recibe una asignación efectiva.

La integración actual admite flujos documentales PDF lineales: entrega inicial, revisiones o registro de PDF sellado y aceptación final. Los requisitos de fotografía, captura y comentario conservan su recorrido independiente en General. El sello exige un PDF nuevo; no se declara aplicado por reutilizar el documento recibido. Las condiciones se validan contra la acción anterior para impedir publicar una ruta imposible.

## Evidencias

| Comprobación | Resultado y evidencia |
| --- | --- |
| Solución completa | 0 advertencias y 0 errores: [compilación](../../outputs/procesos/final-solution-build.log). |
| Catálogo, permisos, publicación, pertenencia e inmutabilidad | [122 líneas PASS](../../outputs/procesos/verification-catalog.log), incluidas las reglas puras compartidas. |
| Plan preparado, reservas, activación e intentos | [116 líneas PASS](../../outputs/procesos/verification-plan.log), incluidas las reglas puras compartidas. |
| Regresión de tareas existentes | [359 líneas PASS](../../outputs/procesos/verification-regression.log). |
| Tablero, reportes y matriz de roles existentes | [Tablero](../../outputs/procesos/final-board-checks.log), [reportes](../../outputs/procesos/final-report-checks.log), [roles](../../outputs/procesos/final-role-checks.log). No equivalen a un nuevo informe agregado de procesos. |
| HTTP autenticado en copia recuperada | [Recorrido completo](../../outputs/procesos/final-authenticated-http-checks.log): cuatro cuentas sintéticas, CSRF, restricciones, activar plan, V1, devolución, corrección V2, revisiones y aceptación con diferencias. Se usa el escáner ClamAV configurado. MFA se atiende si el acceso lo exige; no se afirma que existiera un desafío MFA en estos accesos. |
| Diferencias tras devolución | [Reanudación](../../outputs/procesos/http-resume-return.log) y recorrido final comprueban que Piso recibe las diferencias pendientes. El fallo de presentación encontrado y corregido no se cuenta como prueba aprobada. |
| Navegador | [Plan de escritorio](../../outputs/procesos/plan-preparado-desktop.png), [390 px](../../outputs/procesos/plan-preparado-mobile.png), [editor](../../outputs/procesos/editor-desktop.png) y [plan habitual](../../outputs/procesos/plan-ejemplo-entorno-habitual.png). La pantalla estrecha es emulación, no prueba en teléfono real. |

Los conteos de pruebas comparten reglas puras: no deben sumarse como casos distintos. Las pruebas de regresión pertenecen a la versión comprobada; los cambios posteriores de presentación se verificaron mediante HTTP y navegador.

## Respaldo, recuperación y despliegue

Antes del retiro del catálogo se respaldó y ensayó el retiro en copia. Se retiraron 50 definiciones visibles identificadas por el usuario como pruebas, incluidas ocho TAR; se marcaron `Deleted=true` y `Active=false`, con revisión y auditoría. No se eliminaron asignaciones, entregas, revisiones ni archivos. [Antes y después](../../outputs/procesos/cleanup-daily-before-after.json) y [registro](../../outputs/procesos/cleanup-daily.log).

Antes de migrar se cerró el servidor habitual y se produjo [sgol-final-before-processes.dump](../../outputs/procesos/sgol-final-before-processes.dump). SHA-256: `479976FD017D330DEF37EE8CF0CC2AF01A007FCAE707A185C6EAC94CD1802FF4`. Se recuperó en `sgol_task_checks_processes_final_restore_20261013`; [prueba de recuperación](../../outputs/procesos/final-backup-recovery.log) incluye subida, bajada sin procesos usados y reaplicación, preservando los campos históricos y bytes de evidencia.

Se aplicaron al entorno diario las tres migraciones revisadas:

- `20261013193317_AddOperationalSequences`.
- `20261013221906_AddOperationalProcesses`.
- `20261013225847_AddPreparedProcessPlans`.

[SQL revisable](../../outputs/procesos/migracion-revisable.sql), [aplicación diaria](../../outputs/procesos/daily-migration.log) y [preservación anterior a crear el ejemplo](../../outputs/procesos/deployment-daily-before-after.json). Tras provisionar cuatro tareas y cuatro cuentas sintéticas, [la comparación del conjunto histórico](../../outputs/procesos/daily-preservation-after-demo.json) conserva todas las filas anteriores de once tablas, incluidas 35 asignaciones. El estado final del ejemplo contiene un plan preparado y **cero ejecuciones**.

El servidor habitual y su proxy se reiniciaron con la configuración LAN existente; `/health/ready` devolvió 200 y `ready`. Se verificó el acceso autenticado de Dirección al plan habitual desde este equipo. Esto no prueba conectividad desde otro equipo de la red.

La habilitación local se conserva como `OperationalProcessesEnabled=true` en la configuración privada, leída por `scripts/local-env.ps1`. No se publicaron cambios, PR ni merge.

## Límites que siguen abiertos

- La validación humana del recorrido y las personas, cargas y hora del piloto real siguen pendientes. El ejemplo no valida conteos reales de inventario.
- No está completo el traslado coordinado de una tarea entre dos procesos publicados. Se rechaza para proteger ambas versiones.
- La redistribución genérica no modifica asignaciones ni reservas de procesos. Faltan las acciones contextuales de reasignación con nuevo intento, cancelación motivada y ejecución sustituta vinculada; también su interfaz y pruebas. Un límite fijo vencido conserva el bloqueo, sin fabricar una entrega.
- El motor deja etapas posteriores pendientes cuando no encuentra responsable elegible. La activación inicial del plan revalida todos los responsables y puede rechazar el inicio si ya no cumplen rol o capacidad; falta completar el aviso y la resolución contextual del caso.
- Falta la vista agregada de procesos en Supervisión y Reportes, y ampliar flujos con evidencias distintas de PDF.

Estos límites impiden declarar terminado todo el plan general. La selección de proceso en Planificación y el recorrido documental de demostración están integrados y comprobados dentro del alcance descrito.

## Ajuste de navegación de Tareas

Por indicación del usuario, la cabecera queda en este orden: Crear Tarea, Crear especialización y Crear proceso. El acceso único Crear proceso abre el listado; el botón para crear uno nuevo está en la cabecera de ese listado. Se conserva el acceso de consulta de Administración y la autorización existente.

Compilación Web: 0 advertencias y 0 errores ([registro](../../outputs/procesos/tasks-header-build.log)). Verificado en el entorno habitual con sesión de Dirección: [orden de botones](../../outputs/procesos/tasks-buttons-order.png), [listado y creación superior](../../outputs/procesos/processes-list-create-top.png); ambos enlaces recorren listado y formulario sin crear registros.

## Alineación de botones de Planificación

Por indicación del usuario, Crear Nuevo Plan y Crear plan de proceso comparten una fila, en ese orden de izquierda a derecha. El contenedor permite ajustar la fila cuando falta ancho. Compilación Web con cero advertencias y errores ([registro](../../outputs/procesos/planning-buttons-build.log)); alineación comprobada en el navegador habitual ([captura](../../outputs/procesos/planning-buttons-aligned.png)).

## Compatibilidad de procesos con modo oscuro

Se corrigieron los fondos claros fijos de la etapa activa y las etapas en espera. `processes.css` utiliza exclusivamente colores de la paleta compartida para fondo, texto y borde, incluidos los bordes del editor y buscador. Así cada estado cambia con el tema, manteniendo el acento de la etapa disponible.

Verificación en navegador autenticado del entorno habitual: [seguimiento oscuro](../../outputs/procesos/processes-dark.png), [seguimiento claro](../../outputs/procesos/processes-light.png), [editor oscuro](../../outputs/procesos/editor-dark.png) y [planificación oscura](../../outputs/procesos/planning-dark.png). Se leyeron los colores calculados de 58 elementos entre etapas y controles: contraste mínimo texto/fondo 5.46:1; etapa activa oscura 12.71:1 y etapas en espera 7.75:1 ([datos](../../outputs/procesos/process-theme-colors.json)). Esto verifica los elementos muestreados; no es una auditoría completa de accesibilidad de SGOL. Se conservó la preferencia original del navegador después de comparar temas.

El plan incorpora el uso de la paleta y la revisión en ambos modos como criterio obligatorio para futuras pantallas y cambios visuales. Este ajuste es CSS y se comprobó mediante recarga; no requirió migración ni reiniciar el flujo activo.

## Reasignación a las cuentas controladas por el usuario

Registro: 13/10/2026 17:48 CDMX. El usuario había activado el ejemplo y solicitó Piso para `ana.demo` y Dirección para `direccion.local`; autorizó elevar temporalmente la capacidad de Ana. Se comprobó su carga pendiente: 25 minutos. La capacidad queda en 385 minutos diarios del 13 al 21 de octubre inclusive, para cubrir esos 25 más los 360 del inventario; después vuelve a la capacidad habitual.

`OperationalProcessService.ReassignAsync` revalida Dirección, versión de la ejecución, roles, especialización y capacidad bajo el bloqueo existente. Abre nuevos intentos para las etapas modificadas, conserva identidad del intento anterior, PDF de entrada, ronda y vencimiento, y enlaza la nueva asignación a la anterior. La anterior queda REASIGNADA/SUPERVISION según el ciclo existente, sin evento ENTREGA ni avance de etapa. La operación admite reenvío idempotente. Los responsables seleccionados se respetan al habilitarse: si no cumplen capacidad o rol, la etapa queda pendiente en vez de cambiarles automáticamente a otra cuenta.

La herramienta de ejemplo exige una operación explícita, vista previa guardada y conexión local autorizada. La aplicación automatizada se registra como SYSTEM y explica la petición humana; no simula un clic ni una entrega. Subcoordinación y Administración no se modificaron. En el entorno habitual Ana quedó disponible desde 17:47 y conserva el vencimiento de Piso a las 23:21 del 13 de octubre. Dirección sigue en espera hasta recibir la aprobación de Administración.

- [Pruebas del servicio](../../outputs/procesos/reassignment-checks.log): permisos, rollback por capacidad, nuevo intento, plazo preservado, responsables intermedios, historial, idempotencia, petición obsoleta, carga sin duplicación, caducidad de capacidad y recorrido posterior completo con devolución y aceptación V2.
- [Compilación de Web](../../outputs/procesos/controlled-assignment-web-build.log): cero advertencias y errores.
- Respaldo actualizado [sgol-before-controlled-assignment.dump](../../outputs/procesos/sgol-before-controlled-assignment.dump), SHA-256 `5607157BA7B039952CD96BCDAA88B174A5FA8D942B91CFBF60E0BAEEFACFA393`, recuperado en `sgol_task_checks_processes_deployment_reassign_20261013`.
- [Aplicación y reenvío en copia](../../outputs/procesos/controlled-assignment-copy.log), [preservación en copia](../../outputs/procesos/controlled-assignment-copy-preservation.json), [aplicación diaria](../../outputs/procesos/controlled-assignment-daily.log) y [preservación diaria](../../outputs/procesos/controlled-assignment-daily-preservation.json). Se conservan todas las filas anteriores de 17 tablas; únicamente cambian los campos autorizados de asignación, intentos, reserva y ejecución.
- [Captura del entorno habitual](../../outputs/procesos/controlled-assignment.png): Ana en Piso, Dirección · pruebas locales en Dirección e historial de reasignaciones.

El servicio de reasignación está implementado; aún falta su interfaz general y la integración con la redistribución genérica de capacidad. La validación de servicio y la lectura visual con sesión de Dirección son distintas de ejecutar el recorrido humano con las cuentas del usuario. No se cambiaron contraseñas ni se alteraron las publicaciones.
