using System;
using System.Data;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;

public class DataProcessor
{
    private readonly string _connectionString;
    private readonly HttpClient _httpClient;
    private readonly AsyncRetryPolicy _sqlRetryPolicy;
    private readonly AsyncRetryPolicy _httpRetryPolicy;

    public DataProcessor(string connectionString, HttpClient httpClient)
    {
        _connectionString = connectionString;
        _httpClient = httpClient;

        _sqlRetryPolicy = Policy
            .Handle<SqlException>(ex => ex.Number == -2 || ex.Number == 1205)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        _httpRetryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    public async Task ProcessDataAsync()
    {
        try
        {
            var data = await GetDataFromStoredProcedureAsync();
            await PostDataToEndpointAsync(data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private async Task<string> GetDataFromStoredProcedureAsync()
    {
        return await _sqlRetryPolicy.ExecuteAsync(async () =>
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("YourStoredProcedureName", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return reader.GetString(0); // Adjust based on your stored procedure's return type
            }
            
            throw new Exception("No data returned from stored procedure.");
        });
    }

    private async Task PostDataToEndpointAsync(string data)
    {
        var content = new StringContent(data);
        
        await _httpRetryPolicy.ExecuteAsync(async () =>
        {
            var response = await _httpClient.PostAsync("https://yourapiendpoint.com/api", content);
            response.EnsureSuccessStatusCode();
            return response;
        });
    }
}
