# Bootstrap administrativo de la primera cuenta DIRECCION

`TECH-ID-BOOT-001` se ejecuta mediante el proyecto administrativo `Sgol.Admin`, fuera del tráfico web. No existe endpoint HTTP de bootstrap.

## Entradas efímeras

El operador técnico debe inyectar estas variables exclusivamente en el proceso administrativo:

- `ConnectionStrings__Sgol`: conexión PostgreSQL del entorno.
- `SGOL_BOOTSTRAP_PERSON_CODE`: código estable de la persona aprobada.
- `SGOL_BOOTSTRAP_PERSON_DISPLAY_NAME`: nombre visible aprobado.
- `SGOL_BOOTSTRAP_USER_NAME`: usuario individual inicial.
- `SGOL_BOOTSTRAP_INITIAL_PASSWORD`: contraseña temporal, con al menos 14 caracteres.

La contraseña nunca se pasa como argumento de línea de comandos. `Sgol.Admin` elimina su variable del entorno del proceso inmediatamente después de leerla y no imprime valores de entrada, hashes ni detalles de excepción.

## Ejecución controlada

La migración vigente debe estar aplicada antes de ejecutar:

```powershell
dotnet run --no-build --configuration Release --project src/Sgol.Admin
```

Una ejecución satisfactoria crea persona y vigencia activas, cuenta individual, rol `DIRECCION` para `LOR-001`, marcador permanente y `audit_event` en una sola transacción. La cuenta queda con cambio de contraseña obligatorio y sin TOTP enrolado, por lo que requiere completar ambos pasos antes de usar otras funciones.

La tabla `direction_bootstrap` admite sólo la fila singleton aprobada. Una segunda ejecución, incluso concurrente, es rechazada por integridad PostgreSQL y no crea otro evento ni otra identidad. No se elimina ni modifica el marcador para reabrir la vía.

La salida `DIRECCION bootstrap completed` confirma únicamente el commit de la transacción. Cualquier otro resultado exige revisión técnica; no se repite el comando modificando datos manualmente.
