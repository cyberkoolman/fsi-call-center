using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RpCCTransferJsonToDb;
using RpCCTransferJsonToDb.Configuration;
using RpCCTransferJsonToDb.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .Build();

var settings = config.Get<AppSettings>()
    ?? throw new InvalidOperationException("Failed to load appsettings.json.");

var repository = new CallCenterRepository(settings);
await repository.EnsureSchemaAsync();

if (args.Length > 0 && args[0].Equals("generate", StringComparison.OrdinalIgnoreCase))
{
    await Generator.RunAsync(args, repository);
    return;
}

var jsonFilePath = Path.Combine(AppContext.BaseDirectory, "output.json");
var jsonData = await File.ReadAllTextAsync(jsonFilePath);

var issue = JsonSerializer.Deserialize<CustomerIssue>(jsonData)
    ?? throw new InvalidOperationException("Failed to parse output.json.");

await repository.InsertAsync(issue);
Console.WriteLine($"Inserted 1 record from {jsonFilePath} into Call_Center.CustomerIssues.");
