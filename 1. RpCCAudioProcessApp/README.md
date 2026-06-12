# 1. RpCCAudioProcessApp — Audio Transcription Function

🎤 Azure Function (isolated worker, **.NET 9**) that transcribes call-center audio recordings as soon as they land in Blob Storage.

## What it does

1. A blob is uploaded to the `call-center` container of the configured Azure Storage account.
2. The **`CCAudioFileTriggerFunction`** blob trigger fires.
3. The function reads the audio bytes and POSTs them as `multipart/form-data` to **Azure AI Speech — Fast Transcription** (REST endpoint `/speechtotext/transcriptions:transcribe?api-version=2024-11-15`).
4. The response JSON is parsed; the combined transcript text (`combinedPhrases[].text`) is logged.
5. Telemetry events (`BlobProcessingStarted`, `HttpPostSuccess`, `HttpPostFailure`) are sent to Application Insights.

> The Fast Transcription endpoint is a synchronous REST API that ships with Azure AI Speech / Azure AI Foundry. It returns full diarized transcripts in seconds for short files — no async polling required.

## Tech stack

| Component | Version / Service |
|---|---|
| Runtime | .NET 9 |
| Functions model | Azure Functions **isolated worker** (`Microsoft.Azure.Functions.Worker` 2.x) |
| Trigger | `BlobTrigger("call-center/{name}", Connection = "AzureWebJobsStorage")` |
| Transcription | Azure AI Speech **Fast Transcription** (`/speechtotext/transcriptions:transcribe`, `api-version=2024-11-15`) |
| Storage auth | Identity-based (`AzureWebJobsStorage__blobServiceUri`, etc.) via `DefaultAzureCredential` |
| Telemetry | Application Insights worker service + `TelemetryClient` |
| HTTP | `IHttpClientFactory` (named client `speech`) |

## Configuration (`local.settings.json`)

See `local.settings.Sample.json` for the full template. Required keys:

```json
{
  "Values": {
    "AzureWebJobsStorage__blobServiceUri":  "https://<account>.blob.core.windows.net",
    "AzureWebJobsStorage__queueServiceUri": "https://<account>.queue.core.windows.net",
    "AzureWebJobsStorage__tableServiceUri": "https://<account>.table.core.windows.net",
    "AzureWebJobsSecretStorageType": "files",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "InstrumentationKey=...",
    "SpeechToTextApiUrl": "https://<aoai-or-speech-resource>.cognitiveservices.azure.com/speechtotext/transcriptions:transcribe?api-version=2024-11-15",
    "SpeechToTextApiKey": "<speech-resource-key>"
  }
}
```

### Required Azure RBAC (one-time, for the developer / Managed Identity)

On the storage account (`Microsoft.Storage/storageAccounts/<account>`):

- **Storage Blob Data Owner**
- **Storage Queue Data Contributor**
- **Storage Table Data Contributor**

The storage account in this sample has `allowSharedKeyAccess = false`, so Entra ID auth is the only option. Locally, `DefaultAzureCredential` will use your `az login` token.

## Run locally

```powershell
# from this folder
az login                # ensure your account is signed in
dotnet run              # builds and starts the Functions host
```

Then upload an audio file to trigger the function. The repo root contains an `upload.cmd` helper:

```cmd
upload.cmd
```

You'll see the function fire and the transcript logged to the console.

## Files

| File | Role |
|---|---|
| `Program.cs` | Host startup (`FunctionsApplication.CreateBuilder`, App Insights, `IHttpClientFactory`) |
| `CCAudioFileTriggerFunction.cs` | The blob-triggered function (DI-injected `ILogger`, `IHttpClientFactory`, `TelemetryClient`) |
| `host.json` | Functions host + App Insights sampling config |
| `local.settings.Sample.json` | Template for local secrets/config (copy to `local.settings.json`) |
| `RpCCAudioProcessApp.csproj` | net9.0 project, isolated-worker package references |

## Migration notes (from the original .NET 6 in-process version)

- `Microsoft.NET.Sdk.Functions` + `Microsoft.Azure.WebJobs.Extensions.Storage` → `Microsoft.Azure.Functions.Worker.*` + `Worker.Extensions.Storage.Blobs`.
- `[FunctionName]` static method with `ILogger log` parameter → `[Function]` instance method with constructor DI.
- `new TelemetryClient(TelemetryConfiguration.CreateDefault())` → injected `TelemetryClient` via `AddApplicationInsightsTelemetryWorkerService()` + `ConfigureFunctionsApplicationInsights()`.
- `static HttpClient` → `IHttpClientFactory` (`AddHttpClient("speech")`).
- `AzureWebJobsStorage` connection string → `AzureWebJobsStorage__blobServiceUri` (identity-based).
- `APPINSIGHTS_INSTRUMENTATIONKEY` → `APPLICATIONINSIGHTS_CONNECTION_STRING`.
