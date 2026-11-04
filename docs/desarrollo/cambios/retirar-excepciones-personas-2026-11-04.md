# Retiro de excepciones por fecha en Personas

## Alcance y decisión

Cambio mixto autorizado: retirar el apartado de edición de excepciones por fecha y sus funciones. El usuario decidió conservar el efecto de las excepciones existentes e impedir nuevas excepciones. Incidencias gestiona ausencias completas; no sustituye los antiguos horarios excepcionales por tramos.

Se retiraron formulario, consulta por fecha, binding, handlers, DTO y métodos de consulta/preview/guardado de `PersonalScheduleService`. Los POST a handlers retirados responden 404 después de autorización y antiforgery. El horario semanal conserva revisión, fingerprint, permisos e idempotencia.

Persisten `PersonalScheduleException`, su esquema y sus lectores en `TemporalEligibilityContext.LoadAsync`/`Day`, planificación, capacidad, correcciones y `TaskBonusService`. No hay migración ni eliminación de registros. Las pruebas que necesitaban excepciones ahora siembran registros históricos mediante `LegacyScheduleExceptionFixture`, fuera del producto.

## Validación

| Escenario / precondición | Acción y resultado esperado | Entorno / prueba | Estado |
|---|---|---|---|
| Persona existente | Editar muestra horario semanal sin formulario de excepciones | Razor real, `Sgol.UserProfileChecks` | Aprobado |
| URL antigua con fecha | GET no restaura el editor | HTTP aislado | Aprobado |
| POST antiguo con CSRF válido | Preview/guardar devuelve 404; conserva todos los campos históricos | HTTP y comparación JSON en base aislada | Aprobado |
| Horario semanal | Revisar, guardar, repetir; una versión, sesiones conservadas | Formularios y PostgreSQL aislado | Aprobado |
| Permisos / formulario parcial | Administración rechazada; día omitido rechazado | HTTP aislado | Aprobado |
| Excepción histórica | Sigue sustituyendo horario semanal | Reglas y escenario 10 de integración | Aprobado |
| Presentación | Sin controles retirados; horario y foco accesibles | Chromium, 320/768/1440 px, claro/oscuro; Razor real con sesión de fixture | Aprobado, 6 combinaciones |
| Reglas de horarios | Mantener disponibilidad, cobertura y límites | `--schedule-rules-only` | Aprobado, 60 comprobaciones |
| Suite amplia de planificación | Recorrido completo | `--schedule-integration-only` | Fallido tras 31 comprobaciones: esperaba `SCHEDULE_NOT_PRESENT_AT_ACTIVATION` en recepción tardía y no ocurrió. No acredita los pasos posteriores; no se modificó esa regla para hacer pasar el test |
| Compilación | Web y herramientas compilan | Debug, .NET | Aprobado, sin errores ni advertencias |
| Historial de bono | Conservar lectores y tablas | Inspección de `TaskBonusService` y ausencia de escrituras/migración | Inspeccionado; no se recalcularon bonos compartidos |
| Cuenta real compartida | Editar con sesión real | Host compartido | No ejecutado; navegador utiliza sesión sintética aislada |

Evidencias privadas: `.local/remove-person-exceptions/`. El primer intento de la herramienta de perfiles se detuvo por directorio de trabajo incorrecto con `dotnet run`; se ejecutó la DLL desde la raíz con una base nueva y pasó. Las bases usan prefijos `sgol_profile_checks_*` y `sgol_task_checks_*`; no se usó la simulación para regresiones.

## Publicación y documentación

Publicación: `.local/runtime/people-without-exceptions-20261104/Sgol.Web.dll`, mismo SHA-256 que la DLL Debug probada. Se sustituyó únicamente Web, conservando proxy, dependencias y el artefacto anterior `.local/runtime/incidencias-integration-20261104/` para recuperación. Sin migraciones ni escrituras operativas manuales. Readiness interno y acceso local/LAN respondieron 200 tras el arranque. Proceso Web 15708, lanzador 40280; proxy 20652 conservado. La herramienta de perfiles terminó con 79 comprobaciones aprobadas, incluidas las seis combinaciones de navegador.

Referencias reconciliadas con el despliegue: bóveda `docs/design/05-pantallas.md`, `docs/funcional/horarios-cobertura.md`, `docs/instalacion-local.md` y `organizacion/base-documental.md`. No se crean notas de referencia ni se modifican mapas/inventario porque las notas existentes mantienen su propósito.
