using System.Data;
using Npgsql;
using OrionInc.Data;

namespace OrionInc.Forms;

public sealed class FormSensores : Form
{
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        BackgroundColor = Color.White,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };

    private readonly TextBox _search = new()
    {
        Width = 250,
        PlaceholderText = "ID, tipo ou modelo..."
    };

    private readonly ComboBox _status = new()
    {
        Width = 150,
        DropDownStyle = ComboBoxStyle.DropDownList
    };

    private readonly Label _lblAtualizacao = new()
    {
        AutoSize = true,
        Text = "Atualização automática: ativa",
        Padding = new Padding(8, 7, 0, 0),
        ForeColor = Color.FromArgb(80, 90, 100)
    };

    private readonly System.Windows.Forms.Timer _refreshTimer = new()
    {
        Interval = 3000
    };

    public FormSensores()
    {
        Text = "Orion Inc | Sensores";
        Width = 1100;
        Height = 650;
        StartPosition = FormStartPosition.CenterParent;

        // ============================================================
        // BARRA SUPERIOR
        // ============================================================

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(243, 246, 250),
            WrapContents = false,
            AutoScroll = true
        };

        _status.Items.AddRange(new object[]
        {
            "Todos",
            "online",
            "offline",
            "falha",
            "manutencao"
        });

        _status.SelectedIndex = 0;

        // ============================================================
        // BOTÃO ATUALIZAR
        // ============================================================

        var refresh = new Button
        {
            Text = "Atualizar",
            Width = 85,
            Height = 28
        };

        refresh.Click += (_, _) => LoadSensors();

        // ============================================================
        // NOVO SENSOR
        // ============================================================

        var add = new Button
        {
            Text = "Novo sensor",
            Width = 95,
            Height = 28
        };

        add.Click += (_, _) =>
        {
            using var form = new FormSensorEditor();

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                LoadSensors();
            }
        };

        // ============================================================
        // EDITAR
        // ============================================================

        var edit = new Button
        {
            Text = "Editar",
            Width = 70,
            Height = 28
        };

        edit.Click += (_, _) =>
        {
            var id = SelectedId();

            if (id.HasValue)
            {
                using var form = new FormSensorEditor(id.Value);

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadSensors();
                }
            }
        };

        // ============================================================
        // EXCLUIR
        // ============================================================

        var delete = new Button
        {
            Text = "Excluir",
            Width = 70,
            Height = 28
        };

        delete.Click += (_, _) => DeleteSelected();

        // ============================================================
        // HISTÓRICO
        // ============================================================

        var history = new Button
        {
            Text = "Leituras",
            Width = 75,
            Height = 28
        };

        history.Click += (_, _) =>
        {
            var id = SelectedId();

            if (id.HasValue)
            {
                using var form = new FormHistoricoLeituras(id.Value);
                form.ShowDialog(this);
            }
        };

        // ============================================================
        // CONTROLES DA BARRA
        // ============================================================

        top.Controls.AddRange(new Control[]
        {
            new Label
            {
                Text = "Buscar",
                AutoSize = true,
                Padding = new Padding(0, 7, 0, 0)
            },

            _search,

            new Label
            {
                Text = "Status",
                AutoSize = true,
                Padding = new Padding(8, 7, 0, 0)
            },

            _status,

            refresh,
            add,
            edit,
            delete,
            history,

            _lblAtualizacao
        });

        // ============================================================
        // FORM
        // ============================================================

        Controls.Add(_grid);
        Controls.Add(top);

        OrionUi.StyleModuleControls(this);

        // ============================================================
        // EVENTOS
        // ============================================================

        Shown += (_, _) =>
        {
            LoadSensors();
            _refreshTimer.Start();
        };

        FormClosed += (_, _) =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        };

        _refreshTimer.Tick += (_, _) =>
        {
            LoadSensors();
        };

        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                LoadSensors();
                e.SuppressKeyPress = true;
            }
        };

        _status.SelectedIndexChanged += (_, _) =>
        {
            LoadSensors();
        };
    }

    // ================================================================
    // SENSOR SELECIONADO
    // ================================================================

    private int? SelectedId()
    {
        var value = _grid.CurrentRow?.Cells["ID"].Value;

        if (value is null || value == DBNull.Value)
        {
            MessageBox.Show(
                this,
                "Selecione um sensor na tabela.",
                "Sensores",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return null;
        }

        return Convert.ToInt32(value);
    }

    // ================================================================
    // EXCLUIR SENSOR
    // ================================================================

    private void DeleteSelected()
    {
        var id = SelectedId();

        if (!id.HasValue)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Deseja realmente excluir o sensor #{id.Value}?",
            "Confirmar exclusão",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        try
        {
            using var connection = DatabaseHelper.GetConnection();

            using var command = new NpgsqlCommand(
                "DELETE FROM sensores WHERE id_sensor = @id",
                connection);

            command.Parameters.AddWithValue("id", id.Value);

            int affectedRows = command.ExecuteNonQuery();

            if (affectedRows == 0)
            {
                MessageBox.Show(
                    this,
                    "O sensor não foi encontrado ou já foi excluído.",
                    "Sensores",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            LoadSensors();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Não foi possível excluir o sensor.\n" + ex.Message,
                "Sensores",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // ================================================================
    // CARREGAR SENSORES
    // ================================================================

    private void LoadSensors()
    {
        try
        {
            using var connection = DatabaseHelper.GetConnection();

            const string sql = """
                SELECT
                    s.id_sensor AS "ID",
                    s.tipo_sensor AS "Tipo",
                    s.modelo_sensor AS "Modelo",
                    s.status_sensor AS "Status",

                    g.codigo_gateway AS "Gateway",

                    a.nome_area AS "Área de risco",

                    v.codigo_vagao AS "Vagão",

                    s.ultima_comunicacao AS "Última comunicação",

                    s.timeout_comunicacao_segundos AS "Timeout (s)",

                    distancia.valor AS "Distância atual (cm)",

                    peso.valor AS "Peso atual (g)",

                    ultima.recebido_em AS "Última leitura"

                FROM sensores s

                LEFT JOIN gateway g
                    ON g.id_gateway = s.id_gateway

                LEFT JOIN areas_risco a
                    ON a.id_area = s.id_area

                LEFT JOIN vagoes v
                    ON v.id_vagao = s.id_vagao

                LEFT JOIN LATERAL
                (
                    SELECT
                        l.valor
                    FROM leituras_sensores l
                    WHERE
                        l.id_sensor = s.id_sensor
                        AND LOWER(l.metrica) = 'distancia'
                    ORDER BY l.recebido_em DESC
                    LIMIT 1
                ) distancia
                    ON TRUE

                LEFT JOIN LATERAL
                (
                    SELECT
                        l.valor
                    FROM leituras_sensores l
                    WHERE
                        l.id_sensor = s.id_sensor
                        AND LOWER(l.metrica) = 'peso'
                    ORDER BY l.recebido_em DESC
                    LIMIT 1
                ) peso
                    ON TRUE

                LEFT JOIN LATERAL
                (
                    SELECT
                        l.recebido_em
                    FROM leituras_sensores l
                    WHERE
                        l.id_sensor = s.id_sensor
                    ORDER BY l.recebido_em DESC
                    LIMIT 1
                ) ultima
                    ON TRUE

                WHERE
                    (
                        @q = ''
                        OR s.id_sensor::text ILIKE @like
                        OR s.tipo_sensor ILIKE @like
                        OR s.modelo_sensor ILIKE @like
                    )

                    AND

                    (
                        @status = 'Todos'
                        OR LOWER(s.status_sensor) = LOWER(@status)
                    )

                ORDER BY
                    s.id_sensor;
                """;

            using var command = new NpgsqlCommand(sql, connection);

            string searchText = _search.Text.Trim();

            command.Parameters.AddWithValue(
                "q",
                searchText);

            command.Parameters.AddWithValue(
                "like",
                "%" + searchText + "%");

            command.Parameters.AddWithValue(
                "status",
                _status.SelectedItem?.ToString() ?? "Todos");

            var table = new DataTable();

            using var adapter = new NpgsqlDataAdapter(command);

            adapter.Fill(table);

            _grid.DataSource = table;

            // ========================================================
            // FORMATAÇÃO DAS COLUNAS
            // ========================================================

            if (_grid.Columns["ID"] != null)
            {
                _grid.Columns["ID"].FillWeight = 40;
            }

            if (_grid.Columns["Tipo"] != null)
            {
                _grid.Columns["Tipo"].FillWeight = 70;
            }

            if (_grid.Columns["Modelo"] != null)
            {
                _grid.Columns["Modelo"].FillWeight = 150;
            }

            if (_grid.Columns["Status"] != null)
            {
                _grid.Columns["Status"].FillWeight = 75;
            }

            if (_grid.Columns["Gateway"] != null)
            {
                _grid.Columns["Gateway"].FillWeight = 80;
            }

            if (_grid.Columns["Área de risco"] != null)
            {
                _grid.Columns["Área de risco"].FillWeight = 100;
            }

            if (_grid.Columns["Vagão"] != null)
            {
                _grid.Columns["Vagão"].FillWeight = 80;
            }

            if (_grid.Columns["Última comunicação"] != null)
            {
                _grid.Columns["Última comunicação"].FillWeight = 140;

                _grid.Columns["Última comunicação"]
                    .DefaultCellStyle.Format =
                    "dd/MM/yyyy HH:mm:ss";
            }

            if (_grid.Columns["Timeout (s)"] != null)
            {
                _grid.Columns["Timeout (s)"].FillWeight = 70;
            }

            if (_grid.Columns["Distância atual (cm)"] != null)
            {
                _grid.Columns["Distância atual (cm)"].FillWeight = 100;

                _grid.Columns["Distância atual (cm)"]
                    .DefaultCellStyle.Format =
                    "0.00";
            }

            if (_grid.Columns["Peso atual (g)"] != null)
            {
                _grid.Columns["Peso atual (g)"].FillWeight = 90;

                _grid.Columns["Peso atual (g)"]
                    .DefaultCellStyle.Format =
                    "0.00";
            }

            if (_grid.Columns["Última leitura"] != null)
            {
                _grid.Columns["Última leitura"].FillWeight = 140;

                _grid.Columns["Última leitura"]
                    .DefaultCellStyle.Format =
                    "dd/MM/yyyy HH:mm:ss";
            }

            // ========================================================
            // CORES DO STATUS
            // ========================================================

            foreach (DataGridViewRow row in _grid.Rows)
            {
                var statusValue =
                    row.Cells["Status"].Value?.ToString()?.ToLower();

                if (statusValue == "online")
                {
                    row.Cells["Status"].Style.ForeColor =
                        Color.DarkGreen;

                    row.Cells["Status"].Style.Font =
                        new Font(
                            _grid.Font,
                            FontStyle.Bold);
                }
                else if (statusValue == "offline")
                {
                    row.Cells["Status"].Style.ForeColor =
                        Color.DarkOrange;

                    row.Cells["Status"].Style.Font =
                        new Font(
                            _grid.Font,
                            FontStyle.Bold);
                }
                else if (statusValue == "falha")
                {
                    row.Cells["Status"].Style.ForeColor =
                        Color.DarkRed;

                    row.Cells["Status"].Style.Font =
                        new Font(
                            _grid.Font,
                            FontStyle.Bold);
                }
            }

            // ========================================================
            // STATUS DA ATUALIZAÇÃO
            // ========================================================

            _lblAtualizacao.Text =
                $"Atualizado: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _lblAtualizacao.Text =
                "Falha na atualização";

            MessageBox.Show(
                this,
                "Falha ao carregar sensores.\n\n" +
                "Confira a estrutura do banco e se a tabela " +
                "leituras_sensores está disponível.\n\n" +
                ex.Message,
                "Sensores",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}