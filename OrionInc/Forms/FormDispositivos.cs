using System.Data;
using Npgsql;
using OrionInc.Models;

namespace OrionInc.Forms
{
    public class FormDispositivos : Form
    {
        private readonly DataGridView _grid = new();

        public FormDispositivos()
        {
            Text = "Dispositivos (EPI eletrônico)";
            Width = 950;
            Height = 560;

            var btnNovo = new Button { Text = "Novo dispositivo", Location = new Point(15, 12), Width = 140 };
            btnNovo.Click += (_, _) => AbrirFormulario(null);

            var btnEditar = new Button { Text = "Editar", Location = new Point(165, 12), Width = 110 };
            btnEditar.Click += (_, _) => EditarSelecionado();

            var btnExcluir = new Button { Text = "Excluir", Location = new Point(285, 12), Width = 110 };
            btnExcluir.Click += (_, _) => ExcluirSelecionado();

            var btnAtualizar = new Button { Text = "Atualizar lista", Location = new Point(405, 12), Width = 120 };
            btnAtualizar.Click += (_, _) => Recarregar();

            _grid.Location = new Point(15, 50);
            _grid.Size = new Size(910, 460);
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
                // Usa a view vw_funcionarios_dispositivos, mas filtrando apenas quem tem dispositivo
                const string sql = @"
                    SELECT d.id_dispositivo AS ""ID"", d.codigo_dispositivo AS ""Código"",
                           d.tipo_dispositivo AS ""Tipo"", d.status_dispositivo AS ""Status"",
                           d.id_funcionario AS ""IDFuncionario"",
                           f.nome_funcionario AS ""Funcionário vinculado""
                    FROM dispositivos d
                    LEFT JOIN funcionarios f ON f.id_funcionario = d.id_funcionario
                    ORDER BY d.codigo_dispositivo";
                using var adapter = new NpgsqlDataAdapter(sql, conn);
                var dt = new DataTable();
                adapter.Fill(dt);
                _grid.DataSource = dt;
                if (_grid.Columns["ID"] != null) _grid.Columns["ID"]!.Visible = false;
                if (_grid.Columns["IDFuncionario"] != null) _grid.Columns["IDFuncionario"]!.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar dispositivos: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Dispositivo? ObterSelecionado()
        {
            if (_grid.CurrentRow == null) return null;
            var row = _grid.CurrentRow;
            return new Dispositivo
            {
                IdDispositivo = Convert.ToInt32(row.Cells["ID"].Value),
                CodigoDispositivo = row.Cells["Código"].Value?.ToString() ?? "",
                TipoDispositivo = row.Cells["Tipo"].Value?.ToString() ?? "",
                StatusDispositivo = row.Cells["Status"].Value?.ToString() ?? "disponivel",
                IdFuncionario = row.Cells["IDFuncionario"].Value is DBNull
                    ? null
                    : Convert.ToInt32(row.Cells["IDFuncionario"].Value)
            };
        }

        private void AbrirFormulario(Dispositivo? dispositivo)
        {
            using var form = new FormCadastroDispositivo(dispositivo);
            if (form.ShowDialog() == DialogResult.OK)
                Recarregar();
        }

        private void EditarSelecionado()
        {
            var d = ObterSelecionado();
            if (d == null)
            {
                MessageBox.Show("Selecione um dispositivo na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            AbrirFormulario(d);
        }

        private void ExcluirSelecionado()
        {
            var d = ObterSelecionado();
            if (d == null)
            {
                MessageBox.Show("Selecione um dispositivo na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show($"Confirma a exclusão do dispositivo \"{d.CodigoDispositivo}\"?",
                    "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();
                using var cmd = new NpgsqlCommand("DELETE FROM dispositivos WHERE id_dispositivo = @id", conn);
                cmd.Parameters.AddWithValue("id", d.IdDispositivo);
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

    /// <summary>Formulário de cadastro/edição de um dispositivo, com combo de funcionário.</summary>
    public class FormCadastroDispositivo : Form
    {
        private readonly Dispositivo? _dispositivoEditando;

        private readonly TextBox _txtCodigo = new();
        private readonly ComboBox _cmbTipo = new();
        private readonly ComboBox _cmbStatus = new();
        private readonly ComboBox _cmbFuncionario = new();

        public FormCadastroDispositivo(Dispositivo? dispositivo)
        {
            _dispositivoEditando = dispositivo;

            Text = dispositivo == null ? "Novo Dispositivo" : "Editar Dispositivo";
            Width = 420;
            Height = 360;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int y = 20;

            var lblCodigo = new Label { Text = "Código / Tag RFID:", Location = new Point(20, y), AutoSize = true };
            _txtCodigo.Location = new Point(20, y + 20);
            _txtCodigo.Width = 350;
            Controls.Add(lblCodigo);
            Controls.Add(_txtCodigo);
            y += 55;

            var lblTipo = new Label { Text = "Tipo de dispositivo:", Location = new Point(20, y), AutoSize = true };
            _cmbTipo.Location = new Point(20, y + 20);
            _cmbTipo.Width = 350;
            _cmbTipo.Items.AddRange(new object[] { "Capacete Smart", "Pulseira", "Colete", "Crachá RFID" });
            Controls.Add(lblTipo);
            Controls.Add(_cmbTipo);
            y += 55;

            var lblStatus = new Label { Text = "Status:", Location = new Point(20, y), AutoSize = true };
            _cmbStatus.Location = new Point(20, y + 20);
            _cmbStatus.Width = 350;
            _cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbStatus.Items.AddRange(new object[] { "disponivel", "em_uso", "defeito" });
            _cmbStatus.SelectedIndex = 0;
            Controls.Add(lblStatus);
            Controls.Add(_cmbStatus);
            y += 55;

            var lblFuncionario = new Label { Text = "Funcionário vinculado:", Location = new Point(20, y), AutoSize = true };
            _cmbFuncionario.Location = new Point(20, y + 20);
            _cmbFuncionario.Width = 350;
            _cmbFuncionario.DropDownStyle = ComboBoxStyle.DropDownList;
            Controls.Add(lblFuncionario);
            Controls.Add(_cmbFuncionario);
            y += 55;

            var btnSalvar = new Button { Text = "Salvar", Location = new Point(20, y), Width = 160, Height = 32 };
            btnSalvar.Click += BtnSalvar_Click;
            var btnCancelar = new Button { Text = "Cancelar", Location = new Point(210, y), Width = 160, Height = 32 };
            btnCancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnSalvar);
            Controls.Add(btnCancelar);

            Load += (_, _) => CarregarFuncionarios();
        }

        private void CarregarFuncionarios()
        {
            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();
                const string sql = @"
                    SELECT id_funcionario, nome_funcionario
                    FROM funcionarios
                    WHERE status_funcionario = 'ativo'
                    ORDER BY nome_funcionario";
                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                _cmbFuncionario.Items.Add(new ItemCombo(null, "(Nenhum)"));
                while (reader.Read())
                {
                    _cmbFuncionario.Items.Add(new ItemCombo(
                        reader.GetInt32(0), reader.GetString(1)));
                }
                _cmbFuncionario.DisplayMember = "Texto";
                _cmbFuncionario.SelectedIndex = 0;

                if (_dispositivoEditando != null)
                {
                    _txtCodigo.Text = _dispositivoEditando.CodigoDispositivo;
                    if (_cmbTipo.Items.Contains(_dispositivoEditando.TipoDispositivo))
                        _cmbTipo.SelectedItem = _dispositivoEditando.TipoDispositivo;
                    else
                        _cmbTipo.Text = _dispositivoEditando.TipoDispositivo;
                    _cmbStatus.SelectedItem = _dispositivoEditando.StatusDispositivo;

                    if (_dispositivoEditando.IdFuncionario.HasValue)
                    {
                        foreach (var item in _cmbFuncionario.Items)
                        {
                            if (item is ItemCombo ic && ic.Id == _dispositivoEditando.IdFuncionario)
                            {
                                _cmbFuncionario.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar funcionários: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSalvar_Click(object? sender, EventArgs e)
        {
            var codigo = _txtCodigo.Text.Trim();
            var tipo = _cmbTipo.Text.Trim();

            if (codigo.Length == 0 || tipo.Length == 0)
            {
                MessageBox.Show("Preencha o código e o tipo do dispositivo.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var funcionarioSelecionado = _cmbFuncionario.SelectedItem as ItemCombo;
            object idFuncionario = funcionarioSelecionado?.Id is int id ? id : DBNull.Value;

            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();

                if (_dispositivoEditando == null)
                {
                    const string sql = @"
                        INSERT INTO dispositivos (codigo_dispositivo, tipo_dispositivo, status_dispositivo, id_funcionario)
                        VALUES (@codigo, @tipo, @status, @idFunc)";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("codigo", codigo);
                    cmd.Parameters.AddWithValue("tipo", tipo);
                    cmd.Parameters.AddWithValue("status", _cmbStatus.SelectedItem?.ToString() ?? "disponivel");
                    cmd.Parameters.AddWithValue("idFunc", idFuncionario);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    const string sql = @"
                        UPDATE dispositivos
                        SET codigo_dispositivo = @codigo, tipo_dispositivo = @tipo,
                            status_dispositivo = @status, id_funcionario = @idFunc
                        WHERE id_dispositivo = @id";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("codigo", codigo);
                    cmd.Parameters.AddWithValue("tipo", tipo);
                    cmd.Parameters.AddWithValue("status", _cmbStatus.SelectedItem?.ToString() ?? "disponivel");
                    cmd.Parameters.AddWithValue("idFunc", idFuncionario);
                    cmd.Parameters.AddWithValue("id", _dispositivoEditando.IdDispositivo);
                    cmd.ExecuteNonQuery();
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (PostgresException pex) when (pex.SqlState == "P0001")
            {
                // RAISE EXCEPTION do trigger tg_validar_dispositivo (funcionário já tem dispositivo)
                MessageBox.Show(pex.MessageText, "Vínculo já existe",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (PostgresException pex) when (pex.SqlState == "23505")
            {
                MessageBox.Show("Já existe um dispositivo cadastrado com este código.", "Código duplicado",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private sealed class ItemCombo
        {
            public int? Id { get; }
            public string Texto { get; }
            public ItemCombo(int? id, string texto) { Id = id; Texto = texto; }
            public override string ToString() => Texto;
        }
    }
}
