# F07 Adenda 07 — Contrato de carga activa para HU-004

## 1. Estado y alcance

Decisión aprobada por el responsable el 2026-09-04 para desbloquear exclusivamente `HU-004 — Superior consulta carga activa correcta`.

Esta adenda no autoriza elegibilidad, actualización de snapshots, ranking, desempates, elección de ganador, creación o corrección de asignaciones, última asignación automática ni planificación. Tampoco autoriza commit, publicación, pull request o merge.

## 2. Propiedad e infraestructura mínima de `assignment_version`

`Assignment` es propietario de `assignment_version`. `HU-004` puede introducir únicamente la entidad, configuración EF, tabla, relaciones, restricciones e índices necesarios para consultar asignaciones. No incorpora comandos, endpoints ni servicios que creen, corrijan o sustituyan asignaciones.

La tabla usa el modelo siguiente:

| Campo | Tipo y nulabilidad | Regla |
|---|---|---|
| `id` | `uuid`, no nulo | Clave primaria. |
| `obligation_id` | `uuid`, no nulo | FK a `work_obligation(id)` con borrado `RESTRICT`. |
| `person_id` | `uuid`, no nulo | FK a `person(id)` con borrado `RESTRICT`. |
| `status` | texto, no nulo | Sólo `VIGENTE` o `SUSTITUIDA`. |
| `assignment_type` | texto, no nulo | Sólo `AUTOMATICA` o `CORRECCION`. |
| `explanation` | `jsonb`, no nulo | Explicación estructurada; objeto JSON. HU-004 no la produce ni modifica. |
| `reason` | texto, anulable | Nulo para `AUTOMATICA`; obligatorio y no vacío para `CORRECCION`. |
| `assigned_by` | `uuid`, anulable | FK a `app_user(id)` con borrado `RESTRICT`; nulo para `AUTOMATICA` y obligatorio para `CORRECCION`. |
| `assigned_at` | `timestamptz`, no nulo | Instante UTC de creación de la versión. |
| `supersedes_id` | `uuid`, anulable | FK autorreferente con borrado `RESTRICT`; nulo para la primera versión y obligatorio para `CORRECCION`. |

PostgreSQL es la autoridad final para estas restricciones:

- índice único parcial que permite como máximo una fila `VIGENTE` por `obligation_id`;
- `supersedes_id` único cuando no es nulo, distinto de `id`;
- coherencia entre `assignment_type`, `reason`, `assigned_by` y `supersedes_id` conforme a la tabla anterior;
- `explanation` debe ser un objeto JSON;
- índices `assignment_version(person_id, status, assigned_at)` y `assignment_version(obligation_id, status)`;
- ninguna FK usa borrado en cascada.

La integridad que exige comprobar elegibilidad, misma obligación y sucesión cronológica pertenece a los flujos de `HU-018` y `HU-019`; HU-004 no crea un flujo alterno para satisfacerla.

## 3. Definición y universo visible

La carga activa de una persona es el número de obligaciones con `execution_status = PENDIENTE` cuya fila de `assignment_version` visible en el snapshot tiene `status = VIGENTE` y referencia a esa persona. Cada obligación cuenta como máximo una vez. Una obligación `CONCLUIDA` y toda fila `SUSTITUIDA` cuentan cero.

El universo del resultado contiene personas que, en el instante de corte:

1. tienen empleo `ACTIVO` vigente en `LOR-001`;
2. tienen una cuenta `ACTIVA` individual vinculada;
3. tienen exactamente un rol canónico `ACTIVO` vigente en `LOR-001`; y
4. quedan dentro del alcance jerárquico del actor.

Una persona del universo se devuelve con carga cero aunque no tenga asignaciones. Una persona inactiva, sin empleo vigente, sin cuenta activa o sin rol canónico vigente queda fuera del resultado, aunque conserve por inconsistencia una asignación `VIGENTE`. HU-004 no corrige esa inconsistencia.

## 4. Instante de corte y concurrencia

La consulta abre una transacción PostgreSQL de sólo lectura con aislamiento `REPEATABLE READ`, fija su snapshot y captura `IClock.UtcNow` exactamente una vez al comenzar el cálculo. Ese valor es `calculatedAt`, se expresa en UTC y es idéntico en todos los elementos de la respuesta.

Empleo y rol son vigentes cuando `valid_from <= calculatedAt` y `valid_to` es nulo o `calculatedAt < valid_to`, además de conservar su estado vigente. La cuenta debe estar `ACTIVA`. Una asignación debe tener `assigned_at <= calculatedAt` y estado `VIGENTE` dentro del mismo snapshot.

Todos los datos de una respuesta se leen en el mismo snapshot. Una sustitución confirmada después de fijarlo no afecta esa respuesta; aparece en una consulta posterior. Cada página solicitada es un cálculo nuevo y puede tener un `calculatedAt` distinto.

## 5. Contrato de `GET /api/v1/loads`

La ruta pública única de esta historia es `GET /api/v1/loads`. Es una colección JSON `camelCase`:

```json
{
  "data": [
    {
      "person": {
        "id": "019...",
        "stableCode": "EMP-001",
        "displayName": "Persona sintética"
      },
      "activeLoad": 0,
      "calculatedAt": "2026-09-04T18:00:00Z"
    }
  ],
  "meta": {
    "nextCursor": null,
    "count": 1,
    "correlationId": "019..."
  }
}
```

`person.id` es UUID; `stableCode` y `displayName` son texto; `activeLoad` es entero no negativo; `calculatedAt` es un instante UTC ISO 8601. El orden estable es `stableCode` ordinal ascendente y después `person.id` ascendente.

Se admiten exclusivamente:

- `cursor`: cursor opaco de paginación por `stableCode` y `person.id`;
- `limit`: entero entre 1 y 100; valor predeterminado 25;
- `personId`: UUID opcional que restringe el resultado a una persona ya visible.

Un parámetro desconocido, UUID inválido, cursor inválido o límite fuera de rango devuelve `400 application/problem+json`. `personId` inexistente o fuera del alcance devuelve `200` con `data` vacío, `count = 0` y `nextCursor = null`; no revela existencia. Sin personas visibles se devuelve el mismo contrato vacío. Los filtros se aplican después del alcance obligatorio del servidor y nunca pueden ampliarlo.

`meta.count` es la cantidad de elementos de la página actual. El cursor es Base64URL opaco y versionado; contiene `stableCode` y `personId`, sin conceder autoridad. Parámetros duplicados también son inválidos.

Los códigos de Problem Details son:

- `400 FILTRO_CARGA_INVALIDO`, título `Los parámetros de consulta no son válidos`;
- `401 AUTENTICACION_REQUERIDA`;
- `403 ACCESO_DENEGADO`.

## 6. Permiso y alcance jerárquico

El endpoint exige sesión válida y `PER-CARGA-VER`. La autoridad se deriva exclusivamente de la cuenta activa y de su único rol canónico `ACTIVO` vigente en `LOR-001`; puesto, turno, nombre laboral y texto parecido no conceden permiso ni alcance.

- `DIRECCION` consulta todas las personas del universo.
- `ADMINISTRACION` consulta a la persona propia y personas con rol `SUBCOORDINACION` o `PISO_VENTAS`.
- `SUBCOORDINACION` consulta a la persona propia y personas con rol `PISO_VENTAS`.
- `PISO_VENTAS` consulta únicamente a la persona propia.
- Ningún rol distinto de Dirección consulta pares; ningún actor consulta roles superiores.

Una petición no autenticada devuelve `401`. Un actor sin `PER-CARGA-VER`, sin cuenta activa, sin empleo vigente en `LOR-001` o sin rol canónico vigente devuelve `403` sin datos.

## 7. Lectura sin efectos y límites

`GET /api/v1/loads` no inserta `audit_event`, snapshot, contador ni otro registro. Devuelve `correlationId` y puede producir logs técnicos minimizados, pero no auditoría funcional. La atomicidad de auditoría aplica a escrituras y cambios funcionales; esta lectura no es uno de ellos.

La consulta no modifica obligaciones, asignaciones, evaluaciones, candidatos, personas, empleos, cuentas, roles ni disponibilidad. No actualiza `eligibility_candidate.active_load`, `last_auto_assignment_at`, `rank` o `winner_person_id`; no crea ranking, ganador, plan o versión de plan.

## 8. Eficacia y trazabilidad

Esta adenda es la fuente contractual específica de HU-004 junto con F05 y F06. La implementación y la actualización de `docs/traceability/README.md` y `docs/traceability/IMPLEMENTATION_STATUS.md` viajarán en el mismo cambio de HU-004.

HU-004 permanecerá como propuesta en rama hasta cumplir pipeline requerido sobre el commit exacto, aprobación humana, merge en `master` y ascendencia verificada en `origin/master`.
