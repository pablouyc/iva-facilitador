using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;

using IvaFacilitador.Payroll.Services;using System.Linq;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using IvaFacilitador.Services;

var builder = WebApplication.CreateBuilder(args);
// Payroll DB
builder.Services.AddDbContext<IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("PayrollDb")));


builder.Services.AddScoped<IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext>(sp => sp.GetRequiredService<IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext>());
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
    options.Conventions.AllowAnonymousToPage("/Auth/ConnectQboPayroll");
    options.Conventions.AllowAnonymousToPage("/Auth/PayrollCallback");
});

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("intuit");
builder.Services.AddScoped<IPayrollAuthService, PayrollAuthService>();

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
builder.Services.AddDbContext<IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext>(opt =>
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
using (var scope = app.Services.CreateScope()) { var pctx = scope.ServiceProvider.GetRequiredService<IvaFacilitador.Areas.Payroll.BaseDatosPayroll.PayrollDbContext>(); pctx.Database.EnsureCreated(); }
  
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
app.Run();















