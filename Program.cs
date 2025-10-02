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

app.MapGet("/Auth/PayrollCallback", async (
    HttpContext http,
    IConfiguration cfg,
    IMemoryCache cache,
    IPayrollAuthService auth,
    IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext db
) =>
{
    var q       = http.Request.Query;
    var code    = q["code"].ToString();
    var stateB64= q["state"].ToString();
    var realmId = q["realmId"].ToString();

    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(stateB64))
        return Results.BadRequest("Missing code/state.");

    string returnTo = "/Payroll/Empresas";
    int    companyId = 0;
    string? nonce    = null;

    try
    {
        var stateJson = Encoding.UTF8.GetString(Convert.FromBase64String(stateB64));
        using var doc = JsonDocument.Parse(stateJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("returnTo", out var rt)) returnTo = rt.GetString() ?? returnTo;
        if (root.TryGetProperty("companyId", out var cid)) int.TryParse(cid.ToString(), out companyId);
        if (root.TryGetProperty("nonce", out var n))      nonce = n.GetString();
    }
    catch { /* estado inválido => defaults */ }

    var redirectUri = cfg["IntuitPayrollAuth:RedirectUri"]
        ?? $"{http.Request.Scheme}://{http.Request.Host}/Auth/PayrollCallback";

    var tokens = await auth.ExchangeCodeAsync(code, redirectUri);

    await auth.SaveTokensAsync(
        companyId,
        tokens.realmId ?? realmId,
        tokens.accessToken,
        tokens.refreshToken,
        tokens.expiresAtUtc
    );

    // Completar nombre desde QBO si es posible
    if (companyId != 0 && !string.IsNullOrWhiteSpace(tokens.realmId ?? realmId))
    {
        var comp = await db.Companies.FindAsync(companyId);
        if (comp != null)
        {
            comp.QboId ??= tokens.realmId ?? realmId;
            var name = await auth.TryGetCompanyNameAsync(comp.QboId!, tokens.accessToken);
            if (!string.IsNullOrWhiteSpace(name))
            {
                comp.Name = name!;
                await db.SaveChangesAsync();
            }
        }
    }

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



