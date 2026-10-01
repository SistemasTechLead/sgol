# estados-de-dominio.md — SGOL / Loretta Zapatería

## Precisión vigente del estilo v2

La base visual v2 está aceptada y documentada en [ESTILO_VISUAL_V2.md](ESTILO_VISUAL_V2.md), por solicitud expresa de actualización del responsable el 2026-09-30. Sus reglas precisan los ejemplos anteriores: secundario neutro sin borde rojo, fuentes locales, logo/iconos, sesión a la derecha y acceso con composición de marca/formulario. Las correcciones y el complemento fueron aprobados mediante «Apruebo las correcciones y el complemento del plan para implementar». La implementación local se registra por grupo en [DISENO_RENOVADO_V2_IMPLEMENTACION.md](../traceability/DISENO_RENOVADO_V2_IMPLEMENTACION.md); la aprobación del diseño no equivale a publicación o integración. Los contratos y mensajes funcionales se conservan.

## Referencia renovada — semántica conservada

[Adenda 55](../../F07_ADENDA_55_REFERENCIA_DE_DISENO_RENOVADO.md) adopta [referencia-renovada.md](referencia-renovada.md) exclusivamente para presentación. Todos los valores persistidos, etiquetas, iconos, pares semánticos y significados aprobados de este documento se conservan. No hay estado nuevo, permiso ni transición de negocio.

Los badges compartidos conservan pastilla, texto e icono; se agrupan con wrap y `--espacio-8`. Nunca se comunica sólo color ni se colorea un panel entero por el estado de una obligación. La bandera Vencida acompaña al estado base; Concluida sigue siendo diferente de validada, RECUPERADA no es creación nueva y PUBLICADO no acredita todos los niveles. La maqueta no reemplaza mensajes ni semántica. Las pantallas existentes materializarán estos usos visuales únicamente en su hito de adaptación.

## UI-E01/E02 — variantes aprobadas para FRONT-016

Adenda 54 incorpora §6 del plan FRONT-016 aprobado íntegramente. FUTURA: «Futura», calendario, par info. DISPONIBLE: «Disponible», círculo informativo, par info. Ambas acompañan «Pendiente», sin reemplazar executionStatus. VENCIDA conserva la bandera peligro junto a Pendiente. CONCLUIDA conserva el badge exito. UNREAD: «Sin leer», sobre cerrado, par info. READ: «Leído», sobre abierto, texto-secundario/superficie-elevada. Todo lleva texto e icono; son presentación de valores recibidos, no estados persistidos nuevos. Programada es procedencia; Concluida no significa Validada.

## Plan semanal PUBLICADO

Para UI-G05/G06, aprobado por la sección 7 de `F07_ADENDA_53_PROPUESTA_CONSUMIDOR_FRONT_015.md`, PUBLICADO se presenta como «Publicado», icono de documento con check y `--color-info` / `--color-info-fondo`. Indica al menos una publicación efectiva, sin afirmar validación, conclusión o publicación de todos los niveles. BORRADOR, VIGENTE y SUSTITUIDA conservan texto, iconos y tokens existentes. Se incorpora sólo presentación de un estado ya persistido.

Estos son los estados que aparecen en casi cualquier pantalla del sistema.

**Regla obligatoria, sin excepción: ningún estado se comunica solo por color.** Todo badge de estado lleva texto en español visible y un ícono. El color es refuerzo, no el mensaje. Un usuario con daltonismo, o una captura de pantalla en escala de grises impresa para revisión, tiene que poder leer el estado igual.

Segunda regla: no todo es verde/rojo. Un estado que es simplemente historia (`SUSTITUIDA`) o un reintento (`RECUPERADA`) no es ni éxito ni error — pintarlo de rojo o verde le mentiría al usuario sobre qué pasó. Por eso varios estados abajo usan `--color-info` o un gris neutro en vez de forzarlos al par éxito/peligro.

## Personas, cuentas, sucursal

| Estado | Texto (ES) | Token de color | Ícono | Por qué |
|---|---|---|---|---|
| ACTIVA | "Activa" | `--color-exito` / `--color-exito-fondo` | círculo con check | Es el estado normal de operación, no una aprobación puntual — se usa el verde de éxito porque comunica "todo en orden", que es lo que realmente significa aquí. |
| INACTIVA | "Inactiva" | `--color-texto-secundario` / `--color-superficie-elevada` | círculo con línea diagonal (⊘) | Deliberadamente neutro, no rojo: una cuenta o sucursal inactiva no es un error ni una falla, puede ser una baja programada o temporal. Pintarla de peligro alarmaría sin necesidad. |

## Tareas

| Estado | Texto (ES) | Token de color | Ícono | Por qué |
|---|---|---|---|---|
| PENDIENTE | "Pendiente" | `--color-advertencia` / `--color-advertencia-fondo` | reloj | Es trabajo normal por hacer, no un error del sistema. Ámbar comunica "requiere acción" sin la urgencia falsa de un rojo. |
| CONCLUIDA | "Concluida" | `--color-exito` / `--color-exito-fondo` | check | Tarea terminada por quien la ejecuta. Distinto de "validada" (ver evidencia más abajo): concluida es lo que reporta el responsable, no lo que confirma el superior. |

## Configuraciones versionadas

| Estado | Texto (ES) | Token de color | Ícono | Por qué |
|---|---|---|---|---|
| BORRADOR | "Borrador" | `--color-texto-secundario` / `--color-superficie-elevada` | lápiz sobre documento | Configuración en edición, todavía no aplica a nadie. Neutro a propósito: no es ni buena ni mala señal, es simplemente "no está en uso todavía". |
| VIGENTE | "Vigente" | `--color-exito` / `--color-exito-fondo` | escudo con check | Es la versión que el sistema está aplicando ahora mismo. Verde porque es información operativa positiva: "esta es la que manda". |
| SUSTITUIDA | "Sustituida" | `--color-info` / `--color-info-fondo` | reloj de historial / archivo | **No es un error.** Es una versión anterior reemplazada por una VIGENTE más nueva — es historia, trazabilidad. Usar rojo o gris apagado la haría ver como una falla; usar info azul comunica "esto es dato histórico, consúltalo si necesitas contexto". |

Para UI-C05, `INACTIVA_NUEVAS` se presenta como «Inactiva para nuevas generaciones» con `--color-advertencia` / `--color-advertencia-fondo` e ícono de pausa. Describe sólo la generación futura; no implica baja, borrado ni cambio de obligaciones existentes.

## Solicitudes idempotentes

| Estado | Texto (ES) | Token de color | Ícono | Por qué |
|---|---|---|---|---|
| ACEPTADA | "Aceptada" | `--color-exito` / `--color-exito-fondo` | check | La solicitud se procesó como una operación nueva y exitosa. |
| RECUPERADA | "Recuperada" | `--color-info` / `--color-info-fondo` | flecha circular (repetir) | **No es exactamente éxito.** Es lo que pasa cuando alguien reenvía una solicitud que ya se había procesado antes (una doble carga de evidencia, un doble clic en "enviar") y el sistema, por ser idempotente, devuelve el resultado que ya existía en vez de duplicarlo. Pintarla de verde haría creer que se creó algo nuevo cuando en realidad no se creó nada. Info azul comunica "no pasó nada nuevo, esto ya existía". |
| RECHAZADA | "Rechazada" | `--color-peligro` / `--color-peligro-fondo` | equis en círculo | La solicitud no se procesó. Aquí sí es una señal negativa real, por eso rojo. |

## Evidencia

| Estado | Texto (ES) | Token de color | Ícono | Por qué |
|---|---|---|---|---|
| COMPLETA | "Completa" | `--color-exito` / `--color-exito-fondo` | check en círculo relleno | Toda la evidencia requerida está cargada. |
| INCOMPLETA | "Incompleta" | `--color-advertencia` / `--color-advertencia-fondo` | triángulo de advertencia | Ámbar, no rojo: falta evidencia por subir, pero no es una falla del sistema ni una acción rechazada — es trabajo pendiente del responsable. |

## Bandera: Vencida

`Vencida` no es un estado del ciclo de vida de un registro — es una **bandera** que se superpone a un estado existente cuando se cruza una fecha límite. Una tarea puede estar `PENDIENTE` y además `Vencida`; nunca sustituye al estado base, se muestra junto a él.

| Bandera | Texto (ES) | Token de color | Ícono | Por qué |
|---|---|---|---|---|
| Vencida | "Vencida" | `--color-peligro` / `--color-peligro-fondo` | reloj con signo de exclamación | Es la única bandera de este set que sí amerita rojo: comunica que se pasó un límite de tiempo y requiere atención inmediata, independientemente del estado base que acompañe. |

### Ejemplo de composición: estado base + bandera

```html
<span class="badge badge--advertencia">
  <svg class="badge__icono" aria-hidden="true"><!-- reloj --></svg>
  Pendiente
</span>
<span class="badge badge--peligro">
  <svg class="badge__icono" aria-hidden="true"><!-- reloj-exclamación --></svg>
  Vencida
</span>
```

Los dos badges van uno junto al otro, nunca uno reemplaza al otro. Ver `componentes.md` para el marcado completo del componente badge.

## UI-G01 — resultado confirmado y recuperación

Aclaración consumidora aprobada durante FRONT-013. ACEPTADA y RECUPERADA se presentan sólo cuando el servidor las devuelve; recuperar conserva el ID y no significa otra creación. GET conserva el resultado persistido y no lo cambia a RECUPERADA porque la persona haya vuelto a consultar.

Un rechazo HTTP no inventa una solicitud persistida RECHAZADA ni un ID. La interfaz comunica el intento rechazado con el mensaje aprobado. La presencia de obligationId confirmado permite mostrar el vínculo; su ausencia no se anuncia como obligación creada. Se conservan los badges, texto e iconos de «Solicitudes idempotentes»; no se agregan estados de dominio.

## UI-G02/G03/G04 — composición consumidora aprobada

El responsable aprobó íntegramente las secciones 2–7 de `docs/traceability/FRONT_014_PROPUESTA_CONSUMIDOR.md` mediante «La apruebo» el 2026-09-29. Esas secciones se incorporan por referencia como contrato operativo específico de estas unidades, exclusivamente para FRONT-014. La composición vive en `/planificacion`, conserva el snapshot confirmado, carga recibida y confirmación motivada con intención protegida. No incorpora bandeja ni asignación automática. Se reutilizan BR-D04/D13, tokens y estados existentes; el servidor decide elegibilidad y autoridad.

## FRONT-017 — composición consumidora aprobada

Adenda 56 incorpora íntegramente FRONT_017_PLAN_DE_IMPLEMENTACION.md, aprobado mediante «Apruebo integramente el plan, recuerda seguir los documentos de diseño para crear las pantallas a como están diseñadas las anteriores». §§3–7 definen BR-D07/D08, los 18 formularios cerrados, etiquetas, mensajes, estados y navegación dentro del detalle de tarea. Se conserva estilo v2, CSS propio/variables/componentes compartidos. Sólo primera aportación; LIMPIO permite aportar, no confirma evidencia. Sin preview/descarga/sustitución/conclusión. Los textos de §6 son catálogo operativo literal; PENDIENTE_CARGA y PENDIENTE_ESCANEO se distinguen. Cada booleano Sí/No sin selección inicial. No hay JSON libre ni biblioteca adicional.

## FRONT-018 — composición consumidora aprobada

Adenda 57 incorpora íntegramente el plan FRONT_018_PLAN_DE_IMPLEMENTACION.md, aprobado mediante «Si apruebo integramente el plan». Sus §§5–7 son contrato operativo literal de UI-E06/E07/E08: versiones paginadas, revisión explícita, faltantes exactos, sustitución con autoridad/motivo y conclusión sin cuerpo. Conserva estilo v2, seis estados, componentes compartidos, variables oficiales, accesibilidad y navegación en el detalle existente. Descarga/preview excluidos. Revisión tiene únicamente la excepción atómica de snapshot/auditoría de Adenda 19; no cambia ejecución ni decisiones anteriores.
## FRONT-019 — composición consumidora aprobada

Adenda 58 incorpora el plan único FRONT_019_PLAN_DE_IMPLEMENTACION.md aprobado mediante «Apruebo el plan». Sus §§4–8 son el contrato operativo de UI-V01..V04: pendientes/supervisión en /validaciones; emisión, sustitución e historia en el detalle existente, tres resultados, fundamento y motivos separados, confirmación y conflicto. BR-D09/M09 resueltas sólo para esta consumidora. Catálogo literal y seis estados en §§7–8; mapas cerrados de requisito, resultado y autoridad con pares/iconos existentes. CSS propio, variables oficiales y base v2 conservados. Proyección validationActions sólo de presentación, servidor reautoriza. Lecturas puras sin efectos; revisión explícita conserva snapshot/auditoría atómicos deduplicados. Descarga/preview excluidos sólo para FRONT-019; BR-API04 global permanece abierta. Sin FRONT-020/TECH-FRONT-005.
