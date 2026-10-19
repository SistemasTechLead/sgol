> Estado histórico: las políticas temporales de tarea, la rotación y la lista especial de domingos descritas aquí fueron sustituidas por la [simplificación posterior](simplificacion.md). Sus resultados y conteos anteriores no representan por sí solos el estado actual.

> Documento histórico. La política vigente exige presencia en la activación y, para recepción tardía, también al recibir. Las referencias siguientes a ventanas flexibles, fraccionamiento y espera de entrada quedaron sustituidas; no describen reglas activas. Véase [simplificación vigente](simplificacion.md).

# Horarios personales y asignación — verificación local

Fecha: 15/10/2026. Zona operativa: `America/Mexico_City`.

Implementación funcional aplicada y política activa para nuevas asignaciones. Las asignaciones anteriores se conservan. Los conflictos operativos y los límites de la verificación se detallan abajo; este registro no acredita una agenda sin solapamientos ni una validación física de dispositivos.

## Fases realizadas

| Fase | Resultado |
| --- | --- |
| 0 | Lectura del plan, referencias visuales y servicios; inventario real; identidades aclaradas por el usuario; respaldo y restauración en copia. Checkout sin `.git`; no se inicializó ni cambió de directorio de trabajo. |
| 1 | Horarios versionados, descanso y día sin configurar distintos; excepciones completas; rotación determinista, cobertura y política compartida. Validaciones de servidor y restricciones PostgreSQL. |
| 2 | Editor dentro de Personas y accesos, con guardado separado del perfil, revisión del impacto, excepciones, próximos miércoles y configuración compartida de rotación. |
| 3 | Activación por minuto, variantes por día, inicio fijo/ventana flexible, fraccionamiento explícito y margen exclusivo de cierre. Se conservan carga y plazo base. |
| 4 | Generación normal, maestro semanal, ampliación de planes, composición con capacidad, vistas previas y huellas. Escrituras revalidadas bajo el bloqueo transaccional existente. |
| 5 | Ocasionales manuales/automáticas, Supervisión, redistribución, reservas, activación y reasignación de Procesos. Se mantienen sus acciones especializadas. |
| 6 | Tres migraciones, configuración auditada de cinco personas, rotación y tareas verificadas, clasificación aprobada y activación local desde el 15/10/2026. |
| 7 | Build, verificaciones de dominio, integración, regresión, preservación y navegador. Persisten las limitaciones visuales y los conflictos de negocio indicados en este documento. |

## Configuración y uso

En **Personas y accesos → Editar**, después del perfil aparecen **Horario laboral**, **Excepciones por fecha** y **Cobertura de cierre**. El horario y la excepción tienen acciones independientes de revisión y guardado. Un envío parcial no borra días omitidos. La excepción sustituye el día completo; una indisponibilidad diaria registrada tiene prioridad.

La sección desplegable **Configurar rotación compartida de cierre** administra una sola rotación para todo el equipo. Una cobertura por fecha se registra junto con una excepción de presencia y motivo; no mueve el ciclo siguiente. Las ediciones conservan versiones, autor, fecha y auditoría y rechazan revisiones obsoletas. No modifican la identidad ni invalidan las sesiones.

En **Tareas → Editar** se configuran hora con minutos, cambios por día, política de ejecución, fraccionamiento y margen. El plazo base se conserva: para cierre el vencimiento efectivo es la salida más 60 minutos. La creación de ocasionales también permite elegir inicio fijo o ventana flexible.

En **Planificación → Asignar cargas de trabajo** se muestran fecha, tarea, persona, horario y resultado. La confirmación de guardado también permite consultar estos resultados. La vista previa queda ligada a la intención de guardado y se comprueba nuevamente dentro de la transacción. La API conserva la creación directa sin vista previa; esa ruta valida el estado actual bajo el mismo bloqueo. Cuando se aporta una huella, se exige que siga vigente.

**Mi trabajo** conserva inicio, vencimiento y carga, explica el margen de cierre y distingue una reserva flexible con ventana laboral posterior. El horario no añade un bloqueo de entrega o evidencia fuera del turno. El tablero conserva su pertenencia al día por `DueAt`.

## Identidades comprobadas y vigencia

El usuario confirmó expresamente Denisse = Dennis, Ale = Alejandra y Director = Alex. No se renombraron perfiles ni se cambiaron sus roles.

| Referencia | Usuario | PersonId | UserId | Rol conservado |
| --- | --- | --- | --- | --- |
| Denisse / Dennis | dennis | `01a13c6f-2e3a-7de7-bda5-de5218c9983c` | `01a13c6f-2e3a-7464-bdcc-8d98f92531c9` | Piso de ventas |
| Ale / Alejandra | alejandra | `01a13c6f-2d9e-735f-b73c-0cfca472228b` | `01a13c6f-2d9e-7b7c-a57b-34c00c522400` | Administración |
| Viri | viri | `01a13c6f-2dd2-715e-b334-db514f4cf726` | `01a13c6f-2dd2-7d3b-ac6c-71f7b18cdd16` | Subcoordinación |
| Mariana | mariana | `01a13c6f-2e06-7456-bf83-2937b68d64aa` | `01a13c6f-2e06-740d-9624-6b72b0b2977e` | Piso de ventas |
| Director / Alex | alex | `01a13c6f-2ccd-7b0f-add9-9d3ff77f5885` | `01a13c6f-2ccd-7e8d-8c17-747ed2794482` | Dirección |

Las cinco versiones iniciales rigen desde **15/10/2026** y reproducen los tramos acordados. Director permanece **sin configurar el domingo** y está expresamente excluido del cierre. No se cambió capacidad operativa. La rotación tiene ancla 14/10/2026 y vigencia desde la puesta en marcha; no creó asignaciones para el ancla histórica.

| Tarea | Id | Configuración aplicada | Carga / plazo base conservados |
| --- | --- | --- | --- |
| TAR-0200 · Cierre De Tienda / Poner Alarma 8:00 | `01a13c76-1cbd-7577-8ba2-131abc01f18e` | Todos los días; 19:50, domingo 13:50; inicio fijo; margen 60 min y cobertura compartida | 5 min / 1 h |
| TAR-0201 · Barrer Banqueta y Regar Arboles | `01a13c76-1ccc-7fdb-aa5d-fdee3a5d7b9e` | Conserva lunes/miércoles/viernes a las 18:00; añade domingo a las 10:00; inicio fijo | 15 min / 2 h |
| TAR-0224 · Barrer y trapear zona de piso | `01a13c77-5038-7da1-b6e8-5794cb690694` | Conserva lunes a sábado a las 09:00 y domingo a las 10:00; inicio fijo | 15 min / 8 h |

El usuario confirmó que las dos barridas corresponden a zonas diferentes y ambas se programan el domingo. El nombre original del cierre se conservó; su texto «8:00» no gobierna el calendario.

También aprobó inicio fijo para apertura y subir/bajar cortinas, ventana flexible para limpieza, exhibición, reposición, difusor y ocasionales, y fraccionamiento únicamente para la meta semanal de reposición. Quedaron clasificados los 27 registros activos comprobados. Las tareas antiguas eliminadas lógicamente no se reclasificaron en lote.

La política guarda por ID las tres tareas dominicales autorizadas. Otras ocurrencias dominicales devuelven conflicto; se conservan sus calendarios. Las ventanas flexibles de otras tareas tampoco consumen tiempo de domingo por el solo hecho de que Mariana tenga presencia.

## Rutas de asignación

| Entrada | Integración y prueba |
| --- | --- |
| Plan normal y maestro semanal | `OperationalPlanGenerator` usa el inicio de la ocurrencia y la recepción real si ya comenzó; conserva `ScheduleKey` por tarea/fecha/slot. |
| Asignar o ampliar un plan desde Supervisión | Reutiliza el mismo generador, bajo el bloqueo existente. La regresión conserva sus recorridos y atomicidad. |
| Ocasional manual y automática | Componen elegibilidad temporal y capacidad; rechazan destinatarios fuera de turno; pueden reservar una ventana flexible posterior. |
| Reasignación de Supervisión | Conserva inicio y vencimiento de trabajo fijo; valida también la recepción real y crea otra asignación conservando la anterior. |
| Redistribución por capacidad | Filtra destinos por horario y conserva en auditoría la decisión anterior y la nueva. No usa esta vía para sustituir intentos de Procesos. |
| Reserva y preparación de Proceso | Usa la política capturada en la publicación y conserva el contexto en cada reconstrucción del ledger. |
| Activación y temporizador de Proceso | Revalida la recepción real. Si una persona reservada queda ausente, conserva el Proceso pendiente y su reserva, con motivo de reintento. |
| Reasignación de Proceso | Valida política, rol, cualificación, presencia y capacidad por la acción especializada; mantiene intentos y documentos anteriores. |

`CanFit` continúa representando capacidad. `CanAssign` compone esa comprobación con la política temporal. No se añade un filtro del reloj actual dentro de la capacidad que convierta toda planificación futura en una cita del presente. Los límites de empleo y rol también acotan la ventana y los minutos reales utilizables.

Las nuevas asignaciones capturan política/revisión, versiones de horario y excepción, rotación/cobertura, ventanas efectivas y límite de cierre. Los campos nuevos de las asignaciones anteriores permanecieron nulos; no se les fabricó una decisión histórica.

## Migración, respaldo y preservación

SDK comprobado: **10.0.402**, permitido por `global.json` 10.0.400 con `latestPatch`. No se modificó `global.json`.

Migraciones:

- `20261015224854_AddPersonalSchedules`: esquema aditivo, minutos iniciales cero y políticas pendientes de clasificar.
- `20261015231000_PersonalScheduleIntegrity`: intervalos, vigencias, exclusión de solapamientos, autores, unicidad y protección de versiones históricas.
- `20261015232257_ConfirmSundayOperations`: catálogo explícito por ID de las operaciones dominicales autorizadas, separado de la clasificación temporal.

El [SQL revisable](../../outputs/horarios/migration.sql) no contiene identidades de esta instalación. La configuración de negocio se aplicó mediante una operación aparte, con previsualización, bloqueo, comprobaciones de identidad, auditoría y reejecución idempotente.

Respaldos locales: `outputs/horarios/before.dump` y `outputs/horarios/predeploy.dump`. Su integridad está en [backup-integrity.json](../../outputs/horarios/backup-integrity.json). Contienen la base completa y se mantienen como archivos de recuperación locales.

Se restauraron respaldos en bases dedicadas `sgol_task_checks_horarios_*`. Las huellas se calculan sin imprimir contenidos de cuentas, credenciales o archivos.

- [Preservación del esquema local](../../outputs/horarios/local-schema-preservation.json): las tablas previas conservaron sus filas y huellas, salvo el registro de migraciones que incorporó las nuevas entradas.
- [Preservación durante configuración](../../outputs/horarios/configuration-preservation.json): cuentas, personas, roles, empleos, planes, asignaciones, entregas, revisiones y demás tablas ajenas a la configuración conservaron sus huellas.
- [Propiedades del catálogo](../../outputs/horarios/task-properties-preservation.json): se conservaron requisitos, carga, plazos base, procesos, especializaciones y demás propiedades anteriores, excluyendo solamente revisiones y los calendarios autorizados de las tres tareas.
- [Idempotencia de configuración](../../outputs/horarios/configuration-idempotency.json): la repetición no añadió filas ni alteró huellas.
- [Inventario final](../../outputs/horarios/local-final-inventory.json): permite comprobar los conteos finales. Se conservaron **73 asignaciones**, **73 fragmentos de entrega** y **73 fragmentos de revisión**. La base real inventariada no contenía fotografías ni adjuntos en esas tablas.
- [Comparación final del historial](../../outputs/horarios/final-history-preservation.json): confirma también la preservación después de completar la política dominical y reiniciar el Web.

## Evidencia automatizada

| Verificación | Resultado y evidencia |
| --- | --- |
| Solución | Compilación sin errores ni advertencias; [solution-build.log](../../outputs/horarios/solution-build.log). |
| Web desplegado | Compilación sin errores ni advertencias; [live-build.log](../../outputs/horarios/live-build.log). |
| Modelo EF | Sin cambios pendientes respecto al snapshot; [model-check.log](../../outputs/horarios/model-check.log). |
| Horarios y rotación | 47 comprobaciones dedicadas; [calendar-checks.log](../../outputs/horarios/calendar-checks.log). |
| Integración con filtro activo | 18 casos: planes normal/semanal, idempotencia, vistas obsoletas, ocasionales manuales/automáticas, recepción tardía, Supervisión, redistribución y Procesos; [route-checks.log](../../outputs/horarios/route-checks.log). |
| Regresión de tareas | 381 comprobaciones emitidas por la suite; [task-regression.log](../../outputs/horarios/task-regression.log). Incluye recuperación de esquema en base aislada. |
| Regresión de Procesos | 137 comprobaciones emitidas por la suite; [process-regression.log](../../outputs/horarios/process-regression.log). Incluye entrega desde Mi trabajo, reasignación, versiones y reintentos. |
| Personas y formularios HTTP | 68 comprobaciones; [profile-checks.log](../../outputs/horarios/profile-checks.log). Incluye CSRF, autorización, campos deshabilitados omitidos, revisión/guardado, doble envío y conservación de sesiones. |
| Tablero | 100 comprobaciones; [board-checks.log](../../outputs/horarios/board-checks.log). Se conserva la pertenencia por vencimiento. |
| Acceso por rol | Cinco grupos de verificación; [role-checks.log](../../outputs/horarios/role-checks.log). |
| Simulación real sin generar trabajo | [local-simulation.log](../../outputs/horarios/local-simulation.log): domingo, rotación, año nuevo, roles y conflictos de asignaciones existentes. |

Los conteos de suites se solapan en comprobaciones compartidas; no deben sumarse como casos independientes de aceptación.

Los checks de dominio cubren entradas/salidas exclusivas, comidas, descanso del miércoles, ventana flexible desde comida o medianoche, rotación negativa y entre años, sustitución sin mover el ciclo, ausencia con precedencia, límites 21:00/15:00, carga agregada de cierre y fin de empleo. La presencia de ocho horas no aumenta los 240 minutos de capacidad diaria de la fixture.

## Navegador y diseño

Se utilizó una instancia HTTPS aislada con copia de la base. Su sesión de prueba solo existe en esa herramienta, que rechaza ejecutarse como servidor de prueba sobre `sgol_local`. No se cambiaron cuentas reales para iniciar pruebas ni se creó trabajo de prueba en la base compartida.

Se comprobaron **Personas, Tareas, previsualización de planificación y detalle de Mi trabajo**, a **320, 390, 768 y 1280 px**, en claro y oscuro. Las mediciones y capturas están en `outputs/horarios/`:

- [Personas claro](../../outputs/horarios/visual-light.json) y [oscuro](../../outputs/horarios/visual-dark.json).
- [Tareas claro](../../outputs/horarios/task-visual-light.json) y [oscuro](../../outputs/horarios/task-dark.json).
- [Planificación claro](../../outputs/horarios/planning-light.json) y [oscuro](../../outputs/horarios/planning-dark.json).
- [Mi trabajo claro](../../outputs/horarios/work-light.json) y [oscuro](../../outputs/horarios/work-dark.json).
- [Preferencia móvil en escritorio](../../outputs/horarios/mobile-saved-1280.png) y [rotación compartida a 320 px](../../outputs/horarios/rotation-editor-320.png).

Las mediciones no muestran desbordamiento global. Los contenedores de tablas mantienen su desplazamiento interno. Se verificó que los campos ocultos del horario estuvieran deshabilitados, que el POST de revisión volviera a renderizar los tramos y que Tab alcanzara el selector de lunes con foco visible. Se corrigió un desbordamiento de los textos accesibles de acciones de la tabla mediante una regla limitada a esta pantalla.

La interfaz mostró **19:50** y la variante dominical **13:50**. El detalle aislado de cierre mostró inicio **13:50**, vencimiento **15:00**, carga **5 minutos** y el texto del margen; no mostró 14:50 como equivalente.

![Detalle aislado del cierre dominical](../../outputs/horarios/work-dark-390.png)

**Límites:** no se acreditó zoom/texto al 200 %: los atajos del navegador integrado no produjeron una ampliación observable. No se hizo prueba en un dispositivo físico ni una auditoría WCAG integral. Las capturas de ejecución corresponden a una asignación identificada como ejemplo en la copia aislada.

## Conflictos operativos que se conservan

1. **Alejandra y Viri no tienen el rol Piso de ventas que exige el cierre.** El 21/10 corresponde a Alejandra y el 28/10 a Viri, pero sus fechas quedan en conflicto. No se cambiaron perfiles, se saltó el turno ni se amplió el horario de otra persona. El 04/11 corresponde a Dennis y la simulación lo permite, sujeto a capacidad y disponibilidad.
2. **Otras tareas siguen generando domingos:** TAR-0018 (exhibiciones), TAR-0062 (apertura), TAR-0066 (difusor), TAR-0181 (cintas) y TAR-0336 (muebles y repisas). Sus calendarios permanecen intactos; la política presenta conflicto para esas ocurrencias hasta su revisión. No se desactivaron en lote.
3. **Domingo del Director sin confirmar.** Sigue representado como horario no configurado; no se infirió disponibilidad.
4. **Doce asignaciones futuras anteriores en conflicto:** la simulación las enumera por ID, tarea y fecha. Se conserva el responsable y cada fecha original, incluidos cierres con horario anterior y tareas eliminadas lógicamente sin clasificación. Requieren revisión y las acciones de reasignación que correspondan; editar el horario no las corrige automáticamente.

La generación de planes independientes sigue siendo atómica: una ocurrencia requerida sin responsable válido impide guardar un plan parcial. Los Procesos conservan sus estados pendientes propios.

## Ejecución y recuperación

El proceso Web de red se ejecuta desde `.local/horarios-live-verified/Sgol.Web.dll`, con raíz de contenido `src/Sgol.Web`, mediante [ejecutar-horarios-local.ps1](../../scripts/ejecutar-horarios-local.ps1). Se reinició el proceso que escucha `127.0.0.1:7241`; el proxy HTTPS local/LAN conserva sus puertos y configuración. No se publicaron cambios remotos.

La comprobación final de salud y assets está en [runtime-verification.json](../../outputs/horarios/runtime-verification.json). La comprobación sigue la redirección desde `https://localhost:7240` y valida la instancia servida, no solo un build.

Recuperación preferida: desactivar la política para nuevas asignaciones, conservando esquema, horarios, auditoría e historia:

```powershell
. ./scripts/local-env.ps1
$env:SGOL_SCHEDULE_CONNECTION = $env:ConnectionStrings__Sgol
dotnet .local/horarios-operations/Sgol.ScheduleSetup.dll disable
```

Para reactivar, después de revisar configuración y conflictos, usar `enable`. Ambos comandos se autorizan por Dirección, usan el bloqueo transaccional y son auditados. `configure` previsualiza la carga inicial, `configure --apply` la aplica una sola vez y `confirm-sundays` confirma de forma idempotente los tres IDs dominicales ya aprobados.

No ejecutar downgrade sobre datos de horarios: las migraciones de integridad rechazan la reversión cuando perdería historia o la política dominical configurada. Para investigar un respaldo, restaurarlo en una **base nueva aislada**, como en las verificaciones:

```powershell
docker cp outputs/horarios/predeploy.dump sgol-local-postgres-1:/tmp/sgol-horarios-recovery.dump
docker exec sgol-local-postgres-1 createdb -U sgol_local sgol_task_checks_horarios_recovery
docker exec sgol-local-postgres-1 pg_restore -U sgol_local -d sgol_task_checks_horarios_recovery /tmp/sgol-horarios-recovery.dump
```

No sobrescribir la base habitual para comprobar el respaldo. Las bases de pruebas y archivos de evidencia se mantienen para inspección; el servidor de previsualización aislado se detuvo después de la revisión.
