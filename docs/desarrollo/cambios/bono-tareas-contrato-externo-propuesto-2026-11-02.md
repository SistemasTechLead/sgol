# Bono de tareas: contrato externo propuesto

Estado: propuesta para un consumidor futuro, **sin endpoint externo implementado**. El consumidor, su autenticación y el contrato definitivo requieren desarrollo posterior. Los handlers de Razor actuales son consultas internas de sesión; no constituyen una API pública ni autorizan integración externa.

## Semántica

Una evaluación corresponde a persona estable, sucursal, semana local de lunes a domingo y versión de reglas. `calificacionPercent` mide cumplimiento ponderado; `bonoTareasPercent` representa exclusivamente el componente de tareas conservado (0, 50 o 100). Ninguno es avance de tareas ni bono total. No redondear para determinar el tramo. No convertir ausencia/incidencia en cero.

La primera semana es 2026-11-02–2026-11-08. Ejecución termina 2026-11-09T00:00:00-06:00; revisión cierra 2026-11-09T12:00:00-06:00. El primer sábado de referencia es 2026-11-14. Fechas y horas provienen del servidor en `America/Mexico_City`.

## Representación propuesta

```json
{
  "personaId": "uuid-estable",
  "sucursalId": "uuid-estable",
  "semanaInicio": "2026-11-02",
  "semanaFin": "2026-11-08",
  "timezone": "America/Mexico_City",
  "executionCutoffAt": "2026-11-09T00:00:00-06:00",
  "reviewCutoffAt": "2026-11-09T12:00:00-06:00",
  "paymentReferenceDate": "2026-11-14",
  "estadoCierre": "CERRADO",
  "estadoCalculo": "CALCULABLE",
  "calificacionPercent": 90,
  "bonoTareasPercent": 50,
  "puntosEvaluables": 50,
  "puntosDescontados": 5,
  "calculatedAt": "2026-11-09T12:00:01-06:00",
  "closedAt": "2026-11-09T12:00:00-06:00",
  "rulesVersion": "tasks-2026-11-02-v1",
  "resultVersion": "hash-opaco"
}
```

Los puntos y porcentajes son decimales; los instantes son ISO 8601 con zona. `closedAt` indica el corte económico efectivo, aunque el cálculo se materialice más tarde al recuperarse de una interrupción. `calculatedAt` indica cuándo se calculó realmente. `resultVersion` identifica el contenido económico, no el momento de cada lectura.

| Caso | Estado de cálculo | Calificación | Bono de tareas |
|---|---|---|---|
| Base válida | CALCULABLE | 0–100 | 0/50/100 |
| Sin trabajo según horario/ausencias y sin base | SIN_TAREAS_EVALUABLES | null | 0 |
| Trabajo según horario/ausencias pero sin base, o historial insuficiente | INCIDENCIA | null | null |
| Semana anterior a vigencia | NO_VIGENTE | null | null |

`estadoCierre` distingue PROVISIONAL/CERRADO independientemente del estado de cálculo. Una incidencia puede estar cerrada sin tener valor pagable. El consumidor debe conservar esa distinción y aplicar sus propias reglas para otros componentes del bono.

## Consultas y acceso futuros

- Lectura individual por persona/semana; lote por sucursal/semana; histórico acotado por fechas. Versionar el contrato (`v1` propuesto) y limitar tamaño del lote.
- Paginación estable por personaId con cursor opaco ligado a sucursal, semana y versión del conjunto. No mezclar revisiones de un lote sin avisar al consumidor.
- Autorización explícita de la integración y por sucursal/personas; no reutilizar cookies personales, cuentas de quiosco ni credenciales incluidas en URL. Definir scopes, revocación y auditoría al implementar el consumidor.
- Enviar únicamente el resumen necesario: no adjuntos, comentarios de evidencia, motivos privados, nombres de archivos ni detalle de tareas. El detalle actual permanece en Supervisión bajo sus permisos.
- `Cache-Control: no-store` para datos personales; ETag/resultVersion pueden soportar validación condicional sin almacenamiento compartido. Un cierre repetido conserva la misma versión y valores.
- Errores propuestos: 400 filtros inválidos; 401 autenticación; 403 ámbito denegado; 404 evaluación no disponible sin revelar existencia ajena; 409 lote/versionado incompatible; 503 dependencia temporalmente indisponible. El cuerpo debe incluir código estable, mensaje y correlación, nunca porcentajes inventados como sustituto del error.

No se implementan escritura externa, pagos, ventas, cálculo del bono total ni reapertura de cierres. Las pruebas de este contrato externo son futuras. Las pruebas ejecutadas corresponden al calculador, persistencia y consultas internas descritas en el plan del velocímetro.
