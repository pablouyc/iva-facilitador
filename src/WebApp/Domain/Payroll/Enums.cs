namespace IvaFacilitador.Domain.Payroll;

public enum TipoCuenta { SalarioBruto = 1, HorasExtras = 2, CCSS = 3, Deducciones = 4, SalarioNeto = 5, Otros = 6 }
public enum TipoPeriodo { Semanal = 1, Quincenal = 2, Mensual = 3 }
public enum EstadoPeriodo { Borrador = 1, Cerrado = 2 }
public enum TipoEvento { Planilla = 1, HorasExtra = 2, Deduccion = 3, FeriadoTrabajado = 4, Incapacidad = 5, Otro = 9 }
