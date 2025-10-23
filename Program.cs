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
builder.Services.Configure<IntuitOAuthSettings>(builder.Configuration.GetSection("IntuitAuth"));

// ===== Servicios base =====
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("intuit");

// Payroll: DI (una sola vez, sin duplicados)
builder.Services.AddScoped<IPayrollAuthService, PayrollAuthService>();
builder.Services.AddScoped<IPayrollQboApi, PayrollQboApi>();
builder.Services.AddScoped<IQboEmployeeService, QboEmployeeService>();

// Stores/servicios ya existentes en tu app (NO tocados)
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
    options.Conventions.AllowAnonymousToPage("/Auth/PayrollCallback");
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
    PayrollDbContext db
) =>
{
    var q        = http.Request.Query;
    var code     = q["code"].ToString();
    var stateB64 = q["state"].ToString();
    var realmIdQ = q["realmId"].ToString();

    if (string.IsNullOrWhiteSpace(code))
        return Results.BadRequest("Missing code.");

    string returnTo = "/Payroll/Empresas";
    int companyId   = 0;

    try
    {
        if (!string.IsNullOrWhiteSpace(stateB64))
        {
            var stateJson = Encoding.UTF8.GetString(Convert.FromBase64String(stateB64));
            using var doc = JsonDocument.Parse(stateJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("returnTo", out var rt)) returnTo = rt.GetString() ?? returnTo;
            if (root.TryGetProperty("companyId", out var cid)) int.TryParse(cid.ToString(), out companyId);
        }
    }
    catch { /* estado inválido => ignorar */ }

    var redirectUri = cfg["IntuitPayrollAuth:RedirectUri"]
        ?? $"{http.Request.Scheme}://{http.Request.Host}/Auth/PayrollCallback";

    // 1) Intercambia code -> tokens
    var tokens = await auth.ExchangeCodeAsync(code, redirectUri);
    // 2) Realm final
    var realm = tokens.realmId ?? realmIdQ;
    if (string.IsNullOrWhiteSpace(realm))
        return Results.BadRequest("Missing realmId.");

    // 3) Asegura Company por realm
    Company? comp = null;

    if (companyId > 0)
        comp = await db.Companies.FindAsync(companyId);

    if (comp == null)
        comp = await db.Companies.FirstOrDefaultAsync(c => c.QboId == realm);

    if (comp == null)
    {
        comp = new Company
        {
            Name = $"Empresa vinculada {realm}",
            QboId = realm
        };
        db.Companies.Add(comp);
        await db.SaveChangesAsync();
    }

    // 4) Guarda/actualiza tokens con el Id real de la empresa
    await auth.SaveTokensAsync(
        comp.Id,
        realm,
        tokens.accessToken,
        tokens.refreshToken,
        tokens.expiresAtUtc
    );

    // 5) Intentar nombre real desde QBO (tolerante a fallos)
    try
    {
        var name = await auth.TryGetCompanyNameAsync(realm, tokens.accessToken);
     Console.WriteLine($"[Payroll][Callback] realm={realm} name={name}");
        if (!string.IsNullOrWhiteSpace(name)) { comp.Name = name!; await db.SaveChangesAsync(); Console.WriteLine($"[Payroll][DB] Updated CompanyId={comp.Id} Name={comp.Name}"); }
    }
    catch { /* opcional */ }

    // 6) Volver al listado
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
        var allowed = new[] { "/Parametrizador", "/Auth/PayrollCallback", "/Auth/Disconnect", "/Payroll" };
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

if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/qbo/companyinfo/{companyId:int}", async (
        int companyId,
        PayrollDbContext db,
        IHttpClientFactory httpFactory) =>
    {
        try
        {
            var tok = db.PayrollQboTokens
                .Where(t => t.CompanyId == companyId)
                .OrderByDescending(t => t.Id)
                .FirstOrDefault();

            if (tok == null)
                return Results.NotFound($"No hay tokens para CompanyId={companyId}.");

            var client = httpFactory.CreateClient("intuit");
            if (client.BaseAddress == null)
                client.BaseAddress = new Uri("https://quickbooks.api.intuit.com/");

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tok.AccessToken ?? "");

            var url = $"v3/company/{tok.RealmId}/companyinfo/{tok.RealmId}?minorversion=65";
            var resp = await client.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            var header = $"STATUS {(int)resp.StatusCode} {resp.ReasonPhrase}\nURL {client.BaseAddress}{url}\n";
            var mime   = "application/json";

            return Results.Text(header + "\n" + body, mime);
        }
        catch (Exception ex)
        {
            return Results.Text("EXCEPTION: " + ex.ToString(), "text/plain");
        }
    });
}

IvaFacilitador.Payroll.Endpoints.EmployeesApi.Register(app);

app.Run();










