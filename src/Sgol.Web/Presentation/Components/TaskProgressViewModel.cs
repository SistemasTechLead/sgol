using Sgol.Planning.Contracts;

namespace Sgol.Web.Presentation.Components;

public sealed record TaskProgressViewModel(int Concluded, int Total, string Label)
{
    public int Percent => Total == 0 ? 0 : (int)(100L * Concluded / Total);
    public string Color => Percent >= 80 ? "exito" : Percent >= 50 ? "advertencia" : "peligro";
    public string Description => Total == 0 ? "Sin tareas asignadas" : $"{Concluded} de {Total} concluidas";

    public static TaskProgressViewModel FromAssignments(IEnumerable<OperationalAssignmentDetails> tasks, string label)
    {
        var assigned = tasks.ToArray();
        return new(assigned.Count(t => t.ReviewStatus == "CONCLUIDA"), assigned.Length, label);
    }
}
