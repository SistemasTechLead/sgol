# SGOL — Propuesta de siguiente hito después de TECH-FRONT-005

Fecha: 2026-10-03, America/Mexico_City. Estado: **HITO DOCUMENTAL APROBADO Y EJECUTADO LOCALMENTE; RESOLUCIÓN GLOBAL PROPUESTA PENDIENTE DE DECISIÓN**. Aprobación literal del responsable: «Apruebo integramente la propuesta». Autoriza la revisión mínima y redacción de §4; no aprueba de antemano la resolución global de BR-API04 ni su incorporación contractual. No es una tarea de código aprobada. No se asigna un identificador nuevo. §§1–9 conservan la propuesta aprobada y su contexto; §§10–14 registran la ejecución y la resolución para decisión.

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

La aprobación del plan original no aprueba este texto ni las propuestas de §11. Se requiere la decisión del responsable sobre esta resolución y, separadamente, si desea autorizar la verificación mínima de la diferencia de lectura. Hasta entonces se detiene esa parte y se conserva la brecha abierta.

## 13. Alcance, costo, archivos y aceptación de la opción recomendada

Si se aprueba la resolución de §12: siguiente trabajo sólo contractual. Dependencias: esta decisión global, fuentes mínimas ya localizadas y respuesta expresa a los puntos de §11. Costo estimado: 3–6 h de redacción y revisión más decisiones humanas, sin implementación, servicios nuevos o ensayos. La diferencia de lectura tiene costo aparte de §10.2 y no se suma como trabajo autorizado. El costo de código no se estima hasta cerrar el contrato.

Archivos previstos: este mismo Markdown, una adenda F07 documental nueva en raíz cuyo número libre se compruebe justo antes de crear, y referencias mínimas en docs/INDICE_IDS.md e IMPLEMENTATION_STATUS.md para distinguir autorización/incorporación documental de implementación. No se modifica F06 congelado, Adenda 18, consumidores aprobados, Fuentes, código, tests, UI, CI, dependencias ni global.json. No se asigna ahora número de adenda o tarea. No se prepara una implementación ni un plan paralelo antes de aprobar esa etapa.

Aceptación del contrato posterior: las materias de §11 tienen una regla/campo/código exactamente aprobado o una exclusión explícita; no existen garantías falsas de revocación inmediata o lectura auditada; se define matriz positiva/negativa de rol/recurso/jerarquía/estado/versiones, no limpio/no vinculado, expiración/revocación y S3 privado real, con no-efecto y cero filtración; toda necesidad de UI se pospone a un plan consumidor con diseño aprobado. Contrato redactado no significa descarga Implementada localmente ni BR-API04 cerrada.

Validación de esa redacción: revisión de fuente, contradicciones, IDs, reglas, exclusiones y git diff --check. Sin repetir los ciclos aceptados ni preparar certificados/fixtures. Aislamiento y cleanup: misma rama codex/ desde merge aceptado; si se ocupa, worktree separado sin alterar cambios ajenos. Sin recursos de ejecución que limpiar; reportes finales anteriores permanecen intactos. Cualquier ensayo autorizado después usa sólo infraestructura/datos sintéticos propios, certificados Manual y evidencia sanitizada; nunca publica tokens/URLs firmadas, contenido, conexiones o credenciales.

## 14. Trazabilidad y entrega de esta ejecución documental

Fuentes adicionales mínimas consultadas: F05_CRITERIOS_DE_ACEPTACION.md líneas 53–59; F05_ESPECIFICACION_FUNCIONAL_MVP.md líneas 96–97 y 138–141, localizadas por INDICE_IDS; F06_REGISTRO_ADR.md ADR-007 líneas 105–113; F06_CONTRATO_DE_API.md §4 estados/códigos y §5.6 líneas 189–201; Adenda 15 §§3–6/12; Adenda 17 §§5.2–5.3; Adenda 18 §§3.2/5/6.2/12 y revisiones CORS §§24–25; FRONT_018_PLAN_DE_IMPLEMENTACION.md §3 línea 32. ADR-009 se consultó en su rango localizado únicamente como límite de idempotencia, sin derivar escritura del GET. Adendas 57..61 y el cierre previo conservan su aceptación.

Comparación estática acotada: src/Sgol.Web/Interface/Endpoints/EvidenceApiEndpoints.cs; src/Modules/Evidence/Contracts/EvidenceInfrastructureContracts.cs e EvidenceContributions.cs; src/Sgol.Web/Infrastructure/Evidence/S3PrivateObjectStorage.cs; src/Sgol.Web/Infrastructure/Persistence/Evidence/EfEvidenceContributionService.cs, únicamente listado/autoridad. La búsqueda de registros de rutas se limitó a Interface/Endpoints; localización de nombres de archivos de evidencia excluyó bin/obj. No se abrieron archivos binarios, ZIP, Excel, originales de marca ni contenido de evidencia. Dos búsquedas con comodín como ruta literal de Windows fueron rechazadas por sintaxis de ruta; se corrigieron usando globs de rg sobre directorios explícitos. No afectaron archivos ni prueban ausencia de contenido.

Resultado: hito documental **ejecutado localmente**, con único documento actualizado y resolución concreta pendiente de aprobación global. No hay implementación de producto que marcar Implementada localmente. IMPLEMENTATION_STATUS e índice no se actualizan aún: la propuesta aprobada condiciona esas incorporaciones a la aprobación de la decisión contractual; la autorización y ejecución se trazan en este archivo. Sin adenda creada anticipadamente.

Comprobaciones ejecutadas para esta entrega: revisión documental de referencias/contradicciones; git diff --cached --check exit 0 sobre el único Markdown añadido; diff --cached --stat/status limitados a ese archivo; ascendencia del merge aceptado en HEAD exit 0. Preflight exit 0 con incompatibilidad SDK registrada, sin diagnóstico adicional. El commit local que contiene este entregable conserva el trabajo documental autorizado, sin publicación ni commit administrativo sólo para hashes. No build, restore, formato, suites, tests de código o checks remotos: el cambio es sólo documental. Todos los diferidos de §5 permanecen con causa original; no se ensayaron ni recibieron PASS. No se creó infraestructura/temporales sensibles, por lo que cleanup de esta revisión no requiere eliminación. Se espera decisión sobre §12 antes de cualquier incorporación contractual o nuevo trabajo.
