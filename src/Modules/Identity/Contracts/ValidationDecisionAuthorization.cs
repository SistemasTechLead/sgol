
namespace Sgol.Identity.Contracts;

public static class ValidationDecisionAuthorization
{
    public static string? Issue(string actorRole, string responsibleRole, string executorRole, string validatorRole, bool samePerson)
    {
        if (samePerson) return RoleHierarchy.CanSelfValidateAsDirection(actorRole, true) ? "AUTOVALIDACION_DIRECCION" : null;
        if (responsibleRole != executorRole) return null;
        if (RoleHierarchy.CanIssueValidationOrdinarily(actorRole, responsibleRole, executorRole, validatorRole, false)) return "ORDINARIA";
        return RoleHierarchy.CanEscalateValidation(actorRole, responsibleRole, validatorRole, false) ? "ESCALAMIENTO" : null;
    }

    public static string? Replace(string actorRole, string responsibleRole, string originalRole, bool sameValidator, bool sameResponsible)
    {
        if (RoleHierarchy.CanReplaceValidationAsOriginal(actorRole, responsibleRole, originalRole, sameValidator, sameResponsible)) return "SUSTITUCION_ORIGINAL";
        return RoleHierarchy.CanReplaceValidationAsSuperior(actorRole, responsibleRole, originalRole, sameResponsible) ? "SUSTITUCION_SUPERIOR" : null;
    }
}
