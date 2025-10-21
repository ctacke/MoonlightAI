# MoonlightAI

MoonlighAI is a nightly AI assistant designed to pull code and do menial tasks for developers while they do other, more interesting things.

## Process

## Workloads

MoonlightAI is built around scheduled workloads.

The following workloads are planned:

- code documentation
  These workloads add XML documentation to public methods, classes, and properties in C# codebases.
- code clean up
  These workloads do general code clean-up such as fomatting, removing unused sections, and simplifying code.
- unit tests
  These workloads generate simple unit tests for code

## Code pulls

Before running workloads, MoonlightAI pulls code from a git repository.  These are defined in the `moonlight.config` file, which provides all of the information in needs to connect, clone and pull.

MoonlightAI checks for existing PRs before running workloads to prevent running a workload that has been completed, but not yet reviewed or merged.

Moonlight uses a branch for each workload run, named using the pattern `moonlight/{date}-{workload-name}`.

## Results

After a workload is run, MoonlightAI creates a pull request with the changes it made. All MoonlightAI   The pull request includes a summary of the changes made and any relevant information about the workload that was run.


## Process

The general process for MoonlightAI is as follows:

- Pull (or clone) the latest code from the repository
- Choose a workload to run, verifying that there are no existing PRs for that workload
- Load the project/solution defined in the workload
- Choose a code file from the project/solution
- Send the code file to the AI server with instructions for the workload
- Receive the modified code from CodeLlama
- Replace the code file in the project/solution with the modified code
- Build the project/solution to verify that the changes did not break the build
- If the build is successful, create a pull request with the changes
- If the build fails, send the error to the AI server

## AI Server Support

- CodeLlama