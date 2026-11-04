# Integración local de Graphify con la bóveda — 2026-11-03

## Alcance y decisiones

Cambio funcional de herramientas de desarrollo y documentación, sin cambios funcionales o visuales en SGOL. Autorizado: instalar desde el repositorio oficial, integrar la consulta actualizada de Obsidian, investigar buenas prácticas y actualizar AGENTS.md al finalizar. No requiere desplegar, compilar Web ni escribir datos de la simulación.

Se conservaron los cambios previos en AssignmentRefreshService, páginas Tasks/Refresh, _ValidationCards, herramientas de pruebas y sus documentos de trabajo. No se hizo commit ni push.

La bóveda ya tenía 40 notas, mapas y enlaces. Se eligió indexación estructural Markdown local, con actualización bajo demanda y sin extracción semántica. Los productores son las notas; Graphify produce nodos de páginas/encabezados, enlaces y manifiesto; el wrapper verifica hashes y consume consultas; el agente lee las notas fuente seleccionadas. No se modifican ni copian automáticamente reglas de negocio a otro sistema.

## Implementación

- Graphify 0.9.102 instalado mediante uv desde `git+https://github.com/Graphify-Labs/graphify.git@59b5ab1151e031fb42e39cfc9a50bf96f2d57ae0`. Clon oficial de inspección en `%USERPROFILE%/.local/share/graphify/source`.
- uv 0.13.7 instalado desde su instalador oficial. La descarga de Python 3.13.18 funcionó, pero uv falló al reconocer el enlace de versión menor. Se completó la instalación indicando el ejecutable absoluto descargado. Un segundo intento con 3.12 presentó el mismo fallo; no se usa ese intérprete.
- `scripts/consultar-boveda.ps1`: Query, Explain, Path, Update, Status y Rebuild. Configura backend none solo en el proceso y restaura las variables anteriores. Serializa consultas con bloqueo de archivo, actualiza incrementalmente, verifica raíz/hashes/cobertura de páginas antes y después de consultar y falla ante índice obsoleto.
- Índice privado fuera de la bóveda: `.local/graphify/boveda`, ignorado por Git. `.graphifyignore` de la bóveda admite Markdown y excluye adjuntos/configuración/salidas.
- Query entrega ubicaciones; relaciones se piden explícitamente. Profundidad 1, presupuesto orientativo 600 y máximo 5.000 caracteres. El recorte es explícito. No se lee el grafo completo en el contexto del agente.
- Procedimiento vigente en la bóveda y `docs/boveda/docs/desarrollo/consulta-boveda-graphify.md`; mapa, inventario y reglas reconciliados tras comprobar diferencias. AGENTS.md dirige el descubrimiento documental al wrapper y conserva fallback, lecturas y verificaciones.

## Hallazgos y límites

El presupuesto nativo no es un límite estricto: una consulta de bono con presupuesto 1.200 devolvió unas 2.845 unidades estimadas por Graphify. Por ello se compacta la salida y se limita además por caracteres. No se afirma un porcentaje de ahorro de tokens ni equivalencia exacta caracteres/tokens.

La consulta `planificacion` priorizó una sección visual; `planificacion capacidad` encontró la referencia funcional. Las consultas no son exhaustivas. El índice no contiene todas las reglas del cuerpo ni el código de SGOL, y no acredita el despliegue.

Aunque la documentación general de Graphify describe `update` como actualización de código y reserva la semántica de documentos para otro recorrido, el código y la prueba de 0.9.102 confirman extracción estructural Markdown en `update --no-cluster`. Las actualizaciones futuras deben repetir esta prueba y revisar el formato del manifiesto.

Una primera fixture con origen y salida hermanos bajo el mismo padre produjo claves de manifiesto con prefijo inesperado; el wrapper rechazó correctamente esa raíz incompatible. Las fixtures reproducibles separan fuentes e índices en directorios distintos, al igual que la instalación real. No se disimuló la incompatibilidad eliminando la comprobación.

## Matriz ejecutada

| Escenario | Resultado esperado | Prueba/entorno | Estado |
|---|---|---|---|
| Instalar versión oficial fijada | CLI disponible con procedencia Git | uv tool list, instalación local | Aprobado |
| Construir y consultar Markdown | Rutas/encabezados consultables | scripts/comprobar-indice-boveda.ps1, fixture aislada | Aprobado |
| Repetir sin cambios | No reescribir grafo | Comparación de LastWriteTimeUtc, fixture y bóveda | Aprobado |
| Modificar nota | Status rechaza índice viejo; Query actualiza | Fixture aislada, hashes y nuevos encabezados | Aprobado |
| Alta, renombrado, retirada | Nuevas páginas presentes; anteriores ausentes | Fixture aislada, grafo real | Aprobado |
| Adjuntos | JSON fuera del índice | Fixture aislada | Aprobado |
| Sin coincidencias | Aviso y fallback, sin inventar resultados | Fixture aislada | Aprobado |
| Relaciones | Ruta explícita por enlace Markdown | Fixture y bono → supervisión en bóveda | Aprobado |
| Rebuild explícito | Reconstruye índice derivado | Fixture aislada | Aprobado |
| Bóveda ausente | Error sin servir grafo viejo | Fixture aislada | Aprobado |
| Salida grande | Recorte visible a límite configurado | Bono con Relations y MaxChars 1000 | Aprobado |
| Dos consultas simultáneas | Ambas completan, acceso serializado | Dos procesos contra índice real | Aprobado |
| Bono, procesos, capacidad | Localizar referencias funcionales | Consultas reales, capacidad con dos términos | Aprobado con límite léxico descrito |
| Enlaces y documentación | Rutas locales válidas, mapa e inventario actualizados | 149 enlaces locales revisados; git diff --check | Aprobado |
| Ahorro neto por tarea completa | Comparar tokens, calidad y tiempo de chats equivalentes | No se ejecutó benchmark de conversaciones | No ejecutado |
| UI/BD/despliegue | Sin impacto en aplicación | Herramienta CLI documental | No aplicable |

La suite reproducible aprobó 12 comprobaciones. Evidencia privada: `.local/graphify/pruebas/indices/e350dcb1990f470d9a68e984cc9d8d7d/resultados.txt`; pruebas exploratorias adicionales en `.local/graphify/checks/results.txt`. Estado final del índice tras documentar: 41 notas, 278 nodos y 529 relaciones.

## Referencias consultadas

- [Graphify oficial](https://github.com/Graphify-Labs/graphify).
- [Consultas acotadas](https://docs.graphify.com/open-source/features/querying).
- [Actualización incremental](https://docs.graphify.com/open-source/features/incremental-updates).
- [Exclusiones](https://docs.graphify.com/open-source/configuration/ignoring-files).
- [Agentes y contexto breve](https://docs.graphify.com/cookbooks/make-agents-use-the-graph).
- [OpenAI: carga progresiva y scripts para mecánica repetida](https://developers.openai.com/blog/skills-agents-sdk).
- [Instalación de uv](https://docs.astral.sh/uv/getting-started/installation/).
