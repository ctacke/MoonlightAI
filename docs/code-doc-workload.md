# Code Documentation Workload (Working)

Generates XML documentation comments for public-scoped C# code using AI.

**What it Documents:**
- 📘 Methods (including parameters and return values)
- 📗 Properties (get/set descriptions)
- 📙 Fields (constants and read-only fields)
- 📕 Events (when they're raised)
- 📓 Classes (coming soon)

**Features:**
- Analyzes code with Roslyn compiler
- Respects visibility levels (configurable: Public, Internal, Protected, Private)
- Processes one member at a time for accuracy
- Validates AI responses and removes hallucinations:
  - Invalid parameters that don't exist
  - Duplicate `<param>` tags
  - Multiple `<summary>`, `<remarks>`, or `<returns>` tags
  - `<returns>` tags on void methods
  - Empty XML tags
- Tracks statistics: tokens used, items documented, errors, sanitization fixes

**Configuration:**
```json
{
  "Workload": {
    "SolutionPath": "MyProject.sln",
    "IgnoreProjects": ["MyProject.Tests.csproj"],
    "CodeDocumentation": {
      "DocumentVisibility": "Public"
    }
  }
}
```

Note: `SolutionPath` and `IgnoreProjects` are configured at the parent Workload level. By default, all projects in the solution are processed. See [Configuration Guide](configuration.md) for details.
