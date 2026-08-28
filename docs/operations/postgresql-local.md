# PostgreSQL local y de CI para TECH-BASE-002

La prueba `Sgol.IntegrationTests.PostgreSqlPersistenceTests` crea una instancia desechable y aislada de PostgreSQL mediante Testcontainers. Usa la imagen oficial fijada `postgres:18.6-alpine3.23`, genera la contraseña en memoria para cada ejecución, aplica dos veces la migración inicial y elimina el contenedor al terminar.

## Requisitos

- SDK .NET fijado por `global.json`.
- Un runtime de contenedores compatible con Docker y accesible por Testcontainers.
- Acceso al registro de contenedores la primera vez que se descarga la imagen fijada.

No se necesita una cadena de conexión versionada para las pruebas. Local y CI reciben recursos y credenciales efímeros; no deben apuntar a staging ni producción.

## Verificación aislada

Desde la raíz del repositorio:

```powershell
dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --configuration Release
```

La prueba pasa sólo si abre una conexión real, registra exactamente `20260827000000_InitializePersistence`, una segunda aplicación no cambia el resultado y el esquema `public` contiene únicamente `__EFMigrationsHistory`.

## Ejecución del host contra PostgreSQL local

El host obtiene la conexión mediante la clave estándar `ConnectionStrings:Sgol`. Debe inyectarse fuera de Git, por ejemplo con la variable de entorno `ConnectionStrings__Sgol`. El endpoint `/health/live` permanece independiente de PostgreSQL; resolver `SgolDbContext` sin configuración falla de forma explícita.

La identidad de aplicación y la identidad temporal de migración se separarán cuando se implemente el despliegue correspondiente. TECH-BASE-002 no crea usuarios productivos, secretos persistentes ni scripts de despliegue.
