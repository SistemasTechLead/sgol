# Implementación y validación de seguridad, 28/10/2026

Este documento complementa el [plan](plan-correccion-seguridad-2026-10-28.md) y conserva la [auditoría original](../auditoria-seguridad-2026-10-28.md) como línea base. Los únicos cambios previos eran esos dos documentos sin seguimiento; se conservó el informe original. Tras terminar la implementación, el usuario autorizó commit, push y publicación en GitHub. Solo se publica código, migraciones, pruebas mantenibles y documentación; secretos, bases, respaldos y evidencias generadas permanecen locales e ignorados.

## Implementación

| Paquete | Cambios y límites |
|---|---|
| Secretos | `scripts/secret-store.ps1`: DPAPI CurrentUser, comprobación de lectura antes del reemplazo atómico, ACL exclusiva de usuario ejecutor/SYSTEM/administradores, migración repetible de `config.json` y `red.json`. Certificado LAN cargado por thumbprint desde CurrentUser/My, conservando confianza y clave. Sin contraseña bootstrap en Web/Worker/proxy ni `compose.env` permanente. La imagen S3 exige un archivo nativo de identidades: está separado de metadatos, protegido por ACL y montado de solo lectura. Docker/administradores siguen pudiendo inspeccionarlo. |
| Sesiones | `personal_session`, migración `20261028222902_AddPersonalSessions`, claim protegido `sgol:session_id`, registro creado antes del commit/emisión del ticket. Validación consulta registro y estado/stamp/rol actuales sin caché positiva. Logout bloquea la fila y confirma revocación y `AUTH_LOGOUT` en una transacción; reintentos no duplican eventos. Recuperación MFA incompleta no crea sesión utilizable. Cookies heredadas sin ID rechazadas, sin tocar keyring ni PANTALLA. |
| Error de cierre | Se corrigió también `RazorAntiforgeryBridge` y `MyWork/Index`: fallo de logout conserva cookie, muestra 503 y permite reintentar; no confirma cierre. Se reutilizan alertas, estilos y navegación existentes; foco en el aviso. |
| PostgreSQL | `Sgol.DatabaseSecurity`: mantenimiento separado, migrador LOGIN propietario sin superusuario y runtime sin propiedad, membresías, CREATE de esquema/base/rol, BYPASSRLS ni REPLICATION. DML explícito, secuencias y defaults del migrador; runtime no modifica auditoría ni historial EF. `LocalSetup` ya no migra. Web y Worker verifican privilegios al arrancar y fallan si reciben una cuenta privilegiada. `run-job` utiliza runtime para su infraestructura; las conexiones de backup/restore siguen siendo opciones separadas de mantenimiento. |
| Readiness/solicitudes | Readiness solo loopback sin headers de forwarding; bloqueo adicional en proxy LAN. Resultado compartido por instancia, caché 5 s, timeout 3 s, sin cancelar el probe compartido por aborto de cliente ni acumular checks que ignoren cancelación. Cuotas acotadas a 256 particiones, 600/min por origen y 300/min por identidad, concurrencia global 64 sin cola, gates existentes de operaciones, cuerpo 32 MiB, headers 10 s y mínimo de cuerpo 240 B/s. Plazo total máximo configurado de 300 s. 429/503 incluyen motivo, correlationId y Retry-After donde corresponde. Liveness permanece barato. |
| SeaweedFS | Se conserva 4.45 por digest; no se atribuye corrección a una actualización. Todos sus listeners se atan a loopback **dentro del contenedor** y solo el gateway comparte su namespace. Desde contenedores vecinos se alcanza 8080, no 18333. Gateway bloquea todo POST, incluido POST Object, antes del storage; firma Host preservada y cargas PUT funcionan. Identidades de aprovisionamiento y runtime separadas por acciones/buckets. IAM HTTP, WebDAV e interfaces no necesarias deshabilitadas. |
| Gateway | Imagen local derivada de un digest oficial, con módulos/intérpretes no usados retirados y `apk upgrade`; usuario 101, filesystem readonly, tmpfs, sin capabilities y no-new-privileges. Sin logs de solicitudes ni errores HTTP que puedan contener firmas. Imagen/SBOM/avisos registrados localmente; no se publicó la imagen. |

MFA permanece **false en Development** para la simulación habitual. Solo las pruebas aisladas lo habilitan; perfiles fuera de Development rechazan MFA desactivado. La sesión personal mantiene **24 horas absolutas**, sin nueva caducidad por inactividad. La limpieza de sesiones es una operación explícita de mantenimiento para expiraciones anteriores a 90 días; no elimina auditoría ni se creó una automatización.

Las particiones de cuota usan un número fijo de buckets para limitar memoria. Orígenes/identidades distintos pueden compartir bucket; los valores son una calibración inicial, no un SLA para tráfico distribuido o todos los equipos de una tienda.

## Composición y aislamiento

La ejecución final registró **100 escenarios integrados aprobados**, **10 casos de archivos aprobados** y dos denegaciones SQL adicionales sobre `data_protection_key` y `personal_session`, verificadas después de endurecer sus permisos. Evidencia: `implementation/results.json`, `implementation/media-results.json`, `implementation/additional-runtime-denials.json` y `security-checks-verified.log`. Estas dos denegaciones están incorporadas al runner para ejecuciones futuras; no se contabilizan como parte de los 100 escenarios de esa ejecución. La compilación final de la solución terminó con cero errores y cero advertencias (`final-solution-build.log`).

`tools/Sgol.SecurityChecks/compose.yaml` usa proyecto/red/volúmenes propios y puertos loopback 5549 (PostgreSQL), 8339 (gateway de objetos) y 3319 (ClamAV). Las bases tienen prefijo `sgol_task_checks_security_*`; Web real escucha HTTPS 7287, con servidor auxiliar 7288 para deadlines. Credenciales inventadas y temporales bajo `artifacts/security-audit/implementation`, con ACL restringida. El keyring de esos servidores está en su DB aislada, envuelto con un certificado efímero propio; no usan el keyring habitual. TMP/TEMP también se separan.

El ejecutable inicia **el entrypoint real de Sgol.Web en otro proceso**, con Razor, middleware, endpoints, API bridge, EF, cookies, PostgreSQL, S3 y scanner reales. Los fixtures y las pruebas de reloj/HealthCheckService complementan ese proceso; no lo sustituyen. El runner comprueba que el puerto esté libre para evitar validar accidentalmente otro servidor. Compilaciones con `ArtifactsPath` independiente evitan DLL de servicios activos.

Comandos mantenibles desde la raíz:

```powershell
./scripts/comprobar-seguridad.ps1
./scripts/comprobar-secretos.ps1
./scripts/comprobar-rotacion-seguridad.ps1
./scripts/ensayar-recuperacion-seguridad.ps1
./scripts/comprobar-recuperacion-almacenamiento.ps1
```

La prueba de readiness con dependencia detenida requiere el servidor aislado retenido mediante `-Browser`, y `scripts/comprobar-readiness-aislado.ps1` verifica la etiqueta del proyecto antes de detener solo su gateway. `release-browser` libera el servidor retenido. No usar `down -v` ni borrar snapshots para repetir pruebas. Los scripts de recuperación son específicos de este host y verifican nombres/destinos; no son un procedimiento portable de producción.

## Evidencia ejecutada

Directorio privado ignorado: `artifacts/security-audit/implementation/`. Logs de tandas anteriores se conservan para diagnóstico; **no se suman sus comprobaciones** como casos independientes. `results.json` corresponde a la última tanda íntegra. `media-results.json` contiene diez casos de archivos. `security-checks-*.log` identifica fallos de fixtures y sus repeticiones; la matriz del plan distingue el alcance aprobado.

- HTTP real: dos sesiones, replay tras logout, cookie heredada protegida con el keyring real del stack, renovación con mismo ID/plazo, fallo de auditoría inducido, cookie y reintento Razor, ocho revocaciones concurrentes, 23 h de inactividad y corte exacto a 24 h.
- MFA real: password/challenge sin cookie plena, enrolamiento, TOTP reutilizado, recuperación sin sesión plena hasta regenerar, código consumido, desafío vencido y dos verificaciones concurrentes con un solo éxito. El perfil habitual sigue sin MFA.
- Privilegios: CREATE ROLE/DATABASE/TABLE, ALTER/DROP, SET ROLE al migrador y DELETE/TRUNCATE de auditoría rechazados con 42501. Login, catálogo, entrega por formulario, revisión y job real `CLEAN_EXPIRED_EVIDENCE_UPLOADS` con locks/reintento operan con runtime.
- SQLi: cuatro payloads de login; búsqueda de catálogo, `WorkSearch`, filtro real `F.Task` y exportación de reportes. No hubo bypass, SQL filtrado ni ampliación de filas. La auditoría no había confirmado una inyección y este trabajo no afirma haber corregido una inexistente.
- IDOR: usuarios del mismo rol contra asignaciones, POST de entrega, galería, bytes de fotografía/adjunto; rol de piso contra plan/proceso y revisión; Dirección contra edición/borrado de plantilla personal ajena. Se verifica ausencia de autoría/escrituras parciales.
- XSS: nombre y descripción persistidos, renderizados en Razor real. Navegador observó texto literal, cero handlers `onerror`, ningún script del payload ni diálogo. No equivale a fuzzing de todos los campos/formatos de exportación.
- Storage: rechazo POST 405, anónimo 403, runtime sin aprovisionamiento de buckets, PUT/GET y SHA256 firmados, firma vencida, CORS verificado por el adaptador, cuarentena/validación/ClamAV/promoción reales, EICAR y limpieza.
- Readiness: 64 solicitudes HTTP reales; prueba instrumentada de 32 clientes con una ejecución y waiter abortado; expiración de éxito y timeout. Detener solo storage aislado dio 200 → 503 → 200 en el servidor real. Ráfaga acotada de 650 solicitudes recibió 429/Retry-After y mantuvo liveness.
- Windows: roundtrip DPAPI, metadatos sin secreto, migración repetida y denegación de lectura con token restringido Users; también se verificó la denegación sobre el almacén habitual. No se creó ni se simuló que se hubiera usado otra cuenta Windows completa.
- Navegador: login/cierre reales, fallo de auditoría, aviso enfocado y reintento exitoso; escritorio y 320 px, sin desborde horizontal. Capturas `browser-*.png`. No se modifican estilos/temas; la variante oscura no se declara recorrida.

## Recuperación y conservación

Antes de cambiar la simulación se restauró `pg_dump -Fc` en **otro contenedor PostgreSQL**. Conteos/hashes de todas las tablas coincidieron. Sobre la copia se migraron esquema/ownership/permisos y se comprobaron **88 tablas existentes sin cambios**, excluyendo únicamente historial EF y la tabla nueva de sesiones. Esto incluye asignaciones, avances, entregas, revisiones, evidencia y auditoría.

El ensayo detectó y corrigió la transferencia de una secuencia ligada a una tabla: su ownership debe cambiar junto con la tabla. No se resolvió concediendo privilegios al runtime.

El keyring efectivo habitual **no está en la tabla DB** (estaba vacía): son dos archivos bajo el perfil ASP.NET del usuario. Se copiaron y se comprobó que la copia descifra un valor protegido con las claves originales, deshabilitando generación automática en ambos proveedores. Se endureció su ACL sin cambiar sus hashes. Certificado/clave y confianza LAN se conservan.

Este ensayo prueba recuperación aislada en el mismo host/identidad. La protección DPAPI no se presenta como respaldo portable para pérdida del host o cambio de identidad Windows.

La copia de storage se arrancó separadamente, conservando la identidad propietaria de buckets. Un primer comando PowerShell separó argumentos con puntos y dejó esa copia sin la configuración pretendida: su validación positiva se **invalidó**, se conservó la evidencia del fallo y se repitió sobre copia nueva con argumentos entre comillas. La repetición verificó comando efectivo, buckets, rechazo anónimo 403 y rechazo de la identidad anterior. No se considera válido el primer resultado.

Los dos buckets habituales estaban vacíos. Por tanto se probó restaurar sus metadatos y acceder a ellos, y el pipeline de objetos nuevos se probó aparte; no se inventa una descarga S3 histórica inexistente. La evidencia histórica que está en DB sí está incluida en los fingerprints. El origen no se reinicializó ni se borró la simulación.

Si se recupera un snapshot antiguo, revocar las sesiones personales restauradas **antes de abrir acceso**; conservar auditoría y keyring. Restaurar una DB anterior puede restaurar revocaciones anteriores. No hacer downgrade sobre volúmenes originales ni volver a entregar superusuario a Web como fallback.

## Activación y límites

La migración, cuentas, secretos, rotación de mantenimiento PostgreSQL y S3, listeners/gateway y certificado de almacén Windows se activaron en el entorno habitual tras los ensayos aislados. Web usa `sgol_runtime`; la cuenta original queda exclusivamente como mantenimiento. Se comprobó nuevamente que las 88 tablas existentes no cambiaron antes de abrir los procesos. Los servicios local y LAN comparten esa misma DB: no se presentan como dos entornos aislados.

Resultado HTTP habitual: `/acceso` 200, `/health/live` 200, `/health/ready` LAN 403, monitor loopback directo 200 y sesión anónima 401. Procesos/configuración/binarios finales se registran en `local-activation-*.json`. La prueba funcional autenticada y los ataques pertenecen al stack aislado; no se presentan como entregas/revisiones realizadas en la simulación compartida.

El relevo final encontró el puerto ocupado por el proceso Web anterior. Se detuvo exclusivamente su árbol de procesos y se repitió el arranque. `final-runtime-verification.json` verifica que el listener 7241 pertenece al binario congelado `.local/runtime/security-20261028-final/Web/Sgol.Web.dll`, con readiness directo 200, acceso y liveness por proxy 200, readiness por proxy 403 y log de errores vacío. El proxy, PostgreSQL y los volúmenes se conservaron. El servidor de pruebas 7287 se cerró al terminar las pruebas de navegador.

## Avisos y riesgos residuales

Los avisos [GHSA-5fx4-c9qp-36jc](https://github.com/seaweedfs/seaweedfs/security/advisories/GHSA-5fx4-c9qp-36jc) y [GHSA-qmx6-fpcw-q3xg](https://github.com/seaweedfs/seaweedfs/security/advisories/GHSA-qmx6-fpcw-q3xg) seguían indicando `Patched versions: None` durante esta implementación. Se clasifican como **mitigados en las superficies ensayadas**, no corregidos en SeaweedFS. El gateway comparte namespace por necesidad y es parte de la frontera confiable; un compromiso del host/Docker o del gateway cambia esa amenaza.

NuGet no reportó avisos del catálogo consultado y npm devolvió cero avisos para 32 paquetes del lock consultados sin ejecutar hooks. Gitleaks escaneó ocho commits/todas las refs, aproximadamente 22 MB, sin hallazgos y con redacción. No es una garantía de ausencia de secretos o vulnerabilidades desconocidos.

Se generaron SBOM y reportes locales de Docker Scout. La imagen gateway original tenía 37 avisos; la imagen reducida conserva uno de musl (`CVE-2026-107891`, versión detectada 1.2.6-r3, catálogo propone r6; `apk upgrade` no lo resolvió). Las imágenes existentes de SeaweedFS, PostgreSQL y ClamAV reportaron respectivamente 67, 101 y 63 avisos. Esos conteos de catálogo **no equivalen a explotabilidad desde LAN**, tampoco se ocultan: requieren triage/versiones compatibles en un paquete posterior. No se actualizó ciegamente SeaweedFS ni se publicaron imágenes/SBOM privados.

Todos los servicios locales siguen ejecutándose como el mismo usuario Windows. DPAPI/ACL contienen a otros principales, pero un proceso comprometido con ese mismo usuario podría leer/descifrar el almacén, incluido mantenimiento. La separación de roles PostgreSQL contiene abuso SQL; no sustituye separación de identidades de SO frente a RCE. No se declara implementada una nueva cuenta de servicio Windows.

Pendientes delimitados: varias instancias simultáneas, campañas exhaustivas XSS/IDOR/legacy, reset/bloqueo administrativo completos tras esta migración, operación real de una tienda completa bajo cuota, derribo sostenido/distribuido, otra cuenta Windows/máquina LAN, auditoría Windows/firewall externa y triage de avisos nativos. Se conservan como no ejecutados o riesgos residuales, sin presentar inspección/compilación como aprobación funcional.
