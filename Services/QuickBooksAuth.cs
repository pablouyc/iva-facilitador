using System.Net.Http.Headers; using Microsoft.Extensions.Options;
namespace IvaFacilitador.Services{
public interface IQuickBooksAuth{ string BuildAuthorizeUrl(string state="xyz"); Task<TokenResponse?> ExchangeCodeForTokenAsync(string code,CancellationToken ct=default); }
public class QuickBooksAuth:IQuickBooksAuth{
    private readonly IntuitOAuthSettings _settings; private readonly IHttpClientFactory _httpFactory;
    public QuickBooksAuth(IOptions<IntuitOAuthSettings> options,IHttpClientFactory httpFactory){ _settings=options.Value; _httpFactory=httpFactory; }
    public string BuildAuthorizeUrl(string state="xyz"){
        var scope=Uri.EscapeDataString(_settings.Scopes); var redirect=Uri.EscapeDataString(_settings.RedirectUri);
        return $"https://appcenter.intuit.com/connect/oauth2?client_id={_settings.ClientId}&response_type=code&scope={scope}&redirect_uri={redirect}&state={state}&prompt=consent";
    }
    public async Task<TokenResponse?> ExchangeCodeForTokenAsync(string code,CancellationToken ct=default){
        var client=_httpFactory.CreateClient(); var tokenEndpoint="https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";
        var req=new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
        var authValue=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
        req.Headers.Authorization=new AuthenticationHeaderValue("Basic", authValue);
        req.Content=new FormUrlEncodedContent(new Dictionary<string,string>{
          {"grant_type","authorization_code"}, {"code",code}, {"redirect_uri",_settings.RedirectUri}
        });
        var resp=await client.SendAsync(req, ct); if(!resp.IsSuccessStatusCode) return null;
        var json=await resp.Content.ReadAsStringAsync(ct); return System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(json);
    }
}}
