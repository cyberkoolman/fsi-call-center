using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Npgsql;

if (args.Length > 0 && args[0].ToLower() == "generate")
{
    Generator.Generate(args);
    return;
}

string jsonFilePath = "./output.json";
string jsonData = File.ReadAllText(jsonFilePath);

// Deserialize JSON data to C# object
CustomerIssue issue = JsonSerializer.Deserialize<CustomerIssue>(jsonData);

var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder();

builder.Host = configuration["Database:Host"];
builder.Port = int.Parse(configuration["Database:Port"]);
builder.Database = configuration["Database:Database"];
builder.Username = configuration["Database:Username"];
builder.Password = configuration["Database:Password"];

using (NpgsqlConnection connection = new NpgsqlConnection(builder.ConnectionString))
{
    connection.Open();         string query = @"
        INSERT INTO customer_issues (
            classified_reason, resolve_status, call_summary, customer_name, employee_name, order_number, 
            customer_contact_nr, new_address, sentiment_initial, sentiment_final, satisfaction_score_initial, 
            satisfaction_score_final, eta, action_item, call_date
        ) VALUES (
            @classified_reason, @resolve_status, @call_summary, @customer_name, @employee_name, @order_number, 
            @customer_contact_nr, @new_address, @sentiment_initial, @sentiment_final, @satisfaction_score_initial, 
            @satisfaction_score_final, @eta, @action_item, @call_date
        )";

    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))    {
        command.Parameters.AddWithValue("@classified_reason", issue.ClassifiedReason);
        command.Parameters.AddWithValue("@resolve_status", issue.ResolveStatus);
        command.Parameters.AddWithValue("@call_summary", issue.CallSummary);
        command.Parameters.AddWithValue("@customer_name", issue.CustomerName);
        command.Parameters.AddWithValue("@employee_name", issue.EmployeeName);
        command.Parameters.AddWithValue("@order_number", issue.OrderNumber);
        command.Parameters.AddWithValue("@customer_contact_nr", issue.CustomerContactNr);
        command.Parameters.AddWithValue("@new_address", issue.NewAddress);
        command.Parameters.AddWithValue("@sentiment_initial", string.Join(",", issue.SentimentInitial));
        command.Parameters.AddWithValue("@sentiment_final", string.Join(",", issue.SentimentFinal));
        command.Parameters.AddWithValue("@satisfaction_score_initial", issue.SatisfactionScoreInitial);
        command.Parameters.AddWithValue("@satisfaction_score_final", issue.SatisfactionScoreFinal);
        command.Parameters.AddWithValue("@eta", issue.Eta);
        command.Parameters.AddWithValue("@action_item", string.Join(",", issue.ActionItem));
        command.Parameters.AddWithValue("@call_date", issue.CallDate);        

        command.ExecuteNonQuery();
    }
}

Console.WriteLine("Data inserted successfully.");