# SGOL en esta computadora

Para compartirlo con compañeros de la red del trabajo, consulta la
[guía de acceso en red](acceso-red.md).

El proyecto es una solución de .NET 10: `Sgol.Web` contiene Razor Pages y la API,
los módulos contienen los contratos de negocio y PostgreSQL guarda los datos.
El flujo operativo actual funciona en la web; `Sgol.Worker` conserva comandos de
respaldo y compatibilidad con la cola anterior. ClamAV verifica los archivos;
las evidencias operativas se guardan en tablas separadas. El circuito anterior usa S3.
Consulta [la normalización del esquema](normalizacion-bd.md) para las migraciones
y la preparación del despliegue.

## Abrir y probar

Desde PowerShell, en la carpeta del proyecto:

```powershell
.\scripts\iniciar-local.ps1
```

Abre **https://localhost:7240** en el navegador de esta computadora.
Esta dirección muestra directamente la aplicación.

La cuenta inicial es **direccion.local**. Su contraseña temporal está en
`.local/acceso.txt`. En el primer acceso, cambia la contraseña y configura MFA
con una aplicación de autenticación. Conserva los códigos de recuperación.
La contraseña temporal deja de servir después del cambio; los siguientes
arranques conservan la cuenta y tus datos.

## Ver cambios

Edita y guarda los archivos del proyecto. `dotnet watch` ejecuta la web.
El worker no se inicia por defecto; se puede habilitar con `-WithWorker` cuando
se necesita. La web reinicia al guardar cambios para evitar un fallo de Hot Reload
del SDK con archivos estáticos.
Actualiza el navegador con F5 para ver los cambios. Si el navegador conserva
archivos en caché, usa Ctrl+F5. La actualización automática del navegador está desactivada.

- Pantallas: `src/Sgol.Web/Pages/**/*.cshtml`
- Estilos: `src/Sgol.Web/wwwroot/css/`
- Interacciones: `src/Sgol.Web/wwwroot/js/`
- Servidor: archivos `.cs` de la solución.

Los cambios de esquema requieren migraciones de EF Core. El iniciador aplica las
migraciones existentes; vuelve a ejecutarlo cuando agregues una migración.
No puede aplicar código que tenga errores de compilación: consulta los registros.

## Detener y volver a iniciar

```powershell
.\scripts\detener-local.ps1
.\scripts\iniciar-local.ps1
```

La detención conserva los volúmenes de Docker. No se borra la base de pruebas.
Docker Desktop debe estar abierto para iniciar. Los procesos web y worker arrancan
en segundo plano; no se instalan como servicios de Windows ni se inician solos
al encender la computadora. Ejecuta el iniciador después de reiniciarla.

## Pantallas por rol

Dirección tiene acceso a todas las pantallas. Administración ve Mi trabajo,
Supervisión, Configuración, Tareas, Planificación y Reportes. El catálogo de
Tareas está disponible para consulta; Dirección conserva su administración.
Subcoordinación ve Mi trabajo, Supervisión, Configuración y Reportes.
Piso de ventas ve Mi trabajo y Configuración. Todos los roles también tienen acceso
a **Tablero Vivo** en `/tablero`, una vista para la pantalla de la sucursal.

El tablero permite elegir un plan y muestra sus tareas por rol con seis tarjetas
por fila en escritorio. Muestra exclusivamente tareas cuyo cierre cae hoy:
una tarea activada ayer con vencimiento hoy aparece hoy; una activada hoy con
vencimiento mañana aparece mañana. Los pendientes vencidos de días anteriores
siguen accesibles en Mi trabajo, pero salen del tablero al cambiar el día.
Usa la hora de Ciudad de México y muestra la jornada de 08:00 a 20:00.
Las tarjetas cambian de gris (programadas) a verde (activas), amarillo (última hora)
y rojo (últimos 30 minutos o vencidas). Las entregadas conservan su marca durante
el día de vencimiento, incluso si se entregaron anticipadamente. La barra mide
las entregas de las tareas con cierre hoy, sin esperar aprobación de Supervisión;
solo alcanza 100% cuando todas las tareas mostradas se entregaron.
Las entregas y los avances se reciben mediante una conexión de eventos del servidor,
con detección de cambios cada dos segundos; si se pierde, se consulta cada cinco
segundos. Los colores se actualizan cada segundo. Si las tarjetas
no caben, se desplazan automáticamente; la interacción pausa el desplazamiento.
El límite de sesión de ocho horas sigue vigente: la pantalla solicita iniciar sesión
de nuevo al vencer. No requiere cambios de esquema ni migraciones.

Para crear el plan semanal de prueba del tablero sin modificar planes existentes:

```powershell
. ./scripts/local-env.ps1
dotnet run --project tools/Sgol.LocalSetup/Sgol.LocalSetup.csproj -c Release -- --seed-board
```

Para comprobar específicamente la visibilidad por vencimiento:

```powershell
. ./scripts/local-env.ps1
dotnet run --project tools/Sgol.LocalSetup/Sgol.LocalSetup.csproj -c Release -- --seed-board-deadlines
```

Crea un plan semanal independiente llamado **Prueba · Tablero por vencimiento · fecha**,
asignado a Ana de prueba: una tarea de ayer 18:00 a hoy 18:00, dos de hoy
(08:00–12:00 y 14:00–20:00), y una de hoy 18:00 a mañana 18:00.
Verifica contra la base local que ayer aparezcan cero, hoy tres y mañana una.
Repetir el comando el mismo día conserva el plan y sus entregas. Las definiciones
de ejemplo quedan inactivas para no usarlas accidentalmente en otros planes.

El comando anterior `--seed-board` incluye tareas de Piso de ventas con todos los tipos de evidencia y calendario, colores de ejemplo, una entrega
y un avance inicial de 5/20 productos. Solo usa cuentas locales de prueba y genera
una simulación por semana. Repetirlo conserva las entregas y avances ya registrados.
Registra nuevas entregas y unidades desde Mi trabajo con el responsable asignado;
el tablero refleja esos cambios automáticamente.

Para preparar las tareas pendientes del día para Ana en esa simulación:

```powershell
. ./scripts/local-env.ps1
dotnet run --project tools/Sgol.LocalSetup/Sgol.LocalSetup.csproj -c Release -- --prepare-board-ana
```

El ajuste se limita al plan de prueba de la semana actual, conserva entregas y
avances de Piso de ventas y renueva sus horarios de ejemplo para probar los colores.
El tablero ordena las tarjetas por rojo, verde, amarillo y gris, con las entregadas
al final; ese orden se recalcula automáticamente cuando cambia un color.

Dirección y Administración consultan la misma pantalla de planes activos y sus
detalles en `/planificacion`. La creación, modificación y eliminación de planes
siguen reservadas a Dirección. La interfaz anterior de planificación fue retirada;
sus enlaces con filtros del calendario redirigen a `/planificacion`.

Personas y accesos y sus subpantallas quedan reservadas a Dirección. Las
restricciones se aplican también al abrir una URL directamente o enviar un
formulario. Para verificar la matriz de accesos:

```powershell
dotnet run --project tools/Sgol.RoleAccessChecks
```

## Apariencia

En **Configuración → Apariencia** elige Claro u Oscuro. El cambio se aplica a
todas las pantallas y se conserva en ese navegador, incluso después de recargar.
Cada navegador conserva su propia preferencia; el valor inicial es Claro.

En la misma pantalla, **Versión de la aplicación** permite elegir Escritorio,
Móvil o Automática. Escritorio conserva la composición amplia; Móvil limita
la vista a 420 px y usa la navegación compacta; Automática se adapta a la pantalla.
La elección se conserva en ese navegador al recargar y volver a iniciar sesión.
Las composiciones responsivas siguen el ancho de la aplicación mediante consultas
de contenedor CSS. Configuración aparece al final de la navegación, con un engranaje.

La creación y publicación de borradores del calendario está en
`/planificacion/versiones`, disponible con el permiso de
administración de configuración. Los servicios y datos compartidos siguen
disponibles para los demás módulos.

## Reportes

En **Reportes**, elige una de las ocho consultas: resumen operativo, cumplimiento,
vencimientos, carga de trabajo, avance de metas, pendientes de revisión, resultados
de supervisión o seguimiento de planes. Ajusta el periodo y pulsa **Generar reporte**.
Los indicadores, gráficas y detalle usan los datos reales visibles para tu cuenta.

En **Más filtros y presentación** puedes seleccionar estados, tareas, especializaciones,
requisitos, agrupación, orden y columnas. Los periodos relativos se recalculan cada
vez que se aplica una plantilla. Las plantillas personales se guardan por cuenta en
la base de datos; se pueden editar, duplicar y eliminar.

**Exportar a PDF** ofrece un resumen visual o el reporte completo con las columnas
elegidas. Pulsa **Ver vista previa** y después **Descargar PDF**. El documento conserva
el logo actual de Loretta, los filtros y la fecha de corte del reporte mostrado.
La búsqueda dentro de la tabla solo cambia las filas visibles en pantalla.

Los periodos anteriores muestran el estado actual de las tareas al corte de consulta.
La entrega y la aprobación son momentos distintos. La carga en minutos es estimada;
los intentos reasignados se separan del total para no duplicar el cumplimiento.

La migración `AddReportTemplates` crea la tabla de plantillas. El iniciador local
aplica las migraciones existentes. Para comprobar las reglas de cálculo:

```powershell
dotnet run --project tools/Sgol.ReportChecks/Sgol.ReportChecks.csproj
```

## Simulación de reportes

Para agregar ejemplos al entorno local:

```powershell
. ./scripts/local-env.ps1
dotnet run --project tools/Sgol.LocalSetup -- --seed-reports
```

Crea planes semanales históricos, actuales y futuros, tareas con siete estados,
metas con distintos avances, revisiones y reasignaciones. También agrega ocho
plantillas personales por cuenta de prueba y disponibilidad de ejemplo cuando
no existe una configuración en ese período. En Reportes selecciona una plantilla
`Simulación · …` para consultar el mes actual y probar la vista previa o el PDF.

Todos los registros nuevos se identifican como simulación. No reemplaza datos,
contraseñas ni plantillas existentes; repetirlo durante la misma semana no duplica
los planes. Las definiciones ficticias quedan inactivas para evitar su generación
recurrente. La herramienta solo permite Development y `sgol_local` en localhost.

## Servicios y registros

Desde **Planificación → Consultar** un plan de proceso, Dirección puede pulsar
**Eliminar plan de proceso**. La revisión muestra las asignaciones, ejecuciones,
avances y archivos que se borrarán. Puedes descargar los documentos de todas las
versiones, fotografías y material de referencia en un ZIP con su índice, o elegir
eliminar sin descargarlos. Si eliges descargar, guarda y verifica el ZIP antes de
escribir `ELIMINAR` y confirmar. Una descarga no borra el plan.

La eliminación libera las reservas y borra el historial de ejecución de ese plan;
conserva el catálogo, las personas, otros planes y la auditoría. Si cambia el plan
después de la revisión o descarga, se requiere revisar nuevamente. PostgreSQL solo
permite retirar documentos y eventos dentro del borrado completo y auditado del
plan, mediante la migración `AllowAuditedProcessPlanDeletion`.

Las comprobaciones se ejecutan con `--process-deletion-only` en
`tools/Sgol.TaskChecks`, usando una base vacía `sgol_task_checks_*` en localhost.
Incluyen ambos recorridos HTTP, CSRF, archivos originales, versiones anteriores,
permisos, cambios concurrentes, reintentos, rollback y preservación del catálogo.

| Servicio | Dirección local |
| --- | --- |
| Aplicación HTTPS | https://localhost:7240 |
| PostgreSQL | localhost:5440, base `sgol_local` |
| S3 / SeaweedFS | http://localhost:8333 |
| ClamAV | localhost:3310 |

Las credenciales generadas se guardan en `.local/config.json`, excluido de Git.
La preparación solo admite la base `sgol_local` en localhost y Development.
Todos los puertos publicados se limitan a esta computadora. HTTPS usa el certificado
de desarrollo de .NET; se conserva la autenticación, MFA y protección CSRF del sistema.

Los registros están en `.local/web.log`, `.local/web.error.log`, `.local/worker.log`
y `.local/worker.error.log`. Para comprobar los servicios:

```powershell
Invoke-RestMethod https://localhost:7240/health/ready
docker compose --env-file .local/compose.env -f compose.local.yaml ps
```

El worker local consume la **outbox**. Los trabajos recurrentes del proyecto se
ejecutan mediante su comando `run-job`; este entorno no agrega un planificador
automático de esos trabajos.

Referencias: [dotnet watch](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch),
[PostgreSQL en Docker](https://hub.docker.com/_/postgres),
[SeaweedFS](https://github.com/seaweedfs/seaweedfs).

