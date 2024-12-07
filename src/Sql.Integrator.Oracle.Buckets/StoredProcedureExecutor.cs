using Microsoft.Data.SqlClient;

namespace Sql.Integrator.Oracle.Buckets;

public class StoredProcedureExecutor
{
    public static async Task ExecuteNonReader(SqlToExecSettings settings, Dictionary<string, object?> parameters)
    {
        await using var connection = new SqlConnection(settings.ConnectionString);
        await using var command = new SqlCommand(settings.StoredProcedureName, connection);
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.Clear();
        foreach (var param in settings.SqlParametersMapping)
        {
            if (parameters.TryGetValue(param.Value, out var parameter))
            {
                command.Parameters.AddWithValue(param.Key, parameter ?? DBNull.Value);
            }
        }

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    public static async Task ExecuteReader(SqlToExecSettings settings, Action<Dictionary<string, object?>> mapper,
        Dictionary<string, object?> parameters)
    {
        await using var connection = new SqlConnection(settings.ConnectionString);
        await using var command = new SqlCommand(settings.StoredProcedureName, connection);
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.Clear();
        foreach (var param in settings.SqlParametersMapping)
        {
            if (parameters.TryGetValue(param.Value, out var parameter))
            {
                command.Parameters.AddWithValue(param.Key, parameter ?? DBNull.Value);
            }
        }

        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            mapper(DataReaderHelper.ConvertDataReaderToDictionary(reader));
        }
    }
}

public class DataReaderHelper
{
    public static Dictionary<string, object?> ConvertDataReaderToDictionary(SqlDataReader reader)
    {
        var result = new Dictionary<string, object?>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var key = reader.GetName(i);
            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            result.Add(key, value);
        }

        return result;
    }
}