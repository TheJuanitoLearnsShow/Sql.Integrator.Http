// See https://aka.ms/new-console-template for more information

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
Console.WriteLine();

if (args.Length > 1)
{
    
    var title = args[1];
    var episodeNum = args[2];

    var xml = GetXml(title, 
        fileSize,
        $"https://idjjtwzuv8so.objectstorage.us-ashburn-1.oci.customer-oci.com/n/idjjtwzuv8so/b/fbcwh-podcasts/o/{objName}",
        (int)GetDuration(inputFile).TotalMinutes,
        int.Parse(episodeNum)
    );
    Console.WriteLine(xml);
}

TimeSpan GetDuration(string filePath)
{
    var tfile = TagLib.File.Create(filePath);
    string title = tfile.Tag.Title;
    TimeSpan duration = tfile.Properties.Duration;
    return duration;
}
string GetXml(string title, long size, string url, int durationMinutes, int episodeNumber)
{
    var formattedDate = DateTime.Now.ToString("ddd, dd MMM yyyy HH:mm:ss 'EST'");
    var formattedTitle = System.Security.SecurityElement.Escape(title);
    return $"""
            <item>
              <guid isPermalink="false">{4069 + episodeNumber - 12}</guid>
              <enclosure url="{url}" type="audio/mpeg" length="{size}"/>
              <itunes:season>2</itunes:season>
              <itunes:episode>{episodeNumber}</itunes:episode>
              <pubDate>{formattedDate}</pubDate>
              <title>{formattedTitle}</title>
              <description>{formattedTitle}</description>
              <itunes:duration>00:{durationMinutes + 1}:00</itunes:duration>
              <itunes:explicit>false</itunes:explicit>
              <itunes:episodeType>full</itunes:episodeType>
            </item>
            """;
} 