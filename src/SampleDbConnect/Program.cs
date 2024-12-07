using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        OpenSqlConnection();
        Console.ReadLine();
    }

    const string defaultScopeSuffix = "/.default";

    // Reuse credential objects to take advantage of underlying token caches
    private static ConcurrentDictionary<string, DefaultAzureCredential> credentials = new ConcurrentDictionary<string, DefaultAzureCredential>();

    // Use a shared callback function for connections that should be in the same connection pool
    private static Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> myAccessTokenCallback =
        async (authParams, cancellationToken) =>
        {
            var scope = authParams.Resource.EndsWith(defaultScopeSuffix)
                ? authParams.Resource
                : $"{authParams.Resource}{defaultScopeSuffix}";

            var options = new DefaultAzureCredentialOptions();
            options.ManagedIdentityClientId = authParams.UserId;

            // Reuse the same credential object if we are using the same MI Client Id
            var token = await credentials.GetOrAdd(authParams.UserId, new DefaultAzureCredential(options)).GetTokenAsync(
                new TokenRequestContext([scope]),
                cancellationToken);

            return new SqlAuthenticationToken(token.Token, token.ExpiresOn);
        };

    private static void OpenSqlConnection()
    {
        // (Optional) Pass a User-Assigned Managed Identity Client ID.
        // This will ensure different MI Client IDs are in different connection pools.
        var connectionString = "Server=sql-jpt-api-test.database.windows.net,1433;Encrypt=Mandatory;";

        using var connection = new SqlConnection(connectionString);
        // The callback function is part of the connection pool key. Using a static callback function
        // ensures connections will not create a new pool per connection just for the callback.
        connection.AccessTokenCallback = myAccessTokenCallback;
        connection.Open();
        Console.WriteLine("ServerVersion: {0}", connection.ServerVersion);
        Console.WriteLine("State: {0}", connection.State);
    }
}