# SGOL para pruebas en la red del trabajo

Desde la carpeta del proyecto, en PowerShell:

```powershell
.\scripts\iniciar-red.ps1
```

El iniciador detecta la IPv4 privada con puerta de enlace. Si hay más de una,
indica la interfaz que corresponde al trabajo:

```powershell
.\scripts\iniciar-red.ps1 -Direccion 192.168.1.110
```

La dirección de acceso es **https://TU-IP:7440**. Tú y tus compañeros deben usar
esa misma dirección mientras esté activo el modo de red. La aplicación conserva
los datos, cuentas y contraseñas de las pruebas locales. Crea una cuenta para cada
compañero desde la administración de usuarios del sistema.

También puedes abrir **https://localhost:7240** en esta computadora: redirige a
la dirección de red, conservando una sola dirección de sesión y autenticación.
El proxy se ejecuta con `dotnet.exe`, que coincide con la regla del firewall.

## Firewall: una vez por dirección IP

Ejecuta desde PowerShell **como administrador**, en la carpeta del proyecto:

```powershell
.\scripts\habilitar-red.ps1
```

La regla solo permite conexiones TCP a los puertos 7440 y 7443 desde la subred
local y hacia la dirección seleccionada. No cambia el perfil de red de Windows.
Si cambia tu IP, vuelve a ejecutar ambos iniciadores de configuración.
La red del trabajo debe permitir comunicación entre equipos; una red de
invitados puede bloquearla aunque el firewall esté configurado.

## Certificado: una vez por compañero

Entrega únicamente `.local/SGOL-red.cer` y el script
`scripts/confiar-certificado-red.ps1`. El iniciador imprime la huella del
certificado: compártela también para comprobar que están instalando el correcto.
En la computadora del compañero:

```powershell
.\confiar-certificado-red.ps1 -Certificado .\SGOL-red.cer -Huella HUELLA_IMPRESA
```

Instala el certificado público para el usuario actual, sin privilegios de
administrador. Después abre `https://TU-IP:7440`. Si el navegador o la política
de la empresa usa otro almacén de confianza, TI debe instalar el certificado allí.
No compartas `red.pfx`, `red.json`, `config.json`, contraseñas ni toda la carpeta
`.local`: contienen claves privadas o credenciales.

El certificado dura un año y contiene la IP elegida. El iniciador lo reemplaza
cuando cambia la dirección o está próximo a caducar. En ese caso, distribuye de
nuevo el certificado público y su nueva huella. Conviene reservar tu IP por DHCP
con ayuda de TI para conservar la dirección.

## Cambios, reinicio y regreso a localhost

Edita y guarda como siempre: `dotnet watch` aplica Hot Reload o reinicia la web
y el worker. Los compañeros actualizan con F5 o Ctrl+F5. Para reiniciar todo y
aplicar migraciones:

```powershell
.\scripts\iniciar-red.ps1
```

Para detener:

```powershell
.\scripts\detener-local.ps1
```

Para volver al modo original, accesible en `https://localhost:7240`:

```powershell
.\scripts\iniciar-local.ps1
```

Los iniciadores detienen los procesos del modo anterior antes de arrancar.
La computadora debe permanecer encendida, conectada y sin suspensión. Los
reinicios interrumpen el servicio brevemente y los cambios en datos son compartidos.
Para eliminar la regla del firewall, desde PowerShell como administrador:

```powershell
.\scripts\habilitar-red.ps1 -Cerrar
```

## Comprobar el servicio

```powershell
Invoke-RestMethod https://TU-IP:7440/health/ready
Test-NetConnection TU-IP -Port 7440
Test-NetConnection TU-IP -Port 7443
```

El proxy HTTPS en 7440 envía las solicitudes a la aplicación en
`127.0.0.1:7241`. El proxy HTTPS en 7443 envía las evidencias a SeaweedFS en
`127.0.0.1:8333`, conservando las firmas S3 y CORS. PostgreSQL, SeaweedFS sin TLS
y ClamAV siguen publicados únicamente en loopback. La aplicación solo confía
en las cabeceras reenviadas por el proxy en `127.0.0.1`.

Configuración y certificado privado: `.local/red.json` y `.local/red.pfx`.
Registros: `.local/proxy.log`, `.local/proxy.error.log`, y los registros web y
worker habituales. El proxy usa YARP y arranca como proceso de desarrollo;
no instala servicios de Windows ni cambia el arranque de la computadora.

Si Windows muestra `Una directiva de Control de aplicaciones bloqueó este
archivo (0x800711C7)`, el problema impide arrancar también en localhost.
Smart App Control no permite excepciones para aplicaciones individuales desde
su aviso. Consulta con TI una solución compatible con la política del equipo; los
registros de `Microsoft-Windows-CodeIntegrity/Operational` identifican la
política que lo bloquea. No es un fallo de puertos ni del certificado.
