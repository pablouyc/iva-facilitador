param([string]$BaseDir = "C:\IvaFacilitador_ProdPack")
$ErrorActionPreference = "Stop"; Set-StrictMode -Version Latest
function Stamp { Get-Date -Format "yyyyMMdd-HHmmss" }
function Backup-IfExists([string]$path){ if(Test-Path $path){ $bak="$path.bak.$(Stamp)"; Copy-Item $path $bak -Force; Write-Host "Backup -> $bak" -ForegroundColor Yellow } }

if (!(Test-Path $BaseDir)) { throw "No existe: $BaseDir" }
Set-Location $BaseDir
$empDir   = Join-Path $BaseDir "Areas\Payroll\Pages\Empresas"
$cshtml   = Join-Path $empDir "Index.cshtml"
$csbehind = Join-Path $empDir "Index.cshtml.cs"
New-Item -ItemType Directory -Force -Path $empDir | Out-Null

$cshtmlContent = @"
@page
@model IvaFacilitador.Areas.Payroll.Pages.Empresas.IndexModel
@{
    ViewData["Title"] = "Empresas";
}
<partial name="~/Areas/Payroll/Components/_Topbar.cshtml" />

<div class="d-flex justify-content-between align-items-center mb-3">
    <h2 class="m-0">Empresas</h2>
    <div>
        <a class="btn btn-primary" id="btnAdd" href="#" title="Agregar empresa (próximamente)">
            Agregar
        </a>
    </div>
</div>

<div class="table-responsive">
    <table class="table table-sm table-striped align-middle">
        <thead class="table-light">
            <tr>
                <th>Nombre</th>
                <th>Cédula / TaxId</th>
                <th>QBO</th>
                <th class="text-end">Acciones</th>
            </tr>
        </thead>
        <tbody>
        @if (Model.Rows.Count == 0)
        {
            <tr><td colspan="4" class="text-center text-muted">No hay empresas registradas.</td></tr>
        }
        else
        {
            foreach (var row in Model.Rows)
            {
                <tr>
                    <td>@row.Name</td>
                    <td>@row.TaxId</td>
                    <td>
                        @if (row.QboConnected)
                        {
                            <span class="badge bg-success">Conectada</span>
                            <small class="text-muted ms-1">@row.QboId</small>
                        }
                        else
                        {
                            <span class="badge bg-secondary">No conectada</span>
                        }
                    </td>
                    <td class="text-end">
                        <a class="btn btn-outline-secondary btn-sm" title="Editar parámetros (próximamente)" href="#">
                            Editar
                        </a>
                        @if (row.QboConnected)
                        {
                            <form method="post" class="d-inline" asp-page-handler="Disconnect" asp-route-id="@row.Id">
                                <button type="submit" class="btn btn-outline-danger btn-sm" title="Desligar QBO">
                                    Desligar
                                </button>
                            </form>
                        }
                        else
                        {
                            <a class="btn btn-outline-primary btn-sm" title="Conectar QBO"
                               href="/Auth/Login?companyId=@row.Id">
                                Conectar QBO
                            </a>
                        }
                    </td>
                </tr>
            }
        }
        </tbody>
    </table>
</div>
"@

$csContent = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;

namespace IvaFacilitador.Areas.Payroll.Pages.Empresas
{
    public class IndexModel : PageModel
    {
        private readonly PayrollDbContext _db;

        public IndexModel(PayrollDbContext db)
        {
            _db = db;
        }

        public List<RowVM> Rows { get; set; } = new();

        public class RowVM
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string? TaxId { get; set; }
            public string? QboId { get; set; }
            public bool QboConnected => !string.IsNullOrWhiteSpace(QboId);
        }

        public async Task OnGet()
        {
            Rows = await _db.Companies
                .OrderBy(c => c.Name)
                .Select(c => new RowVM
                {
                    Id = c.Id,
                    Name = c.Name,
                    TaxId = c.TaxId,
                    QboId = c.QboId
                })
                .ToListAsync();
        }

        // POST /Payroll/Empresas?handler=Disconnect&id=123
        public async Task<IActionResult> OnPostDisconnect(int id)
        {
            var company = await _db.Companies.FindAsync(id);
            if (company != null)
            {
                company.QboId = null; // desligar QBO (limpia vínculo)
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}
"@

Backup-IfExists $cshtml
Backup-IfExists $csbehind
Set-Content -LiteralPath $cshtml   -Value $cshtmlContent -Encoding UTF8
Set-Content -LiteralPath $csbehind -Value $csContent     -Encoding UTF8

Write-Host "Actualizados:" -ForegroundColor Cyan
Write-Host " - $cshtml"
Write-Host " - $csbehind"

# Build de validación (si falla, igual deja backups para revertir)
try { & dotnet build | Out-Host } catch { Write-Warning $_ }
