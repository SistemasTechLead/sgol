# F06 — Seguridad y operación del MVP de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación literal del responsable | `Apruebo los seis entregables de Fase 06 y autorizo su incorporación a Fuentes` |
| Alcance | Autenticación, autorización, archivos, auditoría, entornos, despliegue, monitoreo, respaldos y recuperación |
| Decisiones rectoras | DTEC-02=C; DTEC-03=B; DTEC-04=A; DTEC-05=A; DTEC-06=A; DTEC-07 |
| Gate externo explícito | Revisión legal de conservación y privacidad antes de producción plena |

## 2. Objetivos de seguridad

1. Sólo una persona identificada y con MFA accede a SGOL.
2. La autorización preserva rol, jerarquía, propiedad, sucursal y estado definidos en F05.
3. Evidencia, auditoría y versiones no se exponen, sustituyen o destruyen indebidamente.
4. Un fallo o reintento no crea efectos parciales ni duplicados.
5. Credenciales, secretos y datos sensibles no aparecen en código, logs o reportes.
6. El servicio puede recuperarse en RPO ≤1 hora y RTO ≤4 horas.
7. La operación evita depender del conocimiento de una sola persona.

## 3. Clasificación de información

| Clase | Ejemplos | Controles mínimos |
|---|---|---|
| Pública | Ninguna información operativa de SGOL se presume pública. | Publicación expresa fuera del sistema. |
| Interna | Definiciones, calendario, plan sin datos personales detallados. | Sesión autenticada y alcance. |
| Confidencial | Personas, tareas, evidencias, decisiones, auditoría, indicadores por persona. | MFA, autorización por recurso, cifrado, logs minimizados, bucket privado. |
| Secreto | Contraseñas/hash, semilla TOTP, códigos de recuperación, cookies, claves S3/DB, secretos de firma. | Almacén de secretos; nunca log/API/auditoría; rotación y acceso técnico mínimo. |

Las fotografías, comprobantes, expedientes y controversias se tratan como confidenciales aunque una fuente concreta no marque sensibilidad.

## 4. Modelo de amenazas resumido

| ID | Amenaza | Control principal |
|---|---|---|
| AMZ-001 | Robo/reutilización de contraseña. | MFA TOTP, hashing de Identity, bloqueo, rate limit y sesiones cortas. |
| AMZ-002 | Cuenta compartida o suplantación. | Una cuenta por persona, activación individual, auditoría y prohibición funcional. |
| AMZ-003 | Elevación por puesto/rol manipulado. | Rol canónico versionado, sólo Dirección, política en servidor, denegación por defecto. |
| AMZ-004 | IDOR o filtración jerárquica. | Carga del recurso + evaluación de relación en cada consulta/descarga. |
| AMZ-005 | Manipulación o borrado de historia. | Append-only, privilegios DB, trigger defensivo, respaldo y alertas. |
| AMZ-006 | Archivo malicioso o público. | Tipos mínimos, tamaño, firma real, cuarentena, antimalware, URLs cortas y bucket privado. |
| AMZ-007 | Inyección/XSS/CSRF. | EF parametrizado, encoding, CSP, validación por esquema, cookie same-site y token CSRF. |
| AMZ-008 | Reintento/race crea duplicados. | Restricción única, transacción, ETag e idempotencia. |
| AMZ-009 | Pérdida/corrupción regional. | PITR, réplica horaria en segunda región, exportación portable y simulacro. |
| AMZ-010 | Secreto filtrado en repositorio/log. | Secret manager, escaneo CI, redacción y rotación. |
| AMZ-011 | Administrador funcional abusa de privilegio. | Auditoría no eliminable, MFA, motivo, alertas y separación del operador técnico. |
| AMZ-012 | Dependencia del proveedor impide recuperar. | OCI/PostgreSQL/S3, exportación, manifiesto y prueba de salida. |

## 5. Autenticación local

### 5.1 Ciclo de cuenta

1. Dirección crea la cuenta vinculada a una persona activa.
2. El sistema genera activación temporal de un solo uso, entregada fuera de SGOL por un canal operativo controlado; no correo/SMS automático.
3. En primer acceso, el usuario define contraseña y enrola TOTP antes de entrar a funciones.
4. Se generan códigos de recuperación de un solo uso, mostrados una vez.
5. Desactivar cuenta o persona invalida sesiones y bloquea acceso sin borrar hechos.
6. Cambio de contraseña, reset MFA o cambio de rol rota el `securityStamp` e invalida otras sesiones.

### 5.2 Política

- Contraseña mínima 14 caracteres y máxima suficiente para frases largas; permitir gestores de contraseña y pegar.
- No exigir rotación periódica sin evidencia de compromiso.
- Rechazar contraseñas comunes/comprometidas mediante lista local versionada; no enviar contraseña a servicios externos.
- Hash mediante el `PasswordHasher` soportado de ASP.NET Core Identity, con parámetros de la versión fijados y rehash al iniciar sesión cuando se actualicen.
- MFA TOTP obligatorio para todos; SMS no permitido.
- Cinco fallos de contraseña o MFA bloquean 15 minutos; reincidencia incrementa demora y alerta.
- Respuestas de login no distinguen usuario inexistente, inactivo o contraseña incorrecta.
- Sesión: inactividad 30 minutos, máximo absoluto 8 horas; reautenticación MFA reciente para cambio de contraseña, regenerar códigos, reset MFA de terceros y operación break-glass.
- Cookies `Secure`, `HttpOnly`, `SameSite=Strict`; rotación tras contraseña y MFA; HSTS en producción.

### 5.3 Recuperación y break-glass

- Usuario con código de recuperación: entra con contraseña + código y debe regenerar códigos.
- Usuario sin segundo factor: Dirección inicia reset motivado; se invalidan sesiones, TOTP y códigos; nueva activación presencial/controlada.
- Si la única cuenta Dirección pierde acceso, un **operador técnico designado** ejecuta runbook con aprobación documentada del responsable, acceso temporal a la plataforma y comando administrativo que sólo resetea autenticación. El operador no adquiere rol funcional ni consulta datos de negocio.
- Cada uso break-glass genera evidencia fuera y dentro de SGOL, rota credenciales técnicas usadas y requiere revisión posterior.
- Antes del piloto deben nombrarse al menos dos custodios autorizados; no se guardan secretos break-glass en el repositorio.

## 6. Autorización

- Denegación por defecto.
- Permisos estables de F05, evaluados con rol vigente y recurso.
- Puesto y turno nunca conceden autoridad.
- Consultas aplican filtros de alcance en servidor; detalle y archivos reevalúan autorización.
- Dirección administra cuentas/roles/configuración, pero tampoco puede eliminar auditoría.
- Operador de plataforma no es usuario funcional y no recibe permisos de negocio.
- Cuenta de aplicación tiene privilegios DB mínimos; migraciones usan una identidad distinta y temporal.
- Acceso directo de humanos a producción se limita a incidente, requiere ticket/aprobación y queda en logs del proveedor.

## 7. Protección de aplicación y API

- TLS 1.2+; preferencia TLS 1.3; HTTP redirige a HTTPS.
- HSTS, CSP restrictiva, `frame-ancestors 'none'`, `nosniff`, política de referencia y permisos mínimos.
- CSRF en todas las mutaciones de cookie; CORS apagado.
- Validación por allowlist y esquemas por TAR; límites de longitud y profundidad JSON.
- Consultas EF parametrizadas; SQL manual sólo revisado y parametrizado.
- Salida HTML codificada; contenido enriquecido no es necesario en MVP.
- Manejo de error uniforme sin stack, SQL, secreto o existencia de recurso ajeno.
- Rate limiting conforme al contrato API.
- Dependencias fijadas por lockfile, SBOM por imagen y escaneo antes de liberar.

## 8. Archivos

### 8.1 Controles

- Tipos finales: JPEG, PNG y PDF; máximo 15 MiB.
- Verificar firma/magic bytes y parseo seguro; no confiar en extensión o `Content-Type` del navegador.
- Nombre original se conserva como metadato sanitizado; la clave de objeto es aleatoria.
- Carga directa a prefijo/bucket de cuarentena por URL firmada de 10 minutos.
- Bucket privado, sin website/CDN/listado público; acceso sólo con credencial limitada.
- Escaneo antimalware y validación estructural antes de `LIMPIO`.
- Descarga mediante URL firmada de 5 minutos después de autorización.
- SHA-256 calculado/verificado; réplica y restauración concilian el hash.
- SVG, HTML, ejecutables, archivos Office, macros, ZIP y formatos no aprobados se rechazan.
- Imágenes no se procesan con metadatos activos; se elimina metadata EXIF no necesaria al producir una copia de visualización, conservando el original privado cuando sea requerido como evidencia.

### 8.2 Estados y respuesta

`PENDIENTE → LIMPIO` o `PENDIENTE → INFECTADO/INVALIDO/ERROR_ESCANEO`. No existe “permitir por falla”. Malware genera alerta crítica; el objeto permanece aislado el tiempo técnico necesario para investigación y luego su tratamiento depende de la política legal/seguridad aprobada, sin enlazarlo como evidencia.

## 9. Cifrado y secretos

- TLS para navegador, base y S3.
- Cifrado en reposo provisto por plataforma para base, objetos y respaldos; exportaciones portables se cifran antes de copiarse.
- Secretos sólo en el almacén de la plataforma: conexión DB, claves S3, protección de datos/cookies, credenciales de escáner y telemetría.
- Claves de Data Protection se persisten cifradas y comparten entre instancias; no se pierden en redeploy.
- El key ring de Data Protection se guarda en PostgreSQL o almacenamiento privado y se protege con una clave de envoltura separada del repositorio; su restauración forma parte del simulacro.
- Rotación: inmediata ante sospecha; ordinaria cada 90 días para credenciales técnicas que lo permitan; documentar dependencias y prueba.
- Las claves primaria y de respaldo de objetos son distintas.
- Producción y staging no comparten secretos.

## 10. Auditoría y logs

### 10.1 Auditoría funcional

`audit_event` es append-only y se inserta en la transacción. Registra actor, instante, acción, recurso, sucursal, antes/después mínimo, motivo, resultado y correlación. La cuenta de aplicación no puede modificar/borrar; el trigger lo impide. Dirección consulta según F05, pero no elimina.

### 10.2 Logs operativos

- JSON estructurado con ambiente, versión, servicio, severidad y correlación.
- Seudonimizar IP mediante hash con clave rotatoria cuando sea útil para abuso; no usarla como identidad funcional.
- Nunca registrar contraseña, hash, TOTP, código de recuperación, cookie, URL firmada, clave, cadena completa de conexión ni binario.
- Separar auditoría funcional de logs técnicos: una caída del agregador de logs no elimina la auditoría transaccional.
- Relojes sincronizados; alertar deriva.

## 11. Monitoreo y alertas

| Severidad | Condición | Respuesta inicial |
|---|---|---|
| Crítica | Servicio caído; base inaccesible; malware; alteración de auditoría; respaldo/restauración fallido; réplica >60 min. | Aviso inmediato; incidente; preservar evidencia. |
| Alta | Tasa 5xx sostenida; recurrencia >30 min tarde; múltiples bloqueos/MFA; bucket inaccesible. | Atender ≤30 min en horario piloto acordado. |
| Media | p95 excedido; almacenamiento >70 % previsto; job con reintentos; dependencia vulnerable media. | Analizar jornada siguiente. |
| Baja | Tendencia de capacidad o advertencia no operativa. | Backlog controlado. |

Panel mínimo: disponibilidad, latencia p50/p95/p99, errores, sesiones/bloqueos, jobs, outbox, generación, conflictos idempotentes, cuarentena, escaneo, bytes/objetos pendientes de réplica, último respaldo y último simulacro.

El horario de soporte del piloto y las personas de guardia se nombrarán en Fase 07/10; la ausencia de nombres no cambia los objetivos técnicos.

## 12. Entornos y acceso operativo

| Entorno | Regla |
|---|---|
| Local | Secretos locales fuera de Git; datos sintéticos; HTTPS de desarrollo cuando aplique. |
| CI | Recursos efímeros, credenciales de mínima duración, sin acceso a producción. |
| Staging | Separado de producción; datos sintéticos/anonimizados; mismo tipo de servicios. |
| Producción piloto | Despliegue sólo desde pipeline aprobado; acceso humano excepcional. |

- Rama protegida, revisión obligatoria y CI exitoso.
- Imagen por digest, SBOM y firma/procedencia del build.
- Producción requiere aprobación humana distinta del autor cuando haya equipo disponible; si sólo existe una persona, se registra la excepción y revisión posterior.
- Migraciones usan trabajo previo único y bloqueo para evitar ejecución doble.
- Ninguna edición desde consola cuenta como configuración válida del sistema; debe volver a código/manifiesto.

## 13. Despliegue y rollback

1. Verificar respaldo reciente y salud antes de desplegar.
2. Desplegar en staging, migrar y ejecutar gates.
3. Aprobar producción.
4. Ejecutar migración expand-only.
5. Desplegar imagen inmutable con health checks.
6. Ejecutar smoke técnico y funcional seguro.
7. Registrar digest, migración, actor y resultado.

Si falla la aplicación, volver a la imagen anterior compatible. Si falla una migración, no se improvisa rollback destructivo: se detiene, se restaura a cluster nuevo si es necesario y se sigue el runbook. Cambios destructivos requieren dos liberaciones y respaldo probado.

## 14. Respaldos

### 14.1 Objetivos

- RPO máximo: 1 hora para datos y archivos.
- RTO máximo: 4 horas para restaurar operación prioritaria.
- Copias en al menos dos regiones norteamericanas y credenciales distintas.

### 14.2 Esquema

| Activo | Mecanismo | Frecuencia/retención piloto | Verificación |
|---|---|---|---|
| PostgreSQL | Respaldo administrado + PITR | Según servicio; restauración puntual disponible | Estado diario y simulacro trimestral |
| PostgreSQL portable | `pg_dump` cifrado | Diario; 30 copias diarias mientras dure piloto | Restore automático a entorno aislado al menos mensual |
| Objetos primarios | Copia incremental a bucket de segunda región | Cada hora; sin purga automática de evidencia del piloto | Manifiesto y SHA-256 diario |
| Configuración/imagen | Git protegido, manifiesto e imagen por digest | Cada liberación | Reconstrucción en simulacro |
| Secretos | Procedimiento de recreación/rotación; no exportar secretos en dumps | Revisión trimestral | Prueba controlada de rotación |

El respaldo administrado del proveedor no basta para objetos: la documentación de la plataforma indica que el almacenamiento S3 de referencia no incluye respaldo automático. Tampoco se destruye un cluster de base antes de exportar lo necesario, porque sus respaldos asociados pueden perderse.

## 15. Recuperación

### 15.1 Prioridad

1. Identidad, personas, roles y configuración.
2. Obligaciones, asignaciones, planes y estados.
3. Evidencias y validaciones.
4. Auditoría, avisos e indicadores derivados.

La prioridad no permite declarar éxito si falta historia; sólo ordena la recuperación.

### 15.2 Runbook resumido

1. Declarar incidente, congelar escrituras y registrar instante objetivo.
2. Seleccionar respaldo/PITR que cumpla RPO.
3. Crear PostgreSQL nuevo; nunca restaurar destructivamente sobre la única copia.
4. Restaurar objetos desde réplica en bucket nuevo o aislado.
5. desplegar imagen/configuración conocidas y rotar credenciales expuestas.
6. Ejecutar migraciones necesarias.
7. Conciliar conteos, IDs, vínculos, versiones, hashes, idempotencia y auditoría.
8. Ejecutar smoke de autenticación, bandeja, plan, evidencia y validación.
9. Autorizar reapertura; registrar RPO/RTO observados y diferencias.
10. Realizar postmortem sin culpa y acciones con responsable/fecha.

No se fabrican registros para ocultar una diferencia. CA-035 exige reportarla y fallar la reconciliación.

## 16. Gestión de incidentes

| Fase | Acción |
|---|---|
| Detectar | Alerta, usuario o revisión. Crear ID y correlaciones. |
| Contener | Revocar sesión/clave, aislar archivo, detener job o poner modo sólo lectura. |
| Preservar | Logs, auditoría, hashes, versiones, imagen y tiempos; no borrar. |
| Erradicar | Corregir causa, rotar secretos, parchear y probar. |
| Recuperar | Seguir runbook y validar negocio/seguridad. |
| Aprender | Línea de tiempo, impacto, causa, acciones y verificación. |

Incidentes de datos personales o requisitos legales se escalan al asesor competente; este documento no inventa plazos de notificación.

## 17. Parches, vulnerabilidades y mantenimiento

- Revisar actualizaciones y vulnerabilidades semanalmente; aplicar parches críticos de componente expuesto con prioridad máxima y prueba proporcional.
- Imágenes base mínimas, versión fijada y reconstrucción mensual aunque no cambie código.
- .NET LTS debe actualizarse antes de fin de soporte; la versión mayor requiere ADR y regresión completa.
- Vulnerabilidad crítica/alta explotable bloquea despliegue; excepción requiere riesgo, mitigación, responsable y vencimiento.
- Probar renovación de certificado y rotación de claves antes de expirar.

## 18. Retención y revisión legal

Decisión DTEC-06=A:

- no hay purga automática durante el piloto;
- evidencia, auditoría y versiones permanecen vinculadas;
- se mide crecimiento y acceso;
- antes de producción plena, asesoría competente debe definir base aplicable, plazos, privacidad, acceso, archivo, conservación bajo incidente y eliminación técnica cuando corresponda;
- la política aprobada deberá producir un ADR sucesor y pruebas de retención.

La falta de revisión legal bloquea producción plena, pero no la preparación técnica de Fase 07 ni la implementación controlada previa al piloto.

## 19. Responsabilidades

| Rol operativo | Responsabilidad |
|---|---|
| Responsable de SGOL | Aprobar alcance, usuarios Dirección, liberación y aceptación. |
| Dirección funcional | Administrar personas, cuentas, roles y configuración dentro de SGOL. |
| Operador técnico | Plataforma, secretos, despliegue, respaldo, recuperación e incidentes; sin rol funcional implícito. |
| Desarrollo | Código, migraciones, pruebas, SBOM y correcciones. |
| Revisor de seguridad/legal | Dictámenes especializados cuando corresponda; no se sustituyen por suposición técnica. |

Una persona puede ocupar más de una función en el MVP, pero debe registrar qué función ejerció y evitar usar acceso técnico para operaciones funcionales ordinarias.

## 20. Gates operativos

### Antes de piloto

- MFA y recuperación probados;
- custodios break-glass designados;
- dominios/TLS/secretos configurados;
- bucket privado y escáner probados;
- alertas con destinatario;
- respaldo y restauración completa dentro de objetivos;
- runbook accesible fuera de la plataforma afectada;
- pruebas F05/F06 sin bloqueantes.

### Antes de producción plena

- todo lo anterior;
- revisión legal y política de retención aprobadas;
- soporte/incidentes formalizados;
- resultados del piloto y F09/F10 según plan maestro;
- capacidad y costo observados dentro de límites.

## 21. Criterios de aceptación

1. Identidad local/MFA tiene ciclo, recuperación y break-glass definidos.
2. Autorización y administración técnica/funcional están separadas.
3. Evidencias privadas tienen cuarentena, escaneo, hash, réplica y restauración.
4. Auditoría no puede ser eliminada por la aplicación.
5. Entornos, secretos, despliegue, monitoreo e incidentes tienen controles verificables.
6. RPO/RTO se prueban, no se presumen.
7. El gate legal está explícito y no se presenta un plazo inventado.
