using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;
using PayPeriodModel = IvaFacilitador.Areas.Payroll.ModelosPayroll.PayPeriod;
using IvaFacilitador.Areas.Payroll.BaseDatosPayroll;

namespace IvaFacilitador.Areas.Payroll.Services
{
    public interface ICollaboratorsStore
    {
        Task EnsureSchemaAsync(PayrollDbContext db, CancellationToken ct=default);
        Task<List<Collaborator>> ListAsync(PayrollDbContext db, int companyId, bool includeInactive=false, CancellationToken ct=default);
        Task<int> AddManualAsync(PayrollDbContext db, Collaborator c, CancellationToken ct=default);
        Task<int> UpsertFromQboAsync(PayrollDbContext db, int companyId, IEnumerable<(string id,string name)> employees, CancellationToken ct=default);
        Task<int> SetStatusAsync(PayrollDbContext db, int id, int status, CancellationToken ct=default);
    }

    public class SqlCollaboratorsStore : ICollaboratorsStore
    {
        private static string GetCs(PayrollDbContext db) => db.Database.GetDbConnection().ConnectionString;

        public async Task EnsureSchemaAsync(PayrollDbContext db, CancellationToken ct=default)
        {
            var cs = GetCs(db);
            await using var conn = new SqliteConnection(cs);
            await conn.OpenAsync(ct);

            var ddl = @"
CREATE TABLE IF NOT EXISTS Collaborators (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  CompanyId INTEGER NOT NULL,
  Name TEXT NOT NULL,
  TaxId TEXT NULL,
  Sector TEXT NULL,
  Position TEXT NULL,
  MonthlySalary REAL NULL,
  PayPeriod INTEGER NOT NULL DEFAULT 1,
  Split1 INTEGER NULL,
  Split2 INTEGER NULL,
  Split3 INTEGER NULL,
  Split4 INTEGER NULL,
  UseCCSS INTEGER NOT NULL DEFAULT 0,
  UseSeguro INTEGER NOT NULL DEFAULT 0,
  Status INTEGER NOT NULL DEFAULT 0,
  QboEmployeeId TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_Collaborators_Company ON Collaborators(CompanyId);
";
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = ddl;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<List<Collaborator>> ListAsync(PayrollDbContext db, int companyId, bool includeInactive=false, CancellationToken ct=default)
        {
            var cs = GetCs(db);
            await using var conn = new SqliteConnection(cs);
            await conn.OpenAsync(ct);

            var sql = "SELECT Id,CompanyId,Name,TaxId,Sector,Position,MonthlySalary,PayPeriod,Split1,Split2,Split3,Split4,UseCCSS,UseSeguro,Status,QboEmployeeId FROM Collaborators WHERE CompanyId=@cid";
            if (!includeInactive) sql += " AND Status=0";
            sql += " ORDER BY Name";

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new SqliteParameter("@cid", companyId));

            var list = new List<Collaborator>();
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                list.Add(new Collaborator{
                    Id = rd.GetInt32(0),
                    CompanyId = rd.GetInt32(1),
                    Name = rd.GetString(2),
                    TaxId = rd.IsDBNull(3)? null: rd.GetString(3),
                    Sector = rd.IsDBNull(4)? null: rd.GetString(4),
                    Position = rd.IsDBNull(5)? null: rd.GetString(5),
                    MonthlySalary = rd.IsDBNull(6)? null: rd.GetDecimal(6),
                    PayPeriod = (PayPeriodModel)rd.GetInt32(7),
                    Split1 = rd.IsDBNull(8)? null: rd.GetInt32(8),
                    Split2 = rd.IsDBNull(9)? null: rd.GetInt32(9),
                    Split3 = rd.IsDBNull(10)? null: rd.GetInt32(10),
                    Split4 = rd.IsDBNull(11)? null: rd.GetInt32(11),
                    UseCCSS = rd.GetInt32(12) == 1,
                    UseSeguro = rd.GetInt32(13) == 1,
                    Status = rd.GetInt32(14),
                    QboEmployeeId = rd.IsDBNull(15)? null: rd.GetString(15),
                });
            }
            return list;
        }

        public async Task<int> AddManualAsync(PayrollDbContext db, Collaborator c, CancellationToken ct=default)
        {
            var cs = GetCs(db);
            await using var conn = new SqliteConnection(cs);
            await conn.OpenAsync(ct);

            var sql = @"INSERT INTO Collaborators
(CompanyId,Name,TaxId,Sector,Position,MonthlySalary,PayPeriod,Split1,Split2,Split3,Split4,UseCCSS,UseSeguro,Status,QboEmployeeId)
VALUES (@CompanyId,@Name,@TaxId,@Sector,@Position,@MonthlySalary,@PayPeriod,@Split1,@Split2,@Split3,@Split4,@UseCCSS,@UseSeguro,@Status,@QboEmployeeId)";
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddRange(new[]{
                new SqliteParameter("@CompanyId",     c.CompanyId),
                new SqliteParameter("@Name",          c.Name ?? ""),
                new SqliteParameter("@TaxId",         (object?)c.TaxId ?? DBNull.Value),
                new SqliteParameter("@Sector",        (object?)c.Sector ?? DBNull.Value),
                new SqliteParameter("@Position",      (object?)c.Position ?? DBNull.Value),
                new SqliteParameter("@MonthlySalary", (object?)c.MonthlySalary ?? DBNull.Value),
                new SqliteParameter("@PayPeriod",     (int)c.PayPeriod),
                new SqliteParameter("@Split1",        (object?)c.Split1 ?? DBNull.Value),
                new SqliteParameter("@Split2",        (object?)c.Split2 ?? DBNull.Value),
                new SqliteParameter("@Split3",        (object?)c.Split3 ?? DBNull.Value),
                new SqliteParameter("@Split4",        (object?)c.Split4 ?? DBNull.Value),
                new SqliteParameter("@UseCCSS",       c.UseCCSS ? 1 : 0),
                new SqliteParameter("@UseSeguro",     c.UseSeguro ? 1 : 0),
                new SqliteParameter("@Status",        c.Status),
                new SqliteParameter("@QboEmployeeId", (object?)c.QboEmployeeId ?? DBNull.Value)
            });
            return await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> UpsertFromQboAsync(PayrollDbContext db, int companyId, IEnumerable<(string id,string name)> employees, CancellationToken ct=default)
        {
            var cs = GetCs(db);
            await using var conn = new SqliteConnection(cs);
            await conn.OpenAsync(ct);

            var tx = await conn.BeginTransactionAsync(ct);
            var count = 0;

            foreach (var (id, name) in employees)
            {
                // Inserta si no existe por QboEmployeeId
                var sql = @"INSERT INTO Collaborators (CompanyId, Name, QboEmployeeId, Status)
SELECT @CompanyId, @Name, @QboId, 0
WHERE NOT EXISTS (SELECT 1 FROM Collaborators WHERE CompanyId=@CompanyId AND QboEmployeeId=@QboId);";
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx as SqliteTransaction;
                cmd.CommandText = sql;
                cmd.Parameters.AddRange(new[]{
                    new SqliteParameter("@CompanyId", companyId),
                    new SqliteParameter("@Name", name ?? ""),
                    new SqliteParameter("@QboId", id ?? "")
                });
                count += await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return count;
        }

        public async Task<int> SetStatusAsync(PayrollDbContext db, int id, int status, CancellationToken ct=default)
        {
            var cs = GetCs(db);
            await using var conn = new SqliteConnection(cs);
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Collaborators SET Status=@s WHERE Id=@id";
            cmd.Parameters.Add(new SqliteParameter("@s", status));
            cmd.Parameters.Add(new SqliteParameter("@id", id));
            return await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}

