using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using Elastic.Clients.Elasticsearch;
using System;
using System.Linq;
using System.Threading.Tasks;
using Elastic.Apm;

[ApiController]
[Route("Alert/")]
public class AlertController : ControllerBase
{
    private readonly ElasticsearchClient _elasticClient;

    public AlertController(ElasticsearchClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    [HttpPost("ReceiveAlert")]
    public async Task<IActionResult> ReceiveAlert([FromBody] AlertRequest alertRequest)
    {
        try
        {

            if (alertRequest == null)
        {
            return StatusCode(400, "Alert data is null");
        }

        if (alertRequest.Alerts == null || !alertRequest.Alerts.Any())
        {
            return StatusCode(400, "No alerts found in the request");
        }

        Console.WriteLine(JsonConvert.SerializeObject(alertRequest, Formatting.Indented));
            

            // Processar o alerta
            foreach (var alert in alertRequest.Alerts)
            {

                //if (alert.Timestamp == default)
                //{
                //    alert.Timestamp = DateTime.UtcNow;
                //}

                //var documentId = alert.Fingerprint ?? Guid.NewGuid().ToString();
                var customDocumentId = Guid.NewGuid().ToString();
                var customTransactionId = Guid.NewGuid().ToString();

                // Criar uma transação personalizada usando o Elastic APM
                var transaction = Agent.Tracer.StartTransaction(customDocumentId, "AlertTransaction");

                if (string.IsNullOrEmpty(alert.Fingerprint))
                {
                    alert.Fingerprint = transaction.TraceId;
                }

                //Console.WriteLine($"Using Fingerprint: {alert.Fingerprint}");
                //var transactionId = alert.Fingerprint;

                var enrichedAlert = new
                {
                    alert.Status,
                    alert.Labels,
                    alert.Annotations,
                    alert.StartsAt,
                    alert.EndsAt,
                    alert.GeneratorURL,
                    alert.Fingerprint,
                    //alert.Timestamp,
                    // transactionId=transaction.Id,
                    customTransactionId,
                    customDocumentId
                };
                // Console.WriteLine($"Indexing alert with Fingerprint: {alert.Fingerprint}");
                var response = await _elasticClient.IndexAsync(alertRequest, idx => idx.Index("alerts_grafana")
                //.Id(documentId)
                .Id(customDocumentId)
                );
                transaction.End();

                if (!response.IsValidResponse)
                {
                    var errorMessage = response.DebugInformation?? "Unknown error";
                    Console.WriteLine($"Error saving alert to Elasticsearch: {errorMessage}");
                    return StatusCode(500, $"Error saving alert to Elasticsearch: {errorMessage}");
                }

            }
            
             return Ok("Alert saved to Elasticsearch");
        }
         catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred while indexing alert: {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
  
    }


    
