using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RpCCAudioProcessApp;

public class CCAudioFileTriggerFunction
{
    private readonly ILogger<CCAudioFileTriggerFunction> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelemetryClient _telemetryClient;

    public CCAudioFileTriggerFunction(
        ILogger<CCAudioFileTriggerFunction> logger,
        IHttpClientFactory httpClientFactory,
        TelemetryClient telemetryClient)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _telemetryClient = telemetryClient;
    }

    [Function("CCAudioFileTriggerFunction")]
    public async Task Run(
        [BlobTrigger("call-center/{name}", Connection = "AzureWebJobsStorage")] Stream myBlob,
        string name)
    {
        _logger.LogInformation("C# Blob trigger function processed blob. Name: {Name} Size: {Size} bytes", name, myBlob.Length);
        _telemetryClient.TrackEvent("BlobProcessingStarted", new Dictionary<string, string>
        {
            { "BlobName", name },
            { "BlobSize", myBlob.Length.ToString() }
        });

        var apiUrl = Environment.GetEnvironmentVariable("SpeechToTextApiUrl");
        var apiKey = Environment.GetEnvironmentVariable("SpeechToTextApiKey");

        if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("API URL or Subscription Key is not configured.");
            throw new InvalidOperationException("API URL or Subscription Key is not configured.");
        }

        using var memoryStream = new MemoryStream();
        await myBlob.CopyToAsync(memoryStream);

        var byteArrayContent = new ByteArrayContent(memoryStream.ToArray());
        byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");

        using var multipartContent = new MultipartFormDataContent
        {
            { byteArrayContent, "audio", name }
        };

        var jsonDefinition = """
            {
                "locales": ["en-US"],
                "profanityFilterMode": "Masked",
                "channels": [0, 1]
            }
            """;
        var stringContent = new StringContent(jsonDefinition);
        stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        multipartContent.Add(stringContent, "definition");

        using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = multipartContent;

        var httpClient = _httpClientFactory.CreateClient("speech");
        using var response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("HTTP POST request was successful.");
            _telemetryClient.TrackEvent("HttpPostSuccess", new Dictionary<string, string>
            {
                { "BlobName", name },
                { "ApiUrl", apiUrl }
            });

            var conversation = ExtractConversation(jsonResponse);
            _logger.LogInformation("{Conversation}", conversation);
        }
        else
        {
            _logger.LogError("HTTP POST request failed with status code {StatusCode}.", response.StatusCode);
            var errorResponse = await response.Content.ReadAsStringAsync();
            _logger.LogError("Error response: {Error}", errorResponse);

            _telemetryClient.TrackEvent("HttpPostFailure", new Dictionary<string, string>
            {
                { "BlobName", name },
                { "StatusCode", response.StatusCode.ToString() }
            });
        }
    }

    private string? ExtractConversation(string jsonResponse)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(jsonResponse);
            var root = jsonDocument.RootElement;
            string? conversationText = null;

            if (root.TryGetProperty("combinedPhrases", out var combinedPhrases))
            {
                foreach (var phrase in combinedPhrases.EnumerateArray())
                {
                    if (phrase.TryGetProperty("text", out var textElement))
                    {
                        conversationText = textElement.GetString();
                    }
                }
            }
            else
            {
                _logger.LogWarning("No combinedPhrases found in the JSON response.");
            }
            return conversationText;
        }
        catch (JsonException ex)
        {
            _logger.LogError("Failed to parse JSON response: {Message}", ex.Message);
            return null;
        }
    }
}
