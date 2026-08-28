# SGOL

Base técnica mínima de SGOL para `TECH-INIT-001` y `TECH-BASE-002`. Contiene un host ASP.NET Core sin funciones de negocio, primitivas compartidas vacías y persistencia EF Core sobre PostgreSQL real. No incluye tablas funcionales, identidad, infraestructura de archivos, módulos funcionales ni despliegue.

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
```

Las versiones NuGet se administran en `Directory.Packages.props` y cada proyecto con paquetes mantiene su `packages.lock.json`. El modo bloqueado impide que la restauración resuelva versiones distintas de las registradas. El manifiesto local fija `dotnet-ef` para que las migraciones no dependan de una herramienta global.

## Ejecución local

```powershell
dotnet run --project src/Sgol.Web
```

El endpoint técnico `GET /health/live` responde `200 OK` con un estado de vida fijo. No consulta base de datos, almacenamiento, `Fuentes/` ni ninguna dependencia externa. La configuración versionada sólo ajusta logging y hosts permitidos; cualquier configuración local futura debe permanecer libre de secretos en Git.

La persistencia obtiene su cadena mediante `ConnectionStrings:Sgol`; para ejecución local se inyecta como `ConnectionStrings__Sgol` fuera de Git. La prueba de integración crea PostgreSQL y credenciales efímeros mediante Testcontainers. Consulta [docs/operations/postgresql-local.md](docs/operations/postgresql-local.md) para ejecutar y verificar la migración inicial vacía.

## Protección documental

`Fuentes/` es de solo lectura lógica y no se utiliza como entrada o salida de compilación, pruebas o ejecución. La trazabilidad inicial de esta tarea está en `docs/traceability/README.md`.
