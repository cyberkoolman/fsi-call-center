using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace RpCCDurableFunctionApp
{
    public static class OrchestratorFunction
    {
        [FunctionName("OrchestratorFunction")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            // Step 1: Process the audio file
            var audioFileName = context.GetInput<string>();
            var transcript = await context.CallActivityAsync<string>("ProcessAudioFile", audioFileName);
            outputs.Add(transcript);

            // Step 2: Convert transcript to JSON
            var jsonTranscript = await context.CallActivityAsync<string>("ConvertTranscriptToJson", transcript);
            outputs.Add(jsonTranscript);

            // Step 3: Save JSON to database
            await context.CallActivityAsync("SaveJsonToDb", jsonTranscript);

            return outputs;
        }
    }
}