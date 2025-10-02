using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;
using IvaFacilitador.Payroll.Services;
using IvaFacilitador.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== Servicios base =====
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("intuit");
builder.Services.AddScoped<IPayrollAuthService, PayrollAuthService>();

// Stores/servicios ya existentes en tu app
builder.Services.AddSingleton<ICompanyStore, FileCompanyStore>();
builder.Services.AddSingleton<ICompanyProfileStore, FileCompanyProfileStore>();
builder.Services.AddSingleton<ITokenStore, FileTokenStore>();
builder.Services.AddScoped<IQuickBooksAuth, QuickBooksAuth>();
builder.Services.AddScoped<IQuickBooksApi, QuickBooksApi>();

// ===== Razor + Auth (las páginas requieren login; se exceptúan las de Auth) =====
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Auth/Login");
    options.Conventions.AllowAnonymousToPage("/Auth/Callback");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
        options.Cookie.Name = "IVA.Auth";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// ===== DbContext de Payroll (único) con fallback a Data\payroll.db =====
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

// ===== Endpoint: Intuit Payroll OAuth callback =====
app.MapGet("/Auth/PayrollCallback", async (
    HttpContext http,
    IConfiguration cfg,
    IMemoryCache cache,
    IPayrollAuthService auth,
    PayrollDbContext db
) =>
{
    var q       = http.Request.Query;
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
    catch { /* estado inválido => seguir con defaults */ }

    var redirectUri = cfg["IntuitPayrollAuth:RedirectUri"]
        ?? $"{http.Request.Scheme}://{http.Request.Host}/Auth/PayrollCallback";

    // Intercambia code -> tokens con el servicio de RRHH
    var tokens = await auth.ExchangeCodeAsync(code, redirectUri);

    // Realm final
    var realm = tokens.realmId ?? realmId;
    if (string.IsNullOrWhiteSpace(realm))
        return Results.BadRequest("Missing realmId.");

    // Asegura Company por realm
    var comp = db.Companies.FirstOrDefault(c => c.QboId == realm);
    if (comp == null)
    {
        comp = new Company { Name = $"Empresa vinculada {realm}", QboId = realm };
        db.Companies.Add(comp);
        await db.SaveChangesAsync();
    }

    // Guarda/actualiza tokens
    await auth.SaveTokensAsync(comp.Id, realm, tokens.accessToken, tokens.refreshToken, tokens.expiresAtUtc);

    // Vuelve al listado
    return Results.Redirect(returnTo);
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

    if (context.Request.Cookies.TryGetValue("must_param_realm", out var realmId) && !string.IsNullOrEmpty(realmId))
    {
        var allowed = new[] { "/Parametrizador", "/Auth/Callback", "/Auth/Disconnect", "/Payroll" };
        bool esEstatico = path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith("/images", StringComparison.OrdinalIgnoreCase);

        if (!allowed.Any(a => path.StartsWith(a, StringComparison.OrdinalIgnoreCase)) && !esEstatico)
        {
            context.Response.Redirect($"/Auth/Disconnect?realmId={realmId}&reason=guard");
            return;
        }
    }

    await next();
});

app.MapRazorPages();

// Migraciones en arranque
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PayrollDbContext>();
    db.Database.Migrate();
}

app.Run();
