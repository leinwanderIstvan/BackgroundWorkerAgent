using System.Net.Http.Headers;
using System.Threading.Channels;
using BackgroundWorkerAgent;
using Microsoft.Extensions.Configuration;

var configurationRoot = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var openAiApiKey = configurationRoot["OpenAI:ApiKey"];
var anthropicKey = configurationRoot["Anthropic:ApiKey"];

// Fix: Ensure anthropicKey is not null before passing to AiCall constructor
if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    Console.WriteLine("ERROR: OPENAI_API_KEY environment variable not set!");    
    return;
}

if (string.IsNullOrWhiteSpace(anthropicKey))
{
    Console.WriteLine("ERROR: ANTHROPIC_API_KEY environment variable not set!");
    return;
}

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n Shutdown requested....");
};

var watchFolder = @"E:\source_v3\C#\AI_Projects\TestFolder";
var comparisonOutputFolder = Path.Combine(watchFolder, "comparisons");
var comparisonStore = new JsonComparisonStore(comparisonOutputFolder);

using var aiCall = new AiCall(openAiApiKey, anthropicKey, comparisonStore);

Console.WriteLine("Background Worker Agent started!");
Console.WriteLine($"Comparisons will be saved to: {comparisonOutputFolder}");
Console.WriteLine("Press Ctrl+C to stop.\n");

aiCall.StartWatchingFolder(watchFolder, cts.Token);

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);

}
catch (OperationCanceledException)
{
    Console.WriteLine("Agent stopped cleanly!");
    
}


