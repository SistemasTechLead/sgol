# Demo automatizada y cierre técnico de `CV-03`

## Contrato operativo

`TECH-E2E-CV-03` se ejecuta exclusivamente mediante `tests/Sgol.Cv03Demo`. Es un harness de prueba separado de `Sgol.Cv02Demo`: cada corte conserva su propio catálogo, semilla, infraestructura, reportes y condiciones de cierre. Ningún proyecto de `src/` referencia el harness y el harness no agrega rutas, vistas, migraciones ni comportamiento productivo.

La modalidad aprobada es sólo `Automated`; no existe modo interactivo, UI técnica ni ejecución Playwright. Chromium, WebKit y accesibilidad quedan como `NO_APLICA` para este corte.

Desde el checkout canónico, el comando único es:

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\run-cv03.ps1 -Mode Automated
```

El script rechaza otra raíz Git u OneDrive, obtiene el commit exacto desde `HEAD`, habilita únicamente el corpus EICAR sintético de prueba durante el proceso y restaura las variables temporales al terminar. No instala, diagnostica ni invoca Docker directamente.

## Infraestructura real y ciclo de vida

La corrida externa crea una red privada y recursos desechables con imágenes fijadas:

- PostgreSQL `postgres:18.6-alpine3.23` con base y credenciales aleatorias exclusivas `sgol_cv03_*`.
- SeaweedFS S3-compatible `chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5`, dos buckets privados y credenciales efímeras de privilegio mínimo.
- ClamAV `clamav/clamav:1.5.4-debian@sha256:be3cb41d9833ce9ffb98f3d3e1483c35c0d87060c2bda3624d75fd28bbf0b3bd` con `clamd` real y límites cerrados.

El orden es red, PostgreSQL, validación de base desechable, SeaweedFS, aprovisionamiento S3, ClamAV, host SGOL sólo en `127.0.0.1`, migración desde cero y semilla. Los health checks esperan los puertos internos aprobados; el host sólo se acepta si publica en loopback. El cierre detiene host, escáner, almacenamiento, PostgreSQL y red, y elimina el directorio temporal incluso después de fallos.

SQLite y mocks están prohibidos como evidencia de persistencia, almacenamiento o análisis antimalware. No se agregan Redis, broker, microservicios, scheduler ni infraestructura nueva. La sesión Codex no ejecuta ni diagnostica los contenedores: la corrida completa corresponde al desarrollador fuera de la sesión.

## Escenarios cerrados

| ID | Escenario | HU/CA/CP | Regla o decisión |
|---|---|---|---|
| `S01` | Responsable consulta sólo trabajo permitido | `HU-023`; `CA-023`; `CP-023-P` | `RN-002`, `RN-025`, `RN-027` |
| `S02` | Tarea ajena converge sin filtración | `HU-023`; `CA-023`; `CP-023-N` | `DEC-044`, `DEC-055`, `DEC-056` |
| `S03` | Obligación conserva política congelada | `HU-024`; `CA-024`; `CP-024-N` | `RN-006`, `RN-008`, `RN-017`, `DEC-021` |
| `S04` | Evidencia estructurada por servicio real | `HU-025`, `TECH-EVID-002`; `CA-025`; `CP-025-P` | `RN-018`, `RN-023`, `RN-027` |
| `S05` | Binario limpio atraviesa S3 y ClamAV reales | `HU-025`, `TECH-EVID-001`; `CA-025`; `CP-025-P` | Adendas 17 y 18 |
| `S06` | Archivo inválido o infectado nunca satisface evidencia | `HU-025`, `TECH-EVID-001`; `CA-025`; `CP-025-N` | Denegación cerrada |
| `S07` | Sustitución conserva historia completa | `HU-025`; `CA-025`; `CP-025-P` | `RN-018`, `RN-023`, `RN-027`, `DEC-067`, `DEC-068` |
| `S08` | Evidencia `SUSTITUIDA` no satisface requisito | `HU-025`, `HU-026`; `CA-026`; `CP-026-N` | `RN-017`, `RN-018`, `RN-019` |
| `S09` | `TAR-0092` hace aplicable la fotografía | `TECH-EVID-002`, `HU-026`; `CA-026`; `CP-026-P` | `F_ENT_001`, `DIFERENCIA_O_DANO` |
| `S10` | `TAR-0092` hace no aplicable la fotografía | `TECH-EVID-002`, `HU-026`; `CA-026`; `CP-026-P` | `F_ENT_001`, `DIFERENCIA_O_DANO` |
| `S11` | Requisitos aplicables producen `COMPLETA` | `HU-026`; `CA-026`; `CP-026-P` | `RN-017`, `RN-018`, `RN-019`, `DEC-022` |
| `S12` | Faltante exacto produce `INCOMPLETA` | `HU-026`; `CA-026`; `CP-026-N` | `RN-017`, `RN-018`, `RN-019`, `DEC-022` |
| `S13` | Conclusión incompleta se rechaza sin efecto | `HU-022`; `CA-022`; `CP-022-N` | `RN-016` a `RN-019` |
| `S14` | Conclusión completa persiste snapshot y resultado exactos | `HU-022`; `CA-022`; `CP-022-P` | `RN-016` a `RN-019` |
| `S15` | Repetición de conclusión devuelve el mismo resultado | `HU-022`; `CA-022`; `CP-022-P` | Idempotencia HU-022 |
| `S16` | Carrera de conclusión deja un ganador | `HU-022`; `CA-022`; `CP-022-N` | Serialización HU-022 |
| `S17` | Bandeja propia muestra futura, disponible y vencida | `HU-030`; `CA-030`; `CP-030-P` | `RN-019`, `RN-025`, `RN-029` |
| `S18` | Vencimiento no cambia `execution_status` | `HU-023`, `HU-030`; `CA-030`; `CP-030-P` | `RN-019`, `RN-025` |
| `S19` | Asignación produce un aviso interno | `HU-030`; `CA-030`; `CP-030-P` | `RN-029` |
| `S20` | Lectura de aviso es idempotente y audita atómicamente | `HU-030`; `CA-030`; `CP-030-P` | `RN-029` |
| `S21` | Consultas no escriben snapshots, resultados ni auditoría | `HU-023`, `HU-026`, `HU-030`; `CA-023`, `CA-026`, `CA-030`; `CP-023-P`, `CP-026-P`, `CP-030-P` | Sólo lectura |
| `S22` | No existe correo, SMS, push ni webhook | `HU-030`; `CA-030`; `CP-030-N` | `RN-029` |
| `S23` | No se crean validación, supervisión ni indicadores | `HU-022`, `HU-025`, `HU-026`, `HU-030`; CA y CP negativos correspondientes | Límites `CV-03` |
| `S24` | Segunda ejecución limpia es reproducible y sin residuos | `HU-022` a `HU-026`, `HU-030`; CA y CP positivos correspondientes | Unicidad, idempotencia y limpieza |

## Seguridad y datos

La semilla usa sólo actores, códigos, reloj e identificadores sintéticos deterministas. Los servicios productivos resuelven cuenta, MFA, empleo, rol, permiso y recurso; las consultas negativas convergen sin IDOR. Las mutaciones conservan la protección CSRF de los endpoints y las claves de idempotencia; conclusión y lectura de avisos verifican repetición y concurrencia. No se usan datos reales.

Los mensajes, reportes y artefactos se limitan a códigos técnicos aprobados. Nunca incluyen cadenas de conexión, credenciales, cookies, URLs firmadas, hashes privados, payloads estructurados, contenido binario ni datos personales. El corpus EICAR se construye sólo en memoria y no se escribe en Git ni en los reportes.

## Reportes, rotación y retención

La ruta exclusiva es `.artifacts/cv03/latest/`, ignorada por Git. Cada ejecución elimina el contenido previo de `latest`; si el reporte previo era `FAILED`, conserva únicamente esa última falla en `.artifacts/cv03/previous-failure/`. Los reportes son:

- `TECH-E2E-CV-03-report.json`: esquema `sgol.tech-e2e-cv03.report`, versión `1`, commit exacto, imágenes, semilla identificada por huella, escenarios, duración, estado `PASSED/FAILED`, código técnico y estado de limpieza.
- `TECH-E2E-CV-03-report.md`: resumen humano generado a partir del mismo estado.
- `failures/Snn.json`: sólo para el escenario fallido, con código allowlist y sin excepción cruda.

La evidencia humana de aprobación, pipeline, ejecución externa y merge permanece separada del harness. Un reporte `PASSED` no declara por sí mismo terminados `TECH-E2E-CV-03` ni `CV-03`.
