# F07 — Primera tarea de inicialización para Codex

## 1. Control

| Campo | Valor |
|---|---|
| ID | TECH-INIT-001 |
| Nombre | Inicializar la solución mínima verificable de SGOL |
| Estado | APROBADA E INCORPORADA A `Fuentes`; habilitada al verificarse la puerta de la sección 8 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Corte / épica | CV-00 / EP-00 |
| Tipo | Técnica de inicialización; sin función de negocio |

## 2. Resultado requerido

Crear una solución .NET 10 mínima, reproducible y compilable que establezca el host web, los primeros proyectos de pruebas, configuración común y límites iniciales, sin crear todavía base de datos, identidad, módulos funcionales, endpoints SGOL ni infraestructura de despliegue.

## 3. Fuentes autorizadas

- `F07_ESTRUCTURA_DEL_REPOSITORIO.md`, secciones 3 a 8.
- `F07_AGENTS_MD_PROPUESTO.md`, contenido propuesto y condición de adopción.
- `F07_DEFINICIONES_DE_CONTROL.md`, definiciones y gate de PR.
- `F06_ARQUITECTURA.md`, secciones 4, 5, 8 y 9.
- `F06_ESTRATEGIA_DE_PRUEBAS.md`, secciones 3 y 15.
- `F06_REGISTRO_ADR.md`, ADR-001, ADR-002, ADR-012 y ADR-013.

## 4. Alcance exacto

1. Adoptar en el `AGENTS.md` raíz las reglas aprobadas de `F07_AGENTS_MD_PROPUESTO.md`, fusionándolas con instrucciones vigentes que sigan aplicando.
2. Crear `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `SGOL.slnx` y `README.md` mínimos.
3. Crear `src/Sgol.Web` como host ASP.NET Core vacío con:
   - Razor Pages o MVC habilitado;
   - endpoint técnico de vida que no consulte dependencias;
   - configuración por ambiente sin secretos;
   - respuesta sin datos de negocio.
4. Crear `src/Sgol.BuildingBlocks` vacío salvo el marcador mínimo necesario para compilar.
5. Crear `tests/Sgol.UnitTests` y `tests/Sgol.ArchitectureTests` con xUnit.
6. Añadir un smoke test del host y una prueba inicial que impida que `Sgol.BuildingBlocks` dependa de `Sgol.Web`.
7. Crear `docs/traceability/README.md` con el formato de mapeo ID–criterio–prueba, sin inventar cobertura.
8. Fijar centralmente versiones de paquetes y generar el lockfile aplicable.
9. Verificar que ninguna salida o script escribe dentro de `Fuentes/`.

## 5. Fuera de alcance

- PostgreSQL, EF Core, migraciones y Testcontainers.
- ASP.NET Core Identity, login, MFA o usuarios.
- Los doce módulos funcionales y cualquiera de HU-001 a HU-035.
- Docker Compose, S3, escáner, Worker, outbox, CI/CD o despliegue.
- OpenAPI funcional de SGOL, autenticación, autorización o datos semilla.
- Reorganizar documentos existentes o modificar cualquier archivo dentro de `Fuentes/`.

## 6. Criterios de aceptación

| ID | Criterio |
|---|---|
| INIT-CA-001 | El SDK fijado corresponde a .NET 10 y restore bloqueado funciona desde una copia limpia del repositorio. |
| INIT-CA-002 | La solución compila en Release sin errores ni advertencias nuevas. |
| INIT-CA-003 | Las pruebas unitarias, de arquitectura y smoke pasan. |
| INIT-CA-004 | El host inicia y el endpoint de vida responde éxito sin acceder a base, S3 ni fuentes. |
| INIT-CA-005 | No existe referencia de `Sgol.BuildingBlocks` hacia `Sgol.Web`; la prueba falla si se introduce. |
| INIT-CA-006 | No se agregó función, tabla, endpoint de negocio, secreto ni dependencia no justificada. |
| INIT-CA-007 | `git diff -- Fuentes` no muestra cambios y ninguna configuración genera salidas allí. |
| INIT-CA-008 | README explica requisitos, restore, build, test y ejecución local usando sólo datos/configuración no secreta. |

## 7. Verificación mínima

```text
dotnet --version
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release
dotnet format --verify-no-changes
git diff -- Fuentes
```

Si el SDK .NET 10 aprobado no está disponible, Codex debe reportar el bloqueo; no debe cambiar a otra versión ni instalar software sin autorización.

## 8. Momento exacto para abrir Codex

Abrir el primer chat de implementación **sólo después** de que el responsable humano:

1. revise y apruebe los seis entregables F07;
2. incorpore copias de los seis documentos dentro de `Fuentes/`;
3. compruebe por SHA-256 que cada copia de `Fuentes/` es idéntica a su versión final de la raíz;
4. confirme que no existe una observación bloqueante de F07.

Antes de cumplir las cuatro condiciones no debe ejecutarse TECH-INIT-001 ni abrirse F08.

La decisión F07-JP-001 sobre la primera cuenta de Dirección quedó aprobada con los seis entregables F07; no amplía el alcance de TECH-INIT-001.

## 9. Nombre exacto del primer chat

`08 — TECH-INIT-001 — Inicializar la solución mínima verificable`

## 10. Primera instrucción completa

```text
Implementa TECH-INIT-001 — Inicializar la solución mínima verificable de SGOL.

Trabaja únicamente en el alcance de F07_PRIMERA_TAREA_CODEX.md. Lee antes de editar:
- F07_PRIMERA_TAREA_CODEX.md completo;
- F07_ESTRUCTURA_DEL_REPOSITORIO.md, secciones 3 a 8;
- F07_AGENTS_MD_PROPUESTO.md;
- F07_DEFINICIONES_DE_CONTROL.md;
- F06_ARQUITECTURA.md, secciones 4, 5, 8 y 9;
- F06_ESTRATEGIA_DE_PRUEBAS.md, secciones 3 y 15;
- F06_REGISTRO_ADR.md, ADR-001, ADR-002, ADR-012 y ADR-013.

Resultado requerido: crea una solución .NET 10 mínima, reproducible y compilable con el host Sgol.Web, Sgol.BuildingBlocks, pruebas unitarias, pruebas de arquitectura, smoke test, configuración común, lockfile, README y formato inicial de trazabilidad. Adopta en el AGENTS.md raíz las reglas aprobadas de F07_AGENTS_MD_PROPUESTO.md, conservando cualquier instrucción vigente compatible.

No implementes PostgreSQL, EF Core, Identity/MFA, módulos funcionales, endpoints de negocio, Docker, S3, Worker, CI/CD ni ninguna HU-001 a HU-035. No instales otro SDK si .NET 10 no está disponible: reporta el bloqueo. No renombres, muevas, sobrescribas ni elimines archivos dentro de Fuentes/ y no la uses como salida.

Cumple INIT-CA-001 a INIT-CA-008. Antes de editar, inspecciona el repositorio y resume brevemente tu alcance y plan de verificación. Después implementa el cambio mínimo y ejecuta:
- dotnet --version
- dotnet restore --locked-mode
- dotnet build --no-restore --configuration Release
- dotnet test --no-build --configuration Release
- dotnet format --verify-no-changes
- git diff -- Fuentes

Entrega al final: resultado concreto, archivos cambiados, criterios INIT satisfechos, comandos y resultados, trazabilidad creada, riesgos o límites no verificados. No declares la tarea Terminada si falta un gate.
```

## 11. Condición de cierre de la tarea

TECH-INIT-001 queda Terminada sólo con INIT-CA-001 a INIT-CA-008 comprobados, revisión humana y ausencia de cambios en `Fuentes/`. Su cierre habilita TECH-BASE-002; no habilita por sí solo una historia funcional.
