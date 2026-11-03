# Cobertura de cierre junto a la navegación

Cambio de interfaz: `_DirectionPlans.cshtml` agrupa la navegación y la acción en `planning-navigation-actions`; `navigation.css` alinea ambos con flex y permite reflujo. El botón incorpora `_Icon` con `clock`. Conserva permisos, tipo button, apertura del diálogo y texto visible; permanece fuera del nav.

Entorno: 03/11/2026, America/Mexico_City. Validación en instancia aislada https://localhost:7285, base `sgol_task_checks_desktop_20261103`, artefacto `.local/runtime/closing-button-20261103`. No se modificaron datos operativos. Se conservaron los cambios previos, incluidos los ya preparados en staging. No se activó este artefacto en el host compartido.

| Escenario | Esperado | Prueba y entorno | Estado |
|---|---|---|---|
| Escritorio 1440 | Botón a la derecha en la misma fila, icono visible | Página real autenticada, Chromium y WebKit | Aprobado |
| 1024, 768 y 320, tres temas | Acción visible sin desbordamiento | Misma página, capturas y dimensiones DOM | Aprobado |
| Enter y Escape | Abrir diálogo, cerrar y devolver foco | Ambos motores, cuatro anchos y tres temas | Aprobado |
| Compilación Razor | Sin errores | dotnet publish --no-restore | Aprobado |
| Dispositivo físico y lector de pantalla | Accesibilidad manual | No disponibles | No ejecutado |
| Guardar cobertura, concurrencia e historial | Contratos intactos | No se cambian servicios ni formularios | No aplicable |

Regresión: `tools/Sgol.MobileChecks/closing-button-checks.cjs`. Evidencias privadas en `.local/closing-button-20261103`. El primer intento de navegador falló por sesión antigua; autenticación nueva y ejecución completa aprobadas. Inspección visual de captura de escritorio aprobada. Sin commit ni push.

## Activación compartida

Activada por petición expresa del usuario: artefacto .local/runtime/closing-button-delivery-20261103/Sgol.Web.dll. Fuentes estables durante publicación y ensamblado/CSS idénticos a los probados. Readiness local aprobado y acceso LAN HTTP 200; navigation.css servido contiene el cambio. Se reinició solo Web y se conservó el proxy. Evidencia: .local/closing-button-20261103/deployment.json. Sin migraciones, commit ni push.
