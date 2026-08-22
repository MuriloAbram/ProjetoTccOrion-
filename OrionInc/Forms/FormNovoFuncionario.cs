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

        public FormNovoFuncionario(Funcionario? funcionario)
        {
            _funcionarioEditando = funcionario;

            Text = funcionario == null ? "Novo Funcionário" : "Editar Funcionário";
            Width = 420;
            Height = 340;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var y = 20;
            AdicionarCampo("Nome completo:", _txtNome, ref y);
            AdicionarCampo("CPF:", _txtCpf, ref y);
            AdicionarCampo("Cargo:", _txtCargo, ref y);
            AdicionarCampo("Setor:", _txtSetor, ref y);

            var lblStatus = new Label { Text = "Status:", Location = new Point(20, y), AutoSize = true };
            _cmbStatus.Location = new Point(20, y + 20);
            _cmbStatus.Width = 350;
            _cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbStatus.Items.AddRange(new object[] { "ativo", "afastado", "desligado" });
            _cmbStatus.SelectedIndex = 0;
            Controls.Add(lblStatus);
            Controls.Add(_cmbStatus);
            y += 55;

            var btnSalvar = new Button { Text = "Salvar", Location = new Point(20, y), Width = 160, Height = 32 };
            btnSalvar.Click += BtnSalvar_Click;

            var btnCancelar = new Button { Text = "Cancelar", Location = new Point(210, y), Width = 160, Height = 32 };
            btnCancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(btnSalvar);
            Controls.Add(btnCancelar);

            if (_funcionarioEditando != null)
            {
                _txtNome.Text = _funcionarioEditando.NomeFuncionario;
                _txtCpf.Text = _funcionarioEditando.Cpf;
                _txtCargo.Text = _funcionarioEditando.Cargo;
                _txtSetor.Text = _funcionarioEditando.Setor;
                _cmbStatus.SelectedItem = _funcionarioEditando.StatusFuncionario;
            }
        }

        private void AdicionarCampo(string rotulo, TextBox campo, ref int y)
        {
            var lbl = new Label { Text = rotulo, Location = new Point(20, y), AutoSize = true };
            campo.Location = new Point(20, y + 20);
            campo.Width = 350;
            Controls.Add(lbl);
            Controls.Add(campo);
            y += 55;
        }

        private void BtnSalvar_Click(object? sender, EventArgs e)
        {
            var nome = _txtNome.Text.Trim();
            var cpf = _txtCpf.Text.Trim();
            var cargo = _txtCargo.Text.Trim();
            var setor = _txtSetor.Text.Trim();
            var status = _cmbStatus.SelectedItem?.ToString() ?? "ativo";

            if (nome.Length == 0 || cpf.Length == 0 || cargo.Length == 0 || setor.Length == 0)
            {
                MessageBox.Show("Preencha todos os campos.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();

                if (_funcionarioEditando == null)
                {
                    const string sql = @"
                        INSERT INTO funcionarios (nome_funcionario, cpf, cargo, setor, status_funcionario)
                        VALUES (@nome, @cpf, @cargo, @setor, @status)";
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
                        SET nome_funcionario = @nome, cpf = @cpf, cargo = @cargo,
                            setor = @setor, status_funcionario = @status
                        WHERE id_funcionario = @id";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("nome", nome);
                    cmd.Parameters.AddWithValue("cpf", cpf);
                    cmd.Parameters.AddWithValue("cargo", cargo);
                    cmd.Parameters.AddWithValue("setor", setor);
                    cmd.Parameters.AddWithValue("status", status);
                    cmd.Parameters.AddWithValue("id", _funcionarioEditando.IdFuncionario);
                    cmd.ExecuteNonQuery();
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (PostgresException pex) when (pex.SqlState == "23505")
            {
                MessageBox.Show("Já existe um funcionário cadastrado com este CPF.", "CPF duplicado",
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
