    // Import namespaces: System = basic .NET types, Threading.Tasks = async support, SemanticKernel = AI framework
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace BackgroundWorkerAgent
{
    // sealed = cannot be inherited (performance optimization + design intent)
    public sealed class AiCall : IDisposable
    {
        // Store the Kernel (AI "brain") - readonly = can only be set in constructor (immutability)
        private readonly Kernel _kernel;

        private readonly ExtensionFilter _filter;

        private FileSystemWatcher? _watcher;

        // Constructor: Runs when you create new AiCall("your-api-key")
        // Purpose: Initialize the AI connection with your API key
        public AiCall(string apiKey)
        {
            // Validate API key is not null/empty/whitespace - fail fast if invalid
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Throw exception with clear message - nameof(apiKey) gives parameter name as string
                throw new ArgumentException("API key must not be null or empty.", nameof(apiKey));
            }

            // STEP 1: Create builder (factory pattern to configure Kernel)
            var builder = Kernel.CreateBuilder();

            // STEP 2: Add OpenAI chat completion - modelId = which AI model, apiKey = your secret key
            builder.AddOpenAIChatCompletion(modelId: "gpt-4o-mini", apiKey: apiKey);

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
                    var result = await _kernel.InvokePromptAsync(
                        $"Summarize this text in 2-3 sentences:\n\n{content}", cancellationToken: ct
                    ).ConfigureAwait(false);

                    // Print AI's summary to console
                    Console.WriteLine($"Summary: {result}");

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

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}