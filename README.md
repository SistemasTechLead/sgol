# SistemaSGOL

SistemaSGOL es una aplicación web para organizar y dar seguimiento al trabajo
operativo de las sucursales de Loretta. Conecta la definición de tareas y procesos
con la planificación, la asignación a colaboradores, la entrega de evidencias y
la revisión de los responsables.

Su propósito es que cada persona pueda consultar qué debe hacer y cuándo,
registrar su avance y entregar su trabajo, mientras quienes coordinan la operación
pueden identificar pendientes, vencimientos y cargas de trabajo.

## Qué permite hacer

- **Definir tareas:** mantener un catálogo con instrucciones, duración estimada,
  recurrencia, responsables y requisitos de evidencia.
- **Planificar el trabajo:** preparar planes de tareas y procesos considerando
  horarios, disponibilidad y capacidad de los colaboradores.
- **Asignar actividades:** distribuir el trabajo planificado y atender necesidades
  ocasionales mediante asignaciones específicas.
- **Ejecutar y documentar:** consultar las actividades en **Mi trabajo**, registrar
  avances y entregar fotografías, archivos u otros datos según lo solicitado.
- **Supervisar entregas:** revisar evidencias y dar seguimiento al cumplimiento
  de las actividades.
- **Consultar la operación:** visualizar el estado del día en **Tablero** y la
  distribución semanal en **Rol de tareas**.
- **Analizar resultados:** generar reportes de cumplimiento, vencimientos, cargas
  de trabajo y seguimiento de planes, con exportación a PDF.

## Cómo se utiliza

El flujo comienza con el catálogo de tareas y la configuración de personas,
sucursales y horarios. A partir de esa información se preparan planes y se generan
asignaciones. Cada colaborador atiende sus actividades desde **Mi trabajo**;
las entregas y revisiones alimentan los tableros y reportes.

El acceso se organiza por roles de **Dirección, Administración, Subcoordinación
y Piso de ventas**, con permisos específicos para las funciones de cada uno.
La interfaz se adapta a escritorio y móvil e incluye apariencia clara y oscura.

## Tecnología y organización

El sistema está desarrollado con **.NET 10**, **ASP.NET Core Razor Pages** y
**PostgreSQL**. La solución separa la aplicación web de los módulos de negocio,
incluidos identidad, organización, configuración, planificación, asignación,
ejecución y evidencias.

- `src/`: aplicación web, módulos de negocio y componentes de infraestructura.
- `tools/`: herramientas de preparación, importación y comprobación.
- `scripts/`: inicio local, acceso en red y utilidades de operación.
- `docs/`: documentación funcional, técnica y de diseño.

## Puesta en marcha y documentación

Puede utilizarse en esta computadora o compartirse dentro de la red local.
Consulta las instrucciones de preparación y ejecución:

- [Iniciar y trabajar en esta computadora](docs/instalacion-local.md)
- [Compartir en la red local](docs/acceso-red.md)
- [Esquema y migraciones](docs/normalizacion-bd.md)

## Actualizar GitHub

Desde PowerShell, en esta carpeta, después de implementar y verificar los cambios:

```powershell
.\scripts\publicar-github.ps1 -Mensaje "Describe el cambio realizado"
```

El comando agrega los cambios, crea un commit y los sube directamente a `master`.
No requiere pull request ni espera un pipeline. Si GitHub tiene cambios ajenos,
se detiene para que los integres antes de publicar. Requiere Git y acceso al repositorio.

Este repositorio contiene el código, las migraciones, herramientas y documentación.
Las credenciales, bases locales, evidencias, respaldos y resultados generados no se
publican. Los prototipos y las utilidades de demostración de uso puntual también
quedan fuera de Git. Se conservan las pruebas de regresión y las herramientas
necesarias para preparar, ejecutar y mantener el sistema.
Una clonación nueva necesita preparar su entorno siguiendo la
[guía de instalación local](docs/instalacion-local.md).
