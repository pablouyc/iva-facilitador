using System.Globalization;
using Microsoft.AspNetCore.Localization;
using IvaFacilitador.Services;

var builder = WebApplication.CreateBuilder(args);

// Config
builder.Services.Configure<IntuitOAuthSettings>(builder.Configuration.GetSection("IntuitAuth"));

// MVC/Razor + HttpClient
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

// Stores y servicios
builder.Services.AddSingleton<ICompanyStore, FileCompanyStore>();
builder.Services.AddSingleton<ITokenStore, FileTokenStore>();
builder.Services.AddScoped<IQuickBooksAuth, QuickBooksAuth>();
builder.Services.AddScoped<IQuickBooksApi, QuickBooksApi>(); // ← NUEVO

var app = builder.Build();

// Cultura es-CR
var supportedCultures = new[] { new CultureInfo("es-CR") };
app.UseRequestLocalization(new RequestLocalizationOptions{
    DefaultRequestCulture = new RequestCulture("es-CR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();
