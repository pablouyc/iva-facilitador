using IvaFacilitador.Models;
using IvaFacilitador.Models.Dto;
using Xunit;

namespace IvaFacilitador.Tests;

public class MappingTests
{
    [Fact]
    public void MapsPendingCompanyAndDtoToEmpresa()
    {
        var pc = new PendingCompany
        {
            CompanyName = "Co",
            RealmId = "1",
            AccessToken = "at",
            RefreshToken = "rt",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };
        var dto = new ParametrizacionEmpresaDto
        {
            Moneda = "CRC",
            Pais = "CR",
            PeriodoIva = "Mensual",
            PorcentajeIvaDefault = 0.13m,
            MetodoRedondeo = "Matematico"
        };

        var empresa = new Empresa
        {
            CompanyName = pc.CompanyName,
            RealmId = pc.RealmId,
            Moneda = dto.Moneda,
            Pais = dto.Pais,
            PeriodoIva = dto.PeriodoIva,
            PorcentajeIvaDefault = dto.PorcentajeIvaDefault,
            MetodoRedondeo = dto.MetodoRedondeo,
            ConexionQbo = new ConexionQbo
            {
                AccessTokenEnc = pc.AccessToken,
                RefreshTokenEnc = pc.RefreshToken,
                ExpiresAtUtc = pc.ExpiresAtUtc
            }
        };

        Assert.Equal(pc.CompanyName, empresa.CompanyName);
        Assert.Equal(dto.Moneda, empresa.Moneda);
        Assert.Equal(pc.AccessToken, empresa.ConexionQbo.AccessTokenEnc);
    }
}
