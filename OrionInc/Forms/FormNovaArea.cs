using Npgsql;
using OrionInc.Models;

namespace OrionInc.Forms
{
    public class FormNovaArea : Form
    {
        private readonly AreaRisco? _areaEditando;

        private readonly TextBox _txtNome = new();
        private readonly TextBox _txtDescricao = new() { Multiline = true, Height = 60 };
        private readonly ComboBox _cmbNivel = new();
        private readonly NumericUpDown _numRaio = new();
        private readonly CheckBox _chkAtiva = new();

        public FormNovaArea(AreaRisco? area)
        {
            _areaEditando = area;

            Text = area == null ? "Nova Área de Risco" : "Editar Área de Risco";
            Width = 420
                ;
            Height = 420;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int y = 20;

            var lblNome = new Label { Text = "Nome da área:", Location = new Point(20, y), AutoSize = true };
            _txtNome.Location = new Point(20, y + 20);
            _txtNome.Width = 350;
            Controls.Add(lblNome);
            Controls.Add(_txtNome);
            y += 55;

            var lblDesc = new Label { Text = "Descrição:", Location = new Point(20, y), AutoSize = true };
            _txtDescricao.Location = new Point(20, y + 20);
            _txtDescricao.Width = 350;
            Controls.Add(lblDesc);
            Controls.Add(_txtDescricao);
            y += 85;

            var lblNivel = new Label { Text = "Nível de risco:", Location = new Point(20, y), AutoSize = true };
            _cmbNivel.Location = new Point(20, y + 20);
            _cmbNivel.Width = 160;
            _cmbNivel.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbNivel.Items.AddRange(new object[] { "baixo", "médio", "alto", "crítico" });
            _cmbNivel.SelectedIndex = 0;
            Controls.Add(lblNivel);
            Controls.Add(_cmbNivel);

            var lblRaio = new Label { Text = "Raio (m):", Location = new Point(210, y), AutoSize = true };
            _numRaio.Location = new Point(210, y + 20);
            _numRaio.Width = 160;
            _numRaio.Minimum = 1;
            _numRaio.Maximum = 100000;
            _numRaio.Value = 50;
            Controls.Add(lblRaio);
            Controls.Add(_numRaio);
            y += 55;

            _chkAtiva.Text = "Área ativa (em operação)";
            _chkAtiva.Location = new Point(20, y);
            _chkAtiva.Checked = true;
            _chkAtiva.AutoSize = true;
            Controls.Add(_chkAtiva);
            y += 40;

            var btnSalvar = new Button { Text = "Salvar", Location = new Point(20, y), Width = 160, Height = 32 };
            btnSalvar.Click += BtnSalvar_Click;
            var btnCancelar = new Button { Text = "Cancelar", Location = new Point(210, y), Width = 160, Height = 32 };
            btnCancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnSalvar);
            Controls.Add(btnCancelar);

            if (_areaEditando != null)
            {
                _txtNome.Text = _areaEditando.NomeArea;
                _txtDescricao.Text = _areaEditando.DescricaoArea;
                _cmbNivel.SelectedItem = _areaEditando.NivelRisco;
                _numRaio.Value = _areaEditando.RaioMonitoramento;
                _chkAtiva.Checked = _areaEditando.AreaAtiva;
            }
        }

        private void BtnSalvar_Click(object? sender, EventArgs e)
        {
            var nome = _txtNome.Text.Trim();
            if (nome.Length == 0)
            {
                MessageBox.Show("Informe o nome da área.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();

                if (_areaEditando == null)
                {
                    const string sql = @"
                        INSERT INTO areas_risco (nome_area, descricao_area, nivel_risco, raio_monitoramento, area_ativa)
                        VALUES (@nome, @desc, @nivel, @raio, @ativa)";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("nome", nome);
                    cmd.Parameters.AddWithValue("desc", (object?)_txtDescricao.Text.Trim() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("nivel", _cmbNivel.SelectedItem?.ToString() ?? "baixo");
                    cmd.Parameters.AddWithValue("raio", (int)_numRaio.Value);
                    cmd.Parameters.AddWithValue("ativa", _chkAtiva.Checked);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    const string sql = @"
                        UPDATE areas_risco
                        SET nome_area = @nome, descricao_area = @desc, nivel_risco = @nivel,
                            raio_monitoramento = @raio, area_ativa = @ativa
                        WHERE id_area = @id";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("nome", nome);
                    cmd.Parameters.AddWithValue("desc", (object?)_txtDescricao.Text.Trim() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("nivel", _cmbNivel.SelectedItem?.ToString() ?? "baixo");
                    cmd.Parameters.AddWithValue("raio", (int)_numRaio.Value);
                    cmd.Parameters.AddWithValue("ativa", _chkAtiva.Checked);
                    cmd.Parameters.AddWithValue("id", _areaEditando.IdArea);
                    cmd.ExecuteNonQuery();
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
