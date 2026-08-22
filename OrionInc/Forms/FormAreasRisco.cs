using System.Data;
using Npgsql;
using OrionInc.Models;

namespace OrionInc.Forms
{
    public class FormAreasRisco : Form
    {
        private readonly DataGridView _grid = new();

        public FormAreasRisco()
        {
            Text = "Áreas de Risco";
            Width = 900;
            Height = 560;

            var btnNovo = new Button { Text = "Nova área", Location = new Point(15, 12), Width = 110 };
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
                    SELECT id_area AS ""ID"", nome_area AS ""Nome"", nivel_risco AS ""Nível de risco"",
                           raio_monitoramento AS ""Raio (m)"", area_ativa AS ""Ativa"",
                           descricao_area AS ""Descrição""
                    FROM areas_risco
                    ORDER BY nome_area";
                using var adapter = new NpgsqlDataAdapter(sql, conn);
                var dt = new DataTable();
                adapter.Fill(dt);
                _grid.DataSource = dt;
                if (_grid.Columns["ID"] != null) _grid.Columns["ID"]!.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar áreas de risco: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private AreaRisco? ObterSelecionado()
        {
            if (_grid.CurrentRow == null) return null;
            var row = _grid.CurrentRow;
            return new AreaRisco
            {
                IdArea = Convert.ToInt32(row.Cells["ID"].Value),
                NomeArea = row.Cells["Nome"].Value?.ToString() ?? "",
                NivelRisco = row.Cells["Nível de risco"].Value?.ToString() ?? "baixo",
                RaioMonitoramento = Convert.ToInt32(row.Cells["Raio (m)"].Value),
                AreaAtiva = Convert.ToBoolean(row.Cells["Ativa"].Value),
                DescricaoArea = row.Cells["Descrição"].Value as string
            };
        }

        private void AbrirFormulario(AreaRisco? area)
        {
            using var form = new FormNovaArea(area);
            if (form.ShowDialog() == DialogResult.OK)
                Recarregar();
        }

        private void EditarSelecionado()
        {
            var a = ObterSelecionado();
            if (a == null)
            {
                MessageBox.Show("Selecione uma área na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            AbrirFormulario(a);
        }

        private void ExcluirSelecionado()
        {
            var a = ObterSelecionado();
            if (a == null)
            {
                MessageBox.Show("Selecione uma área na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show($"Confirma a exclusão de \"{a.NomeArea}\"?", "Confirmar exclusão",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();
                using var cmd = new NpgsqlCommand("DELETE FROM areas_risco WHERE id_area = @id", conn);
                cmd.Parameters.AddWithValue("id", a.IdArea);
                cmd.ExecuteNonQuery();
                Recarregar();
            }
            catch (PostgresException pex) when (pex.SqlState == "23503")
            {
                MessageBox.Show("Não é possível excluir: existem sensores ou eventos vinculados a esta área.",
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
