# F07 Adenda 31 — Corrección de unicidad de clave de generación para HU-034

## 1. Control de la propuesta

| Campo | Valor |
|---|---|
| Identificador | `F07_ADENDA_31` |
| Historia | `HU-034` — SGOL recupera mutaciones repetidas y registra conflictos |
| Estado | Propuesta correctiva; requiere aprobación íntegra antes de modificar índices |
| Fecha | 2026-09-12 |
| Contrato base | `F07_ADENDA_30_CONTRATO_DE_IDEMPOTENCIA_INTEGRAL_Y_CONFLICTOS_HU_034.md`, aprobada íntegramente el 2026-09-12 |
| Alcance | Resolver exclusivamente la incompatibilidad entre el scope por actor aprobado y el índice global de `generation_request.idempotency_key` |
| Exclusiones | Sin cambios a DTO, endpoint, permiso, hash funcional, obligación, recurrencia, auditoría, UI o `HU-035` |

Esta adenda complementa únicamente la decisión de persistencia de `F07_ADENDA_30`. Todas sus demás decisiones permanecen vigentes sin reinterpretación.

## 2. Hechos comprobados

1. `F07_ADENDA_30` aprobó que actor, operación y recurso formen parte del scope y que actores distintos puedan usar el mismo UUID sin colisión ni descubrimiento cruzado.
2. `idempotency_record` cumple esa separación mediante su PK `(scope,key)`.
3. `generation_request` conserva además `idempotency_key` y el índice único global `UX_generation_request_idempotency_key` sobre esa única columna.
4. La generación manual persiste `requested_by` con el actor humano. La generación recurrente usa `requested_by = NULL` y una clave UUID v8 determinista.
5. `generation_request` conserva por separado `UX_generation_request_functional_key` sobre regla, sucursal, período, tipo y referencia de origen. Ésta es la protección permanente de `RN-010`.
6. El índice global actual es más restrictivo que el scope aprobado: dos actores con el mismo UUID pero intenciones funcionales diferentes colisionan antes de que `(scope,key)` pueda decidir correctamente.
7. El índice global vigente garantiza que hoy no existen duplicados de ninguna pareja actor/clave, por lo que la transición propuesta no requiere backfill ni resolución de datos.

## 3. Contradicción

La sección 8 de `F07_ADENDA_30` exige que la misma clave pueda usarse por actores distintos. Su sección 13, sin embargo, autorizó sólo cuatro columnas nuevas y ningún cambio de índice.

Mantener `UX_generation_request_idempotency_key` global incumple la primera decisión. Sustituirlo sin una adenda incumple la segunda. No existe una implementación que satisfaga simultáneamente ambas reglas.

## 4. Decisión propuesta

Se autoriza sustituir, dentro de la misma migración aditiva de `HU-034`, el índice:

```text
UX_generation_request_idempotency_key (idempotency_key) UNIQUE
```

por:

```text
UX_generation_request_idempotency_key (requested_by, idempotency_key) UNIQUE NULLS NOT DISTINCT
```

El nombre estable se conserva. La migración elimina el índice anterior y crea el índice compuesto. `NULLS NOT DISTINCT` garantiza que el productor sistema sólo pueda tener una fila para una clave determinista aunque `requested_by` sea `NULL`.

Consecuencias exactas:

- dos actores humanos distintos pueden usar el mismo UUID porque forman parejas distintas;
- el mismo actor humano no puede confirmar dos solicitudes con la misma clave;
- el actor sistema no puede confirmar dos solicitudes recurrentes con la misma clave;
- una clave humana no colisiona con una clave del actor sistema;
- `idempotency_record(scope,key)` sigue decidiendo replay o `IDEMPOTENCY_CONFLICT`;
- `UX_generation_request_functional_key` permanece sin cambios y sigue siendo la autoridad permanente para una sola obligación por identidad de `RN-010`;
- una carrera por claves diferentes contra la misma identidad funcional sigue recuperando o rechazando conforme al contrato de generación, nunca crea dos obligaciones.

## 5. Persistencia y compatibilidad

No se agrega columna, tabla, vista, trigger ni segundo índice. Sólo se sustituye la definición del índice existente y se actualizan la configuración EF y el model snapshot.

No existe backfill. Las filas actuales conservan IDs, claves, hashes, actor, resultado y vínculos. El índice anterior, al ser más fuerte, garantiza que todas las filas existentes satisfacen el nuevo índice compuesto.

La migración sigue siendo forward-only y su `Down()` permanece bloqueado. No se autoriza ejecutar SQL de reparación, eliminar filas ni cambiar `requested_by`.

## 6. Concurrencia y seguridad

La autorización vigente se evalúa antes de consultar idempotencia. La separación por actor no permite consultar la fila de otro actor ni convertir una clave compartida en señal de existencia.

En concurrencia:

1. misma pareja actor/clave y mismo hash converge por `idempotency_record` y el índice compuesto;
2. misma pareja actor/clave y hash distinto produce un ganador y conflictos auditados;
3. actores distintos con la misma clave y distintas identidades funcionales pueden confirmar independientemente;
4. actores distintos con claves iguales contra la misma identidad funcional compiten por `UX_generation_request_functional_key`, no por idempotencia, y conservan una sola solicitud/obligación funcional;
5. actor sistema y clave determinista repetida convergen como hasta ahora.

## 7. Pruebas adicionales obligatorias

La implementación deberá demostrar en PostgreSQL real:

1. dos actores autorizados usan el mismo UUID con identidades funcionales distintas y cada uno confirma su propia solicitud;
2. cada actor recupera sólo su propio resultado y no descubre el del otro;
3. el mismo actor y clave con hash distinto obtiene `409 IDEMPOTENCY_CONFLICT` auditado;
4. dos actores con la misma clave contra la misma identidad funcional producen una sola solicitud/obligación por `RN-010`;
5. dos ejecuciones sistema con la misma clave UUID v8 no duplican `generation_request`;
6. la migración preserva todas las filas existentes y crea exactamente un índice `UX_generation_request_idempotency_key` compuesto con `NULLS NOT DISTINCT`;
7. `UX_generation_request_functional_key` permanece idéntico;
8. no cambian endpoint, DTO, status, permiso, jerarquía, hash funcional, auditoría de éxito ni recurrencia.

## 8. Eficacia y autorizaciones

Esta propuesta no modifica por sí sola el índice y no autoriza commit, push, pull request o merge. La implementación local de la sustitución queda autorizada sólo después de aprobar íntegramente esta adenda.

Las cuatro columnas y todas las demás decisiones de `F07_ADENDA_30` permanecen aprobadas. El código local ya iniciado bajo esa aprobación puede conservarse, pero no se continuará con la adaptación de productores ni con la migración del índice hasta resolver esta contradicción.

## 9. Decisión solicitada

Se solicita aprobar o rechazar `F07_ADENDA_31` como una unidad.

La pregunta de aprobación es: **¿se aprueba íntegramente `F07_ADENDA_31_CORRECCION_DE_UNICIDAD_DE_CLAVE_DE_GENERACION_HU_034.md`, sin cambios, para sustituir el índice global de clave de generación por el índice compuesto `(requested_by,idempotency_key) UNIQUE NULLS NOT DISTINCT` durante la implementación local de `HU-034`?**
