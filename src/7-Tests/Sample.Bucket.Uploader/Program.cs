// See https://aka.ms/new-console-template for more information

Console.WriteLine("Hello, World!");
var inputFile = args[0];

 var uploader = new OracleUploader();
 var objName = Path.GetFileName(inputFile);
 var (fileSize, response) = await uploader.UploadFile(
     "fbcwh-podcasts", 
  "idjjtwzuv8so", 
     objName, 
     inputFile);
Console.WriteLine(fileSize);
Console.WriteLine(response.ETag);
Console.WriteLine($"https://idjjtwzuv8so.objectstorage.us-ashburn-1.oci.customer-oci.com/n/idjjtwzuv8so/b/fbcwh-podcasts/o/{objName}");


string GetXml(string title, int size, string url, int durationMinutes, int episodeNumber)
{
    return $"""
            <item>
              <guid isPermalink="false">{4069 + episodeNumber - 12}</guid>
              <enclosure url="{url}" type="audio/mpeg" length="{size}"/>
              <itunes:season>2</itunes:season>
              <itunes:episode>{episodeNumber}</itunes:episode>
              <pubDate>Sun, 16 Feb 2025 13:00:00 EST</pubDate>
              <title>{title}</title>
              <description>{title}</description>
              <itunes:duration>00:{durationMinutes}:00</itunes:duration>
              <itunes:explicit>false</itunes:explicit>
              <itunes:episodeType>full</itunes:episodeType>
            </item>
            """;
} 