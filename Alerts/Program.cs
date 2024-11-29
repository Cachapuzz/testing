
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using Elastic.Apm.AspNetCore;
using Elastic.Apm.NetCoreAll;
using Elasticsearch.Net;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
//using Elastic.Transport;
//using ApiKey = Elastic.Transport.ApiKey;
using Elastic.Clients.Elasticsearch;
using Elastic.Apm.Extensions.Hosting;
using Elastic.Apm.Api;


var builder = WebApplication.CreateBuilder(args);




var logger = new LoggerConfiguration()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("https://192.168.2.16:9200"))
    {
        AutoRegisterTemplate = true,
        IndexFormat = "logs-{0:yyyy.MM}",
         ModifyConnectionSettings = conn => 
         conn.ServerCertificateValidationCallback((_, _, _, _) => true) // Ignora validação do certificado
            .BasicAuthentication("elastic", "password") 
    }).CreateLogger();

builder.Host.UseSerilog(logger);


// Registrar o IElasticClient com o endpoint do Elasticsearch
builder.Services.AddSingleton(sp =>
{
    var settings = new ElasticsearchClientSettings(new Uri("https://192.168.2.16:9200"))
        .DefaultIndex("alerts_grafana")
        .ServerCertificateValidationCallback(Elastic.Transport.CertificateValidations.AllowAll) // Ignora verificação de certificado
        .Authentication(new Elastic.Transport.ApiKey("NHhnVEJwTUI4VVZYSC1WTi1VVEU6YklYdFU5M05SSHFQODZxOVpMX2NqZw=="))
        .RequestTimeout(TimeSpan.FromSeconds(60)) // Timeout de requisição
        .EnableDebugMode() // Habilita o modo de depuração
        .MaximumRetries(5) // Número máximo de tentativas
        .SniffOnConnectionFault(false) // Desabilita a detecção automática de falhas de conexão
        .DisablePing() // Desabilita o ping para checar o Elasticsearch
        .DisableDirectStreaming(); // Desabilita o streaming direto

    return new ElasticsearchClient(settings);
});
//builder.Services.AddElasticApm(builder.Configuration.GetSection("ElasticApm"));
builder.Services.AddElasticApm();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();




app.UseCors("AllowCorsPolicy");
app.UseCors("AllowCorsPolicy");

//Allow CORS
app.UseCors(x => x.AllowAnyHeader()
      .AllowAnyMethod()
      .WithOrigins("*"));
// Configurar Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();

