using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

if (args.Length == 0)
{
    Console.WriteLine("Please provide a question as an argument.");
    return;
}

string question = args[0];

var configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();
string apiKey = configuration["AzureOpenAI:ApiKey"];
string deploymentChatName = configuration["AzureOpenAI:DeploymentChatName"];
string endpoint = configuration["AzureOpenAI:Endpoint"];

var kernelBuilder = Kernel.CreateBuilder();
// kernelBuilder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));
kernelBuilder.AddAzureOpenAIChatCompletion(deploymentChatName, endpoint, apiKey);
var kernel = kernelBuilder.Build();

// Step 2: Import a Plugin to the kernel
var nsqPluginDirectoryPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Plugins", "NlpToSqlPlugin");
kernel.ImportPluginFromPromptDirectory(nsqPluginDirectoryPath);
kernel.ImportPluginFromType<QueryDbPlugin>();

// Step 3: Enable function calling
PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };


IChatCompletionService chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
ChatHistory chatHistory = [];
// var input = "I want to know how many transactions in the last 3 months";
var input = question;
chatHistory.AddUserMessage(input);

var response = await chatCompletionService.GetChatMessageContentAsync(chatHistory, settings, kernel);
Console.WriteLine("Answer:\n");
Console.WriteLine(response);