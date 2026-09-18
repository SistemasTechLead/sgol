# SGOL

Base técnica incremental de SGOL. Contiene el host ASP.NET Core, primitivas compartidas, persistencia EF Core sobre PostgreSQL real, auditoría append-only y el comando administrativo de un solo uso de `TECH-ID-BOOT-001`. No incluye administración ordinaria de cuentas, endpoints de bootstrap, historias funcionales, infraestructura de archivos ni despliegue.

## Requisitos

- SDK .NET 10.0.400, fijado en `global.json`.
- Acceso al origen NuGet configurado por el entorno para la primera restauración.
- Un runtime de contenedores compatible con Docker para la prueba de integración PostgreSQL.
- Ningún secreto ni dato real se almacena en el repositorio.

## Restaurar, compilar y probar

Desde la raíz del repositorio:

```powershell
dotnet restore --locked-mode
dotnet tool restore
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release
dotnet format --verify-no-changes
./scripts/ci/Assert-NoVulnerablePackages.ps1
```

Las versiones NuGet se administran en `Directory.Packages.props` y cada proyecto con paquetes mantiene su `packages.lock.json`. El modo bloqueado impide que la restauración resuelva versiones distintas de las registradas. El manifiesto local fija `dotnet-ef` para que las migraciones no dependan de una herramienta global.

## Ejecución local

```powershell
dotnet run --project src/Sgol.Web
```

El endpoint técnico `GET /health/live` responde `200 OK` con un estado de vida fijo. No consulta base de datos, almacenamiento, `Fuentes/` ni ninguna dependencia externa. La configuración versionada sólo ajusta logging y hosts permitidos; cualquier configuración local futura debe permanecer libre de secretos en Git.

La persistencia obtiene su cadena mediante `ConnectionStrings:Sgol`; para ejecución local se inyecta como `ConnectionStrings__Sgol` fuera de Git. La prueba de integración crea PostgreSQL y credenciales efímeros mediante Testcontainers. Consulta [docs/operations/postgresql-local.md](docs/operations/postgresql-local.md) para ejecutar y verificar la migración inicial vacía.

El bootstrap de la primera cuenta `DIRECCION` se ejecuta exclusivamente con `Sgol.Admin` y entradas efímeras. Consulta [docs/operations/direction-bootstrap.md](docs/operations/direction-bootstrap.md); no existe una cuenta predeterminada ni una ruta HTTP equivalente. El inicio de sesión hospedado, el primer acceso y la provisión sintética de los cuatro roles se documentan en [docs/operations/hosted-authentication-local.md](docs/operations/hosted-authentication-local.md).

El workflow de pull request aplica estos gates, análisis estático, escaneo de secretos, vulnerabilidades directas/transitivas y protección de `Fuentes/` sin desplegar ni usar credenciales de otros entornos. Consulta [docs/operations/pull-request-pipeline.md](docs/operations/pull-request-pipeline.md) para sus versiones fijadas, evidencia y límites de verificación.

## Protección documental

`Fuentes/` es de solo lectura lógica y no se utiliza como entrada o salida de compilación, pruebas o ejecución. La trazabilidad inicial de esta tarea está en `docs/traceability/README.md`.
