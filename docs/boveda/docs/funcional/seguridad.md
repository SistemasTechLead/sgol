---
categoria: seguridad
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Controles de seguridad implementados

## Identidad y permisos

El acceso personal usa autenticación, MFA y sesiones persistidas. La validación consulta sesión y estado de identidad; revocación y cambios de seguridad impiden usar una sesión inválida. El acceso de pantalla tiene un mecanismo separado. Asesor Externo conserva el código interno ADMINISTRACION.

Los servicios comprueban autorización además de navegación. Las operaciones mutables usan antiforgery y validación de revisión/estado según contrato. No deducir permisos por la presencia de un enlace.

## Secretos y base

Los scripts locales utilizan DPAPI ligado al usuario y ACL restringidas. La cuenta runtime está separada del mantenimiento/migrador; RuntimeDatabaseGuard rechaza privilegios administrativos, propiedad y capacidades incompatibles. Secretos, keyring, certificados privados, respaldos y bases no pertenecen a notas ni Git.

La protección local no impide que administradores o componentes privilegiados inspeccionen el entorno; no aísla frente al administrador de Windows o Docker.

## Contenido y evidencia

Markdown deshabilita HTML, restringe esquemas de enlaces y no carga imágenes remotas. Requisitos y acceso a evidencias se validan en servidor; la captura está vinculada a sesión y asignación. La infraestructura incluye inspección de archivos y almacenamiento privado con controles específicos por circuito.

## Límite

Esta referencia describe mecanismos de código, no una auditoría nueva ni certificación. No convierte hallazgos antiguos en vulnerabilidades abiertas actuales ni acredita toda configuración efectiva del host. Configuración, dependencias y acceso requieren comprobaciones con el alcance de [Calidad](calidad.md).

Relacionados: [Infraestructura](../../mapas/infraestructura.md), [Evidencias](ejecucion-evidencias.md), [Interfaces](interfaces.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [EfHostedAuthenticationService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Identity/EfHostedAuthenticationService.cs)
- [PersonalSession.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Bootstrap/PersonalSession.cs)
- [RuntimeDatabaseGuard.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/RuntimeDatabaseGuard.cs)
- [secret-store.ps1](../../../../scripts/secret-store.ps1)
- [MarkdownContent.cs](../../../../src/Sgol.Web/Presentation/Components/MarkdownContent.cs)

[Mapa de seguridad](../../mapas/seguridad.md) · [Inicio](../../Inicio.md)
