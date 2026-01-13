using System.Net.Http.Headers;
using System.Threading.Channels;
using BackgroundWorkerAgent;
using Microsoft.Extensions.Configuration;

var configurationRoot = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var apiKey = configurationRoot["OpenAI:ApiKey"];


if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("ERROR: OPENAI_API_KEY environment variable not set!");    
    return;
}



using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n Shutdown requested....");
};


using var aiCall = new AiCall(apiKey);

Console.WriteLine("Background Worker Agent started!");
Console.WriteLine("Press Ctrl+C to stop.\n");


var watchFolder = @"E:\source_v3\C#\AI_Projects\TestFolder"; 

aiCall.StartWatchingFolder(watchFolder,cts.Token);

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);

}
catch (OperationCanceledException)
{
    Console.WriteLine("Agent stopped cleanly!");
    
}


