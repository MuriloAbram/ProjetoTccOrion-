using Npgsql;
using OrionInc.Data;

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

            var lblEmail = new Label { Text = "E-mail:", Location = new Point(40, 105), AutoSize = true };
            _txtEmail.Location = new Point(40, 125);
            _txtEmail.Width = 320;

            var lblSenha = new Label { Text = "Senha:", Location = new Point(40, 155), AutoSize = true };
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
                lblTitulo, lblSub, lblEmail, _txtEmail, lblSenha, _txtSenha,
                _btnEntrar, _btnTestarConexao, _lblStatus
            });
        }

        private void BtnTestarConexao_Click(object? sender, EventArgs e)
        {
            var (ok, mensagem) = DatabaseHelper.TestarConexao();
            _lblStatus.ForeColor = ok ? Color.Green : Color.DarkRed;
            _lblStatus.Text = mensagem;
        }

        private void BtnEntrar_Click(object? sender, EventArgs e)
        {
            var email = _txtEmail.Text.Trim();
            var senha = _txtSenha.Text;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            {
                _lblStatus.ForeColor = Color.DarkRed;
                _lblStatus.Text = "Informe e-mail e senha.";
                return;
            }

            try
            {
                using var conn = DatabaseHelper.GetConnection();
                const string sql = @"
                    SELECT id_usuario, nome_usuario, email_usuario, senha_usuario,
                           nivel_acesso, usuario_ativo
                    FROM usuarios
                    WHERE email_usuario = @email";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("email", email);
                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                {
                    _lblStatus.ForeColor = Color.DarkRed;
                    _lblStatus.Text = "Usuário ou senha inválidos.";
                    return;
                }

                var ativo = reader.GetBoolean(reader.GetOrdinal("usuario_ativo"));
                var hash = reader.GetString(reader.GetOrdinal("senha_usuario"));

                if (!ativo)
                {
                    _lblStatus.ForeColor = Color.DarkRed;
                    _lblStatus.Text = "Este usuário está desativado.";
                    return;
                }

                bool senhaOk;
                try
                {
                    senhaOk = BCrypt.Net.BCrypt.Verify(senha, hash);
                }
                catch
                {
                    // Hash antigo/inválido (não gerado pelo BCrypt) - nunca autentica.
                    senhaOk = false;
                }

                if (!senhaOk)
                {
                    _lblStatus.ForeColor = Color.DarkRed;
                    _lblStatus.Text = "Usuário ou senha inválidos.";
                    return;
                }

                Sessao.IdUsuario = reader.GetInt32(reader.GetOrdinal("id_usuario"));
                Sessao.NomeUsuario = reader.GetString(reader.GetOrdinal("nome_usuario"));
                Sessao.EmailUsuario = reader.GetString(reader.GetOrdinal("email_usuario"));
                Sessao.NivelAcesso = reader.GetString(reader.GetOrdinal("nivel_acesso"));
                reader.Close();

                CarregarPermissoes(conn);

                Hide();
                var menu = new FormMenu();
                menu.FormClosed += (_, _) => Close();
                menu.Show();
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = Color.DarkRed;
                _lblStatus.Text = "Erro ao conectar no banco: " + ex.Message;
            }
        }

        private void InitializeComponent()
        {

        }

        private static void CarregarPermissoes(NpgsqlConnection conn)
        {
            const string sql = @"
                SELECT pode_criar_alerta, pode_aprovar_manutencao, pode_editar_usuarios
                FROM permissoes_acesso
                WHERE nivel_acesso = @nivel";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("nivel", Sessao.NivelAcesso);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                Sessao.PodeCriarAlerta = reader.GetBoolean(0);
                Sessao.PodeAprovarManutencao = reader.GetBoolean(1);
                Sessao.PodeEditarUsuarios = reader.GetBoolean(2);
            }
        }
    }
}
