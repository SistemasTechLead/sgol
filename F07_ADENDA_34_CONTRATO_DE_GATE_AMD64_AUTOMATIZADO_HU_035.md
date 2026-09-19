# F07 Adenda 34 — Gate AMD64 automatizado del simulacro HU-035

## 1. Control de la propuesta

| Campo | Valor |
|---|---|
| Historia | `HU-035` — Dirección verifica recuperación con identidades e historia |
| Capacidad | `CAP-047` |
| Criterios | `CA-035`, `CP-035-P`, `CP-035-N` |
| Contrato base | `F07_ADENDA_33_CONTRATO_DE_RECONCILIACION_Y_SIMULACRO_DE_RECUPERACION_HU_035.md` |
| Estado | PROPUESTA; sin eficacia hasta aprobación humana íntegra |
| Motivo | El único equipo local disponible es ARM64 y la imagen contractual `linux/amd64` falla bajo emulación en EF/Npgsql; el pipeline existente dispone de runner AMD64 nativo, pero aún no ejecuta el simulacro funcional |

Esta adenda no modifica F00–F07 ni `Fuentes/`. Complementa únicamente las secciones 19.2, 20 y 21 de la Adenda 33. Su aprobación autoriza implementar y validar localmente el orquestador y el paso de pipeline descritos aquí. No autoriza commit, publicación, PR, uso de datos o secretos reales, despliegue ni merge.

## 2. Hechos y límite observado

1. La imagen local de `HU-035` fue construida como `linux/amd64` con smoke, SBOM y procedencia.
2. El host disponible es ARM64. La ejecución emulada falló antes de migrar, al construir en EF/Npgsql el mapeo existente `uuid[]`, con `TargetInvocationException` y causa `NullReferenceException`.
3. PostgreSQL sintético estaba accesible y la misma migración se aplicó en Testcontainers nativo del host; el fallo no demuestra un defecto de PostgreSQL ni autoriza adaptar el dominio al emulador.
4. El workflow vigente ya usa `ubuntu-24.04` x64 y valida el gate AMD64 de `TECH-OPS-001`, pero no prepara ni ejecuta el simulacro integral de `HU-035`.
5. `verify-hu-035-external.ps1` consume artefactos ya preparados; por sí solo no crea el actor, la historia funcional, la solicitud, la referencia, los negativos ni la evidencia de restore.

## 3. Decisión contractual propuesta

1. El simulacro integral de `HU-035` se ejecuta en el mismo job AMD64 nativo que construye la imagen del SHA exacto del PR, después del gate `TECH-OPS-001`.
2. Un orquestador específico, no reutilizable como plataforma general, crea recursos Docker efímeros y datos exclusivamente sintéticos, ejecuta el flujo completo y siempre intenta limpiar contenedores, red, archivos con credenciales y temporales.
3. La prueba no depende de secretos de GitHub, servicios cloud, registros externos ni datos persistentes entre runs. Credenciales, certificados y llaves son aleatorios, efímeros y permanecen en `$RUNNER_TEMP`.
4. La imagen probada es exactamente la construida por el workflow para `IMPLEMENTATION_SHA`; no se usa tag mutable como evidencia.
5. Un run ARM64, una ejecución emulada, un gate omitido o un artifact incompleto falla cerrado y no satisface `HU-035`.

## 4. Dataset sintético mínimo y creación autorizada

1. El orquestador aplica todas las migraciones hasta `20260914210503_AddRecoveryReconciliation` sobre PostgreSQL nuevo y vacío.
2. Una utilidad de prueba externa y acotada crea mediante contratos de servidor o persistencia modular existente: sucursal `LOR-001`; Dirección autorizada; una segunda identidad sin permiso; cuentas, empleo y roles vigentes e históricos; configuración y catálogos versionados; semana, plan, obligaciones, asignaciones y ejecución; evidencia de archivo y estructurada; sustitución y validación; idempotencia, outbox, aviso y auditoría.
3. Deben existir al menos dos versiones y dos vínculos donde el agregado los soporte. Los objetos S3 privados tienen tamaño y SHA-256 coherentes con `file_object`.
4. Los identificadores y timestamps sintéticos son deterministas respecto de una semilla pública no sensible del SHA. Las credenciales y llaves nunca son deterministas ni se imprimen.
5. La preparación no inserta resultados de reconciliación exitosos, no modifica snapshots y no fabrica datos para compensar diferencias.

## 5. Flujo positivo obligatorio

1. Crear la solicitud como Dirección aplicando `PER-CONTINUIDAD-VER` en servidor; conservar `reconciliationId` y ETag.
2. Ejecutar Worker/outbox y `CAPTURE_RECOVERY_REFERENCE` con snapshot PostgreSQL exportado y manifiesto privado canónico.
3. Generar backup cifrado y réplica `COMPLETE` después de la referencia; ejecutar restore técnico sobre base nueva, vacía y distinta del origen.
4. Capturar en archivo externo la salida JSON `SGOL_TECHNICAL_RESTORE_EVIDENCE` sin secretos.
5. Ejecutar `complete-functional-reference`, `reconcile-functional-restore`, consulta auditada y aprobación de Dirección.
6. Exigir `FUNCTIONAL_RECOVERY_MATCHED`, `APPROVED`, 39 tablas completas, cero diferencias, objetos y auditoría íntegros, `databaseRpo=0`, `observedRpo<=3600` y `observedRto<=14400`.
7. Repetir asociación y reconciliación para demostrar idempotencia sin nuevos efectos.

## 6. Negativos obligatorios

Cada caso usa reconciliación y restore descartables independientes y debe comprobar código o diferencia estable, estado terminal no aprobable y conteos funcionales sin compensación:

- identidad faltante y adicional;
- vínculo faltante y alterado;
- versión y conteo distintos;
- evidencia faltante, corrupta e inaccesible;
- auditoría faltante y alterada;
- manifiesto de referencia corrupto;
- RPO mayor de 3 600 segundos y RTO mayor de 14 400 segundos;
- destino de restore igual al primario;
- replay idéntico, contenido conflictivo y concurrencia.

No se permite presentar una reconciliación parcial como éxito ni editar manualmente un resultado.

## 7. Evidencia y seguridad del artifact CI

1. El artifact público del job contiene sólo: resumen de etapas y resultados, TRX, revisión Git, digest OCI, migración, hashes de manifiestos, conteos, clases/códigos de diferencia, RPO/RTO, confirmación de aislamiento, no-efecto y limpieza.
2. Se excluyen `runtime.env`, cadenas de conexión, contraseñas, llaves, certificados, cookies, tokens, URLs firmadas, URI privadas completas, nombres de objeto, contenido de evidencia y payloads de auditoría.
3. Antes de subir el artifact, un validador de ausencia de secretos inspecciona nombres y contenido permitido. Cualquier coincidencia bloquea el job.
4. Los reportes privados completos permanecen sólo durante el run en `$RUNNER_TEMP` y no se publican. El workflow no implementa purga funcional ni modifica políticas de retención del repositorio.

## 8. Idempotencia, aislamiento y limpieza

1. Los nombres de proyecto, red, contenedores, volúmenes lógicos y directorios incluyen una identidad acotada del run para evitar colisiones.
2. Origen y restore son bases distintas; el restore exige destino nuevo/vacío y rechaza la conexión primaria.
3. Cada negativo parte de un restore independiente. No se borra ni altera el origen para producir el caso.
4. La limpieza se ejecuta en `finally` y sólo apunta a recursos creados por el run. Un fallo de limpieza deja el job fallido y visible.
5. No se usan `--clean`, drop sobre origen, recursos compartidos ni nombres globales reutilizables.

## 9. Archivos autorizados

- `scripts/operations/invoke-hu-035-amd64-gate.ps1`;
- `scripts/operations/verify-hu-035-external.ps1`, sólo si requiere composición no destructiva con el nuevo orquestador;
- `tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryExternalTests.cs` y utilidades sintéticas dentro del mismo proyecto;
- `.github/workflows/pull-request.yml`, únicamente para invocar el gate y publicar evidencia sanitizada;
- `scripts/ci/validate-hu-035.ps1`;
- `docs/operations/functional-recovery-reconciliation.md`;
- `docs/traceability/IMPLEMENTATION_STATUS.md`;
- proyectos y lockfiles sólo si son estrictamente necesarios.

No se autoriza UI, endpoint adicional, infraestructura permanente, recurso cloud, SDK proveedor, cambio funcional de otra HU ni archivo dentro de `Fuentes/`.

## 10. Gates de aceptación

1. Validación estática del orquestador, arquitectura, rutas protegidas y ausencia de secretos.
2. Pruebas unitarias enfocadas del dataset, matriz positiva/negativa, canonicalización, códigos, no-efecto y sanitización.
3. Compilación y descubrimiento local de las pruebas externas sin ejecutar PostgreSQL/Docker dentro de la sesión Codex.
4. Gates locales finales de la Adenda 33 una sola vez después de estabilizar el cambio.
5. En el PR, runner AMD64 nativo: imagen del SHA exacto, gate `TECH-OPS-001`, simulacro positivo y todos los negativos de `HU-035`, evidencia sanitizada y limpieza.
6. Cualquier caso omitido, skipped, inconcluso o ejecutado sobre digest distinto falla el pipeline.

## 11. Condición de cierre

La ejecución verde del nuevo gate es evidencia necesaria pero no suficiente. `HU-035` conserva todas las condiciones de cierre de la Adenda 33 y `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md`: contrato aprobado, implementación y trazabilidad en el mismo commit, pipeline del SHA exacto, aprobación humana, merge, ascendencia en `origin/master`, protección de `Fuentes/` y cero defectos bloqueantes.
