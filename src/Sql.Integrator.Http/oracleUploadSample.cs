using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class OracleCloudStorageUploader
{
    private readonly HttpClient _httpClient;
    private readonly string _namespaceName;
    private readonly string _bucketName;
    private string _authToken;

    public OracleCloudStorageUploader(string namespaceName, string bucketName)
    {
        _httpClient = new HttpClient();
        _namespaceName = namespaceName;
        _bucketName = bucketName;
    }

    public async Task UploadFileAsync(string filePath, string objectName)
    {
        await EnsureTokenAsync();
        
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
//https://idjjtwzuv8so.objectstorage.us-ashburn-1.oci.customer-oci.com/n/idjjtwzuv8so/b/fbcwh-podcasts/o/
        var request = new HttpRequestMessage(HttpMethod.Put, $"https://objectstorage.us-phoenix-1.oraclecloud.com/n/{_namespaceName}/b/{_bucketName}/o/{objectName}")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        Console.WriteLine($"File uploaded: {response.StatusCode}");
    }

    private async Task EnsureTokenAsync()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            _authToken = await GetTokenAsync();
        }
    }

    private async Task<string> GetTokenAsync()
    {
        var tokenEndpoint = "https://identity.oraclecloud.com/oauth2/v1/token";
        var clientId = "your_client_id";
        var clientSecret = "your_client_secret";
        var scope = "your_scope";

        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "scope", scope }
        };

        var requestContent = new FormUrlEncodedContent(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = requestContent
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseContent);

        return tokenResponse.AccessToken;
    }

    public static async Task Main(string[] args)
    {
        string namespaceName = "your_namespace"; // Your Oracle Cloud namespace
        string bucketName = "your_bucket"; // Your Oracle Cloud bucket name
        string filePath = "path/to/your/file"; // Path to the file you want to upload
        string objectName = "uploaded_file_name"; // Name to save the file as in the bucket

        var uploader = new OracleCloudStorageUploader(namespaceName, bucketName);
        await uploader.UploadFileAsync(filePath, objectName);
    }

    private class TokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
    }
}
