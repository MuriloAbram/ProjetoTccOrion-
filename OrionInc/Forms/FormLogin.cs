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
            ClientSize = new Size(820, 500);
            MinimumSize = new Size(760, 460);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            BackColor = OrionUi.Background;
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = OrionUi.Background
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57f));

            var brand = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = OrionUi.Navy,
                Padding = new Padding(42)
            };

            var brandLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = Color.Transparent
            };
            brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            brandLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

            var logo = new Label
            {
                Text = "ORION INC",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 25, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var line = new Panel
            {
                Height = 3,
                Width = 90,
                BackColor = Color.FromArgb(62, 142, 214),
                Anchor = AnchorStyles.Left
            };

            var subtitle = new Label
            {
                Text = "Sistema de Monitoramento\nFerroviário",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(220, 231, 244),
                Font = new Font("Segoe UI", 13, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var description = new Label
            {
                Text = "Monitoramento de sensores, dispositivos,\nalertas e operações ferroviárias em um único ambiente.",
                Dock = DockStyle.Top,
                ForeColor = Color.FromArgb(174, 193, 216),
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = true
            };

            var systemStatus = new Label
            {
                Text = "●  AMBIENTE OPERACIONAL",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(122, 220, 166),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var version = new Label
            {
                Text = "ORION INC  •  Monitoramento ferroviário",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(139, 158, 183),
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.BottomLeft
            };

            brandLayout.Controls.Add(logo, 0, 0);
            brandLayout.Controls.Add(line, 0, 1);
            brandLayout.Controls.Add(subtitle, 0, 2);
            brandLayout.Controls.Add(description, 0, 3);
            brandLayout.Controls.Add(systemStatus, 0, 4);
            brandLayout.Controls.Add(version, 0, 5);
            brand.Controls.Add(brandLayout);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(46, 42, 46, 36)
            };

            var contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 10,
                BackColor = Color.White
            };
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label
            {
                Text = "Acesso ao sistema",
                Dock = DockStyle.Fill,
                ForeColor = OrionUi.Text,
                Font = new Font("Segoe UI", 19, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var helper = new Label
            {
                Text = "Entre com suas credenciais para continuar.",
                Dock = DockStyle.Fill,
                ForeColor = OrionUi.Muted,
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblEmail = new Label
            {
                Text = "E-mail",
                Dock = DockStyle.Fill,
                ForeColor = OrionUi.Text,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft
            };

            _txtEmail.Dock = DockStyle.Fill;
            _txtEmail.Margin = new Padding(0, 6, 0, 6);
            _txtEmail.Font = new Font("Segoe UI", 10.5f);
            _txtEmail.PlaceholderText = "nome@empresa.com";
            _txtEmail.BorderStyle = BorderStyle.FixedSingle;

            var lblSenha = new Label
            {
                Text = "Senha",
                Dock = DockStyle.Fill,
                ForeColor = OrionUi.Text,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft
            };

            var passwordPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(0, 6, 0, 6)
            };

            _txtSenha.Dock = DockStyle.Fill;
            _txtSenha.Margin = new Padding(0, 0, 38, 0);
            _txtSenha.Font = new Font("Segoe UI", 10.5f);
            _txtSenha.PasswordChar = '●';
            _txtSenha.BorderStyle = BorderStyle.FixedSingle;
            _txtSenha.PlaceholderText = "Digite sua senha";

            var btnMostrarSenha = new Button
            {
                Text = "Mostrar",
                Width = 70,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = OrionUi.Primary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMostrarSenha.FlatAppearance.BorderSize = 0;
            btnMostrarSenha.Click += (_, _) =>
            {
                _txtSenha.PasswordChar = _txtSenha.PasswordChar == '\0' ? '●' : '\0';
                btnMostrarSenha.Text = _txtSenha.PasswordChar == '\0' ? "Ocultar" : "Mostrar";
                _txtSenha.Focus();
            };

            passwordPanel.Controls.Add(_txtSenha);
            passwordPanel.Controls.Add(btnMostrarSenha);

            var options = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            var capsLock = new Label
            {
                Text = "Caps Lock ativado",
                AutoSize = true,
                ForeColor = OrionUi.Warning,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Visible = Control.IsKeyLocked(Keys.CapsLock),
                Location = new Point(0, 5)
            };

            var hint = new Label
            {
                Text = "A senha diferencia maiúsculas e minúsculas.",
                AutoSize = true,
                ForeColor = OrionUi.Muted,
                Font = new Font("Segoe UI", 8.5f),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            hint.Left = Math.Max(0, content.ClientSize.Width - hint.Width - content.Padding.Right - 5);
            hint.Top = 5;

            options.Controls.Add(capsLock);
            options.Controls.Add(hint);
            options.Resize += (_, _) =>
                hint.Left = Math.Max(0, options.ClientSize.Width - hint.Width);

            KeyPreview = true;
            KeyDown += (_, _) =>
            {
                capsLock.Visible = Control.IsKeyLocked(Keys.CapsLock);
            };

            _btnEntrar.Text = "Entrar no sistema";
            _btnEntrar.Dock = DockStyle.Fill;
            _btnEntrar.Margin = new Padding(0, 4, 0, 4);
            _btnEntrar.FlatStyle = FlatStyle.Flat;
            _btnEntrar.FlatAppearance.BorderSize = 0;
            _btnEntrar.BackColor = OrionUi.Primary;
            _btnEntrar.ForeColor = Color.White;
            _btnEntrar.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            _btnEntrar.Cursor = Cursors.Hand;
            _btnEntrar.Click += BtnEntrar_Click;
            _btnEntrar.MouseEnter += (_, _) => _btnEntrar.BackColor = Color.FromArgb(29, 72, 117);
            _btnEntrar.MouseLeave += (_, _) => _btnEntrar.BackColor = OrionUi.Primary;

            _btnTestarConexao.Text = "Testar conexão com o banco";
            _btnTestarConexao.Dock = DockStyle.Fill;
            _btnTestarConexao.FlatStyle = FlatStyle.Flat;
            _btnTestarConexao.FlatAppearance.BorderSize = 1;
            _btnTestarConexao.FlatAppearance.BorderColor = OrionUi.Border;
            _btnTestarConexao.BackColor = Color.White;
            _btnTestarConexao.ForeColor = OrionUi.Text;
            _btnTestarConexao.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            _btnTestarConexao.Cursor = Cursors.Hand;
            _btnTestarConexao.Click += BtnTestarConexao_Click;

            _lblStatus.Text = "Aguardando autenticação.";
            _lblStatus.Dock = DockStyle.Fill;
            _lblStatus.AutoEllipsis = true;
            _lblStatus.ForeColor = OrionUi.Muted;
            _lblStatus.Font = new Font("Segoe UI", 8.5f);
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            contentLayout.Controls.Add(title, 0, 0);
            contentLayout.Controls.Add(helper, 0, 1);
            contentLayout.Controls.Add(lblEmail, 0, 2);
            contentLayout.Controls.Add(_txtEmail, 0, 3);
            contentLayout.Controls.Add(lblSenha, 0, 4);
            contentLayout.Controls.Add(passwordPanel, 0, 5);
            contentLayout.Controls.Add(options, 0, 6);
            contentLayout.Controls.Add(_btnEntrar, 0, 7);
            contentLayout.Controls.Add(_btnTestarConexao, 0, 8);
            contentLayout.Controls.Add(_lblStatus, 0, 9);

            content.Controls.Add(contentLayout);

            root.Controls.Add(brand, 0, 0);
            root.Controls.Add(content, 1, 0);
            Controls.Add(root);

            AcceptButton = _btnEntrar;
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

                await RegistrarEventoLogin(
                    conn,
                    null,
                    email,
                    "LOGIN_TENTATIVA",
                    "Tentativa de autenticação iniciada.",
                    new
                    {
                        emailInformado = email,
                        horarioLocal = DateTimeOffset.Now,
                        computador = Environment.MachineName
                    });

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
                            "Usuário não encontrado para o e-mail informado.",
                            new { emailInformado = email, motivo = "USUARIO_NAO_ENCONTRADO" });

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

                await RegistrarEventoLogin(
                    conn,
                    userId,
                    nome,
                    "USUARIO_ENCONTRADO",
                    "Conta localizada no PostgreSQL e dados básicos carregados.",
                    new
                    {
                        emailInformado = email,
                        usuarioId = userId,
                        nivelAcesso = nivel,
                        ativo
                    });

                if (!ativo)
                {
                    await RegistrarEventoLogin(
                        conn,
                        userId,
                        nome,
                        "LOGIN_FALHA",
                        "Tentativa de login de usuário desativado.",
                        new { emailInformado = email, motivo = "USUARIO_INATIVO", usuarioId = userId });

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

                if (senhaOk)
                {
                    await RegistrarEventoLogin(
                        conn,
                        userId,
                        nome,
                        "VALIDACAO_SENHA_SUCESSO",
                        "Hash BCrypt validado com sucesso. A senha nunca é gravada no log.",
                        new { usuarioId = userId });
                }

                if (!senhaOk)
                {
                    await RegistrarEventoLogin(
                        conn,
                        userId,
                        nome,
                        "LOGIN_FALHA",
                        "Senha inválida.",
                        new { emailInformado = email, motivo = "SENHA_INVALIDA", usuarioId = userId });

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
                    "Autenticação concluída. Sessão inicializada e permissões carregadas.",
                    new
                    {
                        emailInformado = email,
                        emailUsuario = emailUsuario,
                        nivelAcesso = nivel,
                        permissoes = new
                        {
                            criarAlertas = Sessao.PodeCriarAlerta,
                            aprovarManutencao = Sessao.PodeAprovarManutencao,
                            editarUsuarios = Sessao.PodeEditarUsuarios
                        }
                    });

                _lblStatus.ForeColor = Color.Green;
                _lblStatus.Text = "Login realizado com sucesso.";

                await RegistrarEventoLogin(
                    conn,
                    userId,
                    nome,
                    "LOGIN_SUCESSO",
                    "Autenticação concluída e sessão autorizada.",
                    new
                    {
                        usuarioId = userId,
                        nivelAcesso = nivel
                    });

                await RegistrarEventoLogin(
                    conn,
                    userId,
                    nome,
                    "DASHBOARD_ABERTA",
                    "Tela principal de monitoramento preparada após autenticação.",
                    new
                    {
                        usuarioId = userId,
                        nivelAcesso = nivel
                    });

                Hide();

                var dashboard = new FormDashboard();
                dashboard.FormClosed += (_, _) => Close();
                dashboard.Show();
            }
            catch (Exception ex)
            {
                try
                {
                    await RegistrarEventoLogin(
                        null,
                        null,
                        email,
                        "LOGIN_ERRO",
                        "Erro inesperado durante a autenticação ou carregamento das permissões.",
                        new
                        {
                            emailInformado = email,
                            tipoErro = ex.GetType().Name,
                            mensagemTecnica = ex.Message
                        });
                }
                catch
                {
                    // Auditoria nunca deve impedir a mensagem de erro ao usuário.
                }

                // Não deixa uma sessão parcial ativa após falha.
                Sessao.Limpar();

                Debug.WriteLine(
                    "Erro durante a autenticação: " + ex);

                _lblStatus.ForeColor = Color.DarkRed;
                _lblStatus.Text =
                    "Não foi possível concluir o login.";

                _lblStatus.Text = "Erro de autenticação. Verifique o banco, as credenciais e as permissões.";
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
            NpgsqlConnection? conn,
            int? userId,
            string actor,
            string type,
            string description,
            object? details = null)
        {
            var resultado = type == "LOGIN_TENTATIVA"
                ? "INICIADO"
                : type is "LOGIN_SUCESSO" or "USUARIO_ENCONTRADO" or "VALIDACAO_SENHA_SUCESSO" or "DASHBOARD_ABERTA"
                    ? "SUCESSO"
                    : "FALHA";

            string detalhesJson = details is null
                ? "{}"
                : System.Text.Json.JsonSerializer.Serialize(details);

            try
            {
                if (conn is not null)
                {
                    const string sql = """
                        INSERT INTO public.auditoria_eventos
                        (
                            tipo_evento,
                            entidade,
                            entidade_id,
                            id_usuario,
                            responsavel,
                            origem,
                            resultado,
                            descricao,
                            modulo,
                            detalhes,
                            hostname
                        )
                        VALUES
                        (
                            @type,
                            'usuarios',
                            @id,
                            @idUsuario,
                            @actor,
                            'DESKTOP_LOGIN',
                            @result,
                            @description,
                            'FormLogin',
                            CAST(@details AS jsonb),
                            @hostname
                        );
                        """;

                    await using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("type", type);
                    cmd.Parameters.Add("id", NpgsqlTypes.NpgsqlDbType.Text).Value =
                        (object?)userId?.ToString() ?? DBNull.Value;
                    cmd.Parameters.Add("idUsuario", NpgsqlTypes.NpgsqlDbType.Integer).Value =
                        (object?)userId ?? DBNull.Value;
                    cmd.Parameters.AddWithValue(
                        "actor",
                        string.IsNullOrWhiteSpace(actor)
                            ? "desconhecido"
                            : actor);
                    cmd.Parameters.AddWithValue("result", resultado);
                    cmd.Parameters.AddWithValue("description", description);
                    cmd.Parameters.AddWithValue("details", detalhesJson);
                    cmd.Parameters.AddWithValue("hostname", Environment.MachineName);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Falha na auditoria PostgreSQL: " + ex);
            }

            try
            {
                await TxtAuditLogger.WriteAsync(
                    "auditoria",
                    type,
                    "USUARIO",
                    userId?.ToString(),
                    actor,
                    "DESKTOP_LOGIN",
                    resultado,
                    description,
                    null,
                    "FormLogin",
                    detalhesJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Falha na auditoria TXT: " + ex);
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