using Microsoft.Data.SqlClient;
using RpCCTransferJsonToDb.Configuration;

namespace RpCCTransferJsonToDb.Services;

/// <summary>
/// Thin data-access layer over the Call_Center.CustomerIssues table.
/// Centralizes the SQL so Program.cs (single-row from JSON) and
/// Generator.cs (bulk fake rows) share one INSERT path.
/// </summary>
public class CallCenterRepository
{
    private readonly string _connectionString;

    public CallCenterRepository(AppSettings settings)
    {
        _connectionString = settings.ConnectionStrings.CallCenter;
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:CallCenter is not configured.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        const string ddl = """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerIssues')
            BEGIN
                CREATE TABLE CustomerIssues (
                    Id                        INT IDENTITY(1,1) PRIMARY KEY,
                    ClassifiedReason          NVARCHAR(64)  NOT NULL,
                    ResolveStatus             NVARCHAR(32)  NOT NULL,
                    CallSummary               NVARCHAR(500) NULL,
                    CustomerName              NVARCHAR(128) NULL,
                    EmployeeName              NVARCHAR(128) NULL,
                    OrderNumber               NVARCHAR(32)  NULL,
                    CustomerContactNr         NVARCHAR(32)  NULL,
                    NewAddress                NVARCHAR(256) NULL,
                    SentimentInitial          NVARCHAR(128) NULL,
                    SentimentFinal            NVARCHAR(128) NULL,
                    SatisfactionScoreInitial  INT           NOT NULL,
                    SatisfactionScoreFinal    INT           NOT NULL,
                    Eta                       NVARCHAR(32)  NULL,
                    ActionItem                NVARCHAR(256) NULL,
                    CallDate                  DATE          NOT NULL,
                    InsertedAt                DATETIME2     NOT NULL CONSTRAINT DF_CustomerIssues_InsertedAt DEFAULT SYSUTCDATETIME()
                );
            END
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(ddl, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task InsertAsync(CustomerIssue issue, CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = BuildInsertCommand(connection, issue);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> InsertManyAsync(
        IEnumerable<CustomerIssue> issues,
        CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        int count = 0;
        try
        {
            foreach (var issue in issues)
            {
                await using var command = BuildInsertCommand(connection, issue, transaction);
                await command.ExecuteNonQueryAsync(ct);
                count++;
            }
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
        return count;
    }

    private static SqlCommand BuildInsertCommand(
        SqlConnection connection,
        CustomerIssue issue,
        SqlTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO CustomerIssues (
                ClassifiedReason, ResolveStatus, CallSummary, CustomerName, EmployeeName, OrderNumber,
                CustomerContactNr, NewAddress, SentimentInitial, SentimentFinal, SatisfactionScoreInitial,
                SatisfactionScoreFinal, Eta, ActionItem, CallDate
            ) VALUES (
                @ClassifiedReason, @ResolveStatus, @CallSummary, @CustomerName, @EmployeeName, @OrderNumber,
                @CustomerContactNr, @NewAddress, @SentimentInitial, @SentimentFinal, @SatisfactionScoreInitial,
                @SatisfactionScoreFinal, @Eta, @ActionItem, @CallDate
            )
            """;

        var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@ClassifiedReason", (object?)issue.ClassifiedReason ?? DBNull.Value);
        command.Parameters.AddWithValue("@ResolveStatus", (object?)issue.ResolveStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("@CallSummary", (object?)issue.CallSummary ?? DBNull.Value);
        command.Parameters.AddWithValue("@CustomerName", (object?)issue.CustomerName ?? DBNull.Value);
        command.Parameters.AddWithValue("@EmployeeName", (object?)issue.EmployeeName ?? DBNull.Value);
        command.Parameters.AddWithValue("@OrderNumber", (object?)issue.OrderNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("@CustomerContactNr", (object?)issue.CustomerContactNr ?? DBNull.Value);
        command.Parameters.AddWithValue("@NewAddress", (object?)issue.NewAddress ?? DBNull.Value);
        command.Parameters.AddWithValue("@SentimentInitial", string.Join(",", issue.SentimentInitial ?? []));
        command.Parameters.AddWithValue("@SentimentFinal", string.Join(",", issue.SentimentFinal ?? []));
        command.Parameters.AddWithValue("@SatisfactionScoreInitial", issue.SatisfactionScoreInitial);
        command.Parameters.AddWithValue("@SatisfactionScoreFinal", issue.SatisfactionScoreFinal);
        command.Parameters.AddWithValue("@Eta", (object?)issue.Eta ?? DBNull.Value);
        command.Parameters.AddWithValue("@ActionItem", string.Join(",", issue.ActionItem ?? []));
        command.Parameters.AddWithValue("@CallDate", issue.CallDate);
        return command;
    }
}
