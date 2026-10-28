namespace Sgol.Web.Presentation.Components;

public sealed record ChecklistEvidenceModel(string Definition, string Draft, Guid AssignmentId, bool ReadOnly = false);
