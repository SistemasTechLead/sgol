# Comparación en los conflictos de actualización

Estado: publicado en la app compartida el 03/11/2026; verificación visual autenticada pendiente. Cambio de interfaz con ampliación del modelo de lectura; no cambia reglas de agenda ni persistencia.

## Alcance y comportamiento

`AssignmentRefreshService.BuildAsync/Block` conserva una copia de la asignación anterior y de la definición actual para cada incidencia con asignación identificada. Prefiere el registro original frente al candidato mutable del buscador. Incluye reservas de etapas sin asignación ejecutable. No modifica catálogo, avances, fechas, responsables ni autorización de actualización.

`RefreshCaseText.Changes` compara esos datos usando `AssignmentDefinition.Differences` y la instantánea anterior; presenta los valores de carga, hora, recurrencia, evidencia y demás campos cambiados. Respeta las exclusiones de calendario de etapas y marca calendarios históricos no verificables. La comparación representa diferencias con el catálogo, no una prueba de que un solo campo cause el conflicto.

`Pages/Tasks/Refresh.cshtml` presenta la comparación dentro del desplegable existente. Una vecina sin diferencias explica el reacomodo por cambios de otras asignaciones. Se conservan el motivo técnico y la codificación HTML de Razor. El caso `carryover` ofrece «Ir a Supervisión» hacia `/validaciones`, sin parámetros de sección.

Se reutilizan detalles, listas, enlaces y estilos existentes. Las entregadas y cerradas conservan el filtrado previo del servicio; el cambio no alcanza ejecución, revisión, historial persistido ni confirmación transaccional.

## Comprobaciones

| Escenario / acción | Resultado esperado | Entorno y prueba | Estado |
|---|---|---|---|
| Carga 20 → 45 | Mostrar ambos valores | `RefreshIssuePresentationChecks` | Aprobado |
| Hora 10:08 → 11:08 junto con requisito | Mostrar cambios simultáneos | Misma prueba | Aprobado |
| Construir comparación | Asignación original intacta | Comparación JSON antes/después | Aprobado |
| Solicitud pendiente | Enlace a `/validaciones` (Supervisión) | Comprobación del descriptor de UI | Aprobado |
| Etapa de proceso | No inventar cambio de calendario independiente | Prueba de presentación | Aprobado |
| Vecina sin diferencias | Comparación vacía | Prueba de presentación | Aprobado |
| Calendario histórico sin instantánea | Identificar dato no verificable | Prueba de presentación | Aprobado |
| Compilación Web/Razor y herramienta | Sin errores ni advertencias | Build con salida aislada | Aprobado |
| Compilación en salida habitual | Poder copiar ensamblados | `dotnet run` | Fallido: otra herramienta abierta bloqueaba archivos; resuelto con salida separada |
| Formulario real, datos de TAR-0215 y enlace en navegador | Comparación correcta del registro efectivo y navegación | App autenticada | No ejecutado; pruebas anteriores son de presentación, no integración |
| 320 px/escritorio, temas, teclado, texto largo | Detalles legibles, foco y enlace operables | Navegador | No ejecutado |
| Migración y escrituras operativas | No necesarias | Cambio de lectura | No aplicable |

Comandos aprobados:

```powershell
dotnet build tools/Sgol.TaskChecks/Sgol.TaskChecks.csproj --no-restore -o .local/refresh-supervision-checks-20261103 -v quiet
dotnet .local/refresh-supervision-checks-20261103/Sgol.TaskChecks.dll --refresh-issue-presentation
```

Ocho aserciones aprobadas. El árbol estaba limpio al empezar. Se publicó Web en `.local/runtime/refresh-supervision-link-20261103`, conservando el artefacto anterior. Lanzador 36368 y proceso 13876 verificados; proxy 25924 sin reinicio. Readiness interno y accesos HTTPS local/LAN: 200, sin omitir TLS. Sin migraciones ni escrituras operativas explícitas, commit o push.

## Referencia documental reconciliada

Consultadas la entrada de la bóveda, mapa de dominio, reglas, referencia de actualización de asignaciones, diseño consolidado y patrones. Con el cambio activado se actualizaron las notas existentes `docs/funcional/actualizacion-asignaciones.md` y `docs/design/04-patrones-y-estados.md` de la bóveda, además de instalación local y base documental. La copia versionada queda para la preparación de un commit documental; no se afirma validación visual o funcional autenticada.
