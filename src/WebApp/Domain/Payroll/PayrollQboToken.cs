using System;
using System.ComponentModel.DataAnnotations;

namespace IvaFacilitador.Domain.Payroll
{
    public class PayrollQboToken
    {
        public Guid Id { get; set; }
        public Guid EmpresaId { get; set; }

        [Required, MaxLength(60)]
        public string RealmId { get; set; } = null!;

        [Required]
        public string AccessToken { get; set; } = null!;

        public string? RefreshToken { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }

        public Empresa Empresa { get; set; } = null!;

        [Timestamp] public byte[]? RowVersion { get; set; }
    }
}
