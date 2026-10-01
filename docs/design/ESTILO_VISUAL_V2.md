# SGOL — Estilo visual v2 y correcciones de revisión

## Estado y precedencia

2026-09-30. El responsable acepta la base v2 mediante «Todas las demas pantallas están correctas, utilizaste bien el diseño que te mandé» y ordena actualizar la referencia operativa. El responsable aprobó después las correcciones y su implementación: «Apruebo las correcciones y el complemento del plan para implementar». Esta guía define la referencia; el código, capturas y comprobaciones se registran por separado en ../traceability/DISENO_RENOVADO_V2_IMPLEMENTACION.md. Publicación e integración requieren evidencia y autorización propias.

Complementa Adenda 55 y precisa sus reglas visuales. Prevalece sobre ejemplos visuales anteriores incompatibles de docs/design y del plan documental. Los contratos funcionales, permisos, mensajes de error, estados y navegación conservan su autoridad. Las tareas futuras consumen esta base sin exigir adaptar todo el sistema ni adelantar FRONT-017..020.

## Reglas compartidas de la base aceptada

- Identidad Loretta: logo encima de SGOL · Gestión operativa, navegación lateral con iconos SVG propios junto a etiquetas. No hay biblioteca nueva. El logo web usa las curvas del EPS aportado por el responsable con relleno oscuro para legibilidad, manteniendo proporciones; el original se conserva.
- Sesión de escritorio a la derecha, avatar decorativo de iniciales del nombre existente, rol, vencimiento y Cerrar sesión con icono. No hay gestión de fotografía, datos extra ni permisos cliente. Orden DOM y foco siguen los contratos existentes; la posición se resuelve con layout.
- Área de trabajo cálida, cabecera y paneles blancos; un h1 operativo en Poppins, paneles con cabecera separada del cuerpo, contenidos completos dentro de padding uniforme.
- Botón primario relleno de acento; secundario neutro sin contorno rojo; acciones auxiliares textuales, navegación Ver tarea con flecha. El foco conserva anillo visible. Peligro se reserva a acciones y estados pertinentes. El ancho habitual es el del contenido; no estirar acciones de planificación a toda una fila. El acceso principal puede ocupar el ancho del formulario.
- Semana recibida del sistema junto al título: número/año y rango legible con mes escrito. Nunca inferir totales o períodos ausentes. Las fechas operativas conservan el calendario y zona del contrato.
- Tablas con caption, th/scope y acciones de cursor; agrupación de tarea/período/origen permite conservar cada valor y etiqueta. Sólo su contenedor desplaza horizontalmente. No transformar tablas en tarjetas ni suprimir datos.
- Poppins local para operación (400/500/600); The Seasons Regular local sólo en bienvenida. La bienvenida usa tamaño propio por token y no transmite ese tamaño a formularios o celdas.
- Se conservan los seis estados, áreas mínimas, reflow, textos ampliados y movimiento reducido. Iconos decorativos aria-hidden y etiqueta visible; no sustituyen el texto. Enlaces de contenido en prosa mantienen subrayado; controles de navegación/acción textual usan área de botón, flecha cuando corresponda y foco visible, sin contorno rojo.

## Correcciones aprobadas

### Detalle de persona

El panel informativo y los paneles de empleo, vigencia y disponibilidad ocupan todo el ancho útil del main; no heredan el límite del formulario de credenciales. Cabecera y cuerpo están separados. Padding del cuerpo: --espacio-24, --espacio-16 en estrecho. El código y su valor no tocan el borde.

Puesto y Turno se distribuyen en dos columnas; Motivo ocupa ambas y Guardar empleo está en su propia fila, al inicio. Gap --espacio-24, --espacio-16 en estrecho. Disponibilidad conserva formularios y acciones independientes, gap y padding uniformes; campos refluyen a una columna. Historia laboral sirve como referencia de ancho y alineación. No se modifica ningún name, asociación, ETag, intención, permiso o mensaje.

### Componente TAR reutilizable

Una sola plantilla compartida, seleccionada con taskCode en /configuracion. Los ocho ejemplos visuales son estados de contenido de ese componente, no nuevas rutas ni ocho implementaciones. Selector Release en borrador y Crear versión en borrador tienen gap --espacio-24, --espacio-16 en estrecho; acción de ancho de contenido y alineación con el control. En móvil se apilan sin tocarse.

No se añade crear una TAR nueva: FRONT-010 y el catálogo MVP limitan a las ocho definiciones aprobadas. La plantilla reutilizable favorece mantenimiento, pero ampliar el catálogo requerirá contrato, reglas, validaciones y alcance aprobados posteriormente.

### Conflicto de plan y Resultado incierto

Separación de --espacio-24 entre consulta y región del mensaje, manteniendo el resumen enfocable, correlationId, acciones existentes y ausencia de reintento automático. Ningún texto contractual cambia.

### Acceso y pasos MFA/recuperación existentes

Composición de dos paneles en escritorio; en estrecho, marca encima del formulario. Logo y SGOL centrados en su panel. Versión debajo de SGOL, procedente de los metadatos del ensamblado Web, sin sufijo de commit, secretos o datos de infraestructura. El artefacto local observado declara 1.0.0; no es una nueva versión comercial asignada por este diseño.

Texto de bienvenida: Tu espacio de trabajo. Ubicación visual solicitada: Delicias, Chihuahua, junto a Loretta · LOR-001. Se mantiene America/Mexico_City para toda operación, fechas y contratos: el texto no migra ni configura la sucursal. Delicias comparte UTC−6 con Ciudad de México actualmente; referencia oficial de zonas: https://www.cenam.mx/hora_oficial/default2.aspx. La ubicación queda visible también en móvil.

## Recursos y situación de incorporación

| Recurso | Procedencia | Incorporación productiva |
|---|---|---|
| Poppins Regular/Medium/SemiBold | Google Fonts oficial, ofl/poppins; licencia OFL | Incorporada localmente; licencia OFL conservada |
| The Seasons Regular | Archivo aportado por el responsable fuera de Fuentes | Uso local privado; pendiente documento de derechos para alojamiento/redistribución web antes de publicación |
| Logo Loretta | EPS aportado por el responsable fuera de Fuentes | SVG derivado conservando curvas, relleno oscuro; original intacto |
| Iconos | SVG propios de línea | Parcial/helper compartido, sin librería |

Recursos incorporados localmente: wwwroot/fonts/Poppins-Regular.ttf, Poppins-Medium.ttf, Poppins-SemiBold.ttf, The-Seasons-Regular.ttf; wwwroot/images/loretta.svg. No se leen ni trasladan archivos congelados. No introducir fuentes remotas. Las reglas y valores de esta guía son autocontenidos; la ruta externa del prototipo no es dependencia operativa de próximas historias.

## Verificación de implementación

Correcciones y complemento aprobados. Orden de implementación: layout, variables y componentes primero, pantallas existentes por grupos, preservando endpoints, sesión, autorización servidor, CSRF, idempotencia, If-Match y cursores.

Comprobar build y pruebas afectadas, sintéticos, teclado/foco/retorno, contraste, controles de 44px, texto ampliado, reflow y movimiento reducido. Actualizar trazabilidad por grupo con evidencia real y causas exactas de validaciones diferidas. No publicar, crear PR, ejecutar gates remotos, merge o despliegue sin autorización expresa correspondiente.

## Precisiones verificables del consumo v2

El helper de Acceso «Ingresa con tu usuario de SGOL.» procede de la propuesta aprobada y acompaña sólo el paso login; no sustituye mensajes de autenticación. Mostrar/Ocultar conserva etiqueta, aria-pressed e icono de ojo; se coloca en fila propia, acción textual, gap --espacio-16. El progreso conserva los mensajes existentes.

Tablas: cabecera blanca, padding --espacio-16 en celdas y --espacio-12 en estrecho; caption legible. Paneles: padding --espacio-24, --espacio-16 estrecho; filtros dentro de superficie elevada con padding --espacio-16 y gap --espacio-12. La bandeja usa --ancho-tabla-bandeja y Ver tarea no se parte en dos líneas. Los avisos conservan nombre/código de tarea y todas sus acciones.

Las palabras largas de paneles pueden partirse para texto al 200 %, sin ocultar datos ni overflow global. Fieldset/legend no imponen mínimos que excedan el formulario. Radio conserva indicador de --espacio-24 y su etiqueta clicable de al menos --alto-control-minimo. Navegación modal mantiene Escape y retorno; Tab/Shift+Tab recorre y envuelve sus controles visibles en ambos motores, sin cambiar permisos ni rutas.

Las tablas conservan partición normal de palabras y pueden exceder el ancho estrecho sólo dentro de su contenedor desplazable; los badges no parten etiquetas en caracteres. Esto evita tablas con columnas de una letra y mantiene legibilidad. Las acciones directas de planificación conservan ancho de contenido también cuando su panel usa grid.
