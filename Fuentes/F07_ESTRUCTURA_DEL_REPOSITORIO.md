# F07 — Estructura propuesta del repositorio de SGOL

## 1. Control del entregable

| Campo | Valor |
|---|---|
| Fase | 07 — Preparación del repositorio y backlog para Codex |
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Alcance | Estructura y reglas de dependencia; no crea proyectos ni implementa funciones |
| Entradas aprobadas | Entregables F00 a F06 y plan maestro |

## 2. Decisiones que condicionan la estructura

La estructura aplica, sin reabrirlas, las decisiones aprobadas de F06: monolito modular, .NET 10 LTS, ASP.NET Core, EF Core, Razor Pages/MVC, API REST `/api/v1`, PostgreSQL como autoridad transaccional, almacenamiento S3-compatible privado y una imagen OCI que puede ejecutar la aplicación web o trabajos programados. No se incorporan Redis, broker, microservicios, SPA separada ni motor general de reglas.

La implementación se limita al MVP aprobado: una sucursal `LOR-001`, ocho definiciones TAR y 35 capacidades. Las funciones posteriores, opcionales y fuera de alcance de F04 no forman parte de la estructura funcional inicial.

## 3. Regla de protección de las fuentes

`Fuentes/` es un repositorio documental de solo lectura lógica.

- El código, pruebas, herramientas y automatizaciones no pueden renombrar, mover, sobrescribir ni eliminar archivos dentro de `Fuentes/`.
- Ninguna compilación, prueba, migración, semilla o ejecución local puede usar `Fuentes/` como directorio de salida.
- Los tests no dependen de modificar una copia ubicada dentro de `Fuentes/`.
- Sólo el protocolo documental del plan permite incorporar entregables aprobados, mediante copia deliberada y verificación SHA-256; esa operación queda fuera de las tareas ordinarias de desarrollo.
- Los datos semilla ejecutables se derivan de decisiones aprobadas y se guardan en `src/` o `tests/`; no se leen hojas de cálculo ni se ejecutan macros en tiempo de ejecución.

## 4. Árbol propuesto

```text
SGOL/
├─ AGENTS.md
├─ SGOL.slnx
├─ Directory.Build.props
├─ Directory.Packages.props
├─ global.json
├─ .editorconfig
├─ .gitignore
├─ README.md
├─ LICENSE                         # sólo si el responsable define una licencia
├─ Fuentes/                        # solo lectura lógica; documentación aprobada
├─ docs/
│  ├─ architecture/               # vistas vigentes y ADR nuevos posteriores a F06
│  ├─ api/                        # snapshot OpenAPI aprobado
│  ├─ operations/                 # runbooks, respaldo, restauración e incidentes
│  └─ traceability/               # mapa historia-criterio-prueba por incremento
├─ src/
│  ├─ Sgol.Web/                   # host ASP.NET Core, Razor/MVC, API, composición
│  ├─ Sgol.Worker/                # comando/host para recurrencias y outbox
│  ├─ Sgol.BuildingBlocks/        # primitivas técnicas mínimas compartidas
│  └─ Modules/
│     ├─ Identity/
│     ├─ Organization/
│     ├─ Configuration/
│     ├─ Generation/
│     ├─ Assignment/
│     ├─ Planning/
│     ├─ Execution/
│     ├─ Evidence/
│     ├─ Validation/
│     ├─ Reporting/
│     ├─ Audit/
│     └─ Continuity/
├─ tests/
│  ├─ Sgol.ArchitectureTests/
│  ├─ Sgol.UnitTests/
│  ├─ Sgol.IntegrationTests/
│  ├─ Sgol.ApiTests/
│  ├─ Sgol.EndToEndTests/
│  ├─ Sgol.SecurityTests/
│  └─ TestAssets/                 # datos sintéticos y archivos hostiles controlados
├─ deploy/
│  ├─ containers/
│  ├─ manifests/
│  └─ scripts/                    # scripts no destructivos y repetibles
├─ tools/                          # validadores y utilidades de desarrollo
└─ .github/
   ├─ workflows/
   └─ CODEOWNERS                  # cuando existan responsables definidos
```

La primera tarea crea sólo el subconjunto señalado en `F07_PRIMERA_TAREA_CODEX.md`; el árbol completo se materializa conforme cada corte lo necesita.

## 5. Estructura interna de un módulo

Cada módulo usa cortes verticales dentro de un límite explícito, sin exigir un proyecto por capa:

```text
Modules/<Module>/
├─ <Module>.csproj
├─ Domain/                         # invariantes, estados y tipos propios
├─ Features/
│  └─ <Resultado>/
│     ├─ CommandOrQuery.cs
│     ├─ Handler.cs
│     ├─ Validation.cs
│     └─ Contract.cs
├─ Infrastructure/                 # EF Core y adaptadores propiedad del módulo
├─ Endpoints/                      # registro HTTP del módulo
└─ ModuleRegistration.cs
```

Se puede omitir una carpeta vacía. No se crean abstracciones sin un consumidor real ni capas de transferencia que sólo dupliquen datos.

## 6. Límites y dependencias

| Regla | Control esperado |
|---|---|
| Un módulo no escribe tablas de otro módulo | API/contrato interno o servicio de aplicación del propietario |
| `Domain` no depende de ASP.NET Core, EF Core, S3 ni proveedor PaaS | Pruebas de arquitectura |
| `Sgol.Web` y `Sgol.Worker` componen módulos; no contienen reglas de negocio | Revisión y pruebas de arquitectura |
| `Sgol.BuildingBlocks` contiene sólo primitivas estables ya usadas | Prohibido convertirlo en cajón de utilidades |
| Las escrituras críticas y `audit_event` comparten transacción PostgreSQL | Pruebas de integración con PostgreSQL real |
| S3, escáner, reloj, UUID y telemetría se acceden mediante puertos acotados | Adaptadores reemplazables y reloj/UUID inyectables |
| Toda mutación HTTP respeta CSRF, autorización, idempotencia o ETag según contrato | Pruebas API positivas y negativas |

## 7. Convenciones de repositorio

- Ensamblados y namespaces: `Sgol.<Área>`; código C# en inglés y términos funcionales estables conservados en documentación y contratos.
- Ramas de trabajo: `codex/<id>-<resultado-breve>`; una historia o incremento por rama y chat de F08.
- Commits pequeños con referencia al ID de backlog; no mezclar refactorizaciones ajenas.
- Dependencias NuGet centralizadas y versiones fijadas; `global.json` fija el SDK .NET 10 aprobado.
- Configuración no secreta versionada; secretos, TOTP, cookies, conexiones y claves nunca se almacenan en Git.
- Migraciones EF Core avanzan de forma compatible. No se admiten migraciones destructivas en el MVP.
- No existe `DELETE` funcional para auditoría, evidencias, versiones, asignaciones o validaciones históricas.

## 8. Proyectos de prueba y responsabilidad

| Proyecto | Responsabilidad |
|---|---|
| `Sgol.ArchitectureTests` | Límites modulares, dirección de dependencias y ausencia de acceso prohibido |
| `Sgol.UnitTests` | Reglas puras, estados, ranking, zona, esquemas TAR y permisos puros |
| `Sgol.IntegrationTests` | EF Core, PostgreSQL real, restricciones, transacciones, S3 y escáner adaptado |
| `Sgol.ApiTests` | HTTP, contratos, cookies, CSRF, autorización, ETag, errores e idempotencia |
| `Sgol.EndToEndTests` | Flujos críticos Razor/MVC, accesibilidad y sesión con Playwright |
| `Sgol.SecurityTests` | Matriz de permisos, IDOR, archivos hostiles y controles de sesión |

SQLite y mocks de base no sustituyen las pruebas de integración aprobadas.

## 9. Entornos y artefactos

Local y CI usan datos sintéticos y recursos aislados. Staging y producción piloto mantienen base, bucket, secretos y dominio separados. La salida liberable es una imagen OCI inmutable por digest, acompañada de migraciones, snapshot OpenAPI, SBOM, manifiesto declarativo y evidencia de pruebas.

Los scripts operativos deben ser repetibles, fallar de forma segura, identificar el entorno y no alterar `Fuentes/`.

## 10. Criterio de aceptación de esta propuesta

La estructura queda aceptable cuando:

1. representa los doce componentes aprobados de F06 sin convertirlos en servicios desplegables independientes;
2. permite implementar `CV-01` a `CV-05` por incrementos verticales;
3. separa código, pruebas, despliegue y fuentes protegidas;
4. hace verificables los límites modulares, la trazabilidad y los gates de F06;
5. no materializa funciones posteriores ni infraestructura no aprobada.
