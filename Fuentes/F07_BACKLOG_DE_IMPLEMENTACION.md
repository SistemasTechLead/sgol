# F07 — Backlog de implementación del MVP de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Unidad de ejecución F08 | Una historia o incremento pequeño por chat y rama |
| Orden rector | Dependencias y cortes verticales; no orden del legado |
| Cobertura | HU-001 a HU-035, CA/CP/CAT aprobados y tareas técnicas indispensables |

## 2. Reglas de priorización

1. `CV-00` sólo habilita compilación, pruebas y límites; no entrega función de negocio.
2. `CV-01` a `CV-05` conservan el orden aprobado en F04.
3. Auditoría, autorización, idempotencia, tiempo y trazabilidad se implementan dentro del primer corte que los necesita; no se difieren a una limpieza final.
4. Cada historia debe producir un resultado demostrable de extremo a extremo: dominio, persistencia, autorización, HTTP/UI mínima, auditoría y pruebas aplicables.
5. Una historia no puede introducir capacidades posteriores (`CAP-008`, `CAP-016`, `CAP-017`, `CAP-026`, `CAP-034` a `CAP-038`), opcionales o fuera de alcance.

## 3. Cortes verticales y puertas

| Orden | Corte | Resultado demostrable | Entrada | Salida mínima |
|---:|---|---|---|---|
| 0 | CV-00 — Base verificable | Solución compila, tests corren y arquitectura queda protegida | F07 aprobada | TECH-INIT-001 a TECH-BASE-005 terminadas |
| 1 | CV-01 — Gobierno mínimo | Dirección mantiene identidad, autoridad, calendario y ocho TAR | CV-00 | HU-001 a HU-011 aceptadas |
| 2 | CV-02 — Trabajo planificado | Obligación manual/recurrente única, explicable y publicada | CV-01 | HU-012 a HU-021 y HU-034 aplicable aceptadas |
| 3 | CV-03 — Ejecución comprobable | Responsable consulta, aporta evidencia y concluye sólo si está completa | CV-02 | HU-022 a HU-026 y HU-030 aceptadas |
| 4 | CV-04 — Validación y supervisión | Superior válido decide sin autovalidación indebida y supervisa inferiores | CV-03 | HU-027, HU-028 y HU-031 aceptadas |
| 5 | CV-05 — Control y aceptación | Dirección ve indicadores; auditoría e historia son reconstruibles | CV-01 a CV-04 | HU-029, HU-032 a HU-035 aceptadas |

## 4. Épicas

| Épica | Corte | Objetivo | Historias/tareas |
|---|---|---|---|
| EP-00 — Base de ingeniería | CV-00 | Repositorio reproducible y verificable | TECH-INIT-001 a TECH-BASE-005 |
| EP-01 — Identidad y organización | CV-01 | Personas, cuentas, rol, disponibilidad y sucursal | HU-001 a HU-007 |
| EP-02 — Gobierno de configuración | CV-01 | Configuración, calendario, semana y ocho TAR versionadas | HU-008 a HU-011 |
| EP-03 — Generación | CV-02 | Activación manual/recurrente y obligación única | HU-012 a HU-015; HU-034 parcial |
| EP-04 — Elegibilidad y asignación | CV-02 | Selección explicable y corrección trazada | HU-016 a HU-019 |
| EP-05 — Plan semanal | CV-02 | Plan único y publicación incremental | HU-020, HU-021 |
| EP-06 — Ejecución | CV-03 | Consulta, vencimiento y conclusión | HU-022, HU-023, HU-030 |
| EP-07 — Evidencia | CV-03 | Requisitos, archivos seguros, versiones y gate | HU-024 a HU-026 |
| EP-08 — Validación | CV-04 | Política y decisión independiente | HU-027, HU-028 |
| EP-09 — Supervisión y reporte | CV-04/CV-05 | Alcance jerárquico e indicadores aprobados | HU-029, HU-031, HU-032 |
| EP-10 — Auditoría y continuidad | Transversal/CV-05 | Reconstrucción, no eliminación y recuperación | HU-033 a HU-035 |

## 5. Tareas técnicas de base

| ID | Resultado verificable | Dependencias | No incluye |
|---|---|---|---|
| TECH-INIT-001 | Solución mínima .NET 10, host web, proyectos de pruebas, configuración común, `AGENTS.md` adoptado y smoke test | F07 aprobada/incorporada | Base, Identity, Docker, CI o función SGOL |
| TECH-BASE-002 | PostgreSQL local/CI aislado, primera conexión, migración vacía controlada y prueba Testcontainers | TECH-INIT-001 | Tablas funcionales |
| TECH-BASE-003 | Pipeline PR con restore bloqueado, build, unitarias, arquitectura, integración, formato, secretos y dependencias | TECH-INIT-001, TECH-BASE-002 | Despliegue productivo |
| TECH-BASE-004 | Pruebas de límites modulares y plantilla de trazabilidad HU/CA/CP | TECH-INIT-001 | Reglas de negocio |
| TECH-BASE-005 | Primitivas mínimas: reloj inyectable, UUID v7, `correlationId`, Problem Details y logging sin secretos | TECH-INIT-001 | Auditoría funcional completa |
| TECH-AUD-001 | Núcleo append-only de `audit_event`, inserción transaccional y rechazo DB de UPDATE/DELETE | TECH-BASE-002/005 | Pantalla de consulta de HU-033 |
| TECH-ID-BOOT-001 | Crear de forma controlada la primera cuenta `DIRECCION`, obligarla a cambiar contraseña/enrolar TOTP y deshabilitar la vía de arranque | TECH-BASE-002/005; F07-JP-001 | Administración ordinaria de cuentas de HU-006 |

### 5.1 Decisión de seguridad necesaria antes de las historias funcionales

Existe una dependencia circular real: HU-001 exige autorización de Dirección, pero HU-006 necesita una persona existente para crear la cuenta que ejercerá Dirección. Las fuentes aprobadas definen recuperación de la última cuenta, no el alta inicial.

| ID | Estado | Decisión solicitada | Opción recomendada |
|---|---|---|---|
| F07-JP-001 | APROBADA MEDIANTE LA APROBACIÓN DE LOS SEIS ENTREGABLES F07 | Autorizar el mecanismo de creación de la primera cuenta `DIRECCION`. | Comando administrativo de un solo uso, ejecutado fuera del tráfico web con secreto efímero inyectado, persona/cuenta/rol creados en una transacción auditada, cambio de contraseña y TOTP obligatorios al primer acceso, y rechazo de toda segunda ejecución. |

No se autoriza una cuenta predeterminada, contraseña en Git, endpoint público de bootstrap ni datos de negocio precargados. F07-JP-001 no bloquea TECH-INIT-001 a TECH-BASE-005, pero sí TECH-ID-BOOT-001 y cualquier historia que requiera un actor Dirección autenticado.

## 6. Historias ordenadas

Cada fila es candidata a un chat F08. “Trabajo técnico” identifica el mínimo que acompaña la historia, no una autorización para ampliar alcance.

| Orden | ID | Resultado vertical | Dependencias | Trabajo técnico y prueba obligatoria |
|---:|---|---|---|---|
| 1 | HU-005 | Reconocer únicamente `LOR-001` y rechazar otra sucursal | TECH-BASE-002/005 | Semilla, API/UI mínima, CA-005 y CP-005-P/N |
| 2 | HU-001 | Dirección crea/corrige persona y vigencia con historia | HU-005, TECH-AUD-001, TECH-ID-BOOT-001 | Organization, política por recurso y CA-001 |
| 3 | HU-002 | Dirección registra puesto/turno sin conceder permisos | HU-001 | Versionado y prueba de no elevación, CA-002 |
| 4 | HU-006 | Dirección administra cuenta individual | HU-001, HU-005, TECH-ID-BOOT-001 | ASP.NET Core Identity, ciclo ordinario y CA-006 |
| 5 | HU-007 | Dirección asigna un rol canónico activo | HU-006, TECH-ID-BOOT-001 | Política por recurso, sesión invalidada al cambio, CA-007 |
| 6 | HU-003 | Dirección registra disponibilidad binaria diaria | HU-001, HU-007 | Zona local, versionado y CA-003 |
| 7 | HU-008 | Dirección publica configuración versionada | HU-007, HU-033 mínimo transaccional | ETag, auditoría, CA-008 |
| 8 | HU-009 | Dirección administra calendario de `LOR-001` | HU-005, HU-008 | Semana/zona, versiones, CA-009 |
| 9 | HU-010 | Usuario autorizado obtiene semana ISO única | HU-009 | Cálculo inyectable y CA-010 |
| 10 | HU-011 | Dirección mantiene sólo ocho definiciones TAR | HU-008 | Semilla allowlist, versiones, CA-011 |
| 11 | HU-017 | Dirección versiona elegibilidad por TAR | HU-003, HU-011 | Esquemas aprobados F05-JP-001..008, CA-017 |
| 12 | HU-012 | Dirección configura alta manual/recurrencia permitida | HU-009, HU-011 | Rechazo de eventos/condiciones, CA-012 |
| 13 | HU-014 | Actor autorizado solicita generación idempotente | HU-012, HU-034 mínimo | `Idempotency-Key`, restricción única, CA-014 |
| 14 | HU-015 | SGOL crea o recupera una obligación única | HU-014 | Transacción, snapshot de versión TAR, CA-015 |
| 15 | HU-016 | SGOL calcula candidatos y explica exclusiones | HU-003, HU-007, HU-015, HU-017 | Snapshot determinista, CA-016 |
| 16 | HU-004 | Superior consulta carga activa correcta | HU-015, HU-007 | Conteo de pendientes y CA-004 |
| 17 | HU-018 | SGOL asigna por carga y desempates aprobados | HU-004, HU-016 | Concurrencia y explicación, CA-018 |
| 18 | HU-019 | Superior corrige asignación inferior con motivo | HU-018, TECH-AUD-001 | ETag, historia, autorización negativa, CA-019 |
| 19 | HU-020 | SGOL crea/recupera plan semanal único | HU-010, HU-015 | Unicidad PostgreSQL, CA-020 |
| 20 | HU-021 | Superior publica su alcance incrementalmente | HU-007, HU-019, HU-020 | Versiones, autorización, concurrencia, CA-021 |
| 21 | HU-013 | Job genera recurrencias vencidas sin duplicar | HU-012, HU-015, HU-018, HU-020 | Worker, bloqueo, reloj, TEC-JOB-001 y CA-013 |
| 22 | HU-023 | Usuario consulta tarea, procedencia e historia permitida | HU-007, HU-015, HU-019 | Filtro servidor/IDOR y CA-023 |
| 23 | HU-024 | Dirección versiona evidencia requerida por TAR | HU-011, HU-023 | Requisitos de CAT-001..008, CA-024 |
| 24 | HU-025 | Responsable/superior aporta o sustituye evidencia conservando versiones | HU-023, HU-024 | Cuarentena S3, SHA-256, escáner, archivos adversos, CA-025 |
| 25 | HU-026 | SGOL evalúa evidencia completa e informa faltantes | HU-024, HU-025 | Snapshot coherente, CA-026 |
| 26 | HU-022 | Responsable concluye sólo con evidencia completa | HU-019, HU-026 | Transacción, no-efecto negativo, CA-022 |
| 27 | HU-030 | Responsable ve bandeja propia y avisos internos | HU-022, HU-023 | Vencimiento derivado, sin canal externo, CA-030 |
| 28 | HU-027 | Dirección versiona validación por TAR | HU-007, HU-011, HU-022 | Superior inmediato y tres resultados, CA-027 |
| 29 | HU-028 | Superior inmediato emite/sustituye validación separada | HU-022, HU-025, HU-027 | No autovalidación, motivo, snapshot, CA-028 |
| 30 | HU-031 | Superior consulta y supervisa sólo inferiores | HU-028, HU-023 | Matriz completa/IDOR, CA-031 |
| 31 | HU-029 | Superior consulta cinco indicadores objetivos | HU-022, HU-028, HU-031 | Conteos y denominador, sin KPI monetario, CA-029 |
| 32 | HU-032 | Dirección consulta toda `LOR-001` | HU-029, HU-031 | Filtros y exclusiones explícitas, CA-032 |
| 33 | HU-033 | Actor autorizado reconstruye auditoría sin borrado | Todos los módulos usados | Consulta paginada, trigger DB, prueba UPDATE/DELETE, CA-033 |
| 34 | HU-034 | SGOL recupera mutaciones repetidas y registra conflictos | HU-014..HU-033 según operación | TEC-IDEM-001..004, CA-034 |
| 35 | HU-035 | Dirección verifica recuperación con identidades e historia | HU-033, despliegue/respaldos preparados | Reconciliador y simulacro, CA-035/NFR-005 |

TECH-AUD-001 establece que cada escritura posterior inserta `audit_event` en la misma transacción y que el rol de aplicación no puede modificarlo ni borrarlo. La experiencia de consulta completa de HU-033 se cierra en CV-05. “HU-034 mínimo” aplica primero a generación y se extiende a cada mutación idempotente cuando ésta aparece.

## 7. Dependencias de infraestructura por momento

| Momento | Tarea | Justificación |
|---|---|---|
| Antes de HU-001 | TECH-BASE-002 a 005 | Persistencia real, gates y primitivas comunes |
| Antes de HU-001 | TECH-AUD-001 y TECH-ID-BOOT-001 | Toda mutación nace auditada y existe un actor Dirección sin credencial predeterminada |
| Antes de HU-025 | TECH-EVID-001 | S3 privado local/CI, escáner adaptado y corpus seguro |
| Antes de HU-013 | TECH-JOBS-001 | Host Worker, bloqueo PostgreSQL, outbox y telemetría |
| Antes de HU-035 | TECH-OPS-001 | Imagen OCI, manifiesto de staging, respaldo y réplica verificables |
| Antes de cerrar cada corte | TECH-E2E-<CV> | Demo automatizada, accesibilidad crítica y trazabilidad del corte |

## 8. Gate de corte

Un corte sólo se cierra si todas sus historias cumplen la Definición de Terminado, existe una demostración con datos sintéticos, pasan los casos positivos y negativos aprobados, y no hay defectos bloqueantes. El cierre de un corte no autoriza el siguiente si los documentos F07 no están aprobados o si aparece una contradicción de alcance.
