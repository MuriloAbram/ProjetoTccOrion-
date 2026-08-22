using System.Data;
using Npgsql;
using OrionInc.Data;

namespace OrionInc.Forms
{
    public class FormAlertas : Form
    {
        private readonly DataGridView _grid = new();
        private readonly Label _lblContador = new();

        public FormAlertas()
        {
            Text = "Alertas Ativos";
            Width = 1000;
            Height = 580;

            var btnAtualizar = new Button { Text = "Atualizar", Location = new Point(15, 12), Width = 100 };
            btnAtualizar.Click += (_, _) => Recarregar();

            var btnResolver = new Button { Text = "Marcar como resolvido", Location = new Point(125, 12), Width = 180 };
            btnResolver.Click += (_, _) => ResolverSelecionado();

            _lblContador.Location = new Point(320, 18);
            _lblContador.AutoSize = true;
            _lblContador.Font = new Font("Segoe UI", 9, FontStyle.Bold);

            _grid.Location = new Point(15, 50);
            _grid.Size = new Size(960, 480);
            _grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellFormatting += Grid_CellFormatting;

            Controls.AddRange(new Control[] { btnAtualizar, btnResolver, _lblContador, _grid });
            Load += (_, _) => Recarregar();
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].Name != "Nível") return;

            var valor = e.Value?.ToString();
            e.CellStyle!.BackColor = valor switch
            {
                "crítico" => Color.FromArgb(255, 205, 205),
                "atencao" or "atenção" => Color.FromArgb(255, 240, 200),
                _ => e.CellStyle.BackColor
            };
        }

        private void Recarregar()
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                const string sql = @"
                    SELECT id_registro AS ""ID"", data_hora_evento AS ""Data/Hora"",
                           tipo_evento AS ""Tipo"", nivel_alerta AS ""Nível"",
                           descricao_evento AS ""Descrição"",
                           nome_funcionario AS ""Funcionário"", nome_area AS ""Área"",
                           codigo_vagao AS ""Vagão"", tipo_sensor AS ""Sensor""
                    FROM vw_alertas_ativos";
                using var adapter = new NpgsqlDataAdapter(sql, conn);
                var dt = new DataTable();
                adapter.Fill(dt);
                _grid.DataSource = dt;
                if (_grid.Columns["ID"] != null) _grid.Columns["ID"]!.Visible = false;

                _lblContador.Text = $"{dt.Rows.Count} alerta(s) em aberto";
                _lblContador.ForeColor = dt.Rows.Count > 0 ? Color.DarkRed : Color.DarkGreen;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar alertas: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResolverSelecionado()
        {
            if (_grid.CurrentRow == null)
            {
                MessageBox.Show("Selecione um alerta na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var id = Convert.ToInt32(_grid.CurrentRow.Cells["ID"].Value);

            if (MessageBox.Show("Confirma que este evento foi resolvido?", "Confirmar",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                using var conn = DatabaseHelper.GetConnection();
                const string sql = @"
                    UPDATE registros_sistema
                    SET evento_resolvido = TRUE, id_usuario = @idUsuario
                    WHERE id_registro = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("idUsuario", Sessao.IdUsuario);
                cmd.Parameters.AddWithValue("id", id);
                cmd.ExecuteNonQuery();
                Recarregar();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao resolver alerta: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
