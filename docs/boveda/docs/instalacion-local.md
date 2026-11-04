---
categoria: infraestructura
tipo: procedimiento
estado_documental: inspeccionado
ultima_verificacion: 2026-11-04
verificado_en: procesos-puertos-y-scripts
---

# Instalación local y operación de la app compartida

[Infraestructura y operación](../mapas/infraestructura.md) · [Inicio](../Inicio.md)

## Ruta de consulta antes de actuar

1. Entrar por [Infraestructura](../mapas/infraestructura.md) y leer esta guía.
2. Consultar la [base documental](../organizacion/base-documental.md) para identificar publicación y límites de la comprobación anterior.
3. Consultar [acceso LAN](acceso-red.md) si intervienen proxy, dirección o certificados; contrastar sus indicaciones con los scripts vigentes.
4. Verificar proceso, publicación, puertos y alcance de la operación antes de detener o reemplazar nada.

Estos enlaces forman un recorrido navegable en Obsidian. El gráfico muestra relaciones documentales; no registra ni anima automáticamente las lecturas de Codex.

## Ubicaciones conocidas

| Elemento | Ubicación o función |
|---|---|
| Checkout | `C:\Users\siste\Dev\SistemaSGOL` |
| Publicaciones locales | `.local/runtime/` dentro del checkout |
| Registro de lanzadores | `.local/processes.json`; comprobar que los procesos todavía corresponden a esos IDs |
| Lanzador | `scripts/run-local.ps1`, con `-Service web`, modo de red y, cuando aplica, `-BinaryPath` |
| Configuración de entorno | `scripts/local-env.ps1` y `scripts/red-env.ps1`; cargan configuración protegida, no imprimir sus secretos |
| Inicio general | `scripts/iniciar-local.ps1`; prepara dependencias y puede ejecutar preparación de seguridad y datos |
| Detención general | `scripts/detener-local.ps1`; detiene servicios registrados y dependencias Docker |

La instalación es de desarrollo y simulación operativa. Local y LAN pueden compartir aplicación y base. Reiniciar no autoriza borrar datos, ejecutar ejemplos ni regenerar planes.

## Topología observada

En la comprobación del 04/11/2026, el lanzador Web ejecuta `scripts/run-local.ps1 -Service web -Red -BinaryPath` con:

`C:\Users\siste\Dev\SistemaSGOL\.local\runtime\incidencias-integration-20261104\Sgol.Web.dll`

La Web escucha en `127.0.0.1:7241`. El proxy atiende `https://localhost:7240` y `https://192.168.1.92:7440`; el puerto LAN 7443 corresponde al circuito de almacenamiento del proxy. La dirección LAN es una observación, no una dirección garantizada permanentemente.

La publicación activa no es `dotnet watch`. Guardar el código no actualiza automáticamente este proceso. El lanzador también admite un modo de desarrollo con watch cuando se omite BinaryPath; cambiar entre modos es una decisión distinta de reiniciar el mismo artefacto.

Los recursos estáticos requieren una comprobación separada del ensamblado: `run-local.ps1` establece `src/Sgol.Web` como directorio de trabajo al usar `BinaryPath`. El host puede servir CSS y JavaScript del checkout mediante `UseStaticFiles()`. El 03/11/2026 se contrastó por HTTP que `search-text.js`, `mobile.js`, `desktop-workspace.js` e `interactive-controls.css` corresponden al checkout, no a las copias de la carpeta de publicación. Para esos recursos, guardar cambios sí modifica lo servido sin reiniciar Web; comprobar el contenido efectivo antes de copiar o reiniciar. Esto no acredita una prueba autenticada de las pantallas ni implica que las vistas Razor se recompilen.

## Identificar el destino sin buscar desde cero

Desde el checkout, leer `.local/processes.json` y comprobar las líneas de comando con `Get-CimInstance Win32_Process`. Relacionar lanzadores, procesos hijos y listeners mediante `Get-NetTCPConnection`. Confirmar qué proceso atiende 7241 y qué publicación carga; no elegir el proceso más reciente ni cualquier instancia de Sgol.Web, porque pueden coexistir pruebas aisladas.

Verificar que el artefacto existe y conservar modo, argumentos y configuración necesaria. Un PID anotado puede reutilizarse: no detenerlo sin volver a comprobar identidad y relación con el servicio. Si la ruta conocida ya no coincide, investigar desde esos registros y scripts antes de ampliar la búsqueda.

## Reiniciar y actualizar son operaciones distintas

**Reiniciar:** conservar la publicación identificada y el modo actual. Para una petición limitada a la aplicación, detener únicamente su lanzador Web y sus hijos verificados y relanzar la misma publicación mediante `run-local.ps1`. Usar proceso en segundo plano con ventana oculta, registros locales y actualizar el identificador Web en `processes.json` conservando las demás entradas. No usar la detención general por comodidad: también apaga proxy y Docker.

**Actualizar con cambios:** revisar estado de Git, preparar y comprobar una nueva publicación en una carpeta distinta, conservar el artefacto anterior para recuperación y sustituir solo la Web cuando corresponda al alcance autorizado. No sobrescribir binarios cargados ni tratar F5 como despliegue. Si el esquema cambia, aplicar el protocolo de migración y comprobar destino; un reinicio corriente no implica ejecutar migraciones o preparación de datos.

**Arranque completo:** los iniciadores generales son apropiados cuando el alcance incluye servicios y dependencias. Leerlos antes de ejecutarlos; no suponer que solo lanzan Web.

## Comprobar el resultado

Verificar que el nuevo proceso usa el artefacto esperado, que el puerto interno responde y que los accesos local/LAN pertinentes conservan su comportamiento. Revisar registros de arranque y readiness por la ruta interna permitida; el proxy bloquea readiness y una respuesta externa denegada no demuestra por sí sola que Web esté caída. Una redirección de acceso no prueba una sesión autenticada y omitir la validación TLS no acredita confianza del certificado.

Informar qué se reinició o actualizó, publicación utilizada, comprobaciones y límites. Si falla el arranque, diagnosticar con los registros y recuperar la publicación anterior cuando corresponda, sin modificar datos para ocultar el fallo.

## Fuentes y alcance

- [Lanzador](../../../scripts/run-local.ps1).
- [Inicio general](../../../scripts/iniciar-local.ps1) y [detención general](../../../scripts/detener-local.ps1).
- [Proxy LAN](../../../tools/Sgol.LanProxy/Program.cs).
- [Seguridad](funcional/seguridad.md) y [protocolo de implementación](desarrollo/PROTOCOLO_IMPLEMENTACION.md).

Se verificaron procesos, listeners y scripts mediante lectura. La actualización de Web del 04/11/2026 se comprobó con el proceso efectivo y respuestas 200 de readiness interno y acceso local/LAN; el proxy se conservó. El estado efectivo debe comprobarse de nuevo al realizar la operación.
