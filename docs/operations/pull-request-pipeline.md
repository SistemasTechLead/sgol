# Pipeline de pull request para TECH-BASE-003

El workflow `.github/workflows/pull-request.yml` se ejecuta exclusivamente para `pull_request`. No despliega, publica imágenes ni accede a staging o producción. El job usa `permissions: contents: read`, no conserva credenciales de checkout y no recibe secretos del repositorio. El checkout fija `github.event.pull_request.head.sha`; todas las pruebas, etiquetas OCI y evidencias corresponden al commit implementado, no al merge sintético expuesto como `GITHUB_SHA` por el evento.

## Gates

Los pasos tienen nombres independientes para que GitHub identifique el gate que falla:

1. checkout completo sin credenciales persistentes;
2. rechazo de cualquier cambio entre base y cabeza del PR dentro de `Fuentes/`, salvo la transición excepcional, exacta y de un solo uso registrada en `scripts/ci/fuentes-approved-rebaseline.json`;
3. escaneo de secretos sobre el árbol y los commits del PR con salida redactada;
4. SDK .NET exactamente `10.0.400`;
5. `dotnet restore --locked-mode`;
6. `dotnet build --no-restore --configuration Release` con analizadores .NET, nivel recomendado vigente del SDK y advertencias tratadas como errores;
7. `dotnet test --no-build --configuration Release`, que ejecuta unitarias, arquitectura, validación de la plantilla de trazabilidad e integración con PostgreSQL real y efímero mediante Testcontainers;
8. `dotnet format --verify-no-changes`;
9. consulta de paquetes NuGet directos y transitivos contra vulnerabilidades conocidas. Cualquier hallazgo falla mientras no exista un tratamiento explícitamente aprobado;
10. gate integral `TECH-OPS-001` en el runner nativo x64: aprovisionamiento sintético, migración, backup/reintento, réplica/reintento y verificaciones aisladas;
11. conservación, según la política de artefactos configurada en el repositorio, del layout OCI con SBOM/procedencia y de la evidencia técnica pública minimizada: digest, conteos, timestamps, etapa, resultado e inspección de imagen. El runtime privado, TRX detallados, credenciales S3, nombres/URIs de buckets y manifiestos con keys/hashes quedan excluidos. La retención de CI no hereda silenciosamente los 30 días del respaldo PostgreSQL. El layout con attestations se construye mediante un builder efímero `docker-container`, porque el driver `docker` con image store clásico no admite el índice requerido por SBOM/procedencia.

La prueba de integración conserva la imagen PostgreSQL fijada por `TECH-BASE-002`, genera su credencial en memoria y elimina el contenedor al terminar. No se admite SQLite ni un mock como sustituto.

La excepción de rebaselización sólo acepta `Fuentes/SGOL v2.0 Sistema de Gestion Operativa Loretta - S050_BKP_PRE_NORMALIZACION_V1.xlsm` cuando el PR parte del blob Git anterior `07688ab93b219317118099e1954ad2715d1004f8` y llega exactamente al SHA-256 `77C6761B9FF4F390A2D19AE9CBEAA66CF67B2992C3C3340F39372E46229FD302`. Una vez incorporada esa transición, el blob base deja de ser el anterior y la excepción no puede reutilizarse; cualquier modificación posterior vuelve a ser rechazada.

Las pruebas de arquitectura de `TECH-BASE-004` recorren los proyectos y fuentes bajo `src/` y emiten diagnósticos `ARCH-001` a `ARCH-004`: aislamiento de `Domain`, hosts Web/Worker sin reglas de negocio, ausencia de acceso a la implementación interna de otro módulo y dirección de dependencias de `Sgol.BuildingBlocks`. Un escenario sintético prohibido comprueba que las cuatro reglas detectan y nombran la infracción. La plantilla `docs/traceability/TEST_EVIDENCE_TEMPLATE.md` también se valida dentro de `dotnet test`, por lo que el mismo gate protege sus campos mínimos y advertencias de seguridad.

El análisis estático conserva dos excepciones estrechas en `.editorconfig`: CA1707 únicamente en pruebas xUnit, cuyos nombres con guiones bajos expresan el escenario trazable, y CA1852 únicamente en artefactos de migración generados por EF Core, que serían sobrescritos por la herramienta. No se desactiva globalmente ningún analizador.

## Versiones, integridad y licencias

| Componente | Versión fijada | Integridad | Licencia revisada |
|---|---:|---|---|
| `actions/checkout` | 7.0.1 | SHA de commit `3d3c42e5aac5ba805825da76410c181273ba90b1` | MIT |
| `actions/setup-dotnet` | 6.0.0 | SHA de commit `a98b56852c35b8e3190ac28c8c2271da59106c68` | MIT |
| `actions/upload-artifact` | 7.0.1 | SHA de commit `043fb46d1a93c77aae656e7c1c64a875d1fc6a0a` | MIT |
| Gitleaks | 8.30.1 | SHA-256 Linux x64 `551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb` | MIT |
| .NET SDK | 10.0.400 | `global.json`, entrada exacta del workflow y comprobación en ejecución | MIT y licencias .NET aplicables |
| PostgreSQL de integración | `postgres:18.6-alpine3.23` | etiqueta de imagen aprobada en `TECH-BASE-002` | PostgreSQL License |

Gitleaks se descarga desde el release oficial, se valida antes de extraer y se ejecuta sin `gitleaks-action`, licencias comerciales ni credenciales. No se escriben reportes de hallazgos como artefactos: el log conserva el gate, ruta y regla necesarias para investigar, pero `--redact` evita mostrar el valor detectado.

`.gitleaksignore` contiene una sola excepción por fingerprint para el falso positivo histórico del commit `401b422efa2a02a87f325311c92543c795dff75c`, ruta `F07_ADENDA_32_CONTRATO_DE_OPERACION_PORTABLE_TECH_OPS_001.md`, regla `generic-api-key` y línea 331. El texto vigente ya eliminó la expresión que originó el hallazgo. El validador TECH-OPS exige coincidencia exacta y rechaza entradas adicionales; no se desactiva la regla ni se excluye el archivo o su historial completo.

La revisión del 2026-08-28 no encontró avisos publicados en las páginas de seguridad de [`actions/checkout`](https://github.com/actions/checkout/security/advisories), [`actions/setup-dotnet`](https://github.com/actions/setup-dotnet/security/advisories) ni [Gitleaks](https://github.com/gitleaks/gitleaks/security/advisories). Es una revisión puntual: las versiones deben reevaluarse cuando se actualice el workflow o aparezca un aviso nuevo.

## Verificación local

Desde la raíz, con .NET, Git y Docker operativos:

```powershell
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release
dotnet format --verify-no-changes
./scripts/ci/Assert-NoVulnerablePackages.ps1
```

La sintaxis y el comportamiento del workflow sólo quedan verificados en GitHub cuando se publica la rama y se abre un pull request. Un resultado local no sustituye esa ejecución ni la revisión humana.

El orquestador `scripts/operations/invoke-tech-ops-amd64-gate.ps1` no conoce GitHub y exige únicamente host x86-64, Docker, PowerShell y la imagen `linux/amd64` ya cargada. Por ello puede trasladarse en el futuro a un equipo físico Intel o AMD x86-64 sin cambiar comandos, contratos ni datos sintéticos. En ARM64 falla cerrado con `AMD64_HOST_REQUIRED`; la emulación no se presenta como evidencia equivalente.
