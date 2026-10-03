# SGOL — Propuesta de siguiente hito después de TECH-FRONT-005

Fecha: 2026-10-03, America/Mexico_City. Estado: **RESOLUCIÓN GLOBAL Y CONTRATO MÍNIMO APROBADOS E INCORPORADOS LOCALMENTE**. Aprobaciones literales: «Apruebo integramente la propuesta», «Apruebo la resolucion global» y «La apruebo integramente», esta última en respuesta a la solicitud de aprobación íntegra de §15. Adenda 62 incorpora §12; Adenda 63 incorpora exclusivamente §§15.1–15.8. No es una tarea de código aprobada. No se asigna un identificador nuevo. §§1–14/16 conservan investigación y entregas anteriores; §15 es el contrato aprobado y §17 registra su incorporación. BR-API04 sigue ABIERTA; implementación/ensayos/publicación no autorizados.

## 1. Resultado de la revisión y base aceptada

**Hecho:** no se encontró una tarea posterior realmente definida y vigente. El backlog base termina sus historias en HU-035; sus dependencias técnicas no insertan una sucesora del cierre frontend. Adenda 45 termina el orden efectivo en TECH-FRONT-005, orden 25, fila 51: «Habilita cierre frontend». FRONT-020 habilita precisamente esa tarea. La tabla «Tareas insertadas por adenda» y las adendas operativas hasta la 61 no definen una tarea posterior. Las Adendas 60/61 resuelven el alcance y la dependencia de lectura de este hito, sin crear una sucesora. No existen FRONT-021 ni TECH-FRONT-006 aprobadas en las fuentes revisadas.

**Decisión aprobada aportada por el responsable:** TECH-FRONT-005 está **Integrada**. Se acepta [PR #90](https://github.com/SistemasTechLead/sgol/pull/90), cabeza validada `04e2486e9f185629f7e291026adb98a4b52fdd0d`, merge en master `e6355332ea09542c2b38895945c61a1efb83d586` y [pipeline final SUCCESS, intento 1](https://github.com/SistemasTechLead/sgol/actions/runs/37135129548). Source controls, server, browser, operations y TECH-BASE-003 / PR gates finalizaron SUCCESS. HU-035 isolated network opcional permaneció SKIPPED. Plan, D1..D5, capturas, publicación y merge aprobados; seguimiento pausado y verificado; sin despliegue. Estos datos se aceptan como evidencia previa, sin volver a consultar o ejecutar los gates.

La eficacia del cierre se interpreta conforme a F07_ENMIENDA_001 §§3–4: PR, checks, run y merge son resolubles; los textos que aún dicen pendientes son históricos superados. No se modifica IMPLEMENTATION_STATUS.md ni se crea un commit administrativo para rellenar hashes. Este documento reúne la evidencia de base para la decisión siguiente, conservando los registros anteriores.

**Hecho local verificado:** preflight fue la única comprobación inicial del entorno, exit 0: árbol y Fuentes limpios, sin rebaseline pendiente; SDK de PATH 10.0.401 no resuelve global.json 10.0.400. No se diagnostica más el entorno. El checkout inicial estaba limpio en codex/tech-front-005, cabeza `04e2486e9f185629f7e291026adb98a4b52fdd0d`. El objeto del merge aún no estaba disponible; `git fetch origin master` lo obtuvo. Ascendencia de la cabeza aceptada en origin/master comprobada, exit 0. Rama documental `codex/propuesta-post-tech-front-005` creada desde el merge exacto; HEAD y ascendencia comprobados. No hubo cambios ajenos que aislar.

## 2. Evidencia integral que se conserva

Los dos ciclos completos pertenecen exclusivamente a `3693239e181782386d5038d8be4232341fa53a89`, artefacto local `.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a`.

| Capa | Ciclo 1 | Ciclo 2 |
|---|---:|---:|
| INTEGRAL | 2/2 PASS | 2/2 PASS |
| FRONT_BROWSER | 35/35 PASS | 35/35 PASS |
| PostgreSQL/API | 181/181 PASS | 181/181 PASS |
| S3/ClamAV | 1/1 PASS | 1/1 PASS |
| Contratos/fórmulas | 166/166 PASS | 166/166 PASS |

Total 770/770, cero fallos/omisiones; 36 celdas R1..R9 × escritorio/móvil × dos ciclos PASS. HTTPS/backend real; Chromium 151.0.7922.34, Windows AMD64/Docker Linux AMD64; móvil emulado, sin acreditación de dispositivo físico. Cuatro reportes cleanup true, 88 certificados propios ausentes y 212 capturas sanitizadas revisadas y aprobadas. Duración documentada aproximada de los dos ciclos: 61 minutos, incluido aprovisionamiento y atención humana; no mide latencia ni SLO.

La comparación Git entre esa SHA y la cabeza validada sólo enumera documentación, clasificación CI y protección de pasos CI. No cambian src/, tests/ ni scripts/demo. Los 770 resultados no se atribuyen a la cabeza posterior ni al merge. El pipeline final acredita sus checks sobre la cabeza validada; no representa una nueva ejecución de los dos ciclos integrales.

## 3. Pendientes clasificados y tratamiento

| Clase | Hecho documentado | Efecto en esta propuesta |
|---|---|---|
| Brecha contractual global | BR-API04 permanece **ABIERTA**. Las exclusiones de descarga/preview en FRONT-018/019/020 y TECH-FRONT-005 no la cierran | Requiere decisión explícita sobre alcance global antes de derivar trabajo de producto. No constituye por sí sola una tarea autorizada |
| Exclusión contractual heredada | TAR-0026 sólo configuración/políticas y negativos. Fuente persistida de servicio/vencimiento/recibo y cadena CAT-006 excluidas por Adendas 13/15 y D4 | Conservar exclusión. No sembrar su cadena ni añadir backend. No presentarla como defecto descubierto o CAT-006 PASS |
| Carencia documental | No se localizaron los cuatro inventarios de Adenda 44 en copias operativas | Conservar el límite de localización. No reconstruir ni atribuirles contenido, ni buscar en Fuentes. D1/Adenda 60 son la fuente aprobada de R1..R9 |
| Defectos conocidos corregidos | D5: traza previa a asignación devolvía AUDIT_SCOPE_INCONSISTENT. CI: perfiles integrales seleccionados en server y huella protegida desalineada | Resueltos en el alcance aprobado y cierre aceptado. No reabrirlos como tareas ni repetir gates |
| Validaciones diferidas | Ensayos de §5 no ejecutados/acreditados en su plataforma o alcance específicos | Mantener cada causa; ningún PASS ajeno los sustituye |
| Preparación operativa | No hubo despliegue; aislamiento productivo SeaweedFS no acreditado | No autoriza cloud, cuentas, infraestructura o ensayo productivo. Dependería de otro hito explícito |
| Cleanup histórico con evidencia incompleta | Informe, registro de desarrollo sobre 8e274863: limpieza de contenedores/red/imagen/certificado verificada; eliminación de temporal privado/PFX rechazada por revisión automática, «blocked by policy», pendiente confirmación externa en aquel registro | No inferir que sigue presente ni que los cuatro cleanup finales prueban su ausencia. No abrir secretos ni intentar eludir el bloqueo. Una verificación puntual de propiedad/ausencia, si se autoriza, tendría evidencia separada |

No se identifica en estas fuentes un defecto productivo actual demostrado y pendiente dentro del alcance integrado. Esto es una conclusión limitada a la revisión documental autorizada, no una nueva auditoría exhaustiva de código, seguridad o producción. El fallo previo PUT/S3 WebKit sigue documentado sin causa productiva confirmada; no se transforma en un defecto con reparación autorizada.

## 4. Prioridad recomendada y alcance propuesto

**Inferencia:** al agotarse el orden aprobado, conviene resolver primero qué significa la brecha global BR-API04 para la siguiente etapa. Su exclusión consumidora permitió cerrar frontend, pero no decide el alcance global. Ensayar otra suite o preparar cloud no resuelve esa decisión.

**Propuesta de único siguiente hito:** «Delimitación contractual de BR-API04 y decisión sobre su tratamiento en el MVP». Hito documental, sin ID de backlog asignado, sin implementación ni ensayos técnicos en esta fase.

Resultado propuesto: una resolución revisable que establezca si BR-API04 permanece abierta con descarga/preview diferidos globalmente por decisión expresa, o si se requiere preparar un contrato mínimo para una futura tarea. Recomiendo resolver esa elección primero; no recomiendo cerrar BR-API04 por las exclusiones existentes. El responsable conserva la decisión funcional.

Alcance tras aprobar íntegramente esta propuesta:

1. Localizar mediante INDICE_IDS y referencias consumidoras únicamente los contratos de evidencia y autorización que sustenten BR-API04; consultar sus rangos precisos, sin reconstruir F00–F07.
2. Comparar sólo la superficie afectada para separar contrato disponible de ausencia documental. Inventariar lectura de versión vigente/histórica, autoridad por rol/recurso/jerarquía/estado, transporte privado y tratamiento de errores, sin asignar rutas, DTO, permisos ni reglas por inferencia.
3. Presentar la decisión global y sus consecuencias exactas. Si se elige preparar capacidad futura, detallar preguntas de descarga y preview por separado: finalidad, versiones accesibles, autoridad, exposición de binario, caducidad/revalidación, auditoría, errores y criterios de seguridad. Cada elemento ausente será pregunta o propuesta, nunca contrato vigente.
4. Registrar costo y dependencias de la opción elegida y un punto de parada previo al código. Cualquier nueva tarea/ID deberá aprobarse e incorporarse expresamente por adenda, comprobando disponibilidad del número; no se asigna ahora.

**Dependencias:** base integrada aceptada; Adendas 57..61 y políticas de evidencia existentes; decisión funcional del responsable. No hay dependencia de código pendiente identificada para realizar este hito documental. La implementación de descarga/preview sí dependería de un contrato nuevo aprobado, tarea definida y orden expresa de implementarla.

**Costo estimado, no medido:** 2–4 horas de revisión y redacción acotadas, más una revisión humana; sin servicios externos ni ejecución integral. Si la lectura mínima revela una contradicción, detener esa parte y presentar sus dos fuentes. La estimación no autoriza ampliar la revisión indefinidamente.

Alternativa técnica de mayor prioridad después de esa decisión: ensayo enfocado de la carrera sustitución de evidencia contra emisión, por su impacto potencial en integridad/historia. No se propone ejecutarlo dentro de este hito documental; su plataforma y costo se precisan en §5. Accesibilidad nativa sería el siguiente candidato de uso real. Preparación productiva queda subordinada a objetivo operativo y entorno expresamente aprobados, no al simple término del backlog.

## 5. Diferidos conservados y comparación de futuras opciones

Esta tabla no autoriza ejecutar los ensayos. Costos son estimaciones iniciales de preparación/ejecución/documentación, sin incluir correcciones desconocidas, compras o disponibilidad humana. No equivalen a un presupuesto aprobado ni a duración observada.

| Diferido / causa vigente | Plataforma necesaria si se decide ensayarlo | Evidencia exigible | Costo estimado / dependencia |
|---|---|---|---|
| Zoom nativo no ensayado | Windows AMD64, navegador instalado con zoom nativo real sobre HTTPS | Versión, SHA, niveles de zoom y acciones reales; controles, reflow, teclado/foco y resultado por caso | 2–4 h; operador y matriz concreta aprobada |
| Lector de pantalla no ensayado | Windows AMD64, lector real y navegador compatibles, versiones identificadas | Lectura/anuncios, nombres y orden, errores, diálogos y retorno del foco con operador; registro sanitizado | 4–8 h; lector disponible y operador competente; no instalar por esta propuesta |
| Dispositivos físicos no ensayados | Dispositivos reales y navegadores/versiones seleccionados | Acciones sobre dispositivo, viewport/entrada, HTTPS y resultados por recorrido seleccionado | 4–8 h más disponibilidad de equipos y acceso de red; emulación no sustituye |
| PUT/S3 Windows WebKit previo sin HTTP | Windows AMD64, Playwright WebKit y S3/ClamAV reales bajo transporte original identificado | Evento/respuesta HTTP real o fallo primario cerrado; cuarentena, tipo/SHA-256/escaneo/vínculo y cleanup | 3–6 h para diagnóstico enfocado; causa aún no probada. Chromium u otro navegador no acreditan el caso; cambiar TLS requeriría decisión aparte |
| Aislamiento productivo completo SeaweedFS no acreditado | Entorno objetivo representativo y autorizado, accesos/topología definidos | Denegaciones de acceso y redes/buckets, protección de evidencia y escáner, configuración revisable y cleanup propio | 1–2 jornadas más provisión; costo infra desconocido. Sin entorno aprobado no se ensaya ni se estima como validado |
| Carrera sustitución de evidencia contra emisión no reensayada | Windows AMD64/Docker Linux AMD64, PostgreSQL real y comandos API existentes | Barrera/estado real; ambos órdenes relevantes, resultados, ETag, versión/decisión vigente, historia, auditoría transaccional, rollback/no-efecto | 4–8 h; lectura previa de contratos y reutilización de pruebas existentes. Playwright ni otra carrera sustituyen la persistencia |
| Navegador con 100000 diferencias diferido por costo | Navegador real/versionado con backend y colección sintética completa | Conteo completo, rechazo de truncado, tiempos medidos por condición/evento real, recursos observados y no aceptación inadmisible | 4–8 h y posible sesión prolongada; primero aprobar objetivo de medición. La colección unitaria completa no prueba rendimiento de navegador; capturas no son latencia |
| Isolated network histórico opcional SKIPPED | Plataforma Docker Linux AMD64 y topología específica del experimento existente | Ejecución propia con SHA/comandos, salida final real y cleanup; resultado separado de los cinco requeridos | 2–4 h más provisión; mantener opcional. No elevarlo a requerido ni sustituir con otro gate |

Se mantienen **Validación diferida**, fallo previo sin HTTP y SKIPPED con sus significados distintos. No se les asigna PASS. Un defecto demostrado durante un ensayo futuro exige causa y propuesta mínima separada; no autoriza reparación de producto automática.

## 6. Exclusiones y límites obligatorios

Sin código, vistas, marcado, estilos, endpoints, DTO, permisos, indicadores, exportación, descarga/preview implementados, migraciones, dependencias, reglas cliente, productores TAR-0026/CAT-006, siembras que eviten UI, reparaciones, observabilidad productiva, cloud, cuentas externas o despliegue. Sin push, PR, checks remotos, merge, heartbeat o apertura automática de otro chat/hito. Autorizaciones del hito cerrado no se extienden a éste.

Mantener TAR-0005/TAR-0026 recurrentes y TAR-0018 manual. D5/Adenda 61 sólo permite en traza Dirección hechos previos reconocidos como DEPENDENCY sin sujetos bajo sus vínculos/condiciones cerrados; no extender a productores, escrituras, permisos, DTO o endpoints.

Preservar cinco indicadores, dos universos, fórmulas, ceros y denominadores; CONCLUIDA no significa validación vigente. Indicadores/auditoría GET puros, minimización/jerarquía/cadena completa. Continuidad GET registra RECOVERY_RECONCILIATION_VIEWED; Preparar/Cancelar sin API/efectos; aprobación sólo MATCHED admisible, nunca DIFFERENT, FAILED o truncado. Mantener CSRF, idempotencia, ETag/If-Match, auditoría transaccional, decisiones sustituidas e historia inmutable. Evidencia privada, cuarentena/tipo real/SHA-256/antimalware y vínculo sólo LIMPIO.

No se escribe UI en este hito. Si una propuesta futura necesitara UI, antes de escribirla aplicaría las lecturas de diseño exigidas por el prompt, Adenda 55 y extensiones consumidoras; cualquier componente/mensaje ausente exigiría decisión. No se leen originales de IdentidadMarca ni se improvisan valores visuales.

## 7. Archivos previstos, validación, aislamiento y cleanup

**Esta revisión:** único archivo nuevo `docs/traceability/PROPUESTA_POST_TECH_FRONT_005.md`. No se modifican los cinco documentos revisados, IMPLEMENTATION_STATUS, índice, adendas aprobadas ni documentos congelados. La trazabilidad de esta propuesta y su base se concentra en este archivo; no declara implementación del siguiente hito.

**Tras aprobación y orden de ejecutar el hito documental:** ampliar este mismo archivo con la resolución y fuentes precisas. Sólo si se aprueba una decisión contractual para incorporación, preparar una adenda F07 nueva en raíz con número libre verificado y actualizar referencias pertinentes de INDICE_IDS/IMPLEMENTATION_STATUS en el mismo entregable; su redacción no convierte propuestas en contratos aprobados. El conjunto exacto se confirmará con la decisión, sin crear otro plan paralelo. src/, tests/, scripts/demo, CI, global.json y dependencias no son archivos previstos de este hito.

Validación proporcional: revisar IDs y referencias fuente, clasificación de cada pendiente, separación de SHA/resultados, exclusiones y coherencia de decisiones; comprobar alcance con Git y whitespace. No build, restore, formato, navegador ni suites por una propuesta Markdown. No consultar GitHub como diagnóstico ni repetir evidencias aceptadas. SDK futuro fijado a `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe` si otro hito aprobado exige ejecución.

Aislamiento: rama codex/ desde merge aceptado, preservando cambios ajenos. Si se ocupa el checkout, worktree separado desde base aceptada; sin reset/stash o traslado de cambios ajenos. Un commit local sólo si ayuda al entregable aprobado; no commit administrativo ni publicación por esta propuesta.

Cleanup actual: la investigación no crea servicios, contenedores, volúmenes, certificados, temporales sensibles ni datos. No requiere limpieza funcional o infraestructura. Los reportes/capturas anteriores se conservan; no se abren contenidos de evidencia ni secretos. Fuentes, ZIP, Excel y originales de marca quedan protegidos. Para cualquier ensayo futuro, inventario exclusivo de recursos propios, rutas absolutas verificadas antes de borrado recursivo, finally y ausencia comprobada; nunca prune/volúmenes/certificados ajenos. Certificados en modo Manual atendido por el responsable, sin Computer Use, aceptación automática ni modificación de confianza por inferencia. EICAR sólo alrededor de PRIVATE_S3_CLAMAV con restauración del valor previo en finally si ese ensayo llegara a aprobarse.

## 8. Fuentes exactas y límites de la revisión

| Fuente operativa | Sección/rango pertinente y uso |
|---|---|
| AGENTS.md y prompt del responsable de 2026-10-03 | Fuente de autorización actual, orden incremental, protección, límites y evidencia de integración aceptada |
| INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md | §§1–3, 8: solicitud de plan, aprobación previa, cierre y límite de autorización |
| docs/traceability/IMPLEMENTATION_STATUS.md | Bloque inicial TECH-FRONT-005, líneas 3–39; «Tareas insertadas por adenda», líneas 1070–1110: precedencia y registros históricos |
| docs/INDICE_IDS.md | TECH-FRONT-005 → Adenda 45 línea 51; FRONT-020 → línea 103; HU-035 y TECH-OPS-001 → backlog; TOOL-FLOW-001 → Enmienda 001 |
| F07_BACKLOG_DE_IMPLEMENTACION.md | Final de §6, líneas 101–111; §§7–8, líneas 114–127: última historia y dependencias técnicas |
| F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md | §4 línea 51, §5.6 línea 103 y §§6–8: último orden, habilitación, H4 y punto de parada |
| F07_ADENDA_44_PLANIFICACION_CONTRACTUAL_DEFINITIVA_DEL_FRONTEND.md | §6: cuatro inventarios además del backlog; ausencia ya registrada en el plan revisado, no nueva búsqueda en originales |
| F07_ADENDA_57_CONTRATO_CONSUMIDOR_FRONT_018.md | Línea 7: exclusión consumidora, BR-API04 global abierta |
| F07_ADENDA_58_CONTRATO_CONSUMIDOR_FRONT_019.md | Línea 7: mismo límite de exclusión |
| F07_ADENDA_59_CONTRATO_CONSUMIDOR_FRONT_020.md | Línea 5: alcance consumidor, límites y BR-API04 |
| F07_ADENDA_60_CONTRATO_DE_DEMO_INTEGRAL_FRONTEND.md | D1..D4: fuente de recorridos, exclusiones, continuidad real y TAR-0026 |
| F07_ADENDA_61_TRAZA_DE_GENERACION_ANTERIOR_A_ASIGNACION.md | «Decisión y límite» y «Evidencia y seguridad»: D5 cerrada, sin sucesora ni ampliación |
| F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md | §§3–4: eficacia del cierre y referencias resolubles sin commit administrativo |
| docs/traceability/TECH_FRONT_005_PLAN_DE_IMPLEMENTACION.md | §§2–3 carencias/BR-API04; §§8–11 aislamiento/costo/diferidos; §§13–15 D4/D5/cierre final, que superan pendientes anteriores |
| docs/traceability/TECH_FRONT_005_DEMO_INTEGRAL.md | «Evidencia final verificable», «Publicación — primera ejecución remota y corrección de selección» e incidencias de desarrollo: SHA, costo, cleanup, causas y límites |
| docs/traceability/TECH_FRONT_005_COBERTURA_FINAL.md | Introducción, «Capas reutilizadas y propósito», «CAT y límites»: D5 resuelta, CAT-006 excluida y diferidos |
| docs/traceability/TECH_FRONT_005_CAPTURAS.md | Introducción y «Entrega visual final»: revisión aprobada, emulación y ensayos nativos no acreditados |
| docs/operations/tech-front005-demo.md | «Alcance aprobado», «Fixtures, aislamiento y evidencia», «Reportes y cleanup», párrafo final Manual: operación local, límites y propiedad |

Adendas 13 §§1–2/15 y 15 §10 se conservan por referencia expresa de D4 en el plan y Adenda 60; no se reanalizan completas. F05/F06 no se reconstruyen ni se usan para inventar trabajo: sus rangos mínimos se leerían únicamente para la decisión BR-API04 si se aprueba este hito. El inventario de nombres de adendas operativas se limita a archivos de raíz, hasta la 61; sin búsquedas recursivas en Fuentes, bin, obj o .vs. Las rutas exactas de los cuatro documentos TECH_FRONT_005 se localizaron sólo en docs/traceability.

## 9. Decisiones necesarias y criterios de aceptación

**Solicitud original, resuelta por la aprobación indicada al inicio:** aprobar íntegramente esta propuesta y ordenar ejecutar únicamente el hito documental de §4. Esa aprobación permite la revisión mínima y redacción local descritas; no aprueba de antemano una solución para BR-API04, un ID nuevo ni implementación. La decisión global sobre la brecha se someterá a aprobación antes de incorporarla como contrato. Ningún ensayo de §5 se inicia automáticamente.

Se considerará terminado el hito documental propuesto cuando:

1. La ausencia de sucesora vigente y la base integrada aceptada queden explícitas, sin IDs inventados ni gates históricos repetidos.
2. BR-API04 tenga una resolución propuesta con fuentes y consecuencias, separando contrato vigente, carencias, preguntas y decisiones del responsable. Hasta aprobación global permanece ABIERTA.
3. CAT-006/TAR-0026, D5, nueve recorridos y todos los diferidos conserven sus límites; fallos históricos no se borren ni se atribuyan a otra SHA.
4. La opción siguiente tenga alcance/dependencias/costo/archivos/validación y autorización definidos, con parada previa al código; ninguna ausencia se convierta en función autorizada.
5. El entregable único sea revisable, sin secretos ni cambios fuera de alcance, y pase revisión documental y git diff --check.

Estado al entregar la propuesta original: revisión y preparación satisfechas, hito siguiente todavía no iniciado; espera de aprobación íntegra, posteriormente recibida. La ejecución documental actual y su punto de parada se registran en §§10–14.

Comprobaciones de esta entrega: preflight exit 0 con incompatibilidad SDK de PATH registrada; fetch y ascendencia Git exit 0; revisión documental de fuentes/IDs/alcance efectuada; git diff --check exit 0. El archivo nuevo también se comprueba explícitamente frente a NUL mediante git diff --no-index --check: sin diagnósticos de whitespace; su exit 1 indica diferencia de archivos en modo no-index, no fallo de un script de validación. Git advierte conversión LF→CRLF conforme a la configuración existente, sin modificarla. No se ejecutaron build, restore, formato, pruebas o checks remotos: no son proporcionales a esta propuesta documental. Los ensayos de §5 permanecen diferidos por sus causas propias.

Nombre sugerido del siguiente chat, sólo si el responsable decide abrirlo: **SGOL — Decisión contractual BR-API04 después del cierre frontend**.

Mensaje listo para copiar después de la aprobación:

> Aplica AGENTS.md e INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md. Ejecuta únicamente el hito documental aprobado en docs/traceability/PROPUESTA_POST_TECH_FRONT_005.md, sobre una base que incluya e6355332ea09542c2b38895945c61a1efb83d586. Prepara en ese mismo documento la resolución global propuesta de BR-API04 con lectura mínima de fuentes y espera mi decisión antes de incorporarla como contrato. Conserva TECH-FRONT-005 Integrada, SHA real de los dos ciclos, D4/CAT-006, D5 y todos los diferidos. No asignes un ID nuevo, implementes, ensayes diferidos, publiques, despliegues ni inicies otro hito automáticamente.

## 10. Ejecución del hito documental aprobado

La aprobación íntegra permite ejecutar §4, conservando el único entregable. Se realizó lectura mínima de F05/F06, Adendas 15/17/18 y referencia consumidora de FRONT-018; se compararon únicamente registro de rutas de evidencia, servicio de consulta e interfaces/adaptador de almacenamiento afectados. No se implementó, compiló ni ejecutó la aplicación. BR-API04 sigue **ABIERTA globalmente**.

Preflight nuevamente como única comprobación inicial de este turno: exit 0, HEAD `e6355332ea09542c2b38895945c61a1efb83d586`, rama codex/propuesta-post-tech-front-005, Fuentes limpia/sin rebaseline pendiente, incompatibilidad SDK de PATH conservada. treeClean=false corresponde exclusivamente al Markdown nuevo preparado en este chat; no hay cambios ajenos registrados. No se cambia de base ni se hace fetch nuevo, publicación o consulta de gates aceptados.

### 10.1 Contrato declarado, exclusión e implementación

| Aspecto | Hecho comprobado y fuente exacta | Consecuencia |
|---|---|---|
| Descarga declarada en contrato general | F06_CONTRATO_DE_API.md §5.6, líneas 189–201: `GET /files/{id}/download` bajo prefijo `/api/v1`, URL firmada corta, autorización revaluada, archivo limpio, sin CDN público, expiración cinco minutos endurecible por configuración | Existe una definición general aprobada. Sería incorrecto afirmar que no hay ninguna ruta o regla documental de descarga |
| Exclusión de entrega HU-025 | Adenda 18 §3.2, línea 55, excluye expresamente descarga y `GET /api/v1/files/{id}/download`; §5, líneas 87–98, lista sus seis rutas originales | La exclusión limita esa tarea; no elimina silenciosamente la definición de F06 ni cierra BR-API04 global |
| Exclusiones consumidoras posteriores | FRONT_018_PLAN_DE_IMPLEMENTACION.md §3, línea 32; Adendas 57 línea 7, 58 línea 7, 59 línea 5 y 60 D2 | Consulta de metadata/historia sin abrir binarios; cierre integrado válido para el alcance aprobado, sin descarga/preview |
| Ruta implementada | EvidenceApiEndpoints.cs líneas 19–28 registra carga, confirmación, estado, aporte, reemplazo, listado y revisión. Búsqueda acotada del registro de endpoints no encuentra descarga/preview | La superficie de descarga declarada en F06 no está implementada en la base revisada. No se probó una respuesta HTTP, 404 ni un comportamiento de runtime |
| Capacidad técnica de almacenamiento | IPrivateObjectStorage, EvidenceInfrastructureContracts.cs líneas 128–159: firma de carga a cuarentena, metadata, lectura interna acotada, promoción, borrado técnico y disponibilidad. S3PrivateObjectStorage.cs líneas 28–55 firma PUT a cuarentena; OpenReadAsync es lectura interna | Firma de carga y stream interno no constituyen contrato de descarga para usuario; no se reutilizan como permiso ni endpoint nuevo por inferencia |
| Privacidad/limpieza | ADR-007, F06_REGISTRO_ADR.md líneas 105–113; Adenda 17 §5.2–5.3, líneas 61–84 | Sólo limpio, objeto privado, sin acceso público/CDN/URL permanente; TLS fuera de Development/CI conforme a las condiciones aprobadas |
| Versiones | CA-025/CP-025-P/N, F05_CRITERIOS_DE_ACEPTACION.md línea 55; HU-025, F05_ESPECIFICACION_FUNCIONAL_MVP.md línea 140; Adenda 18 §12 líneas 339–381 | Ambas versiones son consultables como historia. Esto no define por sí solo el transporte ni concede lectura binaria |
| Preview | No aparece una ruta de preview en la sección pertinente de F06 ni en el registro de endpoints revisado; exclusiones consumidoras la mencionan | No equiparar preview a descarga ni inventar renderizador, transformación o endpoint. La conclusión se limita a estas fuentes |

**Diferencia contractual a decidir:** F06 §5.6 declara descarga; Adenda 18 §3.2 excluye su implementación en HU-025. No se interpretan como eliminación aprobada de la capacidad global. La expresión histórica «sin contrato» se precisa aquí como **sin contrato operativo completo e implementación de descarga dentro de las tareas consumidoras aprobadas**. No se editan esos documentos históricos/congelados. No se detiene la revisión independiente; sí se detiene cualquier derivación de producto o exclusión global hasta decidir §12.

### 10.2 Autoridad, versión y límites que ya existen

La consulta de metadata/versiones requiere PER-TAREA-VER y alcance de HU-023 según Adenda 18 §12. Adenda 15 §§3–6 exige sesión individual/cuenta/empleo/rol vigentes, LOR-001 y autoridad por asignación vigente: Piso propio; Subcoordinación propio y Piso; Administración propio, Subcoordinación y Piso; Dirección toda LOR-001, incluso sin asignación. Un responsable histórico no conserva acceso por su antecedente. Par/superior u otra sucursal quedan ocultos; recurso ajeno/inexistente converge a 404 para estas consultas existentes. PENDIENTE/CONCLUIDA no son por sí solas permisos binarios; publicar plan tampoco concede visibilidad.

Metadata existente: requisito, IDs/versionNo/status, actor/fecha/motivo/cadena y datos permitidos de archivo; versiones VIGENTE y SUSTITUIDA con cursor y límite. Adenda 18 §12 prohíbe clave/bucket/URL/contenido en ese listado. El contenido estructurado pertenece a las extensiones posteriores y no se rediseña en esta revisión binaria. Los permisos de aporte/sustitución son de mutación y no se convierten en permisos de lectura binaria automáticamente.

**Diferencia puntual documento/código observada, sin ensayo:** ListAsync en EfEvidenceContributionService.cs líneas 483–522 llama a CanViewAsync; líneas 558–572 retornan false sin asignación vigente y exigen cuenta/rol del responsable antes de RoleHierarchy.CanAccess. No se observa allí la excepción documental para Dirección sin asignación de Adenda 15 §4, a la que remite Adenda 18 §12. Esto es una inconsistencia de lectura identificada estáticamente, no una prueba fallida ejecutada ni una causa de los ciclos aceptados. El código afectado no se modifica; no se extrapola a otras consultas.

**Tratamiento propuesto de esa diferencia:** antes de usar ese helper como autoridad de una futura descarga, reproducir por separado consulta metadata de Dirección sobre una obligación sin asignación con historia sintética persistida, contrastar la remisión contractual y presentar corrección mínima de lectura si se confirma. No sembrar una asignación para ocultar la diferencia. No ampliar permisos ni atribuir sujeto futuro a la auditoría. Esa verificación/corrección no está autorizada por el hito documental; requiere una orden separada. Estimación inicial 2–4 h, Windows AMD64/Docker Linux AMD64/PostgreSQL real, sin UI o suites integrales; evidencia de Dirección/no Dirección y no-efecto. Esta observación no invalida ni reejecuta el cierre integrado, cuyo alcance/casos conservan su evidencia.

## 11. Elementos pendientes de un contrato mínimo de descarga

Esta tabla distingue reglas existentes de decisiones aún ausentes; no asigna códigos, campos, permisos ni garantías nuevas. La ruta se cita de F06, no se propone como identificador inventado.

| Materia | Disponible | Resolución todavía necesaria |
|---|---|---|
| Finalidad | F06 declara obtener URL firmada corta de archivo limpio | Confirmar descarga de binario original, sin transformación, y que preview quede fuera |
| Archivo y versión | fileId ligado inmutablemente al ítem/versión; historial VIGENTE/SUSTITUIDA | Decidir lectura de ambos estados y rechazo de objetos limpios aún no vinculados; no basta conocer UUID o haber creado la intención |
| Autoridad | PER-TAREA-VER/alcance para metadata; F06 pide autorización revaluada | Aprobar expresamente si descarga usa ese permiso y ese mismo universo; resolver diferencia de §10.2 antes de reutilizar código. No heredar autoridad de actor histórico/carga |
| Momento de autorización | F06 exige revaluación; URL expira cinco minutos | Fijar instante de emisión y semántica de revocación durante vigencia. Una URL firmada directa puede seguir utilizándose hasta caducar; no prometer reautorización SGOL por cada GET S3 sin mecanismo adicional aprobado |
| Transporte y exposición | S3 privado, HTTPS fuera de Development/CI, sin CDN/acceso público, cinco minutos máximo según F06 | Definir respuesta cerrada, cache de respuesta/token, firma de GET y encabezados de disposición/nombre/tipo. No copiar DTO de carga ni exponer clave/bucket permanentemente |
| Limpieza/integridad | LIMPIO, tipo real, SHA-256 y promoción preceden al vínculo | Especificar verificación antes de emitir, comportamiento si objeto limpio falta o no coincide y respuesta ante dependencia no disponible; nunca promover/escaneo/reparación desde GET |
| Historia y auditoría | Metadata GET puro; historia inmutable; Adenda 18 §5 prohíbe efectos en sus GET existentes | Decidir pureza del nuevo GET y si se exige registrar emisión/acceso: esos dos hechos son diferentes y S3 directo no confirma que el usuario leyó. No inventar evento auditado ni trasladar VIEWED de continuidad |
| Errores y ocultación | Problem Details/correlationId y estados generales F06; 404 de alcance en consultas existentes | Aprobar códigos exactos de descarga, UUID inválido, no vinculado/no limpio, objeto ausente, dependencia/firma fallida y anti-IDOR. No reutilizar errores de carga cambiando su sentido |
| CSRF/idempotencia/ETag | Controles existentes de mutación permanecen | Definir naturaleza del nuevo GET, cache y relación con versión inmutable. No añadir escritura/idempotencia/If-Match ni trasladar reglas de reemplazo por inferencia |
| Preview | Sin definición operativa localizada | Mantener fuera: nada inline, conversión PDF/imagen, miniaturas, librería o ruta nueva. Abrir una descarga con una aplicación externa no acredita preview SGOL |

**Propuestas para una futura redacción**, todavía no aprobadas: conservar cinco minutos como máximo sin ampliarlo; descargar sólo binario original LIMPIO vinculado a una versión funcional; tomar como punto de partida el alcance de metadata y considerar VIGENTE/SUSTITUIDA para reconstruir historia; emisión mediante GET sin efectos de negocio; no preview ni transformación. El detalle de revocación, auditoría, errores y transporte debe resolverse explícitamente antes de código, y no se presume decidido por estos puntos de partida.

## 12. Resolución global concreta sometida a decisión

**Recomendación:** conservar el compromiso de descarga ya declarado en F06 y preparar su contrato operativo mínimo en un siguiente hito exclusivamente documental; mantener preview diferida. Tiene mejor correspondencia con la fuente general que retirar globalmente ambas funciones por una exclusión de HU-025. Evita convertir la brecha en código sin autoridad, semántica de token o errores definidos.

Texto propuesto para aprobación global:

> Se conserva BR-API04 ABIERTA globalmente. La descarga declarada en F06_CONTRATO_DE_API.md §5.6 sigue pendiente de entrega: las exclusiones de HU-025 y FRONT-018/019/020/TECH-FRONT-005 son consumidoras y no suprimen esa definición. Se autoriza preparar únicamente un contrato mínimo de descarga del binario original, con URL firmada privada de vigencia máxima cinco minutos según F06 y archivo LIMPIO vinculado a versión funcional. El contrato resolverá expresamente autoridad, versiones accesibles, instante de revaluación y revocación durante vigencia, transporte/DTO/encabezados/cache, pureza o auditoría, errores y pruebas. Preview, transformación y UI de lectura permanecen diferidas. La diferencia puntual de alcance Dirección identificada en §10.2 queda registrada para decisión/verificación separada; no se copia ese helper como autoridad de descarga sin resolverla. No se implementa ni se asigna una tarea/ID de código por esta decisión. Una adenda documental sólo se redacta e incorpora tras esta aprobación; el contrato resultante requiere aprobación expresa antes de código. BR-API04 no se declara cerrada hasta una decisión global de cierre con sus criterios y evidencia.

**Alternativa, si no se requiere preparar esa capacidad ahora:** mantener BR-API04 ABIERTA y aplazar descarga/preview globalmente sin retirar el compromiso de F06; registrar motivo y condición de reanudación explícitos. Eso permite priorizar un hito independiente de validación residual, pero no convierte la descarga pendiente en PASS ni autoriza automáticamente carreras, accesibilidad o cloud. Una eliminación definitiva del alcance general exigiría una decisión contractual distinta con remisión exacta a F06; no se recomienda inferirla de D2.

**Decisión posterior:** el responsable aprobó la resolución global mediante «Apruebo la resolucion global». Se incorpora exclusivamente ese texto mediante F07_ADENDA_62_RESOLUCION_GLOBAL_BR_API04.md. Autoriza preparar el contrato; no aprueba de antemano las opciones detalladas de §11 ni autoriza la verificación/corrección separada de §10.2. BR-API04 conserva ABIERTA.

## 13. Alcance, costo, archivos y aceptación de la opción recomendada

Si se aprueba la resolución de §12: siguiente trabajo sólo contractual. Dependencias: esta decisión global, fuentes mínimas ya localizadas y respuesta expresa a los puntos de §11. Costo estimado: 3–6 h de redacción y revisión más decisiones humanas, sin implementación, servicios nuevos o ensayos. La diferencia de lectura tiene costo aparte de §10.2 y no se suma como trabajo autorizado. El costo de código no se estima hasta cerrar el contrato.

Archivos previstos: este mismo Markdown, una adenda F07 documental nueva en raíz cuyo número libre se compruebe justo antes de crear, y referencias mínimas en docs/INDICE_IDS.md e IMPLEMENTATION_STATUS.md para distinguir autorización/incorporación documental de implementación. No se modifica F06 congelado, Adenda 18, consumidores aprobados, Fuentes, código, tests, UI, CI, dependencias ni global.json. No se asigna ahora número de adenda o tarea. No se prepara una implementación ni un plan paralelo antes de aprobar esa etapa.

Aceptación del contrato posterior: las materias de §11 tienen una regla/campo/código exactamente aprobado o una exclusión explícita; no existen garantías falsas de revocación inmediata o lectura auditada; se define matriz positiva/negativa de rol/recurso/jerarquía/estado/versiones, no limpio/no vinculado, expiración/revocación y S3 privado real, con no-efecto y cero filtración; toda necesidad de UI se pospone a un plan consumidor con diseño aprobado. Contrato redactado no significa descarga Implementada localmente ni BR-API04 cerrada.

Validación de esa redacción: revisión de fuente, contradicciones, IDs, reglas, exclusiones y git diff --check. Sin repetir los ciclos aceptados ni preparar certificados/fixtures. Aislamiento y cleanup: misma rama codex/ desde merge aceptado; si se ocupa, worktree separado sin alterar cambios ajenos. Sin recursos de ejecución que limpiar; reportes finales anteriores permanecen intactos. Cualquier ensayo autorizado después usa sólo infraestructura/datos sintéticos propios, certificados Manual y evidencia sanitizada; nunca publica tokens/URLs firmadas, contenido, conexiones o credenciales.

## 14. Trazabilidad y entrega de esta ejecución documental

Registro histórico de la entrega anterior a aprobar la resolución global; su espera queda superada por §12 y la incorporación de §16. Conserva la evidencia de esa entrega, sin bloquear la redacción ahora autorizada.

Fuentes adicionales mínimas consultadas: F05_CRITERIOS_DE_ACEPTACION.md líneas 53–59; F05_ESPECIFICACION_FUNCIONAL_MVP.md líneas 96–97 y 138–141, localizadas por INDICE_IDS; F06_REGISTRO_ADR.md ADR-007 líneas 105–113; F06_CONTRATO_DE_API.md §4 estados/códigos y §5.6 líneas 189–201; Adenda 15 §§3–6/12; Adenda 17 §§5.2–5.3; Adenda 18 §§3.2/5/6.2/12 y revisiones CORS §§24–25; FRONT_018_PLAN_DE_IMPLEMENTACION.md §3 línea 32. ADR-009 se consultó en su rango localizado únicamente como límite de idempotencia, sin derivar escritura del GET. Adendas 57..61 y el cierre previo conservan su aceptación.

Comparación estática acotada: src/Sgol.Web/Interface/Endpoints/EvidenceApiEndpoints.cs; src/Modules/Evidence/Contracts/EvidenceInfrastructureContracts.cs e EvidenceContributions.cs; src/Sgol.Web/Infrastructure/Evidence/S3PrivateObjectStorage.cs; src/Sgol.Web/Infrastructure/Persistence/Evidence/EfEvidenceContributionService.cs, únicamente listado/autoridad. La búsqueda de registros de rutas se limitó a Interface/Endpoints; localización de nombres de archivos de evidencia excluyó bin/obj. No se abrieron archivos binarios, ZIP, Excel, originales de marca ni contenido de evidencia. Dos búsquedas con comodín como ruta literal de Windows fueron rechazadas por sintaxis de ruta; se corrigieron usando globs de rg sobre directorios explícitos. No afectaron archivos ni prueban ausencia de contenido.

Resultado: hito documental **ejecutado localmente**, con único documento actualizado y resolución concreta pendiente de aprobación global. No hay implementación de producto que marcar Implementada localmente. IMPLEMENTATION_STATUS e índice no se actualizan aún: la propuesta aprobada condiciona esas incorporaciones a la aprobación de la decisión contractual; la autorización y ejecución se trazan en este archivo. Sin adenda creada anticipadamente.

Comprobaciones ejecutadas para esta entrega: revisión documental de referencias/contradicciones; git diff --cached --check exit 0 sobre el único Markdown añadido; diff --cached --stat/status limitados a ese archivo; ascendencia del merge aceptado en HEAD exit 0. Preflight exit 0 con incompatibilidad SDK registrada, sin diagnóstico adicional. El commit local que contiene este entregable conserva el trabajo documental autorizado, sin publicación ni commit administrativo sólo para hashes. No build, restore, formato, suites, tests de código o checks remotos: el cambio es sólo documental. Todos los diferidos de §5 permanecen con causa original; no se ensayaron ni recibieron PASS. No se creó infraestructura/temporales sensibles, por lo que cleanup de esta revisión no requiere eliminación. Se espera decisión sobre §12 antes de cualquier incorporación contractual o nuevo trabajo.

## 15. Contrato mínimo de descarga — APROBADO E INCORPORADO LOCALMENTE

### 15.1 Alcance y relación con las fuentes

**Aprobado globalmente por Adenda 62:** conservar la descarga pendiente de F06, binario original, archivo LIMPIO vinculado a versión funcional, URL firmada privada con máximo cinco minutos; redactar el contrato sin preview, transformación ni UI de lectura. **Aprobado íntegramente después mediante «La apruebo integramente» e incorporado por Adenda 63:** §§15.1–15.8, incluyendo autoridad, versiones, revocación residual, DTO, nombres, pureza y códigos nuevos. Se conserva la redacción propositiva revisada como antecedente: sus reglas quedan aprobadas en este alcance; estimaciones y pruebas futuras no son resultados ni autorización de ejecución. La resolución global sola no las aprobaba; esta aprobación específica sí.

Se propone exclusivamente `GET /api/v1/files/{id}/download`, ruta ya declarada en F06 §5.6. Devuelve autorización temporal para un objeto exacto; no transmite el binario desde Web, no redirige automáticamente y no crea un endpoint de proxy. No crea otra ruta o permiso, no descarga evidencia estructurada, no permite elegir bucket/clave/nombre/TTL desde el cliente y no habilita integraciones externas.

### 15.2 Solicitud y alcance autorizado

Solicitud sin cuerpo ni parámetros de consulta. `id` es fileId en formato UUID D, no vacío; los recursos públicos generados conservan UUID v7. Se rechazan query, cuerpo y cabeceras funcionales `Idempotency-Key`/`If-Match`, sin cambiar su obligatoriedad en mutaciones existentes. Las cabeceras normales de autenticación/HTTP/correlación no son parámetros de negocio. No requiere CSRF porque se propone una lectura sin efectos; no se reutiliza esa excepción en ningún POST/PUT. Se conserva autenticación de mismo origen con cookie segura, sesión/MFA completos y denegación por defecto.

Permiso propuesto: **PER-TAREA-VER existente**, revaluado en servidor junto con cuenta, empleo y rol vigentes en LOR-001; tener PER-EVIDENCIA-APORTAR/SUSTITUIR o ser creador de la carga no concede lectura. Se propone exactamente el universo de Adenda 15 §§3–4/Adenda 18 §12:

| Rol vigente | Descarga propuesta de obligación visible |
|---|---|
| PISO_VENTAS | Asignación VIGENTE a su propia persona |
| SUBCOORDINACION | Propia o responsable vigente de nivel PISO_VENTAS |
| ADMINISTRACION | Propia o responsable vigente de nivel SUBCOORDINACION/PISO_VENTAS |
| DIRECCION | LOR-001 completa, incluida obligación sin asignación; no depende de vigencia del responsable para consultar el hecho |

No Dirección exige asignación VIGENTE y cuenta/empleo/rol canónico vigentes del responsable; par/superior, otra sucursal y relación histórica no autorizan. Dirección conserva cuenta/empleo/rol propios vigentes y permiso vigente. No se inventa un permiso de «descargar evidencia» ni se toma un puesto textual como autoridad.

El contrato propone acceso al archivo de **VIGENTE y SUSTITUIDA**, por la misma autoridad actual sobre la obligación. Sustituir una versión no borra su archivo ni concede al aportante histórico acceso futuro. PENDIENTE y CONCLUIDA son estados admisibles de la obligación visible; validación vigente/sustituida no altera esta lectura. No exige publicación en plan ni cambia ejecución/validación. TAR-0026 continúa limitada a configuración/políticas y negativos; este contrato no habilita fuente/cadena CAT-006.

### 15.3 Cadena, limpieza e instante de emisión

El lector debe resolver en servidor la cadena persistida exacta `file_object → evidence_version → evidence_item → work_obligation`, con vínculo recíproco `linked_evidence_item_id`, misma LOR-001 y política/requisito capturados coherentes. No usa correlación, nombre, SHA coincidente, JSON ni proximidad temporal para inferir el vínculo. Un objeto limpio aún no vinculado funcionalmente no es descargable. Una evidencia estructurada sin binario no tiene autorización de descarga.

Se propone aplicar alcance en la lectura antes de materializar metadata o consultar S3; identidad/autoridad inválidas no disparan metadata S3 ni firma. Recurso inexistente, ajeno, otra sucursal o no vinculado queda oculto. Ambigüedad o contradicción de una cadena dentro del alcance reconocido falla cerrada, sin escoger un eslabón, sintetizarlo ni reparar. El servicio no expone entidades/DbContext al endpoint ni escribe tablas de otro módulo.

Sólo estado LIMPIO, área CLEAN, tipo real admitido JPEG/PNG/PDF y objeto exacto promovido. Antes de firmar se cotejan existencia y metadata técnica de S3 con tamaño/tipo/SHA-256 persistidos mediante los mecanismos internos existentes; eso no equivale a un nuevo escaneo o hashing integral por GET. Archivo no limpio no recibe token; objeto ausente/inconsistente falla cerrado. No copiar, promover, completar, escanear, cambiar estado o reparar desde esta consulta. No aplicar limpieza de objetos técnicos a evidencia vinculada.

Instante propuesto de autoridad: tras la comprobación técnica, abrir una lectura final PostgreSQL READ ONLY/REPEATABLE READ, capturar una vez `authorizedAt` UTC y revaluar cuenta/empleo/rol/permiso, universo, cadena y estado contra esa instantánea final. Comprobar que el archivo/versión exactos coinciden con los cotejados en S3. La firma local se emite sólo tras esas guardas; se completa la lectura sin escritura y la respuesta se devuelve con el token. Si cambió el recurso/autoridad antes de esa instantánea, no se devuelve una autorización basada en la comprobación previa. Fallo posterior de lectura/firma no devuelve URL parcial. No se mantiene la transacción abierta durante una transferencia binaria.

La autoridad corresponde a esa instantánea de emisión, no al instante futuro de cada GET S3. Una modificación concurrente posterior a authorizedAt queda comprendida en el límite explícito de §15.4; no se promete atomicidad entre PostgreSQL y el consumo externo de la URL. La inconsistencia estática de Dirección en §10.2 es una dependencia real que debe verificarse y resolverse antes de reutilizar el helper en una implementación. Aprobar este contrato no autoriza por sí mismo ese ensayo o arreglo separado.

### 15.4 Vigencia y revocación durante vigencia

Duración propuesta: cinco minutos por defecto, endurecible con configuración validada de duración mayor que cero y no superior a cinco minutos. Sin TTL elegido por cliente, renovación automática, ampliación por retry o URL permanente. `expiresAt = authorizedAt + duración efectiva`, en UTC; si al devolver la respuesta ya caducó, falla sin devolver token. Repetir la petición exige nueva sesión/autoridad y firma; no hay recuperación de URL por idempotencia.

**Aceptación de límite propuesta, requiere aprobación expresa al aprobar §15:** la URL es una credencial temporal al portador. Una baja de cuenta/empleo/rol, corrección de asignación, logout, pérdida de permiso o sustitución posterior impide emitir nuevas URLs según autoridad actual; **no garantiza invalidar inmediatamente una URL ya emitida**. Puede seguir iniciando accesos al objeto hasta su expiración efectiva. No se promete detener una transferencia iniciada antes de caducar ni retirar una copia descargada. No se destruye historia, objeto o credencial compartida para simular revocación individual.

El endpoint no concede autoridad permanente sobre S3. Firma sólo método GET y objeto CLEAN exacto, sin listado/bucket administration/escritura ni cuarentena. Si el responsable exige revalidación SGOL en cada transferencia, revocación inmediata o token ligado al usuario en S3, detener esta parte y decidir otro contrato/arquitectura; no introducir proxy, gateway, almacén de tokens o cuentas por inferencia. El límite no acredita aislamiento productivo SeaweedFS, que sigue diferido.

### 15.5 Respuesta cerrada y transporte

Éxito propuesto: `200 application/json`, envelope singular, sin 302 y sin binario en el cuerpo:

| Campo exacto | Tipo / regla propuesta |
|---|---|
| data.fileId | UUID D minúsculo del archivo autorizado |
| data.download.url | String URI absoluta firmada GET; nunca persistida ni registrada |
| data.download.expiresAt | Instante RFC 3339 UTC con Z, idéntico a la vigencia de firma |
| meta.correlationId | Correlación de respuesta conforme a F06 |

No añade nombre original, hash, contenido, proveedor, bucket o clave como campos separados; no modifica el DTO de listado/status/upload. La URI técnica S3 puede contener host/ruta/clave y firma: se entrega únicamente como credencial efímera a un actor autorizado, no se promete ocultar esos componentes dentro de una URL firmada directa. Si ocultarlos al receptor es requisito, exige otra decisión de transporte antes de implementar. Nunca usar una URL real o ejemplo de firma en documentación, Git, logs, capturas o mensajes.

Cabeceras propuestas para éxito y Problem Details: `Cache-Control: private, no-store`, `Pragma: no-cache`, `Referrer-Policy: no-referrer` y `X-Content-Type-Options: nosniff`. No ETag/304 de esta autorización, no almacenamiento server de URL, y petición con condición HTTP de cache no evita reautorizar ni produce 304. No modifica caché de otros endpoints.

El GET S3 autorizado debe devolver `Content-Type` derivado exclusivamente del tipo real verificado, `Content-Disposition: attachment` y `Cache-Control: private, no-store`. Disposición/cache se fijan en la autorización de respuesta del proveedor y quedan cubiertas por la firma. Nombre propuesto seguro: `evidence-{fileId:D}.jpg`, `.png` o `.pdf`, extensión por tipo real, sin nombre original, rutas, PII o texto de usuario. No inline, thumbnail ni transformación. Los bytes mantienen su SHA-256 original. No se requiere lectura cross-origin mediante fetch ni cambiar CORS del bucket CLEAN: no hay UI en este contrato.

TLS, buckets privados y condiciones locales de HTTP siguen Adenda 17 §5.2 y Adenda 18 §25: HTTP sólo Development/CI con opción explícita y endpoint permitido; HTTPS en el resto. No se autoriza aprovisionamiento productivo, reparación CORS, URL pública, CDN, nueva biblioteca o dependencia. La configuración/signer debe conservar cabeceras y expiración bajo el proveedor real elegido; si éste no puede cumplirlas, falla cerrado y exige decisión, no elimina la garantía para conseguir PASS.

### 15.6 Pureza, auditoría y errores exactos propuestos

GET puro: no escritura de archivo/versión/estado, auditoría, idempotencia, outbox, checkpoint, scheduled_job_run, historial, rol/asignación, semana o plan; tampoco mutaciones S3. No genera un evento de «descargado» ni «leído»: emitir token no demuestra transferencia ni lectura. No traslada RECOVERY_RECONCILIATION_VIEWED de continuidad. Diagnóstico existente sólo correlationId/código cerrado/operación, sin URL, firma, contenido, nombre, hash, conexiones o credenciales; no crea observabilidad productiva nueva.

Todos los errores usan `application/problem+json`, `code` y `correlationId`, sin datos parciales ni URL. Los códigos marcados NUEVO son propuestas para esta operación; no existen como decisión aprobada por la resolución global.

| HTTP / code | Condición propuesta / origen |
|---|---|
| 400 SOLICITUD_DESCARGA_INVALIDA — NUEVO | UUID inválido/vacío, query/cuerpo o Idempotency-Key/If-Match funcionales inadmisibles. Detalle genérico |
| 401 AUTENTICACION_REQUERIDA | Sesión ausente/expirada o MFA incompleto; código existente |
| 403 ACCESO_DENEGADO | Actor sin cuenta/empleo/rol/permiso vigente antes de resolver recurso; existente |
| 404 ARCHIVO_NO_ENCONTRADO | UUID no existente, otra sucursal, fuera de alcance o sin vínculo funcional; código existente, semántica de ocultación extendida propuesta |
| 422 ARCHIVO_NO_LIMPIO | Cadena autorizada pero archivo no LIMPIO/CLEAN; código existente cuyo uso en descarga se propone ampliar |
| 500 CADENA_EVIDENCIA_INCONSISTENTE — NUEVO | Vínculo ambiguo/no recíproco o política/ítem/versión contradictorios tras reconocer alcance; genérico, sin selección parcial |
| 503 INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE | Storage/configuración/signer no disponibles, objeto limpio ausente o metadata incongruente, firma/cabeceras/expiración no garantizadas. Código existente, uso ampliado propuesto; sin autorreparación |
| 500 ERROR_FUNCIONAL_REGISTRADO | Fallo inesperado conforme al código general F06; sin excepción nativa/detalles/URL |

Precedencia propuesta: autenticación/actor → sintaxis cerrada → lookup con alcance → cadena/vínculo → LIMPIO/CLEAN → storage/metadata → reautorización final → firma/respuesta. Una solicitud ajena no recibe estado limpio, error de proveedor o diferencia de vínculo. No devolver 409/412/428 como semántica de reemplazo para este GET. La expiración/denegación de un GET directo al proveedor es respuesta del proveedor, no Problem Details SGOL ni autorización para reintento automático; el usuario de una futura UI tendría que solicitar una nueva autorización y ser revaluado según contrato consumidor todavía no aprobado.

### 15.7 Matriz de aceptación futura por capa

Casos siguientes son criterios propuestos, no pruebas ejecutadas ni IDs nuevos de CP/backlog. Reutilizar suites existentes cuando cubran el criterio; no renombrar CP-025 ni atribuirle una prueba que no ejecuta. Preparar fixtures sintéticos válidos/UUID v7 y sincronización por barrera/condición real, sin sleeps/retries ni reducción de expectativas.

| Criterio | Evidencia futura exigible |
|---|---|
| Ruta, método, DTO/cabeceras y errores cerrados | API/contrato: solicitud válida y query/cuerpo/UUID/cabeceras inválidos; no 302/304 ni campos extra/URL parcial; sin ruta preview |
| Cuatro roles y alcance actual | PostgreSQL/API: propia/inferior permitido, par/superior/otra sucursal/rol perdido ocultos, actor inválido 403, sesión/MFA 401; diferencia Dirección sin asignación resuelta/verificada separadamente antes de depender del helper |
| Historia binaria | PostgreSQL/API más S3 real: VIGENTE y SUSTITUIDA vinculadas exactas; aportante/responsable histórico sin autoridad actual denegado; PENDIENTE/CONCLUIDA sin cambiar decisión vigente/sustituida |
| Limpieza y cadena | API/PostgreSQL: no limpio, limpio no vinculado, vínculo ambiguo/no recíproco y estado/área incorrectos; cero firma/lectura S3 para recursos ajenos; S3 real: archivo/metadata ausentes o discordantes sin URL |
| Reautorización al emitir | Barreras PostgreSQL/API: revocar autoridad antes de instantánea final deniega; una emisión válida lleva el authorizedAt/TTL esperado en oráculo interno sanitizado; cambios posteriores no se presentan como revocación instantánea |
| Token limitado/bytes/headers | S3 compatible real: GET del objeto exacto, bytes/SHA-256 originales, MIME verificado, attachment/nombre seguro/no-store; firma alterada, objeto distinto, PUT y acceso anónimo/listado denegados; sin datos o token en diagnóstico |
| Expiración y límite residual | Firma de corta duración explícitamente permitida en fixture más reloj/condición real del proveedor; antes de caducar acceso válido, después nuevas solicitudes denegadas. Revalidar nueva emisión tras baja/rol/asignación/logout; documentar por separado supervivencia temporal de token previo. No simular expiración sólo con reloj de aplicación |
| No-efecto/historia/minimización | PostgreSQL: conteos/huellas antes/después de éxito/denegación/fallo, sin SaveChanges/audit/idempotency/outbox/job/row_version. S3: sólo metadata/firma/lectura esperadas, cero promoción/borrado/CORS. Prueba de arquitectura impide exponer DbContext/SDK a dominio o endpoint |
| Concurrencia | PostgreSQL/API: reemplazo genera sucesora conservando archivo anterior; descargas a versión exacta sin promover/forzar/reescribir decisión. No acredita la carrera específica evidencia contra emisión de validación, que conserva su diferido |

No se exige nuevo ciclo integral ni Playwright para este contrato API/S3 sin UI. Una implementación futura necesita plan/orden/ID aprobados y build/tests enfocados secuenciales con SDK fijo; su publicación requiere autorización aparte. Estimación inicial de validación enfocada API/PostgreSQL/S3: 1–2 h de ejecución/provisión tras tener código/pruebas, más implementación todavía no estimada; no es evidencia ejecutada ni presupuesto autorizado. S3/ClamAV existentes se reutilizarían sólo donde corresponda, sin inventar un antivirus distinto o consumir evidencia real.

### 15.8 Dependencias, exclusiones y aceptación documental

Dependencias antes de código: aprobación íntegra de §15, incorporación de sus reglas mediante adenda contractual expresa, tarea/ID y plan de implementación aprobados; verificación/resolución separada de §10.2 antes de reutilizar autoridad. No se crea tarea por inferencia. Una aprobación del contrato autoriza incorporar la decisión documental; no iniciar código, ensayos ni publicación salvo orden expresa adicional.

Mantener BR-API04 ABIERTA hasta cierre global con evidencia. Aprobar un contrato sólo de descarga no cierra preview ni BR-API04 por implicación. Preview permanece diferida; su eventual cierre global exige decidir su exclusión o contrato y evidencia. Todas las exclusiones/diferidos de §§3/5/6 permanecen, incluidos D4/CAT-006, D5, SeaweedFS productivo y WebKit Windows histórico sin HTTP. No agregar productores, reparación, indicadores, permisos, rutas auxiliares, UI, diseño, bibliotecas, cloud o cuentas. No modificar fórmulas, historia, validación ni navegación 8/6/6/5.

Aceptación documental propuesta: aprobar íntegramente §§15.1–15.8, incluidos acceso a ambas versiones, PER-TAREA-VER/universo, instantánea final, límite de revocación temporal y transferencias/copias, exposición técnica de URI firmada al receptor, pureza sin evento de descarga, DTO/headers/nombre seguro y nuevos códigos exactos; o señalar cambios concretos en este mismo documento. Si exige revocación inmediata, ocultamiento de URI al receptor o prueba de lectura auditada, revisar sólo esa parte con decisión explícita antes de incorporar contrato. No ocultar esas limitaciones mediante otro navegador, proxy inferido o gate ajeno.

## 16. Incorporación global y entrega del contrato propuesto

2026-10-03. «Apruebo la resolucion global» aprueba §12; resolución incorporada localmente en Adenda 62. Se preparó §15 como contrato mínimo completo para aprobación, con decisiones explícitas para cada materia de §11, sin código ni ensayos. Las fuentes ya leídas se conservan; lecturas adicionales mínimas: F06_CONTRATO_DE_API.md §§1–2/5.6; Adenda 18 §17 y Adenda 15 §15 para errores y no-efecto. Nº62 libre comprobado en raíz; árbol inicial limpio, HEAD bb46a238eb4faa550ee27150c0615f835604c423, base integrada conservada. Preflight exit 0 con incompatibilidad SDK de PATH registrada, sin diagnóstico adicional.

Archivos de esta entrega: este Markdown (aprobaciones/contrato propuesto), F07_ADENDA_62_RESOLUCION_GLOBAL_BR_API04.md (únicamente resolución aprobada), docs/INDICE_IDS.md (rango de resolución y rango de borrador separados) y docs/traceability/IMPLEMENTATION_STATUS.md (estado documental y punto de parada). No se modifican F00–F07 congelados, adendas históricas, Fuentes, código, pruebas, CI, dependencias o SDK. No se presenta la nueva documentación como Publicada/Integrada.

Validación documental proporcional: coherencia fuente/estado, campos cerrados, IDs, referencias y alcance del diff revisados; git diff --check y staged exit 0, rangos del índice cotejados con encabezados reales, base integrada conservada. El commit local que contiene esta entrega conserva sólo los cuatro documentos anteriores, sin publicación. No build/restore/formato/tests/gates remotos porque sólo cambia documentación. Diferidos intactos con sus causas. Aislamiento: misma rama codex/; no cambios ajenos que separar. Cleanup: no se crean servicios/certificados/datos/temporales sensibles, no se limpia infraestructura histórica. Reportes/capturas anteriores se conservan sin abrir secretos. Certificados futuros Manual y sólo recursos propios verificados conforme a §7. Esperar aprobación íntegra del contrato §15 antes de incorporarlo como decisión contractual; no ejecutar otro hito automáticamente.

Nombre del siguiente chat, sólo si el responsable lo abre: **SGOL — Aprobación del contrato mínimo de descarga BR-API04**.

Mensaje listo para copiar:

> Aplica AGENTS.md e INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md. Lee docs/traceability/PROPUESTA_POST_TECH_FRONT_005.md §15 y F07_ADENDA_62_RESOLUCION_GLOBAL_BR_API04.md. La resolución global está aprobada e incorporada localmente; el contrato detallado §15 sigue propuesto. Espera mi aprobación íntegra antes de incorporarlo como contrato. Conserva BR-API04 ABIERTA, preview/UI diferidas, límite de revocación de URL firmada, diferencia Dirección pendiente de verificación separada y todos los límites D4/D5. No asignes un ID, implementes, ensayes, publiques, despliegues ni inicies automáticamente otro hito.

## 17. Aprobación íntegra e incorporación del contrato mínimo

2026-10-03. El responsable respondió «La apruebo integramente» a la solicitud de aprobación de §15. Se incorporan exclusivamente §§15.1–15.8 mediante F07_ADENDA_63_CONTRATO_MINIMO_DE_DESCARGA_BR_API04.md, sin editar reglas materiales ni añadir comportamiento. La aprobación satisface la dependencia documental del contrato, no la definición de tarea/ID/plan ni la autorización de implementación o verificación separada. Adenda 62 y los registros de espera de §§11/13/16 conservan su carácter histórico, superado por esta aprobación; no son bloqueos actuales.

Archivos cambiados en esta incorporación: Adenda 63 nueva; este documento con estado/aprobación y entrega; docs/INDICE_IDS.md con referencias de contrato aprobado; docs/traceability/IMPLEMENTATION_STATUS.md con dependencia documental satisfecha y punto de parada. Sin modificaciones de Fuentes/documentos congelados, Adenda 62 u otras adendas históricas, src/tests/scripts/CI, dependencias o SDK. BR-API04 conserva ABIERTA, preview/UI diferidas y todos los límites D4/D5/diferidos. No se declara producto Implementado localmente, documentación Publicada/Integrada ni cierre global.

Base: árbol inicial limpio, rama codex/propuesta-post-tech-front-005, HEAD histórico 710dff1ee34f895b552d8854e363ed0cd324b1f9; merge aceptado e6355332ea09542c2b38895945c61a1efb83d586 ancestro verificado, exit 0. Nº63 libre comprobado antes de crear. Preflight única comprobación inicial, exit 0, Fuentes limpia/sin rebaseline; incompatibilidad SDK PATH registrada, sin diagnóstico adicional. Validación documental proporcional ejecutada: comparación material de §15 contra la versión aprobada PASS; estado/IDs/referencias/rangos revisados PASS; git diff --cached --check exit 0 y alcance limitado a los cuatro documentos antes del commit local que contiene esta incorporación. Sin build/restore/formato/pruebas de código/gates remotos por tratarse sólo de incorporación documental. Ningún diferido ensayado ni PASS nuevo.

Aislamiento/cleanup: misma rama codex/, sin cambios ajenos que separar ni recursos de ejecución creados; no servicios/volúmenes/certificados/temporales a retirar. Artefactos/capturas aceptados conservados. No se verifica ni limpia el temporal/PFX histórico bloqueado. Punto de parada actual: esperar una orden expresa de definir/autorizar la tarea de implementación y la verificación separada de Dirección; no iniciar automáticamente esa tarea ni asignar ID. No se solicita nuevamente la aprobación contractual ya recibida.

Nombre exacto sugerido del siguiente chat, sólo si el responsable lo abre: **SGOL — Definición de tarea para descarga BR-API04**.

Mensaje listo para copiar y pegar:

> Aplica AGENTS.md e INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md. Lee primero IMPLEMENTATION_STATUS y usa INDICE_IDS para localizar Adendas 62/63 y docs/traceability/PROPUESTA_POST_TECH_FRONT_005.md §§10.2/15/17. La resolución global y el contrato mínimo de descarga están aprobados e incorporados localmente; no vuelvas a pedir su aprobación ni repitas gates TECH-FRONT-005. Prepara únicamente la definición y plan mínimo de la tarea de descarga y la propuesta de verificación separada de Dirección, conservando BR-API04 ABIERTA y todos los límites del contrato. No asignes un ID como aprobado: presenta esa decisión al responsable. Espera mi aprobación del plan y orden expresa antes de implementar o ensayar. Sin publicación, merge, despliegue, preview/UI, productores TAR-0026/CAT-006 ni otro hito automático.
