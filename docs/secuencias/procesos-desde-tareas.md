# Procesos administrados desde Tareas

Fecha: 13 de octubre de 2026.
Estado: ajuste de alcance documentado; integración pendiente.

Este ajuste queda integrado en el [plan vigente de procesos](../plan-procesos-de-trabajo.md).
Se conserva como antecedente de la propuesta; el orden y criterios consolidados
se consultan en ese plan.

El usuario aprobó la propuesta visual del inventario como plantilla reutilizable
y propuso administrar los procesos desde el catálogo de Tareas. «Proceso» sustituye
«Secuencia» en el lenguaje de la interfaz. Se conserva el motor de versiones,
ejecuciones, etapas, intentos, documentos y eventos ya preparado.

## Pertenencia y reglas

- Cada tarea pertenece a un solo proceso por su identificador estable. El nombre
  se muestra desde el proceso; no se copia como texto libre en cada tarea.
- General es el proceso de sistema para las tareas sin agrupación específica.
  No crea una ejecución común, no impone dependencias y conserva los calendarios
  y el reparto individuales actuales.
- Un proceso con flujo reúne tareas y establece su orden, condición de entrada,
  acción, plazo y destino permitido de devolución en una versión de configuración.
- La primera integración conserva el recorrido lineal con devoluciones ya
  preparado. Las ramas y tareas simultáneas requieren ampliar el motor: no se
  prometen como consecuencia de permitir crear procesos nuevos.
- Pertenecer a un proceso no implica que esté listo para ejecutarse. Un borrador
  incompleto no se activa ni genera asignaciones.

## Editor propuesto

En Tareas se mostrarán Crear tarea, Crear especialización y Crear proceso.
Actualmente esa cabecera solo contiene Crear Tarea; el acceso a especializaciones
se incorporará reutilizando su pantalla existente.

Crear proceso permitirá:

1. Capturar nombre y descripción; guardar un borrador con identidad estable.
2. Buscar tareas por nombre o código y añadirlas al proceso. Una tarea de otro
   proceso no se moverá silenciosamente: se mostrará su pertenencia y se validará
   el cambio y la configuración afectada antes de publicarlo.
3. Crear una tarea desde el editor, reutilizando el formulario y validación
   existentes y preseleccionando el proceso. El servidor comprobará esa selección.
4. Ordenar las etapas y definir entrada, entrega/revisión/autorización, plazo,
   instrucciones y devolución. Elegir tareas por sí solo no establece estas reglas.
5. Configurar el inicio del proceso, revisar el flujo y publicar una versión válida.

El formulario de tarea incluirá Proceso, con General como valor inicial cuando
se crea desde el catálogo. El listado incorporará columna y filtro de proceso.
Para una tarea de un proceso con flujo, se mostrará que su inicio lo controla el
proceso; el plazo se editará en las reglas del proceso para evitar dos fuentes
contradictorias. Rol, especialización, evidencia y carga siguen siendo datos de tarea.

## Compatibilidad y generación

Se propone ampliar la identidad existente de secuencia con descripción y distinción
entre General y proceso con flujo, y añadir la referencia de pertenencia a
OperationalTask. Los nombres técnicos actuales pueden conservarse para no rehacer
la migración probada únicamente por un cambio de vocabulario.

La migración de pertenencia asignará las tareas existentes a General. No convertirá
las 202 filas analizadas en etapas ni identificará el inventario por coincidencia
de nombres. Las tareas del piloto se seleccionarán o crearán explícitamente.

Las tareas de procesos con flujo quedan fuera de la generación independiente y
de Asignar tarea ocasional. La activación inicia una ejecución del proceso y
habilita su primera etapa; las siguientes requieren el evento configurado.
Publicar el proceso valida tareas activas, roles, reglas, plazos y destinos.
La generación incorpora versión y reservas a capacidad y concurrencia.

Cambiar nombre, reglas o pertenencia no modifica las ejecuciones anteriores.
La pertenencia y configuración utilizadas se conservan en la versión y los
snapshots operativos. Una tarea no puede retirarse de una configuración vigente
sin publicar una sustitución coherente. Desactivar impide nuevos inicios y
conserva las ejecuciones e historia existentes.

## Inventario y orden de trabajo

El piloto conserva febrero/agosto, lunes entre 20 y 25 o día 20 cuando no haya
lunes; PDF; plazos 6/1/1/168 horas y devoluciones a la etapa anterior. La hora
de inicio sigue pendiente de configuración. Se usarán tareas específicas por
rol para registro, revisión de Subcoordinación, revisión de Administración y
aceptación de Dirección.

Primero se ampliarán modelo, contratos y migración de pertenencia; después el
editor de procesos y su integración con crear/editar tareas; a continuación
generación, capacidad, transiciones y permisos pendientes. Finalmente se conectará
la plantilla visual aprobada y se probará el inventario autenticado.
Este documento no acredita implementación de ese editor ni despliegue del motor.
