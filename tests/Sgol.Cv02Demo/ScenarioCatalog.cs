namespace Sgol.Cv02Demo;

internal sealed record DemoScenario(
    string Id,
    string Page,
    string Title,
    string Coverage,
    string Expected);

internal static class ScenarioCatalog
{
    public static IReadOnlyList<DemoScenario> All { get; } =
    [
        new("S01", "calendario", "Publicar configuración versionada de TAR-0005", "HU-008, HU-009, HU-011, HU-012, HU-017; CA/CP-012-P y 017-P", "Una versión vigente y su historia aplicable."),
        new("S02", "recurrencias", "Recuperar solicitud y obligación manuales", "HU-014, HU-015, HU-034; CA/CP-014-P/N y 015-P/N", "La repetición conserva las mismas identidades."),
        new("S03", "recurrencias", "Generar ventanas laborables", "HU-013; CA-013, CP-013-P", "Exactamente 12:00 y 17:00 de America/Mexico_City."),
        new("S04", "calendario", "Omitir un día inhábil", "HU-013; CA-013, CP-013-N", "Sin adelanto, traslado ni hechos funcionales."),
        new("S05", "plan-semanal", "Explicar elegibilidad y asignar", "HU-016, HU-018; CA/CP-016-P/N y 018-P", "Ranking y ganador deterministas."),
        new("S06", "plan-semanal", "Conservar ausencia de candidato", "HU-016, HU-018; CA-016, CP-016-N", "Sin asignación ficticia."),
        new("S07", "plan-semanal", "Corregir asignación con historia", "HU-019; CA-019, CP-019-P", "Anterior sustituida, nueva vigente y motivo."),
        new("S08", "plan-semanal", "Publicar un plan incremental", "HU-020, HU-021; CA/CP-020-P/N y 021-P", "Un plan; V1 y V2 acumulada."),
        new("S09", "recurrencias", "Repetir la cadena completa", "HU-013 a HU-021 y HU-034 aplicable", "Sin duplicar hechos funcionales."),
        new("S10", "plan-semanal", "Incorporar obligación tardía", "HU-015, HU-020, HU-021; CP-021-P", "V2 del plan conserva V1 histórica."),
        new("S11", "plan-semanal", "Rechazar mutación fuera de jerarquía", "HU-019, HU-021; CP-019-N y CP-021-N", "Rechazo sin efectos parciales."),
        new("S12", "recurrencias", "Recuperar lote después de caída parcial", "HU-013, HU-034; TEC-JOB-001", "Completa faltantes sin duplicar confirmados."),
        new("S13", "plan-semanal", "Confirmar límites del corte", "Límites HU-013, HU-020 y HU-021", "Sin publicación automática, conclusión, evidencia, validación ni TAR-0026."),
    ];

    public static DemoScenario Require(string id) =>
        All.SingleOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal)) ??
        throw new DemoSafetyException("CV02_SCENARIO_FAILED");

    public static IReadOnlyList<DemoScenario> ForPage(string page) =>
        All.Where(item => string.Equals(item.Page, page, StringComparison.Ordinal)).ToArray();
}
