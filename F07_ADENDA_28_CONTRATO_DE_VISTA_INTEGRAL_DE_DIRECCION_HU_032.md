# SGOL — Adenda 28 a F07: contrato de vista integral de Dirección de HU-032

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Propuesta de adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE; IMPLEMENTACIÓN EN CURSO` |
| Fecha | 2026-09-11 |
| Historia | `HU-032 — Dirección consulta toda LOR-001` |
| Corte y épica | `CV-05` / `EP-09` |
| Criterios | `CA-032`, `CP-032-P`, `CP-032-N`, `RN-025`, `RN-026`, `RN-029` |
| Base aceptada | `TECH-E2E-CV-03`, `HU-022`, `HU-023`, `HU-027`, `HU-028`, `HU-029` y `HU-031` terminadas efectivamente conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md` |
| Efecto propuesto | Precisar exclusivamente la lectura integral derivada de Dirección para `LOR-001` |
| Conservación | Mantiene sin cambios F00–F07 aprobados, `Fuentes/`, los contratos de `HU-029` y `HU-031`, estados, políticas, decisiones, asignaciones y persistencia existentes |
| Aprobación | Aprobada íntegramente por el responsable el 2026-09-11 antes de producir código |

La aprobación íntegra de esta adenda autorizará después la implementación local mínima de `HU-032`. No autoriza commit, push, publicación de rama, pull request, aprobación ni merge.

## 2. Precedencia comprobada

1. `origin/master` se verificó exactamente en `f3d078b27dc665ea0851b9545e99f0f59b5750fc`.
2. El commit implementado de `HU-029`, `0d669d3f17245ee796d01885637cfac7536ea702`, y su merge son ancestros de `origin/master`.
3. `HU-029` consta merged en el PR `#50`, con pipeline requerido `SUCCESS` run `34644972538`, aprobación humana y corrida PostgreSQL externa consolidada `203 tests passed, 0 warnings`.
4. La tabla `Tareas insertadas por adenda` no contiene una tarea pendiente que bloquee `HU-032`.
5. El backlog vigente coloca `HU-032` inmediatamente después de `HU-029`, depende de `HU-029` y `HU-031`, y reserva `HU-033`, `HU-034` y `HU-035` para pasos posteriores.
6. `F07_ADENDA_28` era el siguiente identificador libre.
7. La rama local `codex/hu-032` se creó desde el `origin/master` verificado sin incorporar ni modificar `.artifacts/` u otros cambios ajenos.

La propuesta interpreta el registro de `HU-029` en `docs/traceability/IMPLEMENTATION_STATUS.md` conforme a la eficacia posterior al merge: el texto que viajó en el PR conserva el estado de propuesta, pero las evidencias anteriores hacen efectivo su cierre sin un commit administrativo adicional.

## 3. Fuentes y alcance interpretativo

Esta propuesta se limita a `HU-032`, `CAP-039`, `CAP-043`, `CAP-044`, `CA-032`, `CP-032-P`, `CP-032-N`, `RN-025`, `RN-026`, `RN-029`, `PER-DIRECCION-VER`, la matriz de permisos aprobada, las secciones directamente relacionadas de F06 y los contratos aprobados por las Adendas 15, 21, 24, 25, 26 y 27.

Los contratos de consulta, estados de ejecución, política congelada, decisión vigente, jerarquía, indicadores, cursores y snapshot PostgreSQL se reutilizan por referencia normativa. Esta adenda no amplía ni modifica sus operaciones de escritura.

## 4. Hechos documentales

1. `HU-032` exige que Dirección consulte toda `LOR-001` y los cinco indicadores básicos.
2. `CA-032` exige toda la operación y sólo los cinco indicadores aprobados; `CP-032-P` exige conciliación con datos base de todos los roles.
3. `CP-032-N` prohíbe indicadores de incentivo, nómina o salud de integraciones.
4. `RN-025` permite a Dirección consultar todo y la matriz reserva `PER-DIRECCION-VER` exclusivamente a Dirección, incluidos todos los niveles.
5. `RN-026` cierra los cinco indicadores en pendientes, concluidas, validadas, incumplidas y carga activa por persona, sin KPI monetario.
6. `RN-029` excluye integración, mensajería externa, migración histórica, incentivo y movimiento de nómina del MVP.
7. `HU-029` cerró los significados de los cinco indicadores, pero limita su universo a obligaciones con asignación `VIGENTE` y deja las obligaciones sin asignación para `HU-032`.
8. `HU-031` cerró las colecciones de supervisión y validaciones pendientes sólo para niveles estrictamente inferiores y reservó la vista integral para `HU-032`.
9. F06 usa el prefijo común `/api/v1` y nombra conceptualmente `GET /direction/overview`, con permiso `PER-DIRECCION-VER`, `LOR-001` y cinco indicadores.
10. Los indicadores son valores derivados; ninguna fuente autorizada exige medición KPI persistida, exportación, consulta general de auditoría o interfaz para `HU-032`.

## 5. Contradicciones y carencias comprobadas

### 5.1 Toda `LOR-001` frente al universo asignado de `HU-029`

`HU-029` excluye expresamente obligaciones sin asignación vigente. `HU-032` exige toda `LOR-001`, pero no decide si esas obligaciones entran en los cuatro conteos, si se inventa una persona sintética para carga o si se mantienen fuera. Copiar el universo de `HU-029` incumpliría el sentido integral de `HU-032`; inventar una sexta métrica o una persona inexistente violaría `RN-026` y la fuente de datos.

### 5.2 Contenido de la vista

“Vista completa de operación” y “tablero” no fijan si la respuesta contiene obligaciones, personas, niveles, planes, evidencias, validaciones, auditoría o sólo agregados. Tampoco determinan UI. `CAP-044` contiene antecedentes más amplios de KPI, auditoría y exportaciones que contradicen el recorte explícito de `HU-032` y `CP-032-N` si se trasladan íntegramente al MVP.

### 5.3 Contrato HTTP y consistencia

La mención conceptual no fija la ruta versionada, query exacta, obligatoriedad y forma del período, DTO, tratamiento de parámetros, filtros, tipos numéricos, paginación, instante de corte, alcance temporal de identidades, aislamiento, cache, ETag, errores, telemetría, persistencia ni objetivo de rendimiento.

### 5.4 Inferencias que no autorizan código

La similitud con `GET /api/v1/indicators`, `GET /api/v1/supervision/obligations` o `GET /api/v1/validations/pending` permite reutilización técnica, pero no autoriza copiar silenciosamente su universo, permiso o colecciones. “Tablero” no autoriza UI. “Vista completa” no autoriza auditoría, exportaciones, continuidad, montos ni una segunda bandeja de supervisión.

## 6. Propuesta contractual: resultado cerrado y fronteras

### 6.1 Incluido

1. Una única lectura `GET /api/v1/direction/overview` calculada bajo demanda.
2. Exactamente los mismos cuatro conteos agregados y la misma distribución de carga activa por persona de `HU-029`, con universo integral de Dirección definido en esta adenda.
3. Período semanal ISO obligatorio, los cuatro niveles canónicos, filtros cerrados, denominadores, orden, paginación de personas, snapshot, autorización, anti-IDOR, cache, errores y pruebas.
4. Reutilización de `Sgol.Reporting.Contracts`, de `IIndicatorReader`, de los tipos de indicador aprobados, de la expresión jerárquica y del adaptador PostgreSQL read-only existentes. No se crea un modelo alternativo de los cinco indicadores.
5. Trazabilidad mínima de `HU-032` únicamente después de la aprobación y en el mismo cambio de implementación.

### 6.2 Excluido

- cualquier colección de obligaciones, planes, evidencias, requisitos, decisiones, validaciones pendientes, avisos, eventos de auditoría o recuperaciones;
- una segunda bandeja de supervisión o cambio a las rutas y DTO de `HU-031`;
- consulta general o detalle de auditoría de `HU-033`;
- idempotencia integral, reintentos o registro de conflictos de `HU-034`;
- continuidad, recuperación, conciliación o reconciliador de `HU-035`;
- monto, incentivo, nómina, rentabilidad, porcentaje financiero, salud de integraciones y cualquier sexto indicador;
- exportaciones, descargas o reportes de archivo;
- UI, Razor Pages, vistas, componentes, CSS, JavaScript y Playwright;
- mutación de ejecución, resultado, evidencia, asignación, requisito, política, decisión o historia;
- materialización de requisito, recálculo de política congelada, reapertura, sustitución o tarea correctiva;
- auditoría funcional de lecturas, avisos, mensajería, outbox, Worker, scheduler, broker, Redis, microservicio o integración externa; y
- tabla, columna, vista SQL, vista materializada, proyección persistida, índice, migración o backfill.

No se añade UI porque ninguna fuente aprobada fija una superficie visual para `HU-032`.

## 7. Ruta, query exacta y período

La única ruta es:

```http
GET /api/v1/direction/overview
```

Acepta exclusivamente:

| Parámetro | Regla |
|---|---|
| `isoYear` | Obligatorio; entero decimal de `1` a `9999`. |
| `isoWeek` | Obligatorio; entero decimal que identifica una semana ISO válida dentro de `isoYear`. |
| `level` | Opcional; uno de `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION` o `PISO_VENTAS`, con capitalización exacta. |
| `responsiblePersonId` | Opcional; UUID canónico `D` minúsculo, distinto de vacío; coincide sólo con la persona de la asignación `VIGENTE`. |
| `cursor` | Opcional; cursor opaco emitido por esta ruta para el mismo actor, período, filtros y alcance. Pagina sólo `activeLoadByPerson.items`. |
| `limit` | Opcional; entero decimal de `1` a `100`; predeterminado `25`. Pagina sólo `activeLoadByPerson.items`. |

Cada parámetro aparece como máximo una vez. Todos los filtros presentes se combinan con `AND` después de incorporar autorización y sucursal al mismo árbol SQL. Parámetro obligatorio ausente, desconocido, repetido, vacío, incompleto o inválido devuelve `400 FILTRO_DIRECCION_INVALIDO`.

Un `level`, `responsiblePersonId` o período válidos sin coincidencias devuelve `200` con los cuatro conteos y denominadores en cero y una colección de carga vacía. Un identificador de persona inexistente, fuera de `LOR-001` o no visible se normaliza a `scope.responsiblePersonId = null` y no confirma existencia.

El período es la semana ISO lunes–domingo de `isoYear` e `isoWeek` en `America/Mexico_City`. La respuesta deriva `startsOn` y `endsOn` sin crear `week_period`. Una obligación pertenece al período sólo por su relación persistida con el `week_period` de `LOR-001` cuyos `iso_year` e `iso_week` coinciden; no se reasigna por `dueAt`, `concludedAt`, `decidedAt` ni por el instante de consulta. Una semana válida sin fila o sin obligaciones devuelve ceros.

No existen filtros por sucursal, estado de ejecución, resultado de validación, TAR, plan, evidencia, auditoría, fecha arbitraria, monto u otro criterio. `branchCode` es siempre `LOR-001` y no es entrada del cliente.

## 8. Instante único, actor exclusivo y alcance actual

Cada petición inicia una transacción de lectura y captura exactamente una vez `queriedAt = IClock.UtcNow`. Cuenta, empleo, rol, asignación, persona responsable, decisión vigente, filtros, conteos, página y `meta.queriedAt` se resuelven en ese mismo snapshot.

Se exige sesión individual y MFA completos, cuenta y persona activas, empleo activo y vigente en `LOR-001`, exactamente un rol canónico activo y vigente `DIRECCION` y `PER-DIRECCION-VER`. Permiso aislado, otro rol, puesto textual “Dirección”, rol sustituido, cuenta o empleo obsoletos, sucursal distinta, multiplicidad o ausencia producen `403 ACCESO_DENEGADO` antes de resolver datos.

Dirección ve todas las personas y obligaciones de `LOR-001` de los niveles `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION` y `PISO_VENTAS`, incluidas las propias, conforme al universo de la sección 9. No se aplica la jerarquía estrictamente inferior de `HU-031` ni la restricción de persona propia de `HU-029`.

`level` restringe el universo a obligaciones cuya asignación vigente pertenece a una persona de ese rol actual. `responsiblePersonId` restringe a una persona actual de `LOR-001`. Los filtros pueden combinarse y nunca amplían alcance. Una obligación sin asignación vigente pertenece a la vista integral sin filtros de nivel/persona, pero no coincide con esos filtros porque no existe una identidad actual que los satisfaga.

Un cambio confirmado de responsable, rol, empleo o cuenta afecta peticiones posteriores. Esta lectura es operativa actual y no reconstruye responsabilidad histórica ni congela alcance al cierre del período.

## 9. Universo base, asignación e identidad

Sin filtros de `level` o `responsiblePersonId`, el universo base contiene los IDs distintos de `work_obligation` que:

1. pertenecen a `LOR-001`; y
2. pertenecen al período exacto de la sección 7.

No se exige asignación vigente para pertenecer a ese universo. Las obligaciones sin asignación vigente se incluyen en `baseObligationsCount`, `pending`, `concluded`, `validated` y `nonCompliant` según sus hechos propios. No se crea una persona, nivel, fila de carga ni indicador sintético para ellas.

Cuando existe `level` o `responsiblePersonId`, el universo se restringe a obligaciones con exactamente una asignación `VIGENTE` que coincida con todos los filtros. Las obligaciones sin asignación quedan fuera del subconjunto filtrado. Los conteos y denominadores se recalculan sobre ese subconjunto; los filtros no son una decoración aplicada después de calcular un snapshot global.

Una obligación con más de una asignación vigente es inconsistente. Una obligación con una asignación vigente cuyo responsable no tiene exactamente una persona, cuenta individual activa, empleo vigente y rol canónico vigente en `LOR-001` también es inconsistente. La consulta falla completa con `500 CONSULTA_DIRECCION_INCONSISTENTE`; no oculta la obligación ni la interpreta como no asignada.

Las personas de carga son todas las personas activas de `LOR-001` que tienen exactamente una cuenta individual activa, empleo vigente y un rol canónico vigente, restringidas por `level` y `responsiblePersonId` cuando se proporcionan. Incluyen todos los niveles, otras personas Dirección y al actor. Cada persona visible aparece con carga cero aunque no tenga obligación del período.

Puesto textual, turno, nombre, responsabilidad histórica, asignación sustituida, autor de conclusión, validador, creador o datos del cliente no conceden autoridad ni clasifican nivel o responsabilidad vigente.

## 10. Reglas comunes y definición de los cinco indicadores

`baseObligationsCount` es el número entero no negativo de IDs distintos de obligación del universo de la sección 9. Cada obligación cuenta como máximo una vez en cada indicador aplicable. Una reasignación `SUSTITUIDA`, una responsabilidad histórica y una decisión `SUSTITUIDA` nunca agregan otra unidad.

### 10.1 `pending`

`pending.count` cuenta obligaciones del universo con `executionStatus = PENDIENTE`. No exige vencimiento, evidencia faltante, política de validación, requisito ni autoridad para validar. No representa validaciones pendientes de `HU-031`.

`pending.denominator = baseObligationsCount`.

### 10.2 `concluded`

`concluded.count` cuenta obligaciones del universo con `executionStatus = CONCLUIDA`. Depende exclusivamente del estado de ejecución persistido.

`concluded.denominator = baseObligationsCount` y `pending.count + concluded.count = baseObligationsCount`.

### 10.3 `validated`

`validated.count` cuenta obligaciones distintas del universo con exactamente una decisión `VIGENTE`, cualquiera de `CUMPLIDA`, `INCOMPLETA` o `NO_CUMPLIDA`.

`validated.denominator = baseObligationsCount`. Política congelada nula, requisito no materializado, requisito `PENDIENTE` y ausencia de decisión vigente cuentan cero. La lectura no materializa requisito ni busca una política posterior.

### 10.4 `nonCompliant`

`nonCompliant.count` cuenta obligaciones distintas del universo cuya única decisión `VIGENTE` tiene `result = NO_CUMPLIDA`. Es subconjunto de `validated` y no depende de `dueAt`, condición vencida, retraso, evidencia faltante ni ejecución pendiente.

`nonCompliant.denominator = baseObligationsCount`. Una obligación vencida sin decisión vigente `NO_CUMPLIDA` cuenta cero. Una decisión sustituida cuenta cero si la vigente tiene otro resultado.

### 10.5 `activeLoadByPerson`

Cada elemento cuenta obligaciones distintas del universo con `executionStatus = PENDIENTE` y exactamente una asignación `VIGENTE` a esa persona. Reutiliza la definición de carga activa de `HU-004` y los tipos aprobados por `HU-029`, restringidos al período y filtros de esta adenda.

`activeLoadByPerson.denominator = pending.count`, incluidas las pendientes sin asignación. La suma de `count` de la colección completa es menor o igual al denominador. La diferencia corresponde exactamente a obligaciones pendientes sin asignación vigente; no se expone como sexto indicador ni como fila ficticia. Si no hay pendientes, el denominador y todas las cargas son cero.

Una reasignación mueve la obligación sólo al responsable vigente en peticiones posteriores. Una asignación sustituida no cuenta.

### 10.6 Inconsistencias persistidas

Más de una asignación vigente, responsabilidad vigente no resoluble, estado de ejecución desconocido, más de una decisión vigente, decisión vigente sin cadena coherente, requisito `RESUELTA` sin decisión vigente, historia sólo `SUSTITUIDA` sin sucesora vigente o política/requisito/decisión incompatibles producen `500 CONSULTA_DIRECCION_INCONSISTENTE`. La respuesta falla completa; no presenta datos parciales ni interpreta anomalías como cero.

## 11. Envelope y DTO exacto

La respuesta reutiliza íntegramente la forma JSON y los tipos de indicador de `HU-029`; sólo cambian la ruta, el permiso y el universo definidos por esta adenda. Una respuesta exitosa contiene exactamente:

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
      "actorRole": "DIRECCION",
      "includedLevels": ["DIRECCION", "ADMINISTRACION", "SUBCOORDINACION", "PISO_VENTAS"],
      "level": null,
      "responsiblePersonId": null
    },
    "baseObligationsCount": 9,
    "pending": { "count": 4, "denominator": 9 },
    "concluded": { "count": 5, "denominator": 9 },
    "validated": { "count": 4, "denominator": 9 },
    "nonCompliant": { "count": 1, "denominator": 9 },
    "activeLoadByPerson": {
      "denominator": 4,
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

No existen otras propiedades. La respuesta no contiene obligaciones, planes, evidencias, requisitos, validaciones, auditoría ni continuidad. Todos los conteos y denominadores son enteros JSON no negativos respaldados por `Int64`; no se devuelven decimal, porcentaje, tasa, unidad monetaria ni valor nulo.

`scope.includedLevels` muestra el alcance efectivo después de aplicar `level`, en orden `DIRECCION`, `ADMINISTRACION`, `SUBCOORDINACION`, `PISO_VENTAS`; con filtro contiene sólo el nivel solicitado. `scope.responsiblePersonId` devuelve el UUID solicitado sólo cuando la persona actual existe en `LOR-001` y satisface `level` cuando aplica.

`meta.count` es exclusivamente la cantidad de elementos de `activeLoadByPerson.items` en la página. Los cuatro conteos, `baseObligationsCount` y `activeLoadByPerson.denominator` representan todo el universo después de filtros, no sólo la página.

Nombres se serializan en `camelCase`; UUID en formato canónico `D` minúsculo; fechas como `YYYY-MM-DD`; `queriedAt` como RFC 3339 UTC con sufijo `Z`. La identidad personal se minimiza a `id`, `stableCode` y `displayName`; no se exponen cuenta, puesto, turno, datos de contacto, cliente, evidencia, autor de decisiones ni identificadores de obligaciones o validaciones.

## 12. Orden y cursor de carga por persona

`activeLoadByPerson.items` se ordena por `person.stableCode` ordinal ascendente y después por `person.id` ascendente. El lector solicita `limit + 1` personas y devuelve como máximo `limit`.

El cursor es Base64URL opaco, versionado y ligado exclusivamente a `GET /api/v1/direction/overview`, al actor, al período, a los filtros normalizados y al alcance. Contiene las claves completas del último elemento y SHA-256 del contexto; no contiene nombre, monto, carga, PII adicional ni autoridad durable. No sirve en otra ruta ni como prueba de acceso.

Cada página es una petición nueva: reevalúa autorización y recalcula todos los conteos bajo su propio snapshot. Con datos y autoridad sin cambios, la paginación no duplica ni omite personas. Si cambia el alcance, un cursor anterior nunca lo amplía y puede devolver menos personas o `400 FILTRO_DIRECCION_INVALIDO` cuando ya no corresponde a filtros y contexto.

## 13. Snapshot, concurrencia, cache y ausencia de efectos

Cada petición usa una transacción PostgreSQL `REPEATABLE READ, READ ONLY`. Autoridad, personas, obligaciones asignadas y no asignadas, asignaciones, política congelada, requisito, decisión vigente, conteos, denominadores y página pertenecen al mismo snapshot. Todas las consultas de entidad usan `AsNoTracking` y aplican sucursal y filtros en SQL antes de materializar.

Una conclusión, creación o sustitución de asignación, cambio de cuenta/empleo/rol o sustitución de decisión concurrente queda enteramente antes o después del snapshot. Una respuesta nunca mezcla conteos anteriores con distribución posterior ni cuenta a la vez decisión vigente y sustituida. No se promete el mismo snapshot entre páginas.

Las respuestas envían `Cache-Control: private, no-store` y `Pragma: no-cache`. No se emite `ETag` de colección ni se acepta `If-Match` o `Idempotency-Key`.

El lector no llama `SaveChanges`; no crea período, requisito, decisión, snapshot, auditoría, idempotencia, aviso, outbox ni evento; no recalcula política; y no modifica ejecución, resultado, evidencia, asignación, ETag o `row_version`.

Una consulta exitosa o rechazada no inserta `audit_event`. Los rechazos generan únicamente telemetría técnica sanitizada con instante, ruta, status, código, latencia y `correlationId`; no registra nombres, identificadores de persona, filtros sensibles, conteos ni contenido de negocio. `HU-033` no se adelanta.

## 14. Errores y convergencia anti-IDOR

Todos los errores usan `application/problem+json` con `status`, `code`, `title`, `instance` y `correlationId`, detalle público genérico y sin datos sensibles.

| HTTP | `code` | Uso |
|---:|---|---|
| 400 | `FILTRO_DIRECCION_INVALIDO` | Query ausente, desconocida, repetida, vacía, incompleta, inválida o cursor incompatible. |
| 401 | `AUTENTICACION_REQUERIDA` | Sesión ausente, expirada o MFA incompleto. |
| 403 | `ACCESO_DENEGADO` | Cuenta, persona, empleo, rol Dirección, sucursal o permiso inválidos. |
| 500 | `CONSULTA_DIRECCION_INCONSISTENTE` | Asignación, identidad, ejecución, política, requisito, decisión o conciliación persistidos de manera incoherente. |
| 500 | `ERROR_INTERNO` | Falla no controlada, sin detalle sensible ni efecto. |

Al ser una lectura agregada, no acepta identificador directo de obligación. Filtros válidos sin coincidencia devuelven conteos cero y colección vacía; no confirman persona, obligación, requisito o decisión. No existen vínculos de detalle en esta respuesta.

## 15. Persistencia, arquitectura y rendimiento

No se crea persistencia. `Sgol.Reporting.Contracts` conserva una sola familia de tipos para los cinco indicadores. `IIndicatorReader` se amplía sólo con la solicitud de vista integral necesaria para aplicar el universo de Dirección y devuelve el mismo `IndicatorResult`; no se crea otro lector o modelo propietario de indicadores.

El adaptador PostgreSQL read-only existente proyecta mediante contratos internos las tablas poseídas por otros módulos y no las escribe ni expone `DbContext`, entidades rastreadas o SQL libre al endpoint.

La consulta se calcula bajo demanda. Se prohíben N+1, carga de recursos fuera de `LOR-001`, enumeración de historia completa, consulta por persona repetida, cache persistente y materialización previa de todo el universo en memoria. El conteo agrupado y la página de personas se resuelven con un número fijo y acotado de consultas SQL dentro del mismo snapshot.

Se mantiene `NFR-002`: p95 menor a `2 s` en lectura con la carga objetivo del MVP. Las pruebas de rendimiento usan datos sintéticos y plan PostgreSQL. Si un plan demuestra que una vista SQL, tabla agregada o índice nuevo es indispensable, la implementación se detiene y solicita otra decisión documental; esta adenda no lo autoriza.

## 16. Interfaz

La palabra “tablero” de `CP-032-P` describe la conciliación funcional y no exige UI. `HU-032` se satisface con la API aprobada en esta adenda. No se leen ni modifican documentos de diseño porque no se autoriza vista, componente, marcado, estilo o comportamiento de navegador.

## 17. Pruebas obligatorias después de la aprobación

La implementación debe demostrar al menos:

1. sesión ausente o MFA incompleto produce `401` sin exposición ni efecto;
2. permiso aislado sin cuenta, persona, empleo, rol `DIRECCION` o sucursal produce `403`;
3. puesto textual “Dirección”, rol sustituido, cuenta o empleo obsoletos no conceden acceso;
4. Administración, Subcoordinación y Piso con `PER-DIRECCION-VER` no obtienen la vista;
5. otra sucursal no aparece en conteos, denominadores, identidades ni cursores;
6. Dirección ve los cuatro niveles canónicos, otras personas Dirección y recursos propios;
7. un conjunto sintético de los cuatro roles concilia exactamente los cinco indicadores con los datos base;
8. cada obligación cuenta como máximo una vez por indicador y `pending + concluded = baseObligationsCount`;
9. decisiones sustituidas no duplican `validated` o `nonCompliant`;
10. una obligación vencida sin decisión vigente `NO_CUMPLIDA` no entra en `nonCompliant`;
11. reasignaciones históricas no duplican carga y sólo la asignación vigente determina persona y nivel;
12. una obligación pendiente sin asignación entra en `baseObligationsCount` y `pending`, no crea fila de persona y explica la diferencia entre carga total y denominador;
13. una obligación concluida sin asignación entra en `baseObligationsCount` y `concluded`, y su decisión vigente cuenta conforme a las reglas cerradas;
14. responsabilidad vigente con cuenta, empleo o rol ausente o ambiguo falla completa y no se oculta;
15. los filtros de nivel/persona excluyen obligaciones sin asignación y recalculan todos los conteos y denominadores sin ampliar alcance;
16. período válido vacío devuelve el DTO exacto con ceros y personas visibles con carga cero; un filtro de persona sin coincidencia devuelve colección vacía;
17. parámetros ausentes, desconocidos, repetidos, vacíos, incompletos e inválidos producen el error exacto;
18. orden, desempate, límite, cursor, vínculo de filtros y reevaluación de alcance son deterministas;
19. política nula, requisito no materializado o pendiente y ausencia de decisión vigente cuentan cero sólo en validación, sin alterar ejecución o carga;
20. una conclusión, asignación o sustitución concurrente produce un snapshot anterior o posterior completo, nunca conteos incompatibles;
21. huellas y conteos antes/después prueban que el GET no crea período, requisito, decisión, snapshot, auditoría, idempotencia, aviso, outbox ni evento;
22. la lectura no cambia ejecución, asignación, evidencia, ETag o `row_version`;
23. no aparece monto, incentivo, nómina, rentabilidad, porcentaje financiero, exportación, salud de integraciones ni sexto indicador;
24. no aparece colección de supervisión, validaciones pendientes, consulta general de auditoría, conflicto idempotente, continuidad o reconciliación;
25. el envelope contiene únicamente las propiedades de la sección 11 y minimiza identidad personal;
26. arquitectura confirma `Sgol.Reporting.Contracts`, `IIndicatorReader`, lector read-only, autorización SQL, ausencia de persistencia y dependencias correctas; y
27. PostgreSQL real demuestra conciliación completa, `DISTINCT`, obligaciones sin asignación, anti-IDOR, filtros, paginación, snapshot concurrente, ausencia de N+1 y no filtración entre sucursales.

No aplican pruebas de UI, navegador, migración ni modelo EF porque esta propuesta prohíbe esos cambios.

## 18. Gates, trazabilidad y eficacia

Después de aprobación íntegra, durante el desarrollo sólo se ejecutarán pruebas enfocadas. Una sola vez al final se ejecutarán restore locked autorizado, build Release, suite local completa sin integración PostgreSQL, format, pruebas de arquitectura/API/seguridad, vulnerabilidades NuGet, espejo y protección de `Fuentes/` y `git diff --check`.

Cuando los gates locales estén listos se solicitará una única corrida PostgreSQL externa consolidada. Su resultado se incorporará a la evidencia de entrega, no a esta propuesta previa.

`docs/traceability/IMPLEMENTATION_STATUS.md` se actualizará únicamente en el cambio de implementación aprobado, como `Propuesta implementada`, nunca como `Terminada`. Presentar esta adenda no exige alterar ese registro porque todavía no existe implementación ni cierre que registrar.

`HU-032` sólo será `Terminada` después de commit exacto, pipeline requerido exitoso sobre ese commit, aprobación humana, merge, ascendencia verificada en `origin/master`, PostgreSQL externo satisfactorio, protección de `Fuentes/` y cero defectos bloqueantes. Ninguna condición autoriza automáticamente la siguiente acción Git.

## 19. Preguntas cerradas por la propuesta

La aprobación íntegra responderá conjuntamente:

1. que la ruta única es `GET /api/v1/direction/overview` y exige `isoYear` e `isoWeek`;
2. que sólo admite además `level`, `responsiblePersonId`, `cursor` y `limit` con las reglas cerradas anteriores;
3. que el período es la semana ISO solicitada en `America/Mexico_City` y no se materializa durante la lectura;
4. que el actor exclusivo requiere cuenta, empleo y rol `DIRECCION` vigentes, sucursal y `PER-DIRECCION-VER`;
5. que la vista incluye los cuatro niveles canónicos, otras personas Dirección, recursos propios y obligaciones sin asignación de `LOR-001`;
6. que sin filtros el universo base incluye toda obligación del período, tenga o no asignación vigente;
7. que una obligación sin asignación participa en los cuatro conteos según sus hechos, pero no crea persona, nivel, fila de carga ni sexto indicador;
8. que un filtro de nivel o persona produce un subconjunto nuevo con denominadores recalculados y excluye obligaciones sin asignación;
9. que una responsabilidad vigente con identidad ambigua o incompleta falla cerrada;
10. que se reutiliza exactamente la forma del DTO y los tipos de indicador de `HU-029`, sin colecciones adicionales;
11. que `pending`, `concluded`, `validated` y `nonCompliant` conservan íntegramente sus definiciones de `HU-029`;
12. que carga activa conserva su definición por asignación vigente, usa `pending.count` como denominador y puede sumar menos sólo por obligaciones pendientes sin asignación;
13. que política nula, requisito ausente o pendiente y ausencia de decisión vigente no se materializan y no cuentan como validadas o incumplidas;
14. que sólo asignación y decisión vigentes cuentan, sin duplicar versiones históricas;
15. que la colección por persona usa identidad minimizada, orden estable y paginación por cursor;
16. que cada petición usa PostgreSQL `REPEATABLE READ, READ ONLY`, un solo `queriedAt`, `AsNoTracking`, sin ETag, cache ni efectos laterales;
17. que la lectura se calcula bajo demanda y no crea tabla, vista SQL, índice, migración, proyección persistida ni auditoría funcional;
18. que “tablero” no exige UI;
19. que `HU-029` y `HU-031` permanecen sin cambios y no se crea una segunda consulta de supervisión; y
20. que `HU-033`, `HU-034` y `HU-035` conservan íntegramente auditoría general, idempotencia integral y continuidad.

## 20. Decisiones solicitadas y aprobación íntegra

Se solicita aprobar o rechazar la Adenda 28 como una unidad. No existe aprobación parcial implícita.

La pregunta de aprobación es: **¿se aprueba íntegramente `F07_ADENDA_28_CONTRATO_DE_VISTA_INTEGRAL_DE_DIRECCION_HU_032.md`, sin cambios, para autorizar la implementación local de `HU-032` bajo este contrato?**
