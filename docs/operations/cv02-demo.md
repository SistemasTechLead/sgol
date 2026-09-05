# Operación de la demo automatizada de CV-02

`TECH-E2E-CV-02` se ejecuta fuera del producto mediante `tests/Sgol.Cv02Demo`. El host escucha sólo en un puerto efímero de `127.0.0.1`, crea PostgreSQL desechable desde migraciones reales y elimina contenedor, base, navegador y host al terminar.

## Preparación externa única

La instalación de navegadores se realiza fuera de la sesión de Codex, después de compilar el proyecto, con la versión fijada por `Microsoft.Playwright` 1.62.0:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\install-cv02-browsers.ps1
```

Si una instalación anterior terminó abruptamente y dejó exactamente `.artifacts/cv02/browsers/__dirlock`, se comprueba primero que no siga activo otro instalador y se ejecuta una única reparación explícita:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\install-cv02-browsers.ps1 -RepairStaleLock
```

El script resuelve y valida la ruta absoluta antes de eliminar exclusivamente ese lock. Si la instalación falla, produce un error y la demo no debe ejecutarse. El wrapper de ejecución también rechaza un lock presente antes de iniciar PostgreSQL.

No se admite una ruta, imagen, conexión, credencial, fecha, actor, sucursal, TAR o semilla proporcionada por el operador. Antes de ejecutar, deben estar ausentes `ConnectionStrings__Sgol`, `DATABASE_URL`, `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER` y `PGPASSWORD`.

La única dependencia nueva es `Microsoft.Playwright` 1.62.0 (licencia MIT declarada en el paquete .NET), fijada centralmente y registrada en `tests/Sgol.Cv02Demo/packages.lock.json`. Chromium 151.0.7922.34 y WebKit 26.5 son los navegadores aprobados; se instalan sólo con el script versionado que genera ese paquete. La revisión de vulnerabilidades forma parte del gate `Assert-NoVulnerablePackages.ps1`.

## Comandos canónicos

Automatización headless completa:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\run-cv02.ps1 -Mode Automated
```

Para el gate externo, la suite completa `Sgol.IntegrationTests` con PostgreSQL/Testcontainers, la instalación versionada de navegadores y la demo se encadenan con comprobación estricta del código de salida. Un fallo detiene los pasos restantes:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\run-cv02-external-gate.ps1
```

Después de una instalación interrumpida cuyo lock se haya comprobado como obsoleto, se usa `-RepairStaleBrowserLock` una sola vez.

Exhibición humana por teclado en las tres rutas cerradas:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\run-cv02.ps1 -Mode Interactive
```

El modo interactivo imprime la URL local y finaliza con `Ctrl+C`. Las páginas disponibles son `/cv02/calendario`, `/cv02/recurrencias` y `/cv02/plan-semanal`; cualquier otra ruta funcional queda fuera del contrato.

## Evidencia y limpieza

Cada ejecución reemplaza `.artifacts/cv02/latest/` y genera:

- `TECH-E2E-CV-02-report.json`;
- `TECH-E2E-CV-02-report.md`;
- capturas, trazas y videos únicamente cuando falla el navegador.

El reporte contiene commit, entorno, tiempos UTC, identidad y hash de la semilla, cobertura HU/CA/CP, resultados, hechos sintéticos acotados, artefactos y defecto relacionado. No contiene contraseñas, TOTP, cookies, conexiones, SQL, PII, datos reales, payloads completos ni evidencia funcional.

Una salida distinta de cero, un escenario incompleto, un código `CV02_*`, un fallo de limpieza o un artefacto de error impiden aceptar la demostración. La ejecución satisfactoria no cierra por sí sola `TECH-E2E-CV-02` ni `CV-02`: aún requiere pipeline verde sobre el commit exacto, aprobación humana, merge y ascendencia en `origin/master`.
