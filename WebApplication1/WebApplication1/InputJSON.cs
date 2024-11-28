namespace WebApplication1;
using System.Collections.Generic;

public class InputJson{
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