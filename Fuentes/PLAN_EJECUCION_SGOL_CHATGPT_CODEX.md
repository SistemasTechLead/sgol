# SGOL — Plan maestro de ejecución con ChatGPT y Codex

## 1. Propósito

Este documento dirige la planificación y el desarrollo de SGOL mediante un Proyecto de ChatGPT y, posteriormente, Codex.

Define:

- El orden de las fases.
- Qué fases pueden trabajar en paralelo.
- El objetivo de cada chat.
- Los documentos que debe producir cada fase.
- Los criterios necesarios para avanzar.
- El mensaje de transferencia que debe dejar cada chat.
- El momento exacto en que comienza el trabajo con Codex.

Cada fase debe ejecutarse en un chat separado dentro del mismo proyecto. Una fase solo se considera aprobada cuando sus resultados finales están guardados como fuentes del proyecto.

---

## 2. Estado inicial de las fuentes

Las fuentes de SGOL están disponibles directamente dentro de una carpeta. Esa carpeta tiene el nombre que anteriormente correspondía al archivo ZIP y conserva las rutas ya definidas.

Reglas obligatorias:

1. No buscar, crear, abrir ni descomprimir ningún archivo ZIP.
2. Utilizar directamente la carpeta de fuentes existente.
3. Conservar exactamente las rutas actuales.
4. No mover, renombrar, sobrescribir ni eliminar archivos originales.
5. Recorrer las subcarpetas cuando existan.
6. Considerar los archivos actuales como el conjunto inicial corregido: los duplicados conocidos ya fueron retirados y el archivo que faltaba ya fue incorporado.
7. Aun así, verificar al comienzo que todos los archivos sean accesibles y registrar cualquier anomalía real que se encuentre.
8. No volver a reportar como problemas los duplicados o el faltante de una versión anterior que ya no forma parte de las fuentes actuales.
9. Ignorar como fuentes los archivos temporales del sistema o de Excel, por ejemplo los que comienzan con `~$`.
10. La expectativa inicial es encontrar aproximadamente 20 archivos Markdown y 20 archivos Excel válidos. La Fase 00 debe confirmar el total real sin inventar archivos para alcanzar esa cifra.

La carpeta original es material de consulta. Los entregables generados durante las fases deben guardarse fuera de ella, en la ubicación destinada a resultados o documentación.

---

## 3. Responsabilidades

### Proyecto de ChatGPT

Se utiliza para:

- Organizar y comprender las fuentes.
- Normalizar requisitos.
- Detectar dudas y contradicciones.
- Definir alcance y MVP.
- Preparar historias y criterios de aceptación.
- Documentar decisiones funcionales y técnicas.
- Preparar el backlog para Codex.

### Repositorio de SGOL

Es la fuente oficial durante el desarrollo y debe contener:

- Especificaciones aprobadas.
- Decisiones registradas.
- Backlog implementable.
- Código fuente.
- Pruebas.
- Instrucciones para Codex.

### Codex

Se utiliza para:

- Crear y modificar código.
- Agregar y ejecutar pruebas.
- Mantener documentación técnica.
- Revisar cambios y corregir defectos.
- Preparar versiones verificables.

### Responsable humano

Debe aprobar las decisiones de alcance, reglas y arquitectura; resolver contradicciones; revisar los entregables; autorizar el cambio de fase y aceptar o rechazar los incrementos funcionales.

---

## 4. Configuración del Proyecto de ChatGPT

### Nombre sugerido

`SGOL — Diseño y desarrollo`

### Contexto que debe estar disponible

1. Este plan maestro.
2. La carpeta de fuentes de SGOL, accesible mediante las rutas existentes.
3. Los archivos Markdown y Excel contenidos en ella.
4. Los entregables aprobados de cada fase conforme se produzcan.

Si se usa un Proyecto de ChatGPT que no tiene acceso directo a la carpeta local, se deben incorporar al proyecto los archivos de la carpeta como fuentes. No se debe volver a comprimir la carpeta para hacerlo.

### Instrucciones del proyecto

Copiar íntegramente el siguiente texto en las instrucciones del Proyecto de ChatGPT:

```text
Este proyecto tiene como objetivo planificar y desarrollar SGOL, un sistema web para el manejo de empleados y tareas.

PLAN OBLIGATORIO
Sigue el documento "SGOL — Plan maestro de ejecución con ChatGPT y Codex". Cada chat debe trabajar únicamente en la fase asignada. No adelantes fases ni generes código de producción antes de que el plan lo indique.

UBICACIÓN Y ESTADO DE LAS FUENTES
1. Las fuentes están disponibles directamente dentro de una carpeta que conserva la ruta ya definida y tiene el nombre que anteriormente correspondía al archivo ZIP.
2. No busques, abras, generes ni descomprimas archivos ZIP.
3. Accede directamente a la carpeta y recorre sus subcarpetas cuando sea necesario.
4. Conserva las rutas actuales. No muevas, renombres, sobrescribas ni elimines archivos originales.
5. Los duplicados conocidos ya fueron retirados y el archivo que faltaba ya fue incorporado. Trabaja con el contenido actual de la carpeta como conjunto inicial corregido.
6. No reportes como anomalías elementos pertenecientes a versiones anteriores que ya no están presentes.
7. Verifica la accesibilidad y legibilidad del conjunto actual. Si descubres una anomalía real, regístrala con su ruta exacta.
8. Ignora archivos temporales, como los que comienzan con ~$.
9. La expectativa inicial es de aproximadamente 20 archivos Markdown y 20 Excel válidos; confirma el total real durante la Fase 00.

REGLAS SOBRE EL CONTENIDO
1. Los Markdown y Excel son fuentes del sistema, pero pueden contener omisiones o contradicciones funcionales.
2. No inventes funciones, campos, reglas, estados, permisos ni excepciones.
3. Indica qué archivo, ruta, hoja y sección sustentan cada requisito.
4. Distingue entre hecho documentado, inferencia, propuesta, pregunta y decisión aprobada.
5. Si dos fuentes se contradicen, registra la contradicción y solicita una decisión. No elijas silenciosamente.
6. Mantén identificadores estables para fuentes, requisitos, reglas, decisiones, historias y pruebas.

REGLAS DE EJECUCIÓN
1. Trabaja en entregables pequeños, verificables y trazables.
2. Una fase puede dividir trabajo interno en paralelo solo cuando el plan lo permite, pero debe consolidar todos sus resultados antes de cerrarse.
3. No declares terminada una fase si faltan entregables o existen bloqueos críticos.
4. Al terminar una fase, genera sus documentos finales en Markdown.
5. Indica al usuario que debe incorporar los documentos aprobados a las fuentes del proyecto.
6. Después entrega el nombre exacto del siguiente chat y un mensaje listo para copiar y pegar.
7. Si la fase está bloqueada, no indiques que se puede avanzar. Enumera lo necesario para desbloquearla.
8. Responde en español.

FUENTE OFICIAL
Durante la planificación, los entregables aprobados tienen prioridad sobre borradores y conversaciones anteriores. Durante el desarrollo, el repositorio y sus documentos aprobados son la fuente oficial.
```

---

## 5. Protocolo obligatorio de inicio y cierre

### Al comenzar cada chat

1. Leer este plan maestro.
2. Identificar la fase asignada.
3. Leer los entregables aprobados de fases anteriores.
4. Confirmar objetivo, entradas y entregables.
5. Trabajar únicamente en la fase asignada.
6. Registrar dudas sin inventar respuestas.

### Al terminar cada chat

Usar exactamente esta estructura:

```text
## Cierre de fase

Fase: [número y nombre]
Estado: COMPLETADA / COMPLETADA CON OBSERVACIONES / BLOQUEADA

### Entregables producidos
- [archivo o resultado]

### Validaciones realizadas
- [criterio comprobado]

### Decisiones pendientes
- [decisión o "Ninguna"]

### Acción requerida del usuario
1. Revisar los entregables.
2. Aprobarlos o solicitar correcciones.
3. Guardar los documentos finales.
4. Incorporarlos a las fuentes del Proyecto de ChatGPT.

### Cambio de chat
[Indicar si ya se puede avanzar.]

Nombre del siguiente chat:
`[nombre exacto]`

Mensaje para iniciar el siguiente chat:
```text
[mensaje completo listo para copiar]
```
```

Un estado `BLOQUEADA` impide cambiar de fase. Un estado `COMPLETADA CON OBSERVACIONES` solo permite avanzar si las observaciones no afectan la validez de la fase siguiente.

---

## 6. Dependencias y paralelismo

Las fases son puertas de control. Se puede paralelizar trabajo dentro de algunas fases, pero no aprobar la siguiente hasta consolidar los resultados.

| Fase | Forma de ejecución |
|---|---|
| 00 | Secuencial; debe ejecutarse primero |
| 01 | Puede dividir el inventario por lotes y consolidarlo al final |
| 02 | Puede normalizar por módulos y trabajar glosario/contradicciones en paralelo |
| 03 | Secuencial, porque centraliza decisiones humanas |
| 04 | Secuencial, después de resolver contradicciones bloqueantes |
| 05 | Puede especificar módulos del MVP en paralelo y consolidar matrices al final |
| 06 | Puede diseñar datos, API, seguridad y pruebas en paralelo, con aprobación conjunta |
| 07 | Puede preparar backlog, estructura y `AGENTS.md` en paralelo |
| 08 | Puede implementar historias independientes en paralelo usando espacios de trabajo separados |
| 09 | Puede ejecutar tipos de prueba distintos en paralelo |
| 10 | Puede preparar soporte, monitoreo y capacitación en paralelo; el despliegue es una decisión única |

Orden de las puertas de aprobación:

```text
00 → 01 → 02 → 03 → 04 → 05 → 06 → 07 → 08 → 09 → 10
```

---

# 7. Fases de ejecución

## Fase 00 — Preparación y control del proyecto

**Chat:** `00 — Preparación y control del proyecto`

### Objetivo

Confirmar que la carpeta de fuentes, las instrucciones y el mecanismo de entregables están listos. No se realiza todavía análisis funcional.

### Mensaje inicial

```text
Estamos ejecutando la Fase 00 del documento "SGOL — Plan maestro de ejecución con ChatGPT y Codex".

Trabaja únicamente en la preparación y control. No analices todavía las funciones de SGOL.

Las fuentes están directamente en la carpeta de fuentes indicada por las rutas existentes. No existe ninguna tarea de descompresión: no busques ni abras un ZIP. No cambies rutas ni modifiques los originales.

Necesito que:
1. Localices la carpeta de fuentes utilizando la ruta disponible.
2. Recorras sus subcarpetas y enumeres los Markdown y Excel válidos.
3. Ignores archivos temporales como los que comienzan con ~$.
4. Confirmes el total real frente a la expectativa aproximada de 20 MD y 20 Excel.
5. Verifiques accesibilidad y legibilidad básica.
6. Asignes identificadores estables a las fuentes.
7. Crees la lista de control de fases y nombres de entregables.
8. No vuelvas a buscar duplicados o faltantes históricos ya corregidos; registra solamente anomalías que existan en la carpeta actual.
9. Produzcas F00_CONTROL_DEL_PROYECTO.md.

Al terminar, aplica el protocolo de cierre e indica exactamente cuándo cambiar al chat de la Fase 01 y qué mensaje debo copiar.
```

### Entregable

`F00_CONTROL_DEL_PROYECTO.md`

### Criterios de cierre

- La carpeta correcta fue localizada sin descomprimir nada.
- Se conservaron las rutas y originales.
- Los archivos actuales están enumerados e identificados.
- El total real de MD y Excel está registrado.
- Las anomalías reales, si existen, están documentadas con su ruta.
- Existe una lista de control de fases.

### Siguiente chat

`01 — Inventario y correspondencia de fuentes`

---

## Fase 01 — Inventario y correspondencia de fuentes

**Chat:** `01 — Inventario y correspondencia de fuentes`

### Objetivo

Construir el inventario de los Markdown y Excel actuales y determinar cuáles se relacionan entre sí, sin especificar todavía funciones.

### Mensaje inicial

```text
Estamos ejecutando la Fase 01 del plan maestro de SGOL.

Lee F00_CONTROL_DEL_PROYECTO.md y las fuentes directamente desde la carpeta indicada. No intentes abrir un ZIP y no cambies las rutas. Trabaja únicamente en inventario y correspondencia; no definas todavía el MVP ni la arquitectura.

Necesito que:
1. Inventaríes los Markdown y Excel actuales.
2. Registres ruta relativa, tipo, nombre, tamaño o fecha cuando sea útil, y tema aparente.
3. Relacione cada Markdown con el Excel correspondiente cuando exista evidencia.
4. Identifiques archivos sin pareja y referencias cruzadas.
5. No inventes correspondencias; marca como pendiente cualquier relación incierta.
6. Produzcas F01_INVENTARIO_DE_FUENTES.md con una tabla trasladable a la matriz de trazabilidad.

Al terminar, aplica el protocolo de cierre e indica exactamente cuándo cambiar a la Fase 02 y qué mensaje debo copiar.
```

### Criterios de cierre

- Todas las fuentes actuales tienen identificador y ruta.
- Las correspondencias confirmadas están documentadas.
- Las relaciones inciertas están marcadas como preguntas.
- Ninguna fuente actual quedó fuera sin explicación.

---

## Fase 02 — Normalización del catálogo funcional

**Chat:** `02 — Normalización del catálogo funcional`

### Objetivo

Convertir las fuentes en un catálogo uniforme y trazable de capacidades, reglas y datos.

### Mensaje inicial

```text
Estamos ejecutando la Fase 02 del plan maestro de SGOL.

Lee F00_CONTROL_DEL_PROYECTO.md, F01_INVENTARIO_DE_FUENTES.md y las fuentes actuales en su carpeta. No definas todavía el MVP ni selecciones tecnología.

Para cada capacidad documentada crea una ficha con ID, nombre, fuente y ubicación, objetivo, actor, disparador, entradas, reglas, resultados, estados, excepciones, permisos, dependencias y preguntas abiertas.

Distingue datos explícitos de inferencias. Usa "No especificado" cuando corresponda.

Produce:
1. F02_CATALOGO_FUNCIONAL.md
2. F02_GLOSARIO.md
3. F02_PREGUNTAS_Y_CONTRADICCIONES.md

Al terminar, aplica el protocolo de cierre e indica exactamente cuándo cambiar a la Fase 03 y qué mensaje debo copiar.
```

### Criterios de cierre

- Todas las capacidades tienen ID y fuente.
- El glosario está consolidado.
- Las contradicciones actuales están visibles.
- Ninguna inferencia se presenta como hecho.

---

## Fase 03 — Resolución de dudas y consolidación

**Chat:** `03 — Resolución de dudas y consolidación`

### Objetivo

Resolver con el responsable humano las preguntas y contradicciones que impedirían definir el alcance.

### Mensaje inicial

```text
Estamos ejecutando la Fase 03 del plan maestro de SGOL.

Lee los entregables de las fases 00 a 02. Facilita la resolución de preguntas y contradicciones; no decidas por mí ni inventes comportamiento.

Agrupa preguntas por impacto, distingue bloqueantes de posponibles, presenta las fuentes y alternativas de cada contradicción, hazme preguntas en grupos pequeños y registra literalmente las decisiones aprobadas.

Produce:
1. F03_REGISTRO_DE_DECISIONES.md
2. F03_CATALOGO_CONSOLIDADO.md
3. F03_PENDIENTES_NO_BLOQUEANTES.md

No cierres la fase mientras existan decisiones bloqueantes. Al terminar, aplica el protocolo de cierre e indica exactamente cuándo cambiar a la Fase 04 y qué mensaje debo copiar.
```

### Criterios de cierre

- No quedan contradicciones bloqueantes.
- Las decisiones tienen contexto y efecto.
- El catálogo refleja las decisiones aprobadas.

---

## Fase 04 — Alcance, prioridades y MVP

**Chat:** `04 — Alcance, prioridades y MVP`

### Mensaje inicial

```text
Estamos ejecutando la Fase 04 del plan maestro de SGOL.

Lee los entregables aprobados de las fases 00 a 03. Trabaja únicamente en alcance y priorización; no selecciones tecnología ni escribas código.

Define objetivos del MVP, áreas funcionales, clasificación de cada capacidad como MVP/posterior/opcional/fuera de alcance, dependencias, criterios de éxito, riesgos y supuestos. Solicita mi aprobación cuando exista juicio de producto.

Produce:
1. F04_ALCANCE_Y_MVP.md
2. F04_MAPA_DE_VERSIONES.md
3. F04_RIESGOS_Y_SUPUESTOS.md

No cierres la fase hasta que el MVP esté aprobado. Al terminar, aplica el protocolo de cierre e indica exactamente cuándo cambiar a la Fase 05 y qué mensaje debo copiar.
```

### Criterios de cierre

- MVP aprobado.
- Cada capacidad está clasificada.
- Fuera de alcance y criterios de éxito están explícitos.

---

## Fase 05 — Especificación funcional del MVP

**Chat:** `05 — Especificación funcional del MVP`

### Mensaje inicial

```text
Estamos ejecutando la Fase 05 del plan maestro de SGOL.

Lee los entregables aprobados, especialmente F04_ALCANCE_Y_MVP.md. Especifica únicamente capacidades del MVP.

Para cada capacidad define historia de usuario, actor, permisos, precondiciones, flujo principal, alternativas, errores, reglas, datos, validaciones, estados, criterios de aceptación, casos de prueba, dependencias y trazabilidad.

Produce:
1. F05_ESPECIFICACION_FUNCIONAL_MVP.md
2. F05_MATRIZ_DE_ROLES_Y_PERMISOS.md
3. F05_MODELO_DE_ESTADOS.md
4. F05_MATRIZ_DE_TRAZABILIDAD.md
5. F05_CRITERIOS_DE_ACEPTACION.md

No cierres si una historia del MVP no puede probarse objetivamente. Al terminar, aplica el protocolo de cierre e indica exactamente cuándo cambiar a la Fase 06 y qué mensaje debo copiar.
```

### Criterios de cierre

- Todas las capacidades del MVP tienen historias y criterios verificables.
- Permisos y estados están definidos.
- No quedan vacíos funcionales bloqueantes.

---

## Fase 06 — Diseño técnico y arquitectura

**Chat:** `06 — Diseño técnico y arquitectura`

### Mensaje inicial

```text
Estamos ejecutando la Fase 06 del plan maestro de SGOL.

Lee toda la especificación aprobada. Evalúa opciones técnicas sin implementar todavía el sistema completo.

Identifica restricciones, compara opciones, solicita mis decisiones y define arquitectura, componentes, modelo de datos, API, autenticación, autorización, auditoría, manejo de archivos, entornos, despliegue, pruebas, seguridad, respaldos y recuperación. Registra las decisiones importantes como ADR.

Produce:
1. F06_ARQUITECTURA.md
2. F06_MODELO_DE_DATOS.md
3. F06_CONTRATO_DE_API.md
4. F06_ESTRATEGIA_DE_PRUEBAS.md
5. F06_SEGURIDAD_Y_OPERACION.md
6. F06_REGISTRO_ADR.md

No cierres mientras existan decisiones técnicas bloqueantes. Al terminar, aplica el protocolo de cierre e indica exactamente cuándo cambiar a la Fase 07 y qué mensaje debo copiar.
```

### Criterios de cierre

- Arquitectura y tecnología aprobadas.
- Datos, API, seguridad, pruebas y despliegue están definidos.
- Las decisiones principales tienen ADR.

---

## Fase 07 — Preparación del repositorio y backlog para Codex

**Chat:** `07 — Preparación del repositorio y backlog para Codex`

### Mensaje inicial

```text
Estamos ejecutando la Fase 07 del plan maestro de SGOL.

Lee los entregables aprobados de las fases 00 a 06. No implementes todavía todas las funciones.

Prepara la estructura del repositorio, un AGENTS.md para Codex, el orden de implementación por cortes verticales, épicas, historias, tareas técnicas, dependencias, definición de "Listo", definición de "Terminado", plantillas para Codex y la primera tarea de inicialización.

La carpeta de fuentes originales debe mantenerse como solo lectura lógica: el desarrollo no debe renombrar, mover ni sobrescribir sus archivos.

Produce:
1. F07_ESTRUCTURA_DEL_REPOSITORIO.md
2. F07_AGENTS_MD_PROPUESTO.md
3. F07_BACKLOG_DE_IMPLEMENTACION.md
4. F07_DEFINICIONES_DE_CONTROL.md
5. F07_PLANTILLAS_PARA_CODEX.md
6. F07_PRIMERA_TAREA_CODEX.md

Al terminar, indica exactamente cuándo abrir Codex e incluye la primera instrucción completa.
```

### Criterios de cierre

- Backlog ordenado por dependencias.
- `AGENTS.md` protege fuentes, convenciones y validaciones.
- Las primeras tareas son pequeñas y verificables.

---

## Fase 08 — Implementación iterativa con Codex

Esta fase usa un chat de Codex por historia o incremento.

**Formato del nombre:** `08 — [ID] — [resultado concreto]`

### Mensaje base

```text
Implementa la historia [ID y nombre] del backlog aprobado de SGOL.

Fuentes autorizadas:
- [documentos y secciones]

Criterios de aceptación:
- [criterios verificables]

Restricciones:
1. Lee y respeta AGENTS.md.
2. No agregues funciones fuera de esta historia.
3. No cambies contratos o decisiones aprobadas sin explicar la necesidad.
4. No agregues dependencias de producción sin autorización.
5. No guardes secretos.
6. No muevas, renombres, sobrescribas ni elimines archivos de la carpeta de fuentes originales.

Antes de editar, examina el código, relaciona la tarea con la documentación, identifica bloqueos y presenta un plan breve.

Después de implementar, ejecuta validaciones, agrega pruebas, resume archivos modificados, relaciona pruebas con criterios e indica limitaciones. No declares terminada la historia si falla alguna validación.

Al terminar, dime si debo abrir un nuevo chat para la siguiente historia y proporciona nombre y mensaje listos para copiar.
```

### Criterio de cierre de la fase

- Todas las historias del MVP están implementadas y aceptadas.
- Las pruebas acordadas pasan.
- Documentación y trazabilidad están actualizadas.
- No existen defectos bloqueantes conocidos.

### Siguiente chat

`09 — Validación integral y aceptación del MVP`

---

## Fase 09 — Validación integral y aceptación del MVP

**Chat:** `09 — Validación integral y aceptación del MVP`

### Mensaje inicial

```text
Estamos ejecutando la Fase 09 del plan maestro de SGOL.

Lee especificación, arquitectura, backlog, trazabilidad, informes de implementación y resultados de pruebas. No agregues nuevas funciones.

Comprueba la cobertura del MVP, identifica criterios sin evidencia, organiza pruebas integrales y de aceptación, clasifica defectos, genera tareas de corrección para Codex y verifica seguridad, permisos, respaldos, recuperación y operación.

Produce:
1. F09_PLAN_Y_RESULTADOS_DE_VALIDACION.md
2. F09_PRUEBAS_DE_ACEPTACION.md
3. F09_REGISTRO_DE_DEFECTOS.md
4. F09_DICTAMEN_DEL_MVP.md

No cierres mientras existan defectos bloqueantes o criterios críticos sin comprobar. Al terminar, aplica el protocolo de cierre e indica exactamente cuándo cambiar a la Fase 10 y qué mensaje debo copiar.
```

### Criterios de cierre

- Existe evidencia para cada requisito del MVP.
- Pruebas críticas y aceptación pasan.
- No quedan defectos bloqueantes.
- Existe dictamen explícito.

---

## Fase 10 — Piloto, despliegue y operación

**Chat:** `10 — Piloto, despliegue y operación`

### Mensaje inicial

```text
Estamos ejecutando la Fase 10 del plan maestro de SGOL.

Lee todos los entregables aprobados, especialmente F09_DICTAMEN_DEL_MVP.md. Prepara el piloto o despliegue, responsables, ambientes, configuración, secretos, respaldos, recuperación, monitoreo, alertas, reversión, capacitación, soporte y criterios de estabilidad.

Produce:
1. F10_PLAN_DE_DESPLIEGUE.md
2. F10_LISTA_DE_VERIFICACION_OPERATIVA.md
3. F10_PLAN_DE_REVERSION.md
4. F10_PLAN_DE_SOPORTE.md
5. F10_CIERRE_Y_BACKLOG_POSTERIOR.md

No declares completado el proyecto solamente porque la aplicación fue publicada. La fase termina cuando se cumplen los criterios operativos y existe una decisión explícita de cierre del MVP.
```

### Criterios de cierre

- Piloto o despliegue autorizado.
- Respaldos y reversión verificados.
- Monitoreo y soporte activos.
- Incidentes críticos resueltos.
- Backlog posterior separado del MVP.

---

## 8. Identificadores y trazabilidad

Usar identificadores permanentes:

- Fuentes: `FTE-001`
- Requisitos: `REQ-001`
- Reglas: `RN-001`
- Decisiones: `DEC-001`
- Decisiones técnicas: `ADR-001`
- Historias: `HU-001`
- Casos de prueba: `CP-001`
- Defectos: `DEF-001`
- Versiones: `VER-001`

Cadena obligatoria:

```text
Fuente → Requisito → Regla → Historia → Criterio → Prueba → Código → Versión
```

---

## 9. Control de cambios

Después de aprobar el MVP, toda solicitud nueva debe clasificarse como corrección, aclaración, cambio de alcance, nueva función posterior o cambio técnico sin efecto funcional.

Todo cambio de alcance debe registrar motivo, solicitante, requisitos afectados, efecto en datos/pruebas/arquitectura, riesgo y decisión.

---

## 10. Regla final

No avanzar por calendario ni por sensación de progreso. Avanzar únicamente cuando los criterios de cierre estén comprobados.

Cada chat debe dejar una transferencia utilizable por el siguiente. Cada resultado aprobado debe incorporarse al Proyecto de ChatGPT y, cuando exista el repositorio, a su documentación oficial.

