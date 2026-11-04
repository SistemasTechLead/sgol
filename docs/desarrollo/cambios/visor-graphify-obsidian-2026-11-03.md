# Visualización de Graphify dentro de Obsidian

Integración de herramienta documental con interfaz, sin cambios en la app SGOL. El HTML exportado originalmente se abría fuera de Obsidian. La nota existente `docs/desarrollo/consulta-boveda-graphify.md` ahora incrusta el HTML con un iframe; Inicio enlaza a esa sección. Se actualizaron ambas notas y sus copias versionadas; mapa e inventario ya enlazan la guía existente.

`scripts/generar-grafo-boveda.ps1` conserva la generación manual de la instantánea. `scripts/visor-grafo-boveda.ps1` inicia, comprueba y detiene un proceso Node oculto; `scripts/servidor-grafo-boveda.cjs` sirve solo `/graph.html`, `/` y `/health`, en `127.0.0.1:7389`. No se instala plugin, no se abre LAN ni se modifica el host de SGOL. Después de reiniciar Windows hay que iniciar el visor manualmente. El HTML mantiene la dependencia original de vis-network desde unpkg y necesita Internet para cargarla.

| Escenario | Resultado esperado y evidencia | Entorno | Estado |
|---|---|---|---|
| Generar HTML | Exportación correcta desde índice actualizado | CLI Graphify local | Aprobado |
| Abrir nota | Grafo visible en iframe de la pestaña | Obsidian 1.15.0, vista lectura, captura observada | Aprobado |
| Buscar y seleccionar | Buscar bono, elegir Bono de tareas; enfoque y relaciones visibles | UI real de Obsidian | Aprobado |
| Inicio/estado/cierre/reinicio | Proceso propio detectado y reiniciado; HTTP 200 | PowerShell + listener efectivo | Aprobado |
| Aislamiento | Listener solo 127.0.0.1; graph.json 404; POST 405 | HTTP local | Aprobado |
| Recarga tras reinicio de Windows | Iniciar mediante script y reabrir nota | No se reinició el equipo | No ejecutado |
| Sin Internet | Biblioteca externa puede no cargar | Dependencia documentada, no probado offline | No ejecutado |
| Móvil y accesibilidad completa | No forman parte de esta integración local Windows | No probado | No ejecutado |

No se afirma simulación histórica ni animación continua: se usa el enfoque animado y la disposición del grafo de Graphify. No se hizo commit, push ni despliegue de SGOL. Se conservaron cambios anteriores.
