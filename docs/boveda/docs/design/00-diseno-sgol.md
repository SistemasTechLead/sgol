---
categoria: diseno
tipo: referencia
estado_documental: inspeccionado
clasificado_el: 2026-11-03
---

# Diseño actual de SGOL

SGOL combina una identidad cálida de Loretta con pantallas operativas compactas, navegación por rol y controles compartidos. Esta referencia describe la interfaz implementada en el código local de SistemaSGOL revisado el 3 de noviembre de 2026. Los cambios están incluidos en el commit 464d283. La comprobación de procedencia y sus límites se registran en la base documental de la bóveda; esta referencia no certifica una prueba visual completa del host.

## Consultar el diseño

| Documento | Qué define |
|---|---|
| [Fundamentos visuales](01-fundamentos-visuales.md) | Marca, tipografía, colores, temas, medidas y tokens. |
| [Estructura y navegación](02-estructura-y-navegacion.md) | Cabecera, barra lateral, menú móvil, roles, apartados y composición adaptable. |
| [Componentes](03-componentes.md) | Botones, campos, tablas, filtros, ayudas, diálogos, evidencias, avatares y medidores. |
| [Patrones y estados](04-patrones-y-estados.md) | Consulta, selección, revisión y confirmación; estados temporales, errores y resultados. |
| [Pantallas](05-pantallas.md) | Inventario completo de rutas y composición por módulo. |
| [Interacción accesible](06-interaccion-accesible.md) | Teclado, foco, nombres accesibles, reflujo y comportamiento de interacción implementado. |

Los enlaces entre estas notas permiten recorrer el diseño y visualizar sus relaciones en el gráfico de Obsidian. Cada tema tiene una ubicación principal: las pantallas enlazan los componentes y patrones que utilizan en lugar de repetir sus definiciones.

## Cómo interpretar esta referencia

La fuente de comportamiento es la combinación de vistas Razor, componentes de presentación, CSS y JavaScript. Un valor de token no garantiza que todos los componentes lo usen sin ajustes: las variantes relevantes se explican junto al componente. Los permisos de navegación no equivalen a permiso para ejecutar todas las acciones de una pantalla.

La revisión realizada es documental y estática. Se comprobaron rutas, archivos, valores y relaciones del código; no se ejecutó una nueva validación visual autenticada de todas las pantallas, temas, roles o dispositivos. La accesibilidad descrita corresponde a mecanismos implementados, no a una certificación de conformidad.

Esta carpeta contiene la referencia de diseño de la bóveda. Los documentos funcionales del resto de la bóveda explican reglas de negocio; su cronología no redefine la presentación aquí descrita. La copia de la bóveda no se sincroniza automáticamente con el repositorio.

## Organización adoptada

Se separan fundamentos, piezas reutilizables y recorridos de usuario, con un inventario de pantallas propio de SGOL. La separación toma como referencia las secciones de [estilos](https://design-system.service.gov.uk/styles/), [componentes](https://design-system.service.gov.uk/components/) y [patrones](https://design-system.service.gov.uk/patterns/) de GOV.UK. Carbon también distingue los componentes de los [patrones que los combinan para resolver una tarea](https://www.carbondesignsystem.com/building-blocks/core/patterns/overview). Se adopta esa organización documental, no sus colores ni sus controles.

## Mantener una sola versión

Cuando cambie una pantalla, se actualiza directamente su descripción y, si corresponde, el componente o patrón compartido. Las decisiones sustituidas y los resultados de pruebas quedan en los registros de ingeniería del repositorio, no como adendas dentro de esta referencia. Una propuesta aún no implementada no se incorpora como comportamiento actual.

Para contrastar una diferencia, empezar por [el layout](../../../../src/Sgol.Web/Pages/Shared/_Layout.cshtml), [los tokens](../../../../src/Sgol.Web/wwwroot/css/tokens.css) y el archivo de la pantalla indicado en [el inventario](05-pantallas.md).

## Ubicación documental

Pertenece a [Diseño e interacción](../../mapas/diseno.md). Estado documental: **inspeccionado**. La fecha de clasificación no es una fecha de verificación funcional.
