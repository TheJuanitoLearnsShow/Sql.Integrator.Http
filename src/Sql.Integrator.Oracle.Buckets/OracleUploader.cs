using System;
using System.IO;
using System.Threading.Tasks;
using Oci.Common;
using Oci.Common.Auth;
using Oci.ObjectstorageService;
using Oci.ObjectstorageService.Requests;
using Oci.ObjectstorageService.Transfer;

public class OracleUploader
{
    private readonly ObjectStorageClient _osClient;

    public OracleUploader()
    {
        var provider = new ConfigFileAuthenticationDetailsProvider("DEFAULT");
        _osClient = new ObjectStorageClient(provider, new ClientConfiguration { TimeoutMillis = 1000 * 1000 });
    }

    public async Task UploadFile(string bucketName, string namespaceName, string objectName, string filePath)
    {
        var putObjectRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            NamespaceName = namespaceName,
            ObjectName = objectName,
            PutObjectBody = File.OpenRead(filePath),
            ContentType = "application/octet-stream"
        };

        var uploadConfiguration = new UploadConfiguration { AllowMultipartUploads = true };
        var uploadManager = new UploadManager(_osClient, uploadConfiguration);
        var uploadRequest = new UploadManager.UploadRequest(putObjectRequest) { AllowOverwrite = true };
        var uploadResponse = await uploadManager.Upload(uploadRequest);
    }
}
//
// // Usage example:
// public class Program
// {
//     public static async Task Main(string[] args)
//     {
//         var uploader = new OracleUploader();
//         await uploader.UploadFile("your-bucket-name", "your-namespace-name", "your-object-name", "path-to-your-file");
//     }
// }