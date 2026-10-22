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

    public FormSensores()
    {
        Text = "Orion Inc | Sensores";
        Width = 1100;
        Height = 650;
        StartPosition = FormStartPosition.CenterParent;

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

        var refresh = new Button
        {
            Text = "Atualizar",
            Width = 85,
            Height = 28
        };

        refresh.Click += (_, _) => LoadSensors();

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

        var delete = new Button
        {
            Text = "Excluir",
            Width = 70,
            Height = 28
        };

        delete.Click += (_, _) => DeleteSelected();

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
            history
        });

        Controls.Add(_grid);
        Controls.Add(top);

        Shown += (_, _) => LoadSensors();

        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                LoadSensors();
                e.SuppressKeyPress = true;
            }
        };

        _status.SelectedIndexChanged += (_, _) => LoadSensors();
    }

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
                    s.timeout_comunicacao_segundos AS "Timeout (s)"
                FROM sensores s
                LEFT JOIN gateway g
                    ON g.id_gateway = s.id_gateway
                LEFT JOIN areas_risco a
                    ON a.id_area = s.id_area
                LEFT JOIN vagoes v
                    ON v.id_vagao = s.id_vagao
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
                ORDER BY s.id_sensor;
                """;

            using var command = new NpgsqlCommand(sql, connection);

            string searchText = _search.Text.Trim();

            command.Parameters.AddWithValue("q", searchText);
            command.Parameters.AddWithValue("like", "%" + searchText + "%");
            command.Parameters.AddWithValue(
                "status",
                _status.SelectedItem?.ToString() ?? "Todos");

            var table = new DataTable();

            using var adapter = new NpgsqlDataAdapter(command);
            adapter.Fill(table);

            _grid.DataSource = table;

            if (_grid.Columns["ID"] != null)
            {
                _grid.Columns["ID"].FillWeight = 40;
            }

            if (_grid.Columns["Última comunicação"] != null)
            {
                _grid.Columns["Última comunicação"].FillWeight = 140;
                _grid.Columns["Última comunicação"].DefaultCellStyle.Format =
                    "dd/MM/yyyy HH:mm:ss";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Falha ao carregar sensores. Confira a estrutura do banco " +
                "e se a migração 03 foi aplicada.\n\n" + ex.Message,
                "Sensores",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
