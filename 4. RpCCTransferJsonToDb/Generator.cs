using Bogus;
using RpCCTransferJsonToDb.Services;

namespace RpCCTransferJsonToDb;

public static class Generator
{
    public static async Task RunAsync(string[] args, CallCenterRepository repository)
    {
        if (args.Length != 2 || !int.TryParse(args[1], out int count) || count <= 0)
        {
            Console.WriteLine("Usage: dotnet run -- generate <number_of_records>");
            return;
        }

        var issues = GenerateFakeCustomerIssues(count);
        var inserted = await repository.InsertManyAsync(issues);
        Console.WriteLine($"Inserted {inserted} fake records into Call_Center.CustomerIssues.");
    }

    private static List<CustomerIssue> GenerateFakeCustomerIssues(int count)
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
                    "late_package"    => latePackageTemplates,
                    "damaged_package" => damagedPackageTemplates,
                    "wrong_item"      => wrongItemTemplates,
                    _                 => throw new InvalidOperationException("Unknown classified reason")
                };
                return string.Format(f.PickRandom(templates), o.CustomerName, o.OrderNumber, f.Date.Past().ToString("yyyy-MM-dd"));
            })
            .RuleFor(o => o.EmployeeName, f => f.Name.FullName())
            .RuleFor(o => o.CustomerContactNr, f => f.Phone.PhoneNumber())
            .RuleFor(o => o.NewAddress, f => f.Address.FullAddress())
            .RuleFor(o => o.SentimentInitial, f => f.Make(1, () => f.PickRandom(initialSentiments)).ToArray())
            .RuleFor(o => o.SentimentFinal,   f => f.Make(1, () => f.PickRandom(finalSentiments)).ToArray())
            .RuleFor(o => o.SatisfactionScoreInitial, (f, o) => sentimentToScore[o.SentimentInitial![0]])
            .RuleFor(o => o.SatisfactionScoreFinal,   (f, o) => sentimentToScore[o.SentimentFinal![0]])
            .RuleFor(o => o.Eta, f => f.Date.Between(DateTime.Now.AddMonths(-1), DateTime.Now).ToString("yyyy-MM-dd"))
            .RuleFor(o => o.ActionItem, f => f.Make(2, () =>
                    f.PickRandom(new[] { "track_package", "contact_customer", "refund", "resend" })).ToArray())
            .RuleFor(o => o.CallDate, (f, o) => DateTime.Parse(o.Eta!).AddDays(-7));

        return faker.Generate(count);
    }
}
