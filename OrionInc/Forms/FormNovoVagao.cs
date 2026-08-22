using Npgsql;
using OrionInc.Models;

namespace OrionInc.Forms
{
    public class FormNovoVagao : Form
    {
        private readonly Vagao? _vagaoEditando;

        private readonly TextBox _txtCodigo = new();
        private readonly TextBox _txtTipo = new();
        private readonly ComboBox _cmbStatus = new();
        private readonly CheckBox _chkTemManutencao = new();
        private readonly DateTimePicker _dtManutencao = new();
        private readonly TextBox _txtObs = new() { Multiline = true, Height = 60 };

        public FormNovoVagao(Vagao? vagao)
        {
            _vagaoEditando = vagao;

            Text = vagao == null ? "Novo Vagão" : "Editar Vagão";
            Width = 420;
            Height = 460;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int y = 20;

            var lblCodigo = new Label { Text = "Código do vagão:", Location = new Point(20, y), AutoSize = true };
            _txtCodigo.Location = new Point(20, y + 20);
            _txtCodigo.Width = 350;
            Controls.Add(lblCodigo);
            Controls.Add(_txtCodigo);
            y += 55;

            var lblTipo = new Label { Text = "Tipo (carga/função):", Location = new Point(20, y), AutoSize = true };
            _txtTipo.Location = new Point(20, y + 20);
            _txtTipo.Width = 350;
            Controls.Add(lblTipo);
            Controls.Add(_txtTipo);
            y += 55;

            var lblStatus = new Label { Text = "Status operacional:", Location = new Point(20, y), AutoSize = true };
            _cmbStatus.Location = new Point(20, y + 20);
            _cmbStatus.Width = 350;
            _cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbStatus.Items.AddRange(new object[] { "operando", "manutencao", "parado" });
            _cmbStatus.SelectedIndex = 0;
            Controls.Add(lblStatus);
            Controls.Add(_cmbStatus);
            y += 55;

            _chkTemManutencao.Text = "Possui data de última manutenção";
            _chkTemManutencao.Location = new Point(20, y);
            _chkTemManutencao.AutoSize = true;
            _chkTemManutencao.CheckedChanged += (_, _) => _dtManutencao.Enabled = _chkTemManutencao.Checked;
            Controls.Add(_chkTemManutencao);
            y += 25;

            _dtManutencao.Location = new Point(20, y);
            _dtManutencao.Width = 200;
            _dtManutencao.Format = DateTimePickerFormat.Short;
            _dtManutencao.Enabled = false;
            Controls.Add(_dtManutencao);
            y += 40;

            var lblObs = new Label { Text = "Observações:", Location = new Point(20, y), AutoSize = true };
            _txtObs.Location = new Point(20, y + 20);
            _txtObs.Width = 350;
            Controls.Add(lblObs);
            Controls.Add(_txtObs);
            y += 90;

            var btnSalvar = new Button { Text = "Salvar", Location = new Point(20, y), Width = 160, Height = 32 };
            btnSalvar.Click += BtnSalvar_Click;
            var btnCancelar = new Button { Text = "Cancelar", Location = new Point(210, y), Width = 160, Height = 32 };
            btnCancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnSalvar);
            Controls.Add(btnCancelar);

            if (_vagaoEditando != null)
            {
                _txtCodigo.Text = _vagaoEditando.CodigoVagao;
                _txtTipo.Text = _vagaoEditando.TipoVagao;
                _cmbStatus.SelectedItem = _vagaoEditando.StatusOperacao;
                _txtObs.Text = _vagaoEditando.Observacoes;
                if (_vagaoEditando.DataUltimaManutencao.HasValue)
                {
                    _chkTemManutencao.Checked = true;
                    _dtManutencao.Value = _vagaoEditando.DataUltimaManutencao.Value;
                }
            }
        }

        private void BtnSalvar_Click(object? sender, EventArgs e)
        {
            var codigo = _txtCodigo.Text.Trim();
            var tipo = _txtTipo.Text.Trim();

            if (codigo.Length == 0 || tipo.Length == 0)
            {
                MessageBox.Show("Preencha código e tipo do vagão.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            object dataManutencao = _chkTemManutencao.Checked
                ? _dtManutencao.Value.Date
                : DBNull.Value;

            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();

                if (_vagaoEditando == null)
                {
                    const string sql = @"
                        INSERT INTO vagoes (codigo_vagao, tipo_vagao, status_operacao, data_ultima_manutencao, observacoes)
                        VALUES (@codigo, @tipo, @status, @data, @obs)";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("codigo", codigo);
                    cmd.Parameters.AddWithValue("tipo", tipo);
                    cmd.Parameters.AddWithValue("status", _cmbStatus.SelectedItem?.ToString() ?? "operando");
                    cmd.Parameters.AddWithValue("data", dataManutencao);
                    cmd.Parameters.AddWithValue("obs", (object?)_txtObs.Text.Trim() ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    const string sql = @"
                        UPDATE vagoes
                        SET codigo_vagao = @codigo, tipo_vagao = @tipo, status_operacao = @status,
                            data_ultima_manutencao = @data, observacoes = @obs
                        WHERE id_vagao = @id";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("codigo", codigo);
                    cmd.Parameters.AddWithValue("tipo", tipo);
                    cmd.Parameters.AddWithValue("status", _cmbStatus.SelectedItem?.ToString() ?? "operando");
                    cmd.Parameters.AddWithValue("data", dataManutencao);
                    cmd.Parameters.AddWithValue("obs", (object?)_txtObs.Text.Trim() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("id", _vagaoEditando.IdVagao);
                    cmd.ExecuteNonQuery();
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (PostgresException pex) when (pex.SqlState == "23505")
            {
                MessageBox.Show("Já existe um vagão cadastrado com este código.", "Código duplicado",
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
