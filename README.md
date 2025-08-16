# IVA Facilitador

Aplicación ASP.NET Core para conectar con QuickBooks y gestionar el flujo de IVA.

Incluye:
- Conexión OAuth con QuickBooks.
- Modal de parametrización rápida tras el callback.
- Wizard de onboarding para tarifas y datos generales.
- Persistencia del perfil de empresa en archivos JSON por `realmId`.
- Servicios stub para catálogo y detección de tarifas de QuickBooks.

## Build
Requiere .NET 8.0 SDK. Instala dependencias y ejecuta `dotnet build` para compilar el proyecto.

Antes de conectar con QuickBooks, define `ClientId`, `ClientSecret` y `RedirectUri` en la sección `IntuitAuth` del archivo `appsettings.json` o mediante variables de entorno.

Si el SDK no está instalado, `dotnet build` fallará con "command not found".
