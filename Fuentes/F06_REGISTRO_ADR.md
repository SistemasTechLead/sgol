# F06 — Registro de decisiones de arquitectura de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación literal del responsable | `Apruebo los seis entregables de Fase 06 y autorizo su incorporación a Fuentes` |
| Alcance | Diseño técnico del MVP |
| Regla | Los ADR `DECIDIDO` reflejan elecciones expresas del responsable; los ADR derivados quedaron `ACEPTADO` mediante la aprobación conjunta de los seis entregables F06 |

## 2. Convención

- `ADR-###`: identificador permanente.
- **DECIDIDO:** selección expresa ya recibida durante F06.
- **ACEPTADO:** consecuencia técnica aprobada conjuntamente con los seis entregables F06.
- Un cambio posterior exige un ADR sucesor; no se reescribe la razón histórica.

## 3. Índice

| ADR | Título | Estado | Decisión de origen |
|---|---|---|---|
| ADR-001 | Monolito modular | ACEPTADO | Derivada de escala y alcance |
| ADR-002 | .NET 10 LTS y ASP.NET Core | DECIDIDO | DTEC-01=A |
| ADR-003 | PostgreSQL como autoridad transaccional | ACEPTADO | Comparación F06 |
| ADR-004 | Identidad local con MFA obligatorio | DECIDIDO | DTEC-02=C |
| ADR-005 | Autorización por política y recurso | ACEPTADO | Matriz F05 |
| ADR-006 | Auditoría append-only en la misma transacción | ACEPTADO | CAP-045 |
| ADR-007 | Archivos privados S3, cuarentena y SHA-256 | DECIDIDO/AMPLIADO | DTEC-05=A |
| ADR-008 | PaaS administrado y despliegue portable | DECIDIDO/AMPLIADO | DTEC-03=B; DTEC-07 |
| ADR-009 | Idempotencia mediante restricción única y transacción | ACEPTADO | DEC-063; CAP-046 |
| ADR-010 | RPO 1 h, RTO 4 h y prueba trimestral | DECIDIDO | DTEC-04=A |
| ADR-011 | Sin purga en piloto y revisión legal antes de producción | DECIDIDO | DTEC-06=A |
| ADR-012 | REST `/api/v1` y cookies de mismo origen | ACEPTADO | Comparación F06 |
| ADR-013 | Sin broker, Redis ni microservicios en MVP | ACEPTADO | Escala aprobada |
| ADR-014 | UTC para instantes y zona local para operación | ACEPTADO | DEC-058 |
| ADR-015 | Estrategia de pruebas por riesgo y trazabilidad F05 | ACEPTADO | Gate F05/F09 |

## 4. ADR detallados

### ADR-001 — Monolito modular

| Campo | Contenido |
|---|---|
| Estado | ACEPTADO |
| Contexto | El MVP cubre una sucursal, 100 cuentas y 25 usuarios concurrentes, pero exige transacciones fuertes, historial e idempotencia. |
| Opciones | Monolito modular; microservicios; funciones independientes. |
| Decisión | Un despliegue y una base transaccional, con módulos internos y contratos explícitos. |
| Razón | Reduce fallos parciales y costo operativo; permite proteger hechos y auditoría en una transacción. |
| Consecuencias | Se exigen pruebas de límites modulares. Escalar un módulo por separado requiere ADR posterior. |

### ADR-002 — .NET 10 LTS y ASP.NET Core

| Campo | Contenido |
|---|---|
| Estado | DECIDIDO |
| Contexto | Se compararon .NET, Django y NestJS. |
| Decisión | C#/.NET 10 LTS, ASP.NET Core, EF Core y Razor Pages/MVC. |
| Razón | Elección DTEC-01=A; soporte LTS, tipado y capacidades integradas de identidad y prueba. |
| Consecuencias | Parcheo mensual y actualización antes de terminar el soporte LTS. La UI no será una SPA separada en MVP. |

### ADR-003 — PostgreSQL como autoridad transaccional

| Campo | Contenido |
|---|---|
| Estado | ACEPTADO |
| Contexto | SGOL necesita unicidad, relaciones, versiones, bloqueos y restauración puntual. |
| Opciones | PostgreSQL; SQL Server; base documental. |
| Decisión | PostgreSQL administrado; binarios fuera de la base. |
| Razón | Portabilidad, restricciones parciales, JSONB acotado, concurrencia y respaldo maduro. |
| Consecuencias | Las pruebas de integración usan PostgreSQL real; SQLite no valida el comportamiento. |

### ADR-004 — Identidad local con MFA obligatorio

| Campo | Contenido |
|---|---|
| Estado | DECIDIDO |
| Contexto | El responsable seleccionó credenciales locales en vez de un IdP. |
| Decisión | ASP.NET Core Identity, contraseña, MFA TOTP para todas las cuentas y códigos de recuperación de un solo uso. No SMS. |
| Razón | DTEC-02=C. TOTP evita depender de mensajería externa. |
| Consecuencias | SGOL asume hashing, bloqueo, recuperación, custodia de claves y soporte. Se exige procedimiento break-glass para Dirección. |

### ADR-005 — Autorización por política y recurso

| Campo | Contenido |
|---|---|
| Estado | ACEPTADO |
| Contexto | Un rol por sí solo no expresa propiedad, jerarquía, validador inmediato ni estado. |
| Decisión | Políticas ASP.NET Core con evaluadores que consultan rol vigente, relación jerárquica, recurso, estado y motivo. Denegación por defecto. |
| Razón | Preserva exactamente la matriz F05 y evita que ocultar botones sea el control. |
| Consecuencias | Cada endpoint y descarga tiene pruebas positivas y negativas por alcance. |

### ADR-006 — Auditoría append-only en la misma transacción

| Campo | Contenido |
|---|---|
| Estado | ACEPTADO |
| Contexto | CAP-045 exige reconstrucción y prohíbe eliminación por usuarios. |
| Decisión | `audit_event` se inserta dentro de la transacción de negocio; la cuenta de aplicación no puede actualizar ni borrar; trigger defensivo rechaza esas operaciones. |
| Razón | Evita negocio confirmado sin auditoría y protege incluso ante error de código. |
| Consecuencias | Correcciones se registran como eventos nuevos. Operaciones extraordinarias de base requieren procedimiento y evidencia independiente. |

### ADR-007 — Archivos privados S3, cuarentena y SHA-256

| Campo | Contenido |
|---|---|
| Estado | DECIDIDO/AMPLIADO |
| Contexto | Se aprobaron JPEG, PNG, PDF, 15 MiB, privacidad, SHA-256 y antimalware. |
| Decisión | Objetos privados S3-compatible, carga firmada a cuarentena, comprobación de tipo real, escaneo, hash y promoción lógica a limpio. |
| Razón | DTEC-05=A; separa binarios de transacciones sin hacerlos públicos. |
| Consecuencias | No se usa CDN. Un objeto no limpio no puede vincularse ni descargarse como evidencia. |

### ADR-008 — PaaS administrado y despliegue portable

| Campo | Contenido |
|---|---|
| Estado | DECIDIDO/AMPLIADO |
| Contexto | Se eligió una plataforma administrada independiente de hiperescalador y se autorizó Norteamérica. |
| Decisión | Imagen OCI, PostgreSQL y S3; referencia inicial DigitalOcean App Platform `NYC`, con respaldo de objetos en segunda región norteamericana. |
| Razón | DTEC-03=B y DTEC-07; reduce operación y conserva ruta de salida. |
| Consecuencias | El dominio no usa SDK propietario. Se prueba exportación/restauración y se registra manifiesto declarativo. |

### ADR-009 — Idempotencia mediante restricción única y transacción

| Campo | Contenido |
|---|---|
| Estado | ACEPTADO |
| Contexto | Consultar antes de insertar no evita carreras. |
| Decisión | Clave idempotente normalizada, hash de solicitud, restricción única y recuperación transaccional del resultado. |
| Razón | Garantiza DEC-063 bajo reintento y concurrencia. |
| Consecuencias | Misma clave/datos devuelve el resultado; misma clave/datos distintos produce conflicto auditado. |

### ADR-010 — RPO 1 h, RTO 4 h y prueba trimestral

| Campo | Contenido |
|---|---|
| Estado | DECIDIDO |
| Decisión | Base con respaldo administrado/PITR, exportación portable diaria, archivos replicados cada hora y simulacro trimestral. |
| Razón | DTEC-04=A. |
| Consecuencias | El objetivo aplica al conjunto base + objetos + configuración; una copia no probada no cuenta como respaldo válido. |

### ADR-011 — Sin purga en piloto y revisión legal antes de producción

| Campo | Contenido |
|---|---|
| Estado | DECIDIDO |
| Decisión | No hay purga automática durante el piloto. Antes de producción se requiere dictamen aplicable y política aprobada de retención, archivo, privacidad y eliminación técnica cuando corresponda legalmente. |
| Razón | DTEC-06=A; las fuentes no aportan un plazo legal. |
| Consecuencias | El piloto monitorea crecimiento. La ausencia de dictamen bloquea producción, no Fase 07. |

### ADR-012 — REST `/api/v1` y cookies de mismo origen

| Campo | Contenido |
|---|---|
| Estado | ACEPTADO |
| Decisión | API REST JSON versionada; sesión por cookie `Secure`, `HttpOnly`, `SameSite=Strict`; CSRF en mutaciones; CORS deshabilitado. |
| Razón | Sólo existe un cliente web de mismo origen y no hay integración externa aprobada. |
| Consecuencias | Un cliente externo futuro requiere ADR, OAuth/OIDC y contrato de seguridad nuevo. |

### ADR-013 — Sin broker, Redis ni microservicios en MVP

| Campo | Contenido |
|---|---|
| Estado | ACEPTADO |
| Decisión | Trabajos programados y `outbox` se coordinan con PostgreSQL; no se añade infraestructura de mensajes o caché. |
| Razón | La escala no lo exige; esas piezas aumentan recuperación y monitoreo. |
| Consecuencias | Si las mediciones incumplen NFR, un ADR posterior identificará el cuello de botella antes de añadir tecnología. |

### ADR-014 — UTC para instantes y zona local para operación

| Campo | Contenido |
|---|---|
| Estado | ACEPTADO |
| Decisión | `timestamptz` UTC para hechos; `date` y `America/Mexico_City` para calendario, disponibilidad, semanas y vencimientos. |
| Razón | DEC-058 y RN-005; evita depender de la zona del servidor. |
| Consecuencias | Reloj inyectable y pruebas en límites de día/semana. |

### ADR-015 — Estrategia de pruebas por riesgo y trazabilidad F05

| Campo | Contenido |
|---|---|
| Estado | ACEPTADO |
| Decisión | Unitarias de dominio, integración PostgreSQL/S3, contrato API, navegador, seguridad, desempeño y recuperación; cada CA/CP queda mapeado. |
| Razón | El porcentaje de cobertura solo no demuestra permisos, estados ni ausencia de efectos parciales. |
| Consecuencias | Ninguna historia está terminada hasta pasar positivo, negativo, auditoría y no-efecto definidos en F05. |

## 5. Decisiones bloqueantes

No queda una elección técnica sin alternativa seleccionada. La revisión legal de retención es un gate explícito previo a producción conforme a ADR-011; no impide preparar repositorio, backlog ni piloto controlado.

## 6. Condición de aceptación

Los seis entregables F06 fueron aprobados conjuntamente. Cualquier cambio posterior debe identificar el ADR afectado, registrar la nueva alternativa y conservar esta decisión como antecedente histórico.
