# Instrucciones de implementación de SGOL

## Fuente de verdad y alcance

Trabaja sólo en la tarea o historia indicada. Antes de editar, lee su fila en `F07_BACKLOG_DE_IMPLEMENTACION.md`, los criterios y pruebas relacionados de F05 y las decisiones técnicas de F06. Durante el desarrollo, el repositorio y los documentos aprobados son la fuente oficial.

Sigue el documento "SGOL — Plan maestro de ejecución con ChatGPT y Codex". Cada chat debe trabajar únicamente en la fase asignada. No adelantes fases ni generes código de producción antes de que el plan lo indique.

No implementes funciones posteriores, opcionales o fuera de alcance. El MVP se limita a 35 capacidades y a TAR-0005, TAR-0007, TAR-0008, TAR-0011, TAR-0018, TAR-0026, TAR-0092 y TAR-0093 para `LOR-001`.

Si una instrucción contradice un entregable aprobado, no elijas silenciosamente: detén esa parte, cita la contradicción y solicita decisión. Distingue entre hecho documentado, inferencia, propuesta, pregunta y decisión aprobada, y conserva literalmente los identificadores estables.

Para el cierre de tareas, aplica la enmienda específica `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md`. Conserva sin cambios el documento histórico `F07_DEFINICIONES_DE_CONTROL.md` y su copia en `Fuentes/`.

## Protección de `Fuentes`

`Fuentes/` es de solo lectura lógica. No renombres, muevas, sobrescribas, elimines ni generes archivos dentro de ella. No uses `Fuentes/` como salida de compilación, prueba, migración o ejecución. No abras ni ejecutes macros. La incorporación documental posterior a una aprobación no forma parte de una tarea de código ordinaria.

No busques, abras, generes ni descomprimas archivos ZIP; no cambies rutas ni modifiques originales. Ignora archivos temporales como los que comienzan con `~$` y no revivas anomalías de versiones anteriores ya corregidas.

## Localización de contexto

- Para localizar un identificador, consulta primero `docs/INDICE_IDS.md` y lee únicamente el rango de líneas indicado.
- Los documentos F00–F07 de la raíz son copias del contenido congelado y no se editan; toda modificación de su contenido se registra como adenda F07 en la raíz.
- No ejecutes búsquedas recursivas sobre `Fuentes/`, `bin/`, `obj/`, `.vs/` ni sobre archivos `.xlsx` o `.xlsm`. Los binarios de Excel no se abren ni se convierten.
- Durante el desarrollo lee las copias de la raíz; `Fuentes/` conserva el original congelado y `scripts/ci/verify-fuentes-mirror.ps1` comprueba su equivalencia.

## Construcción de interfaz

Antes de escribir cualquier vista, componente, hoja de estilo o marcado, lee `docs/design/tokens.md`, `docs/design/componentes.md`, `docs/design/estados-y-mensajes.md`, `docs/design/estados-de-dominio.md` y `docs/design/accesibilidad.md`; esta lectura es obligatoria, no opcional.
Toda propiedad visual se expresa mediante las variables CSS definidas en `tokens.md`.
Está prohibido escribir un color, tamaño de fuente, radio, sombra o valor de espaciado literal fuera de `docs/design` y de la hoja que declara las variables.
Si falta en `docs/design` un componente, estado o mensaje necesario para la historia, detente y presenta la carencia al responsable.
No inventes el componente, no improvises un color ni copies el estilo de otra pantalla.
`Fuentes/IdentidadMarca/` está congelado y nunca se abre: no leas PDF, `.ai`, `.psd` ni archivos de tipografía; `docs/design` es la única fuente operativa de diseño.
Toda pantalla implementa los estados de `componentes.md`: normal, foco, deshabilitado, error, cargando y vacío.
Una historia con UI no está completa si omite el estado vacío o el de error.

## Entorno conocido

- Ejecuta `scripts/ci/preflight.ps1` como única comprobación inicial. No diagnostiques el entorno más allá de su salida.
- Ejecuta `dotnet restore --locked-mode` con acceso autorizado fuera del aislamiento; no rediagnostiques la carga de la jerarquía de configuración de NuGet dentro del aislamiento.
- Docker y Testcontainers funcionan correctamente. El error `Acceso denegado` dentro del aislamiento no indica un problema de `PATH`, entorno ni instalación. No ejecutes `docker version`, no busques rutas, no modifiques variables de entorno y no interpretes un fallo de aislamiento como Docker ausente.
- Las pruebas de integración con PostgreSQL las ejecuta el desarrollador fuera de la sesión. Escribe las pruebas, pero no las ejecutes: solicita su resultado en un solo mensaje al llegar a los gates.
- En migraciones EF, elimina siempre el BOM del archivo generado y sustituye el cuerpo de `Down()` por una excepción de reversión bloqueada. Es una regla fija; no la redescubras ni la consultes.
- Ejecuta `dotnet format --verify-no-changes` y la suite completa una sola vez, al final. Durante el desarrollo usa `dotnet test --filter` sobre las pruebas nuevas.

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

## Inicio incremental de tareas

1. Lee primero `docs/traceability/IMPLEMENTATION_STATUS.md`.
2. El orden efectivo es `F07_BACKLOG_DE_IMPLEMENTACION.md` más las adendas F07 vigentes. Antes de comenzar cualquier tarea, verifica en `Tareas insertadas por adenda` que no exista una tarea pendiente que deba ejecutarse antes. Si existe, detente, indícala y no inicies la tarea del backlog.
3. Ejecuta `scripts/ci/preflight.ps1` como única comprobación inicial y no diagnostiques el entorno más allá de su salida.
4. Si el checkout contiene el último commit aceptado y no contradice el estado registrado, acepta como evidencia previa las tareas ya Terminadas; no repitas sus análisis ni sus gates antes de editar.
5. Lee sólo la fila de la tarea actual, las secciones expresamente autorizadas en su prompt y los archivos directamente afectados.
6. No vuelvas a analizar íntegramente F00–F07 ni reconstruyas decisiones aprobadas salvo que exista una contradicción, un cambio de hash, una dependencia incompleta o una diferencia respecto al estado registrado.
7. Limita el informe previo a alcance, dependencias, archivos previstos y plan de gates, en un máximo de ocho puntos; después comienza la implementación.
8. Ejecuta al finalizar todos los gates aplicables sobre el nuevo cambio. No uses esta optimización para omitir un gate de salida, una revisión humana ni una comprobación exigida expresamente por la tarea.

## Forma de trabajo

1. Inspecciona el estado del repositorio y no reviertas cambios ajenos.
2. Resume alcance, fuentes autorizadas, dependencias y criterios antes de editar.
3. Implementa el cambio mínimo que produzca el resultado pedido.
4. Añade o actualiza pruebas positivas, negativas, de autorización, auditoría y no-efecto que correspondan.
5. Actualiza trazabilidad y documentación afectadas en el mismo cambio.
6. Ejecuta los gates aplicables y reporta comandos, resultados y límites no comprobados.

## Cierre de tarea en un pull request

1. Incluye la implementación y la actualización de `docs/traceability/IMPLEMENTATION_STATUS.md` en el mismo commit y pull request.
2. En la rama del PR, el registro es una propuesta: no declara la tarea Terminada ni habilita dependencias.
3. Usa el commit que contiene la implementación y el registro, el PR y sus checks como evidencia primaria. Si el registro viaja en ese mismo commit, identifícalo como `commit que contiene esta actualización`; no intentes escribir un SHA autorreferencial. El hash de merge y el número de run son complementarios y no bloqueantes.
4. Requiere un pipeline válido sobre el commit exacto, una aprobación humana y un merge a `master`.
5. Sólo después del merge, y si el registro y el commit implementado pertenecen a la historia de `master`, la tarea queda Terminada.
6. No abras otro PR ni generes un commit automático únicamente para completar el hash de merge o el número de run.

No inventes campos, estados, permisos, endpoints o reglas. Una ausencia documental no autoriza una decisión. Las correcciones de defectos no amplían alcance.

Trabaja en entregables pequeños, verificables y trazables. No declares terminada una fase si faltan entregables o existen bloqueos críticos. Si la fase está bloqueada, enumera lo necesario para desbloquearla y no indiques que se puede avanzar. Responde en español.

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

Además ejecuta las pruebas específicas de arquitectura, API/contrato, seguridad o navegador exigidas por la historia. Un gate omitido debe quedar marcado como no verificado y con causa; no lo presentes como aprobado. Las pruebas de integración con PostgreSQL quedan a cargo del desarrollador fuera de la sesión, conforme a «Entorno conocido».

## Entrega

La respuesta final debe indicar: resultado concreto, archivos cambiados, criterios satisfechos, pruebas ejecutadas con resultado, trazabilidad actualizada, riesgos o pendientes y cualquier decisión humana necesaria. No declares la historia terminada si la Definición de Terminado de F07 y la condición de merge de `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md` no se cumplen.

Al cerrar una fase documental, genera sus documentos finales en Markdown fuera de `Fuentes/`, solicita su aprobación e incorporación conforme al plan y entrega el nombre exacto del siguiente chat con un mensaje listo para copiar y pegar.
