# Comentarios de conclusión en Validación

Cambio de interfaz implementado y publicado el 3 de noviembre de 2026 en la aplicación compartida.

## Causa y alcance

`OperationalTaskService.CompleteCoreAsync` conserva `Conclusion` por separado de `Comment`, normaliza valores vacíos y limita la conclusión a 4000 caracteres. `OperationalSupervisionService` ya proyecta ambos campos. La tarjeta `_ValidationCards.cshtml` mostraba la conclusión únicamente como alternativa para tareas con requisito `NINGUNO`, sin archivo ni comentario de evidencia. El historial conserva la conclusión mediante `OperationalTaskService.History.cs`; no requiere migración ni modificación de registros existentes.

La tarjeta ahora presenta «Comentario de conclusión» cuando existe texto, independientemente del requisito. Mantiene el comentario de evidencia y evita duplicar la conclusión en tareas sin requisito. Razor codifica el contenido y el estilo existente `supervision-text` conserva saltos de línea y ajusta palabras largas. No cambian filtros, formularios, permisos, revisiones, persistencia ni historial.

## Validación

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Conclusión en seis tipos de requisito | Texto visible una sola vez, evidencia conservada | Renderizado de `_ValidationCards` compilado con datos sintéticos, `tools/Sgol.ValidationChecks` | Aprobado |
| Conclusión nula, vacía o espacios | Sin bloque vacío | Mismo ejecutable; 24 combinaciones totales | Aprobado |
| HTML, saltos de línea y texto largo | HTML inerte, texto completo ajustado | Razor y Playwright con CSS del producto | Aprobado |
| 320, 768 y 1440 px; light, dark y special | Sin desbordamiento horizontal, bloque visible, filtros operables con teclado | Chromium/Edge y WebKit; 18 combinaciones | Aprobado |
| Compilación y diff | Compilación sin errores y diff sin errores de whitespace | `dotnet run --project tools/Sgol.ValidationChecks`; `git diff --check` | Aprobado |
| Primera ejecución visual | Cargar estilos reales | El host de prueba resolvía incorrectamente la raíz; se corrigió y repitió la prueba | Fallido inicialmente; aprobado después |
| Pantalla autenticada y recorrido entrega/revisión en app compartida | Ver conclusión persistida junto a evidencia | No ejecutado: pruebas de componente Razor con datos sintéticos, no flujo autenticado ni consulta a base | No ejecutado |
| Escrituras, concurrencia y migraciones | Sin cambios | Cambio exclusivo de presentación | No aplicable |

Ejecutar `dotnet run --project tools/Sgol.ValidationChecks -- --serve` y, con Playwright en `NODE_PATH`, `node tools/Sgol.MobileChecks/validation-conclusions.cjs`. Evidencias ignoradas en `artifacts/validation-conclusions/`.

## Estado documental y entrega

Se consultaron Inicio, reglas de organización, mapa de diseño, diseño consolidado y la sección Supervisión y Validación de `docs/design/05-pantallas.md` de la bóveda. Tras la autorización de publicación se recorrieron Inicio → infraestructura → instalación local → base documental y acceso LAN. Se publicó mediante `dotnet publish --no-restore` en `.local/runtime/validation-conclusions-20261103`, conservando la publicación anterior `refresh-supervision-link-20261103` y sus cambios ya activos. Lanzador 23244 y Web 16048 comprobados por argumentos y listener 7241. Proxy 25924 conservado. Readiness interno y acceso HTTPS local/LAN devolvieron 200 sin omitir TLS; sin errores en stderr de arranque. La pantalla autenticada sigue sin comprobarse.

Se reconciliaron las notas existentes de pantallas, instalación local y base documental de la bóveda; enlaces locales comprobados. No hay cambios de mapas o inventario porque no se crearon notas. La copia versionada se actualizará al preparar un commit documental. Sin commit, push, migraciones ni escrituras operativas explícitas. Se conservan modificaciones ajenas existentes en actualización de tareas y sus pruebas.
