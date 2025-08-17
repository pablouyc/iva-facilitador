using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

using IvaFacilitador.Data;
using IvaFacilitador.Models;   // Para CompanyConnection en import-companies
using IvaFacilitador.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== App settings =====
builder.Services.Configure<IntuitOAuthSettings>(builder.Configuration.GetSection("IntuitAuth"));

// ===== MVC / Razor =====
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Auth/Login");
    options.Conventions.AllowAnonymousToPage("/Auth/Callback");
});

builder.Services.AddHttpClient();

// ===== Auth por cookies =====
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
var connStr = builder.Configuration["POSTGRES_URL"]
    ?? "Host=localhost;Database=iva;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connStr));
builder.Services.AddScoped<ICompanyStore, CompanyStoreEf>();
builder.Services.AddScoped<ITokenStore, EfTokenStore>();
builder.Services.AddScoped<IQuickBooksAuth, QuickBooksAuth>();
builder.Services.AddScoped<IQuickBooksApi, QuickBooksApi>();
builder.Services.AddScoped<ICompanyProfileStore, CompanyProfileStoreEf>();
builder.Services.AddSingleton<IQuickBooksTariffDetector, QuickBooksTariffDetector>();
builder.Services.AddSingleton<IQuickBooksCatalog, QuickBooksCatalog>();
builder.Services.AddScoped<ParametrizacionRepository>();

var app = builder.Build();

// ===== Importar empresas desde archivo a PostgreSQL =====
if (args.Contains("import-companies"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var path = Path.Combine(app.Environment.ContentRootPath, "App_Data", "companies.json");
    if (File.Exists(path))
    {
        var json = File.ReadAllText(path);
        var companies = JsonSerializer.Deserialize<List<CompanyConnection>>(json) ?? new();
        foreach (var c in companies)
        {
            if (!db.CompanyConnections.Any(x => x.RealmId == c.RealmId))
            {
                db.CompanyConnections.Add(c);
            }
        }
        db.SaveChanges();
        Console.WriteLine($"Imported {companies.Count} companies.");
    }
    else
    {
        Console.WriteLine("No App_Data/companies.json file found.");
    }
    return;
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

app.MapRazorPages();
app.Run();
