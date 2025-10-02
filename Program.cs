using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Payroll.Services;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using System.Linq;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using IvaFacilitador.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IPayrollAuthService, PayrollAuthService>();
builder.Services.AddHttpClient("intuit");
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IvaFacilitador.Payroll.Services.IPayrollAuthService, IvaFacilitador.Payroll.Services.PayrollAuthService>();
builder.Services.AddDbContext<IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext>(options =>
{
    // Ajusta el proveedor si tu proyecto no usa Sqlite:
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

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
app.MapGet("/Auth/PayrollCallback", async (
    HttpContext http,
    IConfiguration cfg,
    IMemoryCache cache,
    IPayrollAuthService auth
) =>
{
    var q = http.Request.Query;
    var code    = q["code"].ToString();
    var stateB64= q["state"].ToString();
    var realmId = q["realmId"].ToString();

    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(stateB64))
        return Results.BadRequest("Missing code/state.");

    string returnTo = "/Payroll/Empresas";
    string? nonce = null;

    try
    {
        var stateJson = Encoding.UTF8.GetString(Convert.FromBase64String(stateB64));
        using var doc = JsonDocument.Parse(stateJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("returnTo", out var rt)) returnTo = rt.GetString() ?? returnTo;
        if (root.TryGetProperty("nonce", out var n))     nonce    = n.GetString();
    }
    catch { /* estado inválido => seguimos con defaults */ }

    var redirectUri = cfg["IntuitPayrollAuth:RedirectUri"]
        ?? $"{http.Request.Scheme}://{http.Request.Host}/Auth/PayrollCallback";

    var tokens = await auth.ExchangeCodeAsync(code, redirectUri);

    var key = $"payroll:auth:{nonce ?? Guid.NewGuid().ToString("N")}";
    cache.Set(key, new {
        tokens.accessToken,
        tokens.refreshToken,
        tokens.expiresAtUtc,
        realmId = tokens.realmId ?? realmId
    }, TimeSpan.FromMinutes(15));

    var next = $"{returnTo}?wizard=empresas&nonce={Uri.EscapeDataString(nonce ?? string.Empty)}";
    return Results.Redirect(next);
});

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
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext>();
    db.Database.Migrate();
}
app.Run();







