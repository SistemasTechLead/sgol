# SGOL — Fase 10 · Ejecución operativa

## Propósito del dominio

Este dominio registra la ejecución real de una obligación publicada. Su responsabilidad comienza cuando una `PartidaPlan` ejecutable es iniciada por una persona autorizada y termina, para F10, cuando el responsable declara la ejecución concluida. La validación del resultado pertenece a F12; las evidencias pertenecen a F11; las excepciones como arrastre, posposición, cancelación o reasignación pertenecen a F13; el cierre y congelamiento pertenecen a F14.

La existencia de una obligación publicada no implica que exista una ejecución real. Una partida publicada que todavía no se ha iniciado permanece pendiente de inicio y puede obtenerse mediante consulta, sin crear un registro ficticio de ejecución.

## Principios obligatorios

- `PartidaPlan` representa el compromiso publicado; `EjecucionTarea` representa actividad realmente iniciada.
- La ejecución se crea al ejecutar el comando de inicio, no al publicar la partida.
- No se utilizará `EN_PROCESO` para representar simultáneamente una obligación pendiente y una ejecución realmente iniciada.
- Toda nueva ejecución debe conservar un timestamp de inicio y la identidad del actor que la inició.
- Toda conclusión debe conservar un timestamp de conclusión y la identidad del actor que la concluyó.
- El tiempo real de ejecución es derivable de los timestamps; no se duplica como dato persistente salvo que exista una razón explícita de auditoría o integración.
- La ejecución se relaciona con la asignación vigente utilizada para ejecutarla; no almacena el nombre del responsable como llave.
- La validación del supervisor no modifica silenciosamente los hechos de ejecución; crea o actualiza objetos de F12.
- La evidencia no se almacena como columnas embebidas de la ejecución; F11 conserva las evidencias y F10 sólo verifica su cumplimiento cuando sea precondición de conclusión.
- Arrastre, posposición, cancelación, reasignación y otras excepciones no son estados ordinarios de F10; se ejecutan mediante comandos de F13.
- El cierre de un período no reemplaza el resultado de la ejecución por un estado `CERRADA`; F14 congela el registro y conserva intactos sus hechos operativos.
- Los cambios de estado de ejecución deben registrarse como transiciones inmutables para reconstruir el ciclo de vida.
- Toda transición debe ser atómica, idempotente cuando aplique y protegida contra concurrencia.
- Los timestamps operativos deben provenir del sistema y no de texto introducido manualmente por el usuario.

## Mapa del dominio

1. **Partida ejecutable**: partida publicada de F9 que puede iniciar actividad.
2. **Pendiente de inicio**: condición derivada de una partida ejecutable sin `EjecucionTarea` iniciada.
3. **Ejecución de tarea**: hecho persistente que representa el trabajo realmente iniciado.
4. **Transición de ejecución**: registro inmutable de cada cambio de estado operativo.
5. **Conclusión del responsable**: declaración de finalización que transfiere el flujo a F11/F12 según corresponda.
6. **Guardas operativas**: reglas que impiden iniciar, concluir o modificar cuando la partida, asignación, período o cierre no lo permiten.
7. **Vista operativa**: proyección de partidas, ejecución, asignación, definición y estados de dominios relacionados para UX y reportes.

## Entidades persistentes

### EjecucionTarea

Representa una ejecución real iniciada sobre una partida publicada.

**Atributos relevantes**
- `id_ejecucion`;
- `id_partida_plan`;
- `id_asignacion_ejecutada` o referencia equivalente a la asignación vigente al iniciar;
- `estado_ejecucion`;
- `iniciada_en`;
- `iniciada_por`;
- `concluida_en`, nullable mientras siga en curso;
- `concluida_por`, nullable mientras siga en curso;
- observaciones del responsable cuando correspondan;
- clave idempotente del comando de inicio/conclusión;
- metadatos mínimos de correlación y auditoría.

**Restricciones**
- `id_ejecucion` es opaco y único.
- Una partida no puede tener dos ejecuciones activas simultáneas.
- En el alcance definido, una partida tiene como máximo una ejecución ordinaria; intentos adicionales o reaperturas requieren una política explícita de F12/F13 y no se permiten por defecto.
- `concluida_en >= iniciada_en` cuando ambos timestamps existan.
- Una ejecución no puede iniciarse sobre una partida no publicada, cerrada, anulada o no ejecutable.
- La asignación asociada debe corresponder a la misma obligación y estar vigente en el momento de inicio.

### TransicionEstadoEjecucion

Historial append-only del ciclo de vida operativo.

**Atributos relevantes**
- `id_transicion`;
- `id_ejecucion`;
- `estado_anterior`;
- `estado_nuevo`;
- timestamp;
- actor;
- comando o causa;
- referencia de correlación/idempotencia.

Este historial es un hecho de dominio y no se sustituye únicamente por un log técnico general. F17 puede auditar adicionalmente el comando y sus efectos.

## Objetos derivados; no tablas por defecto

### PendientesInicio

Vista de partidas publicadas y ejecutables para las cuales aún no existe una ejecución iniciada.

### VistaEjecucionOperativa

Proyección que combina `PartidaPlan`, `InstanciaTrabajo`, asignación vigente, `EjecucionTarea`, definición de tarea y datos visibles de evidencia/validación sin duplicarlos en la entidad de ejecución.

### DuracionReal

`concluida_en - iniciada_en` cuando ambos timestamps existen. Es cálculo derivado.

## Estados y transiciones

### Estados propios de F10

- `EN_PROCESO`: existe una ejecución real iniciada y no concluida.
- `CONCLUIDA`: el responsable declaró terminada la ejecución y se registró el timestamp de conclusión.

La condición `PENDIENTE_INICIO` se deriva de la partida publicada sin ejecución y no requiere una fila de ejecución ficticia.

### Transiciones propias de F10

- `PENDIENTE_INICIO -> EN_PROCESO` mediante `iniciar_ejecucion`.
- `EN_PROCESO -> CONCLUIDA` mediante `concluir_ejecucion`.

F10 no define como estados ordinarios:
- `VALIDADA_CUMPLIDA`, `VALIDADA_INCOMPLETA`, `NO_CUMPLIDA`: decisiones/resultados de F12;
- `ARRASTRADA`, `POSPUESTA`, `CANCELADA_JUSTIFICADA`, `REASIGNADA`: excepciones de F13;
- `CERRADA`: condición de cierre y congelamiento de F14.

La vista de usuario puede presentar un estado operativo compuesto a partir de esos dominios, pero no debe forzar todos esos conceptos dentro de una sola columna de estado.

## Inicio de ejecución

`iniciar_ejecucion(id_partida_plan, contexto)` debe:
1. validar que la partida exista y esté publicada;
2. validar que el período y el plan permitan operación;
3. validar que no exista una ejecución activa o previa incompatible;
4. resolver y validar la asignación vigente aplicable;
5. validar autoridad del actor para ejecutar la obligación;
6. respetar las restricciones temporales definidas por F5/F9 cuando correspondan;
7. crear `EjecucionTarea` con timestamp del sistema;
8. registrar `TransicionEstadoEjecucion` a `EN_PROCESO`;
9. confirmar la transacción;
10. emitir `EjecucionIniciada` después del commit.

La repetición del mismo comando con la misma clave idempotente debe devolver la ejecución ya creada sin duplicarla.

## Conclusión de ejecución

`concluir_ejecucion(id_ejecucion, contexto)` debe:
1. validar que la ejecución exista y esté `EN_PROCESO`;
2. validar autoridad del actor;
3. validar que el período no esté cerrado y que el registro sea mutable;
4. solicitar a F11 la comprobación de evidencia obligatoria cuando corresponda;
5. registrar observaciones del responsable cuando existan;
6. fijar `concluida_en` y `concluida_por`;
7. cambiar el estado propio a `CONCLUIDA`;
8. registrar la transición inmutable;
9. confirmar la transacción;
10. emitir `EjecucionConcluida` para que F12 pueda crear/activar la validación necesaria.

La conclusión no calcula bono, no decide cumplimiento supervisor y no ejecuta arrastre, posposición o cancelación.

## Reglas de integridad y concurrencia

- Inicio y conclusión deben revalidar el estado dentro de la misma transacción que escribe el cambio.
- Dos solicitudes concurrentes de inicio sobre la misma partida no pueden crear dos ejecuciones activas.
- Dos solicitudes concurrentes de conclusión no pueden producir dos timestamps/resultados distintos.
- Una vez concluida, la ejecución no vuelve a `EN_PROCESO` mediante edición directa.
- Cualquier reapertura requiere un comando explícito y una regla de negocio aprobada; no se permite por defecto.
- Las modificaciones posteriores al cierre quedan bloqueadas por F14 y sólo una ruta administrativa extraordinaria, si se autoriza en el futuro, podrá operar con auditoría reforzada.

## Funciones y consultas

- `obtener_ejecucion_por_partida(id_partida_plan)`.
- `obtener_ejecucion_activa(id_partida_plan)`.
- `listar_pendientes_inicio(id_periodo, alcance, usuario)`.
- `validar_partida_ejecutable(id_partida_plan, momento, actor)`.
- `validar_asignacion_para_ejecucion(id_partida_plan, actor, momento)`.
- `validar_transicion_ejecucion(estado_actual, comando)`.
- `calcular_duracion_real(id_ejecucion)`.
- `obtener_historial_ejecucion(id_ejecucion)`.
- `puede_modificarse_ejecucion(id_ejecucion)` consultando el gobierno de F14.

## Comandos transaccionales

- `iniciar_ejecucion(id_partida_plan, contexto)`.
- `concluir_ejecucion(id_ejecucion, observaciones, contexto)`.

Los comandos de validación se definen en F12. Los comandos de excepción se definen en F13. El cierre se define en F14.

## Eventos

- `EjecucionIniciada`.
- `EjecucionConcluida`.
- `EstadoEjecucionCambiado` cuando sea útil para integraciones o auditoría.

Los eventos se emiten después del commit y no sustituyen los hechos persistidos.

## Permisos

- Iniciar y concluir requiere que el actor sea el responsable vigente o tenga una delegación/permiso operativo explícito dentro del alcance de F2.
- Un supervisor no obtiene automáticamente permiso para alterar hechos de ejecución por tener autoridad de validación.
- Las correcciones administrativas no se realizan mediante edición directa y requieren permisos específicos y auditoría.
- La consulta de ejecuciones respeta el alcance organizativo definido en F2.

## Errores de dominio

- `PARTIDA_NO_EJECUTABLE`;
- `PARTIDA_NO_PUBLICADA`;
- `EJECUCION_YA_INICIADA`;
- `EJECUCION_DUPLICADA`;
- `EJECUCION_NO_ENCONTRADA`;
- `EJECUCION_NO_EN_PROCESO`;
- `EJECUCION_YA_CONCLUIDA`;
- `TRANSICION_EJECUCION_INVALIDA`;
- `ASIGNACION_EJECUCION_INVALIDA`;
- `ACTOR_NO_AUTORIZADO_PARA_EJECUTAR`;
- `EVIDENCIA_OBLIGATORIA_INCOMPLETA` — originado por F11;
- `PERIODO_CERRADO` — originado por F14;
- `REGISTRO_CONGELADO_POSTCIERRE` — originado por F14;
- `CONFLICTO_CONCURRENCIA_EJECUCION`.

## Dependencias

- **F2**: usuarios, identidad, alcance y permisos.
- **F3**: período operativo y reglas temporales generales.
- **F5**: activación y ventanas temporales aplicables.
- **F6**: `InstanciaTrabajo` origen de la obligación.
- **F8**: asignación vigente utilizada por la ejecución.
- **F9**: `PartidaPlan` publicada.
- **F11**: requisitos y registro de evidencias.
- **F12**: validación y resultado supervisor posteriores a la conclusión.
- **F13**: excepciones y cambios extraordinarios del ciclo de vida.
- **F14**: cierre y congelamiento postcierre.
- **F16**: vistas y comandos UX, incluyendo una acción explícita de inicio.
- **F17**: auditoría, correlación e idempotencia transversal.

## Condiciones excepcionales y tratamiento

- Si una obligación está publicada pero nunca fue iniciada, no se crea una `EjecucionTarea`; permanece visible como pendiente derivado.
- Si una ejecución concluye y requiere validación, F12 administra el estado de validación sin alterar los timestamps de ejecución.
- Si F11 exige evidencia y ésta no existe o es inválida, la conclusión no se confirma.
- Si F13 cancela o pospone una obligación antes de iniciar, puede no existir ejecución alguna.
- Si una excepción ocurre después del inicio, F13 debe conservar la relación con la ejecución ya existente y registrar el hecho de excepción sin reescribir su historia.
- Si F14 cierra el período, los hechos de ejecución permanecen consultables e inmutables.

## Criterios de consistencia

- Una ejecución nueva en `EN_PROCESO` siempre tiene `iniciada_en` e `iniciada_por`.
- Una ejecución en `CONCLUIDA` siempre tiene `concluida_en` y `concluida_por`.
- Ninguna ejecución nueva puede tener conclusión anterior al inicio.
- No existe más de una ejecución activa por partida.
- El estado visible compuesto debe poder reconstruirse desde F9/F10/F11/F12/F13/F14 sin editar una columna monolítica.
- El cierre no destruye ni sustituye el resultado operativo alcanzado.

## Pendientes deliberadamente diferidos

1. Definir en F11 el contrato exacto de evidencias que bloquean conclusión.
2. Definir en F12 el modelo de validación y cómo se presentan `cumplida`, `incompleta` y `no cumplida` sin mezclarlos con el estado propio de ejecución.
3. Definir en F13 el tratamiento de arrastre, posposición, cancelación y posibles ejecuciones interrumpidas.
4. Definir en F14 el mecanismo definitivo de congelamiento y cualquier excepción administrativa postcierre.
5. Incorporar en F16 una acción UX explícita `INICIAR` cuando la operación requiera medir tiempo real de ejecución.
