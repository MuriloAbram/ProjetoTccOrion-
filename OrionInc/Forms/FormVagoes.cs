using System.Data;
using Npgsql;
using OrionInc.Models;

namespace OrionInc.Forms
{
    public class FormVagoes : Form
    {
        private readonly DataGridView _grid = new();

        public FormVagoes()
        {
            Text = "Vagões";
            Width = 900;
            Height = 560;

            var btnNovo = new Button { Text = "Novo vagão", Location = new Point(15, 12), Width = 110 };
            btnNovo.Click += (_, _) => AbrirFormulario(null);

            var btnEditar = new Button { Text = "Editar", Location = new Point(135, 12), Width = 110 };
            btnEditar.Click += (_, _) => EditarSelecionado();

            var btnExcluir = new Button { Text = "Excluir", Location = new Point(255, 12), Width = 110 };
            btnExcluir.Click += (_, _) => ExcluirSelecionado();

            var btnAtualizar = new Button { Text = "Atualizar lista", Location = new Point(375, 12), Width = 120 };
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
                const string sql = @"
                    SELECT id_vagao AS ""ID"", codigo_vagao AS ""Código"", tipo_vagao AS ""Tipo"",
                           status_operacao AS ""Status"", data_ultima_manutencao AS ""Última manutenção"",
                           observacoes AS ""Observações""
                    FROM vagoes
                    ORDER BY codigo_vagao";
                using var adapter = new NpgsqlDataAdapter(sql, conn);
                var dt = new DataTable();
                adapter.Fill(dt);
                _grid.DataSource = dt;
                if (_grid.Columns["ID"] != null) _grid.Columns["ID"]!.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar vagões: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Vagao? ObterSelecionado()
        {
            if (_grid.CurrentRow == null) return null;
            var row = _grid.CurrentRow;
            return new Vagao
            {
                IdVagao = Convert.ToInt32(row.Cells["ID"].Value),
                CodigoVagao = row.Cells["Código"].Value?.ToString() ?? "",
                TipoVagao = row.Cells["Tipo"].Value?.ToString() ?? "",
                StatusOperacao = row.Cells["Status"].Value?.ToString() ?? "operando",
                DataUltimaManutencao = row.Cells["Última manutenção"].Value as DateTime?,
                Observacoes = row.Cells["Observações"].Value as string
            };
        }

        private void AbrirFormulario(Vagao? vagao)
        {
            using var form = new FormNovoVagao(vagao);
            if (form.ShowDialog() == DialogResult.OK)
                Recarregar();
        }

        private void EditarSelecionado()
        {
            var v = ObterSelecionado();
            if (v == null)
            {
                MessageBox.Show("Selecione um vagão na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            AbrirFormulario(v);
        }

        private void ExcluirSelecionado()
        {
            var v = ObterSelecionado();
            if (v == null)
            {
                MessageBox.Show("Selecione um vagão na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show($"Confirma a exclusão do vagão \"{v.CodigoVagao}\"?", "Confirmar exclusão",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();
                using var cmd = new NpgsqlCommand("DELETE FROM vagoes WHERE id_vagao = @id", conn);
                cmd.Parameters.AddWithValue("id", v.IdVagao);
                cmd.ExecuteNonQuery();
                Recarregar();
            }
            catch (PostgresException pex) when (pex.SqlState == "23503")
            {
                MessageBox.Show("Não é possível excluir: existem sensores ou eventos vinculados a este vagão.",
                    "Exclusão bloqueada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao excluir: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
