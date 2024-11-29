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

public class InputJson {
    public string? ID { get; set; }
    public int? IDEvent { get; set; }
    public double? ModoPesDuplaFaseP1 { get; set; }
    public double? ModoPesDuplaFaseP2 { get; set; }
    public bool? ModoPesDuplaFase { get; set; }
    public string? MatriculaLPR { get; set; }
    public string? MatriculaLPRConfience { get; set; }
    public string? ReboqueLPR { get; set; }
    public string? ReboqueLPRConfience { get; set; }
    public string? Cartao { get; set; }
    public string? CartaoRaw { get; set; }
    public double? Peso { get; set; }
    public string? Balanca { get; set; }
    public double? PesoObtidoAutomatico { get; set; }
    public double? Pesagem_Condicoes { get; set; }
    public List<string>? LogsPendentes { get; set; }
    public bool? CartaoRecolhido { get; set; }
    public List<string>? Selos { get; set; }
    public bool? SemCriacaoDoc { get; set; }
    public int? TipoContacto { get; set; }
    public bool? CriaCartao { get; set; }
    public int? TipoPeso { get; set; }
    public bool? Granel { get; set; }
    public int? LeitorIndex { get; set; }
    public List<string>? CamposExtra { get; set; }
}

public class OutputJson {
    public string? ID { get; set; }
    public int? IDEvent { get; set; }
    public bool? ModoPesDuplaFase { get; set; }
    public string? IDDoc { get; set; }
    public string? Filial { get; set; }
    public string? Departamento { get; set; }
    public string? NumSeq { get; set; }
    public string? CartaoFisico { get; set; }
    public string? CartaoLogico { get; set; }
    public string? PostoLogico { get; set; }
    public string? Matricula { get; set; }
    public string? Produto { get; set; }
    public string? ProdutoDesc { get; set; }
    public int? ProdutoInterno { get; set; }
    public string? TipoOperacao { get; set; }
    public double? QtdPedida { get; set; }
    public double? Tara { get; set; }
    public double? Bruto { get; set; }
    public int? Resposta { get; set; }
    public string? CampoAux1 { get; set; }
    public string? CampoAux2 { get; set; }
    public string? CampoAux3 { get; set; }
    public string? CampoAux4 { get; set; }
}