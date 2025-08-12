using System.Text.Json; using IvaFacilitador.Models; using Microsoft.Extensions.Options;
namespace IvaFacilitador.Services{
public class FileCompanyStore:ICompanyStore{
    private readonly string _folder; private readonly string _filePath; private readonly object _lock=new();
    public FileCompanyStore(IOptionsMonitor<DataSettings> dataOptions){
        _folder=dataOptions.CurrentValue.Folder??"App_Data"; Directory.CreateDirectory(_folder);
        _filePath=Path.Combine(_folder,"companies.json"); if(!File.Exists(_filePath)) File.WriteAllText(_filePath,"[]"); }
    public IReadOnlyList<CompanyConnection> GetCompaniesForUser(string userId="demo-user"){
        lock(_lock){ var json=File.ReadAllText(_filePath); return JsonSerializer.Deserialize<List<CompanyConnection>>(json)??new(); } }
    public void AddOrUpdateCompany(CompanyConnection company,string userId="demo-user"){
        lock(_lock){
            var json=File.ReadAllText(_filePath);
            var list=JsonSerializer.Deserialize<List<CompanyConnection>>(json)??new();
            var existing=list.FirstOrDefault(c=>c.RealmId==company.RealmId);
            if(existing is null){ company.ConnectedAt=DateTime.UtcNow; list.Add(company); }
            else{ existing.Name=company.Name; existing.ConnectedAt=DateTime.UtcNow; }
            File.WriteAllText(_filePath, JsonSerializer.Serialize(list,new JsonSerializerOptions{WriteIndented:true})); } } }
public class DataSettings{public string? Folder{get;set;}} }
