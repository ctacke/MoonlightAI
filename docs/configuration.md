# MoonlightAI Configuration Guide

This guide covers all configuration options for MoonlightAI. Configuration is managed through the `appsettings.json` file located in the CLI project.

## Configuration File Location

```
Source/MoonlightAI/MoonlightAI.CLI/appsettings.json
```

⚠️ **Important:** This file contains secrets (GitHub PAT) and is in `.gitignore`. Never commit it to source control!

---

## Quick Configuration Checklist

Before running MoonlightAI, ensure you've configured:

- ✅ GitHub Personal Access Token
- ✅ GitHub user name and email
- ✅ Repository URL
- ✅ AI server URL and model name
- ✅ Project and solution paths for workloads
- ✅ Container settings (if using local AI)

---

## Complete Configuration Template

```json
{
  "GitHub": {
    "PersonalAccessToken": "ghp_YourTokenHere",
    "DefaultBranch": "main",
    "WorkingDirectory": "./repositories",
    "UserName": "MoonlightAI",
    "UserEmail": "moonlight@example.com"
  },
  "Repositories": [
    {
      "RepositoryUrl": "https://github.com/yourusername/your-repo"
    }
  ],
  "AIServer": {
    "ServerUrl": "http://localhost:11434",
    "ModelName": "codellama:13b-instruct",
    "TimeoutSeconds": 300
  },
  "Container": {
    "UseLocalContainer": true,
    "ImageName": "moonlight-llm-server",
    "ContainerName": "moonlight-llm-server",
    "HostPort": 11434,
    "ContainerPort": 11434,
    "ModelsPath": "./ollama-models",
    "AutoStart": true,
    "AutoStop": true,
    "PruneAfterStop": true
  },
  "Workload": {
    "BatchSize": 10,
    "ValidateBuilds": true,
    "MaxBuildRetries": 2,
    "RevertOnBuildFailure": true,
    "SolutionPath": "MySolution.sln",
    "ProjectPath": "src/MyProject",
    "CodeDocumentation": {
      "DocumentVisibility": "Public"
    },
    "CodeCleanup": {
      "Options": {
        "RemoveUnusedVariables": true,
        "RemoveUnusedUsings": true,
        "ConvertPublicFieldsToProperties": true,
        "ReorderPrivateFields": true,
        "ExtractMagicNumbers": false,
        "SimplifyBooleanExpressions": false,
        "RemoveRedundantCode": false,
        "SimplifyStringOperations": false,
        "UseExpressionBodiedMembers": false,
        "MaxOperationsPerRun": 1
      }
    }
  },
  "Database": {
    "DatabasePath": "./moonlight.db",
    "EnableDetailedLogging": false
  },
  "Prompts": {
    "PromptsDirectory": "./prompts"
  }
}
```

---

## GitHub Configuration

Configuration for GitHub integration and repository management.

### Required Settings

```json
{
  "GitHub": {
    "PersonalAccessToken": "ghp_YourTokenHere",
    "DefaultBranch": "main",
    "WorkingDirectory": "./repositories",
    "UserName": "MoonlightAI",
    "UserEmail": "moonlight@example.com"
  }
}
```

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `PersonalAccessToken` | GitHub PAT with `repo` scope | None | ✅ Yes |
| `DefaultBranch` | Default branch to work from | `"main"` | ✅ Yes |
| `WorkingDirectory` | Where to clone repositories | `"./repositories"` | ✅ Yes |
| `UserName` | Git commit author name | `"MoonlightAI"` | ✅ Yes |
| `UserEmail` | Git commit author email | Required | ✅ Yes |

### Generating a GitHub Personal Access Token

1. Go to https://github.com/settings/tokens
2. Click "Generate new token (classic)"
3. Give it a descriptive name (e.g., "MoonlightAI")
4. Select scope: **`repo`** (full control of private repositories)
5. Set expiration (90 days recommended, or "No expiration" for automation)
6. Click "Generate token"
7. **Copy the token immediately** (you won't see it again!)
8. Paste into `appsettings.json`

⚠️ **Security Notes:**
- Never commit this token to source control
- `appsettings.json` is in `.gitignore` by default
- Treat PAT like a password
- Rotate tokens periodically for security

### Working Directory

The `WorkingDirectory` is where MoonlightAI clones repositories:

```
./repositories/
└── your-repo/           # Cloned repository
    ├── .git/
    ├── src/
    └── ...
```

- Path is relative to CLI executable location
- Directory created automatically if it doesn't exist
- Can be absolute path: `/home/user/moonlight-repos`

---

## Repository Configuration

Configure which repositories MoonlightAI should process.

```json
{
  "Repositories": [
    {
      "RepositoryUrl": "https://github.com/yourusername/your-repo"
    },
    {
      "RepositoryUrl": "https://github.com/yourusername/another-repo"
    }
  ]
}
```

| Setting | Description | Required |
|---------|-------------|----------|
| `RepositoryUrl` | Full GitHub repository URL | ✅ Yes |

**Notes:**
- HTTPS URLs only (SSH not currently supported)
- Must have access via GitHub PAT
- Currently processes one repository per run
- Multiple repositories listed for future batch support

**Supported URL formats:**
- ✅ `https://github.com/user/repo`
- ✅ `https://github.com/user/repo.git`
- ❌ `git@github.com:user/repo.git` (SSH not supported)

---

## AI Server Configuration

Configure connection to AI model server (Ollama).

### Local Server (Default)

```json
{
  "AIServer": {
    "ServerUrl": "http://localhost:11434",
    "ModelName": "codellama:13b-instruct",
    "TimeoutSeconds": 300
  }
}
```

### Remote Server

```json
{
  "AIServer": {
    "ServerUrl": "http://192.168.1.100:11434",
    "ModelName": "codellama:13b-instruct",
    "TimeoutSeconds": 300
  }
}
```

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `ServerUrl` | Ollama API base URL | `"http://localhost:11434"` | ✅ Yes |
| `ModelName` | Model to use for generation | `"codellama:13b-instruct"` | ✅ Yes |
| `TimeoutSeconds` | Request timeout | `300` (5 min) | ✅ Yes |

### Supported Models

**Recommended:**
- `codellama:13b-instruct` - Best balance of quality and performance
- `codellama:7b-instruct` - Faster, less VRAM, slightly lower quality

**Alternatives:**
- `mistral:7b-instruct` - Good general-purpose model
- `deepseek-coder:6.7b` - Code-specialized alternative
- `deepseek-coder:33b` - Highest quality, requires 20GB+ VRAM

**Model format:** `{model-name}:{size}-{variant}`

### Timeout Considerations

- **Default 300s (5 min)** - Good for most cases
- **Increase for:**
  - Slower hardware
  - Larger models
  - Complex methods with many parameters
- **Decrease for:**
  - Faster hardware
  - Smaller models
  - Simple code

---

## Container Configuration

Configure Docker container management for local AI server.

### Local Container (Recommended for Development)

```json
{
  "Container": {
    "UseLocalContainer": true,
    "ImageName": "moonlight-llm-server",
    "ContainerName": "moonlight-llm-server",
    "HostPort": 11434,
    "ContainerPort": 11434,
    "ModelsPath": "./ollama-models",
    "AutoStart": true,
    "AutoStop": true,
    "PruneAfterStop": true
  }
}
```

### Remote Server (No Container Management)

```json
{
  "Container": {
    "UseLocalContainer": false
  }
}
```

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `UseLocalContainer` | Enable local container management | `false` | ✅ Yes |
| `ImageName` | Docker image name | `"moonlight-llm-server"` | If enabled |
| `ContainerName` | Docker container name | `"moonlight-llm-server"` | If enabled |
| `HostPort` | Port on host machine | `11434` | If enabled |
| `ContainerPort` | Port inside container | `11434` | If enabled |
| `ModelsPath` | Path to persist models | `"./ollama-models"` | If enabled |
| `AutoStart` | Start container before workloads | `true` | No |
| `AutoStop` | Stop container after workloads | `true` | No |
| `PruneAfterStop` | Clean up stopped containers | `true` | No |

### Container Workflow

When `UseLocalContainer: true`:

1. **Before workload:**
   - Check if container exists
   - Check if container is running
   - If `AutoStart: true` and not running → start container
   - Wait for health check
   - Verify model is available

2. **After workload:**
   - If `AutoStop: true` → stop container
   - If `PruneAfterStop: true` → run docker prune

### Models Path

The `ModelsPath` is mounted into the container to persist models:

```
./ollama-models/
└── models/
    └── blobs/
        └── sha256/...  # Model files
```

- Prevents re-downloading models
- Can be shared across container recreations
- Can be absolute path

---

## Workload Configuration

Configure behavior and settings for workloads.

### Global Workload Settings

These settings apply to all workload types and are shared across Code Documentation and Code Cleanup workloads.

```json
{
  "Workload": {
    "BatchSize": 10,
    "ValidateBuilds": true,
    "MaxBuildRetries": 2,
    "RevertOnBuildFailure": true,
    "SolutionPath": "MySolution.sln",
    "ProjectPath": "src/MyProject"
  }
}
```

| Setting | Description | Default | Range/Type |
|---------|-------------|---------|------------|
| `BatchSize` | Files per batch | `10` | 1-100 |
| `ValidateBuilds` | Enable build validation | `true` | true/false |
| `MaxBuildRetries` | AI fix attempts | `2` | 0-10 |
| `RevertOnBuildFailure` | Revert unfixable files | `true` | true/false |
| `SolutionPath` | Path to solution file for build validation | None | string (relative to repository root) |
| `ProjectPath` | Path to project directory to process | None | string (relative to repository root) |

**Shared Paths:**

The `SolutionPath` and `ProjectPath` are configured once at the Workload level and used by all workload types (Code Documentation and Code Cleanup). This ensures consistency and avoids duplication.

**Path Guidelines:**
- Paths are relative to the repository root
- Use forward slashes: `src/MyProject` not `src\MyProject`
- `SolutionPath` points to your `.sln` or `.slnx` file for build validation
- `ProjectPath` points to the specific project directory to process

**Batch Size Guidelines:**
- **Small (1-5):** Safer, more granular PRs, slower
- **Medium (10-20):** Balanced, recommended
- **Large (50-100):** Faster, but larger PRs to review

**Build Validation:**
- `true` - Safer, catches errors, slower (recommended)
- `false` - Faster, but may generate broken code

**Max Build Retries:**
- `0` - No retries, revert immediately on failure
- `1-3` - Recommended range
- `5+` - May waste tokens on unfixable issues

---

### Code Documentation Workload

Configuration specific to the Code Documentation workload. Note that `SolutionPath` and `ProjectPath` are configured at the parent Workload level (see Global Workload Settings above).

```json
{
  "Workload": {
    "SolutionPath": "MySolution.sln",
    "ProjectPath": "src/MyProject",
    "CodeDocumentation": {
      "DocumentVisibility": "Public"
    }
  }
}
```

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `DocumentVisibility` | Which members to document | `"Public"` | ✅ Yes |

**Document Visibility Options:**

Comma-separated list of visibility levels:
- `Public` - Public methods, properties, etc.
- `Internal` - Internal members
- `Protected` - Protected members
- `Private` - Private members
- `ProtectedInternal` - Protected internal
- `PrivateProtected` - Private protected

**Common combinations:**
- `"Public"` - Public API only (default)
- `"Public,Internal"` - Public and internal (recommended for libraries)
- `"Public,Internal,Protected"` - Include protected
- `"Public,Internal,Protected,Private"` - Document everything

---

### Code Cleanup Workload

Configuration specific to the Code Cleanup workload. Note that `SolutionPath` and `ProjectPath` are configured at the parent Workload level (see Global Workload Settings above).

```json
{
  "Workload": {
    "SolutionPath": "MySolution.sln",
    "ProjectPath": "src/MyProject",
    "CodeCleanup": {
      "Options": {
        "RemoveUnusedVariables": true,
        "RemoveUnusedUsings": true,
        "ConvertPublicFieldsToProperties": true,
        "ReorderPrivateFields": true,
        "ExtractMagicNumbers": false,
        "SimplifyBooleanExpressions": false,
        "RemoveRedundantCode": false,
        "SimplifyStringOperations": false,
        "UseExpressionBodiedMembers": false,
        "MaxOperationsPerRun": 1
      }
    }
  }
}
```

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `Options` | Cleanup operation flags | See below | ✅ Yes |

**Available Operations:**

| Operation | Description | Risk Level |
|-----------|-------------|------------|
| `RemoveUnusedVariables` | Remove unused local variables | Low |
| `RemoveUnusedUsings` | Remove unused using statements | Low |
| `ConvertFieldsToProperties` | Convert public fields to properties | Medium |
| `ReorderPrivateFields` | Reorder fields by convention | Low |
| `ExtractMagicNumbers` | Extract hardcoded numbers to constants | Medium |
| `SimplifyBooleanExpressions` | Simplify boolean logic | Medium |
| `RemoveRedundantCode` | Remove redundant code | Medium |
| `SimplifyStringOperations` | Optimize string operations | Medium |
| `UseExpressionBodiedMembers` | Convert to expression-bodied syntax | Low |

**⚠️ Cleanup Risk Levels:**
- **Low:** Safe, cosmetic changes
- **Medium:** Changes behavior structure but not logic
- **High:** May change logic (none currently available)

**Recommended starting configuration (safest operations only):**
```json
"CodeCleanup": {
  "Options": {
    "RemoveUnusedVariables": true,
    "RemoveUnusedUsings": true,
    "ReorderPrivateFields": true,
    "ConvertPublicFieldsToProperties": false,
    "ExtractMagicNumbers": false,
    "SimplifyBooleanExpressions": false,
    "RemoveRedundantCode": false,
    "SimplifyStringOperations": false,
    "UseExpressionBodiedMembers": false,
    "MaxOperationsPerRun": 1
  }
}
```

---

## Database Configuration

Configure SQLite database for tracking workload runs.

```json
{
  "Database": {
    "DatabasePath": "./moonlight.db",
    "EnableDetailedLogging": false
  }
}
```

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `DatabasePath` | Path to SQLite database file | `"./moonlight.db"` | ✅ Yes |
| `EnableDetailedLogging` | Enable EF Core SQL logging | `false` | No |

**Database Contents:**
- Workload run history
- File processing results
- Build attempts and errors
- AI interactions (tokens, prompts, responses)
- Model performance statistics

**Database Location:**
- Relative paths are relative to CLI executable
- Can use absolute path: `/var/moonlight/moonlight.db`
- File created automatically on first run

**Detailed Logging:**
- `true` - Logs all SQL queries (verbose, for debugging)
- `false` - Minimal logging (recommended for production)

---

## Prompts Configuration

Configure AI prompt templates location.

```json
{
  "Prompts": {
    "PromptsDirectory": "./prompts"
  }
}
```

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `PromptsDirectory` | Path to prompts directory | `"./prompts"` | No |

**Prompt Directory Structure:**

```
prompts/
└── codedoc/
    ├── codellama/
    │   ├── method.txt
    │   ├── property.txt
    │   ├── field.txt
    │   └── event.txt
    ├── deepseek/
    │   └── ...
    └── default/
        └── ...
```

**Customizing Prompts:**

1. Copy existing prompts from `Source/MoonlightAI/MoonlightAI.CLI/prompts/`
2. Modify templates as needed
3. Use variables: `{method}`, `{property}`, `{field}`, `{event}`
4. Test with your codebase

**Prompt Variables:**

Available variables depend on member type:
- `{method}` - Full method signature and body
- `{property}` - Property declaration
- `{field}` - Field declaration
- `{event}` - Event declaration

---

## Environment-Specific Configuration

MoonlightAI supports environment-specific configuration overrides.

### Development Configuration

Create `appsettings.Development.json`:

```json
{
  "Workload": {
    "BatchSize": 2,
    "MaxBuildRetries": 5
  },
  "Database": {
    "EnableDetailedLogging": true
  }
}
```

Set environment:
```bash
export DOTNET_ENVIRONMENT=Development  # Linux/macOS
set DOTNET_ENVIRONMENT=Development     # Windows
```

### Production Configuration

Create `appsettings.Production.json`:

```json
{
  "Workload": {
    "BatchSize": 20,
    "ValidateBuilds": true,
    "RevertOnBuildFailure": true
  }
}
```

**Override Priority:**
1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment-specific)
3. Environment variables (highest priority)

---

## Configuration Best Practices

### Security

✅ **DO:**
- Keep `appsettings.json` in `.gitignore`
- Rotate GitHub PAT regularly
- Use environment-specific configs for sensitive values
- Use separate PATs for different environments

❌ **DON'T:**
- Commit `appsettings.json` to source control
- Share PAT between team members
- Use overly permissive PAT scopes
- Store secrets in code or scripts

### Performance

✅ **DO:**
- Start with small batch sizes (5-10) and increase
- Enable build validation in production
- Use appropriate timeout for your hardware
- Monitor database size and clean old runs

❌ **DON'T:**
- Set batch size too high initially
- Disable build validation without testing
- Use models too large for your hardware
- Set excessive build retries (wastes tokens)

### Maintenance

✅ **DO:**
- Document custom prompt changes
- Back up database periodically
- Test configuration changes on small repos first
- Review model comparison reports regularly

❌ **DON'T:**
- Modify prompts without testing
- Delete database without backup
- Make multiple config changes at once
- Ignore high sanitization fix counts

---

## Troubleshooting Configuration

### Configuration Not Loading

**Problem:** Changes to `appsettings.json` not taking effect

**Solutions:**
1. Check file location: `Source/MoonlightAI/MoonlightAI.CLI/appsettings.json`
2. Verify JSON syntax (use validator: https://jsonlint.com/)
3. Check for trailing commas (not allowed in JSON)
4. Rebuild solution: `dotnet build`

### Invalid JSON

**Error:** `System.Text.Json.JsonException: ...`

**Common issues:**
- Trailing comma in arrays/objects
- Missing quotes around strings
- Unescaped backslashes in paths (use `/` or `\\`)
- Comments (not allowed in JSON)

**Validate JSON:**
```bash
# Linux/macOS
cat appsettings.json | jq .

# Online validator
# https://jsonlint.com/
```

### Path Not Found

**Error:** `Project file does not exist` or `Solution file does not exist`

**Solutions:**
1. Use forward slashes: `src/MyProject` not `src\MyProject`
2. Check path is relative to repository root
3. Verify case sensitivity (matters on Linux)
4. Test path after repository clone

---

## Getting Help

- **Configuration Issues:** [GitHub Issues](https://github.com/ctacke/MoonlightAI/issues)
- **Questions:** [GitHub Discussions](https://github.com/ctacke/MoonlightAI/discussions)
- **Examples:** See `appsettings.json.template` in the CLI project

---

**Need more help?** See the [Getting Started Guide](getting-started.md) for setup walkthroughs.
