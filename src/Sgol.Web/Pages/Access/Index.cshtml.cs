using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;

namespace Sgol.Web.Pages.Access;

public sealed class IndexModel(
    RazorAntiforgeryBridge antiforgery,
    IDataProtectionProvider protection) : PageModel
{
    private const string LoginPath = "/acceso";
    private const string PasswordPath = "/acceso/cambiar-contrasena";
    private const string EnrollPath = "/acceso/mfa/enrolar";
    private const string VerifyPath = "/acceso/mfa/verificar";
    private const string RecoveryPath = "/acceso/codigos-recuperacion";
    private const string ChangePassword = "CHANGE_PASSWORD";
    private const string EnrollMfa = "ENROLL_MFA";
    private const string VerifyMfa = "VERIFY_MFA";
    private const string Regenerate = "REGENERATE_RECOVERY_CODES";
    private const string Authenticated = "AUTHENTICATED";
    private readonly ITimeLimitedDataProtector flowProtector = protection
        .CreateProtector("SGOL", "FRONT-001", "preauth-step-v1")
        .ToTimeLimitedDataProtector();

    public string Stage { get; private set; } = "login";
    public string? Flow { get; private set; }
    public string? Csrf { get; private set; }
    public string? ErrorTitle { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ManualKey { get; private set; }
    public IReadOnlyList<string> RecoveryCodes { get; private set; } = [];

    public IActionResult OnGet(string? flow)
    {
        NoStore();
        var path = Request.Path.Value;
        if (path == LoginPath)
        {
            Stage = "login";
        }
        else if (!HasPreAuthentication() || !ValidFlow(flow, ExpectedStep(path)))
        {
            return Redirect(LoginPath);
        }
        else
        {
            Stage = path switch
            {
                PasswordPath => "password",
                EnrollPath => "enroll",
                VerifyPath => "verify",
                RecoveryPath => "regenerate",
                _ => "login",
            };
            Flow = flow;
        }
        Csrf = antiforgery.Issue(HttpContext);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        NoStore();
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        var action = form["stepAction"].ToString();
        var flow = form["flow"].ToString();
        var path = Request.Path.Value;
        string? expected = path switch
        {
            PasswordPath when action == "change" => ChangePassword,
            EnrollPath when action == "begin" => EnrollMfa,
            VerifyPath when action is "totp" or "recovery" => VerifyMfa,
            RecoveryPath when action == "confirm" => EnrollMfa,
            RecoveryPath when action == "regenerate" => Regenerate,
            _ => null,
        };
        if (path != LoginPath && (!HasPreAuthentication() || !ValidFlow(flow, expected)))
            return Expired();
        if (path == LoginPath && action != "login") return BadRequest();
        Flow = flow;
        Stage = path switch
        {
            PasswordPath => "password",
            EnrollPath => "enroll",
            VerifyPath => "verify",
            RecoveryPath when action == "regenerate" => "regenerate",
            _ => "login",
        };

        var request = (path, action) switch
        {
            (LoginPath, "login") => new ApiRequest(HttpMethod.Post, "/api/v1/auth/login", ApiResponseShape.Item,
                new { userName = form["userName"].ToString(), password = form["password"].ToString() }),
            (PasswordPath, "change") => new ApiRequest(HttpMethod.Post, "/api/v1/auth/password/change", ApiResponseShape.Item,
                new { currentPassword = form["currentPassword"].ToString(), newPassword = form["newPassword"].ToString() }),
            (EnrollPath, "begin") => new ApiRequest(HttpMethod.Post, "/api/v1/auth/mfa/enroll", ApiResponseShape.Item, new { }),
            (RecoveryPath, "confirm") => new ApiRequest(HttpMethod.Post, "/api/v1/auth/mfa/confirm", ApiResponseShape.Item,
                new { totpCode = form["totpCode"].ToString() }),
            (VerifyPath, "totp") => new ApiRequest(HttpMethod.Post, "/api/v1/auth/mfa/verify", ApiResponseShape.Item,
                new { totpCode = form["totpCode"].ToString() }),
            (VerifyPath, "recovery") => new ApiRequest(HttpMethod.Post, "/api/v1/auth/mfa/verify", ApiResponseShape.Item,
                new { recoveryCode = form["recoveryCode"].ToString() }),
            (RecoveryPath, "regenerate") => new ApiRequest(HttpMethod.Post, "/api/v1/auth/recovery-codes/regenerate", ApiResponseShape.Item,
                new { currentPassword = form["currentPassword"].ToString() }),
            _ => throw new InvalidOperationException("Acción de acceso no reconocida."),
        };

        ApiResponse<JsonElement>? response;
        try
        {
            response = await antiforgery.SendValidatedAsync<JsonElement>(HttpContext, request, cancellationToken);
        }
        catch (ApiProtocolException)
        {
            ErrorTitle = "No se pudo continuar";
            ErrorMessage = "Recarga la página antes de volver a intentarlo.";
            Csrf = antiforgery.Issue(HttpContext);
            return Page();
        }
        if (response is null)
        {
            ErrorTitle = "No se pudo verificar la solicitud";
            ErrorMessage = "Recarga la página antes de volver a enviarla.";
            Csrf = antiforgery.Issue(HttpContext);
            return Page();
        }
        if (!response.IsSuccess)
        {
            SetError(response.ErrorCode);
            Csrf = antiforgery.Issue(HttpContext);
            return Page();
        }

        var data = response.Data;
        if (data.ValueKind != JsonValueKind.Object) throw new ApiProtocolException();
        if (action is "login" or "change" or "totp" or "recovery")
        {
            var next = ReadString(data, "nextStep");
            return next switch
            {
                ChangePassword => Go(PasswordPath, next),
                EnrollMfa => Go(EnrollPath, next),
                VerifyMfa => Go(VerifyPath, next),
                Regenerate => Go(RecoveryPath, next),
                Authenticated => FullSession(),
                _ => throw new ApiProtocolException(),
            };
        }
        if (action == "begin")
        {
            ManualKey = ReadString(data, "manualKey");
            Stage = "enroll-key";
            Csrf = antiforgery.Issue(HttpContext);
            return Page();
        }
        if (action is "confirm" or "regenerate")
        {
            if (ReadString(data, "nextStep") != "RECOVERY_CODES") throw new ApiProtocolException();
            var codes = data.GetProperty("recoveryCodes");
            if (codes.ValueKind != JsonValueKind.Array || codes.GetArrayLength() != 10 ||
                codes.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                throw new ApiProtocolException();
            RecoveryCodes = codes.EnumerateArray().Select(item => item.GetString()!).ToArray();
            Stage = "codes";
            Flow = null;
            return Page();
        }
        throw new ApiProtocolException();
    }

    private RedirectResult Go(string path, string nextStep) =>
        Redirect($"{path}?flow={Uri.EscapeDataString(flowProtector.Protect(nextStep, TimeSpan.FromMinutes(10)))}");

    private PageResult FullSession()
    {
        Stage = "complete";
        Flow = null;
        return Page();
    }

    private PageResult Expired()
    {
        Stage = "expired";
        Flow = null;
        return Page();
    }

    private bool ValidFlow(string? flow, string? expected)
    {
        if (string.IsNullOrWhiteSpace(flow) || expected is null) return false;
        try { return flowProtector.Unprotect(flow) == expected; }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
        catch (Exception exception) when (exception.GetType().Name == "PayloadExpiredException") { return false; }
    }

    private bool HasPreAuthentication() => Request.Cookies.ContainsKey("__Host-SGOL-PreAuth");

    private static string? ExpectedStep(string? path) => path switch
    {
        PasswordPath => ChangePassword,
        EnrollPath => EnrollMfa,
        VerifyPath => VerifyMfa,
        RecoveryPath => Regenerate,
        _ => null,
    };

    private static string ReadString(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ApiProtocolException();

    private void SetError(string? code)
    {
        (ErrorTitle, ErrorMessage) = code switch
        {
            "AUTHENTICATION_FAILED" => ("No se pudo iniciar sesión", "Revisa los datos de acceso e inténtalo de nuevo."),
            "PASSWORD_NO_CUMPLE_POLITICA" => ("La contraseña no cumple los requisitos", "Usa una contraseña nueva de 14 a 128 caracteres."),
            "DATOS_AUTENTICACION_INVALIDOS" => ("Revisa los datos ingresados", "Corrige los campos señalados e inténtalo de nuevo."),
            "CODIGO_MFA_INVALIDO" => ("No se pudo verificar el código", "Revisa el código e inténtalo de nuevo."),
            "DESAFIO_INVALIDO" => ("El paso de acceso venció", "Vuelve a iniciar sesión para continuar."),
            "ACCOUNT_LOCKED" => ("El acceso está bloqueado temporalmente", "Espera antes de volver a intentarlo."),
            "RATE_LIMITED" => ("Demasiadas solicitudes", "Espera antes de volver a intentarlo."),
            "CSRF_INVALID" => ("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla."),
            "MFA_STATE_INCONSISTENT" => ("Se requiere recuperación administrada", "Solicita a Dirección el restablecimiento de MFA."),
            _ => ("No se pudo continuar", "Vuelve a iniciar sesión para continuar."),
        };
        if (code == "DESAFIO_INVALIDO") Stage = "expired";
    }

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
