# Normalización móvil de SGOL y verificación

20 de octubre de 2026. Implementación del [plan aprobado](plan-normalizacion-movil.md).

La adaptación está servida por el host activo. Las pantallas móviles comparten cabeceras compactas, filtros plegables y registros operativos verticales con acciones accesibles. Mi trabajo conserva su presentación específica; Configuración mantiene su composición. La comparación visual de las cinco pantallas prioritarias está en la [galería antes y después](../outputs/normalizacion-movil-20261020/index.html).

## Cambios realizados

- Supervisión: filtros dentro de la cabecera de cada apartado, selector de apartados y registros con Supervisar visible. La capacidad conserva el mismo elemento y sus formularios, y pasa después de las colas en móvil cuando no hay una consulta o vista previa abierta que deba atenderse.
- Tablero: título, fecha y accesos a Filtros y Personas en una cabecera breve; progreso diario y semanal compacto; progreso por persona desplegable. Las tareas se leen en una columna y el desplazamiento móvil ocurre en la página. El comportamiento de exhibición en escritorio se conserva.
- Reportes: resultados accesibles antes de la configuración extensa; consulta resumida, detalle desplegable y exportación visible. El botón original Guardar plantilla queda dentro de la configuración móvil, con el mismo listener y diálogo; vuelve a su posición original en escritorio. La generación del PDF conserva el código existente.
- Planificación: registros verticales y acciones auxiliares bajo Más opciones. Tablas de detalle, incidencias y confirmaciones usan la misma adaptación. Las matrices de capacidad generadas dinámicamente conservan todos sus controles editables.
- Personas y accesos: resumen bajo demanda, filtros en la cabecera y acciones dentro de cada perfil. Formularios, horarios, excepciones e historial conservan sus controles.
- Menú: ancho limitado al contenedor de la vista elegida y al dispositivo; alto limitado al espacio dinámico disponible, scroll interno y cierre accesible. Corregido el caso reproducido de vista móvil forzada en una ventana amplia, donde el menú excedía el ancho de la aplicación.
- Consulta semanal: las mismas celdas pueden leerse verticalmente y seleccionar un día para su presentación. Día elegido y foco se conservan después de refrescar; la matriz completa permanece en escritorio.
- Base compartida: controles táctiles, lectura de campos, nombres largos, archivos, diálogos, foco y errores de validación. La vista automática de un teléfono táctil en horizontal mantiene la composición móvil; la preferencia explícita Escritorio conserva su comportamiento.

## Protección del alcance

Se añadió `wwwroot/css/mobile.css`, acotado a pantalla y al contenedor móvil, y `wwwroot/js/mobile.js`, dedicado a presentación. El módulo no hace peticiones, no cambia valores de campos y no reemplaza servicios o scripts de negocio. Se inicializa únicamente al entrar en una composición móvil.

En archivos existentes de la aplicación solo se añadieron referencias a estos recursos en `Pages/Shared/_Layout.cshtml`, `Pages/Shared/_BoardLayout.cshtml` y `Pages/Screen/Index.cshtml`. La comparación SHA 256 contra la línea base de 516 archivos de la aplicación confirmó que los otros archivos existentes permanecieron iguales durante este trabajo. No se modificaron PageModels, servicios, contratos, handlers, endpoints, autenticación, migraciones ni persistencia.

Los formularios y tablas mantienen un solo origen. Los registros conservan sus celdas y acciones; el botón de datos adicionales no añade texto a la búsqueda existente de Reportes. Se mantienen roles y asociaciones de encabezados al cambiar la distribución visual. Los paneles plegados no deshabilitan campos. La validación revela los controles obligatorios antes de enfocarlos.

## Cobertura de las pantallas

Las 23 páginas Razor actuales quedaron incluidas en el inventario y la revisión. Las comprobaciones de navegador recorrieron 38 rutas y variantes, incluidos detalles, edición, alias y estados de error.

| Página | Comprobación y alcance |
| --- | --- |
| Entrada `/` | Navegación y contenido disponible con sesión. |
| Acceso | Acceso anónimo a 320 px y acceso real con cuatro cuentas existentes; alias de acceso con sesión. Formularios de etapas adicionales revisados en fuente, sin alterar MFA o recuperación. |
| Mi trabajo | Lista, acciones y presentación específica conservadas. |
| Detalle heredado de tarea | Estado de error legible de la ruta heredada; véanse los límites de cobertura. |
| Fotografías | Galería de una asignación existente, respuesta 200. |
| Mis procesos | Lista o estado vacío actual y componentes de etapas revisados. |
| Tablero | Cabecera, progreso, filtros, refresco y foco. |
| Rol de tareas semanal | Tablas, totales, día seleccionado y refresco. |
| Supervisión | Listas, detalle existente, filtros, capacidad y validación. |
| Asignar tarea | Lista y formulario ocasional existentes. |
| Tareas | Catálogo, creación y edición existentes. |
| Asignación de tarea | Formulario con un identificador de catálogo obtenido del sistema, respuesta 200. |
| Catálogo de procesos | Composición de lista/editor y componentes adaptables existentes. |
| Planificación | Lista, creación normal, operaciones semanales y detalle actual. |
| Planes de procesos | Pantalla de selección o estado actual y fuente del recorrido de preparación y activación. |
| Versiones | Historial, formulario y componentes de confirmación. |
| Reportes | Filtros, búsqueda, plantillas, PDF e impresión. |
| Personas y accesos | Lista, creación, edición y navegación. |
| Detalle de persona | Persona existente y formularios de empleo, vigencia, disponibilidad e historial. |
| Especializaciones | Lista y formulario de creación. |
| Configuración | Preferencias de vista y tres temas; composición conservada. |
| Sucursal | Detalle actual con respuesta 200. |
| Pantalla dedicada | Pantalla de conexión y layout de exhibición revisados; flujo de conexión sin cambios. |

## Resultados de las verificaciones

- Compilación Release del proyecto web: correcta, sin errores ni advertencias.
- 342 comprobaciones de rutas y tamaño: 38 variantes por nueve tamaños, de 320 a 1920 píxeles CSS. Sin desbordamiento de página ni acciones fuera del ancho en las composiciones móviles probadas; controles de adaptación ocultos en escritorio.
- 28 comprobaciones adicionales con cuatro roles existentes: Dirección, Administración, Subcoordinación y Piso de Ventas, incluidas respuestas de acceso denegado.
- 25 comprobaciones enfocadas de las cinco pantallas prioritarias a 320, 390, 430, 768 y 1440 píxeles, más apertura del diálogo original de guardado de plantilla.
- Apertura y cierre de paneles y datos adicionales: mismos campos y valores de `FormData`.
- Campo obligatorio dentro de capacidad cerrada: panel abierto y campo enfocado por la validación nativa.
- Matriz de capacidad insertada dinámicamente: 31 campos de día visibles dentro del ancho móvil, con valores y obligatoriedad conservados.
- Menú: tamaño dentro de la vista, desplazamiento interno en orientación horizontal, áreas táctiles de los enlaces, Escape y retorno de foco.
- Tablero: paneles abiertos y foco conservados tras refrescar, sin controles duplicados.
- Semanal: día elegido y foco conservados tras refrescar.
- Reportes: búsqueda original, generación de PDF e impresión con los controles originales disponibles.
- Preferencias: móvil forzado en monitor, escritorio forzado en teléfono, modo automático táctil horizontal y temas claro, oscuro y especial.
- Escritorio a 1440 píxeles, con los mismos datos congelados: 25 de 25 capturas idénticas píxel a píxel antes y después. También coinciden posiciones, dimensiones, tipografía declarada y valores de los controles. El módulo deja intacto el DOM de las vistas que se abren directamente en escritorio.
- Sin errores de ejecución JavaScript ni escrituras operativas durante las comprobaciones de interfaz. El ejecutor principal bloquea peticiones que no sean GET o HEAD; los accesos de prueba se prepararon por separado.
- Host activo: `/health/ready` devuelve estado `ready`; acceso y rutas autenticadas responden y sirven los recursos nuevos.

Las dimensiones de prueba fueron 320 × 568, 390 × 844, 430 × 932, 768 × 1024, 769 × 1024, 1024 × 768, 1280 × 900, 1440 × 1000 y 1920 × 1080. La comparación de escritorio usa datos congelados, dado que horas, estados y porcentajes cambian en el sistema activo.

### Altura inicial en móvil

Con datos actuales y sin alertas, a 390 × 844 píxeles CSS:

| Pantalla | Posición vertical del primer registro o resultado útil |
| --- | --- |
| Supervisión | 290 px |
| Tablero | 272 px |
| Reportes | 287 px |
| Planificación | 287 px |
| Personas y accesos | 299 px |

Estos valores sitúan el trabajo principal aproximadamente dentro del primer 35 % del alto. Los registros no requieren desplazamiento horizontal para operar. En datos extensos, títulos largos o texto ampliado se prioriza la lectura completa.

## Evidencia y repetición de las pruebas

La galería contiene capturas de la primera vista; no pretende mostrar todos los registros en una imagen. La línea base, estados autenticados, formularios, mediciones y capturas completas permanecen en `.local/mobile-normalization`. Los estados de sesión son privados y no se incluyen en la galería ni en archivos versionados.

El ejecutor reutilizable está en `tools/Sgol.MobileChecks/mobile-checks.cjs`. Requiere Playwright y un directorio privado con `origin.txt`, `routes-extended.json`, `state-alex.json` y, para la matriz de roles, `roles.json` con sus estados de navegador. Ejecutarlo desde la raíz con el runtime de Node y las dependencias disponibles en el equipo:

```text
node tools/Sgol.MobileChecks/mobile-checks.cjs <directorio privado de verificación>
```

El resultado principal se registra en `verification/results.json`; la comparación de escritorio y el registro de huellas se conservan junto a la línea base. No volver a generar la línea base para comparar cambios ya realizados.

## Límites de la validación

La ejecución de navegador se hizo con Chromium de Microsoft Edge y emulación táctil. Safari y VoiceOver en un iPhone físico, el teclado y la cámara reales y la ampliación de texto del sistema requieren validación en ese dispositivo. No se declara una certificación de accesibilidad ni una validación física de iOS.

El detalle heredado `/mi-trabajo/tareas/{obligationId}` se ejercitó en su presentación de error: con el identificador operacional disponible respondió 503. No se obtuvo una obligación heredada válida para probar ese detalle poblado. Los procesos sin datos actuales y las etapas MFA adicionales se revisaron en fuente; no se crearon registros operativos ni se modificó seguridad para fabricar escenarios.

Las acciones que guardan, revisan, reasignan, activan o eliminan datos se verificaron mediante conservación de contratos y controles. No se ejecutaron escrituras de negocio en el sistema operativo para probar una adaptación visual.
