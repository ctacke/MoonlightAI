# MoonlightAI

> **Your nightly AI assistant for automated code maintenance**

```
     ....:\\%.
   . .....:\\%##
 .. ..;.....;\\%@#
. ..:..:....:o\\%##    -=|MoonlightAI
..  ........;!\\%##  
 ...  .....:|\\%%#
   .net.....;\%%#
     ...._!\%*.
```

MoonlightAI is an AI-powered .NET development automation tool that handles tedious coding tasks while you sleep. It clones your repositories, generates documentation, cleans up code, validates builds, and creates pull requests—all automatically.

## ✨ Features

- 🤖 **AI-Powered Automation** - Uses local LLM models (CodeLlama) via Ollama
- 📝 **Code Documentation** - Generates XML documentation for C# methods, properties, fields, and events
- 🧹 **Code Cleanup** - Automated refactoring and code quality improvements
- 🔄 **Build Validation** - Tests every change and auto-corrects AI mistakes
- 📦 **Git Integration** - Automatic branch creation, commits, and PR generation
- 🐳 **Docker Container Management** - Auto-manages local AI server containers
- 📊 **Performance Tracking** - SQLite database tracks runs, statistics, and model performance
- 💻 **Terminal UI** - Real-time progress monitoring with batch status
- 🔒 **Safe by Design** - Reverts changes that fail build validation

---

## 🎯 What is MoonlightAI?

MoonlightAI automates the repetitive development tasks that, let's face it, you don't like or want to do. Don't use your precious time and limited brain power - offload running that stuff to MoonlightAI and have it run overnight or while you're eating lunch.

MoonlightAI uses local AI models to analyze your codebase, understand context, and generate high-quality documentation and improvements. It validates every change by building your project, automatically fixes AI mistakes, and creates pull requests for human review.

So if you're a .NET developer tired of writing XML comments or cleaning up code, MoonlightAI is here to help!


---

## 🚀 Goals & Vision

**Current Goals:**
- Automate XML documentation generation for C# codebases
- Perform safe, validated code cleanup operations
- Run unattended (nightly) to keep repositories maintained
- Free developers to focus on feature development

**Future Vision:**
- Unit test generation
- Multi-language support (beyond C#)
- Advanced refactoring operations
- Support for additional AI models and providers

---

## ⚙️ How It Works - The Workload System

MoonlightAI operates on a **workload-based** model. A **workload** is a specific automated task (like "generate documentation" or "clean up code") that processes files in your repository.

### The Workflow

MoonlightAI follows these steps for each workload:

```
1. Clone/Pull Repository
   └─> Fetch latest code from GitHub

2. Create Workload Branch
   └─> git checkout -b moonlight/{date}-{workload}

3. Analyze Files
   └─> Use Roslyn to parse C# code
   └─> Identify what needs work

4. Process Files (this is called a Batch)
   └─> For each file:
       ├─> Send code to AI model
       ├─> Receive generated documentation/changes
       ├─> Sanitize AI response (remove hallucinations)
       ├─> Apply changes to file
       ├─> Build solution to validate
       └─> If build fails:
           ├─> Send errors back to AI
           ├─> Retry with corrections (up to N times)
           └─> Revert file if unfixable

5. Commit Changes
   └─> Git commit with detailed statistics

6. Create Pull Request
   └─> Submit PR via GitHub API
   └─> Include statistics and review notes
```

### Safety Features

✅ **Build Validation Loop** - Every change is validated by building the project
✅ **AI Error Correction** - Failed builds are sent back to AI for fixes (configurable retries)
✅ **Automatic Revert** - Files that can't be fixed are reverted
✅ **Human Review** - All changes go through PR process before merging
✅ **Hallucination Detection** - Sanitizes AI responses to remove invalid parameters, duplicate tags, etc.

---

## 📦 Available Workloads

### Code Documentation Workload ✅ **Working**

[Generates XML documentation comments for C# code using AI.](docs/code-doc-workload.md)

### Code Cleanup Workload ✅ **In Process**

[Performs automated refactoring and code quality improvements.](docs/code-cleanup-workload.md)

---

## 🚀 Getting Started

Get up and running with MoonlightAI in just a few minutes!

### Quick Start

```bash
# 1. Clone and navigate to the repository
git clone https://github.com/ctacke/MoonlightAI.git
cd MoonlightAI

# 2. Copy and configure appsettings
cp Source/MoonlightAI/MoonlightAI.CLI/appsettings.json.template \
   Source/MoonlightAI/MoonlightAI.CLI/appsettings.json
# Edit appsettings.json with your GitHub PAT and repository URL

# 3. Build and run
dotnet build Source/MoonlightAI/MoonlightAI.sln
dotnet run --project Source/MoonlightAI/MoonlightAI.CLI/MoonlightAI.CLI.csproj
```

**📖 For detailed setup instructions, prerequisites, and troubleshooting, see the [Getting Started Guide](docs/getting-started.md)**

---

## ⚙️ Configuration

MoonlightAI uses `appsettings.json` for configuration. Key settings include:

- **GitHub PAT** - Required for cloning repositories and creating pull requests
- **Repository URLs** - The repositories MoonlightAI will process
- **AI Server** - Local Docker container or remote Ollama server
- **Workload Settings** - Enable/disable workloads and configure batch sizes
- **Container Management** - Auto-start/stop Docker containers

**📖 For complete configuration reference, see the [Configuration Guide](docs/configuration.md)**

---

## 💻 Hardware & Model Recommendations

### Recommended Hardware

MoolightAI can be run with your LLMs on a local server in your network, so a separate machine from where MoonlightAI itself is running, or on the same machine.  When on the same machine, you can be running Ollama directly, or inside a Docker container.  Flexibility is key here.

Most development right now is done with a local Docker container on a machine with an NVIDIA RTX 3060 GPU with 12GB of VRAM.  This setup works well for 7b and 13b models.

### AI Model Recommendations

The "best" model for you you depends on your hardware and the workload.  

Broadly speaking, more iron with bigger models will get better/faster results, but even modest setups can get good results with the right model and prompts.

For example, I've used a Macbook 2014 re-paved with Debian.  It works, but only with small (i.e. 7b-ish) models and is sloooooow :snail:.  But if it's running unattended, slow is often fine.

Using an RTX 3060 with 12GB or VRAM, I find that Mistral 7b-Instruct actually generates better (i.e. less sanitization hits) docs than CodeLlama 13b-Instruct.  Feel free to test other models and improve the prompts.  If you find solid recommendations, let me know and we can add them in.

---


## 🛠️ Technology Stack

**Core Technologies:**
- **.NET 8.0** - Target framework
- **C# 12** - Language with nullable reference types
- **Roslyn** - C# code analysis and manipulation
- **LibGit2Sharp** - Git operations
- **Octokit** - GitHub API integration
- **Entity Framework Core** - Database ORM
- **SQLite** - Local database storage
- **Terminal.Gui** - Terminal-based UI
- **xUnit** - Testing framework
- **Docker** - Container management
- **Ollama** - Local LLM inference

**Key Dependencies:**
- `Microsoft.CodeAnalysis.CSharp 4.11.0` - Roslyn compiler
- `LibGit2Sharp 0.30.0` - Git operations
- `Octokit 13.0.1` - GitHub API
- `Microsoft.EntityFrameworkCore.Sqlite 8.0.0` - Database
- `Terminal.Gui 1.19.0` - Terminal UI

---

## 🔄 Development Status

### ✅ Fully Implemented

- AI server connection (CodeLlama via Ollama)
- Git repository management (clone, branch, commit, PR)
- Docker container management (auto-start/stop)
- **Code Documentation Workload** (methods, properties, fields, events)
- Build validation with AI error correction
- Database tracking and model comparison
- Terminal UI with real-time progress

### ⏳ In Progress

- Always improving the CodeDoc workload.  It's currently usable, but I use it daily and as I find behaviors I dislike, I improve it.
- Code cleanup is a work in progress. The general skeleton is there, but the performance is not great yet.

### 🎯 Planned

Mid-term goals are not well defined beyond "concepts" that I want to implement.  These include:
- unit test generation workload
- code formatting to match style guides
- cron-like scheduling system


## 🤝 Contributing

MoonlightAI is and is open source in early development. Contributions are welcome!

We need:
- more workloads implemented
- testing of existing workloads on more code bases, with different models, and different GPUs
- improved prompts for the above

---

## 📄 License

MoonlightAI is licensed under the [MIT License](LICENSE).

---

