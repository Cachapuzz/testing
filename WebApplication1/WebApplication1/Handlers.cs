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


    private static async Task<object?> GetValueFromJsonPath(string field, TimeSpan timeWindow,
        ElasticsearchClient client) {
        try {
            // Get the start time for the time window
            var startTime = DateTime.UtcNow - timeWindow;

            //Get most recent document that has specified field property
            var responseAvg = await client.SearchAsync<object>(s => s
                .Index("metrics-*")
                .SearchType(SearchType.QueryThenFetch)
                .From(0)
                .Query(q => q
                    .Bool(b => b
                        .Must(mu => mu
                            .Exists(e => e
                                .Field(field)
                            )
                        )
                    )
                    .Range(range => range
                        .DateRange(dr => dr
                            .Field("@timestamp")
                            .Gte(startTime)
                        ))
                )
                .Size(1000)
                .Sort(ss => ss.Field("@timestamp", s => s.Order(SortOrder.Desc)))
            );

            var response = await client.SearchAsync<object>(s => s
                .Index("metrics-*")
                .SearchType(SearchType.QueryThenFetch)
                .From(0)
                .Query(q => q
                    .Bool(b => b
                        .Must(mu => mu
                            .Exists(e => e
                                .Field(field)
                            )
                        )
                    )
                )
                .Size(1)
                .Sort(ss => ss.Field("@timestamp", s => s.Order(SortOrder.Desc)))
            );

            // Null checks
            if (!response.IsValidResponse || !response.Hits.Any()) {
                Console.WriteLine($"No valid hits found or Source is null for field: {field}");
                return null;
            }

            var total = 0.0;
            var count = 0;

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

                // If the value is found and it is a number, add it to the sum
                if (currentElement.ValueKind == JsonValueKind.Number) {
                    total += currentElement.GetDouble();
                    count++;
                }
            }

            // Return the average if there were any valid documents
            if (count > 0) {
                return total / count;
            }

            Console.WriteLine($"No valid numerical values found for field: {field}");
            return null;
            

        /*// RESPONSE BEFORE

        //Null checks
        var hit = response.Hits.First();
        if (hit?.Source == null) {
            Console.WriteLine($"No hits found or Source is null for field: {field}");
            return null;
        }

        var json = hit.Source.ToString();
        if (json == null) {
            Console.WriteLine($"Source is empty: {field}");
            return null;
        }

        var jsonDocument = JsonDocument.Parse(json);
        var elements = field.Split('.');
        JsonElement currentElement = jsonDocument.RootElement;

        //Parse Json for asked field
        foreach (var element in elements) {
            if (currentElement.TryGetProperty(element, out var nextElement)) {
                currentElement = nextElement;
            }
            else {
                // If any part of the path doesn't exist, return null
                return null;
            }
        }

        // Return the value as the appropriate type
        return currentElement.ValueKind switch {
            JsonValueKind.String => currentElement.GetString(),
            JsonValueKind.Number => currentElement.GetDouble(), // Adjust to GetInt32, GetDecimal, etc., if needed
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => currentElement.ToString(), // Handle other kinds as string
        };*/
        
        
        }
        catch (Exception ex) {
            Console.WriteLine($"Error parsing JSON or retrieving value: {ex.Message}");
            return null;
        }
    }

public static async Task<object?> HostMetrics(ElasticsearchClient client) {
    var timeSpan = TimeSpan.FromHours(1);
    
    // CPU Usage (%) 
    var cpuUsage = GetValueFromJsonPath("system.cpu.total.norm.pct", timeSpan, client).Result;
    Console.WriteLine($"CPU Total Normal Percentage: {cpuUsage}");

    // Normalized Load
    var load1 = await GetValueFromJsonPath("system.load.1", timeSpan,client);
    var cores = await GetValueFromJsonPath("system.load.cores", timeSpan, client);
    if (load1 is double loadValue && cores is double coreValue && coreValue != 0) {
        var normalizedLoad = loadValue / coreValue;
        Console.WriteLine($"Normalized Load: {normalizedLoad}");
    }
    else {
        Console.WriteLine("Normalized Load Error: Values are not valid doubles or cores is zero.");
    }

    // Memory Cache
    var usedBytes = await GetValueFromJsonPath("system.memory.used.bytes", timeSpan, client);
    var actualBytes = await GetValueFromJsonPath("system.memory.actual.used.bytes", timeSpan, client);
    if (usedBytes is double uB && actualBytes is double aB) {
        var memoryCache = uB - aB;
        Console.WriteLine($"Memory Cache: {memoryCache}");
    }
    else {
        Console.WriteLine("Memory Cache Error: Values are not valid doubles.");
    }

    // Memory Free

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