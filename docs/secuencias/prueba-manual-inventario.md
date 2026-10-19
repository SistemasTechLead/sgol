# Prueba manual del ejemplo de inventario

El ejemplo usa datos y cuentas de prueba, PDF y revisión manual de Sizes & Colors.
El plan se guardó preparado y luego el usuario lo activó; actualmente Piso está
disponible para Ana y las etapas posteriores permanecen en espera.

La hora prevista de ejemplo es 11:00 de Ciudad de México. Si el plan se activa después, las seis horas de Piso comienzan al habilitarse efectivamente. Cargas provisionales autorizadas: Piso 360, Subcoordinación 60, Administración 60 y Dirección 60 minutos. Plazos: 6, 1, 1 y 168 horas.

Responsables actuales, por petición posterior del usuario: `ana.demo` (Ana · prueba,
Piso) y `direccion.local` (Dirección · pruebas locales). Subcoordinación conserva
`inventario.ejemplo.2` y Administración `inventario.ejemplo.3`; sus accesos privados
están en `.local/inventario-ejemplo-accesos-sgol_local.txt`. No se cambiaron contraseñas
ni se atribuyó el ejemplo a participantes reales.

El usuario activó el plan y posteriormente pidió estas reasignaciones. Piso tiene
un nuevo intento disponible desde 13/10/2026 17:47 CDMX y conserva el vencimiento
original de 13/10/2026 23:21. Ana tiene capacidad temporal de 385 minutos diarios
del 13 al 21 de octubre de 2026 inclusive: 360 para inventario y 25 de carga pendiente
comprobada al ajustar. Después de esa vigencia vuelve a su capacidad habitual.

1. Con `direccion.local`, abrir Planificación y **Demostración · Plan de chequeo de inventario**. El ejemplo actual ya está activado; abrir su ejecución. Solo Piso está disponible. Para otro plan preparado, revisarlo y usar **Activar plan e iniciar el flujo**.
2. Con Piso, abrir **Mi trabajo → Mis procesos**, adjuntar [PDF V1 de ejemplo](../../outputs/procesos/inventario-ejemplo-v1.pdf), declarar el registro y entregar con conclusión.
3. Con Subcoordinación, abrir el PDF, indicar faltantes/sobrantes y devolver a Piso con motivo y diferencias pendientes.
4. Con Piso, comprobar el motivo y las diferencias recibidas, adjuntar [PDF V2 corregido](../../outputs/procesos/inventario-ejemplo-v2.pdf) y entregar nuevamente.
5. Con Subcoordinación, revisar **V2**, conservar las diferencias que persistan y pasar a Administración. La aprobación anterior no valida la hoja corregida; este paso no se omite.
6. Con Administración, revisar la hoja e irregularidades, registrar conclusión y pasar a Dirección.
7. Con Dirección, abrir V2, registrar conclusión y la lista de diferencias que acepta, y elegir **Aceptar con diferencias pendientes**.
8. Comprobar cierre, documento autorizado V2, conclusión y diferencias, junto con V1 y los intentos anteriores en el historial. Un reenvío no debe cerrar dos veces ni abrir trabajo adicional.

La prueba autenticada automatizada del recorrido original se ejecutó en una copia
dedicada. La reasignación se comprobó mediante pruebas del servicio y en otra copia
recuperada antes de aplicarla al entorno habitual, donde sigue la validación humana.
No se incorporaron las demás tareas del Excel a modo de relleno; su
[cruce documental](tareas-definidas/lectura-y-cruce.md) conserva códigos, tiempos,
coincidencias y datos pendientes.
