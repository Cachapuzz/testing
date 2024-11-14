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
}