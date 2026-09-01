# SGOL — Adenda 03 a F07: núcleo de versionado

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tarea de planeación | `TOOL-PLAN-002` |
| Tipo | Adenda de inserción al orden de ejecución de `F07_BACKLOG_DE_IMPLEMENTACION.md` y aclaración de dependencia registrada |
| Estado | PROPUESTA |
| Fecha | 2026-08-31 |
| Efecto | Amplía F07 mediante una tarea técnica previa y registra una interpretación; no modifica ni renumera el backlog aprobado |
| Conservación | Mantiene sin cambios `F07_BACKLOG_DE_IMPLEMENTACION.md` y los demás entregables F00–F07 aprobados |
| Eficacia | Pasa a `APROBADA` al incorporarse a `master`; desde ese momento su contenido es obligatorio |

## 2. Justificación y evidencia

Seis historias declaran en sus filas un ciclo de creación, publicación o sustitución de versiones para configuración, calendario, definiciones o políticas. La evidencia concreta es:

1. `HU-008`, configuración — `F05_ESPECIFICACION_FUNCIONAL_MVP.md:123`: publica una nueva versión con motivo, rechaza solapamientos y declara versión, vigencia y estados `BORRADOR/VIGENTE/SUSTITUIDA`.
2. `HU-009`, calendario — `F05_ESPECIFICACION_FUNCIONAL_MVP.md:124`: un cambio futuro crea una versión.
3. `HU-011`, definiciones TAR — `F05_ESPECIFICACION_FUNCIONAL_MVP.md:126`: crea una versión, conserva el historial y registra versión en sus datos.
4. `HU-017`, elegibilidad — `F05_ESPECIFICACION_FUNCIONAL_MVP.md:132`: una nueva versión sustituye a la anterior y registra vigencia.
5. `HU-024`, evidencia requerida — `F05_ESPECIFICACION_FUNCIONAL_MVP.md:139`: una nueva versión no cambia tareas existentes y registra vigencia.
6. `HU-027`, validación — `F05_ESPECIFICACION_FUNCIONAL_MVP.md:142`: una versión futura sustituye a la anterior.

Las seis filas declaran versionado, pero no repiten literalmente todos los detalles del mecanismo. El soporte transversal está en F06: `F06_ARQUITECTURA.md:93` agrupa calendario, definiciones y políticas versionadas en `Configuration`; `F06_MODELO_DE_DATOS.md:25`, `:148`, `:157` y `:243` exigen concurrencia optimista, una versión vigente por objeto y alcance, ETag y conservación de predecesora y vigencia; `F06_CONTRATO_DE_API.md:28` y `:263-266` exigen ETag/If-Match para actualizaciones; y `TECH-AUD-001`, en `F07_BACKLOG_DE_IMPLEMENTACION.md:57`, aporta la inserción transaccional en `audit_event`.

`HU-021` (`F05_ESPECIFICACION_FUNCIONAL_MVP.md:136`) y `HU-025` (`:140`) también conservan versiones mediante sustitución, pero son variantes funcionales de publicación de plan y evidencia. No amplían el comportamiento común de esta adenda ni trasladan sus reglas específicas a `TECH-VER-001`.

Resolver el mecanismo transversal antes de `HU-008` evita que cada historia reproduzca infraestructura equivalente a la medida de su entidad y permite que las variaciones permanezcan en la historia que las define.

## 3. Tarea técnica insertada

La fila usa las mismas columnas de la sección 5 de F07.

| ID | Resultado verificable | Dependencias | No incluye |
|---|---|---|---|
| TECH-VER-001 | Mecanismo transversal de versionado: estados `BORRADOR/VIGENTE/SUSTITUIDA`, vigencias sin solapamiento, motivo obligatorio, concurrencia por ETag/If-Match e inserción de `audit_event` en la misma transacción que la escritura | TECH-AUD-001, TECH-UI-001 | Ninguna entidad funcional concreta, ninguna pantalla y ninguna regla de negocio de HU-008 ni de las demás historias |

## 4. Orden efectivo y bloqueo

`TECH-VER-001` se ejecuta **ANTES de HU-008**. Mientras `TECH-VER-001` no esté `Terminada`, bloquea el inicio de HU-008. Las órdenes 1 a 35 de `F07_BACKLOG_DE_IMPLEMENTACION.md` conservan su numeración; esta adenda inserta una puerta previa y no las sustituye.

La comprobación obligatoria del bloqueo se registra en `docs/traceability/IMPLEMENTATION_STATUS.md`, sección `Tareas insertadas por adenda`, y se aplica mediante la regla de precedencia de `AGENTS.md` antes de iniciar cualquier tarea.

## 5. Advertencia de abstracción

**ADVERTENCIA:** un mecanismo genérico construido sin ninguna implementación concreta corre el riesgo de abstraer de más. Por eso `TECH-VER-001` se limita al comportamiento que las seis historias comparten literalmente según sus filas, complementado únicamente por las garantías transversales ya aprobadas en F06, y toda variación específica queda en su historia. Si al implementarla se detecta que dos historias exigen comportamientos incompatibles, la tarea debe detenerse y reportarlo en lugar de generalizar.

## 6. Interpretación de dependencias registradas

La fila de `HU-008` en `F07_BACKLOG_DE_IMPLEMENTACION.md:82` registra la dependencia `HU-033 mínimo transaccional`. Leída literalmente, esa referencia apunta hacia `HU-033`, orden 33, desde `HU-008`, orden 7. El contexto técnico identifica el requisito como el núcleo transaccional de auditoría: `TECH-AUD-001`, definido en `F07_BACKLOG_DE_IMPLEMENTACION.md:57`, ya está `Terminada` según `docs/traceability/IMPLEMENTATION_STATUS.md`.

Para iniciar `HU-008`, la dependencia registrada como `HU-033 mínimo transaccional` se considera satisfecha por `TECH-AUD-001`; la sesión no debe detenerse a esperar `HU-033`. Esta es una interpretación documentada, no una corrección: `F07_BACKLOG_DE_IMPLEMENTACION.md` aprobado no se modifica y conserva literalmente su dependencia original.

La interpretación anterior no elimina el nuevo bloqueo de orden: `HU-008` sólo puede iniciar cuando `TECH-VER-001` esté `Terminada`.

## 7. Aprobación y eficacia

Esta adenda permanece como `PROPUESTA`. No declara `TECH-VER-001` `Terminada` ni habilita `HU-008`. Al incorporarse a `master`, pasa a `APROBADA` y desde ese momento su contenido es obligatorio; hasta entonces, el cambio documental y su registro de precedencia son una propuesta en la rama correspondiente.
