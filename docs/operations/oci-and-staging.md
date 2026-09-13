# Imagen OCI y staging portable de TECH-OPS-001

## Alcance

La imagen contiene Web, Worker y Operations. No contiene secretos, datos, SDK .NET ni buildpack. `deploy/staging/compose.yaml` es el manifiesto normativo; DigitalOcean u otra plataforma sólo puede traducir sus procesos, health checks, configuración y referencias externas sin cambiar comandos.

## Construcción verificable

El constructor materializa un contexto temporal ordinario y explícitamente acotado antes de invocar BuildKit. Esto evita que puntos de análisis del filesystem del checkout se interpreten como solicitudes de archivo inválidas, sin modificar esos archivos ni incluir `Fuentes/`, pruebas, documentación, salidas de compilación o material secreto. La etapa SDK corre en `BUILDPLATFORM` y publica ensamblados framework-dependent sin apphost nativo; el runtime y sus herramientas permanecen en la plataforma objetivo `linux/amd64`, evitando ejecutar MSBuild bajo emulación en hosts ARM64 sin cambiar el artefacto objetivo. Ejecuta:

```powershell
scripts/operations/build-tech-ops-image.ps1
```

El script construye y carga `sgol:tech-ops-local`, comprueba `User=1654:1654`, único puerto `8080/tcp`, health check local, etiquetas OCI, Worker, `pg_dump` y `age`. Si el árbol tiene cambios, sufija la versión con `-dirty` y fija `com.sgol.source.dirty=true`; el pipeline exige un checkout limpio y fija `false`. También devuelve `ImageRef=sha256:<image-id>` y una ruta de evidencia temporal fuera del repositorio. Ese identificador local es admisible sólo para el gate técnico; staging real exige `<registry/repository>@sha256:<digest>`. No se publica ni firma sin autorización separada.

En la misma ejecución, BuildKit genera un layout OCI no publicado con attestations `--sbom=true` y `--provenance=mode=max`, además de `build-metadata.json`, `image-inspect.json` e `image.iid`. El pipeline repite este contrato sobre un checkout Linux normal.

## Render de staging

Inyecta valores sintéticos o secretos efímeros desde el entorno y ejecuta:

```powershell
docker compose --env-file deploy/staging/staging.env -f deploy/staging/compose.yaml config --quiet
```

No copies `secrets.example` a un archivo con valores dentro del repositorio. El operador registra fuera de Git el mapeo de cada nombre al secret store. Para un gate sintético puede apuntar `SGOL_ENV_FILE` a un archivo absoluto fuera del checkout; staging conserva `staging.env`. Web usa `/health/live` para vida y `/health/ready` para PostgreSQL, S3 y lectura/escritura criptográfica del key ring; cualquier excepción no cancelada responde sólo `unavailable/503`. Worker y jobs no publican HTTP.

Para preparar el gate integral local sin reutilizar infraestructura ni secretos, se ejecuta una sola vez:

```powershell
scripts/operations/new-tech-ops-synthetic-environment.ps1 -ImageRef 'sha256:<image-id-local>'
```

El aprovisionador rechaza contenedores o red preexistentes con los nombres reservados. Crea un PostgreSQL sintético con bases separadas `sgol_primary` y `sgol_restore`, dos almacenamientos S3-compatible y cinco buckets: cuarentena/limpio de origen, sus dos destinos de réplica y el bucket de respaldos/manifiestos. Genera cuatro credenciales operativas distintas, un certificado PFX efímero y una identidad `age`; las identidades administrativas temporales sólo crean los buckets y se retiran de la configuración antes de finalizar. También siembra un objeto sintético con SHA-256/tamaño/tipo y comprueba que la identidad fuente de réplica no puede escribir.

`runtime.env` y `environment-summary.json` se escriben en un directorio temporal nuevo fuera del repositorio. El primero contiene secretos efímeros y no debe copiarse, versionarse ni compartirse. El segundo no contiene secretos y entrega las tres rutas/URI necesarias para que el desarrollador ejecute el comando final de `postgresql-portable-backup.md`. Los contenedores permanecen activos deliberadamente; su eliminación requiere una acción posterior explícita y nunca forma parte del verificador.

## Gate integral en x86-64 nativo

La verificación contractual de la imagen `linux/amd64` debe ejecutarse en un host x86-64 nativo. El orquestador es independiente del proveedor y acepta tanto procesadores Intel como AMD:

```powershell
scripts/operations/invoke-tech-ops-amd64-gate.ps1 `
    -ImageRef 'sha256:<image-id-local>' `
    -GateRoot '<directorio-nuevo-fuera-del-repositorio>'
```

El script aprovisiona únicamente datos y credenciales sintéticos, ejecuta el gate externo completo y separa `runtime-private` y `evidence-private` de `evidence-public`. Sólo esta última puede conservarse como artefacto de CI; no contiene credenciales, nombres/URI de buckets, manifiestos de objetos ni TRX detallados. En ARM64 el script falla antes de invocar Docker con `AMD64_HOST_REQUIRED`. El mismo comando puede migrarse en el futuro desde el runner de CI a un equipo físico AMD x86-64 sin cambiar el contrato ni presentar emulación como evidencia equivalente.

## Liberación y rollback

1. Verificar backup reciente y réplica.
2. Ejecutar `migrate --expected-migration` como proceso único expand-only.
3. Iniciar Web con el digest nuevo y esperar readiness.
4. Iniciar Worker no enrutable.
5. Registrar revisión, digest, migración, actor técnico y resultado.

Rollback sólo cambia al digest anterior compatible. Nunca revierte destructivamente la migración. Firma y despliegue real son gates externos no autorizados por `TECH-OPS-001`.
