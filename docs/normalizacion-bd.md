# Normalización del flujo operativo

La migración `20261013001215_NormalizeOperationalStorage` consolida el flujo de las pantallas actuales y refuerza su integridad. No borra tablas históricas ni implementa la limpieza mensual de evidencias.

## Cambios del esquema

- `operational_attachment`: documento o captura de pantalla de una entrega, con PK/FK `AssignmentId`, nombre, tipo, contenido y fecha. La migración copia los bytes originales antes de retirar `FileContent`, `ContentType` y `FileName` de `operational_assignment`. Las fotografías continúan en `operational_photograph` y el material de referencia en `operational_reference_file`.
- `plan_capacity_adjustment`: relación entre plan y ajustes de capacidad, con el estado capturado y revisión de origen. `plan_capacity_previous_segment` conserva los intervalos previos. Los eventos antiguos de planes existentes se convierten desde `audit_event`; la creación y eliminación de planes usan estas tablas. `SourceCapacityId` identifica históricamente una fila mutable que puede haber sido partida o eliminada; por eso no tiene FK a la capacidad vigente.
- FK para las tareas seleccionadas por código y para creadores, revisores y autores de avances.
- FK compuestas para exigir que una foto pertenezca a la asignación de su sesión y que una ocasión pertenezca al plan de la asignación.
- CHECK de fechas, progreso, calendario, revisión, tipos y tamaños de archivos.
- Exclusión GiST de intervalos de capacidad solapados por persona. Es diferida al final de la transacción para permitir dividir/reemplazar intervalos de forma atómica. Requiere `btree_gist`, extensión que ya utilizaba el proyecto.
- `TaskRevision` captura la revisión de la definición para asignaciones nuevas y se conserva en reasignaciones. Los registros anteriores quedan con NULL: no se inventa una revisión histórica.

Se mantienen nombres y reglas capturados al asignar para que editar el catálogo no reescriba el pasado. Son snapshots deliberados, no una afirmación de 3FN estricta de toda la base. El avance acumulado conserva su escritura transaccional con el historial. No se crea un catálogo de canales ni una tabla de días: las pantallas usan texto libre y una máscara escalar, respectivamente.

## Trabajo que se elimina

`LegacyWorkflow:Enabled` es false cuando no está configurado. Con ese valor se omiten las APIs de obligaciones y planificación anteriores, la generación recurrente y asignación manual del worker, la infraestructura de evidencias anterior del worker y la consulta de carga de obligaciones antiguas. Los servicios/tablas históricos se conservan para compatibilidad explícita; no deben reactivarse sin un consumidor real.

En variables de entorno, la clave es `LegacyWorkflow__Enabled`. Tanto web como worker deben usar el mismo valor si se reactiva el circuito anterior. Las pantallas actuales usan `operational_*`; reactivar las APIs anteriores no agrega obligaciones a su listado.

`scripts/iniciar-local.ps1` inicia web y, en modo de red, proxy. No inicia un worker permanente que consultaría una cola sin trabajo. `-WithWorker` permite iniciarlo explícitamente. Los comandos `Sgol.Worker run-job` para respaldos/replicación siguen disponibles cuando se configura su infraestructura; la política de respaldos de producción se decide por separado.

Las asignaciones no contienen bytes ni cargan automáticamente el adjunto. Los listados consultan nombres mediante relaciones; las descargas recuperan el contenido solamente después de autorizar al usuario. Vincular fotografías ya verificadas consulta tamaños en PostgreSQL y actualiza su posición, sin volver a descargar sus bytes a la aplicación.

`CapacityLedger.DayLoad` calcula la distribución diaria una vez por instancia. Los cambios de capacidad y las transferencias invalidan el acumulado; agregar una asignación crea otra instancia. La cache dura la operación, no se comparte entre usuarios ni solicitudes. Si se agrega otra mutación directa de sus tareas, debe llamar a `InvalidateLoads()`.

## Aplicación y reversión

Antes de aplicar, detener la versión web/worker anterior, crear un respaldo y verificar la migración en una copia. Los datos inválidos provocan un error y rollback: no se descartan ni corrigen silenciosamente. El proyecto debe compilar con la migración antes de iniciarse contra el esquema nuevo.

Con la conexión del entorno cargada, usar EF Core:

```powershell
. .\scripts\local-env.ps1
.\.local\ef-tools\dotnet-ef.exe database update --project src/Sgol.Web --startup-project src/Sgol.Web --configuration Release
```

`outputs/normalizacion/normalizar.sql` contiene el SQL incremental desde `AddOccasionalTasks`, para una base en esa versión. No es un script genérico de creación, ni debe ejecutarse repetidamente después de aplicarse. Para otros estados de despliegue, generar el script correspondiente con EF y revisar el historial aplicado.

La reversión devuelve el archivo único a las columnas anteriores y conserva la auditoría que permite reconstruir los ajustes al reaplicar. Antes de reducir nombres de fotos al límite anterior, restituye el nombre original en la asignación y usa el nombre generado de la cámara cuando supera ese límite. La revisión de origen de tareas nuevas no existe en el esquema anterior; para recuperación íntegra se conserva el respaldo.

## Verificación realizada

- Compilación completa, sin advertencias ni errores, y comprobación de que no existen cambios de modelo pendientes.
- 259 comprobaciones de tareas, planes, cámara, supervisión, especializaciones, eliminación de planes, ocasionales e integridad SQL.
- 47 comprobaciones de tablero, 75 de reportes y matriz de acceso de los cuatro roles.
- Prueba de bajar y reaplicar la migración con adjuntos, fotos con nombre de 154 caracteres y ajustes de capacidad existentes. Compara bytes, nombres y snapshots.
- Copia de `sgol_local`: comparación de recuentos y huellas de asignaciones, adjuntos, fotografías y material de referencia, antes y después. Resultados en `outputs/normalizacion/clone-before.jsonl` y `clone-after.jsonl`.
- Microprueba de capacidad: 150 tareas y 360 consultas repetidas sobre el mismo ledger. Verifica resultados iguales y registra duración en `normalization-checks.log`. Es una medición local de esa parte del cálculo, no un benchmark de toda la aplicación ni una estimación de factura.

## Preparación para DigitalOcean

Estos cambios reducen lecturas de bytes, consultas de la cola anterior y redistribuciones repetidas de carga. No eligen recursos, crean infraestructura ni publican el sistema. El tamaño de web, PostgreSQL y el escáner de archivos, los respaldos, el crecimiento del almacenamiento y la transferencia deben medirse con concurrencia y evidencias representativas antes de dimensionar el servidor.

Los Droplets se facturan según recursos y tiempo de existencia, no por cálculo individual de tarea; la transferencia saliente puede añadir costo. La configuración elegida importa, y algunos tipos de Droplet tienen reglas distintas de límite mensual. Referencia: [precios y facturación oficiales](https://docs.digitalocean.com/products/droplets/details/pricing/), consultados el 12 de octubre de 2026.

La retención mensual sigue pendiente. Los archivos ahora tienen tablas específicas; eliminar contenido deberá conservar la asignación, el avance y la decisión de supervisión. No se borraron evidencias para obtener los resultados de estas pruebas.
