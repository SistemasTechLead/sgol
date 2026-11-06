# Auditoría de preparación para DigitalOcean — 6 de noviembre de 2026

## Dictamen y alcance

**No abrir todavía la operación real a Internet.** El proyecto compila y sus migraciones funcionan sobre una base vacía, pero faltan la entrega reproducible de infraestructura, el aprovisionamiento remoto, el traslado selectivo de catálogos y la validación del despliegue Linux completo. Esto no requiere reescribir SGOL: requiere cerrar la capa de entrega y operación.

Petición: auditoría y recomendaciones, sin cambios funcionales, publicación, contratación ni limpieza de datos. El usuario confirmó operación real con base nueva y catálogos seleccionados, presupuesto aproximado de **US$50/mes**, concurrencia prevista igual a las cuentas actuales y ausencia de dominio. Las decisiones de catálogo, retención y recuperación siguen pendientes; no se ejecutó ninguna eliminación.

Auditoría transversal de arquitectura, compilación, persistencia, evidencia, autenticación, red, procesos, recuperación, automatización y pruebas. No es una revisión línea por línea de los 591 archivos C# de `src`, una prueba de penetración ni certificación de todos los recorridos funcionales. Se revisaron productores y consumidores críticos para el despliegue. Las conclusiones distinguen inspección estática, pruebas ejecutadas y validaciones futuras.

## Estado observado

- Checkout en `master`, último commit `4837e40`; ocho archivos modificados y dos informes sin seguimiento ya existían antes de esta auditoría. Se conservaron. Incluyen Reportes, arrastres y altura del tablero. No hay aún una revisión inmutable que represente todos esos cambios.
- App compartida: `.local/runtime/report-carryovers-20261105/Sgol.Web.dll`, PID 7044; proxy PID 21788. Web escucha en `127.0.0.1:7241`; proxy HTTPS local en 7240 y LAN en 7440, almacenamiento en 7443. Se comprobaron procesos, argumentos y listeners; no se reiniciaron.
- Docker compartido: PostgreSQL 18.6, SeaweedFS, gateway y ClamAV. No había un proceso `Sgol.Worker` entre los procesos dotnet observados; esto no demuestra ausencia de otros mecanismos externos de programación.
- Lecturas agregadas, sin exponer nombres ni credenciales: **7 cuentas activas, 1 eliminada, 8 personas, 295 tareas totales, 587 fotografías**. Hay 0 filas en `mfa_totp_credential`; no debe suponerse que los usuarios ya tienen MFA preparado.
- Base simulada: **316 MB**, de los cuales `operational_photograph` ocupa **284 MB** contando tabla e índices. Es una medición puntual de simulación, no una tasa de crecimiento ni una prueba de capacidad.
- `global.json` fija SDK 10.0.400; proyectos `net10.0`. Existen versiones centrales y archivos de bloqueo de NuGet. La solución contiene 15 proyectos, pero solo una herramienta de comprobación (`Sgol.CameraChecks`); existen 12 proyectos `*Checks` en el repositorio.
- La guía de instalación de la bóveda aún identifica `images50-20261105` en su topología, mientras la base documental y el proceso efectivo identifican `report-carryovers-20261105`. Reconciliar esa discrepancia al actualizar procedimientos. La instrucción histórica de consultar readiness por el proxy tampoco coincide con el bloqueo explícito del proxy.

## Arquitectura propuesta para este presupuesto

**Un Droplet Linux, inicialmente una instancia Web, PostgreSQL local privado, ClamAV privado, proxy HTTPS y Spaces para almacenamiento S3 y copias cifradas.** Usar servicios supervisados o contenedores con volúmenes persistentes, reinicio automático y límites de recursos. No publicar PostgreSQL, ClamAV ni el puerto de Kestrel. Registrar un dominio y usar un subdominio, por ejemplo `sgol.<dominio>`; es un ejemplo, no un dominio contratado.

| Concepto | Referencia mensual en USD | Observación |
|---|---:|---|
| Droplet Basic Regular, 2 vCPU, 4 GiB RAM, 80 GiB SSD | 24.00 | Candidato inicial; capacidad pendiente de ensayo con 7 sesiones y cargas de evidencia simultáneas. |
| Backups diarios del Droplet, plan porcentual | 7.20 | 30% de 24; no sustituyen el respaldo lógico y su restauración. |
| Spaces Standard | 5.00 | Base de 250 GiB compartida entre buckets. Mantenerlos privados. |
| **Subtotal de servicios** | **36.20** | Antes de impuestos, dominio, consumos excedentes y recursos temporales. Margen nominal de 13.80 hasta 50. |

Fuentes consultadas: [Droplets](https://www.digitalocean.com/pricing/droplets), [backups](https://docs.digitalocean.com/products/backups/details/pricing/), [Spaces](https://docs.digitalocean.com/products/spaces/details/pricing/). Precios publicados, sujetos a disponibilidad regional y al presupuesto final mostrado al contratar. No se consultó disponibilidad ni tarifa de un dominio concreto. El límite total de 50 debe revisarse incluyendo impuestos y renovación del dominio; el subtotal no es una cotización final.

### Conciliación con la estimación anterior

Se recuperó y leyó el chat **«Evaluar preparación para DigitalOce»**, del 21 de octubre de 2026, identificado como `01a15fa8-81cb-7670-b6e3-1c7803879ee0`. Ya se habían estimado **36.20/mes, redondeados a 40**, para exactamente el mismo conjunto: Droplet 4 GiB de 24, backup diario de 7.20 y Spaces de 5. Por tanto, el subtotal de esta auditoría confirma esa referencia; no es una arquitectura nueva ni una sustitución de un acuerdo anterior.

La alternativa anterior usaba PostgreSQL administrado de **2 GiB por 30.45**, además del mismo Droplet, backup diario y Spaces: **66.65/mes, redondeados a 70**. Los precios de 1 GiB/15.15 y 2 GiB/30.45 se comprobaron en las [tarifas actuales de bases administradas](https://www.digitalocean.com/pricing/managed-databases). Un subtotal de 44.15 usando BD de 1 GiB y omitiendo backup del Droplet es una configuración diferente y no debe compararse como si hubiera bajado el costo de la alternativa anterior. Con el backup diario sería 51.35 antes de dominio e impuestos, todavía sin validar capacidad.

El chat anterior también corrigió su recomendación de hardware propio hacia al menos 8 GiB por el margen necesario para ClamAV. La cifra de 4 GiB en nube sigue siendo una hipótesis económica por validar, no capacidad garantizada. Deben incluirse actualización/recarga de firmas y cargas simultáneas en la prueba, no solo consumo en reposo.

La estimación histórica de 0.2–0.3 GB mensuales de evidencia provenía de apenas 2.5 días de observación. No se reutiliza como pronóstico actual: cambiaron la muestra y la simulación. El chat propuso, sin implementar, retención de 30 días para rutinas, 90 para mensuales, 6 meses para trimestrales y 12 para semestrales, desde cierre de revisión y con protección por incidencia. Es un antecedente pendiente de decisión, no autorización de borrado; el cleanup actual de cámara expirada no implementa esa política histórica.

El Droplet único es un punto único de fallo: su caída interrumpe el servicio. La propuesta acepta recuperación desde copias; **no ofrece alta disponibilidad**. ClamAV, normalización de imágenes y PostgreSQL compiten por RAM/CPU; 4 GiB es una hipótesis que debe probarse. El número de usuarios por sí solo no permite garantizar rendimiento. Si el ensayo exige 8 GiB, el plan de 48 más copias y Spaces excedería el presupuesto: tocaría revisar arquitectura o presupuesto.

App Platform es posible como trabajo posterior, pero el proyecto no trae su especificación ni una imagen Web y habría que resolver scanner, programación, red y sondas. Su [filesystem local es efímero](https://docs.digitalocean.com/products/app-platform/how-to/store-data/). SGOL ya persiste el key ring en PostgreSQL cuando se configura correctamente; no se identifica aquí un impedimento absoluto por claves locales. Para este primer despliegue y presupuesto, el Droplet permite controlar mejor el conjunto de dependencias. No se justifica Kubernetes para este alcance.

## Hallazgos y correcciones prioritarias

P0: condición que debe resolverse antes de abrir producción. P1: corrección o comprobación necesaria para la salida estable. P2: mejora posterior con seguimiento, salvo que las pruebas demuestren mayor impacto.

| ID | Prioridad | Evidencia y consecuencia | Corrección / criterio de cierre |
|---|---|---|---|
| D01 | P0 | No hay `.github/`, manifiesto de despliegue cloud, `.dockerignore` ni Dockerfile Web versionado. `compose.local.yaml` levanta dependencias, no una entrega completa Web/operaciones. La publicación Linux cruzada no prueba ejecución Linux. | Crear paquete de despliegue Linux reproducible: imagen o publicación versionada, proxy, servicios, volúmenes, variables sin secretos, sondas, arranque tras reboot y rollback. Si se usa Docker, excluir `.local`, evidencias, repositorio Git y salidas del contexto. |
| D02 | P0 | [DatabaseSecurity](../../../tools/Sgol.DatabaseSecurity/Program.cs) limita host a localhost/127.0.0.1 y DB a simulación/checks. [StorageSetup](../../../tools/Sgol.StorageSetup/Program.cs) solo acepta endpoints HTTP loopback en puertos específicos. Sus protecciones son deliberadas; no sirven directamente para DO. | Aprovisionador de producción separado o modo explícito con destino esperado, validación TLS, cuentas runtime/migración/mantenimiento y permisos mínimos. Conservar las protecciones locales. Probar que runtime arranca y que no puede alterar esquema/auditoría. |
| D03 | P0 | [Program](../../../src/Sgol.Web/Program.cs) exige MFA fuera de Development; [ApiClientServiceCollectionExtensions](../../../src/Sgol.Web/Interface/ApiClient/ApiClientServiceCollectionExtensions.cs) exige origen HTTPS en Production/Staging; [DataProtection](../../../src/Sgol.Web/Infrastructure/Persistence/DataProtection/DataProtectionServiceCollectionExtensions.cs) exige application name y certificado portable. Los lanzadores locales usan Development/DPAPI. | Configuración propia de Production; dominio, TLS público, secretos nuevos inyectados, copia independiente del certificado de wrapping y contraseña. Bootstrap de Dirección, cambio de contraseña, enrolamiento MFA y recuperación comprobados para usuarios nuevos. No copiar DPAPI ni claves de prueba como solución de producción. |
| D04 | P0 | [LanProxy](../../../tools/Sgol.LanProxy/Program.cs) está acoplado a IP LAN, puertos locales y certificado del almacén del usuario. [LocalSharedTransport](../../../src/Sgol.Web/Infrastructure/Http/LocalSharedTransport.cs) confía en una IPv4 privada/loopback y un salto. Razor hace llamadas HTTP al origen HTTPS autorizado. | Proxy de producción con certificados renovables, host exacto, redirección HTTP, forwarding controlado y resolución del dominio desde el servidor. Probar login, CSRF, cookies y envío real a través del proxy; un 200 de `/acceso` no basta. No confiar indiscriminadamente en cualquier proxy. |
| D05 | P0 | La base nueva crea 8 tareas por migraciones; los catálogos actuales tienen 295 filas totales. [TaskImport](../../../tools/Sgol.TaskImport/Program.cs) es local Development; no es un migrador integral de tareas, especialidades y procesos. | Manifiesto selectivo de catálogos con dependencias, vista previa y validación referencial. Reconciliar semillas por código estable, conservar parámetros aceptados y verificar duplicados. Inicializar usuarios nuevos; no trasladar sesiones, MFA, ejecuciones, evidencias, historial ni asignaciones simuladas. No reescribir migraciones históricas para eliminar semillas. |
| D06 | P0 | Hay código real de backups cifrados, réplica y restauración en [Operations](../../../src/Sgol.Operations/PostgreSqlPortableBackup.cs), pero no una agenda de producción ni un ensayo de recuperación DO ejecutado aquí. Una copia de BD sin el secreto que descifra el key ring puede resultar insuficiente. | Programar backups y alertas, retención acordada y credenciales separadas; conservar fuera del servidor las claves de descifrado. Restaurar en un destino nuevo, verificar login/MFA, datos y archivos y medir recuperación. Acordar pérdida máxima de trabajo (RPO) y tiempo de recuperación (RTO). |
| D07 | P1 | [EvidenceInfrastructure](../../../src/Sgol.Web/Infrastructure/Evidence/EvidenceInfrastructureServiceCollectionExtensions.cs) etiqueta ClamAV como `worker-ready`, pero [ReadinessProbe](../../../src/Sgol.Web/Infrastructure/Http/ReadinessProbe.cs) consulta solo `ready`. El scanner se usa también en el flujo Web actual. | Añadir vigilancia efectiva del scanner y alertar por indisponibilidad/firmas desactualizadas. Decidir si readiness global falla o se declara degradación específica; demostrar que falla cerrada la carga y se recupera. Además readiness solo admite loopback sin forwarding: usar sonda interna, no configurar un balanceador externo contra ese endpoint esperando 200. |
| D08 | P1 | [OperationalPhotographs](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalPhotographs.cs), [Camera](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTaskService.Camera.cs) y [OperationalTaskService](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTaskService.cs) guardan fotos y adjuntos en PostgreSQL. S3 es otra ruta y dependencia; configurar Spaces no mueve estos datos. | Dimensionar disco, respaldo y restauración para binarios en BD. Mantener inicialmente este diseño si supera el ensayo; moverlo a objetos es un cambio funcional separado con integridad, autorización e historial, no requisito automático para siete cuentas. Definir retención antes de aplicar borrados. |
| D09 | P1 | [S3PrivateObjectStorage](../../../src/Sgol.Web/Infrastructure/Evidence/S3PrivateObjectStorage.cs) comprueba CORS exacto, firma PUT con `If-None-Match`, metadatos y GET privado. DigitalOcean declara compatibilidad S3 parcial. | Ensayo real contra Spaces: API, CORS exacto, rechazo anónimo, reintento sin sobrescritura, firma, expiración, copia/borrado y permisos. Usar origen S3 apropiado en CSP. No dar por homologado SeaweedFS→Spaces por compartir SDK. |
| D10 | P1 | CSP está condicionada por `LocalShared:SecurityHeaders`; en `/acceso` compartido se observó CSP y Referrer-Policy, pero no HSTS ni `X-Content-Type-Options`. | Fijar cabeceras de producción y comprobarlas por HTTPS, incluyendo error/estáticos. Activar HSTS al tener HTTPS estable y renovación; añadir `nosniff`. No convertir el valor `AllowedHosts=*` aislado en hallazgo de host abierto: el origen confiable ya configura host filtering en Production. |
| D11 | P1 | [InternalLoginOrigin](../../../src/Sgol.Web/Infrastructure/Authentication/InternalLoginOrigin.cs) guarda tickets en memoria del proceso; rate limits y admisión también son locales. Los timers operativos viven dentro de Web. | Primera versión con una réplica Web siempre encendida. No activar autoscaling horizontal sin probar afinidad/canal interno, cuotas compartidas y concurrencia. Inventariar qué timers son Web y qué jobs se ejecutan por Worker; comprobar reinicio sin doble ejecución. |
| D12 | P1 | [Operations.MigrateAsync](../../../src/Sgol.Operations/Program.cs) aplica todas las migraciones pendientes y después comprueba que `expectedMigration` esté entre las aplicadas. Ese parámetro no limita el esquema al destino indicado. | Verificar antes de modificar que artefacto, esquema actual y migración objetivo son los esperados; verificar después el extremo exacto. Probar rechazo sin escrituras ante objetivo incorrecto. Documentar que revertir binarios no revierte automáticamente una migración. |
| D13 | P1 | [ExternalProcessPipeline](../../../src/Sgol.Operations/ExternalProcessPipeline.cs) propaga PGSSLMODE, pero no traduce `RootCertificate` a PGSSLROOTCERT. La configuración Npgsql no se copia completa a `pg_dump`/`pg_restore`. | Si se elige BD administrada con CA específica, propagar la CA o provisionar un trust store/PGSSLROOTCERT explícito. Probar backup y restore con `verify-full`; no rebajar TLS para resolverlo. Para PostgreSQL dentro de la red privada local del mismo host el contexto es diferente. |
| D14 | P1 | No existe puerta CI del repositorio; la solución omite la mayoría de ejecutables de comprobación. `Directory.Build.props` desactiva NETAnalyzers. `dotnet test` sobre la solución no sustituye ejecutar estos programas. | Pipeline con restore bloqueado, Release, análisis progresivo, auditoría dependencias e imágenes, migración vacía y suite explícita HTTP/SQL/navegador en Linux. Revisar JavaScript vendorizado, imágenes y secretos/historial: NuGet no los cubre. |
| D15 | P2 | [TaskBonusService](../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskBonusService.cs) carga historiales completos en algunos cálculos y recorre semanas desde un inicio fijo cada 30 s; hay coordinación global para escrituras. Existen protecciones y cierres persistidos, por lo que no se afirma que ya falle con siete usuarios. | Medir crecimiento y contención con volumen representativo; reducir lecturas completas y trabajo de semanas ya cerradas cuando los resultados lo justifiquen. Mantener atomicidad e historial. No aumentar CPU a ciegas. |

Compatibilidad de referencia: [Spaces S3](https://docs.digitalocean.com/products/spaces/reference/s3-compatibility/), [CORS](https://docs.digitalocean.com/products/spaces/how-to/configure-cors/), [seguridad PostgreSQL](https://docs.digitalocean.com/products/databases/postgresql/how-to/secure/), [configuración inicial del Droplet](https://docs.digitalocean.com/products/droplets/getting-started/recommended-droplet-setup/).

## Controles existentes que conviene conservar

- Cookies Secure/HttpOnly, CSRF, validación de sesión y MFA obligatorio fuera de desarrollo; bootstrap de Dirección con contraseña efímera y bloqueo de repetición.
- Key ring en PostgreSQL cifrado con certificado portable; guard de runtime que rechaza propiedad/privilegios de administración. No hace falta inventar otra autenticación para desplegar.
- Validación técnica, escaneo antimalware, normalización de imágenes, límites de carga y autorización de consultas/descargas. La ruta actual persiste contenido ya verificado y mantiene contexto de asignación.
- Transacciones, revisiones y locks operativos en recorridos de entrega, supervisión y procesos; hay pruebas dedicadas de reintentos/concurrencia. Su existencia no equivale a haberlas ejecutado todas en esta auditoría.
- Salud, logs JSON/correlación, operaciones de backups y réplica, herramientas de recuperación y dependencias fijadas. Falta conectarlos a la infraestructura y al monitoreo de producción.

## Configuración de producción que debe quedar documentada

| Grupo | Configuración / tratamiento |
|---|---|
| Entorno | `ASPNETCORE_ENVIRONMENT=Production`, `DOTNET_ENVIRONMENT=Production`; escucha interna explícita. |
| Dominio | `SgolApi__TrustedOrigin=https://<dominio>`; DNS y certificado público; dominio resoluble desde Web. |
| Proxy | `ReverseProxy__KnownProxy` con la dirección real controlada; ajustar red del proxy sin abrir confianza global. |
| Cabeceras | `LocalShared__SecurityHeaders=true`, `LocalShared__EvidenceOrigin` HTTPS correcto y cabeceras complementarias en proxy/app. |
| Identidad | `Authentication__RequireMfa=true`; bootstrap ejecutado una vez; revocación y recuperación operativa comprobadas. |
| Cifrado | `DataProtection__ApplicationName` estable por entorno; `WrappingCertificate` y `WrappingCertificatePassword` secretos, respaldados por separado. Ensayar restauración y cualquier rotación. |
| PostgreSQL | `ConnectionStrings__Sgol` runtime; migrador y mantenimiento fuera de Web. Si remoto, TLS con identidad verificada y fuentes permitidas. |
| Evidencia | `Evidence__Storage__*`, CORS/orígenes exactos, buckets privados; `Evidence__Scanner__*` por red privada. Scanner y sus firmas con persistencia. |
| Procesos | `Processes__OperationsEnabled` según uso aprobado y validación; no asumir que el valor predeterminado activa procesos. Mantener `LegacyWorkflow` desactivado salvo necesidad explícita. |
| Operaciones | `Backup__*`, `Replica__*` si aplica, herramientas `pg_dump`/`pg_restore` compatibles y `age`, agenda externa y alertas. Worker `outbox` y `run-job` tienen funciones distintas. |
| Capacidad | Límites coherentes proxy/Kestrel/formulario: máximo de request 32 MiB, hasta 30 MiB agregados por tarea; probar multipart y cancelación, sin aumentarlos indiscriminadamente. |

Las variables anteriores son inventario de claves, no una configuración lista para copiar ni secretos reales. Las claves de infraestructura, certificado TLS y certificado que cifra Data Protection tienen propósitos distintos.

## Puesta en producción con base nueva

1. Cerrar la versión candidata: revisar cambios pendientes, congelar commit, publicar binarios/estáticos juntos y registrar digest. No compilar en el servidor que atiende usuarios como procedimiento habitual.
2. Construir el despliegue reproducible y ensayarlo localmente en Linux. Mantener separadas la simulación diaria y las pruebas de release.
3. Definir catálogo seleccionado: tareas, especialidades, procesos/versiones/etapas, reglas, calendario, horarios y responsables que se conservan. Resolver dependencias y semillas por código; no arrastrar ejecuciones. La selección concreta requiere decisión del usuario.
4. Crear base y buckets nuevos; migrar con identidad separada, aprovisionar permisos, bootstrap de Dirección y cargar catálogo validado de manera repetible. Dejar procesos automáticos sin activar hasta revisar fechas y responsables.
5. Enrolar las siete cuentas activas que deban existir realmente, con credenciales nuevas y MFA. Revisar cuentas de pantalla/dispositivos por separado y excluir la cuenta eliminada salvo decisión expresa.
6. Registrar dominio, configurar DNS/TLS/firewall, desplegar sin abrir todavía la operación cotidiana y ejecutar aceptación por el dominio definitivo. Si se usa un entorno temporal, no confundir sus datos de ensayo con la base productiva limpia.
7. Ejecutar respaldo y recuperación en destino alternativo, comprobar ausencia de simulación y verificar monitoreo/reinicio. Acordar ventana y criterio de rollback antes del primer trabajo real.
8. Abrir operación solo al cerrar P0 y pruebas críticas P1. Conservar la simulación local y la evidencia de auditoría. La bóveda se actualizará con reglas/procedimientos efectivos cuando se despliegue, no con esta propuesta.

## Matriz de comprobación

Todos los estados corresponden a esta auditoría, no a resultados históricos. Las pruebas propuestas no se presentan como ejecutadas.

| Escenario / acción | Resultado esperado | Entorno y método | Estado / evidencia |
|---|---|---|---|
| Compilar solución Release | Cero errores | Windows, `dotnet build SistemaSGOL.slnx -c Release --no-restore` | **Aprobado**: 0 errores, 0 advertencias; `build.log`. No acredita compilación de todas las herramientas. |
| Revisar paquetes NuGet transitivos de la solución | Sin vulnerabilidades reportadas ni error de consulta | Consulta NuGet, `dotnet list ... package --vulnerable --include-transitive --format json` | **Aprobado en ese alcance**: 15 proyectos, 0 vulnerabilidades reportadas, 0 entradas de error; `nuget-audit.json`. |
| Publicar Web linux-x64 | Publicación con archivos estáticos y nativos | Publicación cruzada Windows, `dotnet publish ... -r linux-x64 --self-contained false` | **Aprobado**: `publish-linux.log`; incluye `libSkiaSharp.so`. **No ejecutado** el runtime Linux. |
| Migrar base vacía y repetir | Todas las migraciones, segundo intento sin cambios, modelo coherente | PostgreSQL 18.6 en contenedor aislado, puerto 5558; `SgolDbContext` real | **Aprobado**: 94 migraciones, última `20261105175823_ExpandOperationalImagesToFifty`, sin diferencias de modelo. 8 tareas semilla, 0 personas, 0 asignaciones; `migration-probe.log`. |
| Disponibilidad de app compartida | Salud interna y acceso HTTPS responden | HTTP/HTTPS de lectura, TLS normal | **Aprobado**: readiness interno 200 y acceso HTTPS local/LAN 200. No se autenticó un recorrido funcional. |
| Inventario de cuentas/volumen | Conteos agregados sin cambios | SELECT en simulación compartida | **Aprobado**: conteos indicados; ninguna escritura operativa. |
| Cabeceras HTTPS | CSP, HSTS, nosniff y política de referer según perfil de producción | GET `/acceso` por proxy compartido | **Fallido respecto a objetivo de producción**: HSTS/nosniff ausentes; CSP/Referrer-Policy presentes. No es prueba de todas las rutas. |
| Archivos privados por nombre en Git actual | Sin `.local`, claves y respaldos conocidos | Inventario de paths rastreados | **Aprobado limitado**: 0 candidatos en patrones revisados. No equivale a escaneo de secretos por contenido/historial. |
| Linux Production completo con reinicio | Login/MFA y servicios sobreviven al reboot; sin secretos de desarrollo | Droplet o VM Linux equivalente | **No ejecutado**: todavía no existe paquete ni destino. |
| Bootstrap + MFA + recuperación | Nueva Dirección, enrolamiento y bloqueo de repetición correctos | Base nueva, servicios/formularios reales por HTTPS | **Propuesto**, no ejecutado en esta auditoría. |
| Permisos y ataques a formularios | CSRF, acceso ajeno, payload manipulado y roles rechazados sin efectos | Base aislada, HTTP real + SQL antes/después | **Propuesto**: ejecutar suites Security/RoleAccess/Cookie y casos del producto vigente. |
| Catálogo seleccionado | Dependencias completas; sin simulación, duplicados ni omisiones | Base de preparación aislada + manifiesto y comparaciones | **Propuesto**; selección aún pendiente. |
| Recorrido diario | Catálogo → asignación → ejecución con comentario/checklist/foto/documento → revisión → reportes/historial | Siete cuentas sintéticas, formularios y navegador por proxy Linux | **Propuesto**, incluye permisos y datos históricos de referencia aislados. |
| Procesos, arrastres e incidencias simultáneos | Misma elegibilidad/capacidad, reintento sin duplicados y rollback | Base aislada, pruebas HTTP/SQL con concurrencia | **Propuesto**; incluir recurrencia, reprogramación y bono. |
| Scanner caído y recuperación | Carga falla cerrada y monitor alerta; recuperación sin duplicación | Scanner aislado, fixture benigno/EICAR controlado | **Propuesto**; no detener scanner compartido. |
| Spaces y evidencia histórica | CORS/firma/expiración/denegación anónima y descarga autorizada | Buckets aislados reales en DO | **No ejecutado**: no hay recursos/credenciales DO en este alcance. |
| Carga y crecimiento | Siete sesiones, picos de carga de imágenes, reporte/planificación; sin pérdida ni OOM | VM del tamaño candidato y red representativa | **Propuesto**; fijar umbrales p95, memoria, errores y volumen histórico antes de medir. |
| Backup/restauración y rollback | Recuperar BD, archivos, claves y acceso dentro de RPO/RTO acordados | Destino alternativo vacío, ensayo cronometrado | **Propuesto**, imprescindible antes de apertura. |
| Móvil/temas/cámara/PDF | Funciona por dominio y TLS reales | Chromium/WebKit y dispositivos de trabajo | **Propuesto**; no se ejecutó navegación visual en esta auditoría. |

Evidencias privadas en `.local/deployment-audit-20261106/`. La sonda usa un proyecto temporal que referencia el contexto real; no se añadió al producto. Su contenedor `sgol-deploy-audit-20261106` quedó **detenido**, con datos conservados para diagnóstico. No se instalaron herramientas globales: `dotnet ef` no estaba disponible y se usó EF directamente desde el contexto. La publicación cruzada regeneró locks por RID; se restituyeron únicamente esos archivos inicialmente limpios. El diff final conserva los cambios previos y añade este informe.

## Pendientes de decisión y orden de trabajo

1. **Primero: entrega Linux y configuración segura.** Resolver D01–D04, D07, D10 y D14; el resultado debe ser un paquete desplegable sin secretos y una prueba Production local.
2. **Después: preparación de datos de estreno.** Resolver D05 con una selección revisable; nuevas cuentas/MFA y activación controlada de procesos.
3. **Después: operación recuperable.** D06, D08, D09 y D12; medir siete usuarios, recuperación y margen económico. D13 aplica especialmente a la alternativa administrada.
4. **Finalmente: dominio, recursos y apertura.** Elegir nombre de dominio, confirmar costo total de contratación y región, ejecutar aceptación y actualizar procedimientos vigentes.

Decisiones de negocio aún necesarias: catálogo exacto, personas/cuentas reales, fecha de inicio, retención de evidencia, tolerancia a pérdida de trabajo y a interrupción, responsable de recibir alertas. Propuesta para discutir: RPO de una hora y RTO de cuatro horas; son objetivos, no capacidades demostradas ni reglas ya aprobadas. No hay compras, DNS, publicación, commit o push realizados por esta auditoría.
