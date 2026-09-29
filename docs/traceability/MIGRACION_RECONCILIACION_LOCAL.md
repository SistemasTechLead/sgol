# Reconciliación de tres cambios locales para migración

Fecha: 2026-09-29. Estado: **Implementada localmente**, sin publicación.

## Alcance y bases

Orden del usuario: revisar y reconciliar AGENTS.md, scripts/demo/run-cv03.ps1 y tests/Sgol.ArchitectureTests/Cv03DemoArchitectureTests.cs.

- Base del checkout original: bed96907d26170502d09f4530495c3fd5894bdcc.
- Base integrada utilizada: 0ab7dd3e977a3ec061476f5075bde8a5897d6bbe, merge de FRONT-013 por PR #79.
- Rama de reconciliación: codex/migration-continuity.
- Worktree: C:/Users/josej/.codex/worktrees/migration-continuity/SGOL.
- Comparación HEAD original → origin/master limitada a estos tres archivos: sin diferencias (git diff --quiet, exit 0). Por tanto, los cambios locales no estaban incorporados ni requerían una combinación de contenido concurrente.
- Se copiaron los tres archivos completos y se verificó SHA-256 contra el origen. No se descartó ni se reescribió su contenido.
- El checkout original permanece en su base y conserva las tres modificaciones. El duplicado es deliberado hasta finalizar la migración.

## Resultado por archivo

| Archivo | Decisión y motivo |
|---|---|
| AGENTS.md | Conservar íntegro: contiene la decisión vigente del usuario de trabajo local-first, publicación por hitos, validación proporcional y distinción entre avance local e integración. No altera contratos funcionales ni seguridad. |
| scripts/demo/run-cv03.ps1 | Conservar íntegro: elimina la ruta fija C:/Users/loret/Documents/ChatGPT/SGOL y obtiene la raíz desde el script; conserva rechazo de OneDrive, exige carpeta SGOL, archivos raíz esperados y coincidencia con la raíz resuelta por Git. |
| tests/Sgol.ArchitectureTests/Cv03DemoArchitectureTests.cs | Conservar íntegro: añade comprobaciones de portabilidad y ausencia de rutas de usuario fijas, manteniendo las restricciones del launcher. |

Dependencias funcionales: ninguna nueva; no se cambia producto, API, permisos, datos ni dominio.
Dependencias de código: launcher y prueba presentes en la base integrada; RTK sigue siendo requisito del launcher.
Dependencias contractuales: CV-03 y Adenda 23 conservan modo Automated y separación de demo/producto. No se modifica F00–F07 ni Fuentes. Las reglas operativas de AGENTS.md corresponden a las instrucciones actuales del usuario.

## Validación ejecutada en la base integrada con los cambios

1. scripts/ci/preflight.ps1: SDK 10.0.400; fuentesClean=true; fuentesRebaselinePending=false; sesión gh autenticada fuera del sandbox. Checkout modificado por estos tres archivos, como se esperaba.
2. Parser PowerShell de scripts/demo/run-cv03.ps1: cero errores.
3. dotnet restore tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --locked-mode: correcto; necesario porque el worktree nuevo no tenía artefactos restaurados.
4. dotnet build tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-restore --configuration Release: correcto, 0 errores y 0 advertencias.
5. dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --no-restore --configuration Release --filter FullyQualifiedName~Cv03DemoArchitectureTests: 2/2 correctas, ninguna omitida.
6. git diff --check: correcto.

Build y test se ejecutaron mediante rtk proxy con la ruta explícita del RTK instalado en el equipo origen.

## Límites

No se ejecutó la demo integral CV-03, PostgreSQL/Testcontainers, navegador, suite completa ni compilación de toda la solución: el alcance es reconciliar instrucciones y la guarda de rutas del launcher. La prueba de arquitectura inspecciona el contrato del script; no equivale a ejecutar la demo desde otro equipo.
La validación funcional en SistemasLoretta queda diferida hasta copiar el proyecto y restaurar sus servicios/artefactos. No se afirma que la migración esté completa.
No se hicieron push, PR, pipeline remoto ni merge. No se iniciaron FRONT-014, infraestructura o despliegue. La discrepancia documental de FRONT-013 se registró en CONTEXTO_Y_ESTADO.md del paquete, pero no se alteró su trazabilidad integrada como parte de estos tres cambios.

## Uso al migrar

Preservar el commit local de codex/migration-continuity y la base integrada. Copiar sólo origin/master omitiría otra vez estos cambios. Copiar sólo el viejo checkout principal dejaría abierto código atrasado.
No usar directamente la carpeta de este worktree como repositorio independiente: su archivo .git depende del repositorio común. La preparación posterior debe trasladar/reconstruir la relación Git y verificar HEAD, ramas y archivos.
La carpeta de proyecto de destino debe llamarse SGOL y estar fuera de OneDrive para cumplir el launcher conservado.
