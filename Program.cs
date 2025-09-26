using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using System.Linq;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using IvaFacilitador.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== App settings =====
builder.Services.Configure<IntuitOAuthSettings>(builder.Configuration.GetSection("IntuitAuth"));

// ===== MVC / Razor =====
builder.Services.AddRazorPages(options =>
{
    // Por defecto, todo requiere login
    options.Conventions.AuthorizeFolder("/");
    // Permitir páginas de autenticación y callback de Intuit sin login previo
    options.Conventions.AllowAnonymousToPage("/Auth/Login");
    options.Conventions.AllowAnonymousToPage("/Auth/Callback");
});

builder.Services.AddHttpClient();

// ===== Auth por cookies (login propio) =====
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
        options.Cookie.Name = "IVA.Auth";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// ===== Stores y servicios =====
builder.Services.AddSingleton<ICompanyStore, FileCompanyStore>();
builder.Services.AddSingleton<ICompanyProfileStore, FileCompanyProfileStore>();
builder.Services.AddSingleton<ITokenStore, FileTokenStore>();
builder.Services.AddScoped<IQuickBooksAuth, QuickBooksAuth>();
builder.Services.AddScoped<IQuickBooksApi, QuickBooksApi>();

// ===== DBContext con fallback a Data\payroll.db =====
builder.Services.AddDbContext<PayrollDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Payroll");
    if (string.IsNullOrWhiteSpace(cs))
    {
        var dataDir = System.IO.Path.Combine(builder.Environment.ContentRootPath, "Data");
        System.IO.Directory.CreateDirectory(dataDir);
        cs = $"Data Source={System.IO.Path.Combine(dataDir, "payroll.db")}";
    }
    opt.UseSqlite(cs);
});

var app = builder.Build();  
// === Auto-migrate Payroll DB (idempotente) ===
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext>();
    db.Database.Migrate();
}
catch (Exception ex)
{
    app.Logger?.LogError(ex, "Auto-migrate Payroll DB failed");
}
// ===== Cultura es-CR =====
var supportedCultures = new[] { new CultureInfo("es-CR") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("es-CR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ===== Guard: empresa conectada pero sin parametrizar =====
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    // Si existe cookie "must_param_realm"
    if (context.Request.Cookies.TryGetValue("must_param_realm", out var realmId) && !string.IsNullOrEmpty(realmId))
    {
        // Rutas permitidas sin parametrizar
        var allowed = new[]
        {
            "/Parametrizador",
            "/Auth/Callback",
            "/Auth/Disconnect", "/Payroll"};

        bool esEstatico = path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/images", StringComparison.OrdinalIgnoreCase);

        if (!allowed.Any(a => path.StartsWith(a, StringComparison.OrdinalIgnoreCase)) && !esEstatico)
        {
            // Redirigir a Disconnect forzado
            context.Response.Redirect($"/Auth/Disconnect?realmId={realmId}&reason=guard");
            return;
        }
    }

    await next();
});

app.MapRazorPages();
app.MapGet("/Auth/ConnectQbo", (Microsoft.AspNetCore.Http.HttpContext http, Microsoft.Extensions.Configuration.IConfiguration cfg) =>
{
    var companyId = http.Request.Query["companyId"].ToString();
    var returnTo  = http.Request.Query["returnTo"].ToString();

    // PRIORIDAD: IntuitPayrollAuth (RRHH) -> IntuitAuth (IVA) -> defaults
    string env         = cfg["IntuitPayrollAuth:Environment"] ?? System.Environment.GetEnvironmentVariable("IntuitPayrollAuth__Environment")
                       ?? cfg["IntuitAuth:Environment"]       ?? System.Environment.GetEnvironmentVariable("IntuitAuth__Environment")       ?? "sandbox";
    string clientId    = cfg["IntuitPayrollAuth:ClientId"]    ?? System.Environment.GetEnvironmentVariable("IntuitPayrollAuth__ClientId")
                       ?? cfg["IntuitAuth:ClientId"]          ?? System.Environment.GetEnvironmentVariable("IntuitAuth__ClientId")          ?? "";
    string redirectUri = cfg["IntuitPayrollAuth:RedirectUri"] ?? System.Environment.GetEnvironmentVariable("IntuitPayrollAuth__RedirectUri")
                       ?? cfg["IntuitAuth:RedirectUri"]       ?? System.Environment.GetEnvironmentVariable("IntuitAuth__RedirectUri")       ?? "";
    string scopes      = cfg["IntuitPayrollAuth:Scopes"]      ?? System.Environment.GetEnvironmentVariable("IntuitPayrollAuth__Scopes")
                       ?? cfg["IntuitAuth:Scopes"]            ?? System.Environment.GetEnvironmentVariable("IntuitAuth__Scopes")            ?? "com.intuit.quickbooks.accounting";

    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
        return Results.BadRequest("QBO clientId/redirectUri faltantes (IntuitPayrollAuth/IntuitAuth).");

    var stateObj = new { companyId = companyId, returnTo = string.IsNullOrWhiteSpace(returnTo) ? null : returnTo };
    var stateJson = JsonSerializer.Serialize(stateObj);
    var stateB64  = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));

    string authBase = (env?.ToLowerInvariant() == "production")
        ? "https://appcenter.intuit.com/connect/oauth2"
        : "https://sandbox.appcenter.intuit.com/connect/oauth2";

    string url =
        authBase
        + "?client_id="    + Uri.EscapeDataString(clientId)
        + "&response_type=code"
        + "&scope="        + Uri.EscapeDataString(scopes)
        + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
        + "&state="        + Uri.EscapeDataString(stateB64);

    return Results.Redirect(url);
}).AllowAnonymous();app.MapGet("/Auth/PayrollCallback", (Microsoft.AspNetCore.Http.HttpContext http) =>
{
    var qs = http.Request.QueryString.HasValue ? http.Request.QueryString.Value : "";
    return Results.Redirect("/Auth/Callback" + qs);
}).AllowAnonymous();
app.MapGet("/Auth/PayrollReturn", (Microsoft.AspNetCore.Http.HttpContext http) =>
{
    var state = http.Request.Query["state"].ToString();
    var fallback = "/Payroll/Empresas";
    if (string.IsNullOrWhiteSpace(state))
        return Results.Redirect(fallback);

    try
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(state));
        using var doc = JsonDocument.Parse(json);
        var returnTo = doc.RootElement.TryGetProperty("returnTo", out var rt) ? rt.GetString() : null;
        if (string.IsNullOrWhiteSpace(returnTo)) return Results.Redirect(fallback);
        return Results.Redirect(returnTo!);
    }
    catch { return Results.Redirect(fallback); }
}).AllowAnonymous();
app.Run();






