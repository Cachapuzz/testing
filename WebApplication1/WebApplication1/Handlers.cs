using System.Drawing;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using MongoDB.Bson;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace WebApplication1;

public static class Handlers {
    public static string HelloElastic() {
        Console.WriteLine("Hello Elastic!");

        return "Hello Elastic!";
    }

    public static DocumentModel? CreateDocument(string name, string description, ElasticsearchClient client) {
        var document = new DocumentModel() {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        var responseInsert = client.IndexAsync(document, i => i
            .Pipeline("Added timestamp")
            .Id(document.Id)
        );

        responseInsert.Wait();

        Console.WriteLine("Inserted document {0}", responseInsert.Result);

        return document;
    }


    public static object? GetValueFromJsonPath(string json, string path) {
        try
        {
            var jsonDocument = JsonDocument.Parse(json);
            var elements = path.Split('.'); // Split the dot-separated path
            JsonElement currentElement = jsonDocument.RootElement;

            foreach (var element in elements)
            {
                if (currentElement.TryGetProperty(element, out var nextElement))
                {
                    currentElement = nextElement;
                }
                else
                {
                    // If any part of the path doesn't exist, return null
                    return null;
                }
            }

            // Return the value as the appropriate type
            return currentElement.ValueKind switch
            {
                JsonValueKind.String => currentElement.GetString(),
                JsonValueKind.Number => currentElement.GetDouble(), // Adjust to GetInt32, GetDecimal, etc., if needed
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => currentElement.ToString(), // Handle other kinds as string
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing JSON or retrieving value: {ex.Message}");
            return null;
        }
    }
    
    public static async Task<object?> HostMetrics(ElasticsearchClient client) {
        var response = await client.SearchAsync<MetricDocument>(s => s
            .Index("metrics-*")
            .SearchType(SearchType.QueryThenFetch) // Ensure shard-level sorting
            .From(0)
            .Size(1)
            .Sort(ss => ss.Field("@timestamp", s => s.Order(SortOrder.Desc)))
        );

        var responseCpu = await client.SearchAsync<object>(s => s
            .Index("metrics-*")
            .SearchType(SearchType.QueryThenFetch)
            .From(0)
            .Query(q => q
                .Bool(b => b
                    .Must(mu => mu
                        .Exists(e => e
                            .Field("system.cpu.total.norm.pct")
                        )
                    )
                )
            )
            .Size(1)
            .Sort(ss => ss.Field("@timestamp", s => s.Order(SortOrder.Desc)))
        );

        if (responseCpu.IsValidResponse && responseCpu.Hits.Any()) {

            Console.WriteLine($" type = {responseCpu.Hits.First().Source}");
            
            
            var source = responseCpu.Hits.First().Source.ToString();
            var jsonDocument = JsonDocument.Parse(source);
            
            //get system.cpu.total.norm.pct
            if (jsonDocument.RootElement.TryGetProperty("system", out var systemElement) &&
                systemElement.TryGetProperty("cpu", out var cpuElement) &&
                cpuElement.TryGetProperty("total", out var totalElement) &&
                totalElement.TryGetProperty("norm", out var normElement) &&
                normElement.TryGetProperty("pct", out var pctElement))
            {
                var pct = pctElement.GetDouble();
                Console.WriteLine($"CPU Total Norm Pct: {pct}");
            }
        }


        if (response.IsValidResponse && response.Hits?.Any() == true) {
            var metrics = response.Hits.First().Source;
            Console.WriteLine(metrics.ToJson());
            return new {
                Timestamp = metrics.Timestamp,
                CpuUsage = metrics.CpuPct,
                MemoryUsage = metrics.MemoryPct,
                HostName = metrics.HostName
            };
        }

        return new { Error = "Metrics not found for the specified host." };
    }

    public static List<DocumentModel> GetDocumentsByField(string fieldName, string fieldValue,
        ElasticsearchClient client) {
        try {
            var response = client.Search<DocumentModel>(s => s
                .Query(q => q
                    .Match(m => m
                            .Field(fieldName) // Dynamically set the field to search
                            .Query(fieldValue) // Value to match
                    )
                )
            );

            if (response.IsValidResponse && response.Hits.Any()) {
                var documents = response.Hits.Select(hit => hit.Source).ToList();
                Console.WriteLine($"Fetched {documents.Count} documents where '{fieldName}' equals '{fieldValue}'.");
                return documents!;
            }

            Console.WriteLine($"No documents found where '{fieldName}' equals '{fieldValue}'.");
            return new List<DocumentModel>();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error fetching documents by field: {ex.Message}");
            throw;
        }
    }

    public static bool DeleteDocumentById(string id, ElasticsearchClient client) {
        try {
            var response = client.Delete<DocumentModel>(id);

            if (response.IsValidResponse) {
                Console.WriteLine($"Deleted document with ID: {id}");
                return true;
            }

            Console.WriteLine($"Failed to delete document with ID {id}. Status: {response.Result}");
            return false;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error deleting document: {ex.Message}");
            throw;
        }
    }
}