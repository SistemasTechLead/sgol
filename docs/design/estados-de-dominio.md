# estados-de-dominio.md — SGOL / Loretta Zapatería

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
