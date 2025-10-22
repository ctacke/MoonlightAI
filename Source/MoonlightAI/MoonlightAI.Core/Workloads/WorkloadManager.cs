namespace MoonlightAI.Core.Workloads;

public class WorkloadManager
{
    private readonly List<Workload> _workloads = new();

    public void AddWorkload(Workload workload)
    {
        _workloads.Add(workload);
    }

    public IEnumerable<Workload> GetAllWorkloads()
    {
        return _workloads;
    }
}
