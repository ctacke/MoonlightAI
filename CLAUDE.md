# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MoonlightAI is a nightly AI assistant designed to pull code and perform automated development tasks (documentation, cleanup, unit tests) while developers focus on more interesting work. The project is in early development stages.

## Solution Structure

- **MoonlightAI.CLI** - Console application entry point (net8.0)
- **MoonlightAI.Core** - Core library containing business logic and orchestration (net8.0)
- **MoonlightAI.Tests** - xUnit integration tests for the core library (net8.0)

Solution file: `Source/MoonlightAI/MoonlightAI.sln`

## Build and Test Commands

```bash
# Build the solution
dotnet build Source/MoonlightAI/MoonlightAI.sln

# Build specific configuration
dotnet build Source/MoonlightAI/MoonlightAI.sln --configuration Release

# Run the CLI application
dotnet run --project Source/MoonlightAI/MoonlightAI.CLI/MoonlightAI.CLI.csproj

# Restore dependencies
dotnet restore Source/MoonlightAI/MoonlightAI.sln

# Run all tests
dotnet test Source/MoonlightAI/MoonlightAI.sln

# Run tests with detailed output
dotnet test Source/MoonlightAI/MoonlightAI.sln --logger "console;verbosity=detailed"

# Run specific test class
dotnet test Source/MoonlightAI/MoonlightAI.Tests --filter "FullyQualifiedName~CodeLlamaServerIntegrationTests"
```

## Architecture

### Implemented Components

**AI Server Connection** (✓ Implemented)
- Interface-based design with `IAIServer` for swappable AI providers
- `CodeLlamaServer` implementation connecting to Ollama API
- Configuration-driven (appsettings.json) with `AIServerConfiguration`
- Request/Response DTOs for structured communication (`AIRequest`, `AIResponse`)
- Health check capability and basic error handling
- Dependency injection setup with HttpClient

Key files:
- `MoonlightAI.Core/IAIServer.cs` - AI server interface
- `MoonlightAI.Core/Servers/CodeLlamaServer.cs` - CodeLlama implementation
- `MoonlightAI.Core/Models/` - Request/response DTOs
- `MoonlightAI.Core/Configuration/AIServerConfiguration.cs` - Configuration model

**Git Repository Management** (✓ Implemented)
- Interface-based design with `IGitManager` for git operations and GitHub interactions
- Uses **LibGit2Sharp** for local git operations (clone, pull, branch, commit, push)
- Uses **Octokit** for GitHub API operations (list PRs, create PRs)
- Personal Access Token authentication
- Configurable working directory for cloned repositories
- Support for checking existing PRs before creating new workload branches

Key files:
- `MoonlightAI.Core/IGitManager.cs` - Git manager interface
- `MoonlightAI.Core/Git/GitManager.cs` - Git operations implementation
- `MoonlightAI.Core/Models/RepositoryConfiguration.cs` - Repository model with URL parsing
- `MoonlightAI.Core/Configuration/GitHubConfiguration.cs` - GitHub settings

**Docker Container Management** (✓ Implemented)
- Interface-based design with `IContainerManager` for Docker lifecycle management
- `DockerContainerManager` implementation for managing local AI server containers
- Auto-start container before workloads if configured
- Auto-stop and prune container after workloads complete to save resources
- Health checks to ensure container is running before executing workloads
- Configuration-driven with `ContainerConfiguration`

Key files:
- `MoonlightAI.Core/Containerization/IContainerManager.cs` - Container manager interface
- `MoonlightAI.Core/Containerization/DockerContainerManager.cs` - Docker implementation
- `MoonlightAI.Core/Containerization/Dockerfile` - Docker image definition for CodeLlama
- `MoonlightAI.Core/Configuration/ContainerConfiguration.cs` - Configuration model

### Planned Component Architecture

The system is designed around several key managers that orchestrate the AI-powered workflow:

1. **AIServer** ✓ - Handles connections to AI model APIs (CodeLlama via Ollama)
2. **GitManager** ✓ - Git operations including clone, pull, branch creation, PR management
3. **ProjectLoader** - Loads and manages .NET project/solution files
4. **BuildManager** - Builds projects and captures build results for feedback loops
5. **Workload Manager** - Defines and executes different types of automated tasks
6. **MoonlightAI Orchestrator** - Main workflow coordinator that ties all components together

### Workload System

MoonlightAI operates on a workload-based model where each workload represents a specific automated task:

- **Code Documentation** - Adds XML documentation to public methods, classes, and properties
- **Code Cleanup** - Formatting, removing unused code, simplifying implementations
- **Unit Tests** - Generates unit tests for existing code

### Git Workflow

- Before running workloads, checks for existing PRs to avoid duplicate work
- Creates a branch for each workload run: `moonlight/{date}-{workload-name}`
- After successful workload completion, creates a PR with changes
- Pulls from repositories defined in `moonlight.config` (configuration format TBD)

### Build Verification Loop

The system includes a validation cycle:
1. AI modifies code file
2. Project is built to verify changes
3. If build fails, error is sent back to AI for correction
4. If build succeeds, changes are committed and PR is created

## Configuration

Configuration is managed through `appsettings.json` files. **IMPORTANT**: These files contain secrets and are in `.gitignore`. Use the template files to create your local configuration.

### Setup Configuration

1. Copy `appsettings.json.template` to `appsettings.json` in the CLI project
2. Update the `PersonalAccessToken` with your GitHub PAT
3. Adjust other settings as needed

### AI Server Configuration

```json
{
  "AIServer": {
    "ServerUrl": "http://192.168.4.231:11434",
    "ModelName": "codellama",
    "TimeoutSeconds": 300
  }
}
```

### GitHub Configuration

**IMPORTANT**: Never commit your GitHub Personal Access Token. It's protected by `.gitignore`.

```json
{
  "GitHub": {
    "PersonalAccessToken": "your-github-pat-here",
    "DefaultBranch": "main",
    "WorkingDirectory": "./repositories",
    "UserName": "MoonlightAI",
    "UserEmail": "moonlight@example.com"
  }
}
```

**GitHub PAT Requirements**:
- Scope: `repo` (full control of private repositories)
- Used for: cloning, pushing, creating pull requests
- Can be generated at: https://github.com/settings/tokens

### Repository Configuration

```json
{
  "Repositories": [
    {
      "RepositoryUrl": "https://github.com/SolutionFamily/solution-family"
    }
  ]
}
```

### Container Configuration

MoonlightAI can automatically manage a local Docker container for the AI server.

```json
{
  "Container": {
    "UseLocalContainer": false,
    "ContainerName": "moonlight-llm-server",
    "AutoStart": true,
    "AutoStop": true,
    "PruneAfterStop": true
  }
}
```

**Container Configuration Options**:
- `UseLocalContainer`: Enable local Docker container management (default: false)
- `ContainerName`: Name of the Docker container to manage (default: "moonlight-llm-server")
- `AutoStart`: Automatically start the container before workloads if not running (default: true)
- `AutoStop`: Automatically stop the container after all workloads complete (default: true)
- `PruneAfterStop`: Prune stopped containers after stopping to save resources (default: true)

**Container Workflow**:
1. Before executing workloads, MoonlightAI checks if the container is running
2. If `AutoStart` is enabled and the container is not running, it starts the container
3. After all workloads complete, if `AutoStop` is enabled, the container is stopped
4. If `PruneAfterStop` is enabled, Docker prune is run to reclaim resources

**Building the Container**:
```bash
cd Source/MoonlightAI/MoonlightAI.Core/Containerization
docker build -t moonlight-llm-server .
```

The Dockerfile uses the Ollama base image and pre-pulls the CodeLlama 13b-instruct model.

Environment-specific overrides can be placed in `appsettings.Development.json`.

## Development Notes

- Target framework: .NET 8.0
- Nullable reference types enabled
- Implicit usings enabled
- Uses Microsoft.Extensions.* for dependency injection, configuration, and logging
- Integration tests require:
  - Running Ollama server with CodeLlama model
  - Valid GitHub PAT set in `GITHUB_PAT` environment variable for GitManager tests
  - Most GitManager tests are marked with `Skip` to prevent accidental repository modifications

## Security

- `appsettings.json` files are in `.gitignore` to prevent committing secrets
- Use `appsettings.json.template` files as reference for configuration structure
- GitHub Personal Access Token should never be committed to source control
- The `repositories/` directory (where repos are cloned) is also in `.gitignore`
