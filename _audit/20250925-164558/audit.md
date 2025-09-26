# IVA Facilitador · Auditoría (20250925-164558)

Ruta base: **C:\IvaFacilitador_ProdPack**
## Estructura (resumen)
### C:\IvaFacilitador_ProdPack\Areas\Payroll
 - Areas\Payroll\_ViewImports.cshtml
 - Areas\Payroll\Api\PayrollApi.cs
 - Areas\Payroll\BaseDatosPayroll\PayrollDbContext.cs
 - Areas\Payroll\BaseDatosPayroll\Migraciones\20250921074831_InitPayroll.cs
 - Areas\Payroll\BaseDatosPayroll\Migraciones\20250921074831_InitPayroll.Designer.cs
 - Areas\Payroll\BaseDatosPayroll\Migraciones\PayrollDbContextModelSnapshot.cs
 - Areas\Payroll\BaseDatosPayroll\Plantillas\Colaboradores.csv
 - Areas\Payroll\BaseDatosPayroll\Plantillas\Empresa.csv
 - Areas\Payroll\BaseDatosPayroll\Plantillas\Eventos.csv
 - Areas\Payroll\Components\_Grid.cshtml
 - Areas\Payroll\Components\_Kpis.cshtml
 - Areas\Payroll\Components\_Modal.cshtml
 - Areas\Payroll\Components\_Topbar.cshtml
 - Areas\Payroll\Components\_Topbar.cshtml.bak
 - Areas\Payroll\Components\_Topbar.cshtml.bak-20250924112518
 - Areas\Payroll\Pages\_ViewImports.cshtml
 - Areas\Payroll\Pages\_ViewStart.cshtml
 - Areas\Payroll\Pages\Index.cshtml
 - Areas\Payroll\Pages\Approvals\Index.cshtml
 - Areas\Payroll\Pages\Colaboradores\Index.cshtml
 - Areas\Payroll\Pages\Deducciones\Index.cshtml
 - Areas\Payroll\Pages\Deducciones\Index.cshtml.bak
 - Areas\Payroll\Pages\Deducciones\Index.cshtml.cs
 - Areas\Payroll\Pages\Deducciones\Index.cshtml.cs.bak
 - Areas\Payroll\Pages\Employees\Index.cshtml
 - Areas\Payroll\Pages\Empresas\Index.cshtml
 - Areas\Payroll\Pages\Extras\Index.cshtml
 - Areas\Payroll\Pages\Extras\Index.cshtml.bak
 - Areas\Payroll\Pages\Extras\Index.cshtml.cs
 - Areas\Payroll\Pages\Extras\Index.cshtml.cs.bak
 - Areas\Payroll\Pages\Insights\Index.cshtml
 - Areas\Payroll\Pages\Movements\Index.cshtml
 - Areas\Payroll\Pages\Onboarding\Index.cshtml
 - Areas\Payroll\Pages\PlanillaTotal\Index.cshtml
 - Areas\Payroll\Pages\PlanillaTotal\Index.cshtml.bak.20250924002510
 - Areas\Payroll\Pages\PlanillaTotal\Index.cshtml.cs
 - Areas\Payroll\Pages\Reportes\Index.cshtml
 - Areas\Payroll\Pages\Reports\Index.cshtml
 - Areas\Payroll\Pages\Settings\Index.cshtml

### C:\IvaFacilitador_ProdPack\Areas\Payroll\Pages
 - Areas\Payroll\Pages\_ViewImports.cshtml
 - Areas\Payroll\Pages\_ViewStart.cshtml
 - Areas\Payroll\Pages\Index.cshtml
 - Areas\Payroll\Pages\Approvals\Index.cshtml
 - Areas\Payroll\Pages\Colaboradores\Index.cshtml
 - Areas\Payroll\Pages\Deducciones\Index.cshtml
 - Areas\Payroll\Pages\Deducciones\Index.cshtml.bak
 - Areas\Payroll\Pages\Deducciones\Index.cshtml.cs
 - Areas\Payroll\Pages\Deducciones\Index.cshtml.cs.bak
 - Areas\Payroll\Pages\Employees\Index.cshtml
 - Areas\Payroll\Pages\Empresas\Index.cshtml
 - Areas\Payroll\Pages\Extras\Index.cshtml
 - Areas\Payroll\Pages\Extras\Index.cshtml.bak
 - Areas\Payroll\Pages\Extras\Index.cshtml.cs
 - Areas\Payroll\Pages\Extras\Index.cshtml.cs.bak
 - Areas\Payroll\Pages\Insights\Index.cshtml
 - Areas\Payroll\Pages\Movements\Index.cshtml
 - Areas\Payroll\Pages\Onboarding\Index.cshtml
 - Areas\Payroll\Pages\PlanillaTotal\Index.cshtml
 - Areas\Payroll\Pages\PlanillaTotal\Index.cshtml.bak.20250924002510
 - Areas\Payroll\Pages\PlanillaTotal\Index.cshtml.cs
 - Areas\Payroll\Pages\Reportes\Index.cshtml
 - Areas\Payroll\Pages\Reports\Index.cshtml
 - Areas\Payroll\Pages\Settings\Index.cshtml

### C:\IvaFacilitador_ProdPack\Areas\Payroll\BaseDatosPayroll
 - Areas\Payroll\BaseDatosPayroll\PayrollDbContext.cs
 - Areas\Payroll\BaseDatosPayroll\Migraciones\20250921074831_InitPayroll.cs
 - Areas\Payroll\BaseDatosPayroll\Migraciones\20250921074831_InitPayroll.Designer.cs
 - Areas\Payroll\BaseDatosPayroll\Migraciones\PayrollDbContextModelSnapshot.cs
 - Areas\Payroll\BaseDatosPayroll\Plantillas\Colaboradores.csv
 - Areas\Payroll\BaseDatosPayroll\Plantillas\Empresa.csv
 - Areas\Payroll\BaseDatosPayroll\Plantillas\Eventos.csv

### C:\IvaFacilitador_ProdPack\Services
 - Services\FileCompanyProfileStore.cs
 - Services\FileCompanyStore.cs
 - Services\ICompanyProfileStore.cs
 - Services\ICompanyStore.cs
 - Services\QuickBooksApi.cs
 - Services\QuickBooksAuth.cs
 - Services\TokenModels.cs

## Existencia de rutas/archivos clave
* **ServicesDir**: Sí
* **PagesPayroll**: Sí
* **AreasPayroll**: Sí
* **PayrollDbContext**: Sí
* **BaseDatosPayroll**: Sí
* **Migrations**: Sí
* **Companies**: No
* **Program**: Sí
* **Deducciones**: Sí
* **Extras**: Sí
* **PlanillaTotal**: Sí
* **AppSettings**: Sí
## Program.cs
- RazorPages: True
- Cultura es-CR: True
- PAYROLL_FEATURE: False
- Endpoints OAuth QBO: False
## DbContext (Payroll)
* Exists: True
* HasCompany: True
* HasEmployee: True
* HasPayItem: True
* HasPayEvent: True
* HasPayPeriod: True
* HasPayRunItem: True
## appsettings.json
- Existe: True
- ConnectionStrings:Payroll: False
- Claves QBO (redactadas): True
## Migrations
 - 20250921074831_InitPayroll.cs
 - 20250921074831_InitPayroll.Designer.cs
 - PayrollDbContextModelSnapshot.cs
## Patrones encontrados
* **RazorConventions** → 4 archivos: Areas\Payroll\Pages\Deducciones\Index.cshtml, Areas\Payroll\Pages\Extras\Index.cshtml, Areas\Payroll\Pages\PlanillaTotal\Index.cshtml, payroll_backup_20250924_094952\Index.cshtml
* **TopbarComponent** → 16 archivos: _audit\20250925-152806\audit.json, Areas\Payroll\Pages\Approvals\Index.cshtml, Areas\Payroll\Pages\Colaboradores\Index.cshtml, Areas\Payroll\Pages\Deducciones\Index.cshtml, Areas\Payroll\Pages\Employees\Index.cshtml, Areas\Payroll\Pages\Empresas\Index.cshtml, Areas\Payroll\Pages\Extras\Index.cshtml, Areas\Payroll\Pages\Insights\Index.cshtml, Areas\Payroll\Pages\Movements\Index.cshtml, Areas\Payroll\Pages\Onboarding\Index.cshtml, Areas\Payroll\Pages\PlanillaTotal\Index.cshtml, Areas\Payroll\Pages\Reportes\Index.cshtml, Areas\Payroll\Pages\Reports\Index.cshtml, Areas\Payroll\Pages\Settings\Index.cshtml, Areas\Payroll\Pages\Index.cshtml, payroll_backup_20250924_094952\Index.cshtml
* **EnsureSeedItems** → 2 archivos: Areas\Payroll\Pages\Deducciones\Index.cshtml.cs, Areas\Payroll\Pages\Extras\Index.cshtml.cs
* **HandlersPost** → 2 archivos: Areas\Payroll\Pages\Deducciones\Index.cshtml.cs, Areas\Payroll\Pages\Extras\Index.cshtml.cs
* **IsApprovedCheck** → 6 archivos: _audit\20250925-152806\audit.json, Areas\Payroll\Components\_Topbar.cshtml, Areas\Payroll\Pages\Deducciones\Index.cshtml, Areas\Payroll\Pages\Deducciones\Index.cshtml.cs, Areas\Payroll\Pages\Extras\Index.cshtml, Areas\Payroll\Pages\Extras\Index.cshtml.cs
* **QboServices** → 14 archivos: _audit\20250925-152806\audit.json, Areas\Payroll\BaseDatosPayroll\Migraciones\20250921074831_InitPayroll.cs, Areas\Payroll\BaseDatosPayroll\Migraciones\20250921074831_InitPayroll.Designer.cs, Areas\Payroll\BaseDatosPayroll\Migraciones\PayrollDbContextModelSnapshot.cs, Areas\Payroll\BaseDatosPayroll\PayrollDbContext.cs, Pages\Auth\Callback.cshtml.cs, Pages\Auth\Disconnect.cshtml.cs, Pages\Auth\Start.cshtml.cs, Pages\Empresas\Index.cshtml.cs, Pages\Parametrizador\Index.cshtml, Services\QuickBooksApi.cs, Services\QuickBooksAuth.cs, tools\PayrollSeeder\Program.cs, Program.cs
* **SQLiteFallback** → 1 archivos: Program.cs
## Git
- Repo: True | Branch: main
- Remote(s):
  - origin	https://github.com/pablouyc/iva-facilitador (fetch)
  - origin	https://github.com/pablouyc/iva-facilitador (push)
## Listo para Empresas (resumen)
* PayrollAreaExists: True
* CompaniesPageExists: False
* TopbarExists: True
* HasSeedItemsMethods: True
* HasPostHandlers: True
* HasApprovedChecks: True
* HasSQLiteFallback: True
* HasQboServices: True
* ProgramHasOAuthEndpoints: False
