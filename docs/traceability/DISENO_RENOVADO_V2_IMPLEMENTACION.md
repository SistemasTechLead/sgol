# SGOL — Implementación local del estilo visual v2

2026-09-30. Único hito de adaptación de pantallas existentes, FRONT-001..016. **Implementada localmente; pantallas aprobadas por el responsable. Publicación pendiente de resolver la fuente.** No se inicia FRONT-017..020 ni se amplía el catálogo MVP.

## Autorización, base y conservación

Aprobación literal: «Apruebo las correcciones y el complemento del plan para implementar». La instrucción posterior exige capturas implementadas y concordancia documental antes de «mi aprobacion explicita para hacer el commit, push y PR». Se cumple esa condición: ningún nuevo commit, push, PR, pipeline, merge o despliegue durante esta implementación. HEAD de partida (antes de commits de publicación): de0d6aaed90e5b6efc8e4580fb1d920de60c9c13, rama codex/diseno-renovado; checkout inicial limpio. Los commits previos se conservan.

Se leyó IMPLEMENTATION_STATUS primero y se ejecutó preflight como única comprobación inicial. El SDK exacto no se resolvía en PATH: se usó la instalación aislada 10.0.400 ya disponible en C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400, sin cambiar global.json, instalar herramientas ni restaurar dependencias. Evidencia anterior, incluida FRONT-016, aceptada; no se repiten sus gates de cierre.

Fuentes y F00–F07 congelados intactos. Adenda 55 se amplía con §6; docs/INDICE_IDS agrega su rango. No se cambian modelos/handlers productivos, módulos, endpoints, permisos, jerarquía, sesión, mensajes contractuales, auditoría o contratos. Los formularios conservan name/id, CSRF, intenciones, ETag/If-Match y cursores. La única modificación JS productiva preserva el icono al alternar Mostrar/Ocultar y asegura Tab/Shift+Tab dentro de la navegación modal entre motores; Escape y retorno permanecen.

## Implementación y evidencia por grupo

| Grupo | Superficies y archivos | Referencia documentada | Diseño implementado y cobertura |
|---|---|---|---|
| G0 | Shared/_Layout, nuevo _Icon, _CredentialField, _DataTable, _StatusBadge; wwwroot/css/tokens y components, js/components, fonts e images | ESTILO_VISUAL_V2; tokens; componentes; navegación | Shell Loretta/SGOL, SVG propios, sesión derecha/avatar de iniciales, Poppins local y botones neutros/textuales. Navegación autorizada por servidor intacta. Normal/foco/deshabilitado/error/carga/vacío consumidos por las superficies siguientes. |
| G1 | Access/Index, People/Index y Details; Entry recibe base compartida | Marca centrada, versión, Delicias; panel de persona completo, empleo y disponibilidad | Acceso y ocho pasos/estados existentes conservan formularios. Seasons sólo bienvenida. Puesto/Turno en dos columnas; Motivo y Guardar en filas propias; paneles y padding completos. Cuenta/rol y diálogos existentes mantienen controles. Capturas normales, errores, vacío, foco y progreso. |
| G2 | Configuration/_TaskDefinitions y demás secciones existentes mediante CSS compartido | Componente TAR único y selector/acción con separación | Ocho contenidos canónicos con una plantilla y query taskCode, sin nuevas rutas. Gap 24/16, acciones naturales, release y políticas existentes. Error, vacío, controles deshabilitados y foco conservados. |
| G3 | Planning/_ManualGeneration, _Plans y secciones existentes mediante base; fixtures visuales de asignación/plan | Formularios compactos, mensajes separados, tablas semánticas | Seis formularios manuales presentes, consulta/asignación/carga/plan/publicaciones existentes. Gap de mensaje 24. Conflicto, resultado incierto, confirmación, vacío y denegación renderizados. Recorrido HTTPS de plan creado/preparación, sin publicar alcance. |
| G4 | MyWork/Index, _Inbox, _Notices, _Obligations; Details/_WorkPagination y Branches reciben base | Semana legible, tarea/período/origen agrupados, flecha, estados | Cinco columnas de bandeja conservan cada dato; avisos y consulta separada intactos. Cuatro roles, tarea disponible/no disponible, historia/vacío y sucursal normal/error/cargando/vacío. Sólo las tablas desplazan en estrecho. |

Los archivos de Entry, Branches, MyWork/Details y _WorkPagination no tienen diff semántico: su adaptación proviene de la base compartida. No se copian selectores de muestra, endpoints ficticios, datos de maqueta, contadores inventados ni acciones simuladas a producción.

## Concordancia entre documentos y código

| Regla vigente | Implementación | Evidencia |
|---|---|---|
| Variables oficiales exclusivamente | tokens.css coincide con el bloque root de tokens.md; consumidores sólo usan variables declaradas | InterfaceDesignRulesTests, PASS; sin biblioteca nueva |
| Fuentes/marca aprobadas | 3 fuentes Poppins locales y Georgia del sistema, sin archivo distribuido, y SVG derivado de curvas del EPS, OFL conservada | concordancia.json: recursos byte a byte iguales a propuesta aprobada y SHA-256; carga de fuentes/imágenes en ambos motores |
| Poppins operación / Georgia bienvenida | @font-face local de Poppins, Georgia por token y acceso | medidas computadas, capturas e inspección visual |
| Persona: padding 24/16, gap 24/16, ancho útil | persona-detalle, formulario-persona, grid de empleo y motivo | concordancia.json + capturas; datos completos y radio con etiqueta clicable de 44px |
| Release TAR: gap 24/16 | seccion-definiciones__formulario | ocho variantes + medición de estilos; plantilla única conservada |
| Plan: mensaje separado 24 | #plan-error:not(:empty) | conflicto/resultado incierto y concordancia.json |
| Acceso: logo/SGOL/versionado/Delicias | bienvenida-acceso, metadatos de ensamblado sin sufijo de commit | escritorio/móvil; versión local 1.0.0, ubicación visible; no cambia America/Mexico_City |
| Semana de sistema visible y rango con mes | periodo-semana condicionado a período confirmado | MyWork/Index, pruebas/capturas; no inferencia ante error o ausencia |
| Secundario neutro, auxiliares textuales/flecha | clases boton y parcial SVG | capturas, contraste efectivo, foco y teclado |
| Tablas con caption/th/scope | estructura existente; partición normal de palabras, contenedor scroll | 354 combinaciones, recorrido hospedado y capturas; sin columnas de una letra ni datos ocultos |
| Móvil/200 %/movimiento reducido | layout responsive, palabras largas en paneles, fieldset sin mínimo intrínseco; navegación modal | 254 comprobaciones y HTTPS; no overflow global ocultado |

Se revisaron los nueve documentos operativos de diseño. La precedencia v2 está explícita; se corrigió la exclusión antigua de avatar en tokens y el token de cabecera de tabla en componentes. Reglas históricas del plan documental y del cierre v1 se mantienen identificadas como históricas, sin autoridad sobre ejemplos incompatibles con la guía v2. La referencia de próximas historias es autocontenida: no depende de la carpeta externa de maqueta.

## Validaciones ejecutadas

- `dotnet build --no-restore --configuration Release`: PASS, 0 errores/advertencias. Recompilaciones enfocadas justificadas por cambios de Razor/fixtures: PASS, 0 errores/advertencias. Sin restore, paquetes o cambio de SDK fijado.
- `dotnet test tests/Sgol.UnitTests --no-build --configuration Release --filter 'FullyQualifiedName~ComponentRenderTests|FullyQualifiedName~Front013IntentionTests|FullyQualifiedName~Front014PresentationTests|FullyQualifiedName~Front015PresentationTests|FullyQualifiedName~Front016PresentationTests|FullyQualifiedName~RenewedDesignPreviewTests'`: **135/135 PASS**.
- `dotnet test tests/Sgol.ArchitectureTests --no-build --configuration Release --filter 'FullyQualifiedName~InterfaceDesignRulesTests|FullyQualifiedName~Front016ArchitectureTests'`: **10/10 PASS**. Incluye igualdad de variables documentadas/hoja, consumidores, pares de contraste oficiales y protección de presentación/autorización.
- `dotnet test tests/Sgol.FrontendBrowserTests --no-build --configuration Release --filter 'FullyQualifiedName~RenewedDesignBrowserTests'`: **2/2 PASS**, Chromium escritorio/WebKit móvil, Kestrel HTTPS/PostgreSQL desechable. Consultas de siete superficies sin efecto en datos; caso de elegibilidad ausente 404 y tarea inexistente 404 conservados; plan confirmado, preparación de publicación y Cancelar inicial; diálogo de cuenta con retorno; móvil modal Tab/Escape/retorno; reflow a 320 y texto CSS 200 %. Cleanup confirmado.
- Chromium y WebKit, 59 variantes × 1440/390/320: **354 combinaciones PASS**, sin overflow de página, áreas interactivas menores a 44×44, caption/scope faltantes, fuentes o imágenes rotas. Radio/checkbox se mide por la etiqueta asociada clicable, conservando el indicador nativo.
- Texto CSS al 200 % a 390/320, teclado/foco/retorno y mostrar/ocultar/progreso: **254 comprobaciones PASS**. Los mensajes y controles quedan accesibles; movimiento reducido sin pulsación del esqueleto.
- Concordancia de composición y recursos: **39 comprobaciones PASS**.
- `git diff --check`: PASS. Inspección del diff: sin cambios de Fuentes, contratos, módulos, endpoints o modelos productivos.

Correcciones durante validación: fieldset y palabras largas desbordaban a 320 con texto ampliado; se corrigieron sin ocultar overflow. WebKit requería recorrido explícito Tab en navegación modal; se conserva ciclo y retorno. El fixture de conclusión necesitaba autores sintéticos MFA-enrolados para no crear una segunda cuenta de la misma persona; opción exclusiva del recorrido visual, sin modificar dominio. La comprobación anterior confundía el fondo transparente final con negro y comparaba texto visible de bienvenida oculto intencionalmente en móvil; ahora compone sobre canvas y compara contenido DOM conservado. La consulta de asignación sin evaluación confirmada retorna 404 correctamente: se mantiene como caso negativo, separado de planificación normal. No se reducen umbrales de contraste/área ni se alteran reglas para obtener verde.

TRX de ejecución fuera del repo: C:/Users/siste/.codex/tmp/sgol-v2-implementado/resultados/v2-unit.trx, v2-architecture.trx y v2-https.trx. Scripts locales de captura/medición en esa carpeta; render Razor reproducible mediante los fixtures versionados y variables SGOL_RENEWED_PREVIEW_DIR, SGOL_FRONT014_PREVIEW_DIR y SGOL_FRONT015_PREVIEW_DIR.

## Capturas y revisión

[Evidencia](evidence/diseno-renovado-v2/) contiene **120 capturas Razor** (59 variantes escritorio/móvil + foco/progreso), **47 capturas de HTTPS** y los informes medidas.json, interacciones.json, concordancia.json y capturas-manifiesto.json con huellas. Todas usan datos sintéticos; credenciales/intenciones/CSRF se eliminan de export HTML, y los campos sensibles se enmascaran en captura hospedada. No hay cookies, cadenas de conexión, secretos ni evidencia real en los entregables.

Las variantes Razor de plan/asignación combinan parciales reales renderizados con el layout real; no se presentan como integración. Las capturas bajo https sí proceden de endpoints productivos sobre la base desechable. Inspección visual representativa: Acceso, Mi trabajo, persona, Configuración móvil y conflicto de plan escritorio; después de corregir tablas, revisión adicional de móvil/persona y diálogos hospedados.

Galería local completa para revisión: http://127.0.0.1:8766/ (archivo C:/Users/siste/.codex/tmp/sgol-v2-implementado/galeria/index.html). Explica grupos y distingue recorridos reales de render de estados. No es un endpoint del producto.

## Validaciones diferidas y requisitos pendientes

- Zoom nativo de la aplicación/navegador, lector de pantalla y dispositivos físicos: no automatizados en este recorrido. Se verificó ampliación CSS de texto 200 %, reflow 320, teclado y motores; esto no certifica WCAG integral ni todas las versiones de Chrome/Edge/Safari.
- Suite integral, format integral y checks remotos: reservados al hito de integración/publicación, todavía sin autorización; no se presentan como PASS ni como impedimento para la implementación local validada.
- The Seasons: falta soporte documental de derechos de alojamiento/redistribución web antes de publicación. El archivo autorizado por el responsable está incorporado para trabajo local privado; aportar el archivo no acredita por sí solo esos derechos.
- Aprobación visual de las pantallas implementadas y autorización explícita posterior de nuevos commits/push/PR: pendientes del responsable. Publicación, merge y despliegue no autorizados.

## Aprobación de pantallas y publicación — 2026-09-30

Respuesta literal del responsable: «Apruebo las pantallas, ahora vamos a agregar esto al repositorio, apruebo commit, push y abrir el PR». Autoriza commits, publicación de esta rama y un único PR con todo el hito, seguimiento y correcciones de sus checks. No autoriza merge ni despliegue. La espera de aprobación de pantallas registrada anteriormente quedó superada por esta decisión.

Preparación: origin/master incorpora FRONT-016 mediante ec53eed; el diff de contenido entre la base común ce76ab8 y ese merge es vacío. No se republica la historia anterior. Se conserva como referencia la cabeza que contiene esta actualización y el PR que la incorpore, sin commits administrativos autorreferenciales. La licencia web de The Seasons sigue pendiente de documentación antes del push, según el complemento aprobado; se solicitó su ubicación al responsable mientras se preparan los commits locales.

Validación de formato previa a publicación: primer intento detectó CRLF en los tres archivos de pruebas modificados; normalizados a LF conforme a .editorconfig, sin cambios de comportamiento. Segundo intento: `dotnet format --verify-no-changes --no-restore`, PASS (exit 0); `git diff --check`, PASS.

### Decisión pendiente de fuente antes de publicar

El responsable confirma que no dispone de licencia de The Seasons y prevé uso interno sin ingresos. Esa declaración no acredita permiso de alojamiento o redistribución. Se verificó que origin es un repositorio público. [My Creative Land](https://mycreativeland.com/licensing/) exige licencia para @font-face y limita la distribución de archivos y sus formatos web; [Adobe Fonts](https://fonts.adobe.com/fonts/the-seasons) identifica la misma familia y remite al proveedor para self-hosting. No se afirma una exención por uso interno.

Se presentó fuera del repositorio una alternativa concreta con Georgia, respaldo ya definido por --fuente-marca, únicamente para la frase de bienvenida. Se espera decisión del responsable entre esa sustitución y obtener derechos adecuados manteniendo The Seasons. Los commits locales autorizados se preparan, pero no se sube la rama ni se abre PR mientras no se resuelva este requisito del plan. Si se aprueba excluir The Seasons, se retirará también de los nuevos commits no publicados; borrarlo sólo en la cabeza no bastaría para excluir su archivo del historial enviado.

## Decisión de tipografía para publicación — 2026-09-30

«Autorizar Georgia como en la vista presentada y excluir The Seasons del repositorio y su historial de publicación». Georgia reemplaza la familia de marca sólo en bienvenida, con respaldo Times New Roman/serif; Poppins OFL conserva toda la operación. Se elimina @font-face y el archivo de The Seasons, también de los commits no publicados enviados. No se distribuye ningún archivo de Georgia. Esta decisión supera el requisito pendiente de derechos de The Seasons mediante su exclusión; los registros anteriores describen la secuencia histórica. No cambia alcance, contratos ni autorización de merge/despliegue.

Validación de la sustitución: arquitectura 10/10 y HTTPS/PostgreSQL Chromium/WebKit 2/2 PASS; 354 combinaciones visuales, 254 comprobaciones de interacción/texto y 38 de concordancia PASS. Son 38 al excluir la comparación del binario de fuente retirado; la familia Georgia se comprueba en ambos anchos. Capturas y manifiesto renovados: 167 imágenes sintéticas. Inspección de Acceso de escritorio confirma la composición aprobada. TRX georgia-architecture.trx y georgia-https.trx en la carpeta externa de resultados. Build Release y format integral PASS registrados antes de esta sustitución exclusiva de CSS/documentos. Zoom nativo, lector y dispositivos físicos conservan su validación diferida.

## Publicación y corrección del pipeline PR #85 — 2026-09-30

Hito **Publicada**, no Integrada: https://github.com/SistemasTechLead/sgol/pull/85. Run inicial 36794989105, cabeza 1e2f47d39ea3df55580ecbafb8b855b81edd4d8b. Verificación previa al push: The Seasons ausente de los árboles de los 19 commits enviados; 12 commits previos preservados y árbol final idéntico tras retirar el binario del historial.

Controles de Fuentes/secretos y servidor PASS. Servidor: 64 arquitectura, 895 unitarias, 265 integración y las demás suites seleccionadas, build sin errores/advertencias, format integral y vulnerabilidades PASS. Navegador: 23/25 PASS, dos expectativas antiguas fallidas; operations omitido por dependencia y consolidado PR gates FAIL, sin aceptación del hito. El título operativo aprobado es Mis tareas. El mensaje de sesión permanece literal, pero su contenedor incluye un icono aria-hidden; la igualdad debe comparar el strong del mensaje. Se ajustan únicamente esas expectativas en Front002BrowserTests y ShellBrowserSmokeTests, conservando todos los checks de sesión, CSRF, autorización, invalidación, foco y cookies. No cambia código productivo ni capturas.

Validación de la corrección: build Release sin restore PASS, 0 errores/advertencias; format --verify-no-changes --no-restore --include sobre los dos archivos PASS; diff --check PASS. Recorridos ShellBrowserSmokeTests y Front002BrowserTests: 2/2 PASS, Chromium/WebKit con PostgreSQL y cleanup confirmado. Primera ejecución local interrumpida deliberadamente tras más de seis minutos sin resultado, no se registra como PASS ni se atribuye causa no probada. Reintento con marcadores cerrados temporales de etapas del fixture: 46 s, 2/2 PASS; instrumentación retirada del código final. TRX pr85-regresion-shell-diagnostico.trx fuera del repositorio. Los cuatro cambios de expectativas conservan igualdad exacta y los checks de seguridad originales. Pipeline nuevo requerido para la cabeza de corrección; ningún resultado de una cabeza anterior sustituye sus gates.
