using System.Data;
using Npgsql;
using OrionInc.Models;

namespace OrionInc.Forms
{
    public class FormFuncionarios : Form
    {
        private readonly DataGridView _grid = new();
        private readonly TextBox _txtBusca = new();

        public FormFuncionarios()
        {
            Text = "Funcionários";
            Width = 900;
            Height = 560;
            FormBorderStyle = FormBorderStyle.Sizable;

            var lblBusca = new Label { Text = "Buscar por nome:", Location = new Point(15, 15), AutoSize = true };
            _txtBusca.Location = new Point(130, 12);
            _txtBusca.Width = 250;
            _txtBusca.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Recarregar(); };

            var btnBuscar = new Button { Text = "Buscar", Location = new Point(390, 10), Width = 90 };
            btnBuscar.Click += (_, _) => Recarregar();

            var btnNovo = new Button { Text = "Novo", Location = new Point(500, 10), Width = 90 };
            btnNovo.Click += (_, _) => AbrirFormulario(null);

            var btnEditar = new Button { Text = "Editar", Location = new Point(600, 10), Width = 90 };
            btnEditar.Click += (_, _) => EditarSelecionado();

            var btnExcluir = new Button { Text = "Excluir", Location = new Point(700, 10), Width = 90 };
            btnExcluir.Click += (_, _) => ExcluirSelecionado();

            _grid.Location = new Point(15, 50);
            _grid.Size = new Size(860, 460);
            _grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.DoubleClick += (_, _) => EditarSelecionado();

            Controls.AddRange(new Control[] { lblBusca, _txtBusca, btnBuscar, btnNovo, btnEditar, btnExcluir, _grid });

            Load += (_, _) => Recarregar();
        }

        private void Recarregar()
        {
            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();
                const string sqlBase = @"
                    SELECT id_funcionario AS ""ID"", nome_funcionario AS ""Nome"", cpf AS ""CPF"",
                           cargo AS ""Cargo"", setor AS ""Setor"",
                           status_funcionario AS ""Status"", data_cadastro AS ""Cadastrado em""
                    FROM funcionarios";

                var filtro = _txtBusca.Text.Trim();
                var sql = string.IsNullOrEmpty(filtro)
                    ? sqlBase + " ORDER BY nome_funcionario"
                    : sqlBase + " WHERE nome_funcionario ILIKE @filtro ORDER BY nome_funcionario";

                using var cmd = new NpgsqlCommand(sql, conn);
                if (!string.IsNullOrEmpty(filtro))
                    cmd.Parameters.AddWithValue("filtro", $"%{filtro}%");

                using var adapter = new NpgsqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                _grid.DataSource = dt;

                if (_grid.Columns["ID"] != null)
                    _grid.Columns["ID"]!.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar funcionários: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Funcionario? ObterSelecionado()
        {
            if (_grid.CurrentRow == null) return null;
            var row = _grid.CurrentRow;
            return new Funcionario
            {
                IdFuncionario = Convert.ToInt32(row.Cells["ID"].Value),
                NomeFuncionario = row.Cells["Nome"].Value?.ToString() ?? "",
                Cpf = row.Cells["CPF"].Value?.ToString() ?? "",
                Cargo = row.Cells["Cargo"].Value?.ToString() ?? "",
                Setor = row.Cells["Setor"].Value?.ToString() ?? "",
                StatusFuncionario = row.Cells["Status"].Value?.ToString() ?? "ativo"
            };
        }

        private void AbrirFormulario(Funcionario? funcionario)
        {
            using var form = new FormNovoFuncionario(funcionario);
            if (form.ShowDialog() == DialogResult.OK)
                Recarregar();
        }

        private void EditarSelecionado()
        {
            var f = ObterSelecionado();
            if (f == null)
            {
                MessageBox.Show("Selecione um funcionário na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            AbrirFormulario(f);
        }

        private void ExcluirSelecionado()
        {
            var f = ObterSelecionado();
            if (f == null)
            {
                MessageBox.Show("Selecione um funcionário na lista.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirmacao = MessageBox.Show(
                $"Confirma a exclusão de \"{f.NomeFuncionario}\"?",
                "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirmacao != DialogResult.Yes) return;

            try
            {
                using var conn = Data.DatabaseHelper.GetConnection();
                using var cmd = new NpgsqlCommand(
                    "DELETE FROM funcionarios WHERE id_funcionario = @id", conn);
                cmd.Parameters.AddWithValue("id", f.IdFuncionario);
                cmd.ExecuteNonQuery();
                Recarregar();
            }
            catch (PostgresException pex) when (pex.SqlState == "23503")
            {
                MessageBox.Show(
                    "Não é possível excluir: este funcionário possui registros vinculados " +
                    "(dispositivo, eventos, etc.). Remova os vínculos primeiro.",
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
