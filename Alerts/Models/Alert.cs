


using System.Text.Json.Serialization;

public class AlertRequest
{
    public string Receiver { get; set; }
    public string Status { get; set; }
    public List<Alert> Alerts { get; set; }


}

public class Alert
{
    public string Status { get; set; }
    public Dictionary<string, string> Labels { get; set; }
    public Dictionary<string, string> Annotations { get; set; }
    public string StartsAt { get; set; }
    public string EndsAt { get; set; }
    public string GeneratorURL { get; set; }
    public string Fingerprint { get; set; }

    //public DateTime Timestamp { get; set; } = DateTime.UtcNow;

   //public string TraceId { get; set; }
}

