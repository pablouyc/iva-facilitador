using System.Globalization;
using IvaFacilitador.Data;
using IvaFacilitador.Services;
using IvaFacilitador.Utils;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db"));

builder.Services.AddDataProtection();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.Cookie.Name = "IVA.Auth";
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IQboAuthService, QboAuthService>();
builder.Services.AddScoped<IQboCompanyInfoService, QboCompanyInfoService>();
builder.Services.AddScoped<ISessionPendingCompanyService, SessionPendingCompanyService>();
builder.Services.AddScoped<ICryptoProtector, CryptoProtector>();

var app = builder.Build();

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

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

public partial class Program { }
