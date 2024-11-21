using Newtonsoft.Json;

public class MetricDocument
{
    [JsonProperty("system.cpu.total.norm.pct")]
    public double? CpuPct { get; set; }

    [JsonProperty("system.memory.used.pct")]
    public double? MemoryPct { get; set; }

    [JsonProperty("@timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonProperty("host.name")]
    public string? HostName { get; set; }
}