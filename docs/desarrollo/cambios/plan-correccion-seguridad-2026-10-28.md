# Plan de corrección de seguridad — 28/10/2026

## Objetivo, alcance y estado

Corregir los cuatro hallazgos confirmados de la [auditoría](../auditoria-seguridad-2026-10-28.md), contener los avisos de almacenamiento y completar las verificaciones pendientes con mayor impacto. El resultado debe preservar la operación y permitir demostrar que cada defensa funciona en formularios, API, servicios y almacenamiento reales.

**Estado: implementado por paquetes; resultados reales y límites registrados abajo.** La preparación aislada precedió a la activación habitual. Véase [implementación, recuperación, activación y riesgos residuales](validacion-seguridad-2026-10-28.md). El usuario autorizó posteriormente commit y publicación del código en GitHub; secretos, bases, respaldos y evidencias generadas permanecen fuera de Git.

Clasificación: funcional e infraestructura; mixto en el recorrido Razor del cierre fallido. Se verificaron la composición real de Web y sus formularios, además de servicios, PostgreSQL, almacenamiento y navegador. Las secciones de diseño conservan el alcance original; los resultados siguientes indican qué se ejecutó.

El entorno actual es desarrollo con datos simulados. Las regresiones, ataques, restauraciones y cargas usarán servicios separados, bases `sgol_task_checks_security_*`, almacenamiento y red Docker propios, directorio temporal y credenciales inventadas. No basta cambiar el puerto Web: DB, buckets, scanner y rutas de archivos también deben identificarse. No se borrará la simulación diaria.

## Decisiones confirmadas y reglas que se conservan

El usuario confirmó durante la planificación:

1. **Cerrar sesión revoca solo la sesión actual**, conservando otras sesiones del mismo usuario.
2. **Al instalar la corrección se pedirá iniciar sesión una vez a todos los usuarios con cookies antiguas.** Estas cookies no contienen identificador revocable; no habrá período de aceptación heredada.

Conservar la duración absoluta de 24 horas desde autenticación, sin agregar expiración por inactividad. Mantener CSRF, HttpOnly/Secure/SameSite, permisos y rol vigente consultados en servidor. Los mecanismos actuales de cambio de contraseña, bloqueo administrativo y recuperación deben seguir invalidando tickets mediante estado/stamp; no sustituirlos por la revocación individual.

No modificar cargas, responsables, fechas operativas, evidencia, historial ni asignaciones por una migración de seguridad. Mantener el esquema separado de PANTALLA y su acceso de solo lectura; no convertir sus tickets en sesiones personales. No obligar a habilitar MFA en la simulación como efecto lateral: el perfil de publicación sí deberá exigirlo.

## Orden y entregables

La prioridad de implementación no equivale a severidad de explotación. La contención de infraestructura y el cierre de sesión pueden prepararse independientemente; el cambio de cuenta DB debe probarse antes del cambio efectivo de conexión.

| Orden | Paquete | Prioridad de trabajo | Entregable y condición de salida |
|---|---|---|---|
| 0 | Base de pruebas y recuperación | Previo a activar cambios | Pruebas mantenibles, aislamiento comprobado y restauración ensayada |
| 1 | SEC-03: proteger secretos; contener SeaweedFS | Inmediata | Secretos con permisos mínimos y administración S3 inaccesible sin autorización desde redes no requeridas |
| 2 | SEC-01: sesiones revocables | Alta | Replay rechazado; segunda sesión intacta; transición sin tickets heredados |
| 3 | SEC-02: separar privilegios PostgreSQL | Alta | Web/Worker sin superusuario ni DDL; migración y operación completas con cuentas separadas |
| 4 | SEC-04 y contención de solicitudes | Alta para readiness; calibración posterior para otras rutas | Probes protegidos, trabajo interno acotado y operación normal bajo carga hostil |
| 5 | Cobertura SQLi, IDOR, XSS, MFA y cadena de suministro | Antes de considerar cerrado el programa | Matriz ampliada ejecutada; nuevos defectos corregidos o pendientes explícitos |
| 6 | Activación y comprobación del entorno | Tras aprobar pruebas por paquete | Versión/configuración efectivas verificadas, datos preservados y evidencias finales |

Cada paquete debe ser revisable por separado, con su prueba de regresión y notas de activación. No esperar a terminar todo el programa para tener una corrección comprobable de logout. Tampoco declarar el proyecto completamente seguro tras resolver solo esos cuatro hallazgos.

## 0. Preparación y trazabilidad

- Convertir los reproductores ignorados de `artifacts/security-audit/auth-harness/` y `media-probes/` en comprobaciones mantenibles dentro de `tools/`, con guardas de destino y limpieza acotada. Los artefactos ignorados no son una suite permanente.
- Para HTTP integrado levantar la aplicación con su composición real y el proxy cuando corresponda. El harness anterior solo cubre un subconjunto del arranque; conservarlo como prueba dirigida, sin usarlo como sustituto del proceso completo.
- Compilar con salida de artefactos aislada para evitar DLL bloqueadas por el servicio activo. Inventariar ruta binaria, proceso, configuración, base, almacenamiento, versión de imagen y origen de autenticación de cada prueba.
- Ensayar respaldo/restauración de DB y almacenamiento en destinos aislados; verificar claves de Data Protection y acceso a evidencias restauradas. No regenerar el keyring para cerrar sesiones: rompería otras protecciones cifradas.
- Registrar snapshots de conteos y hashes de campos relevantes de asignaciones, avances, evidencias, auditoría y configuración antes/después de migraciones. Conservar evidencia de errores reproducibles, sin credenciales/cookies.

## 1. Secretos locales y almacenamiento

### SEC-03: protección de secretos existentes y nuevos

**Fuentes reales:** `scripts/iniciar-local.ps1`, `local-env.ps1`, `iniciar-red.ps1`, `red-env.ps1`, `run-local.ps1`; PFX en `tools/Sgol.LanProxy/Program.cs`; configuración y copias descritas en SEC-03 de la auditoría.

1. Inventariar las identidades que ejecutan Web, Worker, proxy, Docker Compose y tareas de mantenimiento. Aplicar ACL explícita mínima al directorio de secretos, archivos existentes, archivos nuevos, temporales y respaldos; no conceder lectura por herencia al grupo adicional observado. Conservar acceso de las identidades necesarias, SYSTEM y administradores según operación.
2. Extraer contraseñas de JSON de metadatos. Para este host Windows proponer DPAPI CurrentUser bajo la identidad efectiva del servicio, con un proveedor común de lectura para los scripts. DPAPI no protege frente a un proceso ya ejecutándose como ese mismo usuario; el aislamiento de identidades sigue siendo necesario. Para despliegues Linux/servicios diferentes usar un proveedor de secretos compatible, no exportar blobs DPAPI como solución portable.
3. Preferir cargar el certificado TLS desde el almacén Windows por referencia, si el proxy puede acceder con su identidad. Si se necesita PFX portable, mantener archivo y contraseña protegidos por mecanismos distintos del JSON público de metadatos. Preservar la clave existente durante la migración para no cambiar innecesariamente la confianza de los dispositivos LAN.
4. Evitar el `compose.env` permanente. Para servicios nuevos, usar entrada de secreto soportada por la imagen; donde no exista, resolver la credencial al crear el proceso y limitar el archivo temporal, con limpieza ante error. Documentar que administradores de Docker y del host pueden inspeccionar secretos del contenedor: no presentar variables de entorno como cifrado.
5. Dejar de exportar la contraseña de bootstrap a Web/Worker/proxy. Usarla solo durante el bootstrap necesario y retirar copias de acceso inicial cuando ya no sean útiles. Revisar logs, respaldos y herramientas consumidoras antes de retirar formatos antiguos.
6. Migrar en forma recuperable: validar lectura con el formato nuevo por todas las identidades antes de retirar el anterior. Rotar credenciales DB/S3 tras validar consumidores; cambiar solo `POSTGRES_PASSWORD` en Compose no rota la contraseña de una base ya inicializada. Cambiar el rol real y los clientes en orden controlado. La reemisión TLS, si llega a ser necesaria por evidencia de exposición, requerirá redistribuir confianza a los dispositivos afectados.

**Aceptación:** un principal local sin permiso no puede leer los archivos; servicios autorizados arrancan local y LAN; repetir la migración no pierde secretos ni cambia los datos; no hay claves/cookies en logs, argumentos o Git. Una imposibilidad de probar con otra identidad queda como validación pendiente, no aprobada por inspección de ACL.

### SeaweedFS: mitigación verificable y corrección de dependencia

**Fuentes:** `compose.local.yaml`, `tools/Sgol.LanProxy/Program.cs`, `S3PrivateObjectStorage`, `EvidenceInfrastructureServiceCollectionExtensions` y ambos avisos enlazados al final.

Los dos avisos oficiales consultados el 28/10/2026 indican **Patched versions: None**. Por ello el plan no promete que subir un número de versión cierre los defectos.

1. Reproducir en stack aislado los listeners y conectividad efectiva desde host/LAN, proxy y contenedores vecinos. Inventariar qué clientes necesitan administración gRPC y cuáles solo S3 HTTP.
2. Restringir administración gRPC con controles soportados de autenticación mutua/TLS o listener/red segregada; validar la configuración efectiva. No confiar únicamente en que el puerto no esté publicado en el host: contenedores de la misma red pueden alcanzarlo.
3. Mantener acceso por defecto denegado a operaciones anónimas de buckets y administración. Revisar identidades S3: separar el aprovisionamiento de buckets/CORS del uso runtime. Autorizar al runtime solamente las acciones y buckets requeridos por sus métodos reales.
4. Inventariar métodos S3 usados por SDK, Worker y proxy. Si POST Object multipart no se usa, bloquearlo en el límite alcanzable por navegador y en las rutas internas pertinentes; verificar que no bloquee llamadas SDK necesarias. Una denegación en proxy no corrige clientes que accedan directamente al backend.
5. Elegir imagen corregida por evidencia del proveedor/código y repetir los casos negativos, fijándola por digest. Si no existe una corrección verificable, documentar mitigación y riesgo residual; evaluar una corrección mantenida o sustitución compatible como decisión técnica aparte, con coste de migración explícito.
6. Comprobar PUT/GET firmados, expiración, límites, CORS, cuarentena, escaneo, promoción, descarga, limpieza y respaldo de objetos existentes. Una actualización se prueba con copia de volúmenes, nunca suponiendo que bajar la imagen después revierta el formato de datos.

**Aceptación:** ningún vecino sin autorización puede administrar identidades; operaciones prohibidas no crean objetos ni cuentas; firmas válidas y evidencia histórica siguen funcionando. Registrar por separado «corregido», «mitigado» y «alcance no demostrado» para cada aviso.

## 2. SEC-01: sesiones personales revocables

**Archivos/contratos:** `src/Modules/Identity/Contracts/Authentication.cs`; `HostedAuthenticationInfrastructure.cs` (`AuthenticationPrincipalFactory`, `HostedCookieEvents`); `EfHostedAuthenticationService.cs` (`BuildSessionAsync`, `ValidateSessionAsync`, `GetSessionAsync`, `RecordLogoutAsync`); `AuthenticationApiEndpoints.cs` (`LogoutAsync` y emisión de cookies); entidades/configuración Identity y `SgolDbContext`; puentes de API/Razor, Access y pruebas de cookies. Revisar también `EfAccountAdministrationService`, recuperación y regeneración MFA.

1. Agregar un registro persistido de sesión personal con identificador aleatorio, usuario, emisión, expiración absoluta, método de autenticación y revocación/motivo. Índices por identificador, usuario y expiración; no almacenar el valor de cookie ni copiar contraseñas/secretos TOTP. Esquema nuevo, sin reescribir historial operacional.
2. Crear la sesión al terminar exitosamente el flujo de autenticación, dentro de la transacción pertinente. Cubrir password en desarrollo, confirmación/enrolamiento MFA, TOTP, recuperación y renovación tras cambio de contraseña. No crear una sesión plena al emitir solo un desafío ni ampliar permisos de flujos restringidos de recuperación.
3. Emitir la cookie únicamente tras confirmar la sesión persistida; agregar su identificador al ticket protegido. Mantener el mismo identificador al renovar cookie y la misma expiración absoluta. Propagarlo por validación de principal y consulta de sesión sin exponer el ticket a JS.
4. Exigir registro existente, usuario coincidente, sesión no revocada y no vencida, además de stamp, cuenta y permisos actuales. No reconstruir una sesión faltante durante validación. Sin caché positiva inicial de revocación: una respuesta exitosa de logout debe invalidar inmediatamente las validaciones posteriores, también con varias instancias.
5. Sustituir el logout de solo auditoría por revocación individual y evento de auditoría en una transacción. Responder éxito y borrar cookie después del commit. Reintentos no deben reactivar la sesión ni revocar otras. Si falla persistencia, no afirmar «sesión cerrada» mientras el ticket siga utilizable; devolver el error seguro y permitir reintento.
6. Rechazar cookies personales antiguas sin identificador al activar la nueva versión. Comunicar la necesidad de iniciar sesión otra vez; mantener Data Protection, evidencia y tickets del esquema PANTALLA. Coordinar activación para que una instancia antigua no siga aceptando cookies heredadas.
7. Verificar carreras logout/renovación/cambio de contraseña: la renovación no debe resucitar una sesión revocada. Una petición que ya pasó autenticación antes del commit de logout puede terminar; no prometer cancelar trabajo ya autorizado en vuelo. Toda validación iniciada tras la revocación debe fallar.
8. Limpiar únicamente sesiones vencidas según una retención definida y documentada, preservando auditoría. El criterio de borrado nunca debe permitir aceptar otra vez una cookie; «registro ausente» siempre es rechazo. No añadir escritura por solicitud para last-seen sin necesidad.

**Interfaz:** reutilizar Access y errores existentes (`Pages/Access`, `Pages/Shared/_SystemError.cshtml`, `AccessNotice`), siguiendo `ESTILO_VISUAL_V2.md`. Mostrar sesión inválida/renovación requerida o fallo real de cierre; mantener foco, teclado, temas y móvil cuando cambie contenido. No se requiere una nueva pantalla de gestión de dispositivos para corregir logout.

## 3. SEC-02: cuenta runtime con mínimos privilegios

**Fuentes:** `compose.local.yaml`, scripts de arranque/entorno, `tools/Sgol.LocalSetup/Program.cs:20–30`, Admin, Worker, Operations, `PersistenceServiceCollectionExtensions`, `PostgreSqlXmlRepository` y migraciones/triggers.

El preparador local llama a `Database.MigrateAsync` con `ConnectionStrings__Sgol`. Web/Worker reciben después la misma conexión. Revocar privilegios sin separar ese paso impediría preparar futuras versiones.

1. Introducir credenciales distintas para administración inicial, migrador/propietario y runtime. Runtime: LOGIN, sin SUPERUSER/CREATEDB/CREATEROLE/BYPASSRLS/REPLICATION, sin pertenencia al propietario y sin capacidad de `SET ROLE` privilegiado. Web y Worker nunca reciben el secreto migrador.
2. Aplicar esquema con el migrador y después ejecutar bootstrap/seeding necesario con los permisos adecuados. Acotar por proceso la variable usada para migrar, para que no se herede al arrancar Web/Worker/proxy. Mantener las guardas de entorno/destino del preparador.
3. Inventariar permisos por operación: CONNECT/USAGE, DML por tabla, secuencias y funciones necesarias. Revisar auditoría append-only, tablas históricas, limpieza de uploads, jobs/outbox, triggers y keyring antes de conceder permisos. No usar `GRANT ALL` ni conceder UPDATE/DELETE a auditoría por comodidad.
4. Mantener ownership con un rol de esquema separado y prohibir CREATE al runtime en esquemas relevantes. Revisar `PUBLIC` y privilegios predeterminados de los objetos creados **por el migrador**; futuras migraciones deben conservar el modelo. No introducir SECURITY DEFINER general; si resulta imprescindible, limitar función, `search_path`, propietario y EXECUTE.
5. Migrar una copia de la base existente sin renombrar usuarios de negocio ni reconstruir tablas. Crear la nueva cuenta runtime, probarla y cambiar conexiones; cerrar pools antiguos al hacer el cambio efectivo. La credencial administradora sigue reservada a mantenimiento, sin volver a Web como fallback.
6. Separar cuenta de respaldo/restauración de runtime cuando sus operaciones lo exijan. Verificar que conexiones de controles y herramientas no usen accidentalmente la credencial privilegiada para «aprobar» pruebas que fallan con la real.

**Aceptación:** login, planificación, entrega, revisión, descargas, auditoría, procesos, jobs habilitados y keyring funcionan con la cuenta nueva; CREATE ROLE/DB, ALTER/DROP y acceso no concedido fallan sin efectos. La siguiente migración crea objetos utilizables por runtime y el arranque repetido no cambia propietarios ni concede privilegios adicionales.

## 4. SEC-04 y abuso de solicitudes

**Fuentes:** `Program.cs`, `HostedAuthenticationInfrastructure`, `InternalLoginOrigin`, `PerformanceWork`, checks PostgreSQL/DataProtection/Evidence y proxy LAN.

### Readiness

1. Mantener `/health/live` barato y sin depender de DB/S3. Proponer readiness solo para monitores internos: por defecto loopback, lista explícita para despliegues que lo requieran; bloquear su publicación en el proxy LAN. Inventariar primero los probes existentes para no romper supervisión.
2. Comprobar el origen efectivo usando conexión y proxy conocido; no conceder acceso por un header `X-Forwarded-For` enviado libremente por el cliente. La petición desde el proxy local no convierte al navegador remoto en monitor autorizado.
3. Coalescer comprobaciones concurrentes en una única ejecución por instancia y usar resultado con vigencia corta. Punto inicial de ensayo: 5 segundos y timeout total de 3 segundos. Tras vencer vigencia, no devolver éxito viejo indefinidamente; fallo/timeout produce 503. El aborto de un cliente no debe cancelar el probe compartido para todos.
4. Acotar frecuencia de monitores y devolver estados mínimos sin detalles internos. Conservar la evidencia de que una caída real de DB/S3 se refleja dentro de la ventana documentada. No usar los tiempos iniciales como SLA sin medirlos.

### Rutas API, formularios y archivos

La cuota de login ya funciona; conservarla. Completar un inventario de rutas y handlers Razor, incluidas variantes de casing, legacy habilitado, carga multipart, reportes, fotos y almacenamiento firmado.

- Incorporar límites tempranos por origen para trabajo previo a autenticación y límites por identidad para operaciones autenticadas; presupuesto global/concurrencia acotada para proteger recursos compartidos. Contemplar equipos tras la misma IP, Pantalla y refresco del tablero para evitar bloquear operación legítima.
- Resolver el orden de middleware con pruebas: hoy `UseRateLimiter` precede autenticación; una nueva política por usuario no puede asumir que `HttpContext.User` ya está disponible en ese punto. No consumir dos veces el ticket de origen interno de login al añadir políticas.
- Establecer límites por operación para tamaño, expansión, número de archivos, plazos, colas y cancelación. Mantener los límites existentes de Excel/fotos y medir DB, CPU, memoria, disco temporal y scanner antes de aumentar umbrales.
- Contener en proxy/almacenamiento los accesos firmados que no pasan por Web. La cuota de la API no limita por sí misma una descarga directa a S3.
- Responder 429 con Retry-After para cuota y 503 para capacidad temporal; incluir motivo comprensible y correlationId, sin reintentar automáticamente escrituras no idempotentes. No registrar cuerpos de autenticación ni query strings firmadas. Acotar crecimiento de particiones y volumen de logs del propio ataque.
- Calibrar en stack aislado ráfagas, carga sostenida limitada y clientes lentos, verificando recuperación y tráfico legítimo concurrente. Los ensayos tendrán presupuesto de solicitudes/tiempo/memoria y criterio de aborto antes de lanzarlos; no generar una saturación sin límites.

**Aceptación:** llamadas no autorizadas no disparan checks internos; la ráfaga permitida comparte una ejecución y no amplifica S3 por cliente; abuso recibe rechazo antes del trabajo caro; sesiones, pantalla, reportes y cargas legítimas siguen operando.

## 5. Ampliación de cobertura y prevención de regresiones

La auditoría no confirmó SQL injection. Se conservará esa distinción: aquí se propone aumentar cobertura, no «arreglar» una inyección inexistente por evidencia.

- **SQLi:** recorrer login, búsqueda/listados, filtros, ordenamientos, cursores, reportes y exportaciones por HTTP real hasta DB. Casos de comillas, comentarios, tautologías y errores tipados deben ser datos o rechazarse sin ampliar resultados ni filtrar SQL. Revisar todos los sinks raw y herramientas privilegiadas; guardas estáticas sobre concatenaciones peligrosas sin prohibir las APIs parametrizadas seguras. No cambiar parametrización por escapes manuales ni agregar regex de palabras SQL como defensa principal.
- **IDOR/permisos:** dos usuarios del mismo rol y usuarios de roles distintos; cambiar identificadores de tarea, asignación, proceso, foto, archivo, plantilla, plan y revisión. Cubrir GET/POST/descargas, endpoints actuales y legacy solo si está habilitado. Verificar ausencia de cambios parciales y filtraciones por mensajes.
- **XSS:** persistir textos/etiquetas/nombres con payloads inertes de prueba; ejecutar navegador real en formularios, historial, tablero refrescado y reportes/exportaciones. Revisar Razor, `Html.Raw`, DOM y contenido exportado, además de CSP. No deshabilitar CSP para que una prueba pase.
- **MFA:** ejecutar enrolamiento, TOTP, recuperación, regeneración, códigos reutilizados, concurrencia y desafíos vencidos con MFA habilitado; mantener desarrollo sin MFA como perfil explícito de simulación. Publicación debe rechazar configuración sin MFA si se exige para ese destino, sin inferir seguridad solo del nombre del entorno.
- **Cadena de suministro/host:** inventario completo de proyectos/herramientas, dependencias nativas y JS vendorizado; SBOM/escaneo de imágenes y SO, verificación de avisos oficiales, secretos en todo el historial Git y comprobación de red/firewall/TLS. No copiar valores encontrados a informes.
- Integrar comprobaciones rápidas de seguridad en el flujo de validación del repositorio y conservar evidencia de consultas a catálogos. Una tarea CI o un monitor recurrente solo se configura como parte de la implementación acordada; este plan no crea automatizaciones.

## Matriz de aceptación ejecutada

Evidencia privada ignorada: `artifacts/security-audit/implementation/`. `results.json` y el log de aceptación final corresponden a una tanda íntegra; no se suman repeticiones. Las salvedades indican exactamente qué variante no se ejecutó. El [reporte de validación](validacion-seguridad-2026-10-28.md) explica composición, comandos, fallos corregidos y límites.

| ID | Precondición y acción | Resultado esperado | Prueba y entorno | Estado |
|---|---|---|---|---|
| S01 | Dos sesiones A/B; logout A; replay copia A | A rechazada; B conserva acceso; auditoría coherente | HTTP con autenticación/EF reales, DB aislada | Aprobado: HTTP real A rechazada/B intacta; una revocación y auditoría. |
| S02 | Renovar A y cerrar desde otra pestaña | Todos los tickets de A rechazados; sin resurrección | HTTP concurrente y varias instancias | Aprobado: renovación conserva ID/plazo y ocho revocaciones concurrentes; no ejecutado en varias instancias simultáneas. |
| S03 | Cookie antigua al activar versión | Reautenticación una vez; login nuevo funciona | Copia aislada + aplicación real | Aprobado: ticket heredado protegido con keyring real rechazado 401; login nuevo funciona. |
| S04 | Password/reset/bloqueo/cambio de rol | Tickets inválidos o permisos actuales; sin escalamiento | Servicios y HTTP, DB aislada | Aprobado password/rol/stamp/regeneración MFA; no ejecutado reset/bloqueo administrativo completo en esta tanda. |
| S05 | MFA/recovery y login desarrollo | Sesión correcta en cada camino; no emitir antes del commit | Formularios/API reales, ambos perfiles | Aprobado: password sin MFA, enrolamiento, verificación y regeneración por HTTP; sin cookie plena antes de completar. |
| S06 | Fallo DB/auditoría durante logout; reintento | No falso éxito; operación recuperable sin revocar B | Fallos inducidos, DB aislada | Aprobado: trigger de fallo aislado, API 500/Razor 503, rollback, cookie retenida y reintento; concurrencia sin duplicar evento. |
| S07 | 24h, inactividad y reinicio | Expiración absoluta conservada; revocación persistente | Reloj controlado + reinicio aislado | Aprobado: 23h inactivo/corte exacto 24h y persistencia DB; no ejecutado replay específico después de reiniciar en varias instancias. |
| S08 | Cookie PANTALLA y renovación | Solo lectura vigente, sin exigir sesión personal | Cliente/servidor aislados | Aprobado: vinculación/ticket/cookie PANTALLA real, tablero 200 y escritura 403 sin fila personal. |
| D01 | Runtime intenta crear rol/base/DDL o asumir owner | Denegado sin efecto; flags mínimos | SQL real con cuenta runtime aislada | Aprobado: SQL real 42501 para privilegios, DDL, SET ROLE y mutación de auditoría. |
| D02 | Migración nueva y base existente con datos | Runtime opera sin DDL; historial idéntico | DB vacía + copia de simulación aislada | Aprobado: base vacía y copia restaurada, defaults del migrador, runtime operativo; 88 tablas existentes sin cambios. |
| D03 | Entrega/revisión/planes/jobs/auditoría/keyring | Recorridos completos con privilegios mínimos | Formularios, servicios, DB/storage reales | Aprobado: login, catálogo, formularios de entrega, revisión, keyring, S3 y job de limpieza/reintento con runtime. No se declara recorrida toda generación de planes/outbox legacy. |
| K01 | Identidad permitida y otra sin permiso leen secretos | Servicio permitido funciona; otro usuario denegado | Directorio y cuentas de prueba Windows | Aprobado lectura permitida/denegación con token Windows restringido, incluido almacén habitual; no ejecutado desde otra cuenta Windows completa. |
| K02 | Migrar secretos; interrumpir y repetir | Recuperación sin pérdida; ningún texto secreto en logs | Copia de configuración inventada | Aprobado: roundtrip DPAPI, metadatos limpios, reemplazo verificado y repetición; interrupción arbitraria entre todos los pasos no ejecutada. |
| K03 | Rotar DB/S3; reiniciar local/LAN/Worker | Todos los consumidores usan secreto nuevo | Stack aislado y pruebas de conexión | Aprobado: rotación DB con rechazo de contraseña anterior; storage restaurado rechaza identidad retirada y sirve identidad nueva; activación local/LAN verificada. |
| O01 | Vecino sin credenciales llama administración gRPC | Rechazo, ninguna identidad creada/modificada | Red Docker y almacenamiento aislados | Aprobado: vecino en red Docker alcanza gateway 8080 y no administración 18333; listeners efectivos loopback. |
| O02 | POST Object evita política deny; PUT permitido | Deny efectivo o ruta no accesible; caso válido funciona | Storage/proxy reales aislados | Aprobado: POST 405 antes de storage, PUT firmado válido; mitigado por ruta inaccesible, sin atribuir parche a SeaweedFS. |
| O03 | Firma vencida, CORS, escaneo y descarga histórica | Rechazos correctos y evidencia válida conservada | Navegador/SDK + copia storage | Aprobado: firmas/expiración/CORS, SHA256, ClamAV, EICAR, promoción y limpieza. Buckets históricos vacíos; evidencia DB conservada por fingerprints. |
| H01 | Cliente LAN pide readiness y falsifica origen | Denegación antes del probe, liveness operativo | Proxy y app reales aislados | Aprobado: header inventado denegado y readiness LAN 403 antes del backend; liveness 200. |
| H02 | Ráfaga de monitor, DB/S3 caído y cliente aborta | Trabajo coalescido/acotado; 503 dentro ventana | Checks reales instrumentados aislados | Aprobado: coalescencia instrumentada, aborto, timeout y HTTP real storage caído/recuperado 200-503-200. |
| H03 | Carga hostil más actividad legítima | Rechazo temprano, recursos acotados y recuperación | HTTP/DB/scanner/storage separados | Aprobado para ráfaga acotada 650 solicitudes, recuperación/liveness y cliente lento aislado; no ejecutado derribo distribuido/sostenido. |
| I01 | Payload SQLi por login/filtros/cursores/export | Sin bypass, SQL filtrado ni modificación extra | HTTP real + DB aislada | Aprobado: cuatro payloads login, búsquedas/filtro real y exportación HTTP; sin bypass/filtrado SQL ni ampliación de filas. |
| I02 | Usuario altera IDs ajenos en lectura/escritura | Sin acceso ni cambios parciales | Roles/usuarios fixture, stack aislado | Aprobado: asignación, galería, bytes de foto/adjunto, plan/proceso/revisión por rol y plantilla personal ajena; datos rechazados intactos. No es fuzzing exhaustivo de todas las combinaciones. |
| I03 | Textos XSS persistidos y fragmentos refrescados | Sin ejecución; contenido seguro en consumidores | Navegador real, temas/tamaños afectados | Aprobado caso persistido en Razor/navegador real sin handlers/script/diálogo; no ejecutada campaña exhaustiva de campos/exportaciones/temas. |
| I04 | MFA repetido/concurrente, códigos y desafíos vencidos | Sin replay ni sesión indebida | HTTP real con MFA habilitado | Aprobado: TOTP y recovery reutilizados, desafío expirado, regeneración y completado concurrente con un éxito. |
| V01 | Mensajes nuevos de logout/sesión/429 | Comprensibles, foco/teclado y sin pérdida silenciosa | Navegador real móvil/escritorio y temas | Aprobado navegador real, desktop/320px, teclado/foco, cierre exitoso y fallo recuperable; tema oscuro no ejecutado. |
| R01 | Actualizar imagen/migración y ensayar recuperación | DB, claves y objetos recuperables; defensas activas | Copias aisladas; no downgrade sobre volúmenes originales | Aprobado: restauración DB/copia storage arrancada, 88 fingerprints, claves efectivas copiadas y descifrado; primer comando storage defectuoso invalidado y repetido correctamente. |

No aplicable: combinaciones de cargas/horarios por cambio de catálogo, porque no se cambia definición de tareas; su conservación sí se comprueba por snapshots y recorridos D02/D03. Las pruebas de host externo se registrarán como no ejecutadas si no se dispone de otra máquina/red, explicando la limitación.

## Línea base de auditoría, anterior a las correcciones

La auditoría ya reprodujo replay tras logout (200 después de 204); confirmó flags privilegiados DB y ACL adicional; observó 64 readiness sin cuota en un harness parcial. Pasaron CSRF omitido, un caso SQLi de login, tres accesos con rol degradado, cuotas login y sus bypass probados, diez casos de archivos y diez comprobaciones de rendimiento. Son la línea base previa, no resultados de las correcciones.

Las pruebas anteriores de archivos no equivalen a flujo integrado antivirus/storage; el readiness anterior no incluyó S3; no se ejecutó IDOR/XSS exhaustivo ni MFA integrado. Usar los JSON e informes de `artifacts/security-audit/` como evidencias locales y el informe de auditoría como registro versionable.

## Activación, recuperación y cierre

1. Revalidar destinos y estado del repositorio antes de implementar; preservar cambios previos. Preparar y probar cada paquete sin escrituras ajenas al alcance.
2. Antes del cambio efectivo de secretos/privilegios/imagen, completar respaldo y restauración ensayada, inventario de consumidores y comprobación de configuración nueva. Las migraciones deben ser repetibles y fallar sin cambios parciales.
3. Activar sesiones con esquema disponible y todas las instancias compatibles; emitir aviso de reingreso confirmado por el usuario. No permitir un fallback que acepte cookies antiguas ni elimine revocaciones.
4. Cambiar cuenta runtime solo cuando servicios y migraciones separados estén comprobados. Si falla la activación, mantener el servicio fuera de cambios operativos hasta corregir conexión/permisos; no restaurar superusuario silenciosamente en Web.
5. Si hace falta recuperar almacenamiento, usar copia compatible y verificar objetos/hash y claves. Restaurar un snapshot puede recuperar sesiones anteriores a revocaciones: invalidar sesiones restauradas antes de abrir acceso, sin borrar auditoría ni keyring.
6. Confirmar proceso/binario/configuración efectivos local y LAN; validar login, entrega, revisión, historial, descarga, tablero y PANTALLA. Comparar snapshots de datos operativos.
7. Cerrar cada SEC con prueba de aceptación y riesgo residual. Un aviso contenido se cierra como mitigado, no como corregido por actualización sin evidencia. Mantener abiertos los escenarios críticos no ejecutados.

No quedan decisiones de negocio bloqueantes para SEC-01: ambas fueron confirmadas. El detalle de cuotas, identidades de servicio y compatibilidad de almacenamiento se resolverá por inventario y pruebas técnicas antes de activarlo; cualquier cambio posterior de permisos de negocio, duración de sesión o comportamiento operativo requerirá una decisión explícita.

## Referencias técnicas consultadas

- [Cookies y validación de principal en ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0).
- [Privilegios y ownership en PostgreSQL 18](https://www.postgresql.org/docs/18/ddl-priv.html).
- [SeaweedFS: administración gRPC sin autenticación](https://github.com/seaweedfs/seaweedfs/security/advisories/GHSA-5fx4-c9qp-36jc).
- [SeaweedFS: políticas omitidas en POST Object](https://github.com/seaweedfs/seaweedfs/security/advisories/GHSA-qmx6-fpcw-q3xg).

## Cierre de implementación y pendientes explícitos

Se conservó MFA desactivado en la simulación por la aclaración vigente del usuario. Las 24 horas absolutas y la revocación exclusiva de la sesión actual se mantuvieron. No se reinicializó la simulación ni se borraron asignaciones/evidencia/historial/keyring.

Los cuatro SEC tienen correcciones de aplicación/configuración y evidencia dirigida. SeaweedFS queda **mitigado**, no corregido por versión: ambos avisos oficiales seguían sin patched version. El catálogo de imágenes detectó avisos nativos pendientes de triage, y el gateway reducido conserva uno de musl. No se declara el sistema completamente seguro ni cubiertas variantes no ejecutadas.

La cuenta Windows compartida sigue siendo una frontera residual: DPAPI no aísla un proceso que ya se ejecuta como ese mismo usuario. Varias instancias, otro equipo/principal Windows, campañas exhaustivas, cargas distribuidas y auditoría externa del host quedan delimitadas en el reporte; no son resultados aprobados por inspección.
