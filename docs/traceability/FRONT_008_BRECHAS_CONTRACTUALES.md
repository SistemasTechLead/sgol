# FRONT-008 — comprobación de contratos y decisión aplicada

Estado: **extensión mínima aprobada por el responsable en este chat** para el contrato consumidor UI-C01/C02/C03, lectura de borradores e idempotencia de PUT. Esta nota no modifica F05, F06 ni las adendas congeladas. La edición de sucursal sigue pendiente de BR-API01 y la creación de releases conserva su asignación a FRONT-009.

## Brechas comprobadas antes de editar

- La fila 76 de la Adenda 45 asigna UI-C01/C02/C03 a FRONT-008. La navegación aprobada sitúa UI-C01 en `/configuracion` y UI-C02/C03 en `/planificacion`.
- `GET /api/v1/branches/LOR-001` existe y rechaza otro código. F06 enumera `PATCH /branches/LOR-001`, pero `BranchApiEndpoints` sólo mapea GET y el contrato de sucursal expone lectura. BR-API01 no autoriza todavía un formulario de edición ni una mutación nueva.
- `GET /api/v1/weeks/{isoYear}/{isoWeek}` devuelve el período ISO único y su estado derivado `VIGENTE`/`TRANSCURRIDA`. Su autorización de servidor exige `PER-PLAN-VER` vigente en `LOR-001`.
- `GET /api/v1/calendar?from=&to=` devuelve sólo días publicados vigentes del rango, en `America/Mexico_City`; la ausencia de filas no equivale a día laborable. `PUT /api/v1/calendar/{date}` sólo acepta una `releaseId` existente de `LOR-001` en `BORRADOR`, con tipo `LABORABLE`, `FESTIVO` o `CIERRE_EXTRAORDINARIO`, valor laborable coherente y motivo. Para corregir un borrador existente exige `If-Match` y rechaza una versión obsoleta con 412.
- `GET /api/v1/configuration/releases` listaba releases y permitía a Dirección identificar un borrador, pero no devolvía los días borrador ni sus versiones. `POST /api/v1/configuration/releases` crea la release, pero su interfaz corresponde a FRONT-009. La lectura de días se resolvió en este cambio; la creación desde interfaz sigue pendiente.
- `CalendarApiEndpoints.HandlePutAsync` y `EfCalendarService.PutAsync` no recibían ni persistían `Idempotency-Key`. Las reglas comunes de la Adenda 45 la exigen por intención. La persistencia y replay se añadieron en este cambio.
- `docs/design` aprobaba la primitiva de fecha/rango BR-D05 y la navegación, pero no especificaba el contrato consumidor UI-C01/C02/C03: presentación de sucursal, período y estado derivado, días de calendario, vacío, mensajes de validación, error de autorización, confirmación de impacto, conflicto 412 ni retorno de foco. La extensión UI-I06/I07 de FRONT-007 no cubría estas unidades. La extensión consumidora se aprobó y añadió en este cambio.

## Alcance de la decisión y ejecución

1. **UI-C01 de sólo lectura.** Se aprobó y se añadió a `docs/design` la ficha de `LOR-001` en `/configuracion`, sin control de edición. BR-API01 aún requiere una decisión separada para PATCH.
2. **UI-C02/C03 de consulta.** Se aprobó y se añadió a `docs/design` el consumidor de semana ISO, rango local y calendario vigente/vacío en `/planificacion`.
3. **Dependencia de edición.** Se aprobó la lectura autorizada de días de una release `BORRADOR` ya existente, por `releaseId` y rango, con versión de cada día. La interfaz lista borradores reales y permite continuar una edición después de recargar. La creación de la primera release desde la interfaz sigue asignada a FRONT-009; por ello FRONT-008 no puede declararse completa como recorrido autónomo de producto.
4. **PUT idempotente.** Se aprobó exigir `Idempotency-Key` UUID canónica, vincularla al actor y a la intención completa, y persistir replay en la misma transacción que versión y auditoría. Las pruebas PostgreSQL enfocadas comprueban replay, concurrencia y no efecto.
5. **Diseño de edición.** Se aprobó la selección de borrador y día, motivo obligatorio, confirmación de impacto, estados de envío, conflicto 412 con recarga explícita, errores asociados, foco/teclado y reflow móvil. El servidor conserva la autorización independiente.

El código y las comprobaciones ejecutadas se registran en `FRONT_008_CONFIGURACION_Y_PLANIFICACION.md`. Las dos decisiones pendientes son BR-API01 para mutar sucursal y la dependencia funcional de FRONT-009 para obtener la primera release borrador desde la interfaz. Ninguna se resuelve con un ID sintético de prueba.
