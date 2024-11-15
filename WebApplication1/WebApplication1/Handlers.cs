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
    
    public static DocumentModel? GetDocumentById(string id, ElasticsearchClient client) {
        try {
            var response = client.Get<DocumentModel>(id, g => g.Index("your-index-name"));

            if (response.IsValidResponse && response.Source != null) {
                Console.WriteLine($"Fetched document: {response.Source}");
                return response.Source;
            }

            Console.WriteLine($"Document with ID {id} not found.");
            return null;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error fetching document: {ex.Message}");
            throw;
        }
    }
}