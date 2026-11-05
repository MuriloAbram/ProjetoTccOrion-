using System.Data;
using Npgsql;
using OrionInc.Data;

namespace OrionInc.Forms
{
    public class FormAlertas : Form
    {
        private readonly DataGridView _grid = new();
        private readonly Label _lblContador = new();
        private readonly Button _btnResolver = new();

        public FormAlertas()
        {
            Text = "Alertas Ativos";
            Width = 1000;
            Height = 580;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(800, 450);

            var btnAtualizar = new Button
            {
                Text = "Atualizar",
                Location = new Point(15, 12),
                Width = 100
            };

            btnAtualizar.Click += (_, _) => Recarregar();

            _btnResolver.Text = "Marcar como resolvido";
            _btnResolver.Location = new Point(125, 12);
            _btnResolver.Width = 180;

            _btnResolver.Click += (_, _) => ResolverSelecionado();

            // Regra provisória: administrador e operador podem
            // resolver alertas; o técnico não pode.
            _btnResolver.Enabled = PodeResolverAlertas();

            _lblContador.Location = new Point(320, 18);
            _lblContador.AutoSize = true;
            _lblContador.Font = new Font(
                "Segoe UI",
                9,
                FontStyle.Bold);

            _grid.Location = new Point(15, 50);
            _grid.Size = new Size(960, 480);
            _grid.Anchor =
                AnchorStyles.Top |
                AnchorStyles.Bottom |
                AnchorStyles.Left |
                AnchorStyles.Right;

            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode =
                DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.Fill;

            _grid.CellFormatting += Grid_CellFormatting;

            Controls.AddRange(new Control[]
            {
                btnAtualizar,
                _btnResolver,
                _lblContador,
                _grid
            });

            OrionUi.StyleModuleControls(this);

            Load += (_, _) => Recarregar();
        }

        private static bool PodeResolverAlertas()
        {
            string nivel = (Sessao.NivelAcesso ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

            return nivel == "administrador" ||
                   nivel == "operador";
        }

        private void Grid_CellFormatting(
            object? sender,
            DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (_grid.Columns[e.ColumnIndex].Name != "Nível")
                return;

            var valor = e.Value?.ToString()?.Trim().ToLowerInvariant();

            e.CellStyle!.BackColor = valor switch
            {
                "crítico" or "critico" =>
                    Color.FromArgb(255, 205, 205),

                "atencao" or "atenção" =>
                    Color.FromArgb(255, 240, 200),

                _ => e.CellStyle.BackColor
            };
        }

        private void Recarregar()
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();

                const string sql = """
                    SELECT
                        id_registro AS "ID",
                        data_hora_evento AS "Data/Hora",
                        tipo_evento AS "Tipo",
                        nivel_alerta AS "Nível",
                        descricao_evento AS "Descrição",
                        nome_funcionario AS "Funcionário",
                        nome_area AS "Área",
                        codigo_vagao AS "Vagão",
                        tipo_sensor AS "Sensor"
                    FROM public.vw_alertas_ativos
                    ORDER BY data_hora_evento DESC;
                    """;

                using var adapter = new NpgsqlDataAdapter(sql, conn);

                var dt = new DataTable();
                adapter.Fill(dt);

                _grid.DataSource = dt;

                if (_grid.Columns["ID"] != null)
                    _grid.Columns["ID"]!.Visible = false;

                _lblContador.Text =
                    $"{dt.Rows.Count} alerta(s) em aberto";

                _lblContador.ForeColor = dt.Rows.Count > 0
                    ? Color.DarkRed
                    : Color.DarkGreen;

                _btnResolver.Enabled = PodeResolverAlertas();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Erro ao carregar alertas:\n\n" + ex.Message,
                    "Erro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ResolverSelecionado()
        {
            // Validação novamente no momento da operação.
            // Desabilitar o botão sozinho não é suficiente.
            if (!PodeResolverAlertas())
            {
                MessageBox.Show(
                    this,
                    "Seu perfil não tem permissão para resolver alertas.",
                    "Acesso negado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (_grid.CurrentRow == null)
            {
                MessageBox.Show(
                    this,
                    "Selecione um alerta na lista.",
                    "Atenção",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            var valorId = _grid.CurrentRow.Cells["ID"].Value;

            if (valorId == null || valorId == DBNull.Value)
            {
                MessageBox.Show(
                    this,
                    "Não foi possível identificar o alerta selecionado.",
                    "Erro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }

            int id = Convert.ToInt32(valorId);

            if (MessageBox.Show(
                    this,
                    "Confirma que este evento foi resolvido?",
                    "Confirmar",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var conn = DatabaseHelper.GetConnection();

                const string sql = """
                    UPDATE public.registros_sistema
                    SET
                        evento_resolvido = TRUE,
                        id_usuario = @idUsuario
                    WHERE id_registro = @id
                      AND evento_resolvido IS DISTINCT FROM TRUE;
                    """;

                using var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue(
                    "idUsuario",
                    Sessao.IdUsuario);

                cmd.Parameters.AddWithValue("id", id);

                int afetadas = cmd.ExecuteNonQuery();

                if (afetadas == 0)
                {
                    MessageBox.Show(
                        this,
                        "O alerta já foi resolvido ou não existe mais.",
                        "Informação",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                Recarregar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Erro ao resolver alerta:\n\n" + ex.Message,
                    "Erro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
