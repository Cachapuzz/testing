using Elastic.Clients.Elasticsearch;
using System.Text.Json;
using SortOrder = Elastic.Clients.Elasticsearch.SortOrder;

namespace WebApplication1;

public static class Handlers {
    
    // Function that creates a new index in ElasticSearch 
    private static async Task CreateIndex(ElasticsearchClient client, string index) {
        
        // Check if the index exists
        var indexResponse = await client.Indices.ExistsAsync(index);
        if (indexResponse.IsValidResponse) {
            Console.WriteLine($"Index \"{index}\" already exists.");
            return;
        }

        // Create the index if it doesn't exist
        try {
            var createIndexResponse = await client.Indices.CreateAsync(index, c => c
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
                Console.WriteLine($"Index \"{index}\" created successfully!");
            }
            else Console.WriteLine($"Error when trying to create \"{index}\"");
        }
        catch (Exception ex) {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    // Receives incomplete string and tries removes problematic fields, returns json in string type
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

    // Parses through a valid json string and returns the requested value in string type
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

    // Deserializes a json string into an InputJson or OutputJson. Check "TransactionClasses.cs" for classes information
    private static T GetDocumentFromJson<T>(string jsonString) where T : class {
        
        if (string.IsNullOrWhiteSpace(jsonString)) throw new ArgumentException("Input JSON string cannot be null or empty.");
        
        try {
            // Deserialize the JSON string into a Document object
            T? document = System.Text.Json.JsonSerializer.Deserialize<T>(jsonString, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true // Handle case-insensitivity for JSON keys
            });

            if (document == null) throw new InvalidOperationException("Failed to deserialize JSON to Document.");
            
            return document;
        }
        catch (JsonException ex) {
            Console.WriteLine($"Error parsing JSON: {ex.Message}");
            throw;
        }
    }

    // Attaches input and output JSONs along additional information into a single ExpeditionTransaction and indexes it to elastic.
    // Check "TransactionClasses.cs" for "ExpeditionTransaction" information
    private static async Task SendToElastic(string json, InputJson inputDocument, OutputJson outputDocument,
        ElasticsearchClient client, string newindex, bool isItNew) {
        
        // Values taken from original json. If this is changed, ExpeditionTransaction class has to change too
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

        //Parse non-string values into their respective types
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

        // Parse as DateTime. If at any point it is changed to a string, you might lose milisecond information depending on DateTime type.
        if (!DateTime.TryParse(timestamp, out var parsedDateTime)) {
            Console.WriteLine("Error parsing");
        }

        // If this document was captured after the application started, send transaction info to Prometheus
        if (isItNew) {
            var transactionPusher = new PrometheusPusher();
            await transactionPusher.PushSingleMetric("transaction_duration", transactionDurationParsed, transactionId, transactionName);
            await transactionPusher.PushSingleMetric("transaction_span_started", parsedSpanStarted, transactionId, transactionName);
            await transactionPusher.PushSingleMetric("transaction_span_dropped", parsedSpanDropped, transactionId, transactionName);
        }

        // Create new ExpeditionTransaction
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

        //Index ExpeditionTransaction in elastic
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
    
    // Just a middle-point function to avoid repeated code, calls other functions that are explained above
    private static async Task TreatJsonAndIngest(ElasticsearchClient client, String json, String index, bool isItNew) {
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
        
        // Index jsons and more
        await SendToElastic(json, inputDocument, outputDocument, client, index, isItNew);
    }

    // Copies all documents from the source index that aren't already in the new index
    private static async Task PopulateIndexFrom(ElasticsearchClient client, string newindex, string sourceindex) {
        try {
            // Index ".ds-traces-apm-default-2024.11.14-000002" has documents that aren't from "ExpeditionService"
            //      but only documents from it have "labels.inputJSON" and "labels.outputJSON" so we specify the service name
            // We also specify that "labels.inputJSON" and "labels.outputJSON" must exist, just to be safe
            
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

            // Get a set of all the documents that the new index already has
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
                // Last argument is set to false because these documents weren't created at runtime (not used for Prometheus)
                await TreatJsonAndIngest(client, json, newindex, false);
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error parsing JSON or retrieving value: {ex.Message}");
        }
    }

    // Returns a DateTime value of the most recent document in an index
    private static async Task<DateTime?> GetLastDocumentTime(ElasticsearchClient client, string dateField, string index) {
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

    // Deletes all documents in specified index (Be careful using this!)
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

    // Deletes an index from elastic (I don't think this deletes the documents but they might get unorganized. Be careful using this!)
    private static async Task DeleteIndex(ElasticsearchClient client, string index) {
        var deleteResponse2 = await client.Indices.DeleteAsync(index);
        if (deleteResponse2.IsValidResponse) Console.WriteLine($"Successfully deleted index expedition_transactions.");
        else
            Console.WriteLine(
                $"Failed to delete index expedition_transactions: {deleteResponse2.ElasticsearchServerError?.Error.Reason}");
    }

    // Copies all documents from the source index that have a more recent timestamp than the newest document from the new index
    private static async Task GetNewerDocumentsFrom(ElasticsearchClient client, DateTime lastTimestamp, string newindex,
        string sourceindex) {
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

        // For each document
        foreach (var hit in searchResponse.Hits) {
            var json = hit.Source?.ToString();
            if (json == null) {
                continue;
            }

            // Last argument is set to true because these documents were created during runtime (used for Prometheus)
            TreatJsonAndIngest(client, json, newindex, true);
        }
    }

    // Starting point of the App
    public static async Task<object?> Start(ElasticsearchClient client) {
        
        //Delete all documents in specified index
        //await DeleteAllDocuments(client, "expedition_transactions");
        
        //Delete index from elastic
        //await DeleteIndex(client, "expedition_transactions");
        
        // Create a new index if it doesn't exist
        await CreateIndex(client, "expedition_transactions");
        
        //Copy documents from the index in the second argument from the index in the third argument
        await PopulateIndexFrom(client, "expedition_transactions", ".ds-traces-apm-default-2024.11.14-000002");
        
        // Run a loop to periodically calculate system metrics
        _ = Task.Run(async () => {
            while (true) {
                await SystemMetrics(client);
                await Task.Delay(60000);
            }
        });
        
        // Run a loop to periodically check for new documents and send new info to Prometheus
        while (true) {
            await Task.Delay(10000);
            var lastTimestamp = GetLastDocumentTime(client, "_timestamp", "expedition_transactions").Result;
            if (lastTimestamp == null) continue;
            await GetNewerDocumentsFrom(client, lastTimestamp.Value, "expedition_transactions",
                ".ds-traces-apm-default-2024.11.14-000002");
        }

        return 0;
    }

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