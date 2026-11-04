---
categoria: desarrollo
tipo: procedimiento
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: CLI local, boveda de prueba y visualizacion incrustada en Obsidian 1.15.0; sin cambios en la app compartida
---

# Consultar la bóveda con Graphify

[Desarrollo](../../mapas/desarrollo.md) · [Reglas documentales](../../organizacion/reglas.md) · [Inventario](../../organizacion/inventario.md)

## Alcance

Graphify indexa las notas Markdown de esta bóveda: archivos, encabezados, propiedades y enlaces explícitos Markdown y Obsidian. Es un índice estructural para localizar documentación. No resume el cuerpo completo, no infiere todas las reglas de negocio y no indexa el código de SGOL. Una relación ausente no demuestra independencia entre funciones. La vigencia operativa sigue sujeta a la [base documental](../../organizacion/base-documental.md).

La integración usa la CLI local con `GRAPHIFY_BACKEND=none`, sin llamadas a modelos para construir o consultar el índice. No necesita Obsidian abierto, un plugin de Obsidian ni un servidor MCP. Las notas originales siguen siendo la fuente documental; no se generan copias de ellas ni una wiki paralela.

## Consultar desde el repositorio

```powershell
./scripts/consultar-boveda.ps1 -Query 'bono'
./scripts/consultar-boveda.ps1 -Query 'planificacion capacidad'
./scripts/consultar-boveda.ps1 -Action Explain -Query 'docs/funcional/bono-tareas.md'
./scripts/consultar-boveda.ps1 -Action Path -Query 'docs/funcional/bono-tareas.md' -Target 'docs/funcional/supervision.md'
```

Cada consulta actualiza incrementalmente el índice, comprueba los hashes de todas las notas contra el manifiesto y verifica que cada nota tenga un nodo de página. Repite la comprobación después de consultar; ante un cambio detectado durante la consulta, falla en vez de presentar el resultado como vigente. Un bloqueo local serializa los chats que usan este comando. Un editor externo puede escribir después de la comprobación: la verificación corresponde al momento de lectura.

La salida normal contiene rutas y líneas de los nodos pertinentes. Leer después Inicio, el mapa temático y las secciones fuente necesarias; conservar los recorridos obligatorios para operaciones de infraestructura. No cargar toda la bóveda ni `graph.json` en el contexto del chat.

## Mantener breve y útil el contexto

- Consultar uno o dos conceptos concretos. La selección de Graphify es léxica y heurística; una palabra general puede priorizar una pantalla sobre la regla funcional. Usar términos combinados o una ruta exacta en `Explain`.
- El valor inicial es `-Budget 600`, profundidad 1 y máximo 5.000 caracteres. El presupuesto de Graphify es aproximado; el wrapper limita caracteres y avisa del recorte, sin garantizar un conteo exacto de tokens.
- Para investigar dependencias documentales, usar `Explain`, `Path` o `-Relations`. Ampliar `-Depth`, `-Budget` o `-MaxChars` solo si el resultado no basta; un recorte no es evidencia completa.
- Si no hay coincidencias o el resultado no responde al tema, usar `rg` sobre las notas pertinentes. Para cadenas exactas, mensajes o código, usar directamente `rg`. No repetir consultas amplias buscando exhaustividad.
- Reutilizar las rutas encontradas durante la tarea. El índice reduce descubrimiento; no sustituye lectura de reglas, comprobaciones del código ni pruebas funcionales.

## Actualización y recuperación

```powershell
./scripts/consultar-boveda.ps1 -Action Update
./scripts/consultar-boveda.ps1 -Action Status
```

`Update` actualiza sin consultar. `Status` solo comprueba el estado y falla si está desactualizado. Los cambios del cuerpo también invalidan el manifiesto, aunque no alteren los nodos estructurales.

Ante errores de actualización o diferencias de hashes, no usar el índice anterior: revisar `.local/graphify/boveda/ultima-actualizacion.log`. Graphify puede protegerse frente a una reducción grande del grafo. Después de comprobar que la retirada de notas fue intencional, `-Action Rebuild` fuerza la reconstrucción del índice derivado. No modifica las notas. Si falta la bóveda, consultar la copia `docs/boveda/` del repositorio e informar esa limitación; no cambiar silenciosamente la raíz del mismo índice.

La configuración `.graphifyignore` admite solo Markdown, mantiene transitables los directorios y excluye configuración de Obsidian, Git, papelera y salidas generadas. No se indexan adjuntos. Los hashes y el grafo se guardan fuera de la bóveda, en `.local/graphify/boveda/` del repositorio, ignorado por Git. No existe un servicio permanente: la actualización ocurre al consultar o ejecutar `Update`.

## Visualización interactiva

<iframe src="http://127.0.0.1:7389/graph.html" width="100%" height="650" title="Grafo interactivo de SistemaSGOL" style="border:0;"></iframe>

En Obsidian, usar **Vista de lectura** para ver el grafo dentro de esta pestaña. [Abrir en navegador](http://127.0.0.1:7389/graph.html).

El grafo se incrusta en esta nota mediante un iframe; sigue siendo una vista separada del gráfico nativo de Obsidian. Arrastrar el fondo desplaza la vista, la rueda ajusta el zoom y seleccionar un nodo muestra sus relaciones. El buscador permite localizar nodos y enfocarlos con animación. La simulación inicial se estabiliza; no representa cambios históricos ni una animación continua.

Para actualizar la instantánea desde el repositorio:

```powershell
./scripts/generar-grafo-boveda.ps1
```

El comando verifica y actualiza el índice, copia el grafo a `.local/graphify/visualizacion/` y exporta allí `graph.html`. Las consultas habituales no regeneran ni retiran esta visualización; repetir el comando después de cambios cuando se quiera una vista nueva. El HTML carga vis-network 9.1.6 desde unpkg con comprobación de integridad y requiere Internet para esa biblioteca. No requiere plugins de Obsidian. Un servidor mínimo sirve únicamente el HTML y su comprobación de estado, escuchando en 127.0.0.1:7389, sin acceso LAN ni listado de carpetas.

### Iniciar o detener el visor

```powershell
./scripts/visor-grafo-boveda.ps1 -Action Start
./scripts/visor-grafo-boveda.ps1 -Action Status
./scripts/visor-grafo-boveda.ps1 -Action Stop
```

Tras reiniciar Windows, ejecutar Start antes de abrir la nota. No se configura arranque automático. Si el panel queda vacío, comprobar Status y la conexión a Internet; después reabrir la nota o cambiar a Vista de lectura. Generar una nueva instantánea sigue siendo una acción separada con `generar-grafo-boveda.ps1`.

## Instalación y versiones

Instalación local: Graphify **0.9.102**, desde el repositorio oficial en el commit `59b5ab1151e031fb42e39cfc9a50bf96f2d57ae0`; Python 3.13.18 y uv 0.13.7. Ejecutable: `%USERPROFILE%\.local\bin\graphify.exe`. La copia de inspección del código está en `%USERPROFILE%\.local\share\graphify\source`.

Para reproducir en una máquina con uv y Python disponibles:

```powershell
uv tool install --python 3.13 'git+https://github.com/Graphify-Labs/graphify.git@59b5ab1151e031fb42e39cfc9a50bf96f2d57ae0'
```

En esta instalación uv descargó Python pero falló al reconocer su enlace de versión menor; se completó pasando la ruta absoluta del `python.exe` descargado en `--python`. No hace falta cambiar el Python de SGOL. El comando admite `-Graphify` para una ubicación distinta del ejecutable.

La integración llama `graphify update --no-cluster`: en la versión fijada, su extractor Markdown actualiza páginas, encabezados y enlaces sin LLM. La documentación general distingue actualización de código y extracción semántica de documentos; aquí no existe capa semántica que actualizar. Revisar esta compatibilidad y el formato del manifiesto antes de actualizar Graphify. No instalar automáticamente nuevas versiones ni activar extracción semántica como recuperación.

## Fuentes propietarias y buenas prácticas

- [Script de consulta de SGOL](../../../../scripts/consultar-boveda.ps1).
- [Repositorio oficial de Graphify](https://github.com/Graphify-Labs/graphify), especialmente `graphify/watch.py`, `extractors/markdown.py`, `cli.py` y `paths.py` en el commit fijado.
- [Consultas acotadas y evidencia](https://docs.graphify.com/open-source/features/querying): usar consultas concretas, inspeccionar fuentes y conservar incertidumbre.
- [Actualización incremental](https://docs.graphify.com/open-source/features/incremental-updates) y [exclusiones](https://docs.graphify.com/open-source/configuration/ignoring-files).
- [Uso por agentes](https://docs.graphify.com/cookbooks/make-agents-use-the-graph): instrucción breve y comprobación de llamadas reales. SGOL limita la regla a descubrir documentación; no bloquea búsquedas exactas ni lecturas necesarias.
- [OpenAI: skills para mantenimiento](https://developers.openai.com/blog/skills-agents-sdk): cargar contexto progresivamente y encapsular mecánica repetida en scripts.

El alcance verificado es instalación, consulta local, actualización de notas y controles del wrapper. No se ha medido ahorro neto de tokens en conversaciones completas ni se certifica comportamiento de la app por consultar este índice.
