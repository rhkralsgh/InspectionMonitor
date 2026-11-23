using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace InspectionMonitor;

public sealed class OracleInspectionRepository
{
    public async Task<List<InspectionResult>> FindAsync(
        DateTime date, string? equipmentName)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 15;
        command.CommandText = """
            SELECT r.inspection_id, r.inspected_at,
                   e.equipment_name, r.measured_value
            FROM inspection_result r
            JOIN equipment e ON r.equipment_id = e.equipment_id
            WHERE r.inspected_at >= :from_date
              AND r.inspected_at < :to_date
              AND (:equipment_name IS NULL OR e.equipment_name = :equipment_name)
            ORDER BY r.inspected_at, r.inspection_id
            """;

        command.Parameters.Add("from_date", OracleDbType.Date).Value = date.Date;
        command.Parameters.Add("to_date", OracleDbType.Date).Value = date.Date.AddDays(1);
        command.Parameters.Add("equipment_name", OracleDbType.NVarchar2, 30).Value =
            equipmentName is null ? DBNull.Value : equipmentName;

        var rows = new List<InspectionResult>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(new InspectionResult
            {
                Id = reader.GetInt32(0),
                InspectedAt = reader.GetDateTime(1),
                Equipment = reader.GetString(2),
                Value = (double)reader.GetDecimal(3)
            });
        }

        return rows;
    }

    internal static OracleConnection CreateConnection()
    {
        string? password =
            Environment.GetEnvironmentVariable("INSPECTION_DB_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "INSPECTION_DB_PASSWORD 환경 변수에 DB 비밀번호를 설정해 주세요.");

        var settings = new OracleConnectionStringBuilder
        {
            UserID = "inspection_app",
            Password = password,
            DataSource = "127.0.0.1:1521/FREEPDB1"
        };

        return new OracleConnection(settings.ConnectionString);
    }
}