using System.Globalization;
using LensLoom.Core.Abstractions;
using LensLoom.Core.Models;
using LensLoom.Ingest;

if (args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase) || args[0] is "--help" or "-h")
{
    PrintUsage();
    return 0;
}

if (!args[0].Equals("ingest", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Unknown command: '{args[0]}'");
    PrintUsage();
    return 1;
}

string? sourceDir = null;
string? destinationDir = null;
string? sessionName = null;
bool recursive = true;

for (int i = 1; i < args.Length; i++)
{
    var current = args[i];
    switch (current)
    {
        case "--source" or "-s" when i + 1 < args.Length:
            sourceDir = args[++i];
            break;
        case "--destination" or "-d" when i + 1 < args.Length:
            destinationDir = args[++i];
            break;
        case "--session" when i + 1 < args.Length:
            sessionName = args[++i];
            break;
        case "--no-recursive":
            recursive = false;
            break;
        case "--help" or "-h":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown or incomplete option: '{current}'");
            PrintUsage();
            return 1;
    }
}

if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(destinationDir))
{
    Console.Error.WriteLine("Both --source and --destination are required for 'ingest'.");
    PrintUsage();
    return 1;
}

if (!Directory.Exists(sourceDir))
{
    Console.Error.WriteLine($"Source directory does not exist: '{sourceDir}'");
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

IExifReader exifReader = new MetadataExtractorExifReader();
IFileHasher fileHasher = new Sha256FileHasher();
IPhotoOrganizer photoOrganizer = new PhotoFolderOrganizer();
ISessionSummaryWriter summaryWriter = new MarkdownSessionSummaryWriter();

var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
var files = Directory.EnumerateFiles(sourceDir, "*.*", searchOption)
    .Where(path =>
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith('.'))
        {
            return false;
        }

        return PhotoFolderOrganizer.DetectFormat(path) is PhotoFormatKind.Raw or PhotoFormatKind.Jpeg;
    })
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (files.Count == 0)
{
    Console.WriteLine("No RAW or JPEG files found to ingest.");
    return 0;
}

var effectiveSessionName = string.IsNullOrWhiteSpace(sessionName)
    ? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    : sessionName.Trim();

var results = new List<OrganizedPhotoResult>(files.Count);
var importedCount = 0;
var duplicateCount = 0;
var errorCount = 0;

foreach (var filePath in files)
{
    if (cts.IsCancellationRequested)
    {
        break;
    }

    try
    {
        var fileInfo = new FileInfo(filePath);
        var sha256 = await fileHasher.ComputeSha256Async(filePath, cts.Token);
        var exif = await exifReader.ReadAsync(filePath, cts.Token);
        var formatKind = PhotoFolderOrganizer.DetectFormat(filePath);

        var asset = new PhotoAsset(
            SourcePath: filePath,
            FileName: fileInfo.Name,
            Sha256: sha256,
            FileSizeBytes: fileInfo.Length,
            FormatKind: formatKind,
            Exif: exif);

        var result = await photoOrganizer.OrganizeAsync(
            asset: asset,
            destinationRoot: destinationDir,
            sessionName: effectiveSessionName,
            cancellationToken: cts.Token);

        results.Add(result);

        if (result.WasSkippedAsDuplicate)
        {
            duplicateCount++;
            Console.WriteLine($"[SKIP] Duplicate: {fileInfo.Name}");
        }
        else
        {
            importedCount++;
            Console.WriteLine($"[OK]   {fileInfo.Name} -> {result.DestinationPath}");
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Ingestion cancelled.");
        break;
    }
    catch (Exception ex)
    {
        errorCount++;
        Console.Error.WriteLine($"[ERR]  {filePath}: {ex.Message}");
    }
}

if (results.Count > 0)
{
    var sampleDate = results[0].Asset.Exif.CapturedAt?.ToLocalTime().DateTime
                     ?? File.GetLastWriteTime(results[0].Asset.SourcePath);

    var safeSession = SanitizeSessionSegment(effectiveSessionName);
    var sessionDirectory = Path.Combine(
        destinationDir,
        sampleDate.ToString("yyyy", CultureInfo.InvariantCulture),
        sampleDate.ToString("MM", CultureInfo.InvariantCulture),
        safeSession);

    await summaryWriter.WriteSummaryAsync(
        sessionDirectory: sessionDirectory,
        sessionName: effectiveSessionName,
        results: results,
        cancellationToken: CancellationToken.None);

    Console.WriteLine($"Summary written to: {Path.Combine(sessionDirectory, "session-summary.md")}");
}

Console.WriteLine($"Done. Imported: {importedCount}, Duplicates: {duplicateCount}, Errors: {errorCount}");
return errorCount > 0 ? 1 : 0;

static string SanitizeSessionSegment(string name)
{
    var cleaned = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"[^\p{L}\p{N}\-_ ]+", "-");
    cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", "-");
    return string.IsNullOrWhiteSpace(cleaned) ? "session" : cleaned;
}

static void PrintUsage()
{
    Console.WriteLine("""
        LensLoom CLI
        Usage:
          lensloom ingest --source <path> --destination <path> [--session <name>] [--no-recursive]

        Options:
          -s, --source <path>       Source folder or SD card mount path (required)
          -d, --destination <path>  Destination library root path (required)
          --session <name>          Session folder name (defaults to current date yyyy-MM-dd)
          --no-recursive            Scan only the top-level source directory
          -h, --help                Show this help message
        """);
}