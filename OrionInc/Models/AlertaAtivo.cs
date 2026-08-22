namespace OrionInc.Models
{
    /// <summary>Mapeia a view vw_alertas_ativos.</summary>
    public class AlertaAtivo
    {
        public int IdRegistro { get; set; }
        public DateTime DataHoraEvento { get; set; }
        public string TipoEvento { get; set; } = string.Empty;
        public string NivelAlerta { get; set; } = string.Empty;
        public string DescricaoEvento { get; set; } = string.Empty;
        public string? NomeFuncionario { get; set; }
        public string? Cargo { get; set; }
        public string? NomeArea { get; set; }
        public string? RiscoDaArea { get; set; }
        public string? CodigoVagao { get; set; }
        public string? TipoVagao { get; set; }
        public string? TipoSensor { get; set; }
        public string? ModeloSensor { get; set; }
        public string? StatusSensor { get; set; }
    }
}
