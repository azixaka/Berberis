namespace Berberis.SampleApp.Examples.ProcessInfo;

public struct ProcessInfo
{
    public DateTime Timestamp { get; set; }
    public int ProcessId { get; set; }
    public string Name { get; set; }
    public double CpuTimeMs { get; set; }
}
