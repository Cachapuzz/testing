using RestSharp;
using System;
using System.Threading.Tasks;

namespace WebApplication1;

public class PrometheusPusher {
    private const string PushgatewayUrl = "http://192.168.2.16:9091/metrics/job/cpu_metrics";

    public async Task PushSingleMetric(string metricName, double value) {
        var client = new RestClient(PushgatewayUrl);
        var request = new RestRequest(PushgatewayUrl) {
            Method = Method.Put
        };
 
        var metric = $"{metricName} {value}\n";
        request.AddHeader("Content-Type", "text/plain");
        request.AddParameter("text/plain", metric, ParameterType.RequestBody);
        
        // Send the metrics
        var response = await client.ExecuteAsync(request);
        if (response.IsSuccessful) {
            Console.WriteLine("Metric pushed successfully!");
        }
        else {
            Console.WriteLine($"Failed to push metric: {response.StatusDescription}");
        }
    }
    
    public async Task PushMetrics(MetricDocument metric) {
        var client = new RestClient(PushgatewayUrl);
        var request = new RestRequest(PushgatewayUrl) {
            Method = Method.Put
        };

        // Prepare the metrics in Prometheus exposition format
        var metrics = "# HELP system_cpu_usage CPU usage as a percentage\n" +
                      $"# TYPE system_cpu_usage gauge\n" +
                      $"system_cpu_usage {metric.CpuUsage ?? 0.0}\n" +
                      $"system_normalized_load {metric.NormalizedLoad ?? 0.0}\n" +
                      $"system_memory_cache {metric.MemoryCache ?? 0.0}\n" +
                      $"system_memory_free {metric.MemoryFree ?? 0.0}\n" +
                      $"system_memory_total {metric.MemoryTotal ?? 0.0}\n" +
                      $"system_memory_usage {metric.MemoryUsage ?? 0.0}\n" +
                      $"system_network_inbound {metric.NetworkInbound ?? 0.0}\n" +
                      $"system_network_outbound {metric.NetworkOutbound ?? 0.0}\n" +
                      $"system_disk_latency {metric.DiskLatency ?? 0.0}\n" +
                      $"system_disk_usage_available {metric.DiskUsageAvailable ?? 0.0}\n" +
                      $"system_disk_usage_max {metric.DiskUsageMax ?? 0.0}\n";

        request.AddHeader("Content-Type", "text/plain");
        request.AddParameter("text/plain", metrics, ParameterType.RequestBody);

        // Send the metrics
        var response = await client.ExecuteAsync(request);
        if (response.IsSuccessful) {
            Console.WriteLine("Metrics pushed successfully!");
        }
        else {
            Console.WriteLine($"Failed to push metrics: {response.StatusDescription}");
        }
    }
}