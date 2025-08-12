using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.RazorPages; using IvaFacilitador.Services; using IvaFacilitador.Models;
namespace IvaFacilitador.Pages.IVA{
public class SeleccionModel:PageModel{
  private readonly ICompanyStore _companyStore;
  public SeleccionModel(ICompanyStore companyStore){ _companyStore=companyStore; }
  [BindProperty] public string? SelectedRealmId{get;set;}
  [BindProperty] public int SelectedMonth{get;set;}
  [BindProperty] public int SelectedYear{get;set;}
  public List<CompanyConnection> Companies{get;private set;}=new();
  public List<int> Years{get;private set;}=new();
  public void OnGet(){
    Companies=_companyStore.GetCompaniesForUser().ToList();
    var now=DateTime.Now; Years=Enumerable.Range(now.Year-3,5).ToList();
    var prev=now.AddMonths(-1); SelectedMonth=prev.Month; SelectedYear=prev.Year;
  }
  public IActionResult OnPost(){
    Companies=_companyStore.GetCompaniesForUser().ToList();
    if(string.IsNullOrWhiteSpace(SelectedRealmId)) ModelState.AddModelError(nameof(SelectedRealmId),"Selecciona una empresa.");
    if(SelectedMonth<1||SelectedMonth>12) ModelState.AddModelError(nameof(SelectedMonth),"Selecciona un mes válido.");
    if(SelectedYear<2000||SelectedYear>DateTime.Now.Year+1) ModelState.AddModelError(nameof(SelectedYear),"Selecciona un año válido.");
    if(!ModelState.IsValid){ var now=DateTime.Now; Years=Enumerable.Range(now.Year-3,5).ToList(); return Page(); }
    TempData["Success"]=$"Empresa seleccionada: {SelectedRealmId} · Período: {SelectedMonth:00}/{SelectedYear}"; return RedirectToPage("/IVA/Seleccion");
  }
}}
