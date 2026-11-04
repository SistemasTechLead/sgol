# Ubicación única de arrastres en Supervisión

Cambio de interfaz en `/validaciones`. `Index.cshtml` renderizaba `_Carryovers` incondicionalmente arriba y otra vez después de `pending` cuando el contador era cero. Se condiciona la primera ubicación a `PendingAuthorizedCarryoverCount > 0`; se conserva la ubicación inferior para cero.

El contador procede de `pending.Total` en `Index.Carryover.cs::LoadCarryoversAsync`, no de la página actual ni del historial. Se conservan permisos, consultas, decisiones y datos. Los cambios previos de capacidad y scripts en esta vista son ajenos a esta corrección y se preservan.

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Cero pendientes, con o sin historial | Un bloque debajo de Pendientes de revisión | Condiciones Razor y regresión estructural local | Aprobado por inspección; navegador no ejecutado |
| Una o varias pendientes autorizadas | Un bloque antes de Pendientes de revisión | Condiciones Razor y regresión estructural local | Aprobado por inspección; navegador no ejecutado |
| Paginación de peticiones | Posición determinada por total autorizado | Inspección de LoadCarryoversAsync | Aprobado por inspección |
| Móvil, escritorio, temas, teclado | Una sola sección y sus controles existentes | Pantalla autenticada | No ejecutado |
| Persistencia y migraciones | Sin cambios | Diff | No aplicable |

Regresión: `node tools/Sgol.MobileChecks/carryover-placement-checks.cjs`, aprobada. `git diff --check`, aprobado. Compilación de Sgol.Web aprobada: cero errores y advertencias. La prueba estructural no acredita renderizado ni flujo HTTP.

Estado: implementado en checkout; despliegue y validación visual pendientes. No se actualiza la bóveda como comportamiento publicado. Al activarlo, reconciliar `docs/design/05-pantallas.md` (posición de arrastres) y la referencia funcional si corresponde. La nota existente ya describe la posición dependiente de pendientes; no requiere notas ni mapas nuevos.
