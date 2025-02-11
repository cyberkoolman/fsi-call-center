using Microsoft.Extensions.Configuration;
using Bogus;
using Microsoft.Data.SqlClient;
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
        var issues = GenerateFakeCustomerIssues(count);

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

            foreach (var issue in issues)
            {
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