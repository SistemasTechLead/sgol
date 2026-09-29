# TECH-BASE-003 — Optimización del pipeline de PR

## Alcance y base

Cambio técnico autorizado por «Vamos con la ejecucion entera del plan», el 2026-09-29, sobre `9518597dcb4e4d321a82285f31a3b4c22773b536`, rama `codex/optimize-pr-gates`. Contrato: `F07_ADENDA_52_OPTIMIZACION_DEL_PIPELINE_TECH_BASE_003.md`. No se modifica `src/`, Dockerfile, dependencias, migraciones, pruebas funcionales, scripts operativos, `AGENTS.md`, contratos congelados ni `Fuentes/`.

## Matriz de conservación

| Control previo | Destino | Garantía conservada |
|---|---|---|
| Checkout | Todos los jobs PR | Cabeza exacta, historial completo, credenciales no persistentes; comprobación adicional de `git rev-parse HEAD` |
| Fuentes | `controls` | Rechazo de cambios no autorizados entre base/cabeza |
| Gitleaks | `controls` | Versión, SHA-256, escaneo de árbol e historial y redacción intactos |
| Contrato portátil | `controls` | Mismo validador estático |
| Validadores condicionales CV-05 | `controls` | Mismas condiciones, repositorio público e inventario |
| SDK/restore/build | `server`, `browser`, `operations` | SDK exacto `10.0.400`, restore con lock y build Release; independientes por runner |
| Unitarias/arquitectura/PostgreSQL/S3/EICAR | `server` | Comando/filtro y variable EICAR intactos |
| Instalación Chromium/WebKit y navegador | `browser` | Categoría completa `FRONT_BROWSER`, sin retirar escenarios |
| Formato y vulnerabilidades NuGet | `server` | Mismos comandos, rechazo directo/transitivo; antes de operaciones |
| CV-04 y evidencia | `operations` | Después de ambas suites, dos ciclos, SHA, sanitización, cleanup y condiciones de retención intactos |
| OCI/SBOM/procedencia y Trivy | `operations` | Mismo builder, ambas salidas, escaneo HIGH/CRITICAL y misma imagen exacta |
| TECH-OPS/cleanup/probe/HU-035 | `operations` | Mismo job AMD64 de imagen; orden, matriz, recursos privados y rechazo de diferencias intactos |
| Evidencia operativa/CV-05/PR #58 | `operations` | Pasos y condiciones originales; no hay transporte de evidencia entre jobs |
| Experimento A/B manual | `hu035-network-ab` | Bloque completo sin cambios, no acredita aceptación |
| Aceptación del PR | `verify` | Nombre estable; `always()` y denegación por cualquier resultado distinto de success o SHA distinto/ausente |

`scripts/ci/pr-gate-step-baseline.json` conserva el hash normalizado de los 32 pasos originales y el bloque manual de la base. Los pasos originales se trasladaron sin modificar sus cuerpos. Sólo checkout y preparación SDK/restore/build se repiten por aislamiento. El inventario se ejecuta como prueba en CI.

## Resultados locales

| Comando/control | Resultado |
|---|---|
| `scripts/ci/preflight.ps1` | Checkout inicial limpio; Fuentes limpia; SDK exacto ausente; sesión GitHub autenticada |
| `scripts/ci/test-pr-gate-results.ps1` | 47 casos sintéticos aprobados: éxito exacto, fallo/cancelación/omisión/pendiente en cada job, SHA ausente/distinto, jobs faltantes/adicionales y JSON inválido |
| `scripts/ci/test-pr-workflow.ps1` | 32 pasos originales preservados; SHA, permisos, dependencias, orden operativo y bloque manual verificados |
| `scripts/ci/test-pr-workflow-mutations.ps1` | 7 alteraciones rechazadas; sin escritura de fixtures ni efectos externos |
| Parser PowerShell | Scripts nuevos sin errores de sintaxis |
| `scripts/ci/validate-tech-ops.ps1` | PASS: contrato estático conservado |
| `scripts/ci/verify-fuentes-protection.ps1` | PASS en la base; se exige nuevamente sobre el commit del PR |
| `git diff --check` | PASS |
| Restore/build/test/format .NET locales | NO VERIFICADOS: preflight informa ausencia de `10.0.400`; no se cambia SDK ni configuración |
| PostgreSQL local | NO EJECUTADO: a cargo del desarrollador conforme a las instrucciones expresas de esta conversación; el pipeline remoto sigue siendo obligatorio |
| YAML/expresiones y ejecución de jobs | Pendiente de validación por GitHub en el PR de esta actualización |

## Medición y riesgos

Base verde: `36639787899` 25:08, `36609761032` 26:49 y `36495586807` 25:15. Se elimina la suma secuencial de servidor y navegador; operaciones espera ambas suites. La preparación repetida añade CPU/minutos de runner, aunque puede reducir tiempo de espera. Estimación revisada: aproximadamente 17–20 minutos sin colas, pendiente de medición; no se promete 10–14 manteniendo esta secuencia.

La medición remota se resuelve por el PR y sus checks, sin añadir después un commit administrativo sólo para registrar el run. Debe comparar duración desde el primer job hasta el check final, tiempos por bloque y suma de minutos de runner; un único run permite observar ahorro, no demostrar estabilidad estadística. No se cambia presupuesto, visibilidad ni facturación. El comportamiento condicional CV-05/PR #58 se conserva y se comprueba estáticamente, pero esos caminos no se ejecutan en este PR ordinario.

HU-035 y cachés se evaluaron: los logs mezclan arranque de procesos con operaciones de contenedores/restore; no hay medición suficiente para consolidar mutaciones sin alterar la evidencia. Restore tarda 10–13 s y SDK 6–9 s. Se conserva la orquestación y no se introduce caché ni dependencia nueva. La limpieza de ramas históricas no reduce el tiempo porque sus pasos están omitidos en PR ordinarios.

## Cierre propuesto

Commit implementado: `commit que contiene esta actualización`. PR: `PR que incorpora esta actualización`. Check requerido: `TECH-BASE-003 / PR gates`, sobre la cabeza exacta. Revisión humana y merge pendientes. El trabajo no se declara Terminada y no modifica el orden ni el cierre de las historias funcionales.
