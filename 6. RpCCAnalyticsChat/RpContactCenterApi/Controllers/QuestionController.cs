using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Concurrent;

namespace RpContactCenter.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuestionController : ControllerBase
    {
        private readonly Kernel _kernel;
        private readonly PromptExecutionSettings _settings;
        private static readonly ConcurrentDictionary<string, ChatHistory> ChatHistories = new();

        public QuestionController(Kernel kernel, PromptExecutionSettings settings)
        {
            _kernel = kernel;
            _settings = settings;
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] QuestionRequest request)
        {
            if (string.IsNullOrEmpty(request.Question))
            {
                return BadRequest("Please provide a question.");
            }

            if (string.IsNullOrEmpty(request.ConversationId))
            {
                return BadRequest("Please provide a conversation ID.");
            }

            // Retrieve or create a new ChatHistory for the given ConversationId
            // ChatHistory chatHistory = new();
            ChatHistory chatHistory = ChatHistories.GetOrAdd(request.ConversationId, new ChatHistory());
            chatHistory.AddUserMessage(request.Question);


            IChatCompletionService chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            chatHistory.AddUserMessage(request.Question);

            var response = await chatCompletionService.GetChatMessageContentAsync(chatHistory, _settings, _kernel);
            return Ok(new { Answer = response });
        }
    }

    public class QuestionRequest
    {
        public string Question { get; set; }
        public string ConversationId { get; set; } // Unique ID for conversation
    }
}