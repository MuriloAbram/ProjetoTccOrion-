namespace OrionInc.Models
{
    public class Funcionario
    {
        public int IdFuncionario { get; set; }
        public string NomeFuncionario { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public string Setor { get; set; } = string.Empty;
        public string StatusFuncionario { get; set; } = "ativo";
        public DateTime DataCadastro { get; set; }
    }
}
