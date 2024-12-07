using System;
using System.IO;
using Oci.Common;
using Oci.Common.Auth;
using Oci.ObjectstorageService;
using Oci.ObjectstorageService.Requests;
using Oci.ObjectstorageService.Transfer;

class Program
{
    static async Task Main(string[] args)
    {
        var provider = new ConfigFileAuthenticationDetailsProvider("DEFAULT");
        var osClient = new ObjectStorageClient(provider, new ClientConfiguration { TimeoutMillis = 1000 * 1000 });

        var putObjectRequest = new PutObjectRequest
        {
            BucketName = "your-bucket-name",
            NamespaceName = "your-namespace-name",
            ObjectName = "your-object-name",
            PutObjectBody = System.IO.File.OpenRead("path-to-your-file"),
            ContentType = "application/octet-stream"
        };

        var uploadConfiguration = new UploadConfiguration { AllowMultipartUploads = true };
        var uploadManager = new UploadManager(osClient, uploadConfiguration);
        var uploadRequest = new UploadManager.UploadRequest(putObjectRequest) { AllowOverwrite = true };
        var uploadResponse = await uploadManager.Upload(uploadRequest);
        Console.WriteLine(uploadResponse);
    }
}
