using Npgsql;
using OrionInc.Models;

namespace OrionInc.Forms
{
    public class FormNovoFuncionario : Form
    {
        private readonly Funcionario? _funcionarioEditando;

        private readonly TextBox _txtNome = new();
        private readonly TextBox _txtCpf = new();
        private readonly TextBox _txtCargo = new();
        private readonly TextBox _txtSetor = new();
        private readonly ComboBox _cmbStatus = new();

        private readonly Button _btnSalvar = new();
        private readonly Button _btnCancelar = new();

        public FormNovoFuncionario(Funcionario? funcionario)
        {
            _funcionarioEditando = funcionario;

            Text = funcionario == null
                ? "Novo Funcionário - Orion Inc"
                : "Editar Funcionário - Orion Inc";

            StartPosition = FormStartPosition.CenterParent;
            Width = 600;
            Height = 500;

            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            CriarInterface();
            ConfigurarEventos();

            OrionUi.StyleModuleControls(this);

            if (_funcionarioEditando != null)
                PreencherDados();
        }

        private void CriarInterface()
        {
            // ============================================================
            // CABEÇALHO
            // ============================================================

            var painelCabecalho = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                Padding = new Padding(25, 18, 25, 12)
            };

            var lblTitulo = new Label
            {
                Text = _funcionarioEditando == null
                    ? "Novo funcionário"
                    : "Editar funcionário",

                AutoSize = true,
                Location = new Point(25, 18),
                Font = new Font("Segoe UI", 18, FontStyle.Bold)
            };

            var lblDescricao = new Label
            {
                Text = _funcionarioEditando == null
                    ? "Cadastre um novo funcionário no sistema."
                    : "Atualize os dados do funcionário selecionado.",

                AutoSize = true,
                Location = new Point(27, 53),
                Font = new Font("Segoe UI", 9)
            };

            painelCabecalho.Controls.Add(lblTitulo);
            painelCabecalho.Controls.Add(lblDescricao);

            // ============================================================
            // ÁREA DOS CAMPOS
            // ============================================================

            var painelCampos = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(25, 15, 25, 10)
            };

            // Nome
            var lblNome = CriarLabel("Nome completo", 25, 5);

            ConfigurarTextBox(
                _txtNome,
                25,
                27,
                510,
                "Digite o nome completo");

            // CPF
            var lblCpf = CriarLabel("CPF", 25, 85);

            ConfigurarTextBox(
                _txtCpf,
                25,
                107,
                245,
                "000.000.000-00");

            // Cargo
            var lblCargo = CriarLabel("Cargo", 300, 85);

            ConfigurarTextBox(
                _txtCargo,
                300,
                107,
                235,
                "Ex.: Maquinista");

            // Setor
            var lblSetor = CriarLabel("Setor", 25, 165);

            ConfigurarTextBox(
                _txtSetor,
                25,
                187,
                245,
                "Ex.: Operações");

            // Status
            var lblStatus = CriarLabel("Status", 300, 165);

            _cmbStatus.Location = new Point(300, 187);
            _cmbStatus.Size = new Size(235, 30);
            _cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbStatus.Items.AddRange(new object[]
            {
                "ativo",
                "afastado",
                "desligado"
            });

            _cmbStatus.SelectedIndex = 0;

            painelCampos.Controls.AddRange(new Control[]
            {
                lblNome,
                _txtNome,

                lblCpf,
                _txtCpf,

                lblCargo,
                _txtCargo,

                lblSetor,
                _txtSetor,

                lblStatus,
                _cmbStatus
            });

            // ============================================================
            // RODAPÉ
            // ============================================================

            var painelBotoes = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                Padding = new Padding(25, 15, 25, 15)
            };

            _btnCancelar.Text = "Cancelar";
            _btnCancelar.Size = new Size(130, 38);
            _btnCancelar.Location = new Point(255, 15);
            _btnCancelar.Anchor = AnchorStyles.Right | AnchorStyles.Top;

            _btnSalvar.Text = _funcionarioEditando == null
                ? "Salvar funcionário"
                : "Salvar alterações";

            _btnSalvar.Size = new Size(165, 38);
            _btnSalvar.Location = new Point(390, 15);
            _btnSalvar.Anchor = AnchorStyles.Right | AnchorStyles.Top;

            painelBotoes.Controls.Add(_btnCancelar);
            painelBotoes.Controls.Add(_btnSalvar);

            Controls.Add(painelCampos);
            Controls.Add(painelBotoes);
            Controls.Add(painelCabecalho);

            AcceptButton = _btnSalvar;
            CancelButton = _btnCancelar;
        }

        private Label CriarLabel(string texto, int x, int y)
        {
            return new Label
            {
                Text = texto,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
        }

        private void ConfigurarTextBox(
            TextBox campo,
            int x,
            int y,
            int largura,
            string placeholder)
        {
            campo.Location = new Point(x, y);
            campo.Size = new Size(largura, 30);
            campo.PlaceholderText = placeholder;
            campo.BorderStyle = BorderStyle.FixedSingle;
        }

        private void ConfigurarEventos()
        {
            _btnSalvar.Click += BtnSalvar_Click;

            _btnCancelar.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            // Permite cancelar com ESC.
            KeyPreview = true;

            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };
        }

        private void PreencherDados()
        {
            _txtNome.Text = _funcionarioEditando!.NomeFuncionario;
            _txtCpf.Text = _funcionarioEditando.Cpf;
            _txtCargo.Text = _funcionarioEditando.Cargo;
            _txtSetor.Text = _funcionarioEditando.Setor;

            var status = _funcionarioEditando.StatusFuncionario;

            if (!string.IsNullOrWhiteSpace(status))
            {
                var indice = _cmbStatus.FindStringExact(status);

                if (indice >= 0)
                    _cmbStatus.SelectedIndex = indice;
            }
        }

        private void BtnSalvar_Click(object? sender, EventArgs e)
        {
            var nome = _txtNome.Text.Trim();
            var cpf = _txtCpf.Text.Trim();
            var cargo = _txtCargo.Text.Trim();
            var setor = _txtSetor.Text.Trim();
            var status = _cmbStatus.SelectedItem?.ToString() ?? "ativo";

            if (nome.Length == 0 ||
                cpf.Length == 0 ||
                cargo.Length == 0 ||
                setor.Length == 0)
            {
                MessageBox.Show(
                    "Preencha todos os campos obrigatórios.",
                    "Dados incompletos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;

                using var conn = Data.DatabaseHelper.GetConnection();

                if (_funcionarioEditando == null)
                {
                    const string sql = @"
                        INSERT INTO funcionarios
                            (nome_funcionario, cpf, cargo, setor, status_funcionario)
                        VALUES
                            (@nome, @cpf, @cargo, @setor, @status)";

                    using var cmd = new NpgsqlCommand(sql, conn);

                    cmd.Parameters.AddWithValue("nome", nome);
                    cmd.Parameters.AddWithValue("cpf", cpf);
                    cmd.Parameters.AddWithValue("cargo", cargo);
                    cmd.Parameters.AddWithValue("setor", setor);
                    cmd.Parameters.AddWithValue("status", status);

                    cmd.ExecuteNonQuery();
                }
                else
                {
                    const string sql = @"
                        UPDATE funcionarios
                        SET
                            nome_funcionario = @nome,
                            cpf = @cpf,
                            cargo = @cargo,
                            setor = @setor,
                            status_funcionario = @status
                        WHERE id_funcionario = @id";

                    using var cmd = new NpgsqlCommand(sql, conn);

                    cmd.Parameters.AddWithValue("nome", nome);
                    cmd.Parameters.AddWithValue("cpf", cpf);
                    cmd.Parameters.AddWithValue("cargo", cargo);
                    cmd.Parameters.AddWithValue("setor", setor);
                    cmd.Parameters.AddWithValue("status", status);
                    cmd.Parameters.AddWithValue(
                        "id",
                        _funcionarioEditando.IdFuncionario);

                    cmd.ExecuteNonQuery();
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (PostgresException pex)
                when (pex.SqlState == "23505")
            {
                MessageBox.Show(
                    "Já existe um funcionário cadastrado com este CPF.",
                    "CPF duplicado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _txtCpf.Focus();
                _txtCpf.SelectAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Erro ao salvar o funcionário:\n\n" + ex.Message,
                    "Erro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}