namespace OrionInc.Models
{
    public class Vagao
    {
        public int IdVagao { get; set; }
        public string CodigoVagao { get; set; } = string.Empty;
        public string TipoVagao { get; set; } = string.Empty;
        public string StatusOperacao { get; set; } = "operando";
        public DateTime? DataUltimaManutencao { get; set; }
        public string? Observacoes { get; set; }
    }
}
