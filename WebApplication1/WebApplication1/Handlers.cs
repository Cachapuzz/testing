using Elastic.Clients.Elasticsearch;
using System.Text.Json;
using SortOrder = Elastic.Clients.Elasticsearch.SortOrder;

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

    private static async Task CreateIndex(ElasticsearchClient client) {
        // Check if the index exists
        var indexResponse = await client.Indices.ExistsAsync("expedition_transactions");

        if (indexResponse.IsValidResponse) {
            Console.WriteLine($"Index \"expedition_transactions\" already exists.");
            return;
        }

        // Create the index if it doesn't exist
        try {
            var createIndexResponse = await client.Indices.CreateAsync("expedition_transactions", c => c
                .Mappings(m => m
                    .Properties<object>(p => p
                        .Date("_timestamp")
                        .Object("event", o => o
                            .Properties(p1 => p1
                                .Date("ingestedAt")
                                .Keyword("outcome")
                            )
                        )
                        .Keyword("hostName")
                        .Object("span", o => o
                            .Properties(p1 => p1
                                .Keyword("ID")
                                .IntegerNumber("started")
                                .IntegerNumber("dropped")
                            )
                        )
                        .Object("transaction", o => o
                            .Properties(tp => tp
                                .Keyword("ID")
                                .Keyword("Name")
                                .IntegerNumber("duration")
                            )
                        )
                        .Object("request", o => o
                            .Properties(rp => rp
                                .Keyword("ID")
                                .IntegerNumber("IDEvent")
                                .IntegerNumber("ModoPesDuplaFaseP1")
                                .IntegerNumber("ModoPesDuplaFaseP2")
                                .Boolean("ModoPesDuplaFase")
                                .Keyword("MatriculaLPR")
                                .Keyword("MatriculaLPRConfience")
                                .Keyword("ReboqueLPR")
                                .Keyword("ReboqueLPRConfience")
                                .Keyword("Cartao")
                                .Keyword("CartaoRaw")
                                .IntegerNumber("Peso")
                                .Keyword("Balanca")
                                .IntegerNumber("PesoObtidoAutomatico")
                                .IntegerNumber("Pesagem_Condicoes")
                                .IntegerNumber("LogsPendentes")
                                .Boolean("CartaoRecolhido")
                                .Keyword("Selos")
                                .Boolean("SemCriacaoDoc")
                                .IntegerNumber("TipoContacto")
                                .Boolean("CriaCartao")
                                .IntegerNumber("TipoPeso")
                                .Boolean("Granel")
                                .IntegerNumber("LeitorIndex")
                                .Keyword("CamposExtra")
                            )
                        )
                        .Object("response", o => o
                            .Properties(rp => rp
                                .Keyword("ID")
                                .IntegerNumber("IDEvent")
                                .Boolean("ModoPesDuplaFase")
                                .Keyword("IDDoc")
                                .Keyword("Filial")
                                .Keyword("Departamento")
                                .Keyword("NumSeq")
                                .Keyword("CartaoFisico")
                                .Keyword("CartaoLogico")
                                .Keyword("PostoLogico")
                                .Keyword("Matricula")
                                .Keyword("Produto")
                                .Keyword("ProdutoDesc")
                                .IntegerNumber("ProdutoInterno")
                                .Keyword("TipoOperacao")
                                .IntegerNumber("QtdPedida")
                                .IntegerNumber("Tara")
                                .IntegerNumber("Bruto")
                                .IntegerNumber("Resposta")
                                .Keyword("CampoAux1")
                                .Keyword("CampoAux2")
                                .Keyword("CampoAux3")
                                .Keyword("CampoAux4")
                            )
                        )
                    )
                )
            );
            if (createIndexResponse.IsValidResponse) {
                Console.WriteLine($"Index \"expedition_transactions\" created successfully!");
            }
            else Console.WriteLine($"Error when trying to create \"expedition_transactions\"");
        }
        catch (Exception ex) {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static string FixJson(string rawJson, string field) {
        string resultJson = rawJson;

        // In labels.outputJSON the problematic field is "MensagemFinal" and it is always present so we truncate the json when we find it
        if (field == "labels.outputJSON" && rawJson.Contains("MensagemFinal")) {
            int startIndex = rawJson.IndexOf(",\"MensagemFinal", StringComparison.Ordinal);
            resultJson = rawJson.Substring(0, startIndex);
            resultJson += "}";

            return resultJson;
        }


        // In labels.inputJSON the problematic field is "DadosIntroduzidos" but only when it has data
        // To safely remove it we truncate the whole string when the next field "PesoObtidoAutomatico" isn't found and remove only the "DadosIntroduzidos" field otherwise
        if (field == "labels.inputJSON" && rawJson.Contains("DadosIntroduzidos")) {
            if (rawJson.Contains("PesoObtidoAutomatico")) {
                int startIndex = rawJson.IndexOf("DadosIntroduzidos", StringComparison.Ordinal);
                int endIndex = rawJson.IndexOf("PesoObtidoAutomatico", startIndex, StringComparison.Ordinal);
                if (startIndex != -1 && endIndex != -1) {
                    resultJson = rawJson.Remove(startIndex, endIndex - startIndex);

                    return resultJson;
                }
            }
            else {
                int startIndex = rawJson.IndexOf(",\"DadosIntroduzidos", StringComparison.Ordinal);

                resultJson = rawJson.Substring(0, startIndex);
                resultJson += "}";

                return resultJson;
            }
        }

        return resultJson;
    }

    private static string SearchJsonField(string json, string field) {
        var jsonDocument = JsonDocument.Parse(json);
        var elements = field.Split('.');
        JsonElement currentElement = jsonDocument.RootElement;

        // Parse JSON for the requested field
        foreach (var element in elements) {
            if (currentElement.TryGetProperty(element, out var nextElement)) {
                currentElement = nextElement;
            }
            else {
                break;
            }
        }

        return currentElement.ToString();
    }

    public static T GetDocumentFromJson<T>(string jsonString) where T : class {
        if (string.IsNullOrWhiteSpace(jsonString)) {
            throw new ArgumentException("Input JSON string cannot be null or empty.");
        }

        try {
            // Deserialize the JSON string into a Document object
            T? document = System.Text.Json.JsonSerializer.Deserialize<T>(jsonString, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true // Handle case-insensitivity for JSON keys
            });

            if (document == null) {
                throw new InvalidOperationException("Failed to deserialize JSON to Document.");
            }

            return document;
        }
        catch (System.Text.Json.JsonException ex) {
            Console.WriteLine($"Error parsing JSON: {ex.Message}");
            throw;
        }
    }

    private static async Task SendToElastic(string json, InputJson inputDocument, OutputJson outputDocument,
        ElasticsearchClient client, string newindex) {
        var eventTime = SearchJsonField(json, "event.ingested");
        var hostName = SearchJsonField(json, "host.name");
        var spanId = SearchJsonField(json, "span.id");
        var eventOutcome = SearchJsonField(json, "event.outcome");
        var transactionId = SearchJsonField(json, "transaction.id");
        var transactionName = SearchJsonField(json, "transaction.name");
        var transactionDuration = SearchJsonField(json, "transaction.duration.us");
        var spanDropped = SearchJsonField(json, "transaction.span_count.dropped");
        var spanStarted = SearchJsonField(json, "transaction.span_count.started");
        var timestamp = SearchJsonField(json, "@timestamp");

        //Parse non-string values to their respective types
        DateTime parsedEventTime = DateTime.MinValue;
        if (DateTime.TryParse(eventTime, out var parsedDate)) parsedEventTime = parsedDate;
        else Console.WriteLine("Invalid date format for eventTime.");

        int? transactionDurationParsed = null;
        if (int.TryParse(transactionDuration, out var tdurationValue)) transactionDurationParsed = tdurationValue;
        else Console.WriteLine("Invalid number format for spanDropped.");

        int? parsedSpanStarted = null;
        if (int.TryParse(spanStarted, out var startedValue)) parsedSpanStarted = startedValue;
        else Console.WriteLine("Invalid number format for spanStarted.");

        int? parsedSpanDropped = null;
        if (int.TryParse(spanDropped, out var droppedValue)) parsedSpanDropped = droppedValue;
        else Console.WriteLine("Invalid number format for spanDropped.");

        Console.WriteLine($"DateTime before conversion: {timestamp}");

        // Parse as DateTime
        if (!DateTime.TryParse(timestamp, out var parsedDateTime)) {
            Console.WriteLine("Error parsing");
        }

        Console.WriteLine($"DateTime after conversion: {parsedDateTime}");

        var transaction = new ExpeditionTransaction {
            _timestamp = parsedDateTime,
            Event = new EventInfo {
                IngestedAt = parsedEventTime,
                Outcome = eventOutcome
            },
            HostName = hostName,
            Span = new Span {
                ID = spanId,
                Started = parsedSpanStarted,
                Dropped = parsedSpanDropped
            },
            Transaction = new Transaction {
                ID = transactionId,
                Name = transactionName,
                Duration = transactionDurationParsed
            },
            Request = inputDocument,
            Response = outputDocument
        };

        try {
            var responseInsert = await client.IndexAsync<ExpeditionTransaction>(transaction, idx => idx
                .Index(newindex));

            if (responseInsert.IsValidResponse)
                Console.WriteLine($"Document indexed successfully with ID: {responseInsert.Id}");
            else
                Console.WriteLine($"Failed to index document: {responseInsert.ElasticsearchServerError?.Error.Reason}");
        }
        catch (Exception ex) {
            Console.WriteLine($"Exception while indexing document: {ex.Message}");
        }
    }

    private static async Task TreatJsonAndIngest(ElasticsearchClient client, String json, String index) {
        // Get the input/output JSONs
        var inputJson = SearchJsonField(json, "labels.inputJSON");
        var outputJson = SearchJsonField(json, "labels.outputJSON");

        // Fix the JSONs
        var fixedInputJson = FixJson(inputJson, "labels.inputJSON");
        var fixedOutputJson = FixJson(outputJson, "labels.outputJSON");

        // Parse and process fixed JSON if it's valid
        InputJson inputDocument = GetDocumentFromJson<InputJson>(fixedInputJson);
        OutputJson outputDocument = GetDocumentFromJson<OutputJson>(fixedOutputJson);

        //Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(inputDocument));
        //Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(outputDocument));
        await SendToElastic(json, inputDocument, outputDocument, client, index);
    }

    private static async Task PopulateIndexFrom(ElasticsearchClient client, string newindex, string sourceindex) {
        try {
            var response = await client.SearchAsync<dynamic>(s => s
                .Index(sourceindex)
                .SearchType(SearchType.QueryThenFetch)
                .From(0)
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                                .Match(mm => mm
                                    .Field("service.name")
                                    .Query("ExpeditionService")
                                ),
                            m => m.Exists(e => e.Field("labels.inputJSON")),
                            m => m.Exists(e => e.Field("labels.outputJSON"))
                        )
                    )
                )
                .Size(10000)
                .Sort(ss => ss.Field("@timestamp", s => s.Order(SortOrder.Desc)))
            );

            // Source Index Null check
            if (!response.IsValidResponse || !response.Hits.Any()) {
                Console.WriteLine($"No valid hits found for {sourceindex}!");
                return;
            }

            var existingIdsResponse = await client.SearchAsync<dynamic>(s => s
                .Index(newindex)
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .Exists(e => e.Field("transaction.id")))))
                .Size(10000)
            );

            // New Index Null check
            if (!existingIdsResponse.IsValidResponse || !existingIdsResponse.Hits.Any()) {
                Console.WriteLine($"No valid hits found for {newindex}!");
                return;
            }

            var existingIds = existingIdsResponse.Hits
                .Select(hit => hit.Source?.GetProperty("transaction").GetProperty("id").ToString())
                .ToHashSet();

            // For each document received
            foreach (var hit in response.Hits) {
                var json = hit.Source?.ToString();
                if (json == null) {
                    continue;
                }

                // Check if transaction is already in index
                var transactionId = SearchJsonField(json, "transaction.id");
                if (existingIds.Contains(transactionId)) {
                    //Console.WriteLine($"Found transaction with id: {transactionId}. Skipping...");
                    continue;
                }

                ///// JSON /////
                await TreatJsonAndIngest(client, json, newindex);
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error parsing JSON or retrieving value: {ex.Message}");
        }
    }

    private static async Task<DateTime?>
        GetLastDocumentTime(ElasticsearchClient client, string dateField, string index) {
        var lastIndexedDocResponse = await client.SearchAsync<ExpeditionTransaction>(s => s
            .Index(index)
            .Sort(ss => ss.Field(dateField, s => s.Order(SortOrder.Desc)))
            .Size(1)
        );

        if (!lastIndexedDocResponse.IsValidResponse) {
            Console.WriteLine(
                $"Failed to fetch last document: {lastIndexedDocResponse.ElasticsearchServerError?.Error.Reason}");
            return null;
        }

        if (lastIndexedDocResponse?.Hits is null || !lastIndexedDocResponse.Hits.Any()) {
            Console.WriteLine("No documents found in the search.");
            return null;
        }

        var lastTimestamp = lastIndexedDocResponse.Hits.FirstOrDefault()?.Source?._timestamp;

        return lastTimestamp;
    }

    private static async Task DeleteAllDocuments(ElasticsearchClient client, string index) {
        var deleteResponse1 = await client.DeleteByQueryAsync<object>(index, d => d
            .Query(q => q
                .MatchAll(m => m.QueryName("match_all_documents"))
            )
        );
        if (deleteResponse1.IsValidResponse)
            Console.WriteLine($"Successfully deleted all documents from expedition_transactions.");
        else Console.WriteLine($"Failed to delete documents: {deleteResponse1.ElasticsearchServerError?.Error.Reason}");
    }

    private static async Task DeleteIndex(ElasticsearchClient client, string index) {
        var deleteResponse2 = await client.Indices.DeleteAsync(index);
        if (deleteResponse2.IsValidResponse) Console.WriteLine($"Successfully deleted index expedition_transactions.");
        else
            Console.WriteLine(
                $"Failed to delete index expedition_transactions: {deleteResponse2.ElasticsearchServerError?.Error.Reason}");
    }

    private static async Task GetNewerDocumentsFrom(ElasticsearchClient client, DateTime lastTimestamp, string newindex,
        string sourceindex) {
        var formattedLastTimestamp = lastTimestamp.ToUniversalTime().ToString("o");
        var searchResponse = await client.SearchAsync<dynamic>(s => s
            .Index(sourceindex)
            .Query(q => q
                .Bool(b => b
                    .Must(m => m
                            .Range(r => r
                                .DateRange(dr => dr
                                    .Field("@timestamp")
                                    .Gt(lastTimestamp)
                                )
                            ),
                        m => m
                            .Match(mm => mm
                                .Field("service.name")
                                .Query("ExpeditionService")
                            ),
                        m => m.Exists(e => e.Field("labels.inputJSON")),
                        m => m.Exists(e => e.Field("labels.outputJSON"))
                    )
                )
            )
            .Size(10000)
        );

        if (!searchResponse.IsValidResponse) {
            Console.WriteLine(
                $"Failed to fetch new documents: {searchResponse.ElasticsearchServerError?.Error.Reason}");
            return;
        }

        if (!searchResponse.Hits.Any()) {
            Console.WriteLine($"No new documents found");
            return;
        }


        foreach (var hit in searchResponse.Hits) {
            var json = hit.Source?.ToString();
            if (json == null) {
                continue;
            }

            TreatJsonAndIngest(client, json, newindex);
        }
    }

    public static async Task<object?> HostMetrics(ElasticsearchClient client) {
        //await DeleteAllDocuments(client, "expedition_transactions");
        //await DeleteIndex(client, "expedition_transactions");

        await CreateIndex(client);
        await PopulateIndexFrom(client, "expedition_transactions", ".ds-traces-apm-default-2024.11.14-000002");

        while (true) {
            await Task.Delay(5000);
            var lastTimestamp = GetLastDocumentTime(client, "_timestamp", "expedition_transactions").Result;
            if (lastTimestamp == null) continue;
            await GetNewerDocumentsFrom(client, lastTimestamp.Value, "expedition_transactions",
                ".ds-traces-apm-default-2024.11.14-000002");
        }

        return 0;
    }

    /*private static async Task<object?> GetValueFromJsonPath(string field, int size, TimeSpan timeWindow, string mode,
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
                .Size(size)
                .Sort(ss => ss.Field("@timestamp", s => s.Order(SortOrder.Desc)))
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
    }*/

    /*public static async Task<object?> HostMetrics(ElasticsearchClient client) {

        var timeSpan = TimeSpan.FromHours(1);
        var size = 100;

        var metricDocument = new MetricDocument();

        ////////// CPU USAGE (%) //////////
        var cpuUsage = await GetValueFromJsonPath("system.cpu.total.norm.pct", size, timeSpan, "average", client);
        if (cpuUsage is double cU) {
            Console.WriteLine($"CPU Total Normal Percentage: {Math.Round(cU, 2)}");
            metricDocument.CpuUsage = cU;
        }


        ////////// NORMALIZED LOAD //////////
        var load1 = await GetValueFromJsonPath("system.load.1", size, timeSpan, "average",client);
        var cores = await GetValueFromJsonPath("system.load.cores", size, timeSpan,"max", client);
        if (load1 is double loadValue && cores is double coreValue && coreValue != 0) {
            var normalizedLoad = loadValue / coreValue;
            Console.WriteLine($"Normalized Load: {Math.Round(normalizedLoad, 2)}");
            metricDocument.NormalizedLoad = normalizedLoad;
        }
        else Console.WriteLine("Normalized Load Error: Values are not valid doubles or cores is zero.");

        ////////// MEMORY CACHE //////////
        var usedBytes = await GetValueFromJsonPath("system.memory.used.bytes", size, timeSpan, "average", client);
        var actualBytes = await GetValueFromJsonPath("system.memory.actual.used.bytes", size, timeSpan, "average", client);
        if (usedBytes is double uB && actualBytes is double aB) {
            var memoryCache = uB - aB;
            Console.WriteLine($"Memory Cache: {Math.Round(memoryCache, 2)}");
            metricDocument.MemoryCache = memoryCache;
        }
        else Console.WriteLine("Memory Cache Error: Values are not valid doubles.");

        ////////// MEMORY FREE //////////
        var memoryTotal = await GetValueFromJsonPath("system.memory.total", size, timeSpan, "max", client);
        var memoryActual = await GetValueFromJsonPath("system.memory.actual.used.bytes", size, timeSpan, "average", client);
        if (memoryTotal is double mT && memoryActual is double mA) {
            var memoryFree = mT - mA;
            Console.WriteLine($"Memory Free: {Math.Round(memoryFree, 2)}");
            metricDocument.MemoryFree = memoryFree;
        }
        else Console.WriteLine("Memory Free Error: Values are not valid doubles.");

        ////////// MEMORY TOTAL //////////
        var sysMemoryTotal = await GetValueFromJsonPath("system.memory.total", size, timeSpan, "average", client);
        if (sysMemoryTotal is double sysTotal) {
            Console.WriteLine($"Memory Total: {Math.Round(sysTotal, 2)}");
            metricDocument.MemoryTotal = sysTotal;
        }

        ////////// MEMORY USAGE (%) //////////
        var memoryUsage = await GetValueFromJsonPath("system.memory.actual.used.pct", size, timeSpan, "average", client);
        if (memoryUsage is double mU) {
            Console.WriteLine($"Memory Usage (%): {Math.Round(mU, 2)}");
            metricDocument.MemoryUsage = mU;
        }

        ////////// MEMORY USED //////////
        var memoryUsed = await GetValueFromJsonPath("system.memory.actual.used.bytes", size, timeSpan, "average", client);
        if (memoryUsed is double mUsed) {
            Console.WriteLine($"Memory Used: {Math.Round(mUsed, 2)}");
            metricDocument.MemoryUsed = mUsed;
        }

        ////////// NETWORK INBOUND (RX) //////////
        var netIn = await GetValueFromJsonPath("host.network.ingress.bytes", size, timeSpan, "sum", client);
        if(netIn is double nI){
            var networkInbound = nI * 8 / 100;
            Console.WriteLine($"Network Inbound (RX): {Math.Round(networkInbound, 2)}");
            metricDocument.NetworkInbound = networkInbound;
        }

        ////////// NETWORK OUTBOUND (TX) //////////
        var netOut = await GetValueFromJsonPath("host.network.egress.bytes", size, timeSpan, "sum", client);
        if(netOut is double nO){
            var networkOutbound = nO * 8 / 100;
            Console.WriteLine($"Network Outbound (TX): {Math.Round(networkOutbound, 2)}");
            metricDocument.NetworkOutbound = networkOutbound;
        }

        ////////// DISK LATENCY //////////
        // Average(Read + Write Time)
        var diskTimeAvg = 0.0;
        var diskRead = await GetValueFromJsonPath("system.diskio.read.time", size, timeSpan, "sum&count", client);
        var diskWrite = await GetValueFromJsonPath("system.diskio.write.time", size, timeSpan, "sum&count", client);
        if (diskRead is ValueTuple<double, int> dR && diskWrite is ValueTuple<double, int> dW) {
            diskTimeAvg = (dR.Item1 + dW.Item1) / (dR.Item2 + dW.Item2);
        }

        // Read Count (in specified time span)
        var readCount = await GetValueFromJsonPath("system.diskio.read.count", size, timeSpan, "sum", client);

        // Write Count (in specified time span)
        var writeCount = await GetValueFromJsonPath("system.diskio.write.count", size, timeSpan, "sum", client);

        if (readCount is double rC && writeCount is double wC) {
             var diskLatency = diskTimeAvg / (rC + wC);
             Console.WriteLine($"DISK LATENCY: {Math.Round(diskLatency, 4)}");
             metricDocument.DiskLatency = diskLatency;
        }

        //Console.WriteLine($"diskTimeAvg: {Math.Round(diskTimeAvg, 2)}");
        //Console.WriteLine($"readCount: {readCount}");
        //Console.WriteLine($"writeCount: {writeCount}");

        ////////// DISK READ THROUGHPUT ////////// (no idea how to get this yet)

        ////////// DISK USAGE - AVAILABLE (%) //////////
        var diskUsage = GetValueFromJsonPath("system.filesystem.used.pct", size, timeSpan, "average", client).Result;
        if (diskUsage is double dU) {
            var diskAvailable = 1 - dU;
            Console.WriteLine($"DISK USAGE - AVAILABLE (%): {Math.Round(diskAvailable, 2)}");
            metricDocument.DiskUsageAvailable = diskAvailable;
        }

        ////////// DISK USAGE - MAX (%) //////////
        var maxDisk = await GetValueFromJsonPath("system.filesystem.used.pct", size, timeSpan, "max", client);
        if (maxDisk is double mD) {
            Console.WriteLine($"DISK USAGE - MAX (%): {Math.Round(mD, 2)}");
            metricDocument.DiskUsageMax = mD;
        }



        var pusher = new PrometheusPusher();
        await pusher.PushCpuMetricsAsync(metricDocument);


        return new { Error = "Metrics not found for the specified host." };
    }*/

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