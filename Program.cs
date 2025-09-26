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
        var allowed = new[] {"/Parametrizador",
            "/Auth/Callback",
            "/Auth/Disconnect", "/Payroll", "/Auth/ConnectQboPayroll", "/Auth/PayrollCallback"};

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
app.MapGet("/Auth/ConnectQboPayroll", (Microsoft.AspNetCore.Http.HttpContext http, Microsoft.Extensions.Configuration.IConfiguration cfg) =>
{
    string Env(string key)
    {
        return cfg[$"IntuitPayrollAuth:{key}"]
            ?? System.Environment.GetEnvironmentVariable($"IntuitPayrollAuth__{key}")
            ?? cfg[$"IntuitAuth:{key}"]                                // fallback (por si faltara alguno)
            ?? System.Environment.GetEnvironmentVariable($"IntuitAuth__{key}");
    }

    var companyId = http.Request.Query["companyId"].ToString();
    var returnTo  = http.Request.Query["returnTo"].ToString();

    var stateObj  = new { companyId = companyId, returnTo = returnTo };
    var stateJson = System.Text.Json.JsonSerializer.Serialize(stateObj);
    var state     = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(stateJson));

    var clientId    = Env("ClientId") ?? "";
    var redirectUri = Env("RedirectUri") ?? "";
    var scopes      = Env("Scopes") ?? "com.intuit.quickbooks.accounting";

    var authorizeUrl = "https://appcenter.intuit.com/connect/oauth2"
        + "?client_id="    + System.Uri.EscapeDataString(clientId)
        + "&response_type=code"
        + "&scope="        + System.Uri.EscapeDataString(scopes)
        + "&redirect_uri=" + System.Uri.EscapeDataString(redirectUri)
        + "&state="        + System.Uri.EscapeDataString(state);

    return Results.Redirect(authorizeUrl);
}).AllowAnonymous();
app.Run();



