using WebApplication1; // Adjust namespace as needed

class Program {
    static async Task Main(string[] args) {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) => { services.AddSingleton<ElasticsearchService>(); })
            .Build();

        var elasticsearchService = host.Services.GetService<ElasticsearchService>();
        if (elasticsearchService == null) {
            Console.WriteLine("No Elasticsearch service found");
            return;
        }

        await Handlers.Start(elasticsearchService.Client);

        Console.WriteLine("Operation completed successfully.");
    }
}