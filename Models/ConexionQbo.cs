namespace IvaFacilitador.Models;

public class ConexionQbo
{
    public int Id { get; set; }
    public string AccessTokenEnc { get; set; } = string.Empty;
    public string RefreshTokenEnc { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string Environment { get; set; } = "prod";
}
