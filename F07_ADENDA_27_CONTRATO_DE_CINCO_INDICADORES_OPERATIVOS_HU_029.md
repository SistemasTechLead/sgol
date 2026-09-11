# SGOL — Adenda 27 a F07: contrato de cinco indicadores operativos de HU-029

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Propuesta de adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE; IMPLEMENTACIÓN EN CURSO` |
| Fecha | 2026-09-11 |
| Historia | `HU-029 — Superior consulta cinco indicadores objetivos` |
| Corte y épica | `CV-05` / `EP-09` |
| Criterios | `CA-029`, `CP-029-P`, `CP-029-N`, `RN-004`, `RN-025`, `RN-026` |
| Base aceptada | `HU-022`, `HU-023`, `HU-027`, `HU-028` y `HU-031` terminadas efectivamente conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md` |
| Efecto propuesto | Precisar exclusivamente la lectura derivada de los cinco indicadores operativos de `HU-029` |
| Conservación | Mantiene sin cambios F00–F07 aprobados, `Fuentes/`, estados, políticas, decisiones, asignaciones y persistencia existentes |
| Aprobación | Aprobada íntegramente por el responsable el 2026-09-11 antes de producir código |

La aprobación íntegra de esta adenda autorizará después la implementación local mínima de `HU-029`. No autoriza commit, push, publicación de rama, pull request, aprobación ni merge.

## 2. Precedencia comprobada

1. `origin/master` se verificó exactamente en `e47b6de6daa433ec0a62027c4e523931219086e5`.
2. El commit implementado de `HU-031`, `9b75e1c2bf1d1719f960fa1ed310a1ef40eb3275`, y su merge son ancestros de `origin/master`.
3. `HU-031` consta merged en el PR `#49`, con pipeline requerido `SUCCESS` run `34550189196`, aprobación humana y corrida PostgreSQL externa consolidada `202 tests passed, 0 warnings`.
4. La tabla `Tareas insertadas por adenda` no contiene una tarea pendiente que bloquee `HU-029`.
5. El backlog vigente coloca `HU-029` inmediatamente después de `HU-031`, depende de `HU-022`, `HU-028` y `HU-031`, y reserva `HU-032` y `HU-033` para pasos posteriores.
6. `F07_ADENDA_27` era el siguiente identificador libre.
7. La rama local `codex/hu-029` se creó desde el `origin/master` verificado sin incorporar cambios ajenos.

La propuesta interpreta el registro de `HU-031` en `docs/traceability/IMPLEMENTATION_STATUS.md` conforme a la eficacia posterior al merge: el texto que viajó en el PR conserva el estado de propuesta, pero las evidencias anteriores hacen efectivo su cierre sin un commit administrativo adicional.

## 3. Fuentes y alcance interpretativo

Esta propuesta se limita a `HU-029`, `CAP-019`, `CAP-027`, `CAP-033`, `CAP-039`, `CA-029`, `CP-029-P`, `CP-029-N`, `RN-004`, `RN-025`, `RN-026`, la matriz de permisos aprobada, las secciones directamente relacionadas de F06 y los contratos aprobados por las Adendas 7, 15, 21, 24, 25 y 26.

Los contratos de consulta, conclusión, carga activa, política congelada, requisito, decisión vigente, sustitución e historia se reutilizan por referencia normativa. Esta adenda no los amplía ni modifica sus operaciones de escritura.

## 4. Hechos documentales

1. `HU-029` exige exactamente cinco indicadores: pendientes, concluidas, validadas, incumplidas y carga activa por persona.
2. `CA-029` exige período, alcance y denominador visibles; `CP-029-P` exige conciliación sobre un conjunto conocido.
3. `CP-029-N` establece que una obligación vencida sin decisión `NO_CUMPLIDA` no cuenta como incumplida y prohíbe mostrar monto.
4. `RN-025` permite al responsable consultar lo propio, a los superiores consultar lo propio y niveles inferiores, y a Dirección consultar todo.
5. La matriz concede `PER-INDICADOR-VER` a Dirección, Administración, Subcoordinación y Piso, con alcance visible para los tres primeros y propio para Piso.
6. `HU-004` ya define carga activa como obligaciones `PENDIENTE` con asignación `VIGENTE`, una vez por obligación, y devuelve personas visibles con carga cero.
7. Una obligación sólo persiste `executionStatus = PENDIENTE|CONCLUIDA`; el vencimiento no cambia ese estado.
8. Una decisión de validación usa `result = CUMPLIDA|INCOMPLETA|NO_CUMPLIDA` y `status = VIGENTE|SUSTITUIDA`; validar no cambia la ejecución.
9. La política exacta queda congelada en `work_obligation.validation_policy_version_id`; obligaciones históricas pueden conservar `NULL` y no reciben backfill implícito.
10. `HU-031` reservó expresamente para `HU-029` los cinco indicadores, `GET /api/v1/indicators`, los conteos y sus denominadores.
11. F06 usa el prefijo común `/api/v1`, nombra conceptualmente `GET /indicators` y exige filtros desconocidos rechazados, JSON `camelCase`, `application/problem+json` y `correlationId`.
12. Los indicadores son valores derivados y no existe autorización para monto, incentivo, nómina, porcentaje financiero, definición KPI versionada ni medición persistida.

## 5. Contradicciones y carencias comprobadas

### 5.1 Alcance por rol

`RN-025` y la matriz incluyen datos propios y conceden `PER-INDICADOR-VER` a Piso, mientras el título de `HU-029` habla de un superior. `HU-031`, además, se cerró deliberadamente sólo para niveles estrictamente inferiores. Los documentos no deciden por sí solos si `HU-029` hereda la restricción de `HU-031` o la regla general de `RN-025`.

Dirección posee `PER-INDICADOR-VER`, pero `HU-032` reserva su vista integral de toda `LOR-001`. Falta precisar si Dirección puede consumir la lectura operativa limitada de `HU-029` antes de esa vista integral.

### 5.2 Significado de los cinco indicadores

Los documentos no fijan de forma ejecutable:

- si “pendientes” significa ejecución pendiente, validación pendiente, vencimiento o una combinación;
- si “concluidas” depende exclusivamente de `executionStatus`;
- si “validadas” incluye cualquier resultado vigente o sólo resultados favorables;
- si “incumplidas” exige una decisión vigente `NO_CUMPLIDA`;
- el universo, denominador y estructura de carga activa por persona;
- el tratamiento de política nula, requisito ausente o pendiente, decisión sustituida y reasignación histórica; ni
- la unidad de conteo y el uso de identidad distinta de obligación.

### 5.3 Contrato HTTP y consistencia

La ruta conceptual no fija versión, query exacta, obligatoriedad y forma del período, DTO, tipos numéricos, nombres JSON, paginación de personas, instante de corte, aislamiento, cache, ETag, errores, telemetría, persistencia ni objetivo de rendimiento.

### 5.4 Inferencias que no autorizan código

La similitud con `GET /api/v1/loads`, `GET /api/v1/supervision/obligations` o `GET /api/v1/validations/pending` permite reutilización técnica, pero no autoriza copiar silenciosamente su permiso, universo, DTO o filtros. Tampoco puede inferirse que “vencida” equivale a incumplida, que toda conclusión está validada, que cualquier fila histórica es vigente o que la palabra “indicadores” autoriza UI, porcentajes o persistencia KPI.

## 6. Propuesta contractual: resultado cerrado y frontera con otras historias

### 6.1 Incluido

1. Una única lectura `GET /api/v1/indicators` calculada bajo demanda.
2. Exactamente cuatro conteos agregados y una distribución de carga activa por persona.
3. Período semanal ISO obligatorio, alcance visible, filtros cerrados, denominadores, orden, paginación de personas, snapshot, autorización, anti-IDOR, cache, errores y pruebas.
4. Reutilización de `Sgol.Reporting.Contracts`, de la expresión jerárquica y del lector PostgreSQL read-only aprobados, sin crear un segundo modelo propietario por indicador.
5. Trazabilidad mínima de `HU-029` únicamente después de la aprobación y en el mismo cambio de implementación.

### 6.2 Excluido

- `GET /api/v1/direction/overview`, vista integral, tablero, gráfica o composición de `HU-032`;
- consulta general, detalle o persistencia de auditoría de `HU-033`;
- la colección accionable de validaciones pendientes o una segunda consulta de supervisión de `HU-031`;
- monto, incentivo, nómina, rentabilidad, salud de integraciones, porcentajes y cualquier sexto indicador;
- UI, Razor Pages, vistas, componentes, CSS, JavaScript y Playwright;
- mutación de ejecución, resultado, evidencia, asignación, requisito, política, decisión o historia;
- materialización de requisito, recálculo de política congelada, reapertura, sustitución o tarea correctiva;
- auditoría funcional de lecturas, avisos, mensajería, outbox, Worker, scheduler, broker, Redis, microservicio o integración externa; y
- tabla, columna, vista SQL, vista materializada, proyección persistida, índice, migración o backfill.

No se añade UI porque ninguna fuente aprobada fija una superficie visual de `HU-029`.

## 7. Ruta, query exacta y período

La única ruta es:

```http
GET /api/v1/indicators
```

Acepta exclusivamente:

| Parámetro | Regla |
|---|---|
| `isoYear` | Obligatorio; entero decimal de cuatro dígitos. |
| `isoWeek` | Obligatorio; entero decimal que identifica una semana ISO válida dentro de `isoYear`. |
| `level` | Opcional; uno de `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION` o `PISO_VENTAS`, con capitalización exacta. |
| `responsiblePersonId` | Opcional; UUID canónico no vacío; coincide únicamente con la persona de la asignación `VIGENTE`. |
| `cursor` | Opcional; cursor opaco emitido por esta ruta para el mismo período, filtros y alcance. Pagina sólo `activeLoadByPerson.items`. |
| `limit` | Opcional; entero decimal de `1` a `100`; predeterminado `25`. Pagina sólo `activeLoadByPerson.items`. |

Cada parámetro aparece como máximo una vez. Todos los filtros presentes se combinan con `AND` después de incorporar autorización y alcance al mismo árbol SQL. Parámetro ausente obligatorio, desconocido, repetido, vacío, incompleto o inválido devuelve `400 FILTRO_INDICADORES_INVALIDO`.

Un `level`, `responsiblePersonId` o período válidos sin coincidencias o fuera del alcance actual devuelve `200` con los cuatro conteos y denominadores en cero y una colección de carga vacía. No confirma existencia.

El período pedido es siempre la semana ISO lunes–domingo de `isoYear` e `isoWeek` en `America/Mexico_City`. La respuesta deriva `startsOn` y `endsOn` sin crear `week_period`. Una obligación pertenece al período únicamente por su relación persistida con el `week_period` cuyos `iso_year` e `iso_week` coinciden; no se reasigna por `dueAt`, `concludedAt`, `decidedAt` ni por el instante de consulta. Una semana válida sin fila o sin obligaciones devuelve ceros.

## 8. Instante único, autoridad y alcance actual

Cada petición captura exactamente una vez `queriedAt = IClock.UtcNow` antes de autorizar o leer. Cuenta, empleo, rol, asignación, alcance, decisión vigente, filtros y `meta.queriedAt` usan ese mismo instante y snapshot.

Se exige sesión individual y MFA completos, cuenta y persona activas, empleo activo y vigente en `LOR-001`, exactamente un rol canónico activo y vigente y `PER-INDICADOR-VER`. Ausencia, multiplicidad, rol desconocido, sucursal distinta o permiso ausente produce `403 ACCESO_DENEGADO` antes de resolver datos.

El alcance operativo de `HU-029` es:

| Rol vigente del actor | Personas y obligaciones incluidas |
|---|---|
| `DIRECCION` | Persona propia y personas con rol `ADMINISTRACION`, `SUBCOORDINACION` o `PISO_VENTAS`, sólo mediante asignación `VIGENTE`. |
| `ADMINISTRACION` | Persona propia y personas con rol `SUBCOORDINACION` o `PISO_VENTAS`, sólo mediante asignación `VIGENTE`. |
| `SUBCOORDINACION` | Persona propia y personas con rol `PISO_VENTAS`, sólo mediante asignación `VIGENTE`. |
| `PISO_VENTAS` | Únicamente la persona propia y sus obligaciones con asignación `VIGENTE`. |

No se incluyen pares distintos del actor, superiores, otra sucursal, personas inactivas, obligaciones sin asignación vigente ni obligaciones cuyo responsable vigente carezca de cuenta, empleo y exactamente un rol canónico vigentes en `LOR-001`. Puesto textual, turno, nombre, responsabilidad histórica, autor de conclusión, validador, creador o datos del cliente no conceden visibilidad.

Dirección usa esta ruta sólo con el universo asignado anterior y sin recursos integrales adicionales. Obligaciones sin asignación vigente, composiciones de toda `LOR-001`, navegación, filtros amplios y vista integral permanecen reservados a `HU-032`.

`level` restringe el universo a personas visibles con ese rol. El nivel propio sólo puede incluir al actor, nunca pares. `responsiblePersonId` restringe a una persona ya visible. Los filtros no amplían alcance y pueden combinarse.

Un cambio confirmado de responsable, rol, empleo o cuenta afecta peticiones posteriores. `HU-029` es una vista operativa actual filtrada por período; no reconstruye responsabilidad histórica ni congela alcance al cierre del período.

## 9. Universo base y reglas comunes de conteo

El universo base contiene los IDs distintos de `work_obligation` que, en el mismo snapshot:

1. pertenecen a `LOR-001`;
2. pertenecen al período exacto de la sección 7;
3. tienen exactamente una `assignment_version` `VIGENTE` en `queriedAt`;
4. su responsable vigente satisface el alcance de la sección 8; y
5. satisface `level` y `responsiblePersonId` cuando se proporcionan.

`baseObligationsCount` es el número entero no negativo de IDs distintos de obligación de ese universo. Cada obligación cuenta como máximo una vez en cada indicador aplicable. Una reasignación `SUSTITUIDA`, un responsable histórico y una fila histórica de decisión nunca agregan otra unidad.

Las personas de carga forman un universo independiente de presentación: personas activas, con cuenta individual activa, empleo vigente, exactamente un rol canónico vigente y dentro del alcance y filtros de la sección 8. Se incluye una persona visible con carga cero aunque no tenga obligación del período.

`RN-004` no crea un filtro diario ni cambia los conteos: la disponibilidad binaria pertenece a elegibilidad y no determina estos cinco indicadores.

## 10. Definición exacta de los cinco indicadores

### 10.1 `pending`

`pending.count` cuenta obligaciones del universo base con `executionStatus = PENDIENTE`. No exige vencimiento, evidencia faltante, política de validación, requisito ni autoridad para validar. No representa la colección de validaciones pendientes de `HU-031`.

`pending.denominator = baseObligationsCount`.

### 10.2 `concluded`

`concluded.count` cuenta obligaciones del universo base con `executionStatus = CONCLUIDA`. Depende exclusivamente del estado de ejecución persistido y no de evidencia, requisito o resultado de validación.

`concluded.denominator = baseObligationsCount`.

Por integridad, `pending.count + concluded.count = baseObligationsCount`.

### 10.3 `validated`

`validated.count` cuenta obligaciones distintas del universo base que tienen exactamente una decisión `VIGENTE`, cualquiera de `CUMPLIDA`, `INCOMPLETA` o `NO_CUMPLIDA`. Una decisión `SUSTITUIDA` no cuenta. La cadena completa cuenta como máximo una vez por su única decisión vigente.

`validated.denominator = baseObligationsCount`.

La política nula, el requisito no materializado, el requisito `PENDIENTE` y la ausencia de decisión vigente cuentan cero en `validated`. La lectura no materializa el requisito ni busca una política posterior.

### 10.4 `nonCompliant`

`nonCompliant.count` cuenta obligaciones distintas del universo base cuya única decisión `VIGENTE` tiene `result = NO_CUMPLIDA`. Es un subconjunto de `validated` y no depende de `dueAt`, condición vencida, retraso, evidencia faltante ni `executionStatus = PENDIENTE`.

`nonCompliant.denominator = baseObligationsCount`.

Una obligación vencida sin decisión vigente `NO_CUMPLIDA` cuenta cero en `nonCompliant`. Una decisión `NO_CUMPLIDA` sustituida cuenta cero si la vigente tiene otro resultado; una decisión vigente `NO_CUMPLIDA` cuenta una vez aunque existan versiones sustituidas anteriores.

### 10.5 `activeLoadByPerson`

Cada elemento cuenta las obligaciones del universo base con `executionStatus = PENDIENTE` cuya única asignación `VIGENTE` referencia a esa persona. El conteo es entero no negativo y reutiliza exactamente la definición de carga activa de `HU-004`, restringida adicionalmente al período y filtros de `HU-029`.

`activeLoadByPerson.denominator = pending.count`. La suma de `count` de todas las personas de la colección completa, no sólo de una página, es exactamente `pending.count`. Si no hay pendientes, el denominador y todas las cargas devueltas son cero.

Una asignación `SUSTITUIDA` no cuenta y una reasignación mueve la obligación únicamente al responsable vigente en peticiones posteriores. No se devuelve una fila sintética para obligaciones sin asignación: esas obligaciones quedan fuera de `HU-029` y permanecen en la frontera de la vista integral de `HU-032`.

### 10.6 Inconsistencias persistidas

Más de una asignación vigente, más de una decisión vigente, decisión vigente sin cadena coherente, requisito `RESUELTA` sin decisión vigente, historia sólo `SUSTITUIDA` sin sucesora vigente o política/requisito/decisión incompatibles producen `500 CONSULTA_INDICADORES_INCONSISTENTE`. La respuesta falla completa; no presenta conteos parciales ni interpreta una anomalía como cero.

## 11. Envelope y DTO exacto

Una respuesta exitosa contiene exactamente:

```json
{
  "data": {
    "period": {
      "isoYear": 2026,
      "isoWeek": 37,
      "startsOn": "2026-09-07",
      "endsOn": "2026-09-13",
      "timeZone": "America/Mexico_City"
    },
    "scope": {
      "branchCode": "LOR-001",
      "actorRole": "ADMINISTRACION",
      "includedLevels": ["ADMINISTRACION", "SUBCOORDINACION", "PISO_VENTAS"],
      "level": null,
      "responsiblePersonId": null
    },
    "baseObligationsCount": 8,
    "pending": { "count": 3, "denominator": 8 },
    "concluded": { "count": 5, "denominator": 8 },
    "validated": { "count": 4, "denominator": 8 },
    "nonCompliant": { "count": 1, "denominator": 8 },
    "activeLoadByPerson": {
      "denominator": 3,
      "items": [
        {
          "person": {
            "id": "01900000-0000-7000-8000-000000000010",
            "stableCode": "EMP-001",
            "displayName": "Persona sintética"
          },
          "level": "SUBCOORDINACION",
          "count": 2
        }
      ]
    }
  },
  "meta": {
    "nextCursor": null,
    "count": 1,
    "queriedAt": "2026-09-11T18:00:00Z",
    "correlationId": "01900000-0000-7000-8000-000000000099"
  }
}
```

No existen otras propiedades. Todos los conteos y denominadores son enteros JSON no negativos respaldados por `Int64`; no se devuelven decimal, porcentaje, tasa, unidad monetaria ni valor nulo.

`scope.includedLevels` muestra el alcance efectivo después de aplicar `level`; conserva orden jerárquico desde el rol del actor hacia abajo y nunca enumera un nivel no visible. `scope.responsiblePersonId` devuelve el UUID solicitado sólo cuando esa persona es visible; un identificador inexistente o fuera de alcance se normaliza a `null` junto con resultado vacío para no confirmar existencia.

`meta.count` es exclusivamente la cantidad de elementos de `activeLoadByPerson.items` en la página. Los cuatro conteos, `baseObligationsCount` y `activeLoadByPerson.denominator` siempre representan todo el universo autorizado después de filtros, no sólo la página.

Nombres se serializan en `camelCase`; UUID en formato canónico `D` minúsculo; fechas como `YYYY-MM-DD`; `queriedAt` como RFC 3339 UTC con sufijo `Z`.

## 12. Orden y cursor de carga por persona

`activeLoadByPerson.items` se ordena por `person.stableCode` ordinal ascendente y después por `person.id` ascendente. El lector solicita `limit + 1` personas y devuelve como máximo `limit`.

El cursor es Base64URL opaco, versionado, ligado exclusivamente a `GET /api/v1/indicators`, al actor, al período, a los filtros normalizados y al alcance; contiene las claves completas del último elemento y SHA-256 de ese contexto. No contiene nombre, monto, carga, PII adicional ni autoridad durable. No sirve en otra ruta ni como prueba de acceso.

Cada página es una consulta nueva: reevalúa autorización y vuelve a calcular todos los conteos bajo su propio snapshot. Con datos y autoridad sin cambios, la paginación no duplica ni omite personas. Si cambia el alcance, un cursor antiguo nunca lo amplía y puede devolver menos personas o `400 FILTRO_INDICADORES_INVALIDO` cuando ya no corresponde a los filtros y contexto firmados.

## 13. Snapshot, concurrencia, cache y ausencia de efectos

Cada petición usa una transacción PostgreSQL `REPEATABLE READ, READ ONLY`. Autoridad, personas, obligaciones, asignaciones, política congelada, requisito, decisión vigente, conteos, denominadores y página pertenecen al mismo snapshot. Todas las consultas de entidad usan `AsNoTracking` y aplican alcance y filtros en SQL antes de materializar.

Una conclusión, reasignación, cambio de rol o sustitución de decisión concurrente queda enteramente antes o después del snapshot. Una misma respuesta nunca mezcla el conteo anterior con la distribución posterior ni cuenta a la vez decisión vigente y sustituida.

Las respuestas envían `Cache-Control: private, no-store` y `Pragma: no-cache`. No se emite `ETag` de colección ni se acepta `If-Match` o `Idempotency-Key`.

El lector no llama `SaveChanges`; no crea período, requisito, decisión, snapshot, auditoría, idempotencia, aviso, outbox ni evento; no recalcula política; y no modifica ejecución, resultado, evidencia, asignación, ETag o `row_version`.

Una consulta exitosa o rechazada no inserta `audit_event`. Los rechazos producen únicamente telemetría técnica sanitizada ya prevista por la plataforma; `HU-033` no se adelanta.

## 14. Errores y convergencia anti-IDOR

Todos los errores usan `application/problem+json` con `status`, `code`, `title`, `instance` y `correlationId`, detalle público genérico y sin datos sensibles.

| HTTP | `code` | Uso |
|---:|---|---|
| 400 | `FILTRO_INDICADORES_INVALIDO` | Query ausente, desconocido, repetido, vacío, incompleto, inválido o cursor incompatible. |
| 401 | `AUTENTICACION_REQUERIDA` | Sesión ausente, expirada o MFA incompleto. |
| 403 | `ACCESO_DENEGADO` | Cuenta, empleo, rol, sucursal, permiso o alcance base inválidos. |
| 500 | `CONSULTA_INDICADORES_INCONSISTENTE` | Asignación, política, requisito, decisión o conciliación persistidos de manera incoherente. |
| 500 | `ERROR_INTERNO` | Falla no controlada, sin detalle sensible ni efecto. |

Al ser una lectura agregada, no acepta identificador directo de obligación. Filtros válidos que intentan escapar del alcance devuelven conteos cero y colección vacía; no confirman persona, período, obligación, requisito o decisión. No se devuelven identificadores de obligaciones ni decisiones.

## 15. Persistencia, arquitectura y rendimiento

No se crea persistencia. Se amplían los contratos explícitos existentes bajo `Sgol.Reporting.Contracts` y el adaptador PostgreSQL read-only ya usado por `HU-023` y `HU-031`. `Reporting` proyecta mediante contratos internos las tablas poseídas por otros módulos y no las escribe ni expone `DbContext`, entidades rastreadas o SQL libre al endpoint.

La consulta se calcula bajo demanda. Se prohíben N+1, carga de recursos fuera de alcance, enumeración de historia completa, consulta por persona repetida, cache persistente y materialización previa de todo el universo en memoria. El conteo agrupado y la página de personas se resuelven con un número fijo y acotado de consultas SQL dentro del mismo snapshot.

Se mantiene el objetivo aprobado de lectura p95 menor a `2 s` con la carga objetivo del MVP. Las pruebas de rendimiento usan datos sintéticos y plan PostgreSQL. Si un plan demuestra que una vista o índice nuevo es indispensable, la implementación se detiene y solicita otra decisión documental; esta adenda no lo autoriza.

## 16. Pruebas obligatorias después de la aprobación

La implementación debe demostrar al menos:

1. sesión ausente o MFA incompleto produce `401` sin exposición ni efecto;
2. permiso aislado sin cuenta, empleo, rol, sucursal o alcance produce `403`;
3. puesto textual, rol o empleo obsoleto y responsabilidad histórica no conceden visibilidad;
4. Administración ve sólo propia, Subcoordinación y Piso; Subcoordinación sólo propia y Piso; Piso sólo propia;
5. pares, superiores, otra sucursal, personas inactivas y recursos fuera de alcance no afectan ningún conteo ni denominador;
6. Dirección obtiene sólo la lectura asignada de `HU-029`, sin obligación sin asignación ni vista integral;
7. filtros válidos de nivel y persona restringen todos los conteos y nunca amplían alcance;
8. parámetros ausentes, desconocidos, repetidos, vacíos, incompletos e inválidos producen el error exacto;
9. período ISO válido sin datos devuelve DTO exacto, cuatro conteos y denominadores cero y las personas visibles con carga cero;
10. un conjunto sintético conocido concilia exactamente `pending`, `concluded`, `validated`, `nonCompliant` y `activeLoadByPerson`;
11. cada obligación cuenta como máximo una vez por indicador y `pending + concluded = baseObligationsCount`;
12. la suma de la colección completa de carga es `pending.count`, incluso con paginación;
13. `pending` significa ejecución pendiente y no validación pendiente o vencimiento;
14. `concluded` depende sólo de `executionStatus = CONCLUIDA`;
15. cualquier resultado vigente permitido cuenta una vez en `validated`;
16. una decisión sustituida no duplica `validated` ni `nonCompliant`;
17. una obligación vencida sin decisión vigente `NO_CUMPLIDA` no cuenta en `nonCompliant`;
18. una decisión vigente `NO_CUMPLIDA` cuenta exactamente una vez, independientemente de versiones sustituidas;
19. política nula, requisito no materializado o `PENDIENTE` y ausencia de decisión vigente cuentan cero sólo en los indicadores de validación, sin alterar ejecución o carga;
20. reasignaciones históricas no duplican carga y una sustitución mueve el conteo sólo en peticiones posteriores;
21. persona visible sin obligaciones permanece en carga con cero;
22. orden, desempate, límite, cursor, vínculo de filtros y reevaluación de alcance son deterministas;
23. una conclusión, reasignación o sustitución concurrente produce un snapshot anterior o posterior completo, nunca conteos incompatibles;
24. incoherencias persistidas fallan cerradas sin conteos parciales;
25. huellas y conteos antes/después prueban que el GET no crea período, requisito, decisión, snapshot, auditoría, idempotencia, aviso, outbox ni evento;
26. la lectura no cambia ejecución, asignación, evidencia, ETag o `row_version`;
27. no aparecen monto, incentivo, nómina, rentabilidad, porcentaje financiero ni indicador adicional;
28. no aparece `/api/v1/direction/overview`, vista integral, UI ni consulta general de auditoría;
29. arquitectura confirma contratos explícitos, lector read-only, autorización SQL, ausencia de persistencia y dependencias correctas; y
30. PostgreSQL real demuestra conciliación, `DISTINCT`, anti-IDOR, filtros, paginación, snapshot concurrente, ausencia de N+1 y no filtración entre alcances.

## 17. Gates, trazabilidad y eficacia

Después de aprobación íntegra, durante el desarrollo sólo se ejecutarán pruebas enfocadas. Una sola vez al final se ejecutarán restore locked autorizado, build Release, suite local completa sin integración PostgreSQL, format, pruebas de arquitectura/API/seguridad, vulnerabilidades NuGet, espejo y protección de `Fuentes/` y `git diff --check`. No aplican gates de migración ni comprobación especial de modelo EF porque esta adenda prohíbe cambios de persistencia.

Cuando los gates locales estén listos se solicitará una única corrida PostgreSQL externa consolidada. Su resultado se incorporará a la evidencia de entrega, no a esta propuesta previa.

`docs/traceability/IMPLEMENTATION_STATUS.md` se actualizará únicamente en el cambio de implementación aprobado, como `Propuesta implementada`, nunca como `Terminada`. Presentar esta adenda no exige alterar ese registro porque todavía no existe implementación ni cierre que registrar.

`HU-029` sólo será `Terminada` después de commit exacto, pipeline requerido exitoso sobre ese commit, aprobación humana, merge, ascendencia verificada en `origin/master`, PostgreSQL externo satisfactorio, protección de `Fuentes/` y cero defectos bloqueantes. Ninguna condición autoriza automáticamente la siguiente acción Git.

## 18. Preguntas y decisiones solicitadas

La aprobación íntegra responderá conjuntamente:

1. que la ruta única es `GET /api/v1/indicators` y exige `isoYear` e `isoWeek`;
2. que sólo admite además `level`, `responsiblePersonId`, `cursor` y `limit` con las reglas cerradas anteriores;
3. que el período es la semana ISO solicitada en `America/Mexico_City` y no se materializa durante la lectura;
4. que el universo usa obligaciones del período con asignación y responsable vigentes al único `queriedAt`;
5. que `RN-025` prevalece para esta lectura: Administración y Subcoordinación agregan propia e inferiores y Piso consulta sólo propia;
6. que Dirección puede usar `HU-029` sólo sobre obligaciones asignadas propias e inferiores, mientras obligación sin asignación y vista integral quedan en `HU-032`;
7. que filtros fuera de alcance producen resultado vacío sin confirmar existencia;
8. que `pending` significa exclusivamente `executionStatus = PENDIENTE` y no la bandeja de validaciones de `HU-031`;
9. que `concluded` significa exclusivamente `executionStatus = CONCLUIDA`;
10. que `validated` exige una decisión `VIGENTE` de cualquiera de los tres resultados permitidos;
11. que `nonCompliant` exige exclusivamente decisión `VIGENTE` `NO_CUMPLIDA` y nunca se deriva de vencimiento;
12. que los cuatro conteos usan `baseObligationsCount` como denominador;
13. que carga activa reutiliza `HU-004`, se restringe al período, incluye personas visibles con cero y usa `pending.count` como denominador;
14. que la suma completa de carga por persona debe conciliar exactamente con pendientes;
15. que política nula, requisito ausente o pendiente y ausencia de decisión vigente no cuentan como validadas o incumplidas y no se materializan;
16. que sólo asignación y decisión vigentes cuentan, sin duplicar versiones históricas;
17. que la colección por persona usa identidad minimizada, orden estable y paginación por cursor;
18. que todos los números son enteros no negativos `Int64` y no existen porcentajes ni montos;
19. que la consulta usa PostgreSQL `REPEATABLE READ, READ ONLY`, `AsNoTracking`, sin ETag, cache ni efectos laterales;
20. que no se crea auditoría funcional por lectura, persistencia, migración, vista SQL, índice o proyección materializada; y
21. que `HU-031`, `HU-032` y `HU-033` conservan íntegramente las fronteras de la sección 6.

Si cualquiera de estas decisiones no es aceptable, debe corregirse esta propuesta antes de producir código. No se implementará una interpretación parcial o implícita.
