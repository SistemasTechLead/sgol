# SGOL — Adenda 44 a F07: planificación contractual definitiva del frontend

## 1. Control de la adenda

| Campo | Valor |
|---|---|
| Tipo | Adenda F07 de inserción documental |
| Estado | `APROBADA ÍNTEGRAMENTE POR EL RESPONSABLE EL 2026-09-22 — INCORPORADA LOCALMENTE` |
| Número oficial | `44` |
| Tarea insertada | `TECH-UI-PLAN-001 — Inventario y planificación contractual definitiva del frontend` |
| Efecto si se aprueba e incorpora | Inserta una tarea documental posterior al cierre real del backend; no autoriza código |
| Conservación | No modifica F00–F07 congelados ni `Fuentes/`; la incorporación posterior se hará mediante adenda F07 nueva |

El responsable aprobó íntegramente esta adenda y ordenó su incorporación el 2026-09-22. `TECH-UI-PLAN-001` queda insertada al incorporarse este documento; la aprobación documental no autoriza código frontend, publicación ni despliegue.

## 2. Hecho que motiva la tarea

SGOL contiene API REST y una base Razor mínima, pero no un contrato integral de pantallas, recorridos, navegación, mensajes, estados ni orden de implementación. Implementar directamente desde la lista de endpoints produciría tres riesgos: duplicar reglas de autorización en el cliente, inventar DTO o navegación ausente y ocultar brechas entre F06 y la superficie real.

La tarea propuesta cierra primero el inventario por recorridos verticales y sólo después deriva un backlog frontend. No cambia semántica funcional del backend.

## 3. Puerta de entrada obligatoria

`TECH-UI-PLAN-001` sólo puede incorporarse y adquirir eficacia cuando se demuestre, desde cero y sobre un SHA exacto, todo lo siguiente:

1. `TECH-E2E-CV-04` está `Integrada/Terminada` y `CV-04` está cerrado.
2. `CV-05`, `HU-029`, `HU-032`, `HU-033`, `HU-034`, `HU-035` y `TECH-AUTH-001` están integradas y sus commits implementados son ancestros de `origin/master`.
3. Existe `TECH-E2E-CV-05`, está `Integrada/Terminada` y `CV-05` está cerrado.
4. No queda dependencia técnica o contractual insertada por adenda con estado distinto de `Terminada` que bloquee la tarea.
5. El usuario ha aprobado los borradores y ha ordenado explícitamente incorporarlos.

Foto reconciliada del 2026-09-22 sobre `origin/master = 32c3961ff67f79670d0824da71d4f70a06d1dc01`:

| Condición | Evidencia | Estado |
|---|---|---|
| CV-04 | PR `#58`, cabeza `945c966...`, run requerido `35772495865` `SUCCESS`, merge `00da83c...`, ascendencia comprobada | Satisfecha |
| HU-029, HU-032..035, TECH-OPS-001 y TECH-AUTH-001 | PR `#50..#54`, `#57` y `#56`; cabezas y merges comprobados como ancestros; checks requeridos `SUCCESS` | Satisfecha |
| CV-05 | Adenda 43 aprobada; PR `#59`, cabeza `5f92fbc...`, run requerido `35797088036` `SUCCESS`, merge `32c3961...`, ascendencia comprobada | Satisfecha materialmente; BR-G05 conserva una corrección de rótulos de trazabilidad |
| Dependencias insertadas | No se encontró adenda posterior a la 43 ni dependencia de código pendiente aplicable | Satisfecha en esta foto |
| Aprobación e incorporación | Aprobación íntegra y orden explícita recibidas el 2026-09-22 | Satisfecha para esta incorporación documental; no autoriza código frontend |

Un pipeline, merge o documento histórico inferido no sustituye estas comprobaciones. Si `origin/master` cambia, todas se repiten. Una discrepancia detiene la incorporación; no se corrige silenciosamente.

## 4. Tarea insertada

| ID propuesto | Resultado verificable | Dependencias | Ejecutar antes de | No incluye |
|---|---|---|---|---|
| `TECH-UI-PLAN-001` | Inventario exacto por recorridos, matriz rol/sesión/pantalla/contrato/API/DTO/guardas/estado/mensaje/accesibilidad/prueba, registro de brechas, trazabilidad propuesta y backlog frontend ordenado y aprobados | Puerta completa de la sección 3 | Cualquier cambio funcional de frontend posterior al backend | Razor, HTML, CSS, JavaScript, pruebas, migraciones, endpoints, DTO, rutas productivas, publicación o despliegue |

## 5. Fuentes cerradas

La tarea usa únicamente:

- `docs/INDICE_IDS.md` y los rangos aplicables de F05/F06/F07;
- adendas F07 aprobadas y eficaces;
- `docs/traceability/IMPLEMENTATION_STATUS.md` contrastado con Git;
- `docs/design/tokens.md`, `componentes.md`, `estados-y-mensajes.md`, `estados-de-dominio.md` y `accesibilidad.md`;
- `F07_ADENDA_02_TAREAS_DE_BASE_DE_INTERFAZ.md` y `F07_ADENDA_04_CRITERIO_DE_INTERFAZ.md`;
- contratos funcionales aprobados de autenticación, evidencia, validación, indicadores, auditoría y continuidad;
- endpoints, DTO, permisos y páginas existentes en el `origin/master` exacto verificado.

Una inferencia se etiqueta como tal y nunca se promueve a contrato. Una diferencia entre documento y código se registra como brecha y se resuelve antes de la historia afectada.

## 6. Salidas verificables

La tarea produce y somete a aprobación, fuera de `Fuentes/`:

1. `INVENTARIO_EXACTO_FRONTEND.md`.
2. `BRECHAS_Y_DECISIONES_PENDIENTES_FRONTEND.md`.
3. `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md`.
4. `TRAZABILIDAD_PROPUESTA_FRONTEND.md`.
5. `ACTA_DE_PREPARACION_FRONTEND.md`.

El inventario es verificable si:

- cubre los nueve recorridos obligatorios;
- identifica cada rol y condición de sesión;
- relaciona cada unidad UI con HU/CA/CP/CAT/RN;
- cita sólo rutas y campos reales o marca la brecha;
- explicita permiso, jerarquía, propiedad, estado y guardas del servidor;
- identifica CSRF, ETag/`If-Match`, idempotencia y concurrencia;
- cubre normal, foco, deshabilitado, error, cargando y vacío;
- separa mensajes existentes de mensajes faltantes;
- incluye teclado/accesibilidad, positivo, negativo, autorización, auditoría, no-efecto, datos sintéticos y navegador;
- deriva el backlog exclusivamente desde esa matriz.

## 7. Arquitectura de frontend conservada

Toda historia derivada debe mantener:

1. Razor Pages/MVC dentro de `Sgol.Web` con mejora progresiva.
2. API `/api/v1` y contratos internos existentes; una vista no accede directamente a tablas de otro módulo.
3. Mismo origen; CORS continúa deshabilitado.
4. Cookie `Secure`, `HttpOnly`, `SameSite=Strict`; el navegador no recibe JWT persistente.
5. CSRF ligado a sesión para toda mutación.
6. Autorización en servidor por permiso, rol vigente, recurso, jerarquía, sucursal y estado.
7. Traducción común de Problem Details y conservación segura de `correlationId`.
8. Tokens/componentes aprobados como única fuente visual.

Quedan prohibidos una SPA separada, repositorio frontend separado, nuevo CORS, autorización por ocultamiento, rol/puesto del cliente como autoridad, almacenamiento de secretos en browser y duplicación de reglas de dominio en JavaScript.

## 8. Regla de brechas de diseño

Antes de escribir una pantalla se verifica que todos sus componentes, estados y mensajes estén definidos en `docs/design`. Si falta cualquiera:

1. se detiene sólo esa pantalla;
2. se redacta la mínima propuesta documental;
3. se aprueba e incorpora la extensión de diseño;
4. se reanuda la historia sin ampliar funcionalidad.

No se copia el estilo de otra pantalla, no se introduce un valor visual literal fuera de la hoja de variables y no se usa un componente aproximado para evitar la decisión.

## 9. Prohibición de código antes de una orden de implementación

La preparación, revisión o aprobación parcial del inventario no autoriza:

- crear/editar Razor, HTML, CSS, JavaScript, C#, pruebas o artefactos productivos;
- asignar rutas de página o navegación no aprobadas;
- cambiar API, DTO, errores, permisos, jerarquía o persistencia;
- añadir Playwright o paquetes;
- abrir rama, worktree, PR, ejecutar pipeline, publicar o desplegar.

El primer código sólo podrá comenzar después de: puerta de sección 3 satisfecha, esta adenda incorporada, inventario/backlog aprobados y orden explícita para iniciar la primera historia.

## 10. Criterios de aprobación documental

La aprobación es íntegra y cubre:

- exhaustividad y exactitud del inventario;
- aceptación explícita de cada brecha, su resolución o su bloqueo;
- orden y granularidad del backlog;
- fronteras Razor/mismo origen/cookie/CSRF;
- alcance de pruebas y datos sintéticos;
- exclusiones y vocabulario de estado.

Una observación pendiente sobre una ruta, DTO, mensaje, navegación o componente impide iniciar la historia afectada, no la eficacia de este marco documental ni el trabajo documental o técnico independiente. Debe quedar vinculada a una decisión concreta anterior a esa historia y no se transforma en una “decisión durante implementación”.

## 11. Estados de avance

| Estado | Significado en esta iniciativa |
|---|---|
| Aprobación documental | Inventario/adendas aceptados; todavía no existe código ni publicación |
| Implementada localmente | Código y trazabilidad de una historia existen y pasaron validación local enfocada; no implica GitHub |
| Publicada | Rama/PR del hito existen; no implica aprobación ni merge |
| Integrada | Commit implementado incorporado y ancestro de `origin/master` |
| Terminada | Integración, evidencia completa, criterios satisfechos y ningún defecto bloqueante conocido |

Estos estados no son intercambiables. La aprobación de esta adenda no vuelve `Implementada localmente` ninguna historia frontend.

## 12. Exclusiones

No se incluye despliegue, proveedor cloud, cuentas externas, SSO/OAuth/OIDC, CORS, observabilidad productiva, alertamiento, migración histórica, exportación, KPI adicional, mensajería externa, nuevo canal o cambio a continuidad/antimalware.

## 13. Eficacia, integración y punto de parada

Esta Adenda 44 fue aprobada íntegramente y autorizada para incorporación el 2026-09-22. Su presencia en una rama local constituye incorporación documental local; sólo será `Integrada` cuando el commit que la contiene sea ancestro de `origin/master`.

La misma incorporación registra la Adenda 45 con el backlog derivado. Después de incorporar ambas:

1. no comienza código frontend;
2. `TECH-UI-PLAN-001` conserva trazabilidad documental separada de `TECH-FRONT-001`;
3. BR-G05 debe quedar corregida en el mismo cambio documental;
4. cada brecha API/diseño/mensaje/navegación bloquea sólo su historia consumidora;
5. una orden futura y explícita debe autorizar el inicio de la primera tarea técnica.

La integración de esta adenda no equivale a aprobación de despliegue, publicación remota ni comienzo de implementación frontend.
