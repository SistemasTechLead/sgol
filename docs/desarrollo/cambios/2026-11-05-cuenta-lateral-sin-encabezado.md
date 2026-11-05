# Retirada del encabezado operativo y cuenta lateral

Fecha: 2026-11-05. Tipo: interfaz. Estado: desplegado en la app compartida; integración autenticada pendiente.

## Alcance e investigación

El layout compartido `src/Sgol.Web/Pages/Shared/_Layout.cshtml` producía título, ayuda general, identidad, cierre de sesión y acceso al menú móvil en una fila superior. `desktop-workspace.js` copiaba explicaciones a esa ayuda y también administraba filtros: se retira únicamente la primera responsabilidad. `sidebar.js` conserva el ancho de 72–224 px y el umbral compacto de 180 px.

Se elimina la fila superior en las pantallas operativas del layout. `_SidebarAccount.cshtml` reutiliza SessionSnapshot, SessionPresentation, PersonAvatarViewModel, el token antifalsificación y el manejador existente de logout. Nombre y rol acompañan al avatar cuando la barra está ampliada; la compacta conserva el avatar y su nombre accesible. El diálogo móvil muestra la cuenta completa y se abre desde un botón al inicio del contenido. Se retira la ayuda general de pantalla; las indicaciones de campos, errores y acciones permanecen. Los títulos originales permanecen disponibles para lectores de pantalla e impresión.

Acceso, errores del sistema y pantalla kiosco conservan su composición separada. No cambian datos, permisos, asignaciones, cierre de sesión ni contratos HTTP. Las modificaciones previas de arrastres no forman parte del cambio.

## Escenarios y evidencia

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Compilar Razor y parcial de cuenta | Compila sin errores | `rtk dotnet build src/Sgol.Web/Sgol.Web.csproj --no-restore -p:UseSharedCompilation=false -p:BuildProjectReferences=false -o .local/headerless-build` | Aprobado, 0 errores y advertencias |
| Tres temas y 320/768/1024/1440 px | Sin desbordamiento horizontal ni explicación general | Fixture de componentes con CSS/JS reales, Edge; `.local/headerless-checks/check.cjs` y capturas | Aprobado; no representa pantallas autenticadas |
| Compactar y ampliar por teclado | Nombre visible solo ampliada, avatar siempre | Fixture, Home/End sobre separador | Aprobado |
| Abrir cuenta y Escape | Cierre disponible; menú cierra y devuelve foco | Fixture, manejador real de components.js | Aprobado |
| Menú móvil | Abre navegación y cuenta completa, Escape cierra | Fixture a 320/768 px | Aprobado |
| Formularios, filtros, navegación por rol, avatar real y logout HTTP | Mismo comportamiento y autorizaciones | Instancia aislada autenticada con Razor candidato | No ejecutado: esta ejecución solo preparó build y fixture, no instancia autenticada |
| Pantallas reales, impresión, carga, vacío, error y éxito; dispositivos físicos | Composición correcta sin ocultar controles | Integración autenticada / dispositivo | No ejecutado; pendiente antes de declarar validación completa |
| Integridad de registros e historial | Sin escrituras ni cambios de modelo | Revisión del alcance del diff | No aplicable prueba de datos: cambio de presentación sin escrituras operativas |

La sintaxis de desktop-workspace.js y el diff de los archivos modificados se comprobaron. El chequeo global de whitespace detecta además un espacio previo en OperationalCarryover.Paging.cs, ajeno a este trabajo.

## Documentación y publicación

Se consultaron mediante Graphify y lectura Inicio, mapas/diseno, organizacion/reglas y docs/design/00-diseno-sgol y 02-estructura-y-navegacion de la bóveda. La descripción vigente todavía contiene cabecera superior y ayuda; se deja intacta porque el candidato no está desplegado. Al activarlo, actualizar 02-estructura-y-navegacion y las referencias aplicables de componentes/accesibilidad, verificar enlaces, mapa e inventario, actualizar el índice y reconciliar la copia versionada. No se requiere migración. No se hizo commit, push ni despliegue.

## Activación autorizada

El 05/11/2026 se activó `.local/runtime/headerless-sidebar-20261105/Sgol.Web.dll`, SHA-256 `E062B2444D7CF75CF04C2574F3A4729B850C3A091128F6DDAFF95E7783F76DC1`. Se reutilizó el ensamblado compilado en este chat para excluir cambios concurrentes posteriores de arrastres. Los documentos/hash del PDB frente al artefacto activo difieren solo en _Layout y _SidebarAccount, incluidos sus generados Razor. La carpeta conserva dependencias y configuración de la publicación anterior; se comprobaron referencias de ensamblado y arranque.

Sin parámetros force: solo Web se sustituyó (lanzador 13644, proceso 25960). Proxy 20652 conservado; readiness interno ready, acceso local y LAN HTTP 200 con TLS normal; CSS servido coincide con el candidato. Evidencia: `.local/headerless-checks/deployment-health.json`, registros Web y capturas de componente. No migraciones, pruebas de escritura compartidas ni commit/push. La publicación anterior permanece disponible. Se reconciliaron estructura, componentes, accesibilidad, instalación y base documental de la bóveda; integración autenticada sigue pendiente.
