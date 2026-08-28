# SGOL — Fase 2 · Organización, sucursales, usuarios, roles y permisos

## Propósito del dominio

Este dominio define la estructura organizacional sobre la que opera SGOL y determina quién puede acceder al sistema, bajo qué autoridad actúa, qué acciones puede ejecutar y sobre qué alcance organizacional puede ejercerlas. La identidad laboral de una persona y su puesto pertenecen al dominio de personal; la identidad de acceso, los roles y los permisos pertenecen a este dominio y no deben confundirse entre sí.

## Mapa del dominio

El dominio se divide en ocho responsabilidades:

1. **Organización**: representar la entidad operativa a la que pertenecen las sucursales y áreas.
2. **Sucursales**: identificar ubicaciones o unidades operativas con identidad propia.
3. **Áreas organizacionales**: clasificar las funciones de negocio dentro de una organización o sucursal.
4. **Usuarios**: representar identidades habilitadas para utilizar SGOL, vinculables a un empleado cuando corresponda.
5. **Roles de seguridad**: agrupar responsabilidades de autorización sin sustituir al puesto laboral.
6. **Permisos**: definir capacidades atómicas que pueden ser concedidas a uno o varios roles.
7. **Asignaciones y alcances**: conservar qué roles tiene un usuario, durante qué vigencia y sobre qué sucursales o áreas puede ejercerlos.
8. **Autorizaciones**: conservar decisiones explícitas sobre operaciones que requieren autoridad adicional o trazabilidad formal.

## Principios obligatorios

- Un **usuario** no es un **empleado** y un **rol de seguridad** no es un **puesto**.
- El rol de un usuario no debe almacenarse como un único atributo fijo dentro de la identidad del usuario.
- La autoridad se determina por una asignación vigente de rol y por permisos explícitos.
- La ausencia de un permiso equivale a denegación.
- Un valor que represente “todas las sucursales” es un **alcance**, no una sucursal ficticia.
- Los cambios de rol o alcance deben conservar histórico.
- La resolución de permisos debe ser independiente del nombre visible de la persona.
- Las equivalencias entre puestos, responsabilidades y roles deben ser configurables cuando sean necesarias; no deben permanecer codificadas como listas cerradas dentro de funciones.
- Las autorizaciones excepcionales son hechos auditables distintos de los permisos permanentes.

## Entidades definitivas

### Organización

Entidad persistente que representa la unidad empresarial propietaria de la operación.

**Atributos relevantes**
- `id_organizacion`.
- `codigo_organizacion`.
- `nombre`.
- `activa`.

**Reglas**
- `codigo_organizacion` debe ser único y estable.
- La desactivación no elimina su histórico ni el de sus sucursales.

### Sucursal

Entidad persistente con identidad propia.

**Atributos relevantes**
- `id_sucursal`.
- `id_organizacion`.
- `codigo_sucursal`.
- `nombre`.
- `domicilio` cuando sea necesario para la operación.
- `zona_horaria` cuando el despliegue opere en más de una zona.
- `activa`.

**Reglas**
- `codigo_sucursal` debe ser único dentro de la organización.
- Una sucursal inactiva conserva su histórico y no admite nuevas asignaciones operativas salvo procesos de migración o corrección autorizada.
- El alcance global se representa mediante reglas de alcance, nunca creando una sucursal denominada “TODAS”.

### AreaOrganizacional

Catálogo persistente de áreas funcionales.

**Atributos relevantes**
- `id_area`.
- `id_organizacion`.
- `id_sucursal` nullable cuando el área sea corporativa o transversal.
- `codigo_area`.
- `nombre`.
- `id_area_padre` nullable para jerarquía cuando exista.
- `activa`.

**Reglas**
- El área utilizada por un puesto debe resolverse mediante `id_area`, no por texto libre.
- Las áreas corporativas pueden no depender de una sucursal específica.
- Una misma etiqueta histórica no se fusiona con otra área sin una equivalencia aprobada.

### Usuario

Entidad persistente que representa una identidad habilitada para acceder a SGOL.

**Atributos relevantes**
- `id_usuario`.
- `id_empleado` nullable.
- `identificador_acceso` o identificador estable entregado por el proveedor de autenticación.
- `nombre_visible`.
- `estado_usuario`.
- `fecha_alta`.
- `fecha_baja` nullable.

**Reglas**
- `id_usuario` es la referencia interna para auditoría y autorización.
- El nombre visible no puede utilizarse como clave de autenticación ni de relación.
- Un usuario puede existir sin empleado únicamente cuando exista una razón operativa aprobada, por ejemplo una cuenta técnica.
- Un empleado puede no tener usuario si no necesita acceso al sistema.
- Un usuario inactivo no puede iniciar nuevas operaciones ni recibir nuevas asignaciones de rol.
- La tecnología de autenticación no forma parte de la regla de negocio; el dominio sólo requiere una identidad autenticada estable.

### RolSeguridad

Catálogo administrado por el negocio para agrupar autoridad funcional.

**Roles iniciales del dominio**
- `DIRECCION`.
- `ADMINISTRACION`.
- `SUBCOORDINACION`.
- `PISO_VENTAS`.

**Atributos relevantes**
- `id_rol`.
- `codigo_rol`.
- `nombre`.
- `descripcion`.
- `activo`.

**Reglas**
- Un rol no representa un puesto laboral.
- Un rol puede asignarse a múltiples usuarios.
- Un usuario puede tener más de un rol cuando exista autorización y los alcances no produzcan conflicto.
- La baja de un rol no elimina asignaciones históricas.

### Permiso

Catálogo de capacidades atómicas del sistema.

**Capacidades mínimas identificadas**
- consultar información de Dirección;
- completar una tarea propia;
- validar tareas de Piso;
- validar tareas de Subcoordinación;
- validar tareas de Administración;
- consultar control semanal;
- autorizar excepciones operativas;
- consultar alertas ejecutivas;
- consultar auditoría ejecutiva;
- ejecutar cierre semanal;
- revisar tareas ejecutivas.

**Atributos relevantes**
- `id_permiso`.
- `codigo_permiso`.
- `descripcion`.
- `activo`.

**Reglas**
- Cada permiso debe representar una sola capacidad verificable.
- Los permisos de acceso visual y los permisos de modificación deben permanecer separados cuando impliquen niveles distintos de autoridad.
- La ausencia de concesión activa equivale a denegación.

### RolPermiso

Relación persistente entre un rol y un permiso.

**Atributos relevantes**
- `id_rol`.
- `id_permiso`.
- `permitido`.
- `tipo_alcance`.
- referencia de alcance cuando aplique.
- `requiere_autorizacion_adicional`.
- `id_rol_autorizador` nullable.
- `vigente_desde`.
- `vigente_hasta` nullable.
- `activo`.

**Reglas**
- No pueden existir dos concesiones vigentes incompatibles para la misma combinación de rol, permiso y alcance.
- El alcance debe representarse con identificadores o reglas explícitas, no mediante textos ambiguos.
- Un permiso que requiere autorización adicional no queda satisfecho sólo porque el usuario posea el rol.

### AsignacionUsuarioRol

Hecho persistente e histórico que concede un rol a un usuario dentro de un alcance.

**Atributos relevantes**
- `id_asignacion_usuario_rol`.
- `id_usuario`.
- `id_rol`.
- `tipo_alcance`.
- `id_sucursal` nullable.
- `id_area` nullable.
- `vigente_desde`.
- `vigente_hasta` nullable.
- `asignado_por`.
- `motivo` cuando corresponda.

**Reglas**
- La vigencia de una asignación debe evaluarse por fecha y contexto.
- Un alcance global no requiere una sucursal ficticia.
- Las asignaciones revocadas se conservan para auditoría.
- La asignación de un rol sensible debe quedar trazada con el usuario que la otorgó.

### EquivalenciaPuestoRol

Configuración opcional que permite proponer o resolver una equivalencia entre un puesto operativo y un rol de seguridad cuando el negocio decida derivar autoridad desde una responsabilidad organizacional.

**Atributos relevantes**
- `id_equivalencia`.
- `id_puesto`.
- `id_rol`.
- `tipo_equivalencia`.
- `vigente_desde`.
- `vigente_hasta` nullable.
- `activa`.

**Reglas**
- La equivalencia no sustituye una asignación explícita de rol salvo que exista una política aprobada que así lo establezca.
- Una equivalencia ambigua debe devolver error o requerir decisión, nunca elegir silenciosamente.
- Las responsabilidades canónicas de tareas que describen “responsable” o “validador” no se convierten automáticamente en roles de seguridad.

### Autorizacion

Hecho persistente que registra una decisión explícita de autoridad sobre una operación concreta.

**Atributos relevantes**
- `id_autorizacion`.
- `fecha_hora`.
- `id_usuario_autorizador`.
- `tipo_autorizacion`.
- referencia a la operación o entidad autorizada.
- `rol_objetivo` o alcance funcional cuando corresponda.
- `motivo`.
- `estado_autorizacion`.
- `resultado`.
- `observaciones`.

**Estados mínimos soportados por el dominio**
- `PENDIENTE`.
- `AUTORIZADA`.
- `RECHAZADA`.

**Reglas**
- Una autorización debe identificar al usuario real que tomó la decisión.
- El motivo es obligatorio para operaciones críticas cuando la política lo exija.
- Una autorización no concede permanentemente un permiso ni modifica el rol del usuario.
- La autorización debe poder correlacionarse con la operación que habilitó.

## Relaciones

- Una **Organización** contiene una o varias **Sucursales**.
- Una **Organización** contiene **AreasOrganizacionales** corporativas o locales.
- Una **Sucursal** puede contener múltiples áreas locales.
- Un **Empleado** de Fase 1 puede estar vinculado con cero o un **Usuario** de acceso, sujeto a las reglas de identidad definitivas del despliegue.
- Un **Usuario** tiene cero o múltiples **AsignacionUsuarioRol** a lo largo del tiempo.
- Un **RolSeguridad** tiene múltiples **RolPermiso**.
- Un **Permiso** puede pertenecer a múltiples roles.
- Una **AsignacionUsuarioRol** puede estar limitada a una sucursal, área o alcance global.
- Un **Puesto** de Fase 1 puede tener cero o múltiples **EquivalenciaPuestoRol** históricas.
- Una **Autorizacion** referencia al usuario autorizador y a la operación de negocio correspondiente.

## Matriz inicial de autoridad

### DIRECCION

Puede:
- consultar información e indicadores de Dirección;
- validar tareas de Administración;
- validar tareas de Subcoordinación;
- autorizar excepciones operativas;
- consultar alertas y auditoría ejecutiva;
- revisar tareas ejecutivas;
- ejecutar el cierre semanal.

No debe validar directamente tareas de Piso mediante la cadena ordinaria de supervisión.

### ADMINISTRACION

Puede:
- completar tareas propias;
- consultar y ejecutar el control semanal que no implique cierre definitivo.

No puede:
- ejecutar cierre semanal definitivo;
- validar tareas de Piso por la cadena ordinaria.

### SUBCOORDINACION

Puede:
- completar tareas propias;
- revisar el trabajo de su equipo;
- validar tareas de Piso.

No puede:
- validar tareas de Administración;
- ejecutar cierre semanal definitivo salvo que una futura política explícita le conceda ese permiso.

### PISO_VENTAS

Puede:
- consultar y completar tareas propias;
- adjuntar la evidencia requerida por sus tareas.

No puede:
- validar tareas;
- ejecutar cierre semanal definitivo.

## Reglas de autorización y acceso

1. **Denegación por defecto**: toda acción protegida se considera no autorizada mientras no exista una concesión vigente que la habilite.
2. **Identidad única**: una identidad autenticada debe resolver un único usuario activo.
3. **Roles vigentes**: los roles efectivos se obtienen de asignaciones activas y vigentes, no de un campo fijo del usuario.
4. **Alcance**: un permiso se evalúa contra el contexto de sucursal, área, rol objetivo y entidad afectada.
5. **Permiso + autorización**: cuando una política exige autorización adicional, ambas condiciones deben cumplirse.
6. **Separación de funciones**: poseer autoridad ejecutiva no implica automáticamente saltar la cadena ordinaria de validación.
7. **Histórico**: cambios de rol, alcance y autorización deben conservar quién, cuándo y por qué.
8. **Equivalencias explícitas**: cualquier derivación entre puesto y rol debe provenir de configuración vigente y no de comparación textual libre.
9. **Cuenta inactiva**: un usuario inactivo no puede obtener permisos efectivos aunque conserve asignaciones históricas.
10. **Objeto objetivo**: la autorización debe comprobarse sobre la operación concreta y no únicamente sobre el rol general del usuario.

## Funciones del dominio

### `resolver_usuario_autenticado(identidad_autenticada)`
Devuelve un único usuario activo. Error si la identidad no está registrada, está inactiva o resuelve múltiples usuarios.

### `listar_roles_vigentes(id_usuario, fecha, contexto)`
Devuelve los roles efectivos del usuario para la fecha y el alcance organizacional solicitado.

### `resolver_alcance_usuario(id_usuario, fecha)`
Devuelve sucursales, áreas y alcances globales sobre los que el usuario puede actuar.

### `tiene_permiso(id_usuario, codigo_permiso, contexto, fecha)`
Evalúa roles vigentes, permisos activos y alcance. Devuelve falso por defecto cuando no existe una concesión aplicable.

### `puede_validar(id_usuario, contexto_validacion, fecha)`
Evalúa el permiso de validación aplicable al nivel o rol objetivo. La regla concreta de qué tarea requiere qué validador se completa en Fase 12.

### `resolver_equivalencia_puesto_rol(id_puesto, fecha)`
Devuelve una equivalencia configurada cuando exista y esté vigente. Debe distinguir entre ausencia de equivalencia y ambigüedad.

### `resolver_autoridad_requerida(tipo_operacion, contexto)`
Devuelve el permiso requerido y, cuando aplique, la necesidad de autorización adicional. Los tipos de operación concretos se amplían en los dominios transaccionales posteriores.

### `validar_autorizacion(id_usuario, tipo_operacion, referencia_objeto, contexto)`
Comprueba que exista una autorización válida cuando una operación la requiera.

## Comandos y transacciones

### `registrar_usuario`
Crea una identidad de acceso y opcionalmente la vincula a un empleado existente.

### `activar_usuario`
Habilita una identidad de acceso previamente registrada.

### `desactivar_usuario`
Impide nuevos accesos sin eliminar su histórico.

### `asignar_rol_usuario`
Crea una asignación de rol con vigencia y alcance. Debe registrar quién realizó la asignación.

### `revocar_rol_usuario`
Cierra la vigencia de una asignación sin borrar el histórico.

### `registrar_autorizacion`
Registra una decisión de autorización asociada a una operación concreta, con usuario, fecha, motivo, estado y resultado.

### `crear_sucursal`
Registra una nueva sucursal con clave única.

### `desactivar_sucursal`
Cierra nuevas operaciones para la sucursal sin eliminar su histórico.

### `registrar_area_organizacional`
Crea un área y su relación organizacional.

## Eventos del dominio

Los eventos se emiten únicamente después de una transacción confirmada:

- `UsuarioRegistrado`.
- `UsuarioActivado`.
- `UsuarioDesactivado`.
- `RolAsignadoAUsuario`.
- `RolRevocadoDeUsuario`.
- `AutorizacionRegistrada`.

Estos eventos permiten auditoría e integración, pero no sustituyen el registro persistente de la operación.

## Validaciones y errores de dominio

- `IDENTIDAD_NO_REGISTRADA`.
- `USUARIO_INACTIVO`.
- `IDENTIDAD_AMBIGUA`.
- `ROL_INACTIVO`.
- `PERMISO_INEXISTENTE`.
- `PERMISO_DENEGADO`.
- `ALCANCE_NO_AUTORIZADO`.
- `ASIGNACION_ROL_SOLAPADA` cuando una política prohíba superposición incompatible.
- `EQUIVALENCIA_ROL_AMBIGUA`.
- `AUTORIZACION_REQUERIDA`.
- `AUTORIZACION_NO_VALIDA`.
- `MOTIVO_AUTORIZACION_REQUERIDO`.

## Datos derivados que no deben convertirse en tablas de hechos

- roles efectivos de un usuario en una fecha;
- permisos efectivos de un usuario;
- autoridad efectiva para una operación;
- alcance visible resultante;
- equivalencia vigente entre puesto y rol cuando pueda resolverse desde configuración;
- indicador “puede validar” para una operación concreta.

Estos valores deben calcularse desde asignaciones, permisos, alcances y reglas vigentes.

## Dependencias con otros dominios

- **Fase 1**: `Empleado` y `Puesto` proporcionan identidad laboral y puesto vigente.
- **Fase 3**: calendario y vigencia temporal pueden afectar permisos o configuraciones con fecha.
- **Fases 4 a 10**: tareas, activación, asignación, planificación y ejecución aportan el contexto sobre el que se evalúan permisos.
- **Fase 12**: formaliza validaciones, aprobaciones y gates específicos por operación.
- **Fase 13**: formaliza autorizaciones de excepciones de ciclo de vida.
- **Fase 14**: formaliza el permiso y autorización del cierre semanal.
- **Fase 16**: consume permisos y alcance para filtrar vistas y dashboards.
- **Fase 17**: conserva auditoría de cambios de seguridad y autorizaciones.

## Pendientes que deben resolverse en fases posteriores o en configuración de despliegue

1. Definir el catálogo definitivo de sucursales y sus claves de negocio; el modelo no debe inferirlo a partir de etiquetas de alcance.
2. Definir el mecanismo real de autenticación y el identificador externo estable que utilizará cada usuario.
3. Confirmar si un empleado podrá tener más de un usuario o una cuenta compartida; por defecto el diseño evita cuentas compartidas.
4. Completar roles o permisos adicionales cuando Caja, Cobranza, Almacén u otras funciones requieran acceso propio; no se deben deducir sólo del nombre del puesto.
5. Resolver qué equivalencias de responsabilidad canónica pertenecen a puestos, cuáles pertenecen a reglas de asignación y cuáles realmente requieren un rol de seguridad.
6. Completar en Fase 12 los contratos de aprobación y validación por tipo de operación, preservando la matriz inicial de autoridad aquí definida.
7. Definir el alcance de auditoría y retención de autorizaciones en Fase 17.
