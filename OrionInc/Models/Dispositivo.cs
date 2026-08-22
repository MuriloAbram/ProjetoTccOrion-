namespace OrionInc.Models
{
    public class Dispositivo
    {
        public int IdDispositivo { get; set; }
        public string CodigoDispositivo { get; set; } = string.Empty;
        public string TipoDispositivo { get; set; } = string.Empty;
        public string StatusDispositivo { get; set; } = "disponivel";
        public int? IdFuncionario { get; set; }
        public string? NomeFuncionario { get; set; } // apenas leitura, vindo do JOIN
    }
}
