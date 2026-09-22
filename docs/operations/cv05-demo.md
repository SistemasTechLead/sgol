# Demo automatizada y cierre de `CV-05`

`TECH-E2E-CV-05` se rige por la Adenda 43 aprobada. El proyecto aislado `tests/Sgol.Cv05Demo` ejercita los contratos efectivos de `HU-029`, `HU-032`, `HU-033`, `HU-034` y `HU-035` mediante los endpoints y servicios productivos existentes. No incluye UI; navegador y accesibilidad se registran `NO_APLICA`.

## Comando y precondiciones

En Windows o Linux AMD64, con .NET 10, Docker Linux y compilación Release de `Sgol.Web`, `Sgol.Admin`, `Sgol.Worker`, `Sgol.Operations` y el proyecto demo:

```powershell
./scripts/demo/run-cv05.ps1 -Mode Automated
```

No se suministran conexiones, cuentas, secretos, URLs ni parámetros de tiempo. El launcher lee el SHA local, captura el exit nativo y ejecuta dos ciclos completos. `0` sólo acredita todos los escenarios, reportes y cleanup. Un fallo retorna un código cerrado y exit distinto de cero. Cada ciclo usa red, PostgreSQL de origen y restore, dos almacenes S3 compatibles, ClamAV, certificado, Kestrel HTTPS y cuentas persistidas nuevos. Las cuentas pasan CSRF, login, cambio de contraseña, MFA y cookie hospedados. Secretos y temporales privados se generan en runtime y se eliminan.

## Semilla y matriz

`CV05-SEED-V1` crea precondiciones sintéticas basales: identidades, publicaciones de ocho TAR, período, cinco obligaciones, asignaciones y políticas. Una asignación se obtiene por el servicio productivo. Evidencia, conclusión y validación demostradas se obtienen por HTTPS hospedado. Los negativos de recuperación alteran sólo el restore o un objeto sintético de destino. La semilla no inserta indicadores, vistas, eventos de auditoría, resultados de reconciliación, aprobaciones ni recuperaciones.

La matriz cerrada `S01-S22` se encuentra en la sección 7 de la Adenda 43. `S01-S05` verifican indicadores y Dirección; `S06-S08`, auditoría; `S09-S12`, idempotencia; `S13-S19`, solicitud, backup, réplica, restore, reconciliación, aprobación y fallos controlados; `S20`, autenticación hospedada; `S21`, huella funcional reproducible; `S22`, limpieza. Dos ciclos nuevos deben coincidir en la huella pública. Una consulta de reconciliación sí inserta el evento de vista previsto por `HU-035`.

## Evidencia y CI

El comando escribe `.artifacts/cv05/latest/TECH-E2E-CV-05-report.json` y `.md`, fuera de Git. El validador exige schema, catálogo, dos ciclos, SHA, digest OCI, estados separados y campos sanitizados. Un fallo anterior puede conservarse como `previous-failure`; los archivos condicionales de diagnóstico HU-035 son opcionales y el inventario comprueba tanto su presencia como su ausencia. Un archivo obligatorio ausente bloquea.

Con repositorio público y presupuesto `$0`, el primer pipeline intenta artifacts originales descargables con retención de 14 días: reporte CV-05, evidencia operativa/CV-04 requerida y layout OCI. Se verifican IDs, digest, cabeza SHA y expiración por API. La prueba mínima pública del run `35779296378` sólo acredita una subida pequeña. Si el pipeline completo falla específicamente por cuota, se exige una prueba enfocada de la causa antes de un nuevo SHA. La contingencia de resumen/log sanitizados requiere excepción de retención visible `RETENTION_EXCEPTION_CV05` y `VALIDACION_DIFERIDA_POR_CUOTA`; no acredita JSON/Markdown originales ni OCI descargable. No se reintenta `upload-artifact` esperando que borrar artifacts reduzca el consumo acumulado.

## Límites y diagnóstico

El reporte público incluye sólo fase, escenario, estado, exit, duración, código cerrado y conteos permitidos. No se publican contraseñas, TOTP, recovery codes, cookies, CSRF, conexiones, SQL, payloads, respuestas sensibles, excepciones, rutas privadas ni material criptográfico. El fallo primario conserva su código aunque falle cleanup; cleanup fallido bloquea. La salida posterior `docker ps` vacía puede significar limpieza correcta, por eso se observa Testcontainers mientras corre.

La ejecución es local y sintética. No despliega, no usa cloud ni datos reales y no habilita frontend ni cortes posteriores. Durante la validación, el responsable autorizó tres correcciones productivas mínimas para completar la traza de validación, auditar el intento autenticado de borrar un evento y registrar en Web el handler de referencia de recuperación. No requieren migración ni cambian los contratos aceptados. El merge requiere autorización explícita tras check verde sobre la cabeza exacta del PR.
