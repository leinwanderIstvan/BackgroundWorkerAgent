# Week 3: Domain Model Plan

**Created:** January 21, 2026
**Status:** Ready for implementation
**Estimated Time:** ~90 minutes

---

## Current State (Problem)

`AiCall.cs` does too much:
- Holds API clients
- Watches files
- Calls LLMs
- Builds comparisons
- Displays output

`ComparisonResult` is the only domain entity.

---

## Proposed Domain Model

### Entity 1: `Question`
```csharp
public record Question(
    Guid Id,
    string Content,           // the actual text/prompt
    string? SourceFile,       // filename if from file, null if manual
    DateTime CreatedAt);
```
**Purpose:** Represents what was asked. Decouples "input" from "where it came from."

---

### Entity 2: `LlmResponse`
```csharp
public record LlmResponse(
    Guid Id,
    string ModelName,         // "gpt-4o-mini", "claude-sonnet-4"
    string Content,           // the response text
    Guid QuestionId,          // links back to Question
    DateTime RespondedAt,
    long? DurationMs);        // optional: how long the call took
```
**Purpose:** One response from one model. Can have multiple per Question.

---

### Entity 3: `Comparison` (rename from `ComparisonResult`)
```csharp
public record Comparison(
    Guid Id,
    Guid QuestionId,
    IReadOnlyList<LlmResponse> Responses,      // the two responses being compared
    IReadOnlyList<string> SharedWords,
    IReadOnlyList<string> ResponseAOnlyWords,
    IReadOnlyList<string> ResponseBOnlyWords,
    DateTime ComparedAt);
```
**Purpose:** The analysis result. Links Question → Responses → word diff.

---

## Project Structure

```
BackgroundWorkerAgent.Core/
├── Models/
│   ├── Question.cs          ← NEW
│   ├── LlmResponse.cs       ← NEW
│   └── Comparison.cs        ← RENAME from ComparisonResult.cs
```

No changes to main project structure yet (services extraction is Week 5).

---

## How Code Flow Changes

**Before (current):**
```
File dropped → Read content → Call GPT + Claude → Build ComparisonResult → Display
                              (strings everywhere)
```

**After (Week 3):**
```
File dropped → Create Question → Call GPT + Claude → Create LlmResponses → Build Comparison → Display
                 (entity)                              (entities)            (entity)
```

---

## What Stays in `AiCall.cs` (For Now)

- FileSystemWatcher logic
- API client setup
- Display method

**Week 5** will extract these into proper services.

---

## Implementation Steps

| Step | Task | Time Est. |
|------|------|-----------|
| 1 | Create `Question` record | 10 min |
| 2 | Create `LlmResponse` record | 15 min |
| 3 | Refactor `ComparisonResult` → `Comparison` | 20 min |
| 4 | Update `AiCall.cs` to use new entities | 30 min |
| 5 | Test by dropping file | 10 min |
| 6 | Commit + push | 5 min |

**Total: ~90 minutes**

---

## Design Decisions

| Decision | Choice | Why |
|----------|--------|-----|
| Records vs Classes | **Records** | Immutable, value equality, less boilerplate |
| Factory methods | **Yes** (`Create()`) | Encapsulate validation logic |
| Nullable SourceFile | **Yes** | Supports both file-based and manual prompts |
| Generic "ResponseA/B" naming | **Yes** | Not tied to GPT/Claude specifically |

---

## Notes for Pair Programming

- Start by creating `Question.cs` - simplest entity
- Use `ArgumentNullException.ThrowIfNull()` for validation (modern C# style)
- Keep factory methods simple - no over-engineering
- Test after each entity is wired up
