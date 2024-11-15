using Elastic.Clients.Elasticsearch;

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
    
    public static List<DocumentModel> GetDocumentsByField(string fieldName, string fieldValue, ElasticsearchClient client) {
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