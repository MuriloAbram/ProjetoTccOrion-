using System.Text.Json;
using Npgsql;

namespace OrionInc.Data
{
    /// <summary>
    /// Responsável por montar a connection string a partir do appsettings.json
    /// e abrir conexões com o PostgreSQL (mesmo banco administrado pelo pgAdmin).
    /// </summary>
    public static class DatabaseHelper
    {
        private static string? _connectionString;

        public static string ConnectionString => _connectionString ??= BuildConnectionString();

        private static string BuildConnectionString()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Arquivo appsettings.json não encontrado ao lado do executável. " +
                    "Configure Host, Porta, Banco, Usuário e Senha do PostgreSQL.");
            }

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var cfg = doc.RootElement.GetProperty("ConnectionSettings");

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = cfg.GetProperty("Host").GetString(),
                Port = cfg.GetProperty("Port").GetInt32(),
                Database = cfg.GetProperty("Database").GetString(),
                Username = cfg.GetProperty("Username").GetString(),
                Password = cfg.GetProperty("Password").GetString(),
                Timeout = 15,
                CommandTimeout = 30
            };

            return builder.ConnectionString;
        }

        /// <summary>Abre e retorna uma conexão já aberta. Lembre-se de usar "using".</summary>
        public static NpgsqlConnection GetConnection()
        {
            var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        /// <summary>Testa a conexão com o banco, retornando (sucesso, mensagem).</summary>
        public static (bool ok, string mensagem) TestarConexao()
        {
            try
            {
                using var conn = GetConnection();
                return (true, $"Conectado com sucesso em {conn.Host}:{conn.Port}/{conn.Database}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static void RecarregarConfiguracao() => _connectionString = null;
    }
}
