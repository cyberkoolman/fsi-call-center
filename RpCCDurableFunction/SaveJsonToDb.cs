using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System;

public static class SaveJsonToDb
{
    [FunctionName("SaveJsonToDb")]
    public static async Task Run([ActivityTrigger] string jsonData)
    {
        // log.LogInformation("SaveJsonToDb started.");
        // Deserialize JSON data to C# object
        CustomerIssue issue = JsonSerializer.Deserialize<CustomerIssue>(jsonData);

        var configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();

        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

        builder.DataSource = configuration["Database:Server"];
        builder.UserID = configuration["Database:UserId"];
        builder.Password = configuration["Database:Password"];
        builder.InitialCatalog = configuration["Database:InitialCatalog"];

        using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
        {
            connection.Open();       
            string query = @"
                INSERT INTO CustomerIssues (
                    ClassifiedReason, ResolveStatus, CallSummary, CustomerName, EmployeeName, OrderNumber, 
                    CustomerContactNr, NewAddress, SentimentInitial, SentimentFinal, SatisfactionScoreInitial, 
                    SatisfactionScoreFinal, Eta, ActionItem, CallDate
                ) VALUES (
                    @ClassifiedReason, @ResolveStatus, @CallSummary, @CustomerName, @EmployeeName, @OrderNumber, 
                    @CustomerContactNr, @NewAddress, @SentimentInitial, @SentimentFinal, @SatisfactionScoreInitial, 
                    @SatisfactionScoreFinal, @Eta, @ActionItem, @CallDate
                )";

            try 
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ClassifiedReason", issue.ClassifiedReason);
                    command.Parameters.AddWithValue("@ResolveStatus", issue.ResolveStatus);
                    command.Parameters.AddWithValue("@CallSummary", issue.CallSummary);
                    command.Parameters.AddWithValue("@CustomerName", issue.CustomerName);
                    command.Parameters.AddWithValue("@EmployeeName", issue.EmployeeName);
                    command.Parameters.AddWithValue("@OrderNumber", issue.OrderNumber);
                    command.Parameters.AddWithValue("@CustomerContactNr", issue.CustomerContactNr);
                    command.Parameters.AddWithValue("@NewAddress", issue.NewAddress);
                    command.Parameters.AddWithValue("@SentimentInitial", string.Join(",", issue.SentimentInitial));
                    command.Parameters.AddWithValue("@SentimentFinal", string.Join(",", issue.SentimentFinal));
                    command.Parameters.AddWithValue("@SatisfactionScoreInitial", issue.SatisfactionScoreInitial);
                    command.Parameters.AddWithValue("@SatisfactionScoreFinal", issue.SatisfactionScoreFinal);
                    command.Parameters.AddWithValue("@Eta", issue.Eta);
                    command.Parameters.AddWithValue("@ActionItem", string.Join(",", issue.ActionItem));
                    command.Parameters.AddWithValue("@CallDate", issue.CallDate);        

                    await command.ExecuteNonQueryAsync();
                }            
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.Message);
                // log.LogError($"SaveJsonToDb: Error inserting data: {e.Message}");
            }
        }

        // log.LogInformation("SaveJsonToDb: Data inserted successfully");
    }
}
