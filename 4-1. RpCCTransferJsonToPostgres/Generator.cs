using Microsoft.Extensions.Configuration;
using Bogus;
using Npgsql;
public class Generator
{
    public static void Generate(string[] args)
    {
        if (args.Length != 2 || args[0].ToLower() != "generate" || !int.TryParse(args[1], out int count))
        {
            Console.WriteLine("Usage: dotnet run generate <number_of_records>");
            return;
        }

        // Generate fake data
        var issues = GenerateFakeCustomerIssues(count);        var configuration = new ConfigurationBuilder()
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
            connection.Open();   

            foreach (var issue in issues)
            {                string query = @"
                    INSERT INTO customer_issues (
                        classified_reason, resolve_status, call_summary, customer_name, employee_name, order_number, 
                        customer_contact_nr, new_address, sentiment_initial, sentiment_final, satisfaction_score_initial, 
                        satisfaction_score_final, eta, action_item, call_date
                    ) VALUES (
                        @classified_reason, @resolve_status, @call_summary, @customer_name, @employee_name, @order_number, 
                        @customer_contact_nr, @new_address, @sentiment_initial, @sentiment_final, @satisfaction_score_initial, 
                        @satisfaction_score_final, @eta, @action_item, @call_date
                    )";

                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))                {
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
        }

        Console.WriteLine($"{count} records inserted successfully.");
    }

    static List<CustomerIssue> GenerateFakeCustomerIssues(int count)
    {
        var sentimentToScore = new Dictionary<string, int>
        {
            { "angry", 1 },
            { "frustrated", 2 },
            { "unhappy", 3 },
            { "neutral", 4 },
            { "happy", 5 }
        };

        var initialSentiments = new[] { "angry", "frustrated", "unhappy" };
        var finalSentiments = new[] { "angry", "frustrated", "unhappy", "neutral", "happy" };

        var latePackageTemplates = new[]
        {
            "Customer {0} called regarding their order {1}. The package was supposed to arrive on {2} but has not been delivered yet.",
            "Customer {0} called to complain about the delay in their order {1}. The expected delivery date was {2}.",
            "Customer {0} is frustrated because their order {1} has been delayed multiple times. The latest expected delivery date was {2}."
        };

        var damagedPackageTemplates = new[]
        {
            "Customer {0} reported that their package with order number {1} arrived damaged. They are requesting a replacement.",
            "Customer {0} is requesting a refund for their order {1} because the package was damaged upon arrival.",
            "Customer {0} mentioned that their package with order number {1} was damaged during transit and they need a replacement."
        };

        var wrongItemTemplates = new[]
        {
            "Customer {0} is unhappy because the wrong item was delivered for order {1}. They need the correct item sent as soon as possible.",
            "Customer {0} called to report that the wrong item was delivered for order {1}. They are requesting an exchange.",
            "Customer {0} received the wrong item for order {1} and is requesting a refund or replacement."
        };

        var faker = new Faker<CustomerIssue>()
            .RuleFor(o => o.ClassifiedReason, f => 
                    f.PickRandom(new[] { "late_package", "damaged_package", "wrong_item" }))
            .RuleFor(o => o.ResolveStatus, f => 
                    f.PickRandom(new[] { "resolved", "unresolved", "pending" }))
            .RuleFor(o => o.CustomerName, f => f.Name.FullName())
            .RuleFor(o => o.OrderNumber, f => $"{f.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}-{f.Random.Int(100, 999)}")
            .RuleFor(o => o.CallSummary, (f, o) =>
            {
                string[] templates = o.ClassifiedReason switch
                {
                    "late_package" => latePackageTemplates,
                    "damaged_package" => damagedPackageTemplates,
                    "wrong_item" => wrongItemTemplates,
                    _ => throw new InvalidOperationException("Unknown classified reason")
                };
                return string.Format(f.PickRandom(templates), o.CustomerName, o.OrderNumber, f.Date.Past().ToString("yyyy-MM-dd"));
            })            
            .RuleFor(o => o.EmployeeName, f => f.Name.FullName())
            .RuleFor(o => o.CustomerContactNr, f => f.Phone.PhoneNumber())
            .RuleFor(o => o.NewAddress, f => f.Address.FullAddress())
            .RuleFor(o => o.SentimentInitial, f => f.Make(1, () => f.PickRandom(initialSentiments)).ToArray())
            .RuleFor(o => o.SentimentFinal, f => f.Make(1, () => f.PickRandom(finalSentiments)).ToArray())
            .RuleFor(o => o.SatisfactionScoreInitial, (f, o) => sentimentToScore[o.SentimentInitial[0]])
            .RuleFor(o => o.SatisfactionScoreFinal, (f, o) => sentimentToScore[o.SentimentFinal[0]])
            .RuleFor(o => o.Eta, f => f.Date.Between(DateTime.Now.AddMonths(-1), DateTime.Now).ToString("yyyy-MM-dd"))
            .RuleFor(o => o.ActionItem, f => f.Make(2, () => 
                    f.PickRandom(new[] { "track_package", "contact_customer", "refund", "resend" })).ToArray())
            .RuleFor(o => o.CallDate, (f, o) => DateTime.Parse(o.Eta).AddDays(-7));

        return faker.Generate(count);
    }
}