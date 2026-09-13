# Frontera del restore aislado antes de HU-035

`TECH-OPS-001` comprueba mecanismos, no recuperación funcional. El entorno de verificación debe ser nuevo, privado, vacío y contener sólo datos sintéticos. Nunca se usa producción ni la única copia disponible.

## Evidencia técnica aceptable

1. digest OCI y manifiesto renderizado;
2. backup cifrado + manifiesto `COMPLETE` + SHA-256 revalidado;
3. restore sin `--clean` a PostgreSQL vacío;
4. migración esperada, `pg_restore --list`, tablas estructuralmente accesibles, constraints validadas y key ring consultable;
5. réplica S3 con hashes iguales y manifiesto privado;
6. duración, timestamps UTC, versiones de herramientas y códigos estables sin secretos.

## Lo que no se ejecuta aquí

No se comparan antes/después IDs, vínculos, versiones, conteos funcionales, evidencia ni auditoría. Tampoco se declara RPO ≤1 h o RTO ≤4 h. Esa reconciliación, su reporte de diferencias y el simulacro completo pertenecen exclusivamente a `HU-035`/`CA-035`.

Una diferencia técnica se conserva como fallo. No se borra, recrea, compensa ni modifica información para ocultarla.
