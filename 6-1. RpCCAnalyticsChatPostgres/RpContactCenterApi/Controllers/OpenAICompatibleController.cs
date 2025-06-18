using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RpContactCenter.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RpContactCenter.Controllers
{
    [ApiController]
    [Route("v1")]
    public class OpenAICompatibleController : ControllerBase    {
        private readonly Kernel _kernel;
        private readonly PromptExecutionSettings _settings;
        private readonly ILogger<OpenAICompatibleController> _logger;
        private readonly IConfiguration _configuration;
        private static readonly ConcurrentDictionary<string, ChatHistory> ChatHistories = new();

        public OpenAICompatibleController(Kernel kernel, PromptExecutionSettings settings, ILogger<OpenAICompatibleController> logger, IConfiguration configuration)
        {
            _kernel = kernel;
            _settings = settings;
            _logger = logger;
            _configuration = configuration;
        }        [HttpPost("chat/completions")]
        public async Task<IActionResult> ChatCompletions([FromBody] ChatCompletionRequest request)
        {
            try
            {
                // Optional: Check for API key in header (but don't require it for development)
                var apiKey = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogInformation("API request with key: {ApiKey}", apiKey.Substring(0, Math.Min(8, apiKey.Length)) + "...");
                }

                _logger.LogInformation("Processing chat completion request for model: {Model}", request.Model);

                if (request.Messages == null || !request.Messages.Any())
                {
                    return BadRequest(new { error = new { message = "At least one message is required", type = "invalid_request_error" } });
                }

                // Convert OpenAI messages to ChatHistory
                var chatHistory = new ChatHistory();
                foreach (var message in request.Messages)
                {
                    switch (message.Role.ToLower())
                    {
                        case "system":
                            chatHistory.AddSystemMessage(message.Content);
                            break;
                        case "user":
                            chatHistory.AddUserMessage(message.Content);
                            break;
                        case "assistant":
                            chatHistory.AddAssistantMessage(message.Content);
                            break;
                        default:
                            _logger.LogWarning("Unknown message role: {Role}", message.Role);
                            break;
                    }
                }

                // Configure settings based on request parameters
                var executionSettings = new PromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                };

                if (request.Temperature != 0.7f)
                {
                    executionSettings.ExtensionData = new Dictionary<string, object>
                    {
                        ["temperature"] = request.Temperature
                    };
                }

                if (request.MaxTokens.HasValue)
                {
                    if (executionSettings.ExtensionData == null)
                        executionSettings.ExtensionData = new Dictionary<string, object>();
                    executionSettings.ExtensionData["max_tokens"] = request.MaxTokens.Value;
                }

                var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

                if (request.Stream)
                {
                    return await HandleStreamingResponse(chatCompletionService, chatHistory, executionSettings, request);
                }
                else
                {
                    return await HandleNonStreamingResponse(chatCompletionService, chatHistory, executionSettings, request);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat completion request");
                return StatusCode(500, new
                {
                    error = new
                    {
                        message = "Internal server error",
                        type = "internal_error"
                    }
                });
            }
        }

        private async Task<IActionResult> HandleNonStreamingResponse(
            IChatCompletionService chatCompletionService,
            ChatHistory chatHistory,
            PromptExecutionSettings settings,
            ChatCompletionRequest request)
        {
            var response = await chatCompletionService.GetChatMessageContentAsync(chatHistory, settings, _kernel);

            var openAIResponse = new ChatCompletionResponse
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<ChatChoice>
                {
                    new ChatChoice
                    {
                        Index = 0,
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content = response.Content ?? string.Empty
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = EstimateTokens(string.Join(" ", chatHistory.Select(m => m.Content))),
                    CompletionTokens = EstimateTokens(response.Content ?? string.Empty),
                    TotalTokens = 0
                }
            };

            openAIResponse.Usage.TotalTokens = openAIResponse.Usage.PromptTokens + openAIResponse.Usage.CompletionTokens;

            return Ok(openAIResponse);
        }

        private async Task<IActionResult> HandleStreamingResponse(
            IChatCompletionService chatCompletionService,
            ChatHistory chatHistory,
            PromptExecutionSettings settings,
            ChatCompletionRequest request)
        {            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            var completionId = $"chatcmpl-{Guid.NewGuid():N}";
            var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                await foreach (var streamingContent in chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, settings, _kernel))
                {
                    var chunk = new ChatCompletionChunk
                    {
                        Id = completionId,
                        Created = created,
                        Model = request.Model,
                        Choices = new List<ChatChoiceChunk>
                        {
                            new ChatChoiceChunk
                            {
                                Index = 0,
                                Delta = new ChatMessage
                                {
                                    Role = "assistant",
                                    Content = streamingContent.Content ?? string.Empty
                                }
                            }
                        }
                    };

                    var json = JsonSerializer.Serialize(chunk);
                    await Response.WriteAsync($"data: {json}\n\n");
                    await Response.Body.FlushAsync();
                }

                // Send final chunk
                var finalChunk = new ChatCompletionChunk
                {
                    Id = completionId,
                    Created = created,
                    Model = request.Model,
                    Choices = new List<ChatChoiceChunk>
                    {
                        new ChatChoiceChunk
                        {
                            Index = 0,
                            Delta = new ChatMessage(),
                            FinishReason = "stop"
                        }
                    }
                };

                var finalJson = JsonSerializer.Serialize(finalChunk);
                await Response.WriteAsync($"data: {finalJson}\n\n");
                await Response.WriteAsync("data: [DONE]\n\n");
                await Response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during streaming response");
                await Response.WriteAsync($"data: {{\"error\": \"Internal server error\"}}\n\n");
            }

            return new EmptyResult();
        }        [HttpGet("models")]
        public IActionResult GetModels()
        {
            // Read actual deployment names from configuration
            var chatDeploymentName = _configuration["AzureOpenAI:DeploymentChatName"];

            var modelList = new List<ModelInfo>();

            // Add chat model with user-friendly name
            if (!string.IsNullOrEmpty(chatDeploymentName))
            {
                modelList.Add(new ModelInfo
                {
                    Id = "Contact Center Database",  // User-friendly name for OpenWebUI
                    Object = "model",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    OwnedBy = "rpcontactcenter"
                });
            }

            // Fallback to default models if no configuration found
            if (!modelList.Any())
            {
                modelList.AddRange(new[]
                {
                    new ModelInfo
                    {
                        Id = "Contact Center Database",
                        Object = "model",
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        OwnedBy = "rpcontactcenter"
                    }
                });
            }

            var response = new ModelsResponse
            {
                Data = modelList
            };

            return Ok(response);
        }// OpenWebUI sometimes calls this endpoint to validate the connection
        [HttpGet("")]
        [HttpGet("v1")]
        public IActionResult GetApiInfo()
        {
            return Ok(new
            {
                message = "RP Contact Center API - OpenAI Compatible",
                version = "1.0.0",
                status = "active",
                endpoints = new[]
                {
                    "/v1/chat/completions",
                    "/v1/models"
                }
            });
        }

        // Some clients expect this endpoint for validation
        [HttpOptions("chat/completions")]
        public IActionResult ChatCompletionsOptions()
        {
            return Ok();
        }

        private static int EstimateTokens(string text)
        {
            // Simple token estimation - roughly 4 characters per token
            // This is a rough approximation, real tokenization would be more accurate
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        // Helper method to map user-friendly model names to actual Azure OpenAI deployments
        private string GetActualModelName(string requestedModel)
        {
            var chatDeploymentName = _configuration["AzureOpenAI:DeploymentChatName"];
            var embeddingDeploymentName = _configuration["AzureOpenAI:DeploymentEmbeddingName"];

            return requestedModel switch
            {
                "Contact Center Database" => chatDeploymentName ?? "gpt-4.1",
                "Contact Center Embeddings" => embeddingDeploymentName ?? "text-embedding-3-small",
                // If someone passes the actual deployment name, use it as-is
                _ when requestedModel == chatDeploymentName => chatDeploymentName,
                _ when requestedModel == embeddingDeploymentName => embeddingDeploymentName,
                // Default fallback
                _ => chatDeploymentName ?? requestedModel
            };
        }
    }
}
