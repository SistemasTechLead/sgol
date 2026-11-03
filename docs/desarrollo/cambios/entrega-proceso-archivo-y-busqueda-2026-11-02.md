# Entrega de proceso: archivo conservado y búsqueda de siete días

## Decisiones y diagnóstico

Cambio mixto, continuación de `correccion-entrega-proceso-2026-11-02.md`. El usuario confirmó 117 ítems y eligió buscar el primer hueco completo entre personas compatibles durante los próximos siete días. La fecha/hora más temprana tiene prioridad; la ocupación desempata. Se conservan turnos, descansos, capacidad normal, roles, especializaciones, límites fijos, anclas críticas, historial e idempotencia. No se divide la carga ni se fuerza una excepción por defecto.

El proceso compartido sigue en su primera etapa EXCEL. La reserva de la etapa receptora conserva una fecha nominal del 30/10, mientras se intenta entregar el 02/11. `EnableItemRecipient` buscaba candidatos por carga del día nominal y volvía a usar esa fecha antigua. El nombre mostrado en un conflicto era la reserva actual, no prueba de una nueva asignación guardada. Las consultas de solo lectura no encontraron una entrega persistida.

Los 117 ítems multiplicados por un minuto producen 117 minutos de carga; el margen capturado de 50 % requiere 176 minutos continuos. La copia de la simulación no ofrece un hueco normal para ese bloque entre los candidatos durante los siete días evaluados. Carga diaria y hueco continuo son restricciones distintas; no se afirma que Mariana carezca de carga. En la copia utilizada había carga computable y reservas que fragmentan el turno.

El archivo se perdía porque el POST sustituía el documento HTML completo. Los navegadores no rellenan automáticamente un selector de archivos tras esa navegación.

## Implementación

- `OperationalProcessService.Items.cs`: la opción existente `LOWEST` busca desde la fecha operativa actual hasta seis días después, evalúa bloques completos y capacidad por persona/día sin mutar candidatos descartados, y elige primero la hora más temprana. Solo la decisión explícita actualiza la reserva de la siguiente etapa y registra su persona y horario. La vía normal sin esa opción conserva su comportamiento. Si no hay hueco, se revierte la entrega y se ofrece dejar pendiente; no se inventa una asignación.
- `_ProcessActionForm.cshtml` identifica la reserva actual, diferencia carga y bloque con margen, explica la búsqueda y conserva los controles existentes. `Index.Operational.cs` comunica la persona, fecha y horas resultantes tras una búsqueda aceptada.
- `process-items.js`: los formularios con Excel/PDF/capturas envían el POST normal mediante fetch. Ante 400/409/422 reemplazan únicamente el aviso y las opciones de capacidad, conservando los selectores nativos de archivo y los campos. Se conserva el intento/clave, se actualiza CSRF y se enfoca el conflicto. Un cambio de intento exige recargar; un fallo de red conserva el material y permite reintentar. Un éxito navega al destino del servidor. `assigned-tasks.js` mantiene sus validaciones y delega la indicación de envío en este caso.
- No hay almacenamiento persistente de archivos en el navegador ni borrador nuevo en el servidor. La conservación cubre correcciones en la misma página; recargar, cerrar o cambiar de página sigue requiriendo seleccionar el archivo de nuevo. Las fotografías de cámara mantienen su flujo existente. Sin JavaScript permanece el POST normal.

## Entorno y evidencia

Repositorio y cambios previos conservados, incluidos el velocímetro y la corrección de rollback anterior. Aplicación compartida en `dotnet watch`, `src/Sgol.Web`, HTTP interno 7241 / LAN HTTPS 7440, `127.0.0.1:5440/sgol_local`. No hay migración ni escritura operativa compartida. Los fixtures y copias usan bases `sgol_task_checks_*` aisladas.

| Escenario | Resultado esperado / método | Estado y evidencia |
|---|---|---|
| Conteos, devolución, omisión por cero y snapshots | Servicios y formularios reales; no cambiar reglas existentes | Aprobado: `.local/process-retry-core-final.log`, `ITEMS_PHASE_CORE_PASS` |
| Excel tras dos conflictos consecutivos | Mismo archivo y conclusión; opciones y token renovados; foco en conflicto | Aprobado: Chromium/Edge y WebKit, 320 y 1280 px, `artifacts/process-retry/results.json` |
| Archivo sustituido, error de red, cantidad vacía | Mantener el nuevo archivo; validación nativa; reintento habilitado | Aprobado en los mismos cuatro recorridos |
| Temas y teclado | Controles dentro del ancho, foco/Tab y avisos legibles | Aprobado en light/dark/special, capturas `artifacts/process-retry/*-<tema>.png`; inspección visual de móvil y tema oscuro |
| Conflicto, propuesta obsoleta, fuerza autorizada y pendiente | HTTP 409 o redirect correcto, sin guardados parciales | Aprobado en los recorridos HTTP de capacidad con bono real |
| Entrega tardía y primer hueco futuro | Otra persona con turno más temprano gana; reserva programada, carga completa | Aprobado: `.local/process-retry-search.log`; este intento de suite falló después en la preparación del replay, no en la búsqueda |
| Reintento de la búsqueda | Un solo evento y reserva, usando el intento original | Aprobado: `.local/process-retry-complete.log`, `ITEMS_CAPACITY_HTTP_PASS` |
| Caso copiado de 117 ítems | Examinar siete días; si no cabe, error explícito y entrega sin guardar | Aprobado: `.local/process-retry-117-final.log`, `PASS exact copied case`; Excel sintético y reloj 17:10 sobre copia, no archivo original del usuario |
| Registros históricos y permisos | Mantener anclas críticas, roles, devoluciones concurrentes e historial | Cubiertos por suite HTTP de capacidad; comparar intentos, asignaciones y hechos económicos tras conflictos |
| Compilación | Web y comprobaciones en artifacts separados | Aprobado; `.local/process-retry-build.log` y compilaciones de las suites posteriores |
| Dispositivo físico / navegación sin JS | Safari físico, teclado virtual, lector de pantalla y conservación sin JS | No ejecutado; sin JS no se promete conservar el selector tras un POST |
| Entrega del usuario | Volver a elegir opción con la agenda vigente | No ejecutada; no se entregó ni se eligió responsable en su nombre |

Se corrigieron fallos del fixture de regresión: revisión de horario inmutable y exclusión de versiones solapadas (crear nueva revisión y marcar anterior sustituida), activación efectiva del primer hueco reservado y replay contra el intento original en vez de la etapa futura aún sin asignación. Las validaciones del producto no se desactivaron. La evidencia de esos intentos permanece en los logs locales `process-retry-final*.log`, `process-retry-verified.log` y `process-retry-search.log`.

No se realizó commit ni push. La búsqueda no garantiza que exista un hueco; cuando no existe, la opción de guardar entrega con siguiente etapa pendiente requiere la elección explícita del usuario.
