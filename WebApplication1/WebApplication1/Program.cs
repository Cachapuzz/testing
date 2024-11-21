using Elastic.Apm;
using Microsoft.AspNetCore.Mvc;
using WebApplication1;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddElasticApm();
builder.Services.AddSingleton<ElasticsearchService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/hello", () => StartTransaction("Hello", "Hello Elastic APM", Handlers.HelloElastic))
    .WithName("Hello")
    .WithOpenApi();

app.MapPost("/documents",
        ([FromBody] DocumentRequest request, ElasticsearchService elasticsearchService) => {
            return StartTransaction("Documents", "New Document",
                () => (Handlers.CreateDocument(request.Name, request.Description,
                    elasticsearchService.Client)));
        })
    .WithName("Create Document")
    .WithOpenApi();

app.MapGet("/documents/search",
        (string field, string value, ElasticsearchService elasticsearchService) => {
            return StartTransaction("Search Documents By Field", "Search Documents",
                () => Handlers.GetDocumentsByField(field, value, elasticsearchService.Client));
        })
    .WithName("Search Documents By Field")
    .WithOpenApi();

app.MapDelete("/documents/{id}",
        (string id, ElasticsearchService elasticsearchService) => {
            return StartTransaction("Delete Document", "Remove Document",
                () => Handlers.DeleteDocumentById(id, elasticsearchService.Client));
        })
    .WithName("Delete Document")
    .WithOpenApi();

app.MapGet("/fetch-metrics",
        (ElasticsearchService elasticsearchService) => {
            return StartTransaction("Fetch Metrics", "Metrics",
                () => Handlers.HostMetrics(elasticsearchService.Client));
        })
    .WithName("Fetch Metrics")
    .WithOpenApi();

app.Run();
return;

T? StartTransaction<T>(string name, string type, Func<T?> callback) {
    var transaction = Agent.Tracer.StartTransaction(name, type);
    T? result = default;

    try {
        result = callback.Invoke();
    }
    catch (Exception e) {
        transaction.CaptureException(e);
    }
    finally {
        transaction.End();
    }

    return result;
}