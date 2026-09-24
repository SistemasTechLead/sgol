using System.Security.Claims;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class AccountApiEndpoints
{
    public static IEndpointRouteBuilder MapAccountApi(this IEndpointRouteBuilder endpoints)
    {
        var users = endpoints.MapGroup("/api/v1/users");
        users.MapGet("", HandleListAsync);
        users.MapPost("", HandleCreateAsync);
        users.MapPost("/{userId:guid}/deactivate", HandleDeactivateAsync);
        users.MapPost("/{userId:guid}/reactivate", HandleReactivateAsync);
        users.MapPost("/{userId:guid}/mfa-reset", HandleMfaResetAsync);
        users.MapPost("/{userId:guid}/role-assignments", RoleApiEndpoints.HandleChangeAsync);
        return endpoints;
    }

    public static async Task<IResult> HandleListAsync(
        HttpContext context,
        IAccountAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        try
        {
            var accounts = await service.ListAsync(
                actorUserId,
                GetCorrelationId(context),
                cancellationToken);
            return Results.Ok(new
            {
                data = accounts,
                meta = new { correlationId = context.GetCorrelationId(), count = accounts.Count },
            });
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static async Task<IResult> HandleCreateAsync(
        CreateAccountRequest request,
        HttpContext context,
        IAccountAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!TryGetIdempotencyKey(context, out var idempotencyKey, out var invalidKey))
        {
            return invalidKey;
        }

        try
        {
            var result = await service.CreateAsync(
                new CreateAccountCommand
                {
                    ActorUserId = actorUserId,
                    IdempotencyKey = idempotencyKey,
                    CorrelationId = GetCorrelationId(context),
                    PersonId = request.PersonId,
                    UserName = request.UserName,
                    TemporaryPassword = request.TemporaryPassword,
                },
                cancellationToken);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Created(
                $"/api/v1/users/{result.Account.Id:D}",
                Envelope(context, request.TemporaryPassword is null
                    ? new AccountActivationResponse(result.Account, result.ActivationSecret)
                    : result.Account));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    public static Task<IResult> HandleDeactivateAsync(
        Guid userId,
        AccountReasonRequest request,
        HttpContext context,
        IAccountAdministrationService service,
        CancellationToken cancellationToken) =>
        HandleStatusChangeAsync(
            userId,
            request.Reason,
            temporaryPassword: null,
            reactivate: false,
            context,
            service,
            cancellationToken);

    public static Task<IResult> HandleReactivateAsync(
        Guid userId,
        ReactivateAccountRequest request,
        HttpContext context,
        IAccountAdministrationService service,
        CancellationToken cancellationToken) =>
        HandleStatusChangeAsync(
            userId,
            request.Reason,
            request.TemporaryPassword,
            reactivate: true,
            context,
            service,
            cancellationToken);

    public static async Task<IResult> HandleMfaResetAsync(
        Guid userId,
        ResetMfaRequest request,
        HttpContext context,
        IAccountAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!TryGetIdempotencyKey(context, out var idempotencyKey, out var invalidKey))
        {
            return invalidKey;
        }

        try
        {
            var result = await service.ResetMfaAsync(
                new ResetMfaCommand
                {
                    ActorUserId = actorUserId,
                    IdempotencyKey = idempotencyKey,
                    CorrelationId = GetCorrelationId(context),
                    UserId = userId,
                    Reason = request.Reason,
                    TemporaryPassword = request.TemporaryPassword,
                },
                cancellationToken);
            return Results.Ok(Envelope(context, result.Account));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static async Task<IResult> HandleStatusChangeAsync(
        Guid userId,
        string reason,
        string? temporaryPassword,
        bool reactivate,
        HttpContext context,
        IAccountAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        if (!TryGetIdempotencyKey(context, out var idempotencyKey, out var invalidKey))
        {
            return invalidKey;
        }

        try
        {
            var command = new ChangeAccountStatusCommand
            {
                ActorUserId = actorUserId,
                IdempotencyKey = idempotencyKey,
                CorrelationId = GetCorrelationId(context),
                UserId = userId,
                Reason = reason,
                TemporaryPassword = temporaryPassword,
            };
            var result = reactivate
                ? await service.ReactivateAsync(command, cancellationToken)
                : await service.DeactivateAsync(command, cancellationToken);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(Envelope(context, reactivate && temporaryPassword is null
                ? new AccountActivationResponse(result.Account, result.ActivationSecret)
                : result.Account));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid actorUserId, out IResult denied)
    {
        actorUserId = default;
        if (context.User.Identity?.IsAuthenticated != true)
        {
            denied = Problem(
                context,
                StatusCodes.Status401Unauthorized,
                "AUTENTICACION_REQUERIDA",
                "Se requiere una sesión activa");
            return false;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId))
        {
            denied = Problem(
                context,
                StatusCodes.Status403Forbidden,
                "ACCESO_DENEGADO",
                "La sesión no identifica un actor autorizado");
            return false;
        }

        denied = null!;
        return true;
    }

    private static bool TryGetIdempotencyKey(HttpContext context, out Guid key, out IResult invalid)
    {
        var parsed = IdempotencyKeyHeader.Parse(context.Request);
        key = parsed.Key;
        if (!parsed.IsValid)
        {
            invalid = Problem(
                context,
                StatusCodes.Status400BadRequest,
                parsed.ErrorCode!,
                parsed.Detail!);
            return false;
        }

        invalid = null!;
        return true;
    }

    private static IResult MapException(HttpContext context, Exception exception) => exception switch
    {
        AccountValidationException => Problem(context, 400, "DATOS_CUENTA_INVALIDOS", exception.Message),
        AccountAccessDeniedException => Problem(context, 403, "ACCESO_DENEGADO", "Se requiere PER-USUARIO-ADMIN vigente en LOR-001"),
        AccountPersonNotFoundException => Problem(context, 404, "PERSONA_NO_ENCONTRADA", "No se encontró la persona"),
        AccountPersonOutOfScopeException => Problem(context, 409, "PERSONA_FUERA_DE_ALCANCE", "La persona no está activa en LOR-001"),
        AccountNotFoundException => Problem(context, 404, "CUENTA_NO_ENCONTRADA", "No se encontró la cuenta"),
        AccountConflictException => Problem(context, 409, "CUENTA_DUPLICADA", "La persona o el identificador ya tiene una cuenta"),
        AccountStateConflictException => Problem(context, 409, "ESTADO_CUENTA_SIN_CAMBIO", "El estado solicitado ya es el vigente"),
        AccountIdempotencyConflictException => Problem(context, 409, "IDEMPOTENCY_KEY_CONFLICT", "La clave ya fue usada con otro contenido"),
        _ => throw exception,
    };

    private static Guid GetCorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var correlationId)
            ? correlationId
            : Guid.CreateVersion7();

    private static object Envelope(HttpContext context, object data) => new
    {
        data,
        meta = new { correlationId = context.GetCorrelationId() },
    };

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.GetCorrelationId(),
            });
}

public sealed class CreateAccountRequest
{
    public required Guid PersonId { get; init; }

    public required string UserName { get; init; }

    public string? TemporaryPassword { get; init; }

    public override string ToString() =>
        $"{nameof(CreateAccountRequest)} {{ PersonId = {PersonId}, UserName = {UserName}, " +
        "TemporaryPassword = [REDACTED] }";
}

public sealed record AccountReasonRequest(string Reason);

public sealed class ReactivateAccountRequest
{
    public required string Reason { get; init; }

    public string? TemporaryPassword { get; init; }

    public override string ToString() =>
        $"{nameof(ReactivateAccountRequest)} {{ Reason = {Reason}, TemporaryPassword = [REDACTED] }}";
}

public sealed class ResetMfaRequest
{
    public required string Reason { get; init; }

    public required string TemporaryPassword { get; init; }

    public override string ToString() =>
        $"{nameof(ResetMfaRequest)} {{ Reason = {Reason}, TemporaryPassword = [REDACTED] }}";
}
