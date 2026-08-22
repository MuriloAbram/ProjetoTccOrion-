using System.Data;
using Npgsql;
using OrionInc.Models;

namespace OrionInc.Forms
{
    public class FormUsuarios : Form
    {
        private readonly DataGridView _grid = new();

        public FormUsuarios()
        {
            Text = "Usuários do Sistema";
            Width = 900;
            Height = 560;

            var btnNovo = new Button { Text = "Novo usuário", Location = new Point(15, 12), Width = 130 };
            btnNovo.Click += (_, _) => AbrirFormulario(null);

            var btnEditar = new Button { Text = "Editar", Location = new Point(155, 12), Width = 110 };
            btnEditar.Click += (_, _) => EditarSelecionado();

            var btnExcluir = new Button { Text = "Excluir", Location = new Point(275, 12), Width = 110 };
            btnExcluir.Click += (_, _) => ExcluirSelecionado();

            var btnAtualizar = new Button { Text = "Atualizar lista", Location = new Point(395, 12), Width = 120 };
            btnAtualizar.Click += (_, _) => Recarregar();

            _grid.Location = new Point(15, 50);
            _grid.Size = new Size(860, 460);
            _grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.DoubleClick += (_, _) => EditarSelecionado();

            Controls.AddRange(new Control[] { btnNovo, btnEditar, btnExcluir, btnAtualizar, _grid });
            Load += (_, _) => Recarregar();
        }

        private void Recarregar()
        {
            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();
                // Usa a view vw_usuarios_sistema, que já omite a senha
                const string sql = @"
                    SELECT id_usuario AS ""ID"", nome_usuario AS ""Nome"", email_usuario AS ""E-mail"",
                           nivel_acesso AS ""Nível de acesso"", usuario_ativo AS ""Ativo""
                    FROM vw_usuarios_sistema
                    ORDER BY nome_usuario";
                using var adapter = new NpgsqlDataAdapter(sql, conn);
                var dt = new DataTable();
                adapter.Fill(dt);
                _grid.DataSource = dt;
                if (_grid.Columns["ID"] != null) _grid.Columns["ID"]!.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar usuários: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Usuario? ObterSelecionado()
        {
            if (_grid.CurrentRow == null) return null;
            var row = _grid.CurrentRow;
            return new Usuario
            {
                IdUsuario = Convert.ToInt32(row.Cells["ID"].Value),
                NomeUsuario = row.Cells["Nome"].Value?.ToString() ?? "",
                EmailUsuario = row.Cells["E-mail"].Value?.ToString() ?? "",
                NivelAcesso = row.Cells["Nível de acesso"].Value?.ToString() ?? "",
                UsuarioAtivo = Convert.ToBoolean(row.Cells["Ativo"].Value)
            };
        }

        private void AbrirFormulario(Usuario? usuario)
        {
            using var form = new FormCadastroUsuario(usuario);
            if (form.ShowDialog() == DialogResult.OK)
                Recarregar();
        }

        private void EditarSelecionado()
        {
            var u = ObterSelecionado();
            if (u == null)
            {
                MessageBox.Show("Selecione um usuário na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            AbrirFormulario(u);
        }

        private void ExcluirSelecionado()
        {
            var u = ObterSelecionado();
            if (u == null)
            {
                MessageBox.Show("Selecione um usuário na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (u.IdUsuario == Data.Sessao.IdUsuario)
            {
                MessageBox.Show("Você não pode excluir o próprio usuário logado.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show($"Confirma a exclusão do usuário \"{u.NomeUsuario}\"?",
                    "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();
                using var cmd = new NpgsqlCommand("DELETE FROM usuarios WHERE id_usuario = @id", conn);
                cmd.Parameters.AddWithValue("id", u.IdUsuario);
                cmd.ExecuteNonQuery();
                Recarregar();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao excluir: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>Formulário de cadastro/edição de usuário, com senha em texto plano na tela e hash BCrypt no banco.</summary>
    public class FormCadastroUsuario : Form
    {
        private readonly Usuario? _usuarioEditando;

        private readonly TextBox _txtNome = new();
        private readonly TextBox _txtEmail = new();
        private readonly TextBox _txtSenha = new() { PasswordChar = '●' };
        private readonly ComboBox _cmbNivel = new();
        private readonly CheckBox _chkAtivo = new();

        public FormCadastroUsuario(Usuario? usuario)
        {
            _usuarioEditando = usuario;

            Text = usuario == null ? "Novo Usuário" : "Editar Usuário";
            Width = 420;
            Height = 420;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int y = 20;

            var lblNome = new Label { Text = "Nome:", Location = new Point(20, y), AutoSize = true };
            _txtNome.Location = new Point(20, y + 20);
            _txtNome.Width = 350;
            Controls.Add(lblNome);
            Controls.Add(_txtNome);
            y += 55;

            var lblEmail = new Label { Text = "E-mail:", Location = new Point(20, y), AutoSize = true };
            _txtEmail.Location = new Point(20, y + 20);
            _txtEmail.Width = 350;
            Controls.Add(lblEmail);
            Controls.Add(_txtEmail);
            y += 55;

            var lblSenha = new Label
            {
                Text = usuario == null ? "Senha:" : "Nova senha (deixe em branco para manter):",
                Location = new Point(20, y),
                AutoSize = true
            };
            _txtSenha.Location = new Point(20, y + 20);
            _txtSenha.Width = 350;
            Controls.Add(lblSenha);
            Controls.Add(_txtSenha);
            y += 55;

            var lblNivel = new Label { Text = "Nível de acesso:", Location = new Point(20, y), AutoSize = true };
            _cmbNivel.Location = new Point(20, y + 20);
            _cmbNivel.Width = 350;
            _cmbNivel.DropDownStyle = ComboBoxStyle.DropDownList;
            Controls.Add(lblNivel);
            Controls.Add(_cmbNivel);
            y += 55;

            _chkAtivo.Text = "Usuário ativo";
            _chkAtivo.Location = new Point(20, y);
            _chkAtivo.AutoSize = true;
            _chkAtivo.Checked = true;
            Controls.Add(_chkAtivo);
            y += 40;

            var btnSalvar = new Button { Text = "Salvar", Location = new Point(20, y), Width = 160, Height = 32 };
            btnSalvar.Click += BtnSalvar_Click;
            var btnCancelar = new Button { Text = "Cancelar", Location = new Point(210, y), Width = 160, Height = 32 };
            btnCancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnSalvar);
            Controls.Add(btnCancelar);

            Load += (_, _) => CarregarNiveisAcesso();
        }

        private void CarregarNiveisAcesso()
        {
            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();
                using var cmd = new NpgsqlCommand(
                    "SELECT nivel_acesso FROM permissoes_acesso ORDER BY nivel_acesso", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    _cmbNivel.Items.Add(reader.GetString(0));

                if (_cmbNivel.Items.Count > 0) _cmbNivel.SelectedIndex = 0;

                if (_usuarioEditando != null)
                {
                    _txtNome.Text = _usuarioEditando.NomeUsuario;
                    _txtEmail.Text = _usuarioEditando.EmailUsuario;
                    _cmbNivel.SelectedItem = _usuarioEditando.NivelAcesso;
                    _chkAtivo.Checked = _usuarioEditando.UsuarioAtivo;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar níveis de acesso: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSalvar_Click(object? sender, EventArgs e)
        {
            var nome = _txtNome.Text.Trim();
            var email = _txtEmail.Text.Trim();
            var senha = _txtSenha.Text;
            var nivel = _cmbNivel.SelectedItem?.ToString() ?? "";

            if (nome.Length == 0 || email.Length == 0 || nivel.Length == 0)
            {
                MessageBox.Show("Preencha nome, e-mail e nível de acesso.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_usuarioEditando == null && senha.Length == 0)
            {
                MessageBox.Show("Informe a senha do novo usuário.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();

                if (_usuarioEditando == null)
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(senha);
                    const string sql = @"
                        INSERT INTO usuarios (nome_usuario, email_usuario, senha_usuario, nivel_acesso, usuario_ativo)
                        VALUES (@nome, @email, @senha, @nivel, @ativo)";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("nome", nome);
                    cmd.Parameters.AddWithValue("email", email);
                    cmd.Parameters.AddWithValue("senha", hash);
                    cmd.Parameters.AddWithValue("nivel", nivel);
                    cmd.Parameters.AddWithValue("ativo", _chkAtivo.Checked);
                    cmd.ExecuteNonQuery();
                }
                else if (senha.Length == 0)
                {
                    const string sql = @"
                        UPDATE usuarios
                        SET nome_usuario = @nome, email_usuario = @email, nivel_acesso = @nivel, usuario_ativo = @ativo
                        WHERE id_usuario = @id";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("nome", nome);
                    cmd.Parameters.AddWithValue("email", email);
                    cmd.Parameters.AddWithValue("nivel", nivel);
                    cmd.Parameters.AddWithValue("ativo", _chkAtivo.Checked);
                    cmd.Parameters.AddWithValue("id", _usuarioEditando.IdUsuario);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(senha);
                    const string sql = @"
                        UPDATE usuarios
                        SET nome_usuario = @nome, email_usuario = @email, senha_usuario = @senha,
                            nivel_acesso = @nivel, usuario_ativo = @ativo
                        WHERE id_usuario = @id";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("nome", nome);
                    cmd.Parameters.AddWithValue("email", email);
                    cmd.Parameters.AddWithValue("senha", hash);
                    cmd.Parameters.AddWithValue("nivel", nivel);
                    cmd.Parameters.AddWithValue("ativo", _chkAtivo.Checked);
                    cmd.Parameters.AddWithValue("id", _usuarioEditando.IdUsuario);
                    cmd.ExecuteNonQuery();
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (PostgresException pex) when (pex.SqlState == "23505")
            {
                MessageBox.Show("Já existe um usuário cadastrado com este e-mail.", "E-mail duplicado",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
