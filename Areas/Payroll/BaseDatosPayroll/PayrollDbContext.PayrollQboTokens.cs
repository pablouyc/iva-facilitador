using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;

namespace IvaFacilitador.Areas.Payroll.BaseDatosPayroll
{
    public partial class PayrollDbContext : DbContext
    {
        public DbSet<PayrollQboToken> PayrollQboTokens { get; set; } = default!;
    }
}
