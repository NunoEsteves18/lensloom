using System.Globalization;
using LensLoom.Core.Abstractions;
using LensLoom.Core.Models;
using LensLoom.Ingest;
using LensLoom.Planner;

if (args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase) || args[0] is "--help" or "-h")
{
    PrintUsage();
    return 0;
}

return args[0].ToLowerInvariant() switch
{
    "ingest" => await RunIngestAsync(args[1..]),
    "planner" => await RunPlannerAsync(args[1..]),
    _ => UnknownCommand(args[0])
};

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: '{command}'");
    PrintUsage();
    return 1;
}

static async Task<int> RunIngestAsync(string[] args)
{
    string? sourceDir = null;
    string? destinationDir = null;
    string? sessionName = null;
    bool recursive = true;

    for (int i = 0; i < args.Length; i++)
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
                PrintIngestUsage();
                return 0;
            default:
                Console.Error.WriteLine($"Unknown or incomplete option for ingest: '{current}'");
                PrintIngestUsage();
                return 1;
        }
    }

    if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(destinationDir))
    {
        Console.Error.WriteLine("Both --source and --destination are required for 'ingest'.");
        PrintIngestUsage();
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
}

static async Task<int> RunPlannerAsync(string[] args)
{
    double latitude = 39.7436;   // Leiria por omissão
    double longitude = -8.8071;
    DateOnly targetDate = DateOnly.FromDateTime(DateTime.Now);
    ShootingStyle style = ShootingStyle.GoldenHour;
    double cloudCover = 35.0;
    double visibilityKm = 15.0;
    double windKmh = 10.0;
    double rainMmPerHour = 0.0;
    double temperatureC = 16.0;
    int minimumScore = 65;
    string sessionTitle = "Outdoor Session";
    string? alertLogPath = null;

    for (int i = 0; i < args.Length; i++)
    {
        var current = args[i];
        switch (current)
        {
            case "--lat" when i + 1 < args.Length && TryParseDouble(args[++i], out var lat):
                latitude = lat;
                break;
            case "--lon" when i + 1 < args.Length && TryParseDouble(args[++i], out var lon):
                longitude = lon;
                break;
            case "--date" when i + 1 < args.Length && DateOnly.TryParse(args[++i], CultureInfo.InvariantCulture, out var parsedDate):
                targetDate = parsedDate;
                break;
            case "--style" when i + 1 < args.Length && Enum.TryParse<ShootingStyle>(args[++i], ignoreCase: true, out var parsedStyle):
                style = parsedStyle;
                break;
            case "--clouds" when i + 1 < args.Length && TryParseDouble(args[++i], out var clouds):
                cloudCover = clouds;
                break;
            case "--visibility" when i + 1 < args.Length && TryParseDouble(args[++i], out var vis):
                visibilityKm = vis;
                break;
            case "--wind" when i + 1 < args.Length && TryParseDouble(args[++i], out var wind):
                windKmh = wind;
                break;
            case "--rain" when i + 1 < args.Length && TryParseDouble(args[++i], out var rain):
                rainMmPerHour = rain;
                break;
            case "--temp" when i + 1 < args.Length && TryParseDouble(args[++i], out var temp):
                temperatureC = temp;
                break;
            case "--min-score" when i + 1 < args.Length && int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minScore):
                minimumScore = minScore;
                break;
            case "--title" when i + 1 < args.Length:
                sessionTitle = args[++i];
                break;
            case "--alert-log" when i + 1 < args.Length:
                alertLogPath = args[++i];
                break;
            case "--help" or "-h":
                PrintPlannerUsage();
                return 0;
            default:
                Console.Error.WriteLine($"Unknown or invalid option for planner: '{current}'");
                PrintPlannerUsage();
                return 1;
        }
    }

    var coordinate = new GeoCoordinate(latitude, longitude);
    var calculator = new SolarWindowCalculator();
    var window = calculator.Calculate(coordinate, targetDate, TimeZoneInfo.Local);

    string Fmt(DateTimeOffset? dt) => dt?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "--:--";

    Console.WriteLine($"Location: {latitude.ToString("0.####", CultureInfo.InvariantCulture)}, {longitude.ToString("0.####", CultureInfo.InvariantCulture)} | Date: {targetDate:yyyy-MM-dd}");
    Console.WriteLine($"Sunrise / Sunset: {Fmt(window.Sunrise)} / {Fmt(window.Sunset)}");
    Console.WriteLine($"Morning Golden Hour: {Fmt(window.MorningGoldenHourStart)} - {Fmt(window.MorningGoldenHourEnd)}");
    Console.WriteLine($"Evening Golden Hour: {Fmt(window.EveningGoldenHourStart)} - {Fmt(window.EveningGoldenHourEnd)}");

    var snapshot = new WeatherSnapshot(
        Timestamp: DateTimeOffset.Now,
        CloudCoverPercent: cloudCover,
        VisibilityKm: visibilityKm,
        WindSpeedKmh: windKmh,
        PrecipitationMmPerHour: rainMmPerHour,
        TemperatureC: temperatureC);

    var evaluator = new WeatherConditionsEvaluator();
    var evaluation = evaluator.Evaluate(snapshot, style);

    Console.WriteLine($"Weather ({style}): {evaluation.Score}/100 ({evaluation.Rating})");
    foreach (var reason in evaluation.Reasons)
    {
        Console.WriteLine($"  - {reason}");
    }

    ISessionAlertDispatcher dispatcher = new ConsoleSessionAlertDispatcher();
    var alert = await dispatcher.EvaluateAndDispatchAsync(
        sessionTitle: sessionTitle,
        window: window,
        evaluation: evaluation,
        minimumScore: minimumScore,
        logFilePath: alertLogPath);

    if (alert is null)
    {
        Console.WriteLine($"No alert dispatched (score {evaluation.Score} below threshold {minimumScore}).");
    }

    return 0;
}

static bool TryParseDouble(string raw, out double value) =>
    double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

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
        Commands:
          ingest    Import RAW/JPEG files with EXIF, SHA-256 deduplication, and summary
          planner   Calculate solar/golden-hour windows, score weather, and dispatch alerts

        Run 'lensloom <command> --help' for command-specific options.
        """);
}

static void PrintIngestUsage()
{
    Console.WriteLine("""
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

static void PrintPlannerUsage()
{
    Console.WriteLine("""
        Usage:
          lensloom planner [options]

        Options:
          --lat <double>            Latitude in decimal degrees (default: 39.7436)
          --lon <double>            Longitude in decimal degrees (default: -8.8071)
          --date <yyyy-MM-dd>       Target date (default: today)
          --style <style>           GoldenHour | Landscape | Street | LongExposure (default: GoldenHour)
          --clouds <0..100>         Cloud cover percentage (default: 35)
          --visibility <km>         Visibility in km (default: 15)
          --wind <km/h>             Wind speed in km/h (default: 10)
          --rain <mm/h>             Precipitation in mm/h (default: 0)
          --temp <celsius>          Temperature in °C (default: 16)
          --min-score <0..100>      Minimum score required to trigger an alert (default: 65)
          --title <text>            Session title for alerts (default: "Outdoor Session")
          --alert-log <path>        Optional file path to append alert history
          -h, --help                Show this help message
        """);
}