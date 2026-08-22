namespace OrionInc.Data
{
    /// <summary>Guarda os dados do usuário autenticado durante a execução do programa.</summary>
    public static class Sessao
    {
        public static int IdUsuario { get; set; }
        public static string NomeUsuario { get; set; } = string.Empty;
        public static string EmailUsuario { get; set; } = string.Empty;
        public static string NivelAcesso { get; set; } = string.Empty;

        public static bool PodeCriarAlerta { get; set; }
        public static bool PodeAprovarManutencao { get; set; }
        public static bool PodeEditarUsuarios { get; set; }

        public static void Limpar()
        {
            IdUsuario = 0;
            NomeUsuario = string.Empty;
            EmailUsuario = string.Empty;
            NivelAcesso = string.Empty;
            PodeCriarAlerta = false;
            PodeAprovarManutencao = false;
            PodeEditarUsuarios = false;
        }
    }
}
