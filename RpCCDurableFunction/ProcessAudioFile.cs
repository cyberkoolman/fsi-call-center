using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

public static class ProcessAudioFile
{
    private static readonly HttpClient httpClient = new HttpClient();

    [FunctionName("ProcessAudioFile")]
    public static async Task<string> Run([ActivityTrigger] string audioFileName, ILogger log)
    {
        var apiUrl = Environment.GetEnvironmentVariable("SpeechToTextApiUrl");
        var apiKey = Environment.GetEnvironmentVariable("SpeechToTextApiKey");

        if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey))
        {
            log.LogError("API URL or Subscription Key is not configured.");
            throw new InvalidOperationException("API URL or Subscription Key is not configured.");
        }

        var audioFilePath = Path.Combine(Environment.GetEnvironmentVariable("AudioFilesPath"), audioFileName);
        if (!File.Exists(audioFilePath))
        {
            log.LogError("Audio file not found.");
            throw new FileNotFoundException("Audio file not found.", audioFileName);
        }

        using (var audioFileStream = new FileStream(audioFilePath, FileMode.Open, FileAccess.Read))
        {
            var byteArrayContent = new ByteArrayContent(await File.ReadAllBytesAsync(audioFilePath));
            byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");

            var multipartContent = new MultipartFormDataContent();
            multipartContent.Add(byteArrayContent, "audio", audioFileName);

            var jsonDefinition = @"
            {
                ""locales"": [""en-US""],
                ""profanityFilterMode"": ""Masked"",
                ""channels"": [0, 1]
            }";
            var stringContent = new StringContent(jsonDefinition);
            stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            multipartContent.Add(stringContent, "definition");

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = multipartContent;

            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                log.LogInformation("HTTP POST request was successful.");
                return jsonResponse;
            }
            else
            {
                log.LogError($"HTTP POST request failed with status code {response.StatusCode}.");
                var errorResponse = await response.Content.ReadAsStringAsync();
                log.LogError($"Error response: {errorResponse}");
                throw new HttpRequestException($"HTTP POST request failed with status code {response.StatusCode}.");
            }
        }
    }
}