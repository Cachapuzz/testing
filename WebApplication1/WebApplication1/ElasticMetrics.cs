using System.Text.Json;
using Elastic.Clients.Elasticsearch;

namespace WebApplication1;

public class ElasticMetrics {
     // Searches for a specific metric in elastic's metrics-* indexes and calculates it's value depending on specifics
    // available modes : "sum&count", "average", "max", "sum"
    // timeWindow: Searches all documents since "timeWindow" ago. (can be minutes, hours, days, etc)
    // size: restricts number of documents received (the most recent are captured)
    private static async Task<object?> GetMetrics(string field, int size, TimeSpan timeWindow, string mode,
        ElasticsearchClient client) {
        try {
            // Get the start time for the time window
            var startTime = DateTime.UtcNow - timeWindow;
            

            //Get documents that have specified field property
            var response = await client.SearchAsync<object>(s => s
                .Index("metrics-*")
                .SearchType(SearchType.QueryThenFetch)
                .From(0)
                .Query(q => q
                    .Bool(b => b
                        .Must(mu => mu
                            .Exists(e => e
                                .Field(field!)
                            )
                        )
                    )
                    .Range(range => range
                        .DateRange(dr => dr
                            .Field("@timestamp"!)
                            .Gte(startTime)
                        ))
                )
                .Size(size)
                .Sort(ss => ss.Field("@timestamp"!, sort => sort.Order(SortOrder.Desc)))
            );

            // Null checks
            if (!response.IsValidResponse || !response.Hits.Any()) {
                Console.WriteLine($"No valid hits found or Source is null for field: {field}");
                return null;
            }

            var total = 0.0;
            var count = 0;
            var max = 0.0;

            foreach (var hit in response.Hits) {
                var json = hit.Source?.ToString();
                if (json == null) {
                    continue;
                }

                var jsonDocument = JsonDocument.Parse(json);
                var elements = field.Split('.');
                JsonElement currentElement = jsonDocument.RootElement;

                // Parse JSON for the requested field
                foreach (var element in elements) {
                    if (currentElement.TryGetProperty(element, out var nextElement)) {
                        currentElement = nextElement;
                    }
                    else {
                        // If any part of the path doesn't exist, skip this document
                        break;
                    }
                }

                // If the value is found, and it is a number, add it to the sum
                if (currentElement.ValueKind == JsonValueKind.Number) {
                    total += currentElement.GetDouble();
                    count++;

                    // Update max value
                    if (currentElement.GetDouble() > max) max = currentElement.GetDouble();

                }
            }

            // Return the average if there were any valid documents
            if (count > 0) {
                if (mode == "sum&count") return (total, count);
                if (mode == "average") return total / count;
                if (mode == "max") return max;
                if (mode == "sum" ) return total;

                Console.WriteLine("Incompatible mode");
                return null;
            }

            Console.WriteLine($"No valid numerical values found for field: {field}");
            return null;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error parsing JSON or retrieving value: {ex.Message}");
            return null;
        }
    }

    // Calculates a few metrics with elastic information and pushes to prometheus pushgateway
    public static async Task SystemMetrics(ElasticsearchClient client) {

        // Get the most recent 100 documents that were indexed in the last 5 minutes
        var timeSpan = TimeSpan.FromMinutes(5);
        var size = 100;

        var metricDocument = new MetricDocument();

        ////////// CPU USAGE (%) //////////
        var cpuUsage = await GetMetrics("system.cpu.total.norm.pct", size, timeSpan, "average", client);
        if (cpuUsage is double cU) {
            Console.WriteLine($"CPU Total Normal Percentage: {Math.Round(cU, 2)}");
            metricDocument.CpuUsage = cU;
        }

        ////////// NORMALIZED LOAD //////////
        var load1 = await GetMetrics("system.load.1", size, timeSpan, "average",client);
        var cores = await GetMetrics("system.load.cores", size, timeSpan,"max", client);
        if (load1 is double loadValue && cores is double coreValue && coreValue != 0) {
            var normalizedLoad = loadValue / coreValue;
            Console.WriteLine($"Normalized Load: {Math.Round(normalizedLoad, 2)}");
            metricDocument.NormalizedLoad = normalizedLoad;
        }
        else Console.WriteLine("Normalized Load Error: Values are not valid doubles or cores is zero.");

        ////////// MEMORY CACHE //////////
        var usedBytes = await GetMetrics("system.memory.used.bytes", size, timeSpan, "average", client);
        var actualBytes = await GetMetrics("system.memory.actual.used.bytes", size, timeSpan, "average", client);
        if (usedBytes is double uB && actualBytes is double aB) {
            var memoryCache = uB - aB;
            Console.WriteLine($"Memory Cache: {Math.Round(memoryCache, 2)}");
            metricDocument.MemoryCache = memoryCache;
        }
        else Console.WriteLine("Memory Cache Error: Values are not valid doubles.");

        ////////// MEMORY FREE //////////
        var memoryTotal = await GetMetrics("system.memory.total", size, timeSpan, "max", client);
        var memoryActual = await GetMetrics("system.memory.actual.used.bytes", size, timeSpan, "average", client);
        if (memoryTotal is double mT && memoryActual is double mA) {
            var memoryFree = mT - mA;
            Console.WriteLine($"Memory Free: {Math.Round(memoryFree, 2)}");
            metricDocument.MemoryFree = memoryFree;
        }
        else Console.WriteLine("Memory Free Error: Values are not valid doubles.");

        ////////// MEMORY TOTAL //////////
        var sysMemoryTotal = await GetMetrics("system.memory.total", size, timeSpan, "average", client);
        if (sysMemoryTotal is double sysTotal) {
            Console.WriteLine($"Memory Total: {Math.Round(sysTotal, 2)}");
            metricDocument.MemoryTotal = sysTotal;
        }

        ////////// MEMORY USAGE (%) //////////
        var memoryUsage = await GetMetrics("system.memory.actual.used.pct", size, timeSpan, "average", client);
        if (memoryUsage is double mU) {
            Console.WriteLine($"Memory Usage (%): {Math.Round(mU, 2)}");
            metricDocument.MemoryUsage = mU;
        }

        ////////// MEMORY USED //////////
        var memoryUsed = await GetMetrics("system.memory.actual.used.bytes", size, timeSpan, "average", client);
        if (memoryUsed is double mUsed) {
            Console.WriteLine($"Memory Used: {Math.Round(mUsed, 2)}");
            metricDocument.MemoryUsed = mUsed;
        }

        ////////// NETWORK INBOUND (RX) //////////
        var netIn = await GetMetrics("host.network.ingress.bytes", size, timeSpan, "sum", client);
        if(netIn is double nI){
            var networkInbound = nI * 8 / 100;
            Console.WriteLine($"Network Inbound (RX): {Math.Round(networkInbound, 2)}");
            metricDocument.NetworkInbound = networkInbound;
        }

        ////////// NETWORK OUTBOUND (TX) //////////
        var netOut = await GetMetrics("host.network.egress.bytes", size, timeSpan, "sum", client);
        if(netOut is double nO){
            var networkOutbound = nO * 8 / 100;
            Console.WriteLine($"Network Outbound (TX): {Math.Round(networkOutbound, 2)}");
            metricDocument.NetworkOutbound = networkOutbound;
        }

        ////////// DISK LATENCY //////////
        // Average(Read + Write Time)
        var diskTimeAvg = 0.0;
        var diskRead = await GetMetrics("system.diskio.read.time", size, timeSpan, "sum&count", client);
        var diskWrite = await GetMetrics("system.diskio.write.time", size, timeSpan, "sum&count", client);
        if (diskRead is ValueTuple<double, int> dR && diskWrite is ValueTuple<double, int> dW) {
            diskTimeAvg = (dR.Item1 + dW.Item1) / (dR.Item2 + dW.Item2);
        }

        // Read Count (in specified time span)
        var readCount = await GetMetrics("system.diskio.read.count", size, timeSpan, "sum", client);

        // Write Count (in specified time span)
        var writeCount = await GetMetrics("system.diskio.write.count", size, timeSpan, "sum", client);

        if (readCount is double rC && writeCount is double wC) {
             var diskLatency = diskTimeAvg / (rC + wC);
             Console.WriteLine($"DISK LATENCY: {Math.Round(diskLatency, 4)}");
             metricDocument.DiskLatency = diskLatency;
        }

        //Console.WriteLine($"diskTimeAvg: {Math.Round(diskTimeAvg, 2)}");
        //Console.WriteLine($"readCount: {readCount}");
        //Console.WriteLine($"writeCount: {writeCount}");

        ////////// DISK READ THROUGHPUT ////////// (Not sure how to get this yet. Not a priority right now)

        ////////// DISK USAGE - AVAILABLE (%) ////////// (Don't think this is working)
        var diskUsage = GetMetrics("system.filesystem.used.pct", size, timeSpan, "average", client).Result;
        if (diskUsage is double dU) {
            var diskAvailable = 1 - dU;
            Console.WriteLine($"DISK USAGE - AVAILABLE (%): {Math.Round(diskAvailable, 2)}");
            metricDocument.DiskUsageAvailable = diskAvailable;
        }

        ////////// DISK USAGE - MAX (%) ////////// (Don't think this is working)
        var maxDisk = await GetMetrics("system.filesystem.used.pct", size, timeSpan, "max", client);
        if (maxDisk is double mD) {
            Console.WriteLine($"DISK USAGE - MAX (%): {Math.Round(mD, 2)}");
            metricDocument.DiskUsageMax = mD;
        }
        
        // Push to prometheus pushgateway
        var performancePusher = new PrometheusPusher();
        await performancePusher.PushMetrics(metricDocument);

        return;
    }
}