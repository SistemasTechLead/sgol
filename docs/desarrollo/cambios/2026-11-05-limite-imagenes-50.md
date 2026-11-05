# Rango de imágenes de 1 a 50

Cambio funcional con ajustes de atributos y mensaje en interfaz, sin cambios de composición.

## Decisión y alcance

El usuario pidió ampliar el rango de 20 a 50 y confirmó que se aplique solo al crear o editar tareas. Las tareas nuevas toman 50 como máximo predeterminado; al editar una existente se conserva su valor hasta cambiarlo explícitamente. No se actualizan masivamente catálogo, asignaciones, versiones de procesos ni evidencia histórica. Se mantienen 15 MiB por archivo y 30 MiB por entrega.

## Implementación y recorrido

- `OperationalTasks.cs`: validación de mínimo/máximo, valores iniciales y restricciones del catálogo/asignación.
- `OperationalTaskService.OccasionalAccess.cs` y `DirectionPreparedPlans.cs`: valores predeterminados de contratos.
- `OperationalSequences.cs`: definición capturada de etapas, rango SQL compatible con 50.
- `OperationalPhotographs.cs`: posiciones persistidas 0–49; sin esta ampliación fallaba guardar la imagen 21.
- `Pages/Tasks/Index.cshtml` y `Pages/Occasional/Index.cshtml`: ambos controles numéricos admiten 1–50; mensaje compartido actualizado.
- Migración `ExpandOperationalImagesToFifty`: cambia restricciones y valores predeterminados; no contiene actualización de registros.
- Los generadores, actualización explícita de asignaciones y publicación de procesos copian los parámetros del catálogo. Cámara, capturas, entrega, continuación de procesos y supervisión consultan el máximo capturado. Galerías consultan la colección persistida sin un corte fijo de 20.

## Escenarios y evidencia

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Crear catálogo 50 y rechazar 51 | Guardado válido / error de parámetros | Servicio real, PostgreSQL aislado, `--image-limits-only` | Aprobado |
| Cámara: cargar 50, rechazar 51, entregar y revisar | Persistir 50; rechazar exceso y conjunto incompleto | Servicio real y JPEG válido; escáner simulado limpio | Aprobado |
| Capturas: 51 y luego 50 | Rechazo sin escrituras parciales; guardar y consultar 50 | Servicio real, PostgreSQL aislado | Aprobado |
| Asignación existente con máximo 20 | Mantener 20 | Consulta posterior a crear/entregar otras tareas | Aprobado |
| Modelo y migraciones | Sin diferencias pendientes | EF, base aislada | Aprobado |
| Formularios reales y navegador | Crear/editar 50 y límites HTML; tamaños/temas | No ejecutado | Pendiente |
| Publicación de procesos con 50 | Copiar definición y ejecutar | Inspección de productores/consumidores; sin integración nueva | Pendiente |
| Host compartido | Cargar artefacto nuevo y aplicar migración | Publicación y migración verificadas; HTTP 200 interno/local/LAN | Aprobado |

La primera suite general no alcanzó las pruebas de imágenes: `PLAN_CAPACITY_STALE` en creación del plan. Se agregó una entrada focalizada independiente de planificación. La primera ejecución focalizada detectó `CK_operational_photo_position`, corregida en modelo y migración. Logs privados: `.local/images50-photos.log`, `.local/images50-focused.log`, `.local/images50-final.log`. Bases independientes `sgol_task_checks_images50_*_20261105`; no se escribieron datos en la simulación compartida.

Compilación de Web y TaskChecks aprobada durante la ejecución focalizada. La prueba usa servicios y persistencia reales, pero no formularios HTTP ni navegador; el escáner antivirus es un doble de prueba.

## Estado documental y publicación

Publicado por petición explícita del usuario en `.local/runtime/images50-20261105/Sgol.Web.dll`; Web 14440, lanzador 39164, proxy 20652 conservado. SHA-256 idéntico al ensamblado probado: `88E3213E66F8A3BE6FF776E98FB4F34B5B72263A0B3853A3415DBFD053ACE8FD`. Migración aplicada sobre `sgol_local` después de respaldo privado. Comparación de las huellas completas de filas de las cuatro tablas afectadas: iguales. Readiness interno y HTTPS local/LAN: 200, TLS normal. Se actualizaron catálogo, ejecución, componentes, instalación y base documental en bóveda y copia versionada, conservando cambios previos. No se realizó carga de evidencia de prueba en el host compartido.

La reversión de la migración requiere que no existan límites mayores de 20 ni posiciones mayores de 19: las restricciones antiguas rechazan esos datos; no se recorta ni borra evidencia automáticamente.
