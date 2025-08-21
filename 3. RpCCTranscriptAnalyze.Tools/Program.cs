using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();
string apiKey = configuration["AzureOpenAI:ApiKey"];
string deploymentChatName = configuration["AzureOpenAI:DeploymentChatName"];
string endpoint = configuration["AzureOpenAI:Endpoint"];

var kernelBuilder = Kernel.CreateBuilder();
// kernelBuilder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));
kernelBuilder.AddAzureOpenAIChatCompletion(deploymentChatName, endpoint, apiKey);
kernelBuilder.Plugins.AddFromType<TimeInformation>();    

var kernel = kernelBuilder.Build();

string filePath = "./input.txt";
var input = File.ReadAllText(filePath);

// Step 2: Define and Configure Inline Prompt and a sample input text
string promptString = $@"
{input}

Above is a customer call transcription between customer call employee at Contoso Services and a customer. Extract the following information:

- Classified reason for contact (can be one of 'broken_item', 'damaged_package', 'lost_package', 'late_package', 'address_change', 'new_package_request')
- Is the problem resolved? (can be one of 'resolved', 'unresolved')
- Call summary (in max 100 characters)
- Name of customer
- Name of call center employee
- Customer order number
- Customer contact information (if not mentioned, then 'N/A')
- New customer address (if the call reason is to change address, else 'N/A')
- Customer's sentiment in the beginning of the call (can be one or more of 'calm', 'complaining', 'angry', 'frustrated', 'unhappy', 'neutral', 'happy')
- Customer's sentiment in the end of the call (can be one or more of 'calm', 'complaining', 'angry', 'frustrated', 'unhappy', 'neutral', 'happy')
- How satisfied is the customer in the beginning of the call, 0 being very unsatisfied and 10 being very satisfied
- How satisfied is the customer in the end of the call, 0 being very unsatisfied and 10 being very satisfied
- Estimated time of arrival of package
- Action item (can be one or more of 'no_action', 'track_package', 'inquire_package_status', 'make_address_change', 'cancel_order', 'contact_customer')
- Call date (just enter today)

If customer is satisfied in the end, there is no follow up needed. Else, follow up with the relevant internal department to check the status.

Extract JSON with keys classified_reason, resolve_status, call_summary, customer_name, employee_name, order_number, customer_contact_nr, new_address, sentiment_initial, sentiment_final, satisfaction_score_initial, satisfaction_score_final, eta, action_item, call_date.
";

// Step 3. Invoke the kernel with a prompt and allow the AI to automatically invoke functions
PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

var summaryResult = await kernel.InvokePromptAsync(promptString, new(settings));

Console.WriteLine(summaryResult);