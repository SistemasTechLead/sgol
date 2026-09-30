# SGOL — Adaptación del diseño renovado

Fecha: 2026-09-30. Plan completo aprobado mediante «Si apruebo el plan completo»: `DISENO_RENOVADO_PLAN_DE_IMPLEMENTACION.md`. Referencia documental aceptada: §§3–9 y Adenda 55 mediante «Apruebo las secciones y la adenda».

Estado: implementación en curso, local. Sin autorización de publicación, merge ni despliegue. FRONT-017..020 no iniciadas; evidencia anterior, incluida FRONT-016, aceptada sin repetir sus gates de aprobación.

## Grupos y evidencia

| Grupo | Referencia documentada | Diseño implementado / validación |
|---|---|---|
| Base compartida | Aprobada | Implementada localmente; build Release 0 errores/advertencias, arquitectura de diseño 9/9, render/sesión 33/33; revisión visual transversal pendiente del cierre del hito |
| Acceso, entrada y sucursal | Aprobada | Código adaptado; build Release PASS; validación visual enfocada en curso |
| Personas y accesos | Aprobada | Código adaptado; build Release PASS; validación visual enfocada en curso |
| Configuración | Aprobada | Pendiente |
| Fechas y alta manual | Aprobada | Pendiente |
| Asignaciones, carga y plan | Aprobada | Pendiente |
| Mi trabajo | Aprobada | Pendiente |

## Entorno y límites

Preflight: Fuentes sin cambios; árbol contiene únicamente el plan preparado antes de aprobación. La resolución predeterminada no encuentra 10.0.400. Se utilizará la instalación aislada explícitamente documentada por FRONT-016 (`C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe`) sin cambiar global.json ni instalar herramientas. Cada resultado se registra después de ejecutar; no se acredita el SDK 10.0.401 como sustituto.

## Base compartida

Variables productivas sincronizadas con el bloque root oficial; CSS consumidor usa primitivas de borde, foco, área mínima y esqueleto. Superficie de trabajo cálida y encabezado/navegación blancos; panel, cabecera/filtros, badges agrupados y botón textual disponibles en la misma hoja compartida. Eliminado mínimo universal de tabla móvil. Navegación conserva dialog nativo, su botón de cierre recibe foco inicial. Sin cambios de sesión, permisos, formularios de negocio ni dependencias.

Comandos ejecutados con SDK aislado 10.0.400 en PATH: `dotnet build --no-restore --configuration Release` (PASS, 0 errores/advertencias); arquitectura filtrada a `InterfaceDesignRulesTests` (9/9); unitarias filtradas a `ComponentRenderTests|TechFront003Tests` (33/33). Sin restore. Foco/contraste/área/reflow renderizados pendientes de revisión transversal del mismo hito, no acreditados por estas pruebas.
