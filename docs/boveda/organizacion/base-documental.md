# Base documental aceptada

[Inicio](../Inicio.md) · [Infraestructura](../mapas/infraestructura.md) · [Inventario](inventario.md)

## Decisión del usuario

El 03/11/2026 se estableció que la documentación debe describir los cambios que usa la aplicación compartida. Las propuestas no implementadas y las pruebas históricas se distinguen de la referencia actual. Esta decisión define qué comportamiento documentar; no convierte automáticamente las copias de la bóveda y Git en una sola fuente de edición.

## Estado observado el 03/11/2026

- Repositorio: `C:\Users\siste\Dev\SistemaSGOL`, rama `master`.
- Commit: `464d283418e8546c543727120f0a754fad0f65c8`, «Integra procesos periódicos, resolución de posposiciones y mejoras de interfaz».
- En la comprobación previa al cambio, tras `git fetch origin`, HEAD y origin/master coincidían: cero commits de diferencia y árbol de trabajo limpio. No había cambios pendientes que confirmar o subir.
- Publicación que atiende el puerto interno 7241: `.local/runtime/calendar-preservation-20261104/Sgol.Web.dll`, proceso 16340, lanzador 5028, comprobados el 04/11/2026. Incluye las excepciones de conservación confirmadas hasta el siguiente cambio del catálogo, su conteo y confirmación independiente; conserva la exclusión autorizada de vencidas no reprogramadas en la actualización de asignaciones, restauración de candidatos fallidos y diagnóstico del límite. El formulario se probó en copia aislada; las 789 asignaciones compartidas conservaron su huella completa antes/después de publicar. No hubo migración ni aplicación de la propuesta en la base compartida. Readiness interno y acceso local/LAN respondieron 200 con comprobación TLS normal. El proxy 20652 (lanzador 23392), verificado el 04/11/2026, se conservó. Esta publicación incorpora los comentarios de conclusión en las tarjetas de Validación, los detalles de cambios en conflictos y el enlace de arrastre/posposición a Supervisión; incluye además el rango mensual de posposición y la migración `20261104010000_AddMonthlyCarryoverWindow`. Las 789 asignaciones y 13 solicitudes conservaron su huella completa antes/después de migrar. Esos cambios permanecen sin commit. La comprobación autenticada de esa pantalla sigue pendiente. Los identificadores de proceso son observaciones temporales.
- Proxy LAN: proceso 20652 (comprobado el 04/11/2026); escucha en localhost:7240 y 192.168.1.92:7440/7443. `tools/Sgol.LanProxy/Program.cs` dirige el tráfico web a `http://127.0.0.1:7241`.
- El ensamblado anterior informaba `1.0.0+483b55cadb98e7d01057831f285d7c1f26d40294`. Esa etiqueta se incorporó antes del commit conjunto y no demuestra por sí sola que falten sus cambios.

## Comprobación anterior y límites

| Escenario | Resultado esperado | Método y entorno | Resultado |
|---|---|---|---|
| Repositorio/remoto | Sin divergencia ni cambios pendientes | fetch, status y rev-list en checkout local | Aprobado: 0/0 y limpio |
| Fuente de la publicación | Archivos fuente disponibles coinciden | SHA de documentos en los 11 PDB portables frente al checkout | 684 coincidencias y cero diferencias; 103 rutas no disponibles no pudieron contrastarse |
| Recursos web | Archivos estáticos del checkout coinciden con publicación | SHA-256 de wwwroot | Aprobado: 74 coincidencias, cero diferencias y cero ausentes |
| Acceso LAN | Endpoint de acceso responde | GET HTTPS /acceso | 200 |
| Acceso local | Endpoint responde | GET HTTPS /acceso sin seguir redirección | 302; destino de redirección no validado en esta comprobación |
| Autenticación TLS | Cadena de confianza válida | curl utilizó -k para la comprobación HTTP | No ejecutado |
| Recorridos autenticados | Reglas y pantallas funcionan en todos los casos | Nueva batería funcional/visual | No ejecutado: esta fase organiza documentación |
| Configuración y base | Coincidencia completa con un despliegue reproducible | Comparación de configuración efectiva, esquema y datos | No ejecutado: no se declara equivalencia completa |

Los PDB y recursos aportan evidencia de correspondencia del código disponible con la publicación; no certifican todos los insumos de compilación, cada Razor generado ni el estado de la base. No se reinició el host ni se modificaron datos. No se repitieron pruebas funcionales ajenas al cambio documental.

## Uso posterior

Antes de ejecutar operaciones, comprobar de nuevo proceso, puerto, publicación y configuración pertinente. Las rutas aquí registradas sirven de punto de entrada, no de garantía perpetua. Mantener separados el comportamiento actual, la compatibilidad con registros históricos y las propuestas futuras.

Las referencias de diseño y las quince referencias funcionales tienen alcance de revisión estática. Los planes, informes de casos y ejercicios de importación fueron sustituidos o retirados de la bóveda. La clasificación y la consolidación no equivalen a nuevas pruebas de ejecución. La bóveda continúa siendo independiente de Git, sin sincronización automática.
