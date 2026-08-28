# F06 — Arquitectura técnica del MVP de SGOL

## 1. Control del entregable

| Campo | Valor |
|---|---|
| Fase | 06 — Diseño técnico y arquitectura |
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación literal del responsable | `Apruebo los seis entregables de Fase 06 y autorizo su incorporación a Fuentes` |
| Alcance | MVP aprobado: 35 capacidades, HU-001 a HU-035 y ocho definiciones de tarea |
| Entradas rectoras | Entregables aprobados F03, F04 y F05; `PLAN_EJECUCION_SGOL_CHATGPT_CODEX.md` |
| Decisiones expresas del responsable | DTEC-01=A; DTEC-02=C; DTEC-03=B; DTEC-04=A; DTEC-05=A; DTEC-06=A; DTEC-07=escala suficiente y región autorizada de Norteamérica |
| Límite | Define el diseño del MVP; no implementa el sistema ni amplía el alcance funcional |
| Documentos relacionados | `F06_MODELO_DE_DATOS.md`; `F06_CONTRATO_DE_API.md`; `F06_ESTRATEGIA_DE_PRUEBAS.md`; `F06_SEGURIDAD_Y_OPERACION.md`; `F06_REGISTRO_ADR.md` |

## 2. Restricciones que gobiernan el diseño

1. Una sola sucursal: `LOR-001`; `TODAS` es un alcance de consulta, no una sucursal.
2. Zona operativa `America/Mexico_City`; semanas ISO lunes–domingo.
3. Escala aprobada: hasta 100 cuentas activas, 25 usuarios concurrentes y 10 GB nuevos de evidencias por año.
4. Cuenta individual, credenciales locales y MFA; un rol funcional activo por usuario en `LOR-001`.
5. Sólo ocho definiciones pueden generar obligaciones en el MVP.
6. Activación manual o recurrente; no hay eventos externos, integraciones, correo, SMS, nómina ni incentivos.
7. Evidencias, auditoría, asignaciones, validaciones y configuración versionada no se sobrescriben ni eliminan funcionalmente.
8. La idempotencia debe impedir duplicados aun con concurrencia y reintentos.
9. El sistema debe poder desplegarse en una plataforma administrada sin depender de servicios propietarios para su lógica de negocio.
10. RPO máximo de una hora y RTO máximo de cuatro horas.
11. Durante el piloto no existe purga automática; una revisión legal es gate obligatorio antes de producción.

## 3. Opciones evaluadas

### 3.1 Forma de arquitectura

| Opción | Ventajas | Debilidades | Decisión |
|---|---|---|---|
| Monolito modular | Una transacción puede proteger obligación, asignación, auditoría e idempotencia; operación y pruebas simples; menor costo. | Exige disciplina de límites internos. | SELECCIONADA |
| Microservicios | Escalado y despliegue independiente por dominio. | Complejidad distribuida injustificada para 25 usuarios concurrentes; consistencia y auditoría más difíciles. | DESCARTADA para MVP |
| Funciones aisladas/serverless | Pago por uso y ejecución programada sencilla. | Mayor acoplamiento al proveedor, arranques y transacciones fragmentadas. | DESCARTADA como arquitectura principal |

### 3.2 Familia tecnológica

| Opción | Evaluación | Decisión |
|---|---|---|
| .NET 10 LTS + ASP.NET Core | Tipado fuerte, soporte LTS, Identity/MFA integrado, EF Core, pruebas maduras y contenedor portable. | SELECCIONADA por DTEC-01 |
| Django 5.2 LTS | Alta productividad, administración y autenticación maduras. | Alternativa válida, no seleccionada |
| NestJS + React | Un solo lenguaje y ecosistema web amplio, pero añade más piezas y dependencias sin necesidad del MVP. | No seleccionada |

### 3.3 Despliegue

| Opción | Evaluación | Decisión |
|---|---|---|
| PaaS administrado con contenedores, PostgreSQL y almacenamiento S3 | Reduce operación de infraestructura y conserva portabilidad por estándares. | SELECCIONADA por DTEC-03 |
| Nube empresarial integrada | Más controles nativos, mayor acoplamiento y costo. | No seleccionada |
| VPS autoadministrado | Menor precio aparente, pero transfiere parches, respaldo y recuperación al equipo. | Descartada para MVP |

## 4. Arquitectura seleccionada

SGOL será un **monolito modular** desplegado como una imagen OCI. La aplicación web, la API REST y los módulos de dominio comparten proceso y base de código, pero sólo se comunican mediante contratos internos explícitos. Las tareas programadas ejecutan la misma imagen con un comando distinto.

```text
Navegador
   │ HTTPS + cookie segura + CSRF
   ▼
Aplicación ASP.NET Core
   ├─ Interfaz Razor Pages/MVC
   ├─ API REST /api/v1
   ├─ Identidad local + MFA
   ├─ Políticas de autorización por recurso
   ├─ Módulos de dominio
   └─ Adaptadores de infraestructura
        ├─ PostgreSQL
        ├─ Almacenamiento S3 privado
        ├─ escáner antimalware
        └─ telemetría estructurada

Trabajo programado (misma imagen)
   ├─ recurrencias cada 15 minutos
   ├─ réplica horaria de archivos
   ├─ conciliación y alertas
   └─ respaldo portable diario
```

No se introduce Redis, broker de mensajes, Kubernetes, motor de reglas general ni servicio de búsqueda en el MVP. Una necesidad posterior debe demostrar el límite concreto antes de añadirlos.

## 5. Componentes y responsabilidades

| Componente | Responsabilidad | Trazabilidad principal |
|---|---|---|
| `Identity` | Cuenta local, contraseña, MFA TOTP, sesiones, bloqueo, recuperación y sello de seguridad. | CAP-006, RN-002, RN-006 |
| `Organization` | Personas, vigencia, puesto, turno, sucursal, rol y disponibilidad. | CAP-001 a CAP-007 |
| `Configuration` | Calendario, definiciones y políticas versionadas. | CAP-009 a CAP-012, CAP-014, CAP-021, CAP-029, CAP-032 |
| `Generation` | Solicitudes manuales/recurrentes, unicidad e idempotencia. | CAP-015, CAP-018, CAP-019, CAP-046 |
| `Assignment` | Elegibilidad, carga, desempates y correcciones. | CAP-004, CAP-020 a CAP-023 |
| `Planning` | Período, plan único y publicaciones incrementales. | CAP-010, CAP-011, CAP-024, CAP-025 |
| `Execution` | Obligación, consulta, conclusión y condición temporal. | CAP-027, CAP-028, CAP-042 |
| `Evidence` | Requisitos, carga privada, escaneo, integridad y versiones. | CAP-029 a CAP-031 |
| `Validation` | Autoridad, resultado y sustitución de decisiones. | CAP-032, CAP-033 |
| `Reporting` | Cinco indicadores aprobados y vistas jerárquicas. | CAP-039, CAP-043, CAP-044 |
| `Audit` | Eventos funcionales append-only y consulta por alcance. | CAP-045 |
| `Continuity` | respaldos, conciliación, restauración y evidencia de recuperación. | CAP-047 |

Cada módulo posee sus entidades y servicios; otro módulo no escribe directamente sus tablas. La base sigue siendo única para conservar transacciones ACID.

## 6. Flujos técnicos críticos

### 6.1 Escritura transaccional

Una operación de negocio se ejecuta en una sola transacción PostgreSQL:

1. autenticar y evaluar la política sobre el recurso;
2. validar precondiciones y control optimista de versión;
3. escribir el nuevo hecho o versión, sin sobrescribir historia;
4. insertar el evento de auditoría;
5. registrar el evento `outbox` si existe trabajo posterior;
6. confirmar la transacción;
7. responder con identificador, versión y `correlationId`.

Si falla cualquier paso, no queda cambio parcial. La entrega posterior puede reintentarse desde `outbox` sin duplicar el hecho de negocio.

### 6.2 Generación e idempotencia

- La clave funcional es `regla + alcance + período + hecho originador`.
- Una restricción única en PostgreSQL es la autoridad final, no una comprobación previa en memoria.
- El trabajo programado toma un bloqueo de ejecución, calcula las ocurrencias vencidas y usa inserción transaccional.
- Un reintento idéntico devuelve la solicitud u obligación existente.
- La misma clave con contenido diferente se rechaza, audita y alerta.

### 6.3 Evidencias

1. La API crea una intención de carga en cuarentena.
2. El navegador carga directamente por URL firmada, sólo a un objeto privado y con límite de 15 MiB.
3. La aplicación verifica tamaño, tipo real y SHA-256; el escáner inspecciona el objeto.
4. Sólo `LIMPIO` puede convertirse en una versión de evidencia vigente.
5. `INFECTADO`, `INVALIDO` o `ERROR_ESCANEO` nunca queda enlazado como evidencia y genera auditoría/alerta.
6. Sustituir evidencia crea una nueva versión y conserva la anterior.

### 6.4 Autorización

La autenticación sólo identifica al usuario. Toda operación evalúa además:

- cuenta y persona activas;
- rol funcional vigente;
- sucursal `LOR-001`;
- relación con el recurso: propio, inferior, superior inmediato o Dirección;
- permiso estable de F05;
- guardas de estado y motivo obligatorio.

La interfaz no es una barrera de seguridad: la misma política protege endpoints, descargas y consultas.

## 7. Datos, archivos y tiempo

- PostgreSQL es la autoridad para datos relacionales, estados, versiones, auditoría, idempotencia y metadatos de archivo.
- Los binarios se guardan en almacenamiento S3-compatible privado; nunca dentro de la base.
- Todos los instantes se persisten en UTC con zona; fechas operativas y vencimientos se calculan mediante `America/Mexico_City`.
- Se usa `date` para disponibilidad y calendario local; `timestamptz` para hechos y auditoría.
- Los identificadores públicos son UUID v7; los códigos funcionales aprobados permanecen estables.
- No se usa borrado en cascada sobre hechos históricos.

El esquema completo y sus restricciones se encuentran en `F06_MODELO_DE_DATOS.md`.

## 8. API e interfaz

- API REST JSON bajo `/api/v1`; contrato en `F06_CONTRATO_DE_API.md`.
- Interfaz ASP.NET Core Razor Pages/MVC con mejora progresiva; no hay SPA separada en el MVP.
- Autenticación de navegador con cookie segura y protección CSRF; no se emiten tokens persistentes al navegador.
- Mismo origen en producción; CORS deshabilitado salvo decisión posterior.
- OpenAPI se genera y valida contra el contrato, pero no amplía capacidades.
- Operaciones idempotentes exigen `Idempotency-Key`; actualizaciones exigen `If-Match`.

## 9. Entornos y configuración

| Entorno | Uso | Datos | Acceso |
|---|---|---|---|
| Local | Desarrollo individual | Sintéticos; contenedores locales | Equipo técnico |
| CI | Pruebas automatizadas efímeras | Fábricas deterministas | Pipeline |
| Staging | Aceptación, seguridad y restauración | Sintéticos o anonimizados; nunca copia libre de producción | Equipo y usuarios de prueba autorizados |
| Producción piloto | Operación real de `LOR-001` | Reales | Usuarios aprobados |

Cada entorno tiene base, bucket, claves, dominio y secretos separados. No se comparte una credencial entre entornos. La configuración no secreta se versiona; los secretos se inyectan desde el almacén de la plataforma.

## 10. Despliegue de referencia y portabilidad

La referencia aprobable es:

- DigitalOcean App Platform en región `NYC`, con servicio web y trabajos programados desde la misma imagen OCI;
- un servicio web inicial con al menos 1 vCPU/1 GiB, una instancia; escalar verticalmente o a dos instancias sólo si métricas/pruebas lo exigen;
- un worker interno no enrutable para `outbox`, escaneo y conciliación, con al menos 1 GiB; los trabajos de recurrencia, réplica y respaldo son ejecuciones programadas separadas;
- PostgreSQL administrado en la misma región y red privada;
- bucket S3-compatible privado primario en Norteamérica;
- bucket de respaldo en una segunda región norteamericana y credencial independiente;
- registro de contenedores, dominio HTTPS, logs y alertas administrados;
- manifiesto declarativo de aplicación y migraciones versionadas.

La referencia no se convierte en dependencia de dominio. La portabilidad se prueba con:

1. imagen OCI ejecutable sin buildpack propietario;
2. SQL compatible con PostgreSQL soportado, con extensiones justificadas;
3. API S3 estándar detrás de una interfaz propia;
4. configuración mediante variables/secretos, sin SDK del proveedor en dominio;
5. exportación `pg_dump` y copia verificable de objetos;
6. infraestructura y procedimientos descritos como código en Fase 07/08.

DigitalOcean App Platform admite contenedores, comprobaciones de salud y trabajos programados; PostgreSQL administrado permite restaurar a un punto en el tiempo. El bucket de objetos no incluye respaldo automático, por lo que la réplica horaria es obligatoria, no opcional.

Las medidas iniciales son un punto de arranque para la escala aprobada, no una garantía sin prueba. NFR-001/NFR-002 y el monitoreo determinan cualquier ajuste antes del piloto.

## 11. Estrategia de liberación

1. Compilar, analizar dependencias y ejecutar pruebas.
2. Crear una imagen inmutable identificada por digest.
3. Desplegar en staging.
4. Ejecutar migración compatible hacia adelante.
5. Ejecutar smoke, autorización, carga/descarga y recurrencia controlada.
6. Requerir aprobación humana para producción.
7. Ejecutar migración como trabajo previo único.
8. Desplegar una instancia, comprobar salud y luego sustituir la anterior.
9. Ejecutar smoke de producción sin crear obligaciones reales no autorizadas.
10. Registrar versión, digest, migración, actor y resultado.

Rollback de aplicación usa la imagen anterior sólo si el esquema sigue siendo compatible. Una migración destructiva no está permitida en el MVP; cambios de esquema usan expandir → desplegar → migrar datos → contraer en una liberación posterior.

## 12. Observabilidad y operación

- Logs JSON con `timestamp`, nivel, servicio, entorno, versión, `correlationId`, actor técnico, operación y resultado; nunca contraseña, secreto TOTP, cookie ni contenido de evidencia.
- Métricas: disponibilidad, latencia p95, tasa 5xx, bloqueos de autenticación, fallos MFA, errores de trabajo, recurrencias retrasadas, conflictos idempotentes, archivos en cuarentena, escaneos fallidos, réplica atrasada y antigüedad del último respaldo verificado.
- Endpoints de salud separados: vida sin dependencias y disponibilidad con base/almacenamiento.
- Alertas críticas: aplicación indisponible, base inaccesible, trabajo recurrente retrasado >30 min, réplica >60 min, malware, restauración fallida o alteración de auditoría.

## 13. Requisitos no funcionales verificables

| ID | Requisito |
|---|---|
| NFR-001 | Soportar 100 cuentas, 25 sesiones concurrentes y 10 GB/año sin cambio de arquitectura. |
| NFR-002 | p95 <2 s en lectura y <3 s en escritura ordinaria con la carga objetivo, excluida transferencia del archivo. |
| NFR-003 | Ningún reintento idéntico crea una segunda obligación, asignación, publicación o decisión vigente. |
| NFR-004 | Toda operación crítica y denegación sensible produce auditoría atribuible. |
| NFR-005 | RPO ≤1 h y RTO ≤4 h, demostrados trimestralmente. |
| NFR-006 | Un archivo no puede descargarse ni vincularse sin autorización y estado de escaneo limpio. |
| NFR-007 | No existe endpoint funcional para borrar auditoría, evidencia o versiones históricas. |
| NFR-008 | El despliegue puede reconstruirse desde imagen, manifiesto, secretos externos, respaldo PostgreSQL y objetos replicados. |
| NFR-009 | Una caída del planificador puede recuperar ocurrencias pendientes sin duplicarlas. |
| NFR-010 | La aplicación usa siempre la zona aprobada para fechas operativas y UTC para instantes. |

## 14. Riesgos técnicos y tratamiento

| ID | Riesgo | Tratamiento |
|---|---|---|
| RT-001 | Credenciales locales aumentan la responsabilidad de seguridad. | MFA obligatorio, bloqueo, códigos de recuperación, procedimiento break-glass y métricas. |
| RT-002 | El bucket primario no tiene respaldo automático. | Réplica horaria a otra región/cuenta lógica y conciliación SHA-256. |
| RT-003 | Un monolito sin límites puede degradarse. | Módulos, pruebas de arquitectura y prohibición de acceso directo entre tablas ajenas. |
| RT-004 | Auditoría append-only puede crecer. | Índices por tiempo/recurso/actor, monitoreo y archivado lógico sin purga durante piloto. |
| RT-005 | Escaneo de archivos puede retrasar evidencia. | Estados de cuarentena visibles, reintento controlado y alerta; nunca omitir escaneo silenciosamente. |
| RT-006 | Plataforma de referencia puede cambiar servicio o precio. | Contratos OCI/PostgreSQL/S3, respaldo portable y prueba anual de salida. |
| RT-007 | Sin revisión legal no existe plazo definitivo de conservación. | Gate de producción; el piloto conserva sin purga automática. |

## 15. Condición de aprobación

Este documento y los otros cinco entregables F06 forman una sola decisión técnica. La aprobación conjunta acepta los ADR propuestos y habilita su incorporación a `Fuentes`. No autoriza implementar historias ni ampliar el MVP; Fase 07 preparará repositorio y backlog.

## 16. Referencias técnicas consultadas

- Microsoft, ciclo de soporte de .NET: <https://learn.microsoft.com/en-us/dotnet/core/releases-and-support>.
- Microsoft, selección de Identity para ASP.NET Core: <https://learn.microsoft.com/en-us/aspnet/core/security/how-to-choose-identity-solution?view=aspnetcore-10.0>.
- Microsoft, MFA en ASP.NET Core: <https://learn.microsoft.com/en-us/aspnet/core/security/authentication/mfa?view=aspnetcore-10.0>.
- DigitalOcean, App Platform y health checks: <https://docs.digitalocean.com/products/app-platform/how-to/manage-health-checks/>.
- DigitalOcean, trabajos programados de App Platform: <https://docs.digitalocean.com/products/app-platform/how-to/manage-jobs/>.
- DigitalOcean, restauración PostgreSQL/PITR: <https://docs.digitalocean.com/products/databases/postgresql/how-to/restore-from-backups/>.
- DigitalOcean, almacenamiento S3 y límite de respaldo: <https://docs.digitalocean.com/products/spaces/details/features/> y <https://docs.digitalocean.com/products/spaces/details/limits/>.

Estas referencias sustentan viabilidad técnica actual; las reglas funcionales continúan proviniendo exclusivamente de los entregables SGOL aprobados.
