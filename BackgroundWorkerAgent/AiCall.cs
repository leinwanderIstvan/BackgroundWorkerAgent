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

                    // Send file content to AI with prompt asking for 2-3 sentence summary
                    // \n\n = two newlines for better formatting in prompt

                    var prompt = $"Summarize this text in 2-3 sentences:\n\n{content}";

                    Task<string> gptTask = SummerizeWithGptAsync(prompt, ct);
                    Task<string> claudeTask = SummarizeWithClaudeAsync(prompt, ct);

                    await Task.WhenAll(gptTask, claudeTask).ConfigureAwait(false);

                    var comparison = ComparisonResult.Create(prompt, gptTask.Result, claudeTask.Result);
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

        private async Task<string> SummerizeWithGptAsync(string prompt, CancellationToken ct)
        {
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct
            ).ConfigureAwait(false);
            return result.ToString();
        }

        private async Task<string> SummarizeWithClaudeAsync(string prompt, CancellationToken ct)
        {
            // Use GetClaudeMessageAsync instead of non-existent CreateAsync
            var request = new Message(RoleType.User, prompt);

            var claudeResponse = await _anthropicClient.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = "claude-sonnet-4-20250514",
                MaxTokens = 1024,
                Messages = [request]
            }, ct).ConfigureAwait(false);

            // Assuming MessageResponse has a Content property or similar
            // If not, you may need to inspect the MessageResponse type for the correct way to extract the text
            return claudeResponse?.Content?.FirstOrDefault()?.ToString() ?? string.Empty;
        }

        private static void DisplayComparison(ComparisonResult comparison)
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("                    COMPARISON RESULTS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");

            Console.WriteLine();
            Console.WriteLine("─── GPT Response ───");
            Console.WriteLine(comparison.GptResponse);

            Console.WriteLine();
            Console.WriteLine("─── Claude Response ───");
            Console.WriteLine(comparison.ClaudeResponse);

            Console.WriteLine();
            Console.WriteLine("─── Word Analysis ───");
            Console.WriteLine($"Shared words ({comparison.SharedWords.Count}): {string.Join(", ", comparison.SharedWords.Take(15))}{(comparison.SharedWords.Count > 15 ? "..." : "")}");
            Console.WriteLine($"GPT only ({comparison.GptOnlyWords.Count}): {string.Join(", ", comparison.GptOnlyWords.Take(10))}{(comparison.GptOnlyWords.Count > 10 ? "..." : "")}");
            Console.WriteLine($"Claude only ({comparison.ClaudeOnlyWords.Count}): {string.Join(", ", comparison.ClaudeOnlyWords.Take(10))}{(comparison.ClaudeOnlyWords.Count > 10 ? "..." : "")}");

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