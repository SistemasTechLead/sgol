# Plan de implementación: acceso móvil compacto y centrado

Fecha: 31/10/2026 · America/Mexico_City.

Estado: implementado y cargado en el host local/LAN el 31/10/2026; validación de dispositivos físicos y pasos MFA pendiente. Clasificación: interfaz/frontend. La petición posterior autorizó implementar, compilar y recargar el host compartido; no autorizó commit ni push. Las secciones de propuesta conservan el razonamiento original; la ejecución y la matriz definitiva están al final.

## Objetivo y alcance

Aplicar al login móvil la composición presentada: logo y ubicación próximos al formulario, un único título, campos y botón alineados y versión discreta al final. Centrar el conjunto cuando quepa; permitir desplazamiento natural cuando el contenido o el teclado reduzcan el espacio útil.

Rediseño limitado al estado `login` de `/acceso`, incluidos sus avisos y errores. Conservar la composición de escritorio y los demás pasos de acceso. No cambiar autenticación, permisos, sesiones, contraseñas, MFA, datos, contratos HTTP ni reglas operativas. No crear migraciones ni incorporar bibliotecas o fuentes externas.

## Investigación actual

Se inspeccionaron el protocolo, AGENTS.md, la referencia visual y el código. Rama observada: `master`. Hay cambios anteriores en el layout, estilos y scripts móviles, documentación visual y módulos operativos; deben conservarse y separarse del diff de esta tarea.

| Fuente | Hallazgo e implicación |
|---|---|
| `src/Sgol.Web/Pages/Access/Index.cshtml` | Una misma estructura de marca y formulario envuelve login, cambio de contraseña, MFA, códigos y finalización. La marca contiene logo SVG, versión, bienvenida, ubicación y símbolo. El login muestra «Acceso a SGOL» y después «Acceso». |
| `src/Sgol.Web/wwwroot/css/components.css`, reglas `.pagina-acceso`, `.bienvenida-acceso` y `.acceso` | En móvil se apilan los paneles, pero el contenedor mantiene `min-height:100vh`. La marca conserva `justify-content:space-between`; las filas de grid pueden absorber espacio libre. Son causas probables de los huecos, pendientes de comprobar en el render real. |
| `src/Sgol.Web/wwwroot/css/mobile.css` | Hay reglas compartidas para títulos, controles y contenido. Deben considerarse al resolver la cascada; los inputs móviles ya reciben texto de 16 px y controles de al menos 44 px. |
| `src/Sgol.Web/Pages/Shared/_Layout.cshtml` | `SkipSession` produce la clase `pagina-acceso`; carga tokens, componentes y estilos móviles. El layout contiene cambios previos. |
| `src/Sgol.Web/Pages/Shared/_CredentialField.cshtml` | Mantiene etiquetas, autocomplete, required, errores asociados y Mostrar/Ocultar mediante atributos compartidos. Reutilizarlo sin modificaciones globales. |
| `src/Sgol.Web/wwwroot/js/components.js` | Implementa Mostrar/Ocultar y el estado de envío para `data-access-form`. Conservar los selectores y comportamiento. |
| `src/Sgol.Web/Pages/Access/Index.cshtml.cs`, `OnGetAsync` y `OnPostAsync` | Determina el paso y procesa los formularios; login envía usuario/contraseña a `/api/v1/auth/login` mediante el puente antiforgery. No se propone editar este archivo. |
| `tools/Sgol.MobileChecks/` | Existen verificaciones con Playwright Chromium/WebKit y recorridos de acceso. Algunas crean usuarios o cambian contraseñas: no ejecutarlas indiscriminadamente contra la simulación compartida. |

La guía visual describe versión debajo de SGOL; la plantilla actual la coloca debajo del logo. La decisión de esta propuesta sustituye esa ubicación solamente en login móvil. La maqueta anterior era un esquema, no evidencia de la pantalla real: su rótulo tipográfico no sustituirá `loretta.svg`, su versión no se fijará a 1.0.0 y sus colores oscuros no se copiarán; se usarán los recursos y tokens efectivos.

No se verificaron en esta fase el proceso activo, URL efectiva, binario, base de datos ni render del entorno compartido. No se ejecutaron pruebas de aplicación ni se escribieron datos operativos. Identificar esos destinos antes de las pruebas de implementación.

## Diseño a implementar

1. Fondo continuo con el token existente de superficie elevada; eliminar únicamente en login móvil la separación visual entre marca y formulario, borde y sombra del panel.
2. Columna de contenido de hasta 360 px de ancho útil, centrada, con márgenes laterales mínimos de 24 px. En 320 px quedan 272 px útiles. Permitir reflujo de textos sin reducir su tamaño.
3. Logo SVG original centrado, ancho inicial de 144 px limitado al espacio disponible y alto automático. Ubicación centrada debajo, separación de 8 px, conservando «Loretta · LOR-001» y «Delicias, Chihuahua».
4. Separación de 24 px entre marca y encabezado del formulario. Un solo h1 visible y accesible: «Acceso a SGOL». Ayuda existente debajo, separada 8 px. No reintroducir la frase de bienvenida ni el escudo ocultos actualmente en móvil.
5. Campos alineados a la izquierda, separación de 16 px entre grupos y 8 px entre etiqueta y control. Inputs y botón principal de al menos 48 px de alto; texto de input de 16 px. Mostrar/Ocultar conserva su fila y área de al menos 44 px.
6. Botón principal a todo el ancho. Versión dinámica centrada 24 px después del formulario y sus estados; usar metadatos del ensamblado, sin datos de infraestructura.
7. Centrado vertical del conjunto completo, incluida la versión, solo cuando cabe. Espacio mínimo superior e inferior de 24 px más áreas seguras aplicables. Con errores, texto ampliado, orientación horizontal o poca altura, crecer hacia abajo y permitir scroll del documento.

No usar altura fija, posición absoluta del formulario, botón pegado a la pantalla, recorte ni scroll anidado. `100dvh` puede seguir las barras del navegador, pero no garantiza por sí solo adaptación al teclado virtual; comprobar el teclado real y conservar el desplazamiento nativo al foco.

## Secuencia de implementación propuesta

### 1. Registrar la línea base

- Revisar diff y estado local de nuevo antes de editar. Identificar URL, proceso y artefacto que sirven la pantalla; distinguirlos del checkout.
- Capturar login real claro/oscuro a 390×844, 320×568 y escritorio 1280×800; medir marca, formulario, ancho del documento y espaciados computados.
- Usar aplicación y base aisladas para envíos y fixtures de autenticación; identificar explícitamente el destino. La simulación compartida puede aportar una línea base de lectura, sin reinicios.

### 2. Delimitar el estado y adaptar el marcado

- En `Pages/Access/Index.cshtml`, añadir una clase de presentación específica cuando `Model.Stage == "login"` y, si hace falta para el main, una marca de presentación consumida en `_Layout.cshtml`. No usar una regla genérica que rediseñe todo `.pagina-acceso`.
- Conservar el h1 y su id `acceso-titulo`; usar variantes de texto excluyentes por CSS para mostrar «Acceso» en escritorio y «Acceso a SGOL» en móvil. Ocultar el contexto redundante solo en login móvil. Asegurar un único nombre accesible efectivo.
- Mantener la versión de escritorio donde está. Añadir su presentación móvil al final del conjunto, obtenida de la misma variable Razor, sin ids duplicados; ambas instancias serán mutuamente excluyentes con `display:none`. Así no se mueve el DOM con JavaScript ni se altera el orden de los controles.
- Conservar form, action, method, names, ids, CSRF, `stepAction`, atributos de autocomplete, referencias ARIA y `data-access-form`.

### 3. Aplicar CSS acotado

- Añadir un bloque identificado en `wwwroot/css/mobile.css`, dentro de pantalla y del contenedor existente `sgol-viewport` hasta 48rem. Delimitarlo al login; respetar preferencias automática/móvil/escritorio ya existentes.
- Neutralizar el alto mínimo y distribución expansiva de la tarjeta y marca solo en ese bloque. El main controla el alto mínimo de pantalla, la composición controla su altura por contenido.
- Usar layout flexible con márgenes verticales automáticos que se reduzcan a cero cuando no haya espacio, fallback `100vh` y mejora `100dvh`, padding seguro y scroll documental. Comprobar también el min-height heredado de los ancestros.
- Reutilizar tokens, Poppins local, logo, campos y botones. No copiar CSS de la maqueta ni modificar tokens globales.
- Resolver márgenes acumulados de encabezado y ayuda para que los 24/16/8 px sean separaciones efectivas, no sumas accidentales.
- No añadir JavaScript de viewport inicialmente. Si el teclado físico revela un fallo que CSS no resuelve, documentar la reproducción y evaluar una corrección mínima de presentación sin tocar autenticación.

### 4. Verificar y documentar

- Añadir una regresión de navegador mantenible en `tools/Sgol.MobileChecks/access-mobile-checks.cjs`: geometría centrada cuando cabe, espaciado compacto, ausencia de overflow horizontal y acceso al contenido cuando crece. Comprobar comportamiento observable, no textos de reglas CSS.
- Compilar `src/Sgol.Web/Sgol.Web.csproj` si se modifica Razor y ejecutar las verificaciones dirigidas contra la aplicación real aislada. No considerar la compilación sustituto del recorrido HTTP/UI.
- Añadir a `docs/design/ESTILO_VISUAL_V2.md` la precisión de login móvil conservando sus cambios anteriores. Actualizar este documento con resultados y evidencia local sin secretos.
- Revisar diff por archivo, registrar límites de cobertura y separar implementación, commit/push y carga en entorno compartido. El presente plan no ejecuta esas acciones.

## Matriz de validación propuesta

Esta es la matriz original de escenarios **propuestos**. Su estado ejecutado y sus límites se registran en la matriz definitiva al final. E = aplicación real aislada, Chromium y WebKit; D = dispositivo físico contra un destino de pruebas identificado. Capturas sin credenciales ni códigos secretos.

| Escenario y precondición | Acción/prueba | Resultado esperado | Entorno | Estado |
|---|---|---|---|---|
| Login vacío, 390×844 y 430×932, claro/oscuro | Abrir, capturar y medir rectángulos | Conjunto centrado; diferencia entre espacios superior/inferior ≤8 px cuando cabe, descontando áreas seguras; gap marca/formulario 24 px; inputs y botón alineados | E | Propuesto |
| Login a 320×568 y 360×640 | Inspeccionar y recorrer por scroll | Sin overflow horizontal mayor a 1 px, ningún contenido recortado; scroll vertical permitido cuando sea necesario | E | Propuesto |
| Límite móvil 767/768/769 px y preferencia guardada | Cambiar ancho y modos automático/móvil/escritorio | Se respeta el contenedor real y las preferencias; no persisten ocultamientos ni duplicados al cambiar de modo | E | Propuesto |
| Escritorio 1280×800 y 1440×900, ambos temas | Comparar con línea base | Se conservan los dos paneles, título y posición de versión; sin diferencias atribuibles al rediseño móvil | E | Propuesto |
| Teclado virtual, usuario y contraseña | Enfocar, escribir, alternar Mostrar/Ocultar, desplazarse y cerrar teclado | Campo enfocado visible, controles alcanzables; sin saltos que impidan escribir; vuelve la composición al cerrar | D: Safari iOS y Chrome Android | Propuesto |
| Horizontal y barras del navegador | Rotar y expandir/contraer barras | Documento desplazable; no se pierden marca, campos ni botón | D; geometría adicional en E | Propuesto |
| Teclado físico y lector de pantalla | Tab/Shift+Tab, Enter y lectura de encabezados/etiquetas | Orden usuario → contraseña → Mostrar/Ocultar → enviar; foco visible, un solo título y versión anunciados; toggle mantiene estado accesible | E y lector disponible en D | Propuesto |
| Texto al 200 %, error largo y poca altura combinados | Ampliar texto y provocar error real con fixture aislado | Error completo y asociado; contenido crece sin solaparse ni quedar fuera del área desplazable | E | Propuesto |
| Formulario vacío y credenciales incorrectas | Enviar vacío; después enviar fixture inválido | Validación y mensajes existentes, sin alteración de campos enviados ni CSRF; reintento disponible según conducta vigente | E | Propuesto |
| Inicio de sesión válido con usuario de prueba existente | Completar formulario real | Estado de envío visible y redirección prevista a Mi trabajo; comportamiento de sesión intacto | E | Propuesto |
| Aviso de cierre/expiración y envío lento | Abrir aviso real y limitar red durante envío | Mensajes conservados, botón y estado legibles, sin ocultar avisos para mantener centrado | E | Propuesto |
| Pasos password, enroll, enroll-key, verify, regenerate, codes, complete y expired | Alcanzar con fixtures y flujos reales; comparar presentación | No reciben el rediseño del login; controles y contenido siguen accesibles, incluso códigos largos | E, configuración MFA aislada cuando corresponda | Propuesto |

La emulación WebKit no equivale a Safari físico ni a teclado virtual. Si un dispositivo o fixture no está disponible, registrar el caso como no ejecutado y explicar la limitación; una maqueta no lo sustituye.

Migraciones, cargas, asignaciones, responsables, historial operativo y concurrencia de planes: **no aplicable**, porque la propuesta no modifica sus productores ni consumidores. No se plantea una regresión global de negocio.

## Criterios de cierre y riesgos

- El login móvil reúne marca y formulario sin espacio expansivo entre ellos, mantiene su identidad real y cumple los escenarios de tamaño, tema, foco, error y teclado.
- El escritorio y los demás pasos de acceso conservan su comportamiento y presentación. Cambios de autenticación o de componentes globales quedan fuera del alcance.
- Las pruebas se registran como aprobadas, fallidas, no ejecutadas o no aplicables con evidencia y destino. No afirmar validación completa si quedan pruebas críticas de teclado o del recorrido real pendientes.
- Riesgos principales: cascada con estilos móviles previos; altura dinámica y teclado; ocultamientos incorrectos al cruzar breakpoint; duplicación accesible de título o versión. Los selectores acotados y la matriz cubren esos riesgos.
- No hay decisiones de negocio pendientes para este alcance. Alternativa descartada: centrar solo el formulario conservando un panel de marca expansivo, porque mantendría la separación que motiva el cambio. Centrado rígido descartado porque perjudica pantallas bajas, errores y teclado.
- Reversión: retirar solo el diff de esta implementación y su regresión si procede, sin restauraciones globales de archivos con cambios ajenos. No requiere reversión de datos.

## Ejecución y evidencia — 31/10/2026

### Cambios realizados

- `Pages/Access/Index.cshtml`: clase `bienvenida-acceso--login` exclusivamente para login, un h1 con variantes excluyentes y versión móvil al final obtenida de la misma variable Razor. Los siete formularios se compararon textualmente contra la copia previa: idénticos. No se modificaron PageModel, componentes compartidos, JavaScript, autenticación ni base de datos.
- `wwwroot/css/mobile.css`: reglas acotadas a pantalla/contenedor/login; composición flexible, márgenes automáticos, fallback vh/dvh y padding con áreas seguras. Se neutralizan mínimos heredados de tarjeta y aplicación. Se conservó íntegro el CSS previo. Se eliminó también el margen heredado del h1 que duplicaba la separación de ayuda.
- `tools/Sgol.MobileChecks/access-mobile-checks.cjs`: regresión mantenible sobre páginas reales, ambos motores, geometría, espaciados efectivos, preferencias, formularios, foco automatizado y errores. Modo sin `--write` para comprobación compartida; los envíos exigen localhost:7284 y marcador de base aislada. No guarda formularios, cookies ni credenciales en resultados.
- Se añadió la precisión visual correspondiente a `ESTILO_VISUAL_V2.md`, conservando sus cambios anteriores. No se modificó `_Layout.cshtml` en esta tarea.

### Entorno identificado y carga

La observación actual sustituyó la referencia histórica: al comenzar, `scripts/run-local.ps1 -Service web -Red` ejecutaba `dotnet watch --no-hot-reload` y `src/Sgol.Web/bin/Debug/net10.0/Sgol.Web.exe`, PID 23384, en `127.0.0.1:7241`. El proxy PID 20668 servía `https://localhost:7240` y `https://192.168.1.92:7440` (evidencias en 7443). Configuración efectiva: PostgreSQL local 5440, base simulada `sgol_local`, rol runtime; no se publicaron secretos.

Antes de editar se copiaron el binario operativo y los recursos, se guardó el diff previo y se capturaron ambos motores/temas a 390×844, 320×568, 1280×800 y 1440×900. Se retiró el watcher para evitar compilaciones parciales y se mantuvo el servicio con la copia operativa (PID 9980). Línea base: en 390×844 la marca ocupaba 290 px; el título empezaba a 384 px.

Pruebas con escrituras: `https://localhost:7284`, base existente aislada `sgol_task_checks_mobile_ios_20261030`, configuración explícita en `.local/access-mobile-20261031/start-test.ps1`. Solo intentos de autenticación y sesión del fixture; sin cambios operativos ni catálogos. Las pruebas compartidas no enviaron formularios al servidor.

Compilación/publicación Release aprobada mediante `dotnet publish src/Sgol.Web/Sgol.Web.csproj -c Release --artifacts-path .local/access-mobile-20261031/build`; registros `build.log` y `build-final.log`, sin advertencias ni errores observados. Artefacto final: `.local/runtime/access-mobile-20261031/Sgol.Web.dll`, PID 5540; hash SHA256 `52DB986EDA0A280BFF8D44DDC1E7194B9D3821A5D48F98B939D22C66639195C9`. El ajuste final de margen CSS se sincronizó también con el artefacto publicado; no exigió otro reinicio.

Se recargó únicamente Web, conservando configuración, base y proxy PID 20668. `health/ready` confirmó `ready`; LAN devolvió HTTP 200 y el marcado nuevo. Se actualizó `.local/processes.json` con el proceso de arranque. El mecanismo actual es `run-local.ps1 -Service web -Red -BinaryPath <artefacto>`, sin watcher ni procesos Web compartidos duplicados. El script local `deploy.ps1` contiene recuperación a la copia previa si falla la carga; no fue necesario ejecutarla. No se reiniciaron PostgreSQL ni servicios auxiliares.

### Matriz definitiva

Evidencia privada local: `.local/access-mobile-20261031/`. No se incorpora al repositorio. `baseline.json` y `before-*.png` guardan la línea base; `results.json` y `shared/results.json` guardan resultados finales; `deployment.json` identifica la carga.

| Escenario | Prueba y entorno | Estado y evidencia |
|---|---|---|
| 390×844, 430×932, claro/oscuro | Chromium y WebKit, aplicación real aislada y LAN; rectángulos, capturas, centro ≤8 px | **Aprobado**; columna ≤360, margen ≥24, marca/formulario 24, ayuda 8, campos 16, etiqueta/control 8, versión 24; controles ≥48 |
| 320×568, 360×640 | Ambos motores/temas, ancho y scroll documental | **Aprobado**, overflow ≤1 px y versión alcanzable |
| 767/768/769 y preferencias | Ambos motores, automático/móvil/escritorio persistidos y recarga | **Aprobado**, un título y una versión visibles; composición cambia según contenedor |
| Escritorio 1280×800 y 1440×900, temas | Comparación de geometrías con línea base, capturas reales | **Aprobado**, diferencias <1 px en tarjeta, marca, formulario y título; no es comparación píxel a píxel |
| Horizontal 844×390 | Geometría de ventana automatizada en ambos motores | **Aprobado** para geometría; rotación física y barras dinámicas **no ejecutadas** |
| Tab, Shift+Tab, Enter y Mostrar/Ocultar | Eventos de Playwright en ambos motores, foco y aria-pressed | **Aprobado como automatización**, no prueba de teclado físico ni lector de pantalla |
| Texto 200 %, error real y altura 390 | Duplicación explícita de fuentes e interlineado computados; captura/scroll en ambos motores | **Aprobado** como ampliación artificial del contenido real; no equivale a zoom o ajustes nativos del dispositivo |
| Vacío, credenciales inválidas y envío lento | Formularios reales aislados, espera artificial antes del POST, asociación ARIA y botón deshabilitado | **Aprobado** en ambos motores; mensajes íntegros y reintento disponible |
| Login válido | Fixture existente mediante `.local/mobile-ios/login.cjs`, destino `/mi-trabajo` | **Aprobado Chromium**; WebKit válido **no ejecutado**. La suite general lo marca no ejecutado al no recibir archivo de credenciales; la prueba separada consta en `login-notice.json` |
| Aviso de cierre | Cierre real del fixture desde escritorio y login resultante a 320 px | **Aprobado Chromium**, `logout-notice.png` y `login-notice.json`. Aviso de expiración **no ejecutado** |
| password, enroll, enroll-key, verify, regenerate, codes, complete, expired | Inspección de ramas y comparación exacta de los siete formularios, CSS restringido a login | Contratos **aprobados por inspección**; recorridos/render con fixtures **no ejecutados**, no se preparó fixture MFA. No se afirma validación visual de esos estados |
| Safari iOS y Chrome Android físicos, teclado virtual, áreas seguras reales | Dispositivos no conectados a esta sesión | **No ejecutado**; sigue siendo limitación crítica para validar teclado virtual |
| Teclado físico y lector de pantalla | No se dispuso de interacción física ni lector | **No ejecutado**; la automatización no lo sustituye |
| Cargas, responsables, historial, concurrencia de planes, migraciones | Sin cambios en sus productores/consumidores | **No aplicable** |

Resultados finales: 46 escenarios aprobados en la suite aislada y 44 en la suite LAN, cero fallos finales; más login válido y aviso de cierre por prueba separada. Se inspeccionaron visualmente capturas reales claras/oscuras, estrechas y con error ampliado. La LAN se comprobó desde el equipo anfitrión, no desde otro dispositivo de la red.

### Fallos encontrados y límites de la evidencia

- **Fallido y corregido en producto:** margen heredado del h1 sumaba 8 px extra. La regresión de espaciados lo detectó; se anuló solo en login móvil y se repitieron ambas suites.
- **Fallos de ensayo corregidos:** WebKit requería esperar dos frames tras redimensionar y medir posición documental incluyendo scroll nativo; la ampliación artificial necesitaba tratar interlineados `normal`/unitarios. La captura con líneas superpuestas no se aceptó como validación; se corrigió el ensayo y se volvió a inspeccionar.
- **No completado en el primer intento:** aviso de cierre desde menú móvil agotó el tiempo al no estar visible el control seleccionado. Se comprobó cierre real desde escritorio y su aviso móvil; ese intento no demuestra ni corrige el menú móvil, fuera de alcance.
- El arranque inicial aislado se intentó antes de que terminara publish y falló por ruta aún inexistente; se inició correctamente tras finalizar. La revisión automática rechazó dos comandos combinados de arranque en segundo plano; el host de pruebas se ejecutó de forma supervisada en la terminal. No se creó archivo de credenciales para esta tarea. El despliegue compartido autorizado sí se ejecutó y verificó.
- `git diff --check` convencional marca CRLF como espacios en algunos cambios anteriores; con `core.whitespace=cr-at-eol`, los archivos de producto de esta tarea pasan. Se conservaron los cambios ajenos; sin commit ni push.

Cierre: **implementado y disponible, con validación física y de otros pasos de acceso pendiente**. Revisar en `https://localhost:7240/acceso` o `https://192.168.1.92:7440/acceso`; una sesión ya iniciada puede redirigir a Mi trabajo, por lo que conviene una ventana privada. No se modificó la preferencia de vista del usuario.

Comprobación final adicional: `final-health.json` confirma `ready`, `alive`, PID Web 5540 y proxy 20668; se verificó por HTTP que LAN sirve la regla final de margen del título. La instancia aislada de puerto 7284 se detuvo al terminar. La revisión completa con `git -c core.whitespace=cr-at-eol diff --check` terminó sin errores.

Seguimiento: el usuario autorizó después incorporar estos cambios al repositorio. Se prepara un commit local limitado a los cinco archivos de esta tarea, separando las modificaciones anteriores de CSS y documentación visual. Las referencias anteriores a ausencia de commit describen el cierre de implementación previo a esta autorización.
