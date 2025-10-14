using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IvaFacilitador.Domain.Payroll;

public class Empresa {
    public Guid Id { get; set; }
    [Required] public string RealmId { get; set; } = null!;
    [Required, MaxLength(200)] public string Nombre { get; set; } = null!;
    [Required, MaxLength(50)] public string Cedula { get; set; } = null!;
    
    public string? PayPolicy { get; set; }

    public ICollection<Sector> Sectores { get; set; } = new List<Sector>();
    public ICollection<CuentaContable> Cuentas { get; set; } = new List<CuentaContable>();
    public ICollection<Feriado> Feriados { get; set; } = new List<Feriado>();
    public ICollection<Periodo> Periodos { get; set; } = new List<Periodo>();
    public ICollection<Colaborador> Colaboradores { get; set; } = new List<Colaborador>();

    [Timestamp] public byte[]? RowVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Sector {
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    [Required, MaxLength(120)] public string Nombre { get; set; } = null!;
    public bool Activo { get; set; } = true;

    public Empresa Empresa { get; set; } = null!;
    public ICollection<Colaborador> Colaboradores { get; set; } = new List<Colaborador>();
    public ICollection<CuentaContable> Cuentas { get; set; } = new List<CuentaContable>();

    [Timestamp] public byte[]? RowVersion { get; set; }
}

public class CuentaContable {
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid? SectorId { get; set; }
    public TipoCuenta Tipo { get; set; }
    [Required, MaxLength(80)] public string QboAccountId { get; set; } = null!;
    [MaxLength(200)] public string? QboAccountName { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Sector? Sector { get; set; }

    [Timestamp] public byte[]? RowVersion { get; set; }
}

public class Feriado {
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public DateTime Fecha { get; set; } // usar solo la parte de fecha (00:00)
    [Required, MaxLength(160)] public string Nombre { get; set; } = null!;
    public decimal? FactorPago { get; set; } // p.ej.: 2.0 para doble

    public Empresa Empresa { get; set; } = null!;
    [Timestamp] public byte[]? RowVersion { get; set; }
}

public class Periodo {
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public TipoPeriodo Tipo { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    [MaxLength(60)] public string? Etiqueta { get; set; }
    public EstadoPeriodo Estado { get; set; } = EstadoPeriodo.Borrador;

    public Empresa Empresa { get; set; } = null!;
    public ICollection<PeriodoColaborador> PeriodoColaboradores { get; set; } = new List<PeriodoColaborador>();

    [Timestamp] public byte[]? RowVersion { get; set; }
}

public class Colaborador {
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    [Required, MaxLength(160)] public string Nombre { get; set; } = null!;
    [Required, MaxLength(50)] public string Cedula { get; set; } = null!;
    [MaxLength(100)] public string? Cargo { get; set; }
    [MaxLength(120)] public string? Funcion { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SalarioBase { get; set; }
    public Guid? SectorId { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Sector? Sector { get; set; }
    public ICollection<PeriodoColaborador> PeriodoColaboradores { get; set; } = new List<PeriodoColaborador>();

    [Timestamp] public byte[]? RowVersion { get; set; }
}

public class PeriodoColaborador {
    public Guid Id { get; set; }
    public Guid PeriodoId { get; set; }
    public Guid ColaboradorId { get; set; }

    public Periodo Periodo { get; set; } = null!;
    public Colaborador Colaborador { get; set; } = null!;
    public ICollection<Evento> Eventos { get; set; } = new List<Evento>();

    [Timestamp] public byte[]? RowVersion { get; set; }
}

public class Evento {
    public Guid Id { get; set; }
    public Guid PeriodoColaboradorId { get; set; }
    public DateTime Fecha { get; set; } // día natural
    public TipoEvento Tipo { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Horas { get; set; }   // para extras
    [Column(TypeName = "decimal(18,2)")] public decimal? Monto { get; set; }   // para deducciones/otros
    [MaxLength(240)] public string? Nota { get; set; }

    public PeriodoColaborador PeriodoColaborador { get; set; } = null!;
    [Timestamp] public byte[]? RowVersion { get; set; }
}

