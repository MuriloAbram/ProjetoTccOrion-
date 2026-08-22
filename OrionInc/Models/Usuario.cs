namespace OrionInc.Models
{
    public class Usuario
    {
        public int IdUsuario { get; set; }
        public string NomeUsuario { get; set; } = string.Empty;
        public string EmailUsuario { get; set; } = string.Empty;
        public string NivelAcesso { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
        public bool UsuarioAtivo { get; set; } = true;
    }
}
