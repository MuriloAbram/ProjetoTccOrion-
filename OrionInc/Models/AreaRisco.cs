namespace OrionInc.Models
{
    public class AreaRisco
    {
        public int IdArea { get; set; }
        public string NomeArea { get; set; } = string.Empty;
        public string? DescricaoArea { get; set; }
        public string NivelRisco { get; set; } = "baixo";
        public int RaioMonitoramento { get; set; }
        public bool AreaAtiva { get; set; } = true;
    }
}
