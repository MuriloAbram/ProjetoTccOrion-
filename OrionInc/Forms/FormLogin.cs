using System.Data;
using System.Diagnostics;
using Npgsql;
using OrionInc.Data;
using OrionInc.Services;

namespace OrionInc.Forms
{
    public class FormLogin : Form
    {
        private readonly TextBox _txtEmail = new();
        private readonly TextBox _txtSenha = new();
        private readonly Button _btnEntrar = new();
        private readonly Button _btnTestarConexao = new();
        private readonly Label _lblStatus = new();

        public FormLogin()
        {
            Text = "ORION INC - Login";
            Width = 420;
            Height = 320;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblTitulo = new Label
            {
                Text = "ORION INC",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(130, 25)
            };

            var lblSub = new Label
            {
                Text = "Sistema de Monitoramento Ferroviário",
                AutoSize = true,
                Location = new Point(105, 60)
            };

            var lblEmail = new Label
            {
                Text = "E-mail:",
                Location = new Point(40, 105),
                AutoSize = true
            };

            _txtEmail.Location = new Point(40, 125);
            _txtEmail.Width = 320;

            var lblSenha = new Label
            {
                Text = "Senha:",
                Location = new Point(40, 155),
                AutoSize = true
            };

            _txtSenha.Location = new Point(40, 175);
            _txtSenha.Width = 320;
            _txtSenha.PasswordChar = '●';

            _btnEntrar.Text = "Entrar";
            _btnEntrar.Location = new Point(40, 215);
            _btnEntrar.Width = 150;
            _btnEntrar.Height = 32;
            _btnEntrar.Click += BtnEntrar_Click;

            _btnTestarConexao.Text = "Testar conexão";
            _btnTestarConexao.Location = new Point(210, 215);
            _btnTestarConexao.Width = 150;
            _btnTestarConexao.Height = 32;
            _btnTestarConexao.Click += BtnTestarConexao_Click;

            _lblStatus.Location = new Point(40, 255);
            _lblStatus.Width = 340;
            _lblStatus.Height = 40;
            _lblStatus.ForeColor = Color.DarkRed;

            AcceptButton = _btnEntrar;

            Controls.AddRange(new Control[]
            {
                lblTitulo,
                lblSub,
                lblEmail,
                _txtEmail,
                lblSenha,
                _txtSenha,
                _btnEntrar,
                _btnTestarConexao,
                _lblStatus
            });
        }

        private async void BtnTestarConexao_Click(
            object? sender,
            EventArgs e)
        {
            _btnTestarConexao.Enabled = false;
            _lblStatus.ForeColor = Color.DarkOrange;
            _lblStatus.Text = "Testando conexão...";

            try
            {
                var resultado = await Task.Run(
                    () => DatabaseHelper.TestarConexao());

                _lblStatus.ForeColor =
                    resultado.ok ? Color.Green : Color.DarkRed;

                _lblStatus.Text = resultado.mensagem;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Erro no teste de conexão: " + ex);

                _lblStatus.ForeColor = Color.DarkRed;
                _lblStatus.Text = "Falha na conexão: " + ex.Message;
            }
            finally
            {
                if (!IsDisposed)
                    _btnTestarConexao.Enabled = true;
            }
        }

        private async void BtnEntrar_Click(
            object? sender,
            EventArgs e)
        {
            var email = _txtEmail.Text.Trim();
            var senha = _txtSenha.Text;

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(senha))
            {
                MostrarErro("Informe e-mail e senha.");
                return;
            }

            _btnEntrar.Enabled = false;
            _btnTestarConexao.Enabled = false;
            _lblStatus.ForeColor = Color.DarkOrange;
            _lblStatus.Text = "Autenticando...";

            try
            {
                using var conn = DatabaseHelper.GetConnection();

                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                const string sql = """
                    SELECT
                        id_usuario,
                        nome_usuario,
                        email_usuario,
                        senha_usuario,
                        nivel_acesso,
                        usuario_ativo
                    FROM public.usuarios
                    WHERE email_usuario = @email
                    LIMIT 1;
                    """;

                int userId;
                string nome;
                string emailUsuario;
                string hash;
                string nivel;
                bool ativo;

                // Obtém os dados do usuário e libera o reader
                // antes de executar outras consultas.
                await using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("email", email);

                    await using var reader = await cmd.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        await RegistrarEventoLogin(
                            conn,
                            null,
                            email,
                            "LOGIN_FALHA",
                            "Credenciais inválidas");

                        MostrarErro("Usuário ou senha inválidos.");
                        return;
                    }

                    userId = reader.GetInt32(
                        reader.GetOrdinal("id_usuario"));

                    nome = reader.GetString(
                        reader.GetOrdinal("nome_usuario"));

                    emailUsuario = reader.GetString(
                        reader.GetOrdinal("email_usuario"));

                    hash = reader.GetString(
                        reader.GetOrdinal("senha_usuario"));

                    nivel = reader.GetString(
                        reader.GetOrdinal("nivel_acesso"));

                    int ativoOrdinal = reader.GetOrdinal("usuario_ativo");

                    ativo = !reader.IsDBNull(ativoOrdinal) &&
                            reader.GetBoolean(ativoOrdinal);
                }

                if (!ativo)
                {
                    await RegistrarEventoLogin(
                        conn,
                        userId,
                        nome,
                        "LOGIN_FALHA",
                        "Tentativa de login de usuário desativado");

                    MostrarErro("Este usuário está desativado.");
                    return;
                }

                bool senhaOk;

                try
                {
                    senhaOk = BCrypt.Net.BCrypt.Verify(senha, hash);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        "Falha ao verificar hash BCrypt: " + ex);

                    senhaOk = false;
                }

                if (!senhaOk)
                {
                    await RegistrarEventoLogin(
                        conn,
                        userId,
                        nome,
                        "LOGIN_FALHA",
                        "Senha inválida");

                    MostrarErro("Usuário ou senha inválidos.");
                    return;
                }

                // Inicializa a sessão e nega todas as permissões
                // até que a consulta ao banco seja concluída.
                Sessao.IdUsuario = userId;
                Sessao.NomeUsuario = nome;
                Sessao.EmailUsuario = emailUsuario;
                Sessao.NivelAcesso = nivel;

                Sessao.PodeCriarAlerta = false;
                Sessao.PodeAprovarManutencao = false;
                Sessao.PodeEditarUsuarios = false;

                // As permissões são obrigatórias para prosseguir.
                // Se houver erro ou perfil não cadastrado, o login
                // não abre o dashboard.
                await CarregarPermissoesAsync(conn);

                await RegistrarEventoLogin(
                    conn,
                    userId,
                    nome,
                    "LOGIN_SUCESSO",
                    "Autenticação concluída");

                _lblStatus.ForeColor = Color.Green;
                _lblStatus.Text = "Login realizado com sucesso.";

                Hide();

                var dashboard = new FormDashboard();
                dashboard.FormClosed += (_, _) => Close();
                dashboard.Show();
            }
            catch (Exception ex)
            {
                // Não deixa uma sessão parcial ativa após falha.
                Sessao.Limpar();

                Debug.WriteLine(
                    "Erro durante a autenticação: " + ex);

                _lblStatus.ForeColor = Color.DarkRed;
                _lblStatus.Text =
                    "Não foi possível concluir o login.";

                MessageBox.Show(
                    this,
                    "Ocorreu um erro durante a autenticação " +
                    "ou o carregamento das permissões.\n\n" +
                    ex.Message,
                    "Orion Inc - Login",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed)
                {
                    _btnEntrar.Enabled = true;
                    _btnTestarConexao.Enabled = true;
                }
            }
        }

        private void MostrarErro(string mensagem)
        {
            _lblStatus.ForeColor = Color.DarkRed;
            _lblStatus.Text = mensagem;
        }

        private static async Task RegistrarEventoLogin(
            NpgsqlConnection conn,
            int? userId,
            string actor,
            string type,
            string description)
        {
            var resultado =
                type == "LOGIN_SUCESSO" ? "SUCESSO" : "FALHA";

            // A falha na auditoria do banco não impede o login.
            try
            {
                const string sql = """
                    INSERT INTO public.auditoria_eventos
                    (
                        tipo_evento,
                        entidade,
                        entidade_id,
                        responsavel,
                        origem,
                        resultado,
                        descricao
                    )
                    VALUES
                    (
                        @type,
                        'usuarios',
                        @id,
                        @actor,
                        'DESKTOP_LOGIN',
                        @result,
                        @description
                    );
                    """;

                await using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("type", type);

                cmd.Parameters.Add(
                    "id",
                    NpgsqlTypes.NpgsqlDbType.Text).Value =
                    (object?)userId?.ToString() ?? DBNull.Value;

                cmd.Parameters.AddWithValue(
                    "actor",
                    string.IsNullOrWhiteSpace(actor)
                        ? "desconhecido"
                        : actor);

                cmd.Parameters.AddWithValue("result", resultado);
                cmd.Parameters.AddWithValue(
                    "description",
                    description);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Falha na auditoria PostgreSQL: " + ex);
            }

            // Grava também no arquivo TXT, sem bloquear a interface
            // com chamadas síncronas de operações assíncronas.
            try
            {
                await TxtAuditLogger.WriteAsync(
                    "auditoria",
                    type,
                    "USUARIO",
                    userId?.ToString(),
                    actor,
                    "DESKTOP",
                    resultado,
                    description);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Falha na auditoria TXT: " + ex);
            }
        }

        private static async Task CarregarPermissoesAsync(
            NpgsqlConnection conn)
        {
            const string sql = """
                SELECT
                    pode_criar_alerta,
                    pode_aprovar_manutencao,
                    pode_editar_usuarios
                FROM public.permissoes_acesso
                WHERE LOWER(nivel_acesso) = LOWER(@nivel)
                LIMIT 1;
                """;

            // Política de segurança: começa sem permissões.
            Sessao.PodeCriarAlerta = false;
            Sessao.PodeAprovarManutencao = false;
            Sessao.PodeEditarUsuarios = false;

            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(
                "nivel",
                Sessao.NivelAcesso);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException(
                    "Não existem permissões cadastradas para o perfil '" +
                    Sessao.NivelAcesso + "'.");
            }

            Sessao.PodeCriarAlerta =
                !reader.IsDBNull(0) && reader.GetBoolean(0);

            Sessao.PodeAprovarManutencao =
                !reader.IsDBNull(1) && reader.GetBoolean(1);

            Sessao.PodeEditarUsuarios =
                !reader.IsDBNull(2) && reader.GetBoolean(2);
        }
    }
}