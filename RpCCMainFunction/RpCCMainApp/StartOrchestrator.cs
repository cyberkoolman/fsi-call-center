using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Net;
using System.Threading.Tasks;

public class StartOrchestrator
{
    private readonly IDurableClientFactory _durableClientFactory;
    private readonly ILogger<StartOrchestrator> _logger;

    public StartOrchestrator(IDurableClientFactory durableClientFactory, ILogger<StartOrchestrator> logger)
    {
        _durableClientFactory = durableClientFactory;
        _logger = logger;
    }

    [Function("StartOrchestrator")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        string audioFileName = requestBody; // Assume the request body contains the audio file name

        var durableClient = _durableClientFactory.CreateClient();
        string instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync("OrchestratorFunction", audioFileName);

        _logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync($"Orchestration started with ID = {instanceId}");

        return response;
    }
}
