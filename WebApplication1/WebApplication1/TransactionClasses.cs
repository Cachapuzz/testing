using Elastic.Clients.Elasticsearch;

namespace WebApplication1;

public class ExpeditionTransaction {
    public DateTime _timestamp { get; set; }
    public EventInfo? Event { get; set; }
    public string? HostName { get; set; }
    public Span? Span { get; set; }
    public Transaction? Transaction { get; set; }
    public InputJson? Request { get; set; }
    public OutputJson? Response { get; set; }
}

public class EventInfo {
    public DateTime IngestedAt { get; set; }
    public string? Outcome { get; set; }
}

public class Span {
    public string? ID { get; set; }
    public int? Started { get; set; }
    public int? Dropped { get; set; }
}

// Transaction class
public class Transaction {
    public string? ID { get; set; }
    public string? Name { get; set; }

    public int? Duration { get; set; }
}