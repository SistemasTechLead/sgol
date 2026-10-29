using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Presentation.MyWork;

public sealed record ProcessActionForm(ProcessRunView View, Guid PersonId, string Handler,
    Guid? AssignmentId = null, string? Conclusion = null, string? Differences = null,
    Guid? Key = null, bool Declared = false, string ChecklistDraftJson = "null", string? ItemQuantity = null, string? CapacityResolution = null, string? ItemConflict = null, string? CapacityFingerprint = null, bool UsePreservedWork = false, bool CanForce = true, bool CanSearch = true, Guid? RetainedCaptureSessionId = null, string? RetainedPhotographIds = null);
