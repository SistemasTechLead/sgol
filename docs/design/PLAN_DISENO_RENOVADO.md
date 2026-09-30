# SGOL — Plan documental de diseño renovado con CSS propio

## Precisión vigente del estilo v2

La base visual v2 está aceptada y documentada en [ESTILO_VISUAL_V2.md](ESTILO_VISUAL_V2.md), por solicitud expresa de actualización del responsable el 2026-09-30. Sus reglas precisan los ejemplos anteriores: secundario neutro sin borde rojo, fuentes locales, logo/iconos, sesión a la derecha y acceso con composición de marca/formulario. Las cuatro correcciones solicitadas siguen en revisión visual; no hay nueva implementación productiva por este registro. Los contratos y mensajes funcionales se conservan.

## 1. Control, evidencia y alcance

Fecha: 2026-09-30. Estado: **PLAN APROBADO — INCORPORADO DOCUMENTALMENTE EN LOCAL**.

La elección de «CSS propio renovado» está aprobada por la solicitud del responsable. La aprobación posterior «Apruebo las secciones y la adenda» cubre íntegramente §§3–9 y Adenda 55. Se incorporan en docs/design/referencia-renovada.md y los documentos operativos; no se introduce Tailwind, daisyUI ni otra biblioteca. §2 conserva el contraste previo; las decisiones exactas aprobadas son las de §§3–9.

Esta tarea redacta la referencia y su transición. No modifica vistas, CSS productivo, JavaScript, backend, dependencias ni pruebas funcionales. Tampoco inicia FRONT-017..020 ni adapta pantallas existentes. No requiere una tarea funcional nueva ni renumerar HU, CA, CP, BR o NAV.

Se acepta el registro vigente inicial de [IMPLEMENTATION_STATUS.md](../traceability/IMPLEMENTATION_STATUS.md): FRONT-016 implementada localmente y Publicada, con evidencia previa aceptada; integración y gates de cabeza pendientes según ese registro. No se repiten sus gates. El checkout inicial es `ce76ab888ec7d8c9d2f0fc2461522d917da61340`, rama `codex/front-016`, árbol limpio. La tabla «Tareas insertadas por adenda» conserva las dependencias y FRONT-017..020 no iniciadas. Ninguna dependencia de código impide preparar esta documentación.

Preflight informa árbol y Fuentes limpios, y falta del SDK exacto 10.0.400 frente al 10.0.401 disponible. No se diagnostica ni modifica el entorno. No se necesita compilación para esta tarea documental.

Fuentes operativas leídas: los cinco documentos de diseño, [navegacion.md](navegacion.md), [instrucciones FRONT](../../INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md), índice de IDs, filas 94–96 y 102–103 de Adenda 45, Adenda 54 y secciones acotadas de arquitectura relativas a Razor, API e invariantes. No se altera ningún criterio CA/CP ni contrato funcional.

Referencia externa leída, sin copiarla a producción: `C:/Users/siste/.codex/visualizations/2026/09/30/01a0f38e-1500-7c02-a157-70773b457fa7/sgol-diseno-front016/native.html`; complementos `native.css`, `shared.css`, `tokens.css`, ambas capturas, cuatro `source-*.cshtml` y `README.md`. Las capturas representan maquetas con datos sintéticos, no implementación ni conformidad WCAG integral. La referencia oficial resultante será autocontenida en el repositorio; no dependerá de esa ruta local.

## 2. Contraste y decisiones existentes

| Hecho o diferencia | Clasificación | Tratamiento propuesto |
|---|---|---|
| La paleta, familias tipográficas, radios, sombras y escala de espaciado coinciden con docs/design | Decisión existente | Conservar valores y significados; renovar su composición |
| Fondo de trabajo cálido y paneles blancos, navegación lateral blanca | Propuesta de usos | Documentar superficies por función; mantener tablas claras y densas |
| La maqueta usa título de página mayor que el vigente | Diferencia de token pendiente | Resolver exclusivamente con §3, sin copiar tamaños sueltos |
| La maqueta abre enlaces móviles en línea y oculta el pie lateral | Contradicción con navegación móvil modal y conservación de controles vigentes | Conservar navegación modal, Escape, foco y retorno; no adoptar ocultación de información necesaria |
| La maqueta omite o simula consultas, acciones y datos de las vistas originales | Diferencia funcional excluida | No copiar permisos, textos, columnas, colapsos o comportamientos por arrastre; preservar contratos y totalidad de datos autorizados |
| Consulta autorizada colapsada y avisos convertidos a lista en la maqueta | Propuestas de composición no aprobadas | No imponerlas. Las tablas vigentes siguen semánticas; cualquier cambio de agrupación deberá justificarse en el plan posterior sin pérdida de datos ni comportamiento |
| Barra de comparación, selector de muestra, diálogo de demostración y acciones simuladas | Exclusión expresa | No incorporarlos al diseño operativo |
| Estados, mensajes y autoridad ya aprobados | Decisión existente | Conservar literalmente; la maqueta no los reemplaza |
| Ejemplos antiguos mencionan paginación numérica, modal genérico o detalle técnico crudo | Tensión interna con extensiones aprobadas posteriores | Explicitar que cursor, dialog nativo y catálogo seguro consumidor prevalecen sobre ejemplos ilustrativos; no crear capacidades de ordenación, borrado o consulta técnica |

No se resolvió silenciosamente una contradicción: §§3–9 se sometieron a aprobación y fueron aprobadas mediante «Apruebo las secciones y la adenda». Cualquier contradicción funcional adicional detiene sólo esa parte y se presenta al responsable.

## 3. Tokens exactos y usos permitidos — aprobado

Conservar todos los valores actuales de colores de marca, superficies, bordes, texto, acento, semánticos, fuentes, espaciado, radios, sombras y anchos máximos. Mantener las denominaciones funcionales españolas. No incorporar los aliases ingleses ni todos los tokens auxiliares de la maqueta.

| Token | Cambio o valor propuesto | Uso permitido |
|---|---|---|
| `--tipografia-titulo` | Cambiar de `700 24px/32px var(--fuente-base)` a `600 32px/40px var(--fuente-base)` | h1 de página; nunca tablas, campos ni badges |
| `--tipografia-titulo-compacto` | Nuevo: `600 24px/32px var(--fuente-base)` | h1 en viewport estrecho; título de bienvenida de marca |
| `--grosor-borde` | Nuevo: `1px` | Divisores y contornos existentes |
| `--grosor-foco` | Nuevo: `2px` | Anillo de foco |
| `--desfase-foco` | Nuevo: `2px` | Separación del anillo |
| `--alto-control-minimo` | Nuevo: `44px` | Alto y área mínima de interacción; ancho mínimo en controles de icono |
| `--tamano-icono` | Nuevo: `20px` | Iconos de controles y navegación |
| `--tamano-icono-grande` | Nuevo: `32px` | Icono de vacío o mensaje de sección |
| `--ancho-navegacion` | Nuevo: `224px` | Columna lateral de escritorio; no mínimo de página |
| `--alto-esqueleto` | Nuevo: `16px` | Bloque de carga dentro de una fila reservada; no altura universal de fila |
| `--duracion-esqueleto` | Nuevo: `1.4s` | Animación existente, anulada con movimiento reducido |
| `--peso-medio` | Nuevo: `500` | Nombre de registro, navegación y encabezados de columna |
| `--peso-fuerte` | Nuevo: `600` | Jerarquía puntual de valores y títulos |

Las restantes tipografías permanecen sin cambio. `--tipografia-grande` corresponde a h2 de sección y `--tipografia-media` a h3; texto primario para contenido y secundario para contexto. La precisión v2 carga Poppins y The Seasons locales aportadas fuera de Fuentes; conserva fallback y no introduce recursos remotos.

Uso nuevo de `--color-superficie-elevada`: fondo general del área autenticada. Navegación, encabezado de sesión, paneles y tablas usan `--color-superficie`. La superficie cálida puede reforzar navegación activa, encabezados de tabla o hover. Marca decorativa sólo en identidad; nunca controles. No se colorea un panel entero según el estado de una obligación.

Las propiedades visuales consumidoras se expresan mediante variables, incluyendo las nuevas primitivas de borde, foco y área. No se importa un mínimo universal de tabla, anchos por columna ni tiempos de muestra. La precisión v2 documenta avatar decorativo y ancho propio de bandeja en tokens.md. La excepción documentada de `@media` en `48rem` se conserva, porque CSS no admite variables como umbral de media query.

## 4. Layout, navegación y encabezados — aprobado

Composición de escritorio: encabezado de sesión compartido, columna lateral y área de trabajo con ancho máximo `--ancho-contenido`. Preservar el orden semántico vigente: salto al contenido, identidad/sesión/logout, navegación y main. La posición visual no altera tabulación ni lectura. Acceso y MFA siguen fuera del shell, con composición `--ancho-acceso` y límites internos de `--ancho-formulario`, conforme a ESTILO_VISUAL_V2.md.

Área de trabajo: padding `--espacio-32` en escritorio y `--espacio-16` en estrecho. Navegación: ancho `--ancho-navegacion`, fondo blanco, separador `--color-borde`. Los items conservan texto, icono, área mínima, acento y `aria-current` para activo, con señal adicional de borde. No se agregan enlaces, permisos, roles, datos de sucursal ni consultas sólo para decorar.

En teléfono se conserva el panel de navegación modal existente, su disparador, Escape, foco contenido y retorno. Identidad, rol, expiración y logout refluyen y permanecen disponibles. No se adopta la navegación móvil abierta de la maqueta.

Encabezado de página compartido: un h1, descripción aprobada cuando exista y contexto confirmado opcional (por ejemplo período), con acciones autorizadas. No agrega frases decorativas, breadcrumb nuevo, contadores totales o período supuesto. Si se muestra un conteo, debe expresar exactamente lo recibido; una página con cursor no acredita un total.

Panel de sección compartido: fondo blanco, borde decorativo, `--radio-tarjeta`, `--sombra-baja`; cabecera con h2, ayuda y acciones, padding `--espacio-24`. Filtros en banda propia con gap `--espacio-12`, padding vertical `--espacio-16` y horizontal `--espacio-24`. Separación de secciones `--espacio-24`; del encabezado a contenido `--espacio-32`. En estrecho cabecera/filtros usan `--espacio-16` y reflow.

Una columna es la composición predeterminada. Dos paneles independientes pueden compartir fila sólo si su contenido cabe sin recortes; en estrecho pasan a una columna en el mismo orden. Las tablas densas ocupan todo el ancho disponible. No se copian proporciones o colapsos de la maqueta como regla universal.

## 5. Componentes compartidos — aprobado

| Componente | Referencia renovada y límites |
|---|---|
| Botones | Mantener primario, secundario y destructivo. Una acción primaria por bloque. Añadir variante textual para acciones auxiliares: acento, fondo transparente, mismo padding/radio/área; hover con superficie elevada y foco completo. No usar un botón para simular navegación ni enlace para mutar |
| Campos | Mantener input, select, textarea, fecha, credencial, checkbox y radio nativos con sus etiquetas, ayudas y validación. Fondo blanco, borde fuerte, radio de control, padding de escala existente y área mínima. Los controles no heredan el tamaño de un título en móvil |
| Tablas | Mantener caption, th/scope, columnas y cursor Anterior/Siguiente. Cabecera cálida y divisores suaves. Contenido simple: padding vertical `--espacio-8`, horizontal `--espacio-12`; celdas con varios datos: `--espacio-16`, en estrecho `--espacio-12`. Acciones siempre con área mínima |
| Agrupación de datos | Código/contexto en pequeña, nombre en base y peso medio, metadatos secundarios; permitir agrupación en celda sólo preservando asociación, etiquetas y todos los valores. Sin truncar datos esenciales ni convertir tablas a tarjetas |
| Badges | Mantener pastilla, texto e icono, colores de estados-de-dominio. Permitir grupo con wrap y gap `--espacio-8`; estado base y bandera permanecen distintos. No usar badge como permiso ni operación |
| Alertas | Mantener estructura común con texto e icono y par semántico aprobado; radio de tarjeta y padding `--espacio-16`. Error/resumen enfocable y éxito anunciado. Sólo mensajes aprobados y correlationId seguro; nada de JSON crudo |
| Diálogos | Mantener dialog nativo compartido, superficie blanca, sombra alta, radio y anchos vigentes. Título, contexto y verbo exactos; motivo únicamente cuando el contrato lo exige. Cancelar inicia foco, Escape/retorno se conservan |
| Vacío y carga | Reutilizar componentes compartidos; vacío con icono/texto/acción autorizada, sin contenido inventado. Esqueleto en consulta tabular, progreso en el botón o upload según contrato; sin overlays que oculten errores o sesión |
| Upload | Conservar selección, progreso, cuarentena/escaneo y mensajes aprobados. La renovación sólo define composición visual; LIMPIO sigue siendo condición contractual de vínculo |

No se crea una segunda biblioteca de componentes paralela. Las futuras tareas extienden los parciales y CSS compartidos existentes; el hito de adaptación decide los archivos concretos. El diseño no añade acciones ni determina qué actor puede usarlas.

## 6. Seis estados, responsive y accesibilidad — aprobado

| Estado | Regla aplicable a toda pantalla |
|---|---|
| Normal | Información confirmada, jerarquía estable y acciones permitidas |
| Foco | `:focus-visible`, grosor/desfase por tokens, acento o peligro según componente; no recortar el anillo. Orden DOM vigente y regreso después de diálogo/detalle |
| Deshabilitado | Control temporalmente inactivo con significado visible, sin hover activo; contenido sigue legible. La falta de autorización oculta la acción, no crea una opción atenuada |
| Error | Alerta de sección o resumen enfocable y error asociado por aria-describedby/aria-invalid; no afirmar éxito, no ocultar conflicto ni reintentar automáticamente |
| Cargando | aria-busy en región afectada, mensaje aprobado, prevención de doble envío; esqueleto conserva espacio de tabla. No presume identidad, datos ni resultado |
| Vacío | Distinguir ausencia, filtro sin resultados y falta de historia conforme al catálogo aprobado; sugerir únicamente una acción disponible y autorizada |

Se conserva la matriz de componentes con sus «No aplica»: los seis estados son cobertura de pantalla, no seis variantes artificiales para cada elemento estático.

Aceptar WCAG 2.2 AA con el área mínima de proyecto de 44×44. Conservar los pares de contraste ya aprobados y verificar que los nuevos usos usan esos pares; no declarar que las capturas prueban conformidad. Texto e icono acompañan los estados; nunca sólo color. Enlaces de contenido llevan subrayado visible para distinguirlos sin depender del color. El foco no depende sólo de hover.

En estrecho, una columna; filtros, campos, diálogos y acciones con wrap sin pérdida de controles. Únicamente el contenedor de tabla puede desplazarse horizontalmente; no ocultar overflow de html/body ni convertir filas a tarjetas. Verificar zoom/reflow a 320 px CSS, texto ampliado y teclado durante implementación. Mantener etiquetas legibles, navegación modal y select nativo sin recorte de foco.

Con prefers-reduced-motion se elimina pulsación/animación de carga y se conserva la información estática y textual. Esta regla se documenta ahora; su cumplimiento productivo se verifica en el hito que la materialice.

## 7. Adopción por tareas pendientes y transición — aprobado

Una vez aprobada e incorporada documentalmente, docs/design será la referencia oficial para cualquier pantalla pendiente. Cada plan FRONT lee los cinco documentos, navegación y la nueva guía; acepta esta aprobación y sólo somete a decisión las carencias específicas de su historia.

| Consumidora | Qué consume | Qué sigue sujeto a su alcance propio |
|---|---|---|
| FRONT-017 | Shell/panel/encabezado, campos y upload compartidos, errores/carga/vacío | BR-D07/D08, tipos/payloads, permisos, CSRF/idempotencia, privado/escaneo; sin preview/descarga |
| FRONT-018 | Tablas de versiones, badges, alertas y dialog compartidos | BR-API04/D04/M08, sustitución, conclusión, faltantes, autoridad y concurrencia; descarga sólo con contrato |
| FRONT-019 | Listados/filtros, formulario de decisión, historia y confirmación compartidos | BR-D09/M09, tres resultados, jerarquía, autovalidación, fundamento, escalamiento y permisos |
| FRONT-020 | Paneles/encabezados, tablas/filtros y estados compartidos | BR-API05/D10..D12/M11/M12, cinco indicadores, auditoría y continuidad; sin sexto indicador/exportación/reparación/deploy |

Dependencias y contratos siguen siendo los de Adenda 45 y sus adendas vigentes. Definir la base visual no resuelve por sí mismo esas brechas ni autoriza implementar historias posteriores.

Las pantallas existentes conservan código y comportamiento hasta el hito posterior de adaptación. Las nuevas tareas materializan únicamente los componentes compartidos necesarios dentro de su alcance aprobado. No se exige migrar todas las pantallas como dependencia previa. Si modificar una regla compartida afecta pantallas antiguas, acotar su consumo para preservar su presentación hasta el hito aprobado, sin duplicar la biblioteca ni esconder la regresión.

Registrar por separado referencia documentada y diseño implementado por superficie. Una aprobación documental no acredita migración, pruebas de navegador ni integración del código. No reclasificar tareas anteriores ni repetir sus gates por este cambio documental.

## 8. Archivos y adenda — aprobado

La aprobación de §§3–9 autoriza la incorporación documental siguiente:

1. Actualizar [tokens.md](tokens.md), tablas y bloque root concordantes con §3.
2. Actualizar [componentes.md](componentes.md) y [navegacion.md](navegacion.md) con §§4–6, preservando los registros de rutas y composiciones funcionales; delimitar ejemplos históricos frente a reglas vigentes.
3. Actualizar [estados-y-mensajes.md](estados-y-mensajes.md), [estados-de-dominio.md](estados-de-dominio.md) y [accesibilidad.md](accesibilidad.md) sólo para presentación, precedencia y referencia; conservar mensajes, estados y significados aprobados.
4. Crear `docs/design/referencia-renovada.md` con adopción y transición, vínculos a las reglas oficiales y procedencia externa; no requiere guardar la maqueta, CSS ni capturas en Git.
5. Crear en raíz `F07_ADENDA_55_REFERENCIA_DE_DISENO_RENOVADO.md` para registrar el suplemento de diseño/adopción que afecta el criterio transversal y consumo del backlog congelado. La serie vigente llega a Adenda 54 y 55 está libre en este checkout; volver a verificar antes de crear. No insertar una tarea ni inventar IDs HU/CAP/CA/CP/BR/NAV. Si 55 se ocupa, presentar el número disponible antes de incorporarlo.
6. Actualizar `docs/INDICE_IDS.md` con ubicación y rangos de la adenda, sin reasignar identificadores; registrar aprobación y alcance en IMPLEMENTATION_STATUS.md sin sustituir el registro FRONT-016.
7. Marcar este plan aprobado sólo con texto real del responsable y secciones cubiertas. Preparar el mensaje del siguiente chat en `docs/traceability/DISENO_RENOVADO_SIGUIENTE_HITO.md`.

No se editan F00–F07 congelados, Fuentes, AGENTS.md ni las instrucciones FRONT. La adenda registra la adopción documental local aprobada; no declara publicación o integración en GitHub.

## 9. Validación, cierre y siguiente hito — aprobado

Validar enlaces locales y anchors, referencias/IDs, consistencia entre tablas y bloque root, valores nuevos y conservación de paleta/mensajes/contratos. Comprobar cobertura de los seis estados y consumidores FRONT-017..020, transición sin dependencia artificial y alcance del diff. Ejecutar git diff --check. Conservar las comprobaciones reales y su salida resumida en trazabilidad.

Build, pruebas funcionales, PostgreSQL, navegador y pipeline no aplican a esta modificación exclusivamente documental. La revisión visual productiva, contraste/foco medidos, responsive y validación de componentes quedan pendientes del hito de implementación, porque no se modifica código. No se presentan como aprobadas por las capturas de la maqueta ni por la validación documental.

Al cerrar: entregar archivos, aprobación registrada, verificaciones y pendientes; solicitar aprobación final e incorporación del paquete documental resultante. Puede conservarse un commit local coherente; no push/PR/merge/despliegue. Estado del resultado: «referencia documentada», nunca «diseño implementado» en pantallas existentes.

Nombre exacto propuesto del siguiente chat: **SGOL — Diseño renovado — Adaptación de pantallas existentes**.

El prompt completo que se entregará tras la incorporación pedirá leer y aceptar la referencia oficial aprobada; preparar un único plan y esperar su aprobación; renovar primero layout y componentes compartidos, después pantallas por grupos; conservar rutas, permisos, contratos, mensajes, auditoría y comportamiento; commits locales pequeños en un único hito; validaciones enfocadas y revisión visual proporcional; actualizar trazabilidad y capturas sintéticas; publicar sólo con autorización del responsable mediante un único PR/pipeline final, sin publicación por pantalla o historia anterior; mantener seguimiento y correcciones del pipeline autorizado y pedir aprobación expresa del merge de la cabeza vigente validada. No inicia FRONT-017..020, no ejecuta el segundo hito ni crea otro chat automáticamente.
