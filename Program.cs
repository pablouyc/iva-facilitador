using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
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
builder.Services.AddSingleton<ITokenStore, FileTokenStore>();
builder.Services.AddScoped<IQuickBooksAuth, QuickBooksAuth>();
builder.Services.AddScoped<IQuickBooksApi, QuickBooksApi>();

options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
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




