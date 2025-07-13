using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace Sample.Bucket.Uploader
{
    public class SpeechDiarizationHelper
    {
        private readonly string subscriptionKey;
        private readonly string region;

        public SpeechDiarizationHelper(string subscriptionKey, string region)
        {
            this.subscriptionKey = subscriptionKey;
            this.region = region;
        }

public async Task<string> UploadFileToBlobAsync(string connectionString, string containerName, string filePath)
{
    var blobServiceClient = new BlobServiceClient(connectionString);
    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    await containerClient.CreateIfNotExistsAsync();

    var fileName = Path.GetFileName(filePath);
    var blobClient = containerClient.GetBlobClient(fileName);

    using (var fileStream = File.OpenRead(filePath))
    {
        await blobClient.UploadAsync(fileStream, overwrite: true);
    }

    // Generate SAS URL valid for 1 hour
    var sasBuilder = new BlobSasBuilder
    {
        BlobContainerName = containerName,
        BlobName = fileName,
        Resource = "b",
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
    };
    sasBuilder.SetPermissions(BlobSasPermissions.Read);

    var sasUri = blobClient.GenerateSasUri(sasBuilder);
    return sasUri.ToString();
}

public async Task<string> TranscribeWithDiarizationAsync(string audioFileSasUrl)
{
    var endpoint = $"https://{region}.api.cognitive.microsoft.com/speechtotext/v3.1/transcriptions";
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

    // 1. Create transcription job
    var requestBody = new
    {
        contentUrls = new[] { audioFileSasUrl },
        properties = new {
            diarizationEnabled = true,
            wordLevelTimestampsEnabled = true,
            punctuationMode = "DictatedAndAutomatic",
            profanityFilterMode = "Masked"
        },
        locale = "en-US",
        displayName = "Speaker Diarization Job"
    };
    var response = await client.PostAsync(
        endpoint,
        new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
    );
    response.EnsureSuccessStatusCode();

    // 2. Get transcription job location
    var jobLocation = response.Headers.Location?.ToString();
    if (string.IsNullOrEmpty(jobLocation))
        throw new Exception("Failed to get transcription job location.");

    // 3. Poll for completion
    var status = "";
    string resultUrl = null;
    for (int i = 0; i < 60; i++) // Poll up to ~10 minutes
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        var jobResp = await client.GetAsync(jobLocation);
        jobResp.EnsureSuccessStatusCode();
        var jobJson = await jobResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jobJson);
        status = doc.RootElement.GetProperty("status").GetString();
        if (status == "Succeeded")
        {
            var resultsUrls = doc.RootElement.GetProperty("resultsUrls");
            resultUrl = resultsUrls.GetProperty("transcriptionFiles").GetString();
            break;
        }
        if (status == "Failed")
            throw new Exception("Transcription job failed.");
    }
    if (status != "Succeeded" || string.IsNullOrEmpty(resultUrl))
        throw new Exception("Transcription job did not complete in time.");

    // 4. Download result file (JSON with recognized phrases and speakers)
    var resultResp = await client.GetAsync(resultUrl);
    resultResp.EnsureSuccessStatusCode();
    var resultJson = await resultResp.Content.ReadAsStringAsync();
    return resultJson;
}

        public (string SpeakerId, List<(double Start, double End)> Segments) GetLongestSpeakerSegments(string transcriptionJson)
        {
            // Azure returns a JSON with recognized phrases and speaker IDs
            // We'll parse the recognized phrases, group by speaker, and sum durations
            using var doc = JsonDocument.Parse(transcriptionJson);
            var phrases = doc.RootElement
                .GetProperty("recognizedPhrases")
                .EnumerateArray()
                .Select(phrase => new
                {
                    Speaker = phrase.GetProperty("speaker").GetString() ?? "unknown",
                    Offset = phrase.GetProperty("offset").GetInt64(), // in 100-nanosecond units
                    Duration = phrase.GetProperty("duration").GetInt64() // in 100-nanosecond units
                })
                .ToList();

            var speakerDurations = phrases
                .GroupBy(p => p.Speaker)
                .Select(g => new
                {
                    Speaker = g.Key,
                    TotalDuration = g.Sum(x => x.Duration),
                    Segments = g.Select(x => (Start: x.Offset / 1e7, End: (x.Offset + x.Duration) / 1e7)).ToList() // seconds
                })
                .OrderByDescending(x => x.TotalDuration)
                .FirstOrDefault();

            if (speakerDurations == null)
                throw new Exception("No speaker segments found in transcription JSON.");

            return (speakerDurations.Speaker, speakerDurations.Segments);
        }

        public void ExtractAudioSegments(string inputMp3, List<(double Start, double End)> segments, string outputMp3)
        {
            // Create a temporary directory for segment files
            var tempDir = Path.Combine(Path.GetTempPath(), "ffmpeg_segments_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            var segmentFiles = new List<string>();
            try
            {
                int idx = 0;
                foreach (var (start, end) in segments)
                {
                    var segFile = Path.Combine(tempDir, $"seg_{idx}.mp3");
                    var duration = end - start;
                    var ffmpegArgs = $"-y -i \"{inputMp3}\" -ss {start:F3} -t {duration:F3} -c copy \"{segFile}\"";
                    var proc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = ffmpegArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    proc!.WaitForExit();
                    if (proc.ExitCode != 0) throw new Exception($"ffmpeg failed: {proc.StandardError.ReadToEnd()}");
                    segmentFiles.Add(segFile);
                    idx++;
                }
                // Create a file list for ffmpeg concat
                var concatListPath = Path.Combine(tempDir, "concat.txt");
                File.WriteAllLines(concatListPath, segmentFiles.Select(f => $"file '{f.Replace("'", "'\\''")}'"));
                // Concatenate segments
                var concatArgs = $"-y -f concat -safe 0 -i \"{concatListPath}\" -c copy \"{outputMp3}\"";
                var concatProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = concatArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                concatProc!.WaitForExit();
                if (concatProc.ExitCode != 0) throw new Exception($"ffmpeg concat failed: {concatProc.StandardError.ReadToEnd()}");
            }
            finally
            {
                // Clean up temp files
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
