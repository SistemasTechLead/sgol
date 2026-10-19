# SistemaSGOL

Sistema operativo de tareas, planificacion, evidencias y supervision con .NET 10,
Razor Pages y PostgreSQL.

- [Iniciar y trabajar en esta computadora](README.local.md)
- [Compartir en la red local](README.red.md)
- [Esquema y migraciones](docs/normalizacion-bd.md)

## Actualizar GitHub

Desde PowerShell, en esta carpeta, despues de implementar y verificar los cambios:

```powershell
.\scripts\publicar-github.ps1 -Mensaje "Describe el cambio realizado"
```

El comando agrega los cambios, crea un commit y los sube directamente a `master`.
No requiere pull request ni espera un pipeline. Si GitHub tiene cambios ajenos,
se detiene para que los integres antes de publicar. Requiere Git y acceso al repositorio.

Tambien puedes usar Git directamente:

```powershell
git add --all
git commit -m "Describe el cambio realizado"
git push
```

Este repositorio contiene el codigo, las migraciones, herramientas y documentacion.
Las credenciales, bases locales, evidencias, respaldos y resultados generados no se
publican. Una clonacion nueva necesita preparar su entorno siguiendo README.local.md.
