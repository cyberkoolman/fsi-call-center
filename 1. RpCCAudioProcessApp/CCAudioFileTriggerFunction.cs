using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System.Collections.Generic;

namespace RpCCAudioProcessApp
{
    public static class CCAudioFileTriggerFunction
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly TelemetryClient telemetryClient = new TelemetryClient(TelemetryConfiguration.CreateDefault());

        [FunctionName("CCAudioFileTriggerFunction")]
        public static async Task Run(
            [BlobTrigger("call-center/{name}", Connection = "AzureWebJobsStorage")] Stream myBlob, 
            string name, 
            ILogger log)        
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
            telemetryClient.TrackEvent("BlobProcessingStarted", new Dictionary<string, string>
            {
                { "BlobName", name },
                { "BlobSize", myBlob.Length.ToString() }
            });
            
            // Read the URL and subscription key from environment variables
            var apiUrl = Environment.GetEnvironmentVariable("SpeechToTextApiUrl");
            var apiKey = Environment.GetEnvironmentVariable("SpeechToTextApiKey");

            if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey))
            {
                log.LogError("API URL or Subscription Key is not configured.");
                throw new InvalidOperationException("API URL or Subscription Key is not configured.");
            }

            // Read the blob content
            using (var memoryStream = new MemoryStream())
            {
                await myBlob.CopyToAsync(memoryStream);
                var byteArrayContent = new ByteArrayContent(memoryStream.ToArray());
                byteArrayContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");

            // Create the multipart form data content
                var multipartContent = new MultipartFormDataContent();
                multipartContent.Add(byteArrayContent, "audio", name);

                // Add the JSON definition
                var jsonDefinition = @"
                {
                    ""locales"": [""en-US""],
                    ""profanityFilterMode"": ""Masked"",
                    ""channels"": [0, 1]
                }";
                var stringContent = new StringContent(jsonDefinition);
                stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                multipartContent.Add(stringContent, "definition");

                // Set up the HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = multipartContent;

                // Send the HTTP POST request
                var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    log.LogInformation("HTTP POST request was successful.");
                    telemetryClient.TrackEvent("HttpPostSuccess", new Dictionary<string, string>
                    {
                        { "BlobName", name },
                        { "ApiUrl", apiUrl }
                    });

                    // Extract conversation from JSON response
                    var conversation = ExtractConversation(jsonResponse, log);
                    log.LogInformation(conversation);
                }
                else
                {
                    log.LogError($"HTTP POST request failed with status code {response.StatusCode}.");
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    log.LogError($"Error response: {errorResponse}");

                    telemetryClient.TrackEvent("HttpPostFailure", new Dictionary<string, string>
                    {
                        { "BlobName", name },
                        { "StatusCode", response.StatusCode.ToString() }
                    });
                }        
            }
        }

        private static string ExtractConversation(string jsonResponse, ILogger log)
        {
            try
            {
                var jsonDocument = JsonDocument.Parse(jsonResponse);
                var root = jsonDocument.RootElement;
                var conversationText = "";

                if (root.TryGetProperty("combinedPhrases", out JsonElement combinedPhrases))
                {
                    foreach (var phrase in combinedPhrases.EnumerateArray())
                    {
                        if (phrase.TryGetProperty("text", out JsonElement textElement))
                        {
                            conversationText = textElement.GetString();
                        }
                    }
                }
                else
                {
                    log.LogWarning("No combinedPhrases found in the JSON response.");
                }
                return conversationText;
            }
            catch (JsonException ex)
            {
                log.LogError($"Failed to parse JSON response: {ex.Message}");
                return null;
            }
        }       
    }
}
