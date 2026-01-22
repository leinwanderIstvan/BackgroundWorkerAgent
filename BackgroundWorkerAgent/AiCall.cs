    // Import namespaces: System = basic .NET types, Threading.Tasks = async support, SemanticKernel = AI framework
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using BackgroundWorkerAgent.Core.Models;

namespace BackgroundWorkerAgent
{
    // sealed = cannot be inherited (performance optimization + design intent)
    public sealed class AiCall : IDisposable
    {
        // Store the Kernel (AI "brain") - readonly = can only be set in constructor (immutability)
        private readonly Kernel _kernel;

        private readonly AnthropicClient _anthropicClient;

        private readonly ExtensionFilter _filter;

        private FileSystemWatcher? _watcher;

        // Constructor: Runs when you create new AiCall("your-api-key")
        // Purpose: Initialize the AI connection with your API key
        public AiCall(string openAiApiKey, string anthropicApiKey)
        {
            // Validate API key is not null/empty/whitespace - fail fast if invalid
            ArgumentException.ThrowIfNullOrWhiteSpace(openAiApiKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(anthropicApiKey);

            var client = new AnthropicClient(anthropicApiKey);
            _anthropicClient = client;

            // STEP 1: Create builder (factory pattern to configure Kernel)
            var builder = Kernel.CreateBuilder();

            // STEP 2: Add OpenAI chat completion - modelId = which AI model, apiKey = your secret key
            builder.AddOpenAIChatCompletion(modelId: "gpt-4o-mini", apiKey: openAiApiKey);



            // STEP 3: Build the Kernel - AI is now ready to accept prompts
            _kernel = builder.Build();


            _filter = new ExtensionFilter(new FileFilterOption(".txt", ".md"));

        }

        // Method to send text to AI and get summary back
        // async = method can await (pause for slow operations), Task<string> = returns string asynchronously
        public async Task<string> SummarizeAsync(string text, string userPrompt, CancellationToken ct = default)
        {
            // Validate text parameter - don't send empty text to AI
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text must not be null or empty.", nameof(text));
            }

            // Combine user's instruction with the actual text (e.g., "Summarize: " + "The quick brown fox...")
            var prompt = userPrompt + text;

            ct.ThrowIfCancellationRequested();

            // Send prompt to AI and wait for response - await = pause here until AI responds (non-blocking)
            // ConfigureAwait(false) = don't capture sync context (library best practice for performance)
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct).ConfigureAwait(false);

            // Convert AI response to string and return it to caller
            return result.ToString();
        }

        // Method to start monitoring a folder for new .txt files
        // When new file appears, automatically summarize it with AI
        public void StartWatchingFolder(string folderPath, CancellationToken ct = default)
        {
            // Validate folder path is not null/empty
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("Folder path must not be null or empty.", nameof(folderPath));
            }

            // Create folder if it doesn't exist - prevents "folder not found" errors
            Directory.CreateDirectory(folderPath);

            // Create FileSystemWatcher to monitor folder for file changes
            _watcher = new FileSystemWatcher(folderPath)
            {
                // Watch all files - ExtensionFilter will decide which to process
                Filter = "*.*",

                // Start watching immediately - watcher is now active
                EnableRaisingEvents = true
            };

            // Subscribe to Created event - runs this code whenever new file is created
            // async (sender, e) => { } is async lambda (anonymous function)
            _watcher.Created += async (sender, e) =>
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (!_filter.IsAllowed(e.FullPath))
                    {
                        Console.WriteLine($"Ignoring invalid file: {e.Name}");
                        return;
                    }

                    // Print filename to console - e.Name contains just filename (e.g., "test.txt")
                    Console.WriteLine($"New file detected: {e.Name}");

                    // Wait 500ms to let Windows finish writing file - prevents reading incomplete file
                    await Task.Delay(500, ct).ConfigureAwait(false);

                    // Read all text from file - e.FullPath = complete path (e.g., "C:\Temp\test.txt")
                    // await = don't block thread while reading (file I/O is slow)
                    var content = await File.ReadAllTextAsync(e.FullPath).ConfigureAwait(false);

                    // Create Question entity from the file
                    var question = Question.FromFile(e.FullPath, content);

                    // Build prompt for summarization
                    var prompt = $"Summarize this text in 2-3 sentences:\n\n{content}";

                    // Call both LLMs in parallel
                    Task<LlmResponse> gptTask = GetGptResponseAsync(prompt, ct);
                    Task<LlmResponse> claudeTask = GetClaudeResponseAsync(prompt, ct);

                    await Task.WhenAll(gptTask, claudeTask).ConfigureAwait(false);

                    // Create comparison with all responses
                    var responses = new List<LlmResponse> { gptTask.Result, claudeTask.Result };
                    var comparison = Comparison.Create(question, responses);
                    DisplayComparison(comparison);


                }
                catch(OperationCanceledException)
                {
                    // Shutdown requested - exit silently
                    return;
                }
                catch (IOException ioEx)
                {
                    Console.WriteLine($"I/O error while processing file '{e.FullPath}': {ioEx.Message}");
                }
                catch (UnauthorizedAccessException unauthorizedEx)
                {
                    Console.WriteLine($"Access denied for file '{e.FullPath}': {unauthorizedEx.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error while processing file '{e.FullPath}': {ex.Message}");
                }

            };

            // Inform user that watcher is active and ready
            Console.WriteLine($"Watching folder: {folderPath}");
            Console.WriteLine("Drop a .txt file to test!");
        }

        private async Task<LlmResponse> GetGptResponseAsync(string prompt, CancellationToken ct)
        {
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct)
                .ConfigureAwait(false);

            return LlmResponse.Create(
                modelName: "gpt-4o-mini",
                provider: "OpenAI",
                responseText: result.ToString());
        }

        private async Task<LlmResponse> GetClaudeResponseAsync(string prompt, CancellationToken ct)
        {
            var request = new Message(RoleType.User, prompt);

            var claudeResponse = await _anthropicClient.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = "claude-sonnet-4-20250514",
                MaxTokens = 1024,
                Messages = [request]
            }, ct).ConfigureAwait(false);

            var responseText = claudeResponse?.Content?.FirstOrDefault()?.ToString() ?? string.Empty;

            return LlmResponse.Create(
                modelName: "claude-sonnet-4",
                provider: "Anthropic",
                responseText: responseText);
        }

        private static void DisplayComparison(Comparison comparison)
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("                    COMPARISON RESULTS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");

            Console.WriteLine();
            Console.WriteLine($"─── Source: {comparison.Question.FileName} ───");
            Console.WriteLine($"ID: {comparison.Id}");

            // Display each response dynamically (supports N responses)
            foreach (var response in comparison.Responses)
            {
                Console.WriteLine();
                Console.WriteLine($"─── {response.Provider} ({response.ModelName}) ───");
                Console.WriteLine(response.ResponseText);
            }

            Console.WriteLine();
            Console.WriteLine("─── Word Analysis ───");
            var analysis = comparison.Analysis;
            Console.WriteLine($"Shared words ({analysis.SharedWords.Count}): {string.Join(", ", analysis.SharedWords.Take(15))}{(analysis.SharedWords.Count > 15 ? "..." : "")}");

            // Display unique words per model dynamically
            foreach (var (modelName, uniqueWords) in analysis.UniqueWordsByModel)
            {
                Console.WriteLine($"{modelName} only ({uniqueWords.Count}): {string.Join(", ", uniqueWords.Take(10))}{(uniqueWords.Count > 10 ? "..." : "")}");
            }

            Console.WriteLine();
            Console.WriteLine($"Compared at: {comparison.ComparedAt:HH:mm:ss} UTC");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}