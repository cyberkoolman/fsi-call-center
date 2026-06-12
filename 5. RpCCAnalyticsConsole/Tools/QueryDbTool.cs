using System.ComponentModel;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using RpCCAnalyticsConsole.Configuration;

namespace RpCCAnalyticsConsole.Tools;

/// <summary>
/// Native function exposed to the MAF agent. The agent emits a Transact-SQL
/// SELECT, this tool runs it against the Call_Center database, and returns
/// the rows as a JSON string the agent can read for the final answer.
/// </summary>
public class QueryDbTool
{
    private readonly string _connectionString;

    public QueryDbTool(AppSettings settings)
    {
        _connectionString = settings.ConnectionStrings.CallCenter;
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:CallCenter is not configured.");
    }

    [Description(
        "Executes a Transact-SQL SELECT statement against the Call_Center database " +
        "and returns the result rows as JSON. Only SELECT statements are permitted; " +
        "any DDL or DML (INSERT, UPDATE, DELETE, DROP, ALTER, MERGE, EXEC) is rejected.")]
    public async Task<string> ExecuteSqlAsync(
        [Description("A single Transact-SQL SELECT statement targeting the CustomerIssues table.")]
        string sql)
    {
        Console.WriteLine();
        Console.WriteLine("───[ tool: ExecuteSqlAsync ]───────────────────────────────");
        Console.WriteLine(sql.Trim());
        Console.WriteLine("────────────────────────────────────────────────────────────");

        if (!IsSelectOnly(sql))
            return "ERROR: Only SELECT statements are permitted.";

        var rows = new List<Dictionary<string, object?>>();
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            var columnNames = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName).ToArray();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(columnNames.Length);
                for (int i = 0; i < columnNames.Length; i++)
                    row[columnNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
        }
        catch (SqlException ex)
        {
            return $"ERROR: {ex.Message}";
        }

        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = false });
    }

    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE",
        "MERGE", "EXEC", "EXECUTE", "CREATE", "GRANT", "REVOKE"
    ];

    private static bool IsSelectOnly(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;

        var trimmed = sql.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("WITH",   StringComparison.OrdinalIgnoreCase))
            return false;

        var upper = sql.ToUpperInvariant();
        foreach (var kw in ForbiddenKeywords)
        {
            // word-boundary check via spaces / punctuation
            if (upper.Contains($" {kw} ") || upper.Contains($"\n{kw} ") || upper.Contains($";{kw}"))
                return false;
        }
        return true;
    }
}
