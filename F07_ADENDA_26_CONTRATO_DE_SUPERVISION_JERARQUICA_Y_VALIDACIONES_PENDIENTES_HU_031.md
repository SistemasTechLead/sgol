# SGOL — Adenda 26 a F07: contrato de supervisión jerárquica y validaciones pendientes de HU-031

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Propuesta de adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE; IMPLEMENTACIÓN EN CURSO` |
| Fecha | 2026-09-10 |
| Historia | `HU-031 — Superior consulta y supervisa sólo inferiores` |
| Corte y épica | `CV-04` / `EP-09` |
| Criterios | `CA-031`, `CP-031-P`, `CP-031-N`, `RN-020`, `RN-025`, `RN-026` |
| Base aceptada | `HU-023`, `HU-027` y `HU-028` terminadas efectivamente conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md` |
| Efecto propuesto | Precisar exclusivamente las dos lecturas de `HU-031`, sin alterar contratos de escritura ni adelantar historias posteriores |
| Conservación | Mantiene sin cambios F00–F07 aprobados, `Fuentes/`, persistencia y contratos de mutación existentes |
| Aprobación | Aprobada íntegramente por el responsable el 2026-09-10 antes de producir código |

La aprobación de esta adenda autorizará después la implementación local mínima de `HU-031`. No autoriza commit, push, publicación de rama, pull request, aprobación ni merge.

## 2. Precedencia comprobada

1. `origin/master` se verificó exactamente en `c87e6c94c5cd00eb3829d27619e16a2e4e6a3012`.
2. El commit implementado de `HU-028`, `6fdc187c6430624f134ddfecf1592f7ccb167df8`, y su merge son ancestros de `origin/master`.
3. `HU-028` consta merged en el PR `#48`, con pipeline requerido `SUCCESS` run `34541989487`, aprobación humana y corrida PostgreSQL externa consolidada `198 tests passed, 0 warnings`.
4. La tabla `Tareas insertadas por adenda` no contiene una tarea pendiente que bloquee `HU-031`.
5. El orden vigente coloca `HU-031` después de `HU-028` y `HU-023`, antes de `HU-029`; `HU-029` depende expresamente de `HU-031`.
6. La rama local `codex/hu-031` fue creada desde el `origin/master` verificado, sin modificar ni incorporar archivos ajenos.

La propuesta interpreta el registro de `HU-028` en `docs/traceability/IMPLEMENTATION_STATUS.md` conforme a la eficacia posterior al merge: el texto que viajó en el PR conserva el estado de propuesta, pero las evidencias anteriores hacen efectivo su cierre sin un commit administrativo adicional.

## 3. Fuentes y alcance interpretativo

Esta propuesta se limita a la fila de `HU-031`, `CAP-007`, `CAP-033`, `CAP-039`, `CAP-043`, `CA-031`, `CP-031-P`, `CP-031-N`, `RN-020`, `RN-025`, `RN-026`, la matriz de permisos aprobada, las filas directamente relacionadas de F06 y los contratos aprobados por las Adendas 15, 18, 21, 24 y 25.

Los contratos de `HU-023`, `HU-025` y `HU-028` se reutilizan por referencia normativa. Esta adenda no los amplía ni cambia sus DTO, autoridad o mutaciones.

## 4. Hechos documentales

1. `HU-031` exige que un superior vea únicamente niveles inferiores e identifique validaciones pendientes.
2. `PER-SUPERVISION-VER` no expone pares ni superiores. Rol y alcance proceden de asignaciones canónicas vigentes, nunca del puesto textual.
3. F06 reserva `GET /api/v1/supervision/obligations` a la vista inferior y `GET /api/v1/validations/pending` a autoridad válida de emisión o escalamiento.
4. La Adenda 25 reservó expresamente `GET /api/v1/validations/pending` a `HU-031`.
5. `HU-023` ya fijó instante único, jerarquía vigente, convergencia anti-IDOR, envelope, cursor opaco, orden, transacción `REPEATABLE READ, READ ONLY`, `AsNoTracking` y ausencia de efectos laterales para consultas de obligaciones.
6. La Adenda 18 ya fijó la consulta paginada de evidencia y su historia por obligación visible.
7. La Adenda 25 ya fijó la consulta del requisito e histórico de validación, la autoridad ordinaria, el escalamiento motivado y la sustitución; `HU-031` no crea operaciones equivalentes.
8. Una obligación sólo persiste `PENDIENTE` o `CONCLUIDA`. Validar no cambia la ejecución.
9. `validation_requirement` sólo persiste `PENDIENTE` o `RESUELTA`; existe a lo sumo uno por obligación y una decisión vigente por requisito.
10. Una obligación histórica con `validation_policy_version_id = NULL` no recibe requisito ni decisión, no consulta una política sustituta y no admite backfill implícito.

## 5. Contradicciones y carencias comprobadas

### 5.1 Contradicción de indicadores

`HU-031` y `CAP-043` mencionan indicadores, pero el backlog coloca `HU-029` después de `HU-031`, hace que `HU-029` dependa de `HU-031` y asigna a `HU-029` los cinco conteos de `RN-026` y `CA-029`.

La resolución propuesta es que `HU-031` implemente únicamente supervisión jerárquica e identificación de validaciones pendientes. Pendientes de validación es una colección de recursos accionables, no el indicador agregado “pendientes”. Conteos totales, porcentajes, denominadores, conciliación, KPI y tableros quedan íntegramente en `HU-029`.

### 5.2 Carencias que impiden producir código sin esta adenda

Los documentos previos no fijan de forma ejecutable:

- DTO, filtros, orden, cursor y errores de las dos rutas;
- diferencia entre supervisión y bandeja pendiente;
- participación de Dirección y tratamiento de obligaciones propias;
- instante aplicable a cambios posteriores de rol, responsable o alcance;
- definición completa de pendiente cuando falta requisito, política o decisión;
- relación entre autoridad ordinaria y escalamiento en la bandeja;
- proyección mínima de evidencia y validación;
- ETag, cache, snapshot y carreras de lectura;
- auditoría de consultas rechazadas; ni
- límites de respuesta, índices o persistencia requerida.

### 5.3 Inferencias que no autorizan código

La similitud con `GET /api/v1/obligations`, el índice futuro de `validation_requirement` y las rutas conceptuales de F06 permiten una solución técnica, pero no autorizan trasladar silenciosamente filtros, DTO o visibilidad. Tampoco puede inferirse que “escalamiento con motivo” cree una solicitud de escalamiento, ni que la palabra “indicadores” autorice los cinco conteos de `HU-029`.

## 6. Resultado cerrado y frontera con otras historias

### 6.1 Incluido

1. `GET /api/v1/supervision/obligations` como lectura compuesta del estado actual de obligaciones asignadas a niveles estrictamente inferiores.
2. `GET /api/v1/validations/pending` como lectura de obligaciones inferiores actualmente pendientes y accionables por el actor.
3. Filtros cerrados, paginación, orden, snapshot, autorización, anti-IDOR, cache, errores y pruebas de ambas rutas.
4. Vínculos a las consultas ya aprobadas de detalle, evidencia, revisión de evidencia e histórico de validación.
5. Trazabilidad mínima de `HU-031` después de la aprobación y en el mismo cambio de implementación.

### 6.2 Excluido

- los cinco indicadores, conteos totales, porcentajes, denominadores, KPI, gráfica o tablero de `HU-029`;
- `GET /api/v1/indicators` y cualquier alias equivalente;
- la vista integral, obligaciones propias o sin asignación y endpoint de Dirección de `HU-032`;
- auditoría general o consulta de `audit_event` de `HU-033`;
- UI, Razor Pages, componentes, CSS, JavaScript y Playwright;
- mutaciones nuevas, nuevo escalamiento, emisión, sustitución, reapertura, conclusión, cambio de resultado o sustitución de evidencia;
- cambio o recálculo de la política congelada de `HU-027`;
- avisos, bandeja personal paralela, mensajería, outbox, Worker, scheduler, broker, Redis, microservicio o integración externa; y
- tablas, columnas, índices, vistas materializadas, migraciones o backfill.

No se añade UI porque ninguna fuente aprobada fija una superficie visual de `HU-031`.

## 7. Instante único, autoridad y jerarquía actual

Cada petición captura exactamente una vez `queriedAt = IClock.UtcNow` antes de autorizar o leer. Toda vigencia, relación, filtro temporal, proyección y `meta.queriedAt` de esa respuesta usa ese mismo instante.

En `queriedAt` se exige sesión individual y MFA completos, cuenta y persona activas, empleo activo y vigente en `LOR-001`, exactamente un rol canónico activo y vigente y el permiso específico de la ruta. Ausencia, multiplicidad, rol desconocido, sucursal distinta o permiso ausente produce `403 ACCESO_DENEGADO` antes de resolver recursos.

La jerarquía estricta actual es:

| Rol del actor | Roles inferiores visibles |
|---|---|
| `DIRECCION` | `ADMINISTRACION`, `SUBCOORDINACION`, `PISO_VENTAS` |
| `ADMINISTRACION` | `SUBCOORDINACION`, `PISO_VENTAS` |
| `SUBCOORDINACION` | `PISO_VENTAS` |
| `PISO_VENTAS` | Ninguno; no obtiene una ruta de supervisión válida |

Dirección participa en `HU-031` sólo con esa regla estricta: no ve por estas rutas obligaciones propias, sin asignación vigente ni una vista integral. Esos recursos y la composición integral permanecen en `HU-032`. La posibilidad ya existente de consultar otros recursos por contratos de `HU-023` no amplía estas dos rutas.

Una obligación entra al universo sólo si tiene asignación `VIGENTE` y la persona responsable tiene cuenta, empleo y exactamente un rol canónico vigentes en `LOR-001` al `queriedAt`. El rol del responsable debe estar en la tabla anterior y ser estrictamente inferior al del actor. Nunca se usa puesto textual, rol histórico, autor de la conclusión, validador histórico, creador de la obligación ni responsable anterior.

Un cambio de responsable, rol, empleo o alcance modifica la visibilidad de peticiones posteriores. No reescribe obligación, evidencia, política congelada, requisito, decisión ni snapshots históricos. Poseer aisladamente `PER-SUPERVISION-VER`, `PER-VALIDACION-EMITIR` o `PER-VALIDACION-ESCALAR` no sustituye rol, jerarquía, sucursal ni alcance.

## 8. Relación entre las rutas

`GET /api/v1/supervision/obligations` responde “qué trabajo inferior puedo supervisar ahora” y exige `PER-SUPERVISION-VER`.

`GET /api/v1/validations/pending` responde “sobre qué trabajo inferior puedo emitir ahora una primera decisión ordinaria o escalada” y exige el permiso de decisión aplicable. No exige ni acepta `PER-SUPERVISION-VER` como sustituto de `PER-VALIDACION-EMITIR` o `PER-VALIDACION-ESCALAR`.

La primera ruta puede contener obligaciones pendientes, concluidas, con validación resuelta o no validables. La segunda sólo contiene el subconjunto concluido, con política congelada no nula, sin decisión vigente y con autoridad actual del actor. Ninguna de las dos sustituye el detalle ni los históricos aprobados.

## 9. `GET /api/v1/supervision/obligations`

### 9.1 Query exacta

Acepta exclusivamente:

| Parámetro | Regla |
|---|---|
| `level` | Uno de los cuatro roles canónicos con capitalización exacta. Un rol válido que no sea inferior al actor produce página vacía. |
| `responsiblePersonId` | UUID canónico no vacío; sólo coincide con la asignación `VIGENTE`. |
| `isoYear` | Entero de cuatro dígitos; debe aparecer junto con `isoWeek`. |
| `isoWeek` | Semana ISO válida para `isoYear`; debe aparecer junto con `isoYear`. |
| `executionStatus` | `PENDIENTE` o `CONCLUIDA`. |
| `cursor` | Cursor opaco emitido por esta ruta para los mismos filtros normalizados. |
| `limit` | Entero decimal de `1` a `100`; predeterminado `25`. |

Cada parámetro aparece como máximo una vez. Los presentes se combinan con `AND` después de incorporar la autorización al mismo árbol SQL. Parámetro desconocido, repetido, vacío, incompleto o inválido devuelve `400 FILTRO_SUPERVISION_INVALIDO`. Persona, nivel o semana válidos pero fuera del alcance producen `200` con página vacía.

### 9.2 Envelope y DTO exacto

La respuesta es:

```json
{
  "data": [],
  "meta": {
    "nextCursor": null,
    "count": 0,
    "queriedAt": "2026-09-10T20:00:00Z",
    "correlationId": "01900000-0000-7000-8000-000000000099"
  }
}
```

Cada elemento contiene exactamente:

```text
obligation: obligation-summary
responsibleLevel: ADMINISTRACION|SUBCOORDINACION|PISO_VENTAS
currentEvidence: current-evidence-summary[]
validation: {
  requirement: validation-requirement|null,
  currentDecision: validation-decision|null
}
links: {
  self: string,
  evidence: string,
  evidenceReview: string,
  validations: string
}
```

`obligation-summary` es exactamente el elemento de listado de la sección 8 de la Adenda 15, salvo que omite su propiedad `links`, reemplazada por los vínculos cerrados anteriores. Conserva tarea, origen, período, fechas, ejecución, condición y asignación actual sin agregar historia general.

Cada `current-evidence-summary` representa exclusivamente una `evidence_version` `VIGENTE` en el snapshot y contiene exactamente:

```text
evidenceItemId: uuid
itemRowVersion: integer
requirement: {
  requirementVersionId: uuid,
  requirementCode: string,
  kind: string
}
version: {
  evidenceVersionId: uuid,
  versionNo: integer,
  status: VIGENTE,
  submittedByUserId: uuid,
  submittedAt: instant,
  reason: string|null,
  supersedesEvidenceVersionId: uuid|null
}
sourceKind: FILE|STRUCTURED
```

No duplica `file`, `structuredPayload` ni versiones sustituidas. La consulta completa y paginada permanece en el vínculo `evidence`, con el contrato aprobado y su propia autorización.

`validation-requirement` y `validation-decision` son exactamente las proyecciones homónimas de la sección 7 de la Adenda 25. Sólo se proyecta la decisión `VIGENTE`. `foundation`, `reason`, actor, rol, autoridad, snapshot e IDs de versiones de evidencia se muestran porque forman parte del contrato de decisión ya aprobado; no se agregan puesto textual ni auditoría. El histórico completo, incluida toda decisión `SUSTITUIDA`, permanece en el vínculo `validations`.

Si no existe requisito, `requirement` y `currentDecision` son `null`. Si existe requisito `PENDIENTE`, `currentDecision` es `null`. Una combinación persistida imposible produce `500 CONSULTA_SUPERVISION_INCONSISTENTE`, sin respuesta parcial.

### 9.3 Orden y cursor

Se reutiliza el orden total de `HU-023`: `dueAt` ascendente con nulos al final, `period.startsOn` descendente, `taskCode` ascendente ordinal y `obligationId` ascendente. El lector solicita `limit + 1` y devuelve como máximo `limit`.

El cursor es Base64URL opaco y versionado, contiene las claves completas del último elemento y SHA-256 de los filtros normalizados, y está ligado exclusivamente a esta ruta. No contiene PII, nombres, autoridad ni datos de recursos ajenos. No sirve en otra ruta y nunca prueba autorización.

Dentro de cada obligación, `currentEvidence` se ordena por `requirementCode` ordinal ascendente y `evidenceVersionId` ascendente.

## 10. Definición exacta de validación pendiente

Una obligación es pendiente para `GET /api/v1/validations/pending` si y sólo si, en el mismo `queriedAt` y snapshot:

1. pertenece al universo inferior estricto de la sección 7;
2. `executionStatus = CONCLUIDA`;
3. `validation_policy_version_id` no es nulo y la política congelada es coherente con TAR, versión y matriz aprobada;
4. no existe una decisión `VIGENTE`; y
5. el actor posee autoridad actual ordinaria o de escalamiento conforme a la sección 11.

Se incluyen dos formas contractuales:

- requisito existente con estado `PENDIENTE` y ninguna decisión; o
- obligación concluida con política no nula, sin requisito y sin decisión, cuya primera emisión materializaría atómicamente el requisito conforme a la Adenda 25.

La segunda forma es una pendencia derivada de lectura; el GET no crea `validation_requirement`, snapshot, decisión, auditoría, idempotencia ni evento.

No son pendientes:

- obligaciones `PENDIENTE` de ejecución;
- obligaciones con `validation_policy_version_id = NULL`;
- política congelada incoherente;
- requisito `RESUELTA` o cualquier decisión `VIGENTE`;
- recursos fuera del alcance actual; ni
- una cadena con historia `SUSTITUIDA` pero sin decisión `VIGENTE`, porque viola las guardas aprobadas y produce `500 CONSULTA_VALIDACION_INCONSISTENTE` en vez de fingir pendencia.

## 11. Autoridad de la bandeja pendiente

La autoridad se deriva de la política exacta congelada y de roles actuales:

- `ORDINARIA`: el actor posee `PER-VALIDACION-EMITIR`, su rol coincide exactamente con `validatorRole`, el responsable conserva `executorRole`, son personas distintas y existe superioridad estricta;
- `ESCALAMIENTO`: el actor posee `PER-VALIDACION-ESCALAR`, su rol es estrictamente superior a `validatorRole` y al rol actual del responsable, y son personas distintas.

La bandeja de `HU-031` nunca incluye obligaciones propias; por tanto, la autovalidación excepcional de Dirección aprobada por `HU-028` no aparece aquí y sigue disponible sólo mediante los contratos de detalle y mutación ya aprobados.

Una obligación aparece a un nivel posterior con autoridad `ESCALAMIENTO` desde que satisface la sección 10. No existe una solicitud o estado persistido de escalamiento. Mostrarla no escala, reserva ni audita la obligación. La posterior llamada a `POST /api/v1/obligations/{id}/validation-decisions` conserva íntegramente el contrato de HU-028 y exige `escalationReason`; sin motivo se rechaza sin efecto.

Un actor que sólo posee permiso ordinario no ve pendientes escalables; quien sólo posee permiso de escalamiento no ve una obligación para la que no sea nivel posterior. Si por configuración inválida ambos modos parecieran aplicar, la consulta falla cerrada con `500 CONSULTA_VALIDACION_INCONSISTENTE`.

## 12. `GET /api/v1/validations/pending`

### 12.1 Query exacta

Acepta exclusivamente `level`, `responsiblePersonId`, `isoYear`, `isoWeek`, `cursor` y `limit`, con las mismas reglas de forma, combinación y ocultación de la sección 9.1. No acepta `executionStatus`, resultado, indicador ni estado de requisito, porque la definición de la sección 10 ya fija el conjunto.

Un query inválido devuelve `400 FILTRO_VALIDACIONES_PENDIENTES_INVALIDO`. Un filtro válido que apunta a persona, nivel o semana fuera de alcance devuelve página vacía.

### 12.2 Envelope y DTO exacto

Usa el mismo envelope y metadatos de la sección 9.2. Cada elemento contiene exactamente:

```text
obligation: obligation-summary
responsibleLevel: ADMINISTRACION|SUBCOORDINACION|PISO_VENTAS
pendingSince: instant
materializationStatus: MATERIALIZED|DERIVED
validationRequirement: validation-requirement|null
availableAuthority: {
  authorityType: ORDINARIA|ESCALAMIENTO,
  validatorRole: DIRECCION|ADMINISTRACION|SUBCOORDINACION,
  escalationReasonRequired: boolean
}
decisionEtag: string
links: {
  self: string,
  evidence: string,
  evidenceReview: string,
  validations: string,
  issueDecision: string
}
```

`obligation-summary` y `validation-requirement` reutilizan exactamente las definiciones de la sección 9.2. `pendingSince` es siempre el `concludedAt` persistido. `materializationStatus` es `MATERIALIZED` si existe requisito `PENDIENTE` y `DERIVED` si todavía no existe.

`decisionEtag` es la representación fuerte entre comillas de `validation_requirement.row_version` cuando existe requisito y de `work_obligation.row_version` en pendencia `DERIVED`, exactamente como exige la emisión de HU-028. No se emite un `ETag` de colección ni se inventa una versión agregada.

`escalationReasonRequired` es `false` sólo para `ORDINARIA` y `true` sólo para `ESCALAMIENTO`. El vínculo `issueDecision` apunta exclusivamente a la mutación ya aprobada; no incluye cuerpo, motivo ni autorización persistida.

### 12.3 Orden y cursor

El orden total es `pendingSince` ascendente y `obligationId` ascendente. El cursor sigue las garantías de opacidad, versión, vínculo a ruta y filtros, `limit + 1` y reevaluación de autoridad de la sección 9.3.

No se devuelve total de pendientes. `meta.count` cuenta sólo la página devuelta y no constituye el indicador de `HU-029`.

## 13. Evidencia, validaciones e historia permitida

La vista compuesta sólo proyecta evidencia vigente y decisión vigente. Para consultar el detalle y la historia:

1. `links.self` usa `GET /api/v1/obligations/{id}` de HU-023;
2. `links.evidence` usa `GET /api/v1/obligations/{id}/evidence` de HU-025, donde `status=VIGENTE|SUSTITUIDA` conserva su contrato;
3. `links.evidenceReview` usa `GET /api/v1/obligations/{id}/evidence-review` de HU-026; y
4. `links.validations` usa `GET /api/v1/obligations/{id}/validations` de HU-028, con fundamento, motivos, actor, autoridad, snapshots e historia versionada ya aprobados.

Cada ruta vuelve a autenticar y autorizar. Un vínculo no es una concesión durable. Si cambia el alcance, el detalle converge en `404 OBLIGACION_NO_ENCONTRADA`.

No se devuelve contenido binario, URL firmada, clave, bucket, scanner, secreto, cookie, cadena de conexión, TOTP, código de recuperación, payload de auditoría general ni puesto textual. La vista compuesta tampoco duplica nombre de archivo, hash o `structuredPayload`; esos datos sólo permanecen donde el contrato de evidencia existente ya los autoriza.

## 14. Snapshot, concurrencia, cache y ausencia de efectos

Cada petición usa una transacción PostgreSQL `REPEATABLE READ, READ ONLY`. Autoridad, filas, `meta.count`, evidencia vigente, requisito y decisión vigente pertenecen al mismo snapshot. Todas las consultas usan `AsNoTracking` y aplican alcance y filtros en SQL antes de materializar.

Una sustitución concurrente de evidencia o validación queda enteramente antes o después del snapshot; nunca se combinan versiones anteriores y posteriores en un elemento. La lectura no bloquea para escritura ni promete el mismo snapshot entre páginas. Cada página captura otro `queriedAt`, reevalúa autoridad y nunca amplía alcance por un cursor viejo.

Las respuestas envían `Cache-Control: private, no-store` y `Pragma: no-cache`. No se emite `ETag` de colección. Los tokens de fila sólo se exponen como datos donde HU-028 los requiere para una mutación posterior.

Los lectores no llaman `SaveChanges`, no materializan requisitos, no recalculan política, no crean snapshots, decisiones, auditoría, idempotencia, aviso, outbox ni evento, y no modifican ejecución, resultado, evidencia, ETag o `row_version`.

Una consulta exitosa o rechazada no inserta `audit_event`. Los rechazos producen únicamente telemetría técnica sanitizada ya prevista por la plataforma; `HU-033` no se adelanta.

## 15. Errores y convergencia anti-IDOR

Todos los errores usan `application/problem+json` con `status`, `code`, `title`, `instance` y `correlationId`, detalle público genérico y sin datos sensibles.

| HTTP | `code` | Uso |
|---:|---|---|
| 400 | `FILTRO_SUPERVISION_INVALIDO` | Query inválido de supervisión. |
| 400 | `FILTRO_VALIDACIONES_PENDIENTES_INVALIDO` | Query inválido de pendientes. |
| 401 | `AUTENTICACION_REQUERIDA` | Sesión ausente, expirada o MFA incompleto. |
| 403 | `ACCESO_DENEGADO` | Cuenta, empleo, rol, sucursal o permiso base inválidos; `PISO_VENTAS` no puede supervisar. |
| 500 | `CONSULTA_SUPERVISION_INCONSISTENTE` | Combinación imposible en la proyección compuesta. |
| 500 | `CONSULTA_VALIDACION_INCONSISTENTE` | Política, requisito, decisión o autoridad persistidos de manera incoherente. |
| 500 | `ERROR_INTERNO` | Falla no controlada, sin detalle sensible ni efecto. |

Al ser colecciones, estas rutas no aceptan identificador directo de obligación. Filtros válidos que intentan escapar del alcance devuelven `200` con `data=[]`, `count=0` y `nextCursor=null`; no confirman existencia. Los vínculos de detalle conservan el `404 OBLIGACION_NO_ENCONTRADA` indistinguible de sus contratos aprobados.

## 16. Persistencia y rendimiento

No se crea persistencia. Se reutilizan las tablas e índices aprobados, incluido `(status, created_at, id)` de `validation_requirement`. Si la implementación demuestra con plan PostgreSQL que un índice nuevo es indispensable, debe detenerse y solicitar otra decisión documental; esta propuesta no lo autoriza.

Cada página materializa como máximo `100` obligaciones. La consulta compuesta obtiene primero IDs autorizados y luego usa un número fijo y acotado de consultas por lotes restringidas a esos IDs para tarea, evidencia vigente y validación vigente. Se prohíben N+1, carga previa de recursos fuera de alcance, historia completa dentro de la página y colecciones sin límite.

Se mantiene `NFR-002` de F06: p95 menor a `2 s` en lectura con la carga objetivo del MVP y sin degradación no acotada por cantidad de historia. La validación de desempeño usa datos sintéticos y plan de consulta PostgreSQL; no introduce cache persistente ni proyección materializada.

## 17. Pruebas obligatorias después de la aprobación

La implementación debe demostrar al menos:

1. sesión ausente o MFA incompleto produce `401` sin exposición ni efecto;
2. permiso aislado sin cuenta, empleo, rol, jerarquía, sucursal o alcance produce `403`;
3. Administración ve sólo Subcoordinación y Piso inferiores; no Dirección, Administración par, otra sucursal ni obligación propia;
4. Subcoordinación ve sólo Piso inferior; Piso no obtiene supervisión;
5. Dirección ve sólo sus tres niveles inferiores por HU-031, no propia, no sin asignación y no vista integral;
6. puesto textual, rol histórico o relación anterior no concede visibilidad;
7. cambio vigente de rol o responsable cambia sólo peticiones posteriores;
8. filtros de nivel, persona, semana y ejecución combinan con `AND` sin ampliar alcance;
9. filtro válido fuera de alcance produce página vacía y los detalles existentes convergen en `404`;
10. orden, cursor, hash de filtros, desempate, límite y reevaluación entre páginas son deterministas;
11. supervisión proyecta exactamente obligación, evidencia vigente y validación vigente aprobadas, y sus vínculos permiten los históricos existentes;
12. evidencia sustituida no aparece en `currentEvidence`, pero permanece consultable en el histórico aprobado;
13. validación pendiente existente y derivada aparecen sin materializar requisito;
14. obligación no concluida, política nula, decisión vigente, requisito resuelto o recurso fuera de alcance no aparecen como pendientes;
15. historia sustituida sin decisión vigente e incoherencias de política fallan cerradas;
16. autoridad ordinaria aparece sólo al rol exacto y escalamiento sólo a un nivel posterior con permiso;
17. una pendiente escalable aparece antes de la mutación, pero emitir sin `escalationReason` conserva el rechazo de HU-028;
18. autovalidación de Dirección y obligaciones propias no aparecen en esta bandeja;
19. una sustitución concurrente de evidencia o validación produce un snapshot anterior o posterior completo, nunca una mezcla;
20. huellas y conteos antes/después prueban que ambos GET no crean requisito, snapshot, decisión, auditoría, idempotencia, aviso, outbox ni cambian estado, evidencia, ETag o `row_version`;
21. no se exponen secretos, binarios, URLs firmadas, payload de auditoría, puesto textual ni datos fuera de alcance;
22. arquitectura confirma contratos explícitos, lector read-only, autorización SQL, ausencia de persistencia y ausencia de dependencias propietarias indebidas;
23. PostgreSQL real demuestra autorización, anti-IDOR, filtros, paginación, snapshot concurrente, ausencia de N+1 y no filtración entre alcances; y
24. no existen rutas, DTO o conteos de indicadores de `HU-029`, vista integral de `HU-032` ni auditoría general de `HU-033`.

## 18. Gates, trazabilidad y eficacia

Después de aprobación íntegra, durante el desarrollo sólo se ejecutarán pruebas enfocadas. Una sola vez al final se ejecutarán restore locked autorizado, build Release, suite local completa sin integración PostgreSQL, format, pruebas de arquitectura/API/seguridad, vulnerabilidades NuGet, espejo y protección de `Fuentes/` y `git diff --check`. No aplica gate de migración ni modelo EF porque esta adenda prohíbe cambios de persistencia.

Cuando los gates locales estén listos se solicitará una única corrida PostgreSQL externa consolidada. Su resultado se incorporará a la evidencia de entrega, no a esta propuesta previa.

`docs/traceability/IMPLEMENTATION_STATUS.md` se actualizará únicamente en el cambio de implementación aprobado, como `Propuesta implementada`, nunca como `Terminada`. Presentar esta adenda no exige alterar ese registro porque todavía no existe implementación ni cierre que registrar.

`HU-031` sólo será `Terminada` después de commit exacto, pipeline requerido exitoso sobre ese commit, aprobación humana, merge, ascendencia verificada en `origin/master`, PostgreSQL externo satisfactorio, protección de `Fuentes/` y cero defectos bloqueantes. Ninguna condición autoriza automáticamente la siguiente acción Git.

## 19. Preguntas cerradas por la propuesta

La aprobación íntegra responderá conjuntamente:

1. que Dirección participa en HU-031 sólo sobre niveles estrictamente inferiores y deja la vista integral para HU-032;
2. que ambas rutas excluyen obligaciones propias y sin asignación vigente;
3. que la jerarquía y el alcance se evalúan al único `queriedAt`, sin reescribir hechos históricos;
4. que supervisión y pendientes tienen permisos y propósitos distintos;
5. que una pendiente requiere conclusión, política congelada no nula, ausencia de decisión vigente y autoridad actual;
6. que la pendencia derivada sin requisito se muestra sin materialización lateral;
7. que política nula, ejecución no concluida y requisito resuelto no son pendientes;
8. que un nivel posterior ve una pendiente escalable antes de emitir, pero la mutación conserva motivo obligatorio;
9. que la vista compuesta muestra sólo evidencia y decisión vigentes y remite a los históricos existentes;
10. que no se crean auditoría por lectura, persistencia, migración, índice, cache persistente ni ETag de colección;
11. que filtros fuera de alcance producen páginas vacías y detalles existentes mantienen convergencia `404`; y
12. que los cinco indicadores quedan íntegramente reservados a HU-029 y la vista integral a HU-032.

## 20. Decisión íntegra solicitada

Se solicita aprobar o rechazar la Adenda 26 como una unidad. No existe aprobación parcial implícita.

La pregunta de aprobación es: **¿se aprueba íntegramente `F07_ADENDA_26_CONTRATO_DE_SUPERVISION_JERARQUICA_Y_VALIDACIONES_PENDIENTES_HU_031.md`, sin cambios, para autorizar la implementación local de `HU-031` bajo este contrato?**
