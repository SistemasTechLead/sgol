# F07 — Enmienda 001: cierre de tarea en un solo pull request

## 1. Control

| Campo | Valor |
|---|---|
| ID de implementación | `TOOL-FLOW-001` |
| Tipo | Enmienda específica a `F07_DEFINICIONES_DE_CONTROL.md` |
| Estado | VIGENTE sólo en `master` después de aprobación y merge; PROPUESTA en cualquier otra rama |
| Fecha | 2026-08-31 |
| Autorización de redacción | Decisión humana del 2026-08-31: crear una nueva enmienda y conservar intacto el F07 aprobado |
| Alcance | Contrato de cierre y trazabilidad de tareas; no modifica el alcance funcional del MVP |
| Conservación | No modifica `F07_DEFINICIONES_DE_CONTROL.md` ni su copia congelada en `Fuentes/` |
| Entrada en vigor | Aprobación humana y merge de este mismo pull request a `master` |

Esta enmienda es más reciente y específica que la secuencia de cierre implícita en
`F07_DEFINICIONES_DE_CONTROL.md`. Una vez vigente, prevalece únicamente para el
registro del cierre de tareas; las demás condiciones de Listo, Terminado, gates,
severidad, alcance y protección de `Fuentes/` permanecen sin cambio.

## 2. Problema corregido

El contrato anterior actualizaba `docs/traceability/IMPLEMENTATION_STATUS.md`
después del merge porque exigía registrar el hash de ese merge. El dato no existe
antes del merge, por lo que una implementación requería un segundo commit, otro
pull request, otro pipeline y otra aprobación para cerrar administrativamente la
tarea.

El número de ejecución del pipeline tiene la misma circularidad si se exige dentro
del commit validado: el run se crea después de publicar ese commit. Por ello, el
identificador numérico del run tampoco puede ser un campo bloqueante del registro.

El SHA del propio commit también es autorreferencial: escribirlo dentro del archivo
cambia el contenido y, por tanto, produce otro SHA. Cuando implementación y registro
viajan en el mismo commit, el contrato usa la referencia semántica `commit que
contiene esta actualización`, que Git resuelve de forma exacta sin reescribir el
archivo. Los SHA históricos ya conocidos pueden conservarse literalmente.

## 3. Decisión

Se adopta `TOOL-FLOW-001`, opción **(b)**:

1. el commit que contiene la implementación y la actualización del registro es la
   referencia Git primaria;
2. el pull request identifica la revisión, aprobación y merge;
3. los checks del proveedor asociados al pull request y al commit de
   implementación acreditan el pipeline requerido;
4. el hash concreto del merge y el identificador numérico del run son evidencia
   complementaria y opcional;
5. `IMPLEMENTATION_STATUS.md` se actualiza junto con la implementación, en el
   mismo commit y pull request;
6. no se crea un commit automático posterior al merge.

Esta opción evita permisos de escritura para GitHub Actions, commits de bot sobre
`master` y un nuevo punto de fallo posterior al merge. Un workflow no bloqueante
que intentara completar el hash podría dejar el registro en estado indeterminado;
uno bloqueante contradiría el requisito de no bloquear `master`.

## 4. Contrato de `IMPLEMENTATION_STATUS.md`

La sección `Base aceptada` usa los siguientes campos:

| Campo | Regla |
|---|---|
| Última tarea Terminada | ID estable de la tarea cuyo cierre propone el pull request |
| Corte y épica | Corte y épica aprobados, o declaración expresa de tarea técnica fuera del backlog |
| Pull request | Número del PR; antes de asignarse puede usarse `PR que incorpora este registro`, resoluble en el proveedor |
| Commit implementado | SHA completo cuando ya sea histórico o `commit que contiene esta actualización` cuando el registro viaje en ese mismo commit |
| Pipeline requerido | Nombre del check y referencia a los checks del commit; el resultado y el run numérico se resuelven en el proveedor |
| Aceptación humana | `aprobación requerida en el PR que incorpora este registro`; una fecha histórica ya conocida es opcional |
| Commit incorporado en `master` | Hash real si ya se conoce; `pendiente de merge` es válido y no bloqueante |
| `Fuentes/` | Check de protección requerido en el PR; un resultado histórico ya conocido puede conservarse |
| Siguiente tarea propuesta | Próxima tarea, sin iniciarla automáticamente |

### 4.1. Formato exacto antes del merge

```markdown
## Base aceptada

| Campo | Valor |
|---|---|
| Última tarea Terminada | `<ID-DE-TAREA>` |
| Corte y épica | `<corte y épica, o justificación de tarea técnica>` |
| Pull request | `PR que incorpora esta actualización` |
| Commit implementado | `commit que contiene esta actualización` |
| Pipeline requerido | `TECH-BASE-003 / PR gates`; checks asociados al commit implementado |
| Aceptación humana | Aprobación requerida en el PR que incorpora este registro |
| Commit incorporado en `master` | `pendiente de merge` |
| `Fuentes/` | Protección requerida en los checks del PR |
| Siguiente tarea propuesta | `<ID y nombre, sin iniciarla automáticamente>` |
```

Si el número de PR ya fue asignado sin modificar el commit, sustituye la referencia
semántica por `#<número>`. No se reescribe el commit únicamente para sustituir una
referencia resoluble por un número, SHA, fecha o resultado literal.

En el pull request, la tabla completa es una **propuesta de base aceptada**. La
fila `Última tarea Terminada` no declara que el cierre ya ocurrió en esa rama. El
mismo registro se convierte en `Base aceptada` sin editarse nuevamente sólo si se
cumplen todas estas condiciones:

1. el registro forma parte de la historia de `master`;
2. el commit implementado, resuelto por SHA o como el commit que introdujo esta
   actualización, pertenece a la historia de `master`;
3. el pull request indicado consta como merged a `master`;
4. el check requerido pasó para ese pull request y ese commit;
5. existe aceptación humana registrada;
6. la protección de `Fuentes/` pasó y no queda un defecto bloqueante.

Si falta cualquiera de esas condiciones, la tarea permanece `Pendiente de merge`
o bloqueada y no puede utilizarse como dependencia Terminada. La presencia del
texto en una rama, por sí sola, nunca prueba el cierre.

Cuando el hash de merge permanezca como `pendiente de merge`, se resuelve de forma
verificable mediante el PR y la historia de `master`. Cuando el run numérico no
esté escrito, se resuelve mediante los checks del commit implementado. Ninguno de
los dos datos sustituye las condiciones anteriores.

## 5. Secuencia de cierre

La secuencia obligatoria es:

1. implementar la tarea y actualizar `IMPLEMENTATION_STATUS.md` en el mismo
   commit, usando la autorreferencia verificable si su SHA todavía no puede
   escribirse literalmente;
2. publicar un único pull request;
3. ejecutar un único ciclo válido del pipeline requerido sobre ese commit;
4. obtener una única aprobación humana;
5. realizar un único merge a `master`;
6. verificar que el commit implementado está en la historia de `master`, sin abrir
   un pull request administrativo posterior.

Si un cambio posterior modifica el commit de implementación, sus checks anteriores
dejan de acreditar el cierre y el pipeline debe validar el nuevo commit. Esto no es
un segundo cierre: es la validación necesaria del contenido que efectivamente se
pretende fusionar.

## 6. Garantías y pérdida aceptada

Se conserva:

- implementación y trazabilidad revisadas juntas;
- aprobación humana previa al merge;
- pipeline requerido ligado al commit exacto, aunque el documento lo identifique
  mediante una autorreferencia resoluble;
- merge comprobable mediante PR e historia Git;
- integridad de `Fuentes/` y todos los gates sustantivos de F07;
- imposibilidad normativa de usar una tarea no fusionada como Terminada.

Se pierde únicamente la garantía de que el documento contenga siempre el hash
literal del merge y el número literal del run. No se pierde la capacidad de
resolverlos y verificarlos en Git/GitHub a partir del PR y del commit implementado.

Registrar una tarea como Terminada sin que exista su merge sería un defecto
bloqueante de trazabilidad. Esta enmienda lo evita haciendo que la eficacia del
registro dependa de su presencia y de la presencia del commit implementado en
`master`, no de una declaración anticipada dentro del pull request.
