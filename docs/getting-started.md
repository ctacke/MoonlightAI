# Getting Started with MoonlightAI

This guide will walk you through setting up and running MoonlightAI for the first time.

## Prerequisites

Before you begin, ensure you have the following installed and configured:

### Required

- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
  - Verify installation: `dotnet --version`
- **Git** - [Install](https://git-scm.com/downloads)
  - Verify installation: `git --version`
- **GitHub Personal Access Token** - [Generate](https://github.com/settings/tokens)
  - Scope required: `repo` (full control of private repositories)
  - ⚠️ Keep this token secure and never commit it to source control

### Optional (but Recommended)

- **Docker** - [Install](https://docs.docker.com/get-docker/)
  - Required if running local AI server in a container
  - Verify installation: `docker --version`
- **NVIDIA GPU with CUDA** - For running local AI models efficiently
  - 8-13GB VRAM recommended for CodeLlama 13b
  - Check GPU: `nvidia-smi`

### Alternative: Remote AI Server

If you don't have a GPU or prefer not to run AI locally, you can:
- Use a remote Ollama server on your network
- Use a cloud-hosted AI instance
- See [Configuration Guide](configuration.md) for remote server setup

---

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/ctacke/MoonlightAI.git
cd MoonlightAI
```

### 2. Copy Configuration Template

```bash
cp Source/MoonlightAI/MoonlightAI.CLI/appsettings.json.template \
   Source/MoonlightAI/MoonlightAI.CLI/appsettings.json
```

### 3. Configure MoonlightAI

Edit the newly created `appsettings.json` file:

```bash
# Linux/macOS
nano Source/MoonlightAI/MoonlightAI.CLI/appsettings.json

# Windows
notepad Source\MoonlightAI\MoonlightAI.CLI\appsettings.json
```

**Minimum required configuration:**

1. **GitHub PAT:** Update `GitHub.PersonalAccessToken` with your token
2. **Repository URL:** Add your repository to `Repositories` array
3. **Project paths:** Set `Workload.CodeDocumentation.ProjectPath` and `SolutionPath`

See the [Configuration Guide](configuration.md) for detailed configuration options.

### 4. Build the Solution

```bash
dotnet build Source/MoonlightAI/MoonlightAI.sln
```

If the build succeeds, you'll see:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### 5. (Optional) Build Local AI Container

If using a local Docker container for AI:

```bash
cd Source/MoonlightAI/MoonlightAI.Core/Containerization
docker build -t moonlight-llm-server .
```

This will:
- Pull the Ollama base image
- Pre-download CodeLlama 13b-instruct model (~7GB)
- Take 10-20 minutes depending on your connection

### 6. Run MoonlightAI

```bash
cd ../../../..  # Return to repo root
dotnet run --project Source/MoonlightAI/MoonlightAI.CLI/MoonlightAI.CLI.csproj
```

You should see the MoonlightAI terminal UI:

```
═══════════════════════════════════════════════════════════════
  MoonlightAI - AI-Powered Code Documentation
═══════════════════════════════════════════════════════════════

Available Commands:
  run doc      - Run code documentation workload
  run cleanup  - Run code cleanup workload
  stop         - Stop current workload (saves completed work)
  report       - View model comparison report
  stats        - Show current statistics
  batch X      - Set batch size to X files
  clear        - Clear log output
  exit         - Exit application

Type a command and press Enter to execute.
═══════════════════════════════════════════════════════════════
```

---

## Your First Workload Run

### Step 1: Test Your Configuration

Before running a full workload, verify your setup:

1. **Check AI server connectivity:**
   - If using local container with `AutoStart: true`, MoonlightAI will start it automatically
   - If using remote server, ensure it's accessible at the configured URL

2. **Verify repository access:**
   - Ensure your GitHub PAT has access to the repository
   - Repository will be cloned to the `WorkingDirectory` path

### Step 2: Run Code Documentation Workload

In the MoonlightAI terminal, type:

```
run doc
```

MoonlightAI will:
1. Clone/pull your repository
2. Create a branch: `moonlight/{date}-codedoc`
3. Analyze C# files for undocumented members
4. Process files in batches (configurable batch size)
5. Generate XML documentation using AI
6. Validate changes by building the solution
7. Create a pull request with the changes

**Watch the progress:**
- Real-time log output shows what's being processed
- Batch progress indicator shows X/Y files completed
- Statistics panel shows tokens used, files processed, errors

### Step 3: Review the Pull Request

When the workload completes:

1. You'll see: `Pull Request: https://github.com/yourrepo/pull/123`
2. Open the PR in your browser
3. Review the generated documentation:
   - Check accuracy of descriptions
   - Verify parameter documentation
   - Look for any AI mistakes (rare after sanitization)
4. Merge if satisfied, or request changes

---

## CLI Commands Reference

| Command | Description | Example |
|---------|-------------|---------|
| `run doc` | Run code documentation workload | Generates XML docs |
| `run cleanup` | Run code cleanup workload | Refactors and cleans code |
| `stop` | Stop current workload gracefully | Saves progress, commits completed files |
| `report` | View model comparison report | Shows success rates, token usage |
| `stats` | Show current run statistics | Files processed, errors, tokens |
| `batch X` | Set batch size to X files | `batch 5` - process 5 files per run |
| `clear` | Clear the log output | Clears terminal display |
| `exit` | Exit MoonlightAI | Closes application |

---

## Troubleshooting

### Build Fails: Project Not Found

**Error:** `Project file does not exist`

**Solution:**
- Check `Workload.CodeDocumentation.ProjectPath` in `appsettings.json`
- Path should be relative to repository root
- Example: `src/MyProject` not `MyProject`

### Build Fails: Solution Not Found

**Error:** `Solution file does not exist`

**Solution:**
- Check `Workload.CodeDocumentation.SolutionPath` in `appsettings.json`
- Path should be relative to repository root
- Example: `MySolution.sln` or `src/MySolution.sln`

### AI Server Connection Failed

**Error:** `Failed to connect to AI server`

**Solutions:**

1. **Local container not running:**
   ```bash
   docker ps  # Check if moonlight-llm-server is running
   docker start moonlight-llm-server  # Start it manually
   ```

2. **Wrong server URL:**
   - Check `AIServer.ServerUrl` in `appsettings.json`
   - Local: `http://localhost:11434`
   - Remote: Use actual server IP/hostname

3. **Model not available:**
   ```bash
   docker exec moonlight-llm-server ollama list
   # Should show codellama:13b-instruct
   ```

### GitHub Authentication Failed

**Error:** `Unauthorized` or `Bad credentials`

**Solutions:**

1. **PAT not set:**
   - Check `GitHub.PersonalAccessToken` in `appsettings.json`
   - Should start with `ghp_` or `github_pat_`

2. **PAT has wrong scope:**
   - Regenerate token with `repo` scope
   - https://github.com/settings/tokens

3. **PAT expired:**
   - Generate a new token
   - Update `appsettings.json`

### No Files Processed

**Message:** `No modifications were needed`

**Possible causes:**

1. **All members already documented:**
   - MoonlightAI skips members with existing XML docs
   - Check if documentation already exists

2. **Visibility filter:**
   - Check `DocumentVisibility` setting
   - Default: `Public,Internal`
   - Add `Private,Protected` if needed

3. **No eligible members:**
   - Only documents methods, properties, readonly fields, constants, events
   - Regular fields are not documented

### Out of Memory / GPU OOM

**Error:** CUDA out of memory or system crashes

**Solutions:**

1. **Use smaller model:**
   - Switch to `codellama:7b-instruct`
   - Or `mistral:7b-instruct`

2. **Use remote server:**
   - Point to a server with more VRAM
   - Set `Container.UseLocalContainer: false`

3. **Reduce batch size:**
   - `batch 1` - process one file at a time
   - Reduces memory pressure

### Build Validation Keeps Failing

**Message:** Multiple build retry attempts

**Solutions:**

1. **Check build locally first:**
   ```bash
   cd repositories/your-repo
   dotnet build YourSolution.sln
   ```
   Fix any existing build errors before running MoonlightAI

2. **Increase retry count:**
   - Set `Workload.MaxBuildRetries: 5` for more attempts

3. **Disable build validation temporarily:**
   - Set `Workload.ValidateBuilds: false`
   - ⚠️ Use with caution - may generate broken code

---

## Next Steps

Now that you have MoonlightAI running:

1. **Review the [Configuration Guide](configuration.md)** for advanced settings
2. **Check the [Model Recommendations](../README.md#-hardware--model-recommendations)** to optimize performance
3. **Run `report`** after a few workloads to compare model performance
4. **Adjust batch size** based on your hardware and repository size
5. **Schedule nightly runs** (coming soon - currently manual)

---

## Getting Help

- **Issues:** [GitHub Issues](https://github.com/ctacke/MoonlightAI/issues)
- **Discussions:** [GitHub Discussions](https://github.com/ctacke/MoonlightAI/discussions)
- **Documentation:** See `/CLAUDE.md` for development details

---

**Happy automating! 🚀**
