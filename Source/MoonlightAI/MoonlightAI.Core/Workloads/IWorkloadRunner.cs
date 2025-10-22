namespace MoonlightAI.Core.Workloads;

/// <summary>
/// Interface for workload runners that execute specific workload types.
/// </summary>
/// <typeparam name="TWorkload">The type of workload this runner handles.</typeparam>
public interface IWorkloadRunner<TWorkload> where TWorkload : Workload
{
    /// <summary>
    /// Executes the workload asynchronously.
    /// </summary>
    /// <param name="workload">The workload to execute.</param>
    /// <param name="repositoryPath">Local path to the cloned repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the workload execution.</returns>
    Task<WorkloadResult> ExecuteAsync(TWorkload workload, string repositoryPath, CancellationToken cancellationToken = default);
}
