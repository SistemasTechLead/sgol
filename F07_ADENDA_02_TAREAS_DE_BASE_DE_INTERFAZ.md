# SGOL — Adenda 02 a F07: tareas de base de interfaz

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Adenda de inserción al orden de ejecución de `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | PROPUESTA |
| Fecha | 2026-08-31 |
| Efecto | Amplía F07 mediante una tarea técnica previa; no modifica ni renumera el backlog aprobado |
| Conservación | Mantiene sin cambios `F07_BACKLOG_DE_IMPLEMENTACION.md` y los demás entregables F00–F07 aprobados |
| Eficacia | Pasa a `APROBADA` al incorporarse a `master`; desde ese momento su contenido es obligatorio |

## 2. Hueco y justificación

Los documentos aprobados establecen tres obligaciones relacionadas, pero no una tarea que construya una base de interfaz compartida:

1. `F06_ARQUITECTURA.md:166` define una interfaz ASP.NET Core Razor Pages/MVC con mejora progresiva y sin SPA separada para el MVP.
2. `F07_BACKLOG_DE_IMPLEMENTACION.md:18`, regla 4 de priorización, exige que cada historia produzca un resultado extremo a extremo que incluya `HTTP/UI mínima`.
3. `F07_BACKLOG_DE_IMPLEMENTACION.md:123` ubica `TECH-E2E-<CV>` antes del cierre de cada corte e incluye accesibilidad crítica en esa verificación.

De esas menciones se infiere un hueco de ejecución: ninguna tarea anterior a las historias funcionales establece layout, navegación, componentes comunes, presentación uniforme de errores, estados vacíos o de carga y accesibilidad crítica. Sin esa base, cada historia con UI podría resolver esos elementos de forma independiente e incompatible.

## 3. Tarea técnica insertada

La fila usa las mismas columnas de la sección 5 de F07.

| ID | Resultado verificable | Dependencias | No incluye |
|---|---|---|---|
| TECH-UI-001 | Base de interfaz compartida: layout, navegación condicionada por rol, componentes de formulario y tabla, presentación de errores a partir de Problem Details, estados vacíos y de carga, y verificación de accesibilidad crítica | TECH-BASE-005, TECH-AUD-001, TECH-ID-BOOT-001 | Ninguna pantalla funcional, ninguna regla de negocio, ningún endpoint de datos y ninguna capacidad de HU-001 a HU-035 |

## 4. Orden efectivo y bloqueo

`TECH-UI-001` se ejecuta **ANTES de HU-005**. Mientras `TECH-UI-001` no esté `Terminada`, bloquea el inicio de HU-005 y de cualquier otra historia que incluya UI. Las órdenes 1 a 35 de `F07_BACKLOG_DE_IMPLEMENTACION.md` conservan su numeración; esta adenda inserta una puerta previa y no las sustituye.

La comprobación obligatoria del bloqueo se registra en `docs/traceability/IMPLEMENTATION_STATUS.md`, sección `Tareas insertadas por adenda`, y se aplica mediante la regla de precedencia de `AGENTS.md` antes de iniciar cualquier tarea.

## 5. Límite de navegación y autorización

La navegación condicionada por rol se implementa en `TECH-UI-001` únicamente como estructura preparada para mostrar opciones según un rol. No implementa autorización funcional ni reglas de HU-007, porque HU-007 aún no existe cuando se ejecuta esta tarea. La autorización real continúa aplicándose en el servidor y no puede depender de ocultar o mostrar elementos de la interfaz.

## 6. Aprobación y eficacia

Esta adenda permanece como `PROPUESTA`. No declara `TECH-UI-001` `Terminada` ni habilita HU-005. Al incorporarse a `master`, pasa a `APROBADA` y desde ese momento su contenido es obligatorio; hasta entonces, el cambio documental y su registro de precedencia son una propuesta en la rama correspondiente.
