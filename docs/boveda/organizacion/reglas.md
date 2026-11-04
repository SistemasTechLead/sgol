# Reglas de organización documental

[Inicio](../Inicio.md) · [Inventario](inventario.md) · [Plantilla](plantilla.md)

## Criterio de vigencia

La aplicación compartida es la fuente aceptada de comportamiento. La [base documental](base-documental.md) identifica el artefacto y las verificaciones. El código explica la implementación cuando su correspondencia con ese artefacto está comprobada. Una fecha reciente o un commit por sí solos no acreditan un despliegue.

## Clasificar y enlazar

Cada nota tiene una categoría principal, un tipo y un estado documental. Antes de crearla, buscar una existente que ya responda a la misma pregunta. Actualizar esa nota si el propósito es el mismo; separar procedimientos, referencias actuales, propuestas e informes fechados. Enlazar desde su mapa principal y añadir relaciones específicas a otras notas cuando expliquen una dependencia, regla, evidencia o procedimiento. No crear enlaces solo para aumentar el número de conexiones.

Los mapas cubren producto, arquitectura, dominio, datos, integraciones, diseño, seguridad, infraestructura, desarrollo y calidad. Un tema transversal puede enlazar varios mapas sin duplicarse. Se conservan por ahora las rutas físicas para mantener enlaces; la clasificación reside en propiedades y mapas.

## Estados y evidencia

- **por-contrastar**: todavía no se reconciliaron todas las afirmaciones con la app compartida.
- **inspeccionado**: referencia contrastada mediante revisión estática, con los límites declarados en su documento. No equivale a una prueba funcional completa.
- **historico**: informe de un momento concreto; no usar sus hallazgos automáticamente como estado actual.
- **propuesta**: diseño futuro o decisión pendiente, separado de la referencia actual.

La propiedad clasificado_el fecha la organización, no verifica el contenido. Solo registrar ultima_verificacion y verificado_en cuando exista evidencia concreta, indicando el alcance y el entorno. Antes de retirar un documento, absorber sus reglas todavía vigentes, conservar las decisiones necesarias y reparar sus enlaces entrantes. No borrar contratos históricos que siguen aplicándose a registros existentes.

## Uso con Codex

Entrar por Inicio, abrir el mapa pertinente y leer solo los documentos útiles para la tarea. Contrastar el estado cambiante con la fuente propietaria. Al cerrar un cambio, actualizar referencia y procedimiento afectados, revisar enlaces e indicar pruebas pendientes. Esta política está incorporada en [AGENTS.md del repositorio](../../../AGENTS.md). Las referencias actuales se mantienen en esta bóveda; los planes y registros de ingeniería se guardan en docs/desarrollo/cambios/ del repositorio y las evidencias privadas fuera de la bóveda. Los cambios aún no desplegados se documentan allí hasta reconciliar la referencia con la app compartida. No existe sincronización automática entre esta bóveda y el repositorio.

Para descubrir qué notas consultar, usar el [índice local de Graphify](../docs/desarrollo/consulta-boveda-graphify.md), que se actualiza y verifica al consultar. Leer después las notas fuente. El índice conserva estructura y enlaces, no acredita reglas ni despliegues; las búsquedas exactas y las rutas ya conocidas no requieren consultarlo.

## Referencias de organización

- [Diátaxis](https://diataxis.fr/): separar documentación por necesidad del lector.
- [arc42](https://arc42.org/overview/): cobertura de arquitectura, despliegue, decisiones y calidad.
- [Propiedades de Obsidian](https://obsidian.md/help/properties) y [enlaces](https://obsidian.md/help/links): metadatos y navegación entre notas.

## Referencia cotidiana

Por decisión del usuario, esta bóveda mantiene documentación actual, no planes de implementación, informes de casos particulares ni ejercicios de importación. Antes de incorporar un cambio, actualizar la referencia de su función. Los informes técnicos y su evidencia pueden conservarse en el repositorio o respaldo externo, sin volver a poblar esta red con documentos históricos. Las reglas de compatibilidad con registros antiguos que siguen implementadas sí pertenecen a la referencia actual.

## Copia versionada

Las referencias consolidadas se conservan también en docs/boveda/ del repositorio. Al preparar un commit documental, actualizar esa copia desde las notas afectadas, revisar divergencias y convertir los enlaces al código en rutas relativas. No existe sincronización automática ni se incluyen configuración de Obsidian o respaldos. La copia versionada sirve de alternativa cuando la bóveda local no está disponible.
