# SGOL — Instrucciones de ejecución de tareas de frontend

## 1. Propósito y alcance

Este documento define el flujo solicitado por el responsable para ejecutar las tareas `FRONT-*`: preparar y aprobar el plan, implementar, conservar un commit local, obtener autorización de publicación, mostrar las pantallas y acompañar el pipeline hasta poder solicitar aprobación de merge.

La orden de redacción es: «Quiero crear un documento md con las instrucciones de ejecucion para las tareas de front». El responsable aprobó el documento y su incorporación mediante «Apruebo el documento y su incorporacion», con la condición de esperar hasta después del merge de FRONT-015. Esa condición se cumplió al integrar el PR #82, merge `e6aab78f762d932a66e889d617dadb5c3edbe1e2`, tras «Apruebo hacer el merge». Documento aprobado e incorporado operativamente con referencia explícita en `AGENTS.md`, en un cambio local posterior. Esta incorporación no declara publicada o integrada en GitHub esta documentación ni autoriza por sí misma publicar otro hito o integrar otro PR.

Se aplica exclusivamente a tareas `FRONT-*`, incluidas sus lecturas, contratos y correcciones indispensables expresamente incluidos en el plan aprobado. No extiende este flujo al backlog de backend, infraestructura ni otras tareas técnicas independientes. No altera las capacidades del MVP, los contratos aprobados, la seguridad ni la arquitectura.

Complementa `AGENTS.md`. Conserva `Fuentes/` y los documentos congelados sin cambios. La implementación sigue siendo local hasta que el responsable autorice publicar. Un pipeline pendiente no bloquea por sí mismo una dependencia local ya habilitada.

## 2. Autorizaciones y puntos de espera

| Momento | Qué permite | Qué sigue pendiente |
|---|---|---|
| Solicitud de plan | Investigar el alcance y preparar una propuesta concreta | Implementación de decisiones nuevas |
| Aprobación del plan y orden de implementarlo | Incorporar las decisiones aprobadas donde corresponda, implementar, validar y crear commits locales coherentes | Publicación remota |
| Aprobación de publicación | Preparar la rama, publicar commits, abrir o actualizar el mismo PR y ejecutar sus checks. Incluye seguir el pipeline, corregir defectos del hito aprobado, validar, crear nuevos commits y subirlos al mismo PR | Merge y despliegue |
| Aprobación expresa de merge | Integrar la cabeza revisada del PR, después de comprobar sus requisitos y resultados vigentes | Despliegue, salvo autorización expresa adicional |

No pedir otra aprobación para cada corrección o cada push necesario para resolver el pipeline del hito ya autorizado. Esa autorización continúa vigente hasta terminar el seguimiento, salvo que el responsable la revoque o pause el trabajo.

Pedir decisión sólo si la solución cambia el alcance aprobado, contradice un contrato, exige una decisión funcional o de diseño ausente, o requiere una acción fuera de la autorización. Explicar la carencia concreta y continuar el trabajo independiente. No rebajar una validación para evitar pedir una decisión necesaria.

## 3. Preparar el plan

1. Leer primero `docs/traceability/IMPLEMENTATION_STATUS.md` y ejecutar `scripts/ci/preflight.ps1` como única comprobación inicial del entorno.
2. Identificar la tarea efectiva mediante el backlog y sus adendas. Verificar las tareas insertadas por adenda y únicamente las dependencias funcionales o de código reales.
3. Usar `docs/INDICE_IDS.md` para localizar la fila de la tarea, sus criterios y pruebas F05 y las decisiones F06 correspondientes. Aceptar la evidencia previa vigente sin repetir análisis o gates completos.
4. Leer los cinco documentos obligatorios de `docs/design` antes de escribir interfaz: `tokens.md`, `componentes.md`, `estados-y-mensajes.md`, `estados-de-dominio.md` y `accesibilidad.md`.
5. Preparar un plan identificable en Markdown fuera de `Fuentes/`. Separar hechos documentados, inferencias, propuestas y decisiones pendientes. No inventar identificadores estables.
6. Incluir alcance, exclusiones, dependencias, contratos disponibles y faltantes, autorización, paginación e historia cuando correspondan, composición de pantallas, mensajes, archivos previstos y validación local proporcional. El resumen previo tendrá como máximo ocho puntos.
7. Si faltan contratos, componentes o mensajes, presentar una propuesta concreta de resolución y sus secciones exactas para revisión. No implementar esa parte hasta su aprobación. Los cambios a contenido congelado se documentan mediante una adenda en la raíz; no se modifica el original.
8. Solicitar aprobación del plan antes de implementar. Conservar el texto de aprobación y las secciones cubiertas en la trazabilidad. No reconstruir ni solicitar de nuevo decisiones ya aprobadas.

## 4. Implementar y cerrar localmente

Implementar exclusivamente el plan aprobado, con el cambio mínimo necesario y sin adelantar la siguiente tarea. Conservar autorización en servidor, historia, auditoría, idempotencia y concurrencia donde correspondan.

La interfaz usa los componentes y variables aprobados. Cada pantalla cubre normal, foco, deshabilitado, error, cargando y vacío. Los permisos de presentación no reemplazan los controles del servidor.

Añadir o actualizar las pruebas directamente afectadas, incluidas las antiguas cuya expectativa deje de ser válida por el cambio aprobado. No limitar la revisión a las pruebas nuevas: revisar también rutas, servicios e inventarios que dependan del comportamiento modificado.

Ejecutar la compilación, las pruebas enfocadas y `git diff --check` conforme a `AGENTS.md`. Ejecutar restore sólo si es necesario. Reservar suites completas y gates costosos para el hito de publicación o integración autorizado. Registrar resultados reales y causas precisas de cualquier validación diferida.

Actualizar el informe de la tarea y `IMPLEMENTATION_STATUS.md` en el mismo cambio. Crear un commit local coherente después de pasar las comprobaciones disponibles. Informar qué se hizo, qué criterios cubre, archivos cambiados, commit, validación y límites.

Usar **Implementada localmente** cuando corresponda. No hacer push ni abrir PR antes de la autorización de publicación. Detenerse en ese punto sin activar seguimiento remoto.

## 5. Publicar el hito aprobado

Al recibir autorización de publicación:

1. Comprobar rama, árbol de trabajo, base y commits del hito; preservar cambios ajenos.
2. Publicar en una rama `codex/` y crear o actualizar un único PR del hito. Incorporar código y trazabilidad conjuntamente, conforme a `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md`.
3. Adjuntar el PR al chat. Incluir en su descripción problema, resultado, criterios, pruebas y validaciones diferidas.
4. Obtener el SHA completo de la cabeza del PR y verificar que el nuevo pipeline corresponda a ese SHA. Si no arranca, investigar el disparador y activar el workflow permitido cuando proceda; no anunciarlo en curso sin evidencia.
5. Registrar **Publicada**, con evidencia real. No declarar Integrada o Terminada antes de cumplir sus requisitos.
6. Iniciar el seguimiento descrito en la sección 7 antes de entregar las capturas o terminar un turno con el pipeline pendiente.

La evidencia verde de una cabeza anterior no valida un commit nuevo. No crear commits sólo para escribir números de PR/run o el SHA del propio commit; usar referencias resolubles y conservar los datos históricos ya conocidos.

## 6. Capturas y explicación para el usuario

Una vez publicada la implementación, preparar las capturas de las pantallas creadas o modificadas mientras el pipeline continúa. Esta entrega no finaliza el seguimiento.

- Capturar la interfaz implementada con datos sintéticos; incluir las secciones principales y las confirmaciones necesarias para entender su uso.
- Identificar si la captura corresponde a una pantalla en funcionamiento o a una previsualización. No presentar una maqueta como una captura de la aplicación ni una previsualización como prueba integral.
- Limpiar datos sensibles, credenciales y valores ocultos antes de guardar o mostrar los archivos. Conservarlos fuera de `Fuentes/` y usar rutas absolutas al mostrarlos.
- Explicar directamente al usuario para qué sirve cada pantalla, cómo consultarla, qué hacen los botones, qué puede hacer cada puesto y qué significan los estados.
- Usar palabras cotidianas. No incluir conceptos de programación, nombres internos, detalles del pipeline ni afirmaciones sobre funciones futuras en esa explicación.
- Mostrar los estados vacíos o de error cuando aporten una instrucción útil. Si hay una corrección visible posterior, actualizar las capturas afectadas.

## 7. Seguimiento continuo del pipeline

El seguimiento es parte del hito publicado. No termina al mostrar las capturas, al enviar una respuesta final ni al cambiar temporalmente de conversación. No esperar a que el usuario pregunte «Revisa el pipeline» para descubrir un fallo.

Mientras el agente esté trabajando, consultar el estado con esperas acotadas y evitar consultas repetidas sin cambios. Atender mensajes del usuario sin perder el seguimiento, salvo pausa o cancelación expresa.

Si el turno va a terminar antes del resultado, crear o actualizar una automatización de seguimiento vinculada a este mismo chat, mediante el mecanismo de heartbeat disponible. La aprobación de publicación dentro de este flujo incluye ese seguimiento y sus correcciones. No crear chats nuevos ni programadores alternativos. Reutilizar el seguimiento existente para evitar duplicados.

La automatización conserva tarea, PR, rama, último SHA completo, run y estado conocido, autorizaciones, comandos proporcionales y referencias al plan y a este documento. En cada ejecución vuelve a consultar la cabeza real del PR; no trabaja sobre un SHA obsoleto ni altera el checkout de otra tarea. Si el checkout está ocupado, usar un entorno aislado conforme a las instrucciones del repositorio o avisar del impedimento concreto.

Mantener silencio mientras el estado no cambie o no sea accionable. Avisar al detectar un fallo, publicar una corrección, encontrar un impedimento que requiera decisión o alcanzar el resultado verde que permite solicitar merge. No enviar avisos repetidos del mismo estado.

Si no se puede activar el seguimiento automático, comunicar esa limitación inmediatamente y mantener el seguimiento dentro del turno cuando sea posible. Nunca prometer vigilancia en segundo plano sin haberla configurado y verificado. El responsable puede pausar o cancelar el seguimiento; al hacerlo no seguir publicando correcciones.

### 7.1. Si falla

1. Detectar el run y job fallidos y comprobar el SHA al que pertenecen.
2. Leer los logs necesarios y distinguir la causa primaria de fallos derivados o jobs omitidos. Informar brevemente del fallo y la acción que se está realizando.
3. Reproducir el caso de manera enfocada cuando sea viable. Un fallo de entorno se registra con su causa; no se transforma en éxito ni se supone intermitente sin evidencia.
4. Corregir el defecto dentro del alcance aprobado. Actualizar las pruebas obsoletas para exigir el contrato vigente; investigar cualquier respuesta inesperada en vez de ajustar la aserción para aceptar el error.
5. Conservar los gates, la seguridad y los criterios. No deshabilitar pruebas, relajar expectativas correctas, eliminar checks requeridos ni marcar errores como aceptados para conseguir verde.
6. Compilar y ejecutar las pruebas directamente afectadas. Registrar el fallo anterior, causa, corrección y resultados en la trazabilidad.
7. Crear commit y hacer push al mismo PR, sin pedir una autorización adicional de publicación. Verificar el nuevo SHA y que arranque un nuevo pipeline. Actualizar el seguimiento a esa cabeza.
8. Repetir el ciclo hasta obtener todos los checks requeridos correctos o encontrar una decisión necesaria. No detenerse sólo porque una primera corrección haya sido enviada.

Un fallo de red o de runner puede justificar un reintento del run sin cambios de código, después de documentar la evidencia. No reintentar a ciegas. Si la misma causa persiste tras tres intentos razonados sin avance, avisar del impedimento y presentar la decisión o acción necesaria; no repetir indefinidamente ni pedir merge.

### 7.2. Si se cancela, se omite o deja de avanzar

Una ejecución cancelada, un timeout, una acción requerida o un job requerido omitido no equivalen a éxito. Determinar el motivo. Si un run fue sustituido por una cabeza más reciente, seguir la ejecución nueva.

Si existe un impedimento externo o falta una decisión del responsable, avisar con el run, causa, trabajo completado y acción requerida. Conservar el estado Publicada y las validaciones pendientes. No pedir aprobación de merge en ese aviso.

Un experimento opcional omitido no bloquea por sí mismo: la lista de checks requeridos se obtiene de las reglas y documentos vigentes, no se inventa ni se rebaja durante la reparación.

### 7.3. Si termina correctamente

Antes de solicitar merge, comprobar que:

- El PR sigue abierto y su cabeza es exactamente la validada.
- Todos los checks requeridos finalizaron correctamente; ninguno requerido está pendiente, fallido, cancelado u omitido.
- La rama puede integrarse y no hay conflictos ni revisiones o requisitos obligatorios pendientes. Resolver los conflictos compatibles con el alcance y volver a validar la cabeza resultante.
- Los criterios y la evidencia exigidos para el hito están cubiertos; los límites o validaciones diferidas restantes se explican sin presentarlos como aprobados.
- La trazabilidad está incluida en el PR y no hace falta otro commit administrativo que invalide la cabeza verde.

Enviar entonces un aviso con el enlace del PR, SHA validado, enlace del run, resultados y límites. Pedir aprobación expresa de merge y esperar. Un ejemplo:

> El pipeline de FRONT-XXX terminó correctamente para la cabeza indicada del PR. Las correcciones están incluidas y los requisitos previos al merge están comprobados. ¿Apruebas el merge de esta cabeza del PR? Esta aprobación no incluye despliegue.

Detener los reintentos y desactivar el seguimiento periódico al pasar a espera de aprobación. No realizar el merge por el mero hecho de estar verde. Si la cabeza cambia después del aviso, comprobar los nuevos resultados y pedir aprobación de la nueva cabeza cuando cambie el contenido revisado.

## 8. Merge y cierre

Sólo después de una autorización expresa de merge, volver a comprobar cabeza, checks y requisitos, integrar el PR y verificar la incorporación real en `master`. Informar PR, cabeza validada y commit de merge. Usar **Integrada** únicamente con esa evidencia; aplicar los requisitos de Terminada cuando corresponda.

No desplegar sin autorización específica. No crear un PR posterior sólo para completar hashes o números que ya puedan resolverse mediante el PR y su historia. Desactivar cualquier seguimiento restante del hito cerrado.

## 9. Incorporación y mensaje para iniciar una tarea

Este documento se conserva en la raíz, fuera de `Fuentes/`, y queda referenciado explícitamente en `AGENTS.md` para las tareas `FRONT-*`, conservando las reglas generales para las demás tareas. Un archivo independiente no garantiza que un chat futuro lo lea: la referencia en `AGENTS.md` o su invocación expresa es necesaria.

Nombre sugerido del siguiente chat: **SGOL — FRONT — Ejecución con seguimiento de pipeline**.

Mensaje listo para copiar y pegar con este documento aprobado e incorporado:

> Aplica AGENTS.md e INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md. Identifica la siguiente tarea FRONT efectiva y prepara su plan con las resoluciones previas necesarias. Espera mi aprobación del plan antes de implementar y mi autorización de publicación antes del push. Cuando autorice publicar, esa autorización incluye seguir el pipeline, corregir los defectos del hito, validar y subir las correcciones al mismo PR sin nuevas confirmaciones de publicación. Entrega capturas de las pantallas creadas o modificadas y una explicación sencilla para el usuario mientras continúa el seguimiento. Si terminas el turno con el pipeline pendiente, configura y verifica el seguimiento en este chat. Avísame de fallos y bloqueos accionables. Cuando la cabeza actual tenga todos los checks requeridos correctos y los requisitos previos estén cubiertos, solicita mi aprobación expresa para el merge. No hagas merge ni despliegue por la autorización de publicación.
