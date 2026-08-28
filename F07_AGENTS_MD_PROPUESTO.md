# F07 — `AGENTS.md` propuesto para la implementación de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes`; su adopción queda asignada a TECH-INIT-001 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aplicación | Adoptar al iniciar la implementación, después de aprobar e incorporar F07 |
| Propósito | Dar a Codex límites persistentes, verificaciones y fuentes de verdad |

## 2. Contenido propuesto

```markdown
# Instrucciones de implementación de SGOL

## Fuente de verdad y alcance

Trabaja sólo en la tarea o historia indicada. Antes de editar, lee su fila en `F07_BACKLOG_DE_IMPLEMENTACION.md`, los criterios y pruebas relacionados de F05 y las decisiones técnicas de F06. Durante el desarrollo, el repositorio y los documentos aprobados son la fuente oficial.

No implementes funciones posteriores, opcionales o fuera de alcance. El MVP se limita a 35 capacidades y a TAR-0005, TAR-0007, TAR-0008, TAR-0011, TAR-0018, TAR-0026, TAR-0092 y TAR-0093 para `LOR-001`.

Si una instrucción contradice un entregable aprobado, no elijas silenciosamente: detén esa parte, cita la contradicción y solicita decisión.

## Protección de `Fuentes`

`Fuentes/` es de solo lectura lógica. No renombres, muevas, sobrescribas, elimines ni generes archivos dentro de ella. No uses `Fuentes/` como salida de compilación, prueba, migración o ejecución. No abras ni ejecutes macros. La incorporación documental posterior a una aprobación no forma parte de una tarea de código ordinaria.

## Arquitectura obligatoria

- .NET 10 LTS, ASP.NET Core, EF Core, Razor Pages/MVC y API REST `/api/v1`.
- Monolito modular: un despliegue, una base PostgreSQL y contratos internos explícitos.
- PostgreSQL real para integración; no uses SQLite para validar comportamiento.
- Binarios en S3-compatible privado; cuarentena, verificación de tipo real, SHA-256 y antimalware.
- Sin Redis, broker, microservicios, Kubernetes, SPA separada, integración externa ni motor general de reglas en el MVP.
- UTC para instantes; `America/Mexico_City` y semana ISO para operación.

Un módulo posee sus tablas y reglas. Otro módulo no escribe directamente sus tablas. Dominio no depende de ASP.NET Core, EF Core, S3 ni proveedor de despliegue. Web y Worker componen; no contienen reglas de negocio.

## Integridad, seguridad e historia

Aplica denegación por defecto y autorización en servidor por permiso, rol vigente, recurso, jerarquía y estado. Ocultar controles en UI no autoriza una operación.

Toda escritura crítica conserva historia e inserta auditoría en la misma transacción. No añadas borrado funcional de auditoría, evidencias, versiones, asignaciones o validaciones. Usa restricción única y transacción para idempotencia; una comprobación previa en memoria no basta.

No registres secretos, contraseñas, TOTP, códigos de recuperación, cookies, URLs firmadas, cadenas de conexión ni contenido de evidencia. No confirmes una carga hasta que el archivo esté `LIMPIO`.

## Forma de trabajo

1. Inspecciona el estado del repositorio y no reviertas cambios ajenos.
2. Resume alcance, fuentes autorizadas, dependencias y criterios antes de editar.
3. Implementa el cambio mínimo que produzca el resultado pedido.
4. Añade o actualiza pruebas positivas, negativas, de autorización, auditoría y no-efecto que correspondan.
5. Actualiza trazabilidad y documentación afectadas en el mismo cambio.
6. Ejecuta los gates aplicables y reporta comandos, resultados y límites no comprobados.

No inventes campos, estados, permisos, endpoints o reglas. Una ausencia documental no autoriza una decisión. Las correcciones de defectos no amplían alcance.

## Convenciones

- IDs de backlog, HU, CAP, CA, CP, CAT, NFR, ADR y RN se conservan literalmente.
- Código y namespaces en inglés; nombres funcionales estables permanecen en contratos y trazabilidad.
- Dependencias NuGet centralizadas y fijadas. No agregues una dependencia sin justificar su necesidad y revisar licencia/vulnerabilidades.
- Migraciones compatibles hacia adelante; ninguna migración destructiva en MVP.
- API: JSON `camelCase`, `application/problem+json`, `correlationId`, paginación por cursor, `Idempotency-Key` e `If-Match` donde F06 lo exige.
- Datos y archivos de prueba son sintéticos; nunca copies evidencia real a Git o CI.

## Gates mínimos antes de declarar terminado

Ejecuta, según exista en el corte actual:

```text
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release
dotnet format --verify-no-changes
```

Además ejecuta las pruebas específicas de arquitectura, PostgreSQL, API/contrato, seguridad o navegador exigidas por la historia. Un gate omitido debe quedar marcado como no verificado y con causa; no lo presentes como aprobado.

## Entrega

La respuesta final debe indicar: resultado concreto, archivos cambiados, criterios satisfechos, pruebas ejecutadas con resultado, trazabilidad actualizada, riesgos o pendientes y cualquier decisión humana necesaria. No declares la historia terminada si la Definición de Terminado de F07 no se cumple.
```

## 3. Condición para adoptarlo

No se debe sobrescribir el `AGENTS.md` actual durante F07. Tras aprobar e incorporar los seis entregables F07, la primera tarea de Codex debe fusionar estas reglas con cualquier instrucción vigente que siga siendo necesaria, sin debilitar la protección de fuentes ni el protocolo por fases.
