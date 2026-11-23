using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace InspectionMonitor;

public sealed class OracleSecomRepository
{
    public async Task ImportAsync(IReadOnlyList<SecomSample> samples)
    {
        using var connection = OracleInspectionRepository.CreateConnection();
        await connection.OpenAsync();

        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = """
                CREATE TABLE IF NOT EXISTS secom_inspection (
                    sample_id NUMBER(9) PRIMARY KEY,
                    inspected_at DATE NOT NULL,
                    result_label NUMBER(2) NOT NULL CHECK (result_label IN (-1, 1))
                )
                """;
            await schema.ExecuteNonQueryAsync();
        }

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 60;
        command.ArrayBindCount = samples.Count;
        command.CommandText = """
            MERGE INTO secom_inspection tgt
            USING (SELECT :sample_id AS sample_id,
                          :inspected_at AS inspected_at,
                          :result_label AS result_label
                   FROM dual) src
            ON (tgt.sample_id = src.sample_id)
            WHEN NOT MATCHED THEN INSERT (sample_id, inspected_at, result_label)
            VALUES (src.sample_id, src.inspected_at, src.result_label)
            """;

        command.Parameters.Add("sample_id", OracleDbType.Int32).Value =
            samples.Select(sample => sample.Id).ToArray();
        command.Parameters.Add("inspected_at", OracleDbType.Date).Value =
            samples.Select(sample => sample.InspectedAt).ToArray();
        command.Parameters.Add("result_label", OracleDbType.Int32).Value =
            samples.Select(sample => sample.Label).ToArray();

        await command.ExecuteNonQueryAsync();
        transaction.Commit();
    }

    public async Task<List<SecomSample>> FindAsync(DateTime fromDate, DateTime throughDate)
    {
        using var connection = OracleInspectionRepository.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 15;
        command.CommandText = """
            SELECT sample_id, inspected_at, result_label
            FROM secom_inspection
            WHERE inspected_at >= :from_date AND inspected_at < :end_exclusive
            ORDER BY inspected_at, sample_id
            """;
        command.Parameters.Add("from_date", OracleDbType.Date).Value = fromDate.Date;
        command.Parameters.Add("end_exclusive", OracleDbType.Date).Value =
            throughDate.Date.AddDays(1);

        var rows = new List<SecomSample>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(new SecomSample(
                reader.GetInt32(0), reader.GetDateTime(1), reader.GetInt32(2)));
        }

        return rows;
    }
}
