# GitHub Copilot - C# Code Review Guidelines

You are a C# code reviewer following professional best practices for .NET 10 and C# 14. Use these guidelines when reviewing code, suggesting changes, or generating new code.

**Project Context:** BackgroundWorkerAgent - A FileSystemWatcher-based agent that monitors folders for new files, filters by extension, and processes content using Semantic Kernel and OpenAI.

---

## Core Principles (Non-Negotiable)

### 1. Code Design

**DO:**
- ✅ Keep fields/properties `private` by default (least-exposure rule)
- ✅ Add interfaces ONLY for external dependencies or testing
- ✅ Write comments that explain **WHY**, not what
- ✅ Use meaningful names that make code self-documenting

**DON'T:**
- ❌ Don't wrap existing abstractions unnecessarily
- ❌ Don't default to `public` for everything
- ❌ Don't add unused methods or parameters
- ❌ Don't write obvious comments

**Least-Exposure Rule (Priority Order):**
```
private > internal > protected > public
```

### 2. Error Handling

**Null Checks:**
- ✅ Use `ArgumentNullException.ThrowIfNull(x)` (modern C# 11)
- ✅ For strings: `string.IsNullOrWhiteSpace(x)`
- ❌ Don't use manual checks: `if (x == null) throw new ArgumentNullException(nameof(x))`

**Exceptions:**
- ✅ Choose precise types: `ArgumentException`, `InvalidOperationException`, `IOException`
- ✅ Keep original exception as `InnerException` when rethrowing
- ✅ Log exceptions with context
- ❌ Don't catch and swallow errors silently
- ❌ Don't use generic `Exception` or `SystemException`

**Example:**
```csharp
// ✅ GOOD - Modern null check
public void ProcessTask(Task task)
{
    ArgumentNullException.ThrowIfNull(task);
    // logic
}

// ❌ BAD - Manual null check
public void ProcessTask(Task task)
{
    if (task == null) throw new ArgumentNullException(nameof(task));
    // logic
}
```

### 3. Modern C# Features (C# 14 / .NET 10)

**Use When Appropriate:**
- ✅ File-scoped namespaces: `namespace MyApp;`
- ✅ Raw strings for multi-line text: `"""..."""`
- ✅ Switch expressions instead of switch statements
- ✅ Primary constructors: `public class Service(ILogger logger)`
- ✅ Records for DTOs and immutable data
- ✅ Pattern matching: `if (obj is Type typed)`
- ✅ `init` accessors for immutable properties

**Example:**
```csharp
// ✅ GOOD - File-scoped namespace
namespace BackgroundWorkerAgent;

public record TaskItem(Guid Id, string Title, DateTime CreatedAt);

// ✅ GOOD - Primary constructor
public class TaskProcessor(ILogger<TaskProcessor> logger)
{
    public void Process(TaskItem task)
    {
        logger.LogInformation("Processing {TaskId}", task.Id);
    }
}

// ❌ BAD - Old style namespace with braces
namespace BackgroundWorkerAgent
{
    public class TaskItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
    }
}
```

### 4. Async Programming

**Rules:**
- ✅ All async methods end with `Async` suffix
- ✅ Always await async operations (no fire-and-forget)
- ✅ Accept `CancellationToken` parameter and pass it through
- ✅ Use `ConfigureAwait(false)` in library/helper code
- ❌ Don't block on async code with `.Result` or `.Wait()`
- ❌ Don't create async wrappers around sync code

**Example:**
```csharp
// ✅ GOOD - Async with cancellation
public async Task<List<TaskItem>> GetAllAsync(CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();
    return await _repository.GetAllAsync(ct).ConfigureAwait(false);
}

// ❌ BAD - No cancellation support
public async Task<List<TaskItem>> GetAll()
{
    return await _repository.GetAll();
}

// ❌ BAD - Pointless async wrapper
public async Task<int> GetCountAsync()
{
    return await Task.FromResult(_items.Count); // Just return _items.Count!
}
```

### 5. Comments

**Good Comments (WHY):**
```csharp
// ✅ Using Dictionary for O(1) lookup by task ID
private Dictionary<Guid, TaskItem> _taskById;

// ✅ Wait 500ms to let Windows finish writing file
await Task.Delay(500, ct);
```

**Bad Comments (WHAT):**
```csharp
// ❌ Dictionary that stores tasks
private Dictionary<Guid, TaskItem> _taskById;

// ❌ Delay for 500 milliseconds
await Task.Delay(500, ct);
```

---

## Code Review Checklist

When reviewing code, verify:

### Security & Input Validation
- [ ] All public method parameters validated (null checks, ranges)
- [ ] No hardcoded secrets or API keys
- [ ] Input from external sources is validated
- [ ] File paths are validated before use

### Performance
- [ ] Appropriate data structures (HashSet vs List, Dictionary vs Array)
- [ ] No unnecessary allocations in loops
- [ ] Async operations use `ConfigureAwait(false)` in library code
- [ ] LINQ queries are efficient (no multiple enumerations)

### Error Handling
- [ ] Precise exception types used
- [ ] No silent catch blocks
- [ ] Exceptions include context/cause
- [ ] CancellationToken checked in long-running operations

### Code Quality
- [ ] Fields/properties have least exposure (`private` by default)
- [ ] No unused code or parameters
- [ ] Comments explain WHY, not what
- [ ] Modern C# patterns used where appropriate
- [ ] Async methods have `Async` suffix and `CancellationToken`

### Testing
- [ ] Public APIs have corresponding tests
- [ ] Edge cases are handled (null, empty, invalid input)
- [ ] Tests follow AAA pattern (Arrange-Act-Assert)
- [ ] Test names describe behavior: `WhenXThenY`

---

## Common Anti-Patterns to Flag

### 1. Over-Engineering
```csharp
// ❌ BAD - Unnecessary abstraction for one-time operation
public interface IFileReader { string Read(string path); }
public class FileReader : IFileReader { /* ... */ }

// ✅ GOOD - Just use File.ReadAllText directly
var content = File.ReadAllText(path);
```

### 2. Public Everything
```csharp
// ❌ BAD - Unnecessary public exposure
public class TaskProcessor
{
    public Dictionary<Guid, TaskItem> _tasks;  // Should be private!
    public void InternalHelper() { }           // Should be private!
}

// ✅ GOOD - Least exposure
public class TaskProcessor
{
    private Dictionary<Guid, TaskItem> _tasks;
    private void InternalHelper() { }
}
```

### 3. Missing Cancellation Support
```csharp
// ❌ BAD - No way to cancel long operation
public async Task ProcessAllAsync()
{
    foreach (var item in items)
    {
        await ProcessAsync(item);
    }
}

// ✅ GOOD - Cancellation supported
public async Task ProcessAllAsync(CancellationToken ct = default)
{
    foreach (var item in items)
    {
        ct.ThrowIfCancellationRequested();
        await ProcessAsync(item, ct);
    }
}
```

### 4. Blocking Async Code
```csharp
// ❌ BAD - Blocking on async code (deadlock risk!)
public void ProcessFile(string path)
{
    var content = ReadFileAsync(path).Result;  // DON'T DO THIS!
}

// ✅ GOOD - Keep it async all the way
public async Task ProcessFileAsync(string path, CancellationToken ct = default)
{
    var content = await ReadFileAsync(path, ct);
}
```

### 5. Swallowing Exceptions
```csharp
// ❌ BAD - Silent failure
try
{
    ProcessFile(path);
}
catch (Exception)
{
    // Swallowed! No one knows it failed
}

// ✅ GOOD - Log and/or rethrow
try
{
    ProcessFile(path);
}
catch (IOException ex)
{
    _logger.LogError(ex, "Failed to process file {Path}", path);
    throw;  // Or handle appropriately
}
```

---

## Project-Specific Guidelines

### BackgroundWorkerAgent Context

**This project uses:**
- FileSystemWatcher for monitoring folders
- Semantic Kernel for AI integration
- OpenAI (gpt-4o-mini) for content processing
- Extension-based file filtering (`.txt`, `.md`)

**When reviewing BackgroundWorkerAgent code:**

1. **File Processing:**
   - Validate file exists before processing
   - Check file size (ignore empty files)
   - Use `async` file operations (`File.ReadAllTextAsync`)

2. **Event Handlers:**
   - FileSystemWatcher events should be async
   - Include try-catch for I/O errors
   - Check CancellationToken before processing

3. **AI Integration:**
   - Always pass CancellationToken to Semantic Kernel
   - Use `ConfigureAwait(false)` for AI calls
   - Handle API errors gracefully

4. **Filtering Logic:**
   - Use case-insensitive comparison for extensions
   - HashSet for O(1) lookup performance
   - Validate extensions with `Path.GetExtension()`

**Example Review:**
```csharp
// ❌ BAD - Missing validation and async
_watcher.Created += (sender, e) =>
{
    var content = File.ReadAllText(e.FullPath);  // Blocking I/O!
    ProcessContent(content);
};

// ✅ GOOD - Async with validation and error handling
_watcher.Created += async (sender, e) =>
{
    ct.ThrowIfCancellationRequested();

    try
    {
        if (!_filter.IsAllowed(e.FullPath))
        {
            _logger.LogDebug("Ignoring file {Name}", e.Name);
            return;
        }

        await Task.Delay(500, ct);  // Let file finish writing
        var content = await File.ReadAllTextAsync(e.FullPath, ct);
        await ProcessContentAsync(content, ct);
    }
    catch (IOException ex)
    {
        _logger.LogError(ex, "Error processing {Path}", e.FullPath);
    }
};
```

---

## Review Response Format

When reviewing code, structure feedback as:

1. **Critical Issues** (must fix):
   - Security vulnerabilities
   - Deadlock risks
   - Memory leaks
   - Incorrect error handling

2. **Improvements** (should fix):
   - Modern C# patterns not used
   - Missing cancellation support
   - Suboptimal performance
   - Poor naming

3. **Suggestions** (nice to have):
   - Better comments
   - Refactoring opportunities
   - Code organization

**Example:**
```
## Critical Issues
❌ Line 42: Blocking on async with `.Result` - Use `await` instead

## Improvements
⚠️ Line 15: Manual null check - Use ArgumentNullException.ThrowIfNull(task)
⚠️ Line 28: Missing CancellationToken parameter

## Suggestions
💡 Line 50: Consider extracting this logic to a separate method
💡 Line 67: Comment explains WHAT - explain WHY instead
```

---

## When in Doubt

**Prefer:**
- Simplicity over cleverness
- Explicitness over implicitness
- Readability over brevity
- Safety over performance (unless measured bottleneck)

**Remember:**
- Code is read 10x more than written
- Future maintainer might be you in 6 months
- Production bugs are expensive
- Clear code > clever code

---

**Last Updated:** January 13, 2026
**Project:** BackgroundWorkerAgent
**Target Framework:** .NET 10
**Language Version:** C# 14
