# TECH-FRONT-005 — Ejecución local de la demo integral

Estado: harness en desarrollo. Ejecución completa pendiente; consultar informe y mapa de cobertura antes de atribuir PASS.

## Alcance aprobado

Único plan TECH_FRONT_005_PLAN_DE_IMPLEMENTACION.md y Adenda 60. R1..R9 en Chromium escritorio y móvil emulado, cuatro roles, PostgreSQL real, HTTPS y S3/ClamAV propios. TAR-0026 conserva D4: configuración y negativos, sin fuente de servicio/pago ni cadena CAT-006. Descarga/preview excluidos por D2; BR-API04 abierta globalmente. D5 y Adenda 61 autorizan expresamente, en commit separado, la corrección mínima de lectura de traza para hechos reconocidos anteriores a asignación, como dependencias sin sujeto sólo para Dirección. No amplía permisos, endpoints, dependencias ni despliega.

## Comandos y orden

Desde raíz y rama codex/tech-front-005, usar SDK exacto. No ejecutar preflight otra vez como diagnóstico de cada pase. No ejecutar restore si existen los artefactos y no cambiaron dependencias. Build/test se esperan hasta exit real y nunca se superponen.

```powershell
$sgolSdk = 'C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe'
& $sgolSdk build SGOL.slnx --no-restore --configuration Release
& $sgolSdk test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~TechFront005ReportTests'
& $sgolSdk test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~TechFront005LateCat003And004'
git diff --check
# Tras conservar el código y la documentación en una cabeza local limpia:
./scripts/demo/run-tech-front005.ps1 -Mode Automated -Cycles 2 -CertificateConfirmation Manual -DotnetPath $sgolSdk
```

El runner exige cabeza comprometida y árbol limpio, guarda su SHA exacto, crea directorio propio con UUID v7 en `.artifacts/tech-front005/`, retira temporalmente **todos** los SGOL_FRONT*_CAPTURE_DIR y restaura sus valores al terminar. Mantiene PATH del SDK y DOTNET_CLI_UI_LANGUAGE=en sólo en su proceso. No lanza gates remotos.

Para identificar fallos de la regresión cuyo diagnóstico anterior no se conservó: `./scripts/demo/run-tech-front005.ps1 -Mode RegressionDiagnostic -CertificateConfirmation Manual -DotnetPath $sgolSdk`. Ejecuta sólo Category=FRONT_BROWSER, registra BROWSER_DIAGNOSTIC con ciclo 0 y nunca genera summary.json ni acredita un ciclo integral. El reporte retiene exclusivamente método fallido sin argumentos, archivo/línea, tipo de excepción y códigos cerrados de cleanup/fallo primario; no conserva mensajes nativos, valores de aserciones ni payloads. No hay retry automático ni cambio de expectativas.

Por ciclo ejecuta secuencialmente: INTEGRAL (R1..R9 en ambos perfiles); BROWSER_REGRESSION (Category=FRONT_BROWSER, con complemento WebKit soportado); POSTGRESQL_API (selección cerrada en script); PRIVATE_S3_CLAMAV (proyecto Sgol.EvidenceIntegrationTests); CONTRACTS_AND_FORMULAS (unitarias relacionadas). Descubre y verifica conteos, cero fallos/omisiones y exit code real. El `summary.json` sólo aparece con cuatro reportes completos, misma cabeza y cleanup correcto. Un fallo detiene el ciclo y conserva resultados; no hay retry automático.

Los comandos del plan para pruebas TechFront005 unitarias/de arquitectura eran propuestas de ubicación. Las pruebas efectivas de reporte están en el proyecto Browser; el borde temporal nuevo está en Integration. No se ejecutan filtros vacíos ni se presentan clases inexistentes como validación.

La fase PRIVATE_S3_CLAMAV activa `SGOL_EVIDENCE_EICAR_TESTS=true` exclusivamente alrededor de su prueba existente y restaura el valor previo incluso al fallar. Es la condición operativa ya documentada en evidence-local-ci.md para ensayar el negativo de antimalware; no se elimina su aserción ni se habilita EICAR en la aplicación o en otras fases.

## Fixtures, aislamiento y evidencia

Cada perfil crea PostgreSQL origen/restauración, S3 privado origen/destino, ClamAV, red e imagen con identificadores únicos. La identidad de Dirección usa bootstrap real; las otras tres identidades son precondiciones sintéticas. Una auxiliar se crea desde UI. Configuración, calendario, seis solicitudes manuales, plan, evidencia, conclusión, decisiones y solicitudes de continuidad se producen por acciones de interfaz; la recurrencia, evaluación/asignación y operaciones técnicas de restore usan contratos existentes del sistema.

El certificado HTTPS de localhost es efímero y de este perfil; el soporte lo añade a CurrentUser Root antes de iniciar Kestrel y verifica TLS normalmente. En Windows usa certutil.exe -user -f -addstore Root sobre un CER público temporal propio y -delstore Root por la huella exacta, con exit code y verificación de presencia/ausencia y eliminación del CER. Estos comandos pueden mostrar confirmaciones de Windows: -f no garantiza funcionamiento desatendido. Computer Use comprobó la aceptación y retirada de un certificado sintético tras cotejar su nombre y huella exacta. El registro owned-certificates conserva únicamente huella, sujeto, vigencia y etapa; cada diálogo se coteja con INSTALL_REQUESTED o REMOVE_REQUESTED antes de actuar. No se aceptan certificados ajenos ni se alteran políticas o privilegios. La salida nativa permanece en memoria. Fuera de Windows conserva X509Store. Un fallo de instalación intenta retirar exclusivamente ese certificado recién generado. La llamada interna Razor mantiene la validación HTTPS. Los diálogos y fallos de pases anteriores se conservan como historia; sólo una ejecución real con cleanup correcto acredita el pase nuevo.

S3 local usa HTTP declarado, con CORS restringido al origen HTTPS del perfil y bucket de cuarentena propio. Chromium observa un PUT HTTP 200 real. Los archivos son PNG/PDF del corpus sintético, tipo real/SHA-256/ClamAV antes de LIMPIO y vínculo. Esto no acredita aislamiento SeaweedFS productivo ni resuelve el fallo histórico PUT de WebKit Windows.

El oráculo PostgreSQL usa lecturas de vínculos/versiones/snapshots y auditoría. Las pruebas independientes con fixtures directos acreditan autorización, rollback, idempotencia y carreras; no sustituyen las acciones UI ni prueban un recorrido completo por sí solas. El caso temporal tardío se identifica expresamente como backend independiente.

## Reportes y cleanup

Cada `cycle-N/desktop|mobile/report.json` conserva navegador/versión, plataforma, SHA, recorridos/roles/estado/duración, HTTPS, fallo cerrado y cleanup. `captures.json` enumera recorrido, archivo y rol de cada imagen propia. `phases.json` conserva proyecto, selector, comando, conteos/exit/duración y cabeza. Imágenes `R*.png` muestran aplicación real y pasan rasterización de accesibilidad en memoria antes de guardarse; se enmascaran secretos y la columna de contenido de evidencia. No se guarda trace/HAR, salida nativa sensible, cuerpos, conexiones, cookies, contraseñas, TOTP, recovery, URLs firmadas ni evidencia binaria.

Cleanup en finally: contextos, browser y driver; fixture; confianza HTTPS propia; proceso web, contenedores/red, certificados/PFX/temporales propios; imagen propia. Registra componentes cerrados y verificación de ausencia. Sólo se eliminan recursos inventariados del perfil. Nunca Docker prune, limpieza global, volúmenes ajenos ni borrado funcional de historia. Si cleanup falla, queda FAIL y bloquea el cierre; investigar logs y causa antes de reintentar. Las capturas/reportes sanitizados permanecen.

Los límites heredados y el coste se conservan en informe/mapa. La estimación del plan de 2–4 h no permite omitir ciclos. El estado Implementada localmente requiere criterios cubiertos, ambos ciclos, mapa y capturas revisables; revisión visual solicitada al responsable. Publicación, PR, checks remotos, merge y despliegue requieren sus autorizaciones separadas.

La fixture R5 ejercita una sola ventana TAR-0005 de las 12:00 del día operativo, ya vencida durante estos pases. El procesador y ScheduledJobRunner/replay usan la misma fecha/corte; PostgreSQL comprueba una obligación antes de las seis altas manuales. No usar UtcNow como corte del Worker: después de las 17:00 incluiría legítimamente otra recurrente, fuera de las siete cadenas de esta fixture. Las ventanas productivas 12:00/17:00 y sus pruebas existentes no se cambian. El contrato del Worker rechaza ScheduledFor futuro; la fixture no fuerza aceptación ni sustituye el reloj productivo. Una discrepancia en R9 conserva diagnóstico cerrado de cantidades/período/rol, sin contenido ni secretos.

Confirmación de Windows: para este operador se usa -CertificateConfirmation Manual, por decisión expresa de atender personalmente los diálogos. El helper espera la salida real de certutil hasta la respuesta humana; no acepta certificados, no reintenta, no introduce sleeps ni cambia aserciones/plazos de navegador. Rechazo del diálogo, exit no cero, TLS inválido o certificado pendiente de retirada siguen siendo FAIL. El modo Bounded predeterminado conserva 120 s y no se modifica para CI. El modo se registra en owned-certificates, phases y summary, con variable sólo del proceso/restaurada al terminar. El tiempo incluye atención humana y no acredita rendimiento. No abrir otra ejecución mientras el operador/fixture anterior sigue pendiente; finalizar o cancelar y comprobar cleanup de recursos propios primero. Los intentos históricos vencidos mantienen su resultado.
