using System.Data;
using System.Text;
using Npgsql;
using OrionInc.Data;
using OrionInc.Services;

namespace OrionInc.Forms;

public sealed class FormAuditoria : Form
{
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };

    private readonly DateTimePicker _from = new()
    {
        Format = DateTimePickerFormat.Short,
        Value = DateTime.Today.AddDays(-7)
    };

    private readonly DateTimePicker _to = new()
    {
        Format = DateTimePickerFormat.Short,
        Value = DateTime.Today
    };

    private readonly TextBox _search = new()
    {
        Width = 170,
        PlaceholderText = "Pesquisar..."
    };

    private readonly TextBox _entity = new()
    {
        Width = 130,
        PlaceholderText = "Entidade"
    };

    private readonly TextBox _actor = new()
    {
        Width = 140,
        PlaceholderText = "Responsável"
    };

    private readonly ComboBox _type = new()
    {
        Width = 145,
        DropDownStyle = ComboBoxStyle.DropDownList
    };

    private readonly ComboBox _result = new()
    {
        Width = 110,
        DropDownStyle = ComboBoxStyle.DropDownList
    };

    private readonly Label _counter = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
        ForeColor = Color.FromArgb(51, 65, 85)
    };

    private DataTable _current = new();

    public FormAuditoria()
    {
        Text = "Orion Inc | Auditoria detalhada";
        Width = 1400;
        Height = 780;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1100, 650);

        _type.Items.AddRange(new object[]
        {
            "Todos",
            "LOGIN_TENTATIVA",
            "LOGIN_SUCESSO",
            "LOGIN_FALHA",
            "LOGIN_ERRO",
            "LOGOUT",
            "CRIACAO",
            "ALTERACAO",
            "EXCLUSAO",
            "SENSOR_OFFLINE",
            "SENSOR_RECUPERADO",
            "MODULO_ABERTO",
            "ALERTA",
            "MANUTENCAO",
            "ERRO"
        });
        _type.SelectedIndex = 0;

        _result.Items.AddRange(new object[]
        {
            "Todos",
            "SUCESSO",
            "FALHA"
        });
        _result.SelectedIndex = 0;

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 105,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(243, 246, 250),
            WrapContents = true
        };

        filters.Controls.AddRange(new Control[]
        {
            MakeLabel("De"),
            _from,
            MakeLabel("Até"),
            _to,
            _search,
            _entity,
            _actor,
            _type,
            _result
        });

        var btnFilter = MakeButton("Filtrar", 85);
        btnFilter.Click += (_, _) => LoadAudit();
        filters.Controls.Add(btnFilter);

        var btnSelected = MakeButton("Exportar selecionado", 145);
        btnSelected.Click += async (_, _) => await ExportarSelecionadoAsync();
        filters.Controls.Add(btnSelected);

        var btnFiltered = MakeButton("Exportar filtrados", 125);
        btnFiltered.Click += async (_, _) => await ExportarFiltradosAsync();
        filters.Controls.Add(btnFiltered);

        var btnAll = MakeButton("Exportar todos", 110);
        btnAll.Click += async (_, _) => await ExportarTodosAsync();
        filters.Controls.Add(btnAll);

        var btnDetails = MakeButton("Ver detalhes", 105);
        btnDetails.Click += (_, _) => MostrarDetalhesSelecionado();
        filters.Controls.Add(btnDetails);

        filters.Controls.Add(_counter);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 430,
            FixedPanel = FixedPanel.Panel2
        };

        split.Panel1.Controls.Add(_grid);

        var detail = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 9),
            BackColor = Color.White
        };

        split.Panel2.Controls.Add(detail);

        _grid.CellDoubleClick += (_, _) =>
        {
            var text = ObterDetalhesSelecionado();
            detail.Text = text;
        };

        Controls.Add(split);
        Controls.Add(filters);

        Shown += (_, _) => LoadAudit();
    }

    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(0, 7, 0, 0)
        };
    }

    private static Button MakeButton(string text, int width)
    {
        return new Button
        {
            Text = text,
            Width = width,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White
        };
    }

    private void LoadAudit()
    {
        try
        {
            using var c = DatabaseHelper.GetConnection();

            const string sql = """
                SELECT
                    id_evento AS "ID Evento",
                    ocorrido_em AS "Data/Hora",
                    tipo_evento AS "Evento",
                    entidade AS "Entidade",
                    entidade_id AS "ID Entidade",
                    responsavel AS "Responsável",
                    modulo AS "Módulo",
                    origem AS "Origem",
                    resultado AS "Resultado",
                    descricao AS "Descrição",
                    valores_anteriores::text AS "Antes",
                    valores_novos::text AS "Depois",
                    detalhes::text AS "Detalhes",
                    ip_origem::text AS "IP",
                    hostname AS "Computador"
                FROM public.auditoria_eventos
                WHERE ocorrido_em >= @from
                  AND ocorrido_em < @to
                  AND (@entity = '' OR entidade ILIKE @entityLike)
                  AND (@actor = '' OR responsavel ILIKE @actorLike)
                  AND (
                      @type = 'Todos'
                      OR tipo_evento = @type
                  )
                  AND (
                      @result = 'Todos'
                      OR resultado = @result
                  )
                  AND (
                      @search = ''
                      OR tipo_evento ILIKE @searchLike
                      OR entidade ILIKE @searchLike
                      OR responsavel ILIKE @searchLike
                      OR descricao ILIKE @searchLike
                      OR modulo ILIKE @searchLike
                  )
                ORDER BY ocorrido_em DESC
                LIMIT 5000;
                """;

            using var cmd = new NpgsqlCommand(sql, c);

            cmd.Parameters.AddWithValue(
                "from",
                DateTime.SpecifyKind(
                    _from.Value.Date,
                    DateTimeKind.Utc));

            cmd.Parameters.AddWithValue(
                "to",
                DateTime.SpecifyKind(
                    _to.Value.Date.AddDays(1),
                    DateTimeKind.Utc));

            AddTextFilter(cmd, "entity", _entity.Text);
            AddTextFilter(cmd, "actor", _actor.Text);
            cmd.Parameters.AddWithValue("type", _type.Text);
            cmd.Parameters.AddWithValue("result", _result.Text);
            AddTextFilter(cmd, "search", _search.Text);

            _current = new DataTable();

            using var da = new NpgsqlDataAdapter(cmd);
            da.Fill(_current);

            _grid.DataSource = _current;

            if (_grid.Columns["ID Evento"] is DataGridViewColumn id)
                id.Width = 75;

            if (_grid.Columns["Descrição"] is DataGridViewColumn description)
                description.FillWeight = 220;

            if (_grid.Columns["Detalhes"] is DataGridViewColumn details)
                details.FillWeight = 180;

            _counter.Text =
                $"{_current.Rows.Count} evento(s) carregado(s)";

            if (_current.Rows.Count == 5000)
            {
                _counter.Text += " — limite de 5.000 na tela";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Não foi possível consultar a auditoria.\n\n" +
                "Execute as migrações do banco antes de usar esta tela.\n\n" +
                ex.Message,
                "Auditoria",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static void AddTextFilter(
        NpgsqlCommand cmd,
        string name,
        string value)
    {
        string trimmed = value.Trim();

        cmd.Parameters.AddWithValue(name, trimmed);
        cmd.Parameters.AddWithValue(
            name + "Like",
            "%" + trimmed + "%");
    }

    private async Task ExportarSelecionadoAsync()
    {
        if (_grid.CurrentRow is null)
        {
            MessageBox.Show(
                this,
                "Selecione um evento.",
                "Auditoria",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var idValue = _grid.CurrentRow.Cells["ID Evento"].Value;

        if (idValue is null || idValue == DBNull.Value)
            return;

        if (!long.TryParse(idValue.ToString(), out long id))
            return;

        using var dialog = new SaveFileDialog
        {
            Filter = "Arquivo TXT (*.txt)|*.txt",
            FileName = $"auditoria-evento-{id}-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            using var c = DatabaseHelper.GetConnection();

            const string sql = """
                SELECT
                    id_evento,
                    ocorrido_em,
                    tipo_evento,
                    entidade,
                    entidade_id,
                    id_usuario,
                    responsavel,
                    origem,
                    resultado,
                    descricao,
                    modulo,
                    detalhes::text,
                    valores_anteriores::text,
                    valores_novos::text,
                    ip_origem::text,
                    hostname
                FROM auditoria_eventos
                WHERE id_evento = @id;
                """;

            using var cmd = new NpgsqlCommand(sql, c);
            cmd.Parameters.AddWithValue("id", id);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                MessageBox.Show(
                    this,
                    "O evento não foi encontrado.",
                    "Auditoria",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            await using var writer = new StreamWriter(
                dialog.FileName,
                false,
                new UTF8Encoding(false));

            await EscreverEventoAsync(reader, writer);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Falha ao exportar o evento.\n\n" + ex.Message,
                "Auditoria",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        await AuditService.RegistrarAsync(
            "EXPORTACAO_TXT",
            "auditoria_eventos",
            id.ToString(),
            "SUCESSO",
            $"Evento de auditoria ID {id} exportado individualmente para TXT.",
            "FormAuditoria");

        MessageBox.Show(
            this,
            "Evento exportado com sucesso.",
            "Auditoria",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async Task ExportarFiltradosAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Arquivo TXT (*.txt)|*.txt",
            FileName = $"auditoria-filtrada-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            using var c = DatabaseHelper.GetConnection();
            using var cmd = CriarConsultaBase(c);

            await using var reader = await cmd.ExecuteReaderAsync();
            await using var writer = new StreamWriter(
                dialog.FileName,
                false,
                new UTF8Encoding(false));

            long total = 0;

            while (await reader.ReadAsync())
            {
                await EscreverEventoAsync(reader, writer);
                total++;
            }

            await writer.WriteLineAsync();
            await writer.WriteLineAsync(
                $"TOTAL DE EVENTOS EXPORTADOS: {total}");
            await writer.WriteLineAsync(
                $"EXPORTADO EM: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");

            await AuditService.RegistrarAsync(
                "EXPORTACAO_TXT",
                "auditoria_eventos",
                null,
                "SUCESSO",
                $"Exportação filtrada concluída. Total de eventos: {total}.",
                "FormAuditoria",
                new
                {
                    tipo = "FILTRADOS",
                    quantidade = total,
                    periodoInicio = _from.Value.Date,
                    periodoFim = _to.Value.Date,
                    entidade = _entity.Text.Trim(),
                    responsavel = _actor.Text.Trim(),
                    evento = _type.Text,
                    resultado = _result.Text,
                    pesquisa = _search.Text.Trim()
                });

            MessageBox.Show(
                this,
                $"{total} evento(s) exportado(s).",
                "Auditoria",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Falha ao exportar os eventos filtrados.\n\n" +
                ex.Message,
                "Auditoria",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task ExportarTodosAsync()
    {
        using var confirm = new SaveFileDialog
        {
            Filter = "Arquivo TXT (*.txt)|*.txt",
            FileName = $"auditoria-todos-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (confirm.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            using var c = DatabaseHelper.GetConnection();

            const string sql = """
                SELECT
                    id_evento,
                    ocorrido_em,
                    tipo_evento,
                    entidade,
                    entidade_id,
                    id_usuario,
                    responsavel,
                    origem,
                    resultado,
                    descricao,
                    modulo,
                    detalhes::text,
                    valores_anteriores::text,
                    valores_novos::text,
                    ip_origem::text,
                    hostname
                FROM auditoria_eventos
                ORDER BY ocorrido_em ASC;
                """;

            using var cmd = new NpgsqlCommand(sql, c);
            await using var reader = await cmd.ExecuteReaderAsync();

            await using var writer = new StreamWriter(
                confirm.FileName,
                false,
                new UTF8Encoding(false));

            long total = 0;

            while (await reader.ReadAsync())
            {
                await EscreverEventoAsync(reader, writer);
                total++;
            }

            await writer.WriteLineAsync();
            await writer.WriteLineAsync(
                $"TOTAL DE EVENTOS EXPORTADOS: {total}");
            await writer.WriteLineAsync(
                $"EXPORTADO EM: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");

            await AuditService.RegistrarAsync(
                "EXPORTACAO_TXT",
                "auditoria_eventos",
                null,
                "SUCESSO",
                $"Exportação completa da auditoria concluída. Total de eventos: {total}.",
                "FormAuditoria",
                new { tipo = "TODOS", quantidade = total });

            MessageBox.Show(
                this,
                $"{total} evento(s) exportado(s).",
                "Auditoria",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Falha ao exportar todos os eventos.\n\n" +
                ex.Message,
                "Auditoria",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private NpgsqlCommand CriarConsultaBase(
        NpgsqlConnection connection)
    {
        const string sql = """
            SELECT
                id_evento,
                ocorrido_em,
                tipo_evento,
                entidade,
                entidade_id,
                id_usuario,
                responsavel,
                origem,
                resultado,
                descricao,
                modulo,
                detalhes::text,
                valores_anteriores::text,
                valores_novos::text,
                ip_origem::text,
                hostname
            FROM auditoria_eventos
            WHERE ocorrido_em >= @from
              AND ocorrido_em < @to
              AND (@entity = '' OR entidade ILIKE @entityLike)
              AND (@actor = '' OR responsavel ILIKE @actorLike)
              AND (@type = 'Todos' OR tipo_evento = @type)
              AND (@result = 'Todos' OR resultado = @result)
              AND (
                  @search = ''
                  OR tipo_evento ILIKE @searchLike
                  OR entidade ILIKE @searchLike
                  OR responsavel ILIKE @searchLike
                  OR descricao ILIKE @searchLike
                  OR modulo ILIKE @searchLike
              )
            ORDER BY ocorrido_em ASC;
            """;

        var cmd = new NpgsqlCommand(sql, connection);

        cmd.Parameters.AddWithValue(
            "from",
            DateTime.SpecifyKind(
                _from.Value.Date,
                DateTimeKind.Utc));

        cmd.Parameters.AddWithValue(
            "to",
            DateTime.SpecifyKind(
                _to.Value.Date.AddDays(1),
                DateTimeKind.Utc));

        AddTextFilter(cmd, "entity", _entity.Text);
        AddTextFilter(cmd, "actor", _actor.Text);
        cmd.Parameters.AddWithValue("type", _type.Text);
        cmd.Parameters.AddWithValue("result", _result.Text);
        AddTextFilter(cmd, "search", _search.Text);

        return cmd;
    }

    private static async Task EscreverEventoAsync(
        NpgsqlDataReader reader,
        StreamWriter writer)
    {
        await writer.WriteLineAsync(new string('=', 78));
        await writer.WriteLineAsync("EVENTO DE AUDITORIA");
        await writer.WriteLineAsync(new string('=', 78));
        await writer.WriteLineAsync($"ID DO EVENTO: {Value(reader, 0)}");
        await writer.WriteLineAsync($"DATA/HORA: {Value(reader, 1)}");
        await writer.WriteLineAsync($"TIPO DE EVENTO: {Value(reader, 2)}");
        await writer.WriteLineAsync($"ENTIDADE: {Value(reader, 3)}");
        await writer.WriteLineAsync($"ID DA ENTIDADE: {Value(reader, 4)}");
        await writer.WriteLineAsync($"ID DO USUÁRIO: {Value(reader, 5)}");
        await writer.WriteLineAsync($"USUÁRIO RESPONSÁVEL: {Value(reader, 6)}");
        await writer.WriteLineAsync($"ORIGEM: {Value(reader, 7)}");
        await writer.WriteLineAsync($"RESULTADO: {Value(reader, 8)}");
        await writer.WriteLineAsync($"DESCRIÇÃO: {Value(reader, 9)}");
        await writer.WriteLineAsync($"MÓDULO/TELA: {Value(reader, 10)}");
        await writer.WriteLineAsync($"DETALHES: {Value(reader, 11)}");
        await writer.WriteLineAsync($"VALORES ANTERIORES: {Value(reader, 12)}");
        await writer.WriteLineAsync($"VALORES NOVOS: {Value(reader, 13)}");
        await writer.WriteLineAsync($"IP: {Value(reader, 14)}");
        await writer.WriteLineAsync($"COMPUTADOR: {Value(reader, 15)}");
        await writer.WriteLineAsync(new string('=', 78));
        await writer.WriteLineAsync(string.Empty);
    }

    private static string Value(
        NpgsqlDataReader reader,
        int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return "N/A";

        return Convert.ToString(reader.GetValue(ordinal))?
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim() ?? "N/A";
    }

    private void MostrarDetalhesSelecionado()
    {
        if (_grid.CurrentRow is null)
            return;

        var sb = new StringBuilder();

        foreach (DataGridViewCell cell in _grid.CurrentRow.Cells)
        {
            string column =
                _grid.Columns[cell.ColumnIndex].HeaderText;

            sb.AppendLine($"{column}: {cell.Value}");
        }

        MessageBox.Show(
            this,
            sb.ToString(),
            "Detalhes do evento",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private string ObterDetalhesSelecionado()
    {
        if (_grid.CurrentRow is null)
            return "Nenhum evento selecionado.";

        var sb = new StringBuilder();

        foreach (DataGridViewCell cell in _grid.CurrentRow.Cells)
        {
            string column =
                _grid.Columns[cell.ColumnIndex].HeaderText;

            sb.AppendLine($"{column}: {cell.Value}");
        }

        return sb.ToString();
    }
}
