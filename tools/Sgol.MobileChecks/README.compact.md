# Comprobaciones de normalización móvil compacta

Complementan `mobile-checks.cjs`; usan páginas Razor y controles reales con Playwright disponible en `NODE_PATH`. No usar los estados de sesión de otra ejecución sin comprobar vigencia.

Preparar una **instancia aislada**, su base copiada `sgol_task_checks_*`, y un directorio privado con:

- `origin.txt`: origen HTTPS local de la instancia.
- `state-alex.json`: storage state autenticado de Dirección, obtenido mediante acceso real.
- `routes-extended.json`: objetos `{ "route": "/ruta", "status": 200 }`, con IDs válidos del fixture y estados HTTP esperados. Un 503 esperado solo comprueba la página de error, nunca un detalle exitoso.
- Para escrituras, `isolated-database.txt` con el nombre de la base de prueba; verificar por separado la conexión efectiva del servidor. Los ejecutores de escritura exigen además el puerto dedicado 7284.

```powershell
node tools/Sgol.MobileChecks/compact-mobile-checks.cjs <directorio-privado>
node tools/Sgol.MobileChecks/compact-mobile-checks.cjs <directorio-privado> --themes-only
node tools/Sgol.MobileChecks/compact-mobile-checks.cjs <directorio-privado> --interactions-only
node tools/Sgol.MobileChecks/compact-mobile-forms.cjs <directorio-privado>
node tools/Sgol.MobileChecks/compact-mobile-roles.cjs <directorio-privado>
```

La primera suite recorre seis anchos en Chromium/Edge y WebKit, comprueba ayudas y cierres, temas/texto ampliado, formularios sin cambios por despliegue, selección/elegibilidad, cancelación, preferencias y retorno. `--themes-only` cruza las 43 rutas/variantes del manifiesto extendido con tres temas y texto al 100/200%; `--chromium-only` restringe el motor para diagnóstico. Reporta un motor no instalado como no ejecutado. No instala navegadores silenciosamente.

Los recorridos de escritura crean fixtures únicos: tareas sin asignaciones (editar, conflicto de revisión, desactivar y eliminar) y usuarios de tres roles (alta, contraseña inicial y rutas permitidas/denegadas). No borran catálogos ni bases. Los usuarios de prueba se conservan para inspección. Contraseñas y sesiones permanecen en el directorio privado y no se imprimen.

Ejecutar secuencialmente: cargar muchas páginas puede alcanzar la cuota HTTP, especialmente al interceptar recursos y deshabilitar la caché. La suite espera ante 429/503 transitorio y comprueba el resultado HTTP. No aumentar cuotas para convertir errores en aprobaciones. Conservar el primer fallo y el resultado de la repetición en la evidencia.

Los resultados y capturas no prueban Safari físico, lector de pantalla, teclado virtual, ni todas las operaciones históricas. Esos límites se documentan en el plan del cambio.

Para la densidad del catálogo real y comparación con el binario anterior:

```powershell
node tools/Sgol.MobileChecks/task-catalog-density.cjs <directorio-privado>
```

Necesita `origin.txt`, `state-alex.json` y `baseline-state.json`; el binario anterior debe estar en HTTPS localhost:7285 con una sesión válida y la misma base aislada del candidato. Es una prueba de lectura: mide posición del primer registro, temas, texto, desplegables, selección, campos y equivalencia de escritorio/impresión. Complementar con `compact-mobile-forms.cjs` para escrituras reales aisladas.

`node tools/Sgol.MobileChecks/mobile-hierarchy-checks.cjs <directorio-privado>` comprueba orden DOM de navegación/acción, conservación de FormData, seis anchos, texto ampliado e impresión en Chromium/WebKit. Usa `origin.txt` y `state-alex.json`; solo lectura sobre una instancia aislada.
