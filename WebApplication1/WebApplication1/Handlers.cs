using Elastic.Clients.Elasticsearch;
using System.Text.Json;
using SortOrder = Elastic.Clients.Elasticsearch.SortOrder;

namespace WebApplication1;

public static class Handlers {
    
    private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new JsonSerializerOptions {
        PropertyNameCaseInsensitive = true
    };
    
    
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
            T? document = JsonSerializer.Deserialize<T>(jsonString, CachedJsonSerializerOptions);

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
    private static async Task TreatJsonAndIngest(ElasticsearchClient client, String json, String index, bool isItNew, bool isJsonFixed) {
        // Get the input/output JSONs
        var inputJson = SearchJsonField(json, "labels.inputJSON");
        var outputJson = SearchJsonField(json, "labels.outputJSON");

        // Fix the JSONs if needed
        var fixedInputJson = isJsonFixed ? inputJson: FixJson(inputJson, "labels.inputJSON");
        var fixedOutputJson = isJsonFixed ? outputJson : FixJson(outputJson, "labels.outputJSON");
        
        // Parse and process fixed JSON if it's valid
        InputJson inputDocument = GetDocumentFromJson<InputJson>(fixedInputJson);
        OutputJson outputDocument = GetDocumentFromJson<OutputJson>(fixedOutputJson);

        //Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(inputDocument));
        //Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(outputDocument));
        
        // Index jsons and more
        await SendToElastic(json, inputDocument, outputDocument, client, index, isItNew);
    }

    // Copies all documents from the source index that aren't already in the new index
    private static async Task PopulateIndexFrom(ElasticsearchClient client, string newindex, string sourceindex, bool isJsonFixed) {
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
                                    .Field("service.name"!)
                                    .Query("ExpeditionService")
                                ),
                            m => m.Exists(e => e.Field("labels.inputJSON"!)),
                            m => m.Exists(e => e.Field("labels.outputJSON"!))
                        )
                    )
                )
                .Size(10000)
                .Sort(ss => ss.Field("@timestamp"!, sort => sort.Order(SortOrder.Desc)))
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
                            .Exists(e => e.Field("transaction.id"!)))))
                .Size(10000)
            );

            // New Index Null check
            if (!existingIdsResponse.IsValidResponse || !existingIdsResponse.Hits.Any()) {
                Console.WriteLine($"No valid hits found for {newindex}!");
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
                await TreatJsonAndIngest(client, json, newindex, false, isJsonFixed);
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
            .Sort(ss => ss.Field(dateField!, sort => sort.Order(SortOrder.Desc)))
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
        string sourceindex, bool isJsonFixed) {
        var searchResponse = await client.SearchAsync<dynamic>(s => s
            .Index(sourceindex)
            .Query(q => q
                .Bool(b => b
                    .Must(m => m
                            .Range(r => r
                                .DateRange(dr => dr
                                    .Field("@timestamp"!)
                                    .Gt(lastTimestamp)
                                )
                            ),
                        m => m
                            .Match(mm => mm
                                .Field("service.name"!)
                                .Query("ExpeditionService")
                            ),
                        m => m.Exists(e => e.Field("labels.inputJSON"!)),
                        m => m.Exists(e => e.Field("labels.outputJSON"!))
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
            TreatJsonAndIngest(client, json, newindex, true, isJsonFixed);
            
        }
        await Task.Delay(60000);
    }

    // Starting point of the App
    public static async Task Start(ElasticsearchClient client, CancellationToken cancellationToken) {

        // If this variable is set to "true", the program will skip JSON fix
        const bool isJsonFixed = false;
        
        //Delete all documents in specified index
        //await DeleteAllDocuments(client, "expedition_transactions");
        
        //Delete index from elastic
        //await DeleteIndex(client, "expedition_transactions");
        
        // Create a new index if it doesn't exist
        await CreateIndex(client, "expedition_transactions");
        
        //Copy documents from the index in the second argument from the index in the third argument
        await PopulateIndexFrom(client, "expedition_transactions", ".ds-traces-apm-default-2024.11.14-000002", isJsonFixed);
        
        // Run a loop to periodically calculate system metrics
        _ = Task.Run(async () => {
            while (!cancellationToken.IsCancellationRequested) {
                await ElasticMetrics.SystemMetrics(client);
                await Task.Delay(60000, cancellationToken);
            }
        }, cancellationToken);
        
        // Run a loop to periodically check for new documents and send new info to Prometheus
        while (!cancellationToken.IsCancellationRequested) {
            await Task.Delay(10000, cancellationToken);
            var lastTimestamp = GetLastDocumentTime(client, "_timestamp", "expedition_transactions").Result;
            if (lastTimestamp == null) continue;
            await GetNewerDocumentsFrom(client, lastTimestamp.Value, "expedition_transactions",
                ".ds-traces-apm-default-2024.11.14-000002", isJsonFixed);
        }

    }
}