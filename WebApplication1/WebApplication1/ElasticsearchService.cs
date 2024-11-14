using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace WebApplication1;

public class ElasticsearchService {
    public ElasticsearchClient Client { get; }

    private readonly IConfiguration _configuration;

    private string? GetElasticUrl() {
        return _configuration["ElasticSettings:Url"];
    }

    private string? GetApiKey() {
        return _configuration["ElasticSettings:ApiKey"];
    }

    private string? GetIndexName() {
        return _configuration["ElasticSettings:IndexName"];
    }

    public ElasticsearchService(IConfiguration configuration) {
        _configuration = configuration;
        var settings = new ElasticsearchClientSettings(new Uri(GetElasticUrl() ?? string.Empty))
            .ServerCertificateValidationCallback(CertificateValidations.AllowAll)
            .Authentication(new ApiKey(GetApiKey() ?? string.Empty))
            .EnableTcpKeepAlive(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))
            .RequestTimeout(TimeSpan.FromSeconds(60))
            .EnableDebugMode()
            .MaximumRetries(5)
            .DefaultIndex(GetIndexName() ?? string.Empty);

        Client = new ElasticsearchClient(settings);
    }
}