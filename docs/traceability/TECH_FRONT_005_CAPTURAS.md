# TECH-FRONT-005 — Capturas y guía de recorridos

**Entrega visual final; revisión humana aprobada.** Capturas de la aplicación real con datos sintéticos, correspondientes a los dos ciclos completos PASS del alcance aprobado (770/770 pruebas, 36 celdas, cleanup correcto). La cabeza, navegador, plataforma y resultados constan en report.json y summary.json; los manifiestos enumeran los archivos por recorrido y rol. Los pases parciales históricos conservan su resultado y no sustituyen esta evidencia.

Las capturas ocultan activación, contraseña, configuración MFA/códigos y contenido de evidencia. No incluyen archivo binario, URLs firmadas, cookies, tokens ni conexiones. Mantienen etiquetas, acciones, requisito, versión y estado para revisar la aplicación. Poppins local OFL, Georgia del sistema y estilos integrados conservados; no hay cambios incidentales de interfaz para acomodar la prueba.

## Qué muestra cada recorrido

| Recorrido / capturas | Roles y uso de la pantalla | Estado que se revisa |
|---|---|---|
| R1 — acceso y menú | Los cuatro puestos entran con su cuenta, cambian la contraseña y confirman el segundo factor. El menú muestra sólo sus rutas. En móvil se abre y se cierra con Escape. | Sesión iniciada y foco de vuelta en el botón del menú. No se captura la pantalla de secretos. |
| R2 — personas y accesos | Dirección registra una persona y cuenta auxiliar, modifica empleo/disponibilidad, cambia y revoca roles, desactiva/reactiva y reinicia MFA. | Confirmaciones y versiones anteriores conservadas. La cuenta/rol inactivo no da acceso. |
| R3 — calendario y releases | Dirección guarda fechas en un borrador y lo publica. Los cuatro puestos consultan la semana vigente. | Borrador vacío, fechas laborables/festivo/cierre extraordinario y publicación efectiva. |
| R4 — tareas y políticas | Dirección prepara y publica las ocho TAR y sus políticas de activación, elegibilidad, evidencia y validación. | Definiciones y políticas vigentes. TAR-0026 sólo participa en configuración y negativos conforme a D4. |
| R5 — generación y asignación | Dirección crea las seis tareas manuales y recupera su resultado sin duplicarlo. La recurrencia TAR-0005 pertenece al sistema. Dirección corrige responsables desde Planificación. | Solicitudes aceptadas/recuperadas y asignaciones vigentes/sustituidas, con explicación e historia. |
| R6 — plan semanal | Dirección crea o recupera el plan, prepara/cancela y confirma publicación. Todos los puestos consultan el resultado permitido. | Plan publicado y versiones conservadas. Cancelar no publica. |
| R7 — trabajo y evidencia | Subcoordinación y Piso aportan los requisitos de sus tareas; leen avisos propios y consultan la revisión. Los archivos se cargan, analizan y sólo se aportan cuando están limpios. | Evidencia incompleta/completa, versión vigente/sustituida y tarea concluida. Concluida todavía puede estar pendiente de validación. |
| R8 — validación y supervisión | Subcoordinación valida tareas de Piso; Administración las de Subcoordinación. Emitir y sustituir una decisión requiere fundamento/motivo. Dirección consulta supervisión. | Una decisión vigente y versiones sustituidas conservadas. Piso recibe denegación de supervisión. |
| R9 — indicadores, auditoría y continuidad | Los cuatro puestos consultan sus conteos y eventos permitidos. Dirección consulta la cadena y solicita un simulacro de recuperación. Sólo un resultado coincidente admisible puede aprobarse. | Ceros y denominadores reales; cadena por vínculos y cuatro etapas con D5 validada; MATCHED aprobado y DIFFERENT sin aprobar. No se presenta una salida fallida como completa. |


## Entrega visual final

Cabeza de las capturas: `3693239e181782386d5038d8be4232341fa53a89`; Chromium 151.0.7922.34, Windows AMD64/Docker Linux AMD64, HTTPS. Los cuatro reportes tienen nueve PASS y cleanup true. Hay 53 imágenes por perfil/ciclo (212 total), todas existentes en sus manifiestos. Los controles compartidos, Poppins local OFL y Georgia se conservan.

- Ciclo 1, desktop: [manifiesto](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-1/desktop/captures.json) y [reporte](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-1/desktop/report.json).
- Ciclo 1, mobile: [manifiesto](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-1/mobile/captures.json) y [reporte](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-1/mobile/report.json).
- Ciclo 2, desktop: [manifiesto](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/desktop/captures.json) y [reporte](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/desktop/report.json).
- Ciclo 2, mobile: [manifiesto](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/mobile/captures.json) y [reporte](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/mobile/report.json).

Selección del ciclo 2 para revisión:

| Recorrido / rol | Aplicación real sanitizada |
|---|---|
| R1 · Dirección | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/desktop/R1-DIRECCION.png) |
| R2 · Dirección | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/desktop/R2-cuentas-y-roles.png) |
| R3 · Dirección | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/desktop/R3-calendario-semana.png) |
| R4 · Dirección | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/desktop/R4-ocho-TAR-politicas.png) |
| R5 · Dirección | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/desktop/R5-generacion-asignacion-historia.png) |
| R6 · Dirección | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/desktop/R6-plan-DIRECCION.png) |
| R7 · Piso | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/mobile/R7-TAR-0007-concluida.png) |
| R8 · Subcoordinación | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/mobile/R8-TAR-0007-validacion.png) |
| R9 · Piso: indicadores | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/mobile/R9-indicadores-PISO_VENTAS.png) |
| R9 · Dirección: traza | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/mobile/R9-traza-completa.png) |
| R9 · Dirección: aceptación | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/desktop/R9-aceptacion-auditada.png) |
| R9 · Dirección: diferencias | [Ver captura](C:/Users/siste/Dev/SGOL-Migracion-20260929/extraido/SGOL-git-portable-v3/SGOL/.artifacts/tech-front005/01a0ff42384a7db783079e62b5b4706a/cycle-2/desktop/R9-restauracion-DIFFERENT.png) |

R1 se captura antes de configurar/publicar calendario y políticas: el estado inicial de Mi trabajo no representa el estado final del sistema; la cadena continúa con R3/R4 y el plan R6. Las máscaras de evidencia son intencionales y conservan controles, requisitos, versiones y estados. Las tablas móviles conservan su desplazamiento horizontal; una imagen fija no acredita un dispositivo físico.

Revisión visual humana solicitada: comprobar etiquetas próximas y alineadas, orden vertical, separación entre filtros/acciones y legibilidad en escritorio/móvil. La revisión automática pasó teclado/foco/retorno, contraste 4.5:1, controles, texto ampliado, reflow y movimiento reducido según el mapa; no sustituye zoom nativo, lector ni dispositivos físicos. Capturas revisadas durante este pase incluyen indicadores de Piso móvil, historia de tres decisiones TAR-0007 y MATCHED aprobado; el responsable aprobó la revisión visual el 2026-10-03 mediante «Ya revise las capturas y todo se encuentra en orden, retomemos el trabajo».

La aprobación visual cierra este pendiente local sobre las capturas entregadas. Conserva los resultados exactos de los dos ciclos y los límites heredados; no acredita los ensayos diferidos ni autoriza publicación, merge o despliegue.
