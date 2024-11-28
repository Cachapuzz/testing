using Elastic.Apm;
using Microsoft.AspNetCore.Mvc;
using WebApplication1;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
builder.Services.AddElasticApm();
builder.Services.AddSingleton<ElasticsearchService>();



var app = builder.Build();

ElasticsearchService? elasticsearchService = app.Services.GetService<ElasticsearchService>();

if (elasticsearchService == null) {
    Console.WriteLine("No Elasticsearch service found");
    return;
}
await Handlers.HostMetrics(elasticsearchService.Client);

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment()) {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }
//
// app.UseHttpsRedirection();

// app.MapGet("/fetch-metrics",
//         (ElasticsearchService elasticsearchService) => {
//             return StartTransaction("Fetch Metrics", "Metrics",
//                 () => Handlers.HostMetrics(elasticsearchService.Client));
//         })
//     .WithName("Fetch Metrics")
//     .WithOpenApi();

//app.Run();

/*T? StartTransaction<T>(string name, string type, Func<T?> callback) {
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
}*/