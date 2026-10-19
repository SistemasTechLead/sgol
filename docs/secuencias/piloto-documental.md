# Piloto: chequeo semestral de inventario

Fecha: 13 de octubre de 2026. Fuente de reglas: definición del usuario en este chat.
Estado: recorrido documental integrado y probado en copia; ejemplo activado por el usuario en el entorno habitual, con Piso reasignado a ana.demo y Dirección a direccion.local. Validación operativa humana en curso.

## Calendario y evidencia

Se realiza en febrero y agosto. Se elige el lunes que caiga entre los días 20 y 25,
ambos inclusive. Si no hay lunes en ese intervalo, se inicia el día 20.
Para el ejemplo, el usuario indicó 11:00 de Ciudad de México y cargas provisionales
de 360/60/60/60 minutos. Dirección conserva su plazo de 168 horas. Los responsables
del ejemplo son cuentas sintéticas; los valores reales requieren selección explícita.
Fechas de calendario: 23 de febrero y 24 de agosto de 2026. La demostración de octubre
se prepara como ocasión del proceso, sin modificar ese calendario.

La hoja se entrega en PDF. La comprobación contra Sizes & Colors la hace el responsable
de la revisión. SGOL guarda el documento, la decisión y su comentario; este alcance
no incluye una conexión a Sizes & Colors ni una comprobación automática del inventario.

Los plazos se cuentan en horas transcurridas desde la habilitación efectiva:
Piso, 6 horas; Subcoordinación, 1 hora; Administración, 1 hora; Dirección, 168 horas.
La semana de Dirección sustituye la indicación anterior de no tener límite.

## Matriz de etapas

| Posición | Acción | Responsable | Entrada y resultado exigido | Condición de avance | Plazo | Devolución |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Registrar inventario completo | Piso de ventas | PDF con el registro completo; en correcciones, resultado de búsqueda de faltantes y sobrantes | Entrega del responsable con PDF y conclusión | 6 horas | No tiene etapa anterior |
| 2 | Revisar inventario | Subcoordinación | Contrastar la hoja con Sizes & Colors; tras una devolución revisar hoja y zapatos; marcar diferencias no resueltas en la versión que entrega | Decisión expresa de pasar a Administración o devolver a Piso | 1 hora | Piso |
| 3 | Chequear hoja e irregularidades | Administración | Revisar toda la hoja y sus diferencias; si tiene información, registrar comentario para que Subcoordinación vuelva a investigar | Decisión expresa de pasar a Dirección o devolver a Subcoordinación | 1 hora | Subcoordinación |
| 4 | Resolver o aceptar resultado | Dirección | Revisar versión recibida; devolver con información adicional, o aceptar el resultado con las diferencias pendientes identificadas | Aceptación expresa del PDF identificado y conclusión final | 168 horas | Administración |

Administración no realiza un sello en este piloto. La aprobación de una etapa no
significa que no existan diferencias: significa que ese responsable entrega el
resultado de su revisión al siguiente. Subcoordinación debe identificar las que
persisten después de buscar, y Dirección puede aceptar el resultado aun con ellas.

Las devoluciones bajan una etapa cada vez. Una observación de Dirección pasa a
Administración, después a Subcoordinación y finalmente a Piso si requiere búsqueda.
Cada devolución exige motivo y solo puede hacerla el responsable de la etapa activa.
No se convierte una observación en una orden arbitraria a otro rol.

La etapa revisora usa una decisión documental propia. Al integrar con Supervisión,
su aceptación y la aceptación de la entrega de entrada se guardan juntas, con el
actor real; no se exige otra aprobación equivalente para avanzar.

## Recorrido favorable

Piso registra todo el inventario y entrega V1. Subcoordinación contrasta V1 con
Sizes & Colors y registra su revisión. Administración chequea el documento recibido
y pasa el resultado a Dirección. Dirección acepta la versión exacta que recibió.
Cuando una etapa modifica la hoja debe entregar otro PDF; una revisión sin cambio
puede conservar la versión recibida y añadir su decisión identificada.

El proceso termina como «Sin diferencias» o «Aceptado con diferencias pendientes».
La segunda opción conserva las diferencias y la conclusión de Dirección; no las
presenta como resueltas ni elimina su historia.

## Recorrido con devolución

Ejemplo ilustrativo para pruebas, no un inventario real: V1 contiene un faltante
de dos pares y un sobrante de un par. Subcoordinación devuelve a Piso con el motivo
de buscar los faltantes y llevar los sobrantes para contrastarlos con la hoja.
Piso entrega V2 con el resultado de esa búsqueda. Subcoordinación vuelve a revisar
la hoja junto con los zapatos y marca lo que aún no se resolvió.

Administración recibe V2 revisada. Si conoce un movimiento que explica la diferencia,
devuelve a Subcoordinación con ese comentario. Si se requiere otra búsqueda,
Subcoordinación devuelve a Piso. Piso produce V3 y el tramo posterior se ejecuta
nuevamente. Las revisiones de V1 y V2 se conservan pero no autorizan V3.

Dirección puede hacer lo mismo a través de Administración, o aceptar la última
versión con una conclusión expresa aun si persisten diferencias. Una aceptación
final impide devoluciones tardías; una nueva investigación requiere otra ejecución.

## Límites y datos de configuración

La afirmación «inventario completo» debe formar parte de la entrega de Piso. Un PDF
por sí mismo no permite al servidor comprobar que se contaron todos los artículos.
La validación operativa se realiza contra la hoja y Sizes & Colors.

No se configuraron IDs de tareas ni responsables reales. No se inventarán vínculos
con `TAR-0069` (inventario anual), `TAR-0085` (conteo mensual) o `TAR-0131` (lectura
por zonas), cuyos nombres no prueban que sean las cuatro tareas de este proceso.
La configuración de Dirección elegirá tareas compatibles y conservará snapshots
de rol, especialización, requisito y carga para cada ejecución.

El libro local conserva 202 nombres y códigos, sin campos operativos llenados ni
comentarios. La [cobertura](clasificacion-catalogo.csv) permanece pendiente para el
resto del catálogo. La [evidencia](evidencia-catalogo.json) registra su huella original.
No se actualiza la guía o el Excel hasta estabilizar los campos finales, conforme al plan.

## Ejemplo integrado

Desde Planificación se selecciona el proceso, se cargan sus cuatro tareas y se guarda
un plan con capacidad reservada. Activar ese plan habilita solo la primera etapa,
respetando el inicio previsto. La corrección de Piso requiere otra revisión de
Subcoordinación antes de Administración. [Prueba manual](prueba-manual-inventario.md)
y [verificación técnica](verificacion-procesos.md).
