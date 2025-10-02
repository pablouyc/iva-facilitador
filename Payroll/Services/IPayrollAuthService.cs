namespace IvaFacilitador.Payroll.Services
{
    public interface IPayrollAuthService
    {
        /// <summary>Devuelve la URL de autorización de Intuit (Payroll) usando IntuitPayrollAuth__*</summary>
        string GetAuthorizeUrl(string returnTo);
    }
}
