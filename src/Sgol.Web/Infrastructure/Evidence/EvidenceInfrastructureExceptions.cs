namespace Sgol.Web.Infrastructure.Evidence;

public abstract class EvidenceInfrastructureException(string safeMessage, Exception? innerException = null)
    : Exception(safeMessage, innerException);

public sealed class EvidenceObjectNotFoundException()
    : EvidenceInfrastructureException("The evidence object was not found.");

public sealed class EvidenceObjectAlreadyExistsException()
    : EvidenceInfrastructureException("The evidence object already exists.");

public sealed class EvidenceObjectIntegrityException()
    : EvidenceInfrastructureException("The evidence object failed its integrity check.");

public sealed class EvidenceStorageUnavailableException()
    : EvidenceInfrastructureException("Private evidence storage is unavailable.");
