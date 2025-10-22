namespace MoonlightAI.Core.Workloads;

public class CodeDocWorkload : Workload
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string SolutionPath { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
}
