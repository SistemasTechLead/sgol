param([string]$BaseUrl = 'https://localhost:7240')
$ErrorActionPreference = 'Stop'
$origin = [Uri]$BaseUrl
if ($origin.Host -ne 'localhost' -or $origin.Scheme -ne 'https') { throw 'Esta prueba solo se ejecuta en el localhost HTTPS.' }
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.UseCookies = $true
$handler.CookieContainer = [System.Net.CookieContainer]::new()
$client = [System.Net.Http.HttpClient]::new($handler)
$client.BaseAddress = $origin
try {
    $response = $client.GetAsync('/acceso').GetAwaiter().GetResult()
    $html = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $form = [System.Collections.Generic.Dictionary[string,string]]::new()
    $form['stepAction'] = 'login'
    $form['userName'] = '__sgol_csrf_regression_nonexistent__'
    $form['password'] = 'Regression!123456789'
    foreach ($attempt in 1..2) {
        $token = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
        if (!$token) { throw 'Falta el token del formulario.' }
        $form['__RequestVerificationToken'] = $token
        $body = [System.Net.Http.FormUrlEncodedContent]::new($form)
        $response = $client.PostAsync('/acceso', $body).GetAwaiter().GetResult()
        $body.Dispose()
        $html = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if ([int]$response.StatusCode -ne 200 -or ![System.Net.WebUtility]::HtmlDecode($html).Contains('No se pudo iniciar sesión')) {
            throw "Fallo en el reintento ${attempt}: HTTP $([int]$response.StatusCode)"
        }
        if (!$handler.CookieContainer.GetCookies($origin)['__Host-SGOL-CSRF']) { throw 'Se perdió la cookie CSRF después del intento fallido.' }
        Write-Output "PASS Reintento $attempt muestra el formulario y conserva la cookie CSRF"
    }
    $form['__RequestVerificationToken'] = 'invalid-token'
    $response = $client.PostAsync('/acceso', [System.Net.Http.FormUrlEncodedContent]::new($form)).GetAwaiter().GetResult()
    if ([int]$response.StatusCode -ne 400) { throw 'El token incorrecto no fue rechazado.' }
    Write-Output 'PASS El token inválido sigue siendo rechazado'
} finally { $client.Dispose() }
