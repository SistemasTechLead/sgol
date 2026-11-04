# Instrucciones de trabajo para SistemaSGOL

## Criterio y comunicación

- Cuestiona las suposiciones de la petición antes de validarlas. Señala puntos débiles, consecuencias y alternativas con razones concretas.
- Sé directo, sin halagos genéricos, relleno ni repetir la petición como respuesta.
- Investiga el comportamiento actual antes de proponer o implementar cambios. Distingue hechos observados, inferencias y decisiones pendientes.
- Conserva el objetivo y las autorizaciones del usuario entre mensajes. No pidas otra vez permisos ya concedidos; pregunta por decisiones de negocio que no estén resueltas.
- Sigue la petición actual del usuario cuando contradiga documentación histórica y explica las discrepancias relevantes.

## Protocolo del proyecto

Antes de planear o implementar cambios funcionales, de interfaz o mixtos, lee [el protocolo de implementación](docs/desarrollo/PROTOCOLO_IMPLEMENTACION.md). Aplica la profundidad proporcional al impacto; una corrección documental no exige pruebas de navegador ni compilación.

- Clasifica el cambio como funcional, de interfaz o mixto. Los mixtos cumplen ambas validaciones.
- Si se solicita solo un plan, investiga y entrega el plan; separa las pruebas actuales de las propuestas. No presentes escenarios propuestos como ejecutados.
- Sigue el dato desde su creación hasta almacenamiento, copias, asignación, ejecución, revisión, consultas e historial. Busca productores y consumidores, no solo coincidencias de texto.
- Identifica archivos, funciones y rutas reales que sustentan el análisis. Revisa registros existentes además de registros nuevos.
- Incluye una matriz de escenarios con resultado esperado, prueba, entorno y estado. Cubre combinaciones relevantes, especialmente cambios simultáneos y estados históricos.
- Pregunta cuando una decisión no resuelta cambie cargas, responsables, plazos, permisos, historial o comportamiento de registros existentes. Presenta opciones y consecuencias; continúa el trabajo independiente.
- Prueba recorridos completos con formularios y servicios reales. Las pruebas aisladas y la compilación no sustituyen la integración.
- Los conflictos esperados deben identificar su causa y la entidad afectada, con una acción comprensible. Evita mensajes genéricos que oculten la regla aplicada.

## Interfaz existente

- Inspecciona pantallas equivalentes y reutiliza componentes, tokens, estilos, navegación y patrones existentes.
- Consulta la referencia visual consolidada de la bóveda (`docs/design/00-diseno-sgol.md` dentro de la ruta indicada abajo), sus referencias aplicables y el código actual. La referencia histórica del repositorio `docs/design/ESTILO_VISUAL_V2.md` no prevalece sobre la consolidada. Señala documentación desactualizada; no restaures restricciones históricas que el usuario ya haya sustituido.
- Verifica la interfaz renderizada en los tamaños y temas afectados, teclado, foco, contenido largo y estados de carga, vacío, error y éxito aplicables.
- Distingue pruebas de componentes o maquetas de las pruebas de la pantalla y el flujo reales.

## Documentación vigente y bóveda de Obsidian

- La bóveda de SGOL está en `C:\Users\siste\Documents\Obsidian Vault\SistemaSGOL`. Empieza por `Inicio.md`, el mapa temático de `mapas/` y `organizacion/reglas.md`; consulta solo las referencias pertinentes a la tarea. La bóveda y `docs/` del repositorio son ubicaciones independientes, sin sincronización automática.
- La copia versionada de las referencias consolidadas está en [docs/boveda/Inicio.md](docs/boveda/Inicio.md); úsala si la bóveda local no está disponible. Al incluir cambios documentales en un commit, actualiza esta copia desde las notas pertinentes de la bóveda, preservando su estructura y convirtiendo enlaces al código local en enlaces relativos al repositorio. Comprueba diferencias antes de reemplazar archivos: no sobrescribas cambios divergentes ni importes planes, respaldos o configuración de Obsidian. Las referencias históricas fuera de `docs/boveda/` no prevalecen sobre las consolidadas.
- Al agregar, modificar o retirar una función, actualiza como parte del trabajo las referencias funcionales, de diseño y los procedimientos afectados. Busca primero una nota existente: modifícala si responde a la misma necesidad. Crea otra solo para un tema distinto que no esté cubierto. No generes adendas ni duplicados por cada cambio.
- La fuente aceptada de comportamiento es la app compartida. Contrasta el código con la publicación efectiva antes de describir un cambio como vigente. Si solo está propuesto o implementado sin desplegar, documenta ese estado fuera de la bóveda y deja explícita la actualización documental pendiente; no despliegues únicamente para satisfacer esta regla. Cuando se active el cambio, reconcilia la referencia con lo que usa la app.
- Cada documento nuevo de la bóveda debe tener `categoria`, `tipo` y `estado_documental`. Elige una categoría principal entre `producto`, `arquitectura`, `dominio`, `datos`, `integraciones`, `diseno`, `seguridad`, `infraestructura`, `desarrollo` y `calidad`. Usa la plantilla y convenciones existentes; enlaza desde su mapa, añade el documento al inventario y crea relaciones útiles con las notas que expliquen dependencias, reglas o procedimientos. No fuerces enlaces ni copies contenido entre categorías.
- Mantén las referencias en presente: comportamiento, límites, permisos, datos e historial que el sistema realmente conserva. Sustituye las reglas obsoletas sin borrar la compatibilidad histórica todavía implementada. Registra fuentes propietarias, alcance y limitaciones; actualiza `ultima_verificacion` y `verificado_en` solo cuando hayas realizado la comprobación correspondiente. Inspección estática no equivale a prueba funcional.
- **No guardes en la bóveda planes de implementación, propuestas, bitácoras, informes de pruebas o casos particulares, diagnósticos temporales ni ejercicios de importación.** Los planes y registros de ingeniería que requiera el protocolo van en `docs/desarrollo/cambios/` del repositorio; evidencias generadas y archivos privados van en `.local/` o `artifacts/` según corresponda, sin publicarlos. Extrae de esos registros únicamente las reglas vigentes que deban incorporarse a la referencia.
- Antes de cerrar, comprueba enlaces locales, mapa, inventario y referencias afectadas; repara enlaces al sustituir o retirar notas. No copies masivamente `docs/` a la bóveda ni reintroduzcas documentos históricos retirados. Si la bóveda no está disponible, informa la limitación y registra la actualización pendiente fuera de ella, sin afirmar que se realizó.

## Índice de la bóveda con Graphify

- Para descubrir qué notas o relaciones documentales intervienen en una tarea, ejecutar primero `./scripts/consultar-boveda.ps1 -Query 'tema concreto'`. Actualiza el índice local y verifica hashes; después leer Inicio, el mapa pertinente y las secciones fuente necesarias. No repetir lecturas ya realizadas sin un cambio relevante.
- Usar `-Action Explain -Query 'ruta/nota.md'` para conexiones de una nota y `-Action Path -Query 'origen' -Target 'destino'` para seguir enlaces. Empezar con los límites predeterminados; ampliar solo cuando falte información. No leer todo `graph.json` ni informes completos en el contexto del chat.
- El índice cubre estructura y enlaces de Markdown, no el cuerpo completo, el código de SGOL ni el despliegue. Consultar las fuentes para reglas y verificaciones. Una búsqueda vacía, parcial o recortada no prueba ausencia; usar `rg` dirigido si hace falta. Las cadenas exactas y rutas ya conocidas pueden consultarse directamente.
- Si la actualización falla, no usar el grafo viejo. Si falta la bóveda, informar y recurrir a `docs/boveda/`. Tras editar notas, ejecutar `./scripts/consultar-boveda.ps1 -Action Update`. No activar extracción con LLM ni reconstrucciones forzadas como recuperación automática.
- Procedimiento, instalación fijada y límites: [consulta con Graphify](docs/boveda/docs/desarrollo/consulta-boveda-graphify.md). Esta regla conserva los recorridos obligatorios de infraestructura y la app compartida como fuente de comportamiento.

## Entornos y datos

- Antes de actualizar, recargar o reiniciar la app local/compartida, sigue obligatoriamente este recorrido dentro de la bóveda: `Inicio.md` → `mapas/infraestructura.md` → `docs/instalacion-local.md` → `organizacion/base-documental.md`; consulta `docs/acceso-red.md` si interviene LAN/proxy. Empieza por esas ubicaciones conocidas, no por una búsqueda general del host. Verifica después los procesos, argumentos, publicación y listeners efectivos: la documentación orienta, pero no prueba el estado actual. Distingue reiniciar el mismo artefacto de publicar cambios; conserva proxy, dependencias y datos cuando la petición solo alcance Web. Si las rutas no coinciden o la bóveda falta, informa y usa los registros/scripts del checkout como alternativa. Al ejecutar la operación, indica brevemente en el chat las notas consultadas y qué comprobación guio la acción; no crees bitácoras de recorrido en la bóveda ni afirmes que su gráfico registra las lecturas.
- Aclaración del usuario, 28/10/2026: el entorno actual se usa exclusivamente para desarrollo y pruebas de operación. Se introducen datos simulados diariamente para explorar variables del sistema. No es producción real.
- El acceso local y el acceso LAN pueden compartir aplicación y base de datos. Identifica el destino efectivo antes de escribir; no supongas que dos direcciones son dos entornos aislados.
- Las pruebas operativas con datos simulados forman parte del trabajo autorizado cuando la petición las incluye. No trates esos datos como registros productivos reales ni exijas aprobación por esa suposición.
- Usa bases aisladas para regresiones automatizadas, casos destructivos y pruebas que puedan interferir con la simulación diaria. Reutiliza una copia de la simulación cuando se necesite reproducir su estado.
- Conserva el estado necesario para reproducir fallos y no alteres simulaciones ajenas al alcance. No desactives validaciones para que una prueba pase.
- El usuario prevé eliminar los datos simulados al pasar a producción. Es una tarea futura de puesta en producción: esta declaración no ordena borrarlos ahora ni autoriza reinicios automáticos del entorno.
- No publiques credenciales, bases, evidencias, respaldos ni archivos privados. Las rutas y el estado de los servicios deben verificarse, no suponerse a partir de documentación antigua.

## Validación y cierre

- Conserva las invariantes acordadas y verifica permisos, datos históricos, concurrencia, reintentos y atomicidad cuando puedan verse afectados.
- Ejecuta pruebas adecuadas al cambio y añade regresiones para fallos corregidos. Una vez cubierto el riesgo y aprobadas las pruebas, evita repeticiones sin motivo.
- Registra escenarios aprobados, fallidos, no ejecutados y no aplicables, con su evidencia o justificación. No declares validado lo que solo se inspeccionó.
- Si una prueba crítica queda pendiente, comunica la limitación y el estado real; no presentes el cambio como completamente validado.
- Revisa el diff antes de atribuir cambios. Conserva modificaciones previas del usuario. Respeta las autorizaciones vigentes para commit, push y despliegue, sin inventar nuevas aprobaciones.

## Salidas de comandos con RTK

- Aplicar [RTK.md](RTK.md): prefijar explícitamente con `rtk` los comandos compatibles de Git, `rg` y .NET. El hook local no sustituye esta instrucción en herramientas que no lo activen.
- Conservar códigos de salida y recuperar salida original para revisiones completas o evidencia exacta. No interpretar un resumen como cobertura total ni repetir operaciones con efectos solo para obtener más detalle.
