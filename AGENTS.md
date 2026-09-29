# Instrucciones de implementación de SGOL

## Fuente de verdad y alcance

Trabaja sólo en la tarea o historia indicada. Antes de editar, lee su fila en `F07_BACKLOG_DE_IMPLEMENTACION.md`, los criterios y pruebas relacionados de F05 y las decisiones técnicas de F06. Durante el desarrollo, el repositorio y los documentos aprobados son la fuente oficial.

Sigue el documento "SGOL — Plan maestro de ejecución con ChatGPT y Codex" para alcance funcional, contratos y orden de dependencias. Los hitos administrativos basados exclusivamente en PR, pipeline, aprobación o merge no impiden implementar localmente la siguiente tarea ni preparar el frontend; no adelantes funcionalidad cuyo contrato o dependencia de código aún no exista.

No implementes funciones posteriores, opcionales o fuera de alcance. El MVP se limita a 35 capacidades y a TAR-0005, TAR-0007, TAR-0008, TAR-0011, TAR-0018, TAR-0026, TAR-0092 y TAR-0093 para `LOR-001`.

Si una instrucción contradice un entregable aprobado, no elijas silenciosamente: detén esa parte, cita la contradicción y solicita decisión. Distingue entre hecho documentado, inferencia, propuesta, pregunta y decisión aprobada, y conserva literalmente los identificadores estables.

La implementación cotidiana usa el flujo local-first definido en este archivo. Esta decisión operativa del usuario prevalece sobre requisitos anteriores de proceso que obliguen a usar PR, pipeline, aprobación o merge por cada tarea; no modifica contratos funcionales, seguridad ni arquitectura. `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md` se conserva como referencia del cierre formal histórico y sólo se aplica cuando el usuario solicite publicar o integrar un hito en GitHub. Su pipeline, PR y merge no bloquean la implementación local de tareas posteriores ni el inicio del frontend. Conserva sin cambios el documento histórico `F07_DEFINICIONES_DE_CONTROL.md` y su copia en `Fuentes/`.

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
- El equipo disponible es Windows AMD64 con Docker Desktop y motor Linux AMD64. Si una imagen, herramienta o prueba carece de soporte AMD64, regístrala como `VALIDACION_DIFERIDA_POR_ARQUITECTURA` y continúa con el trabajo que pueda verificarse localmente.
- Ejecuta `dotnet restore --locked-mode` sólo cuando sea necesario por cambios de dependencias, archivos lock o ausencia de artefactos restaurados. Un fallo atribuible a red, aislamiento o arquitectura se reporta; no bloquea cambios que puedan compilarse y probarse con los artefactos disponibles.
- Docker Desktop, Testcontainers y PostgreSQL real están disponibles en este equipo AMD64. Ejecuta las pruebas enfocadas directamente cuando sean proporcionales al cambio; reserva las suites integrales y costosas para un hito de integración. No solicites al usuario su ejecución después de cada endpoint.
- En migraciones EF, elimina siempre el BOM del archivo generado y sustituye el cuerpo de `Down()` por una excepción de reversión bloqueada. Es una regla fija; no la redescubras ni la consultes.
- Durante el desarrollo usa compilación y `dotnet test --filter` sobre las pruebas nuevas o directamente afectadas. La suite completa, `dotnet format --verify-no-changes` y las validaciones costosas se reservan para hitos de integración solicitados por el usuario.

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
2. El orden efectivo es `F07_BACKLOG_DE_IMPLEMENTACION.md` más las adendas F07 vigentes. Antes de comenzar cualquier tarea, verifica en `Tareas insertadas por adenda` si existe una dependencia funcional o técnica de código pendiente que deba ejecutarse antes. Detente sólo por una dependencia real de contrato o implementación; una publicación, pipeline, prueba AMD64 o cierre formal pendiente no bloquea iniciar la siguiente tarea local.
3. Ejecuta `scripts/ci/preflight.ps1` como única comprobación inicial y no diagnostiques el entorno más allá de su salida.
4. Si el checkout contiene el último commit aceptado y no contradice el estado registrado, acepta como evidencia previa las tareas ya Terminadas; no repitas sus análisis ni sus gates antes de editar.
5. Lee sólo la fila de la tarea actual, las secciones expresamente autorizadas en su prompt y los archivos directamente afectados.
6. No vuelvas a analizar íntegramente F00–F07 ni reconstruyas decisiones aprobadas salvo que exista una contradicción, un cambio de hash, una dependencia incompleta o una diferencia respecto al estado registrado.
7. Limita el informe previo a alcance, dependencias, archivos previstos y validación local prevista, en un máximo de ocho puntos; después comienza la implementación.
8. Ejecuta al finalizar únicamente las comprobaciones locales, rápidas y compatibles con el entorno Windows AMD64/Docker Linux AMD64 que sean proporcionales al cambio. Una validación remota, incompatible o diferida no impide marcar la tarea como `Implementada localmente` ni comenzar una dependencia local; repórtala sin presentarla como aprobada.

## Forma de trabajo

1. Inspecciona el estado del repositorio y no reviertas cambios ajenos.
2. Resume alcance, fuentes autorizadas, dependencias y criterios antes de editar.
3. Implementa el cambio mínimo que produzca el resultado pedido.
4. Añade o actualiza pruebas positivas, negativas, de autorización, auditoría y no-efecto que correspondan. Ejecuta de inmediato sólo las enfocadas y compatibles con el entorno.
5. Actualiza trazabilidad y documentación afectadas en el mismo cambio.
6. Ejecuta la validación local proporcional y reporta comandos, resultados y límites no comprobados. No conviertas una limitación específica del entorno en un bloqueo general.

## Trabajo local y publicación por hitos

1. El modo predeterminado es local. Implementa y valida varios endpoints sin crear un PR, ejecutar un pipeline ni publicar cada versión en GitHub.
2. La autorización para implementar una tarea permite editar archivos y, si ayuda a conservar puntos recuperables, crear commits locales coherentes. No pidas una autorización adicional para cada commit local.
3. No hagas `push`, abras PR ni inicies checks remotos por defecto, y no interrumpas cada tarea para preguntar si debe publicarse. Acumula cambios coherentes hasta que el usuario solicite publicar un hito.
4. Cuando el usuario ordene publicar un hito, esa orden autoriza preparar la rama, los commits necesarios, el `push` y el PR de ese hito sin confirmaciones intermedias. El merge o despliegue sólo queda autorizado si la misma orden lo incluye expresamente.
5. `Implementada localmente` significa que el código y la trazabilidad existen, el alcance está cubierto y pasaron las comprobaciones locales enfocadas disponibles. Este estado habilita continuar con endpoints dependientes y con el frontend.
6. `Validación diferida` identifica pruebas no ejecutables por incompatibilidad de arquitectura, ausencia temporal de Docker/Testcontainers, servicios externos o costo acumulado. No equivale a éxito, pero tampoco bloquea el avance local.
7. `Publicada` o `Integrada` se usa sólo cuando exista evidencia real de publicación o incorporación. El estado formal `Terminada` de F07 puede reservarse para un hito posterior sin invalidar el avance local registrado.

No inventes campos, estados, permisos, endpoints o reglas. Una ausencia documental no autoriza una decisión. Las correcciones de defectos no amplían alcance.

Trabaja en entregables pequeños, verificables y trazables. Un gate remoto pendiente o una prueba incompatible con el entorno local no es por sí solo un bloqueo crítico. No declares publicada o integrada una fase sin evidencia real; para el avance cotidiano usa `Implementada localmente` y enumera las validaciones diferidas. Responde en español.

## Convenciones

- IDs de backlog, HU, CAP, CA, CP, CAT, NFR, ADR y RN se conservan literalmente.
- Código y namespaces en inglés; nombres funcionales estables permanecen en contratos y trazabilidad.
- Dependencias NuGet centralizadas y fijadas. No agregues una dependencia sin justificar su necesidad y revisar licencia/vulnerabilidades.
- Migraciones compatibles hacia adelante; ninguna migración destructiva en MVP.
- API: JSON `camelCase`, `application/problem+json`, `correlationId`, paginación por cursor, `Idempotency-Key` e `If-Match` donde F06 lo exige.
- Datos y archivos de prueba son sintéticos; nunca copies evidencia real a Git o CI.

## Validación local proporcional

Para cada cambio ejecuta sólo lo necesario para detectar regresiones inmediatas en el código afectado. La combinación habitual es:

```text
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release --filter <pruebas afectadas>
git diff --check
```

- Si el cambio no permite `--no-restore`, ejecuta el restore necesario una sola vez; no repitas restore, build o pruebas sin una razón concreta.
- Prioriza pruebas de contrato, autorización, auditoría y no-efecto directamente relacionadas con el endpoint modificado.
- No ejecutes por tarea la suite completa, Testcontainers/PostgreSQL, navegador, escáneres, comprobaciones de toda la solución ni pipelines remotos. Agrúpalos en un hito cuando el usuario lo solicite y exista un entorno compatible.
- Una comprobación omitida o incompatible se registra con su causa exacta. No la presentes como aprobada y no solicites repetidamente al usuario que la ejecute.
- Un fallo de compilación o de una prueba enfocada compatible sí bloquea marcar el cambio como `Implementada localmente`; una prueba incompatible con el entorno local no.

## Entrega

La respuesta final debe indicar: resultado concreto, archivos cambiados, criterios satisfechos, comprobaciones locales ejecutadas con resultado, trazabilidad actualizada y validaciones diferidas con su causa. No pidas publicar ni ejecutar gates remotos al cerrar cada tarea. Usa `Implementada localmente`, `Validación diferida`, `Publicada` e `Integrada` con el significado definido arriba, sin confundirlos entre sí.

Al cerrar una fase documental, genera sus documentos finales en Markdown fuera de `Fuentes/`, solicita su aprobación e incorporación conforme al plan y entrega el nombre exacto del siguiente chat con un mensaje listo para copiar y pegar.
