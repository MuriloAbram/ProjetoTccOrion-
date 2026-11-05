using System.Data;
using System.Drawing.Drawing2D;
using Npgsql;
using OrionInc.Data;
using OrionInc.Services;

namespace OrionInc.Forms;

public sealed class FormDashboard : Form
{
    private readonly FlowLayoutPanel _metrics = new()
    {
        Dock = DockStyle.Top,
        Height = 225,
        Padding = new Padding(18, 12, 18, 10),
        AutoScroll = true,
        WrapContents = true,
        BackColor = Color.FromArgb(243, 246, 250)
    };

    private readonly Panel _statusChart = new()
    {
        Dock = DockStyle.Top,
        Height = 175,
        BackColor = Color.White,
        Padding = new Padding(18)
    };

    private readonly DataGridView _activity = new()
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

    private readonly Label _updated = new()
    {
        AutoSize = true,
        ForeColor = Color.FromArgb(100, 116, 139)
    };

    private readonly Label _lastEvent = new()
    {
        AutoSize = false,
        Width = 420,
        Height = 38,
        ForeColor = Color.FromArgb(71, 85, 105),
        Font = new Font("Segoe UI", 9)
    };

    private readonly int[] _chartValues = new int[4];

    public FormDashboard()
    {
        Text = "Orion Inc | Dashboard operacional";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1050, 700);
        BackColor = Color.White;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            BackColor = Color.FromArgb(20, 36, 59),
            Padding = new Padding(24, 12, 18, 8)
        };

        var title = new Label
        {
            Text = "ORION INC  /  CENTRAL DE MONITORAMENTO",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 10)
        };

        var welcome = new Label
        {
            Text = $"{Sessao.NomeUsuario}  •  {Sessao.NivelAcesso}",
            ForeColor = Color.FromArgb(205, 220, 238),
            AutoSize = true,
            Location = new Point(27, 48)
        };

        var btnRefresh = new Button
        {
            Text = "Atualizar",
            Width = 110,
            Height = 34,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(header.Width - 145, 20),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(20, 36, 59)
        };

        btnRefresh.Click += (_, _) => LoadDashboard();

        header.Resize += (_, _) =>
            btnRefresh.Left = header.ClientSize.Width - btnRefresh.Width - 24;

        header.Controls.AddRange(new Control[]
        {
            title,
            welcome,
            btnRefresh
        });

        var activityHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(20, 9, 20, 5),
            BackColor = Color.White
        };

        activityHeader.Controls.Add(new Label
        {
            Text = "Atividades recentes e eventos operacionais",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 8)
        });

        _updated.Location = new Point(20, 35);
        activityHeader.Controls.Add(_updated);

        _lastEvent.Location = new Point(510, 12);
        activityHeader.Controls.Add(_lastEvent);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            Padding = new Padding(16, 8, 8, 8),
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.FromArgb(248, 249, 251)
        };

        var modules = CreateButton("Abrir módulos", 130);

        modules.Click += (_, _) =>
        {
            try
            {
                using var f = new FormMenu();
                f.ShowDialog(this);

                if (Sessao.IdUsuario == 0)
                {
                    Close();
                    return;
                }

                LoadDashboard();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Não foi possível abrir o menu de módulos.\n\n" +
                    ex.Message,
                    "ORION INC",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };

        var sensors = CreateButton("Sensores", 110);

        sensors.Click += (_, _) =>
        {
            // Não bloqueia a interface esperando a auditoria.
            _ = RegistrarAcaoAsync(
                "MODULO_ABERTO",
                "sensores",
                "Tela de sensores aberta.");

            try
            {
                using var f = new FormSensores();
                f.ShowDialog(this);

                LoadDashboard();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Não foi possível abrir a tela de sensores.\n\n" +
                    ex.Message,
                    "ORION INC",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };

        var audit = CreateButton("Auditoria", 110);

        audit.Click += (_, _) =>
        {
            // Não bloqueia a interface esperando a auditoria.
            _ = RegistrarAcaoAsync(
                "MODULO_ABERTO",
                "auditoria_eventos",
                "Tela de auditoria aberta.");

            try
            {
                using var f = new FormAuditoria();
                f.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Não foi possível abrir a tela de auditoria.\n\n" +
                    ex.Message,
                    "ORION INC",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };

        var logout = CreateButton("Sair", 90);
        logout.BackColor = Color.FromArgb(170, 56, 56);
        logout.ForeColor = Color.White;

        logout.Click += async (_, _) =>
        {
            try
            {
                await RegistrarAcaoAsync(
                    "LOGOUT",
                    "usuarios",
                    "Sessão encerrada pelo usuário na Dashboard.");
            }
            catch
            {
                // Auditoria não pode impedir o logout.
            }

            Sessao.Limpar();
            Close();
        };

        footer.Controls.AddRange(new Control[]
        {
            modules,
            sensors,
            audit,
            logout
        });

        _statusChart.Paint += DrawStatusChart;

        Controls.Add(_activity);
        Controls.Add(activityHeader);
        Controls.Add(_statusChart);
        Controls.Add(_metrics);
        Controls.Add(footer);
        Controls.Add(header);

        Shown += (_, _) => LoadDashboard();
    }

    private static Button CreateButton(string text, int width)
    {
        return new Button
        {
            Text = text,
            Width = width,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(20, 36, 59)
        };
    }

    private void LoadDashboard()
    {
        try
        {
            using var c = DatabaseHelper.GetConnection();

            const string metricsSql = """
                SELECT
                    (SELECT count(*) FROM usuarios) AS total_usuarios,
                    (SELECT count(*) FROM funcionarios WHERE lower(status_funcionario) = 'ativo') AS funcionarios_ativos,
                    (SELECT count(*) FROM vagoes) AS total_vagoes,
                    (SELECT count(*) FROM sensores) AS total_sensores,
                    (SELECT count(*) FROM sensores WHERE lower(status_sensor) = 'online') AS sensores_online,
                    (SELECT count(*) FROM sensores WHERE lower(status_sensor) = 'offline') AS sensores_offline,
                    (SELECT count(*) FROM sensores WHERE lower(status_sensor) = 'falha') AS sensores_falha,
                    (SELECT count(*) FROM sensores WHERE lower(status_sensor) IN ('manutencao','manutenção')) AS sensores_manutencao,
                    (SELECT count(*) FROM gateway) AS total_gateways,
                    (SELECT count(*) FROM gateway WHERE lower(status_gateway) = 'online') AS gateways_online,
                    (SELECT count(*) FROM areas_risco WHERE area_ativa = true) AS areas_ativas,
                    (SELECT count(*) FROM registros_sistema WHERE evento_resolvido = false) AS alertas_ativos,
                    (SELECT count(*) FROM chamados_tecnicos WHERE lower(status_chamado) NOT IN ('encerrado','concluido','concluído')) AS chamados_abertos,
                    (SELECT count(*) FROM ordens_manutencao WHERE lower(status_ordem) NOT IN ('concluida','concluída','concluido','concluído')) AS ordens_pendentes,
                    (SELECT count(*) FROM auditoria_eventos) AS total_auditoria,
                    (SELECT count(*) FROM sensores
                        WHERE ultima_comunicacao IS NULL
                           OR ultima_comunicacao < now() -
                              (coalesce(timeout_comunicacao_segundos,180) * interval '1 second')
                    ) AS sensores_sem_comunicacao;
                """;

            using var cmd = new NpgsqlCommand(metricsSql, c);
            using var reader = cmd.ExecuteReader();

            _metrics.Controls.Clear();

            if (reader.Read())
            {
                var values = new long[16];

                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = Convert.ToInt64(reader.GetValue(i));
                }

                AddMetric(
                    "Usuários",
                    values[0].ToString(),
                    Color.FromArgb(35, 86, 139));

                AddMetric(
                    "Funcionários ativos",
                    values[1].ToString(),
                    Color.FromArgb(35, 86, 139));

                AddMetric(
                    "Vagões",
                    values[2].ToString(),
                    Color.FromArgb(35, 86, 139));

                AddMetric(
                    "Sensores",
                    values[3].ToString(),
                    Color.FromArgb(35, 86, 139));

                AddMetric(
                    "Sensores online",
                    values[4].ToString(),
                    Color.FromArgb(31, 133, 92));

                AddMetric(
                    "Sensores offline",
                    values[5].ToString(),
                    Color.FromArgb(211, 139, 35));

                AddMetric(
                    "Sensores em falha",
                    values[6].ToString(),
                    Color.FromArgb(177, 55, 55));

                AddMetric(
                    "Sensores manutenção",
                    values[7].ToString(),
                    Color.FromArgb(126, 91, 166));

                AddMetric(
                    "Gateways",
                    values[8].ToString(),
                    Color.FromArgb(35, 86, 139));

                AddMetric(
                    "Gateways online",
                    values[9].ToString(),
                    Color.FromArgb(31, 133, 92));

                AddMetric(
                    "Áreas ativas",
                    values[10].ToString(),
                    Color.FromArgb(35, 86, 139));

                AddMetric(
                    "Alertas ativos",
                    values[11].ToString(),
                    Color.FromArgb(177, 55, 55));

                AddMetric(
                    "Chamados abertos",
                    values[12].ToString(),
                    Color.FromArgb(211, 139, 35));

                AddMetric(
                    "Manutenções pendentes",
                    values[13].ToString(),
                    Color.FromArgb(126, 91, 166));

                AddMetric(
                    "Eventos de auditoria",
                    values[14].ToString(),
                    Color.FromArgb(35, 86, 139));

                AddMetric(
                    "Sensores sem comunicação",
                    values[15].ToString(),
                    Color.FromArgb(177, 55, 55));

                _chartValues[0] =
                    checked((int)Math.Min(values[4], int.MaxValue));

                _chartValues[1] =
                    checked((int)Math.Min(values[5], int.MaxValue));

                _chartValues[2] =
                    checked((int)Math.Min(values[6], int.MaxValue));

                _chartValues[3] =
                    checked((int)Math.Min(values[7], int.MaxValue));

                _statusChart.Invalidate();
            }

            reader.Close();

            var table = new DataTable();

            const string activitySql = """
                SELECT
                    ocorrido_em AS "Data/Hora",
                    tipo_evento AS "Evento",
                    entidade AS "Entidade",
                    entidade_id AS "ID",
                    responsavel AS "Responsável",
                    modulo AS "Módulo",
                    resultado AS "Resultado",
                    descricao AS "Descrição"
                FROM auditoria_eventos
                ORDER BY ocorrido_em DESC
                LIMIT 30;
                """;

            using (var activityCmd = new NpgsqlCommand(activitySql, c))
            using (var adapter = new NpgsqlDataAdapter(activityCmd))
            {
                adapter.Fill(table);
            }

            _activity.DataSource = table;

            if (_activity.Columns["Descrição"] is DataGridViewColumn description)
            {
                description.FillWeight = 220;
            }

            if (table.Rows.Count > 0)
            {
                var row = table.Rows[0];

                _lastEvent.Text =
                    $"Último evento: {row["Evento"]} • {row["Responsável"]}\r\n" +
                    $"{row["Descrição"]}";
            }
            else
            {
                _lastEvent.Text =
                    "Último evento: nenhum evento registrado.";
            }

            _updated.Text =
                $"Atualizado em {DateTimeOffset.Now:dd/MM/yyyy HH:mm:ss zzz}";
        }
        catch (Exception ex)
        {
            _metrics.Controls.Clear();

            AddMetric(
                "Dashboard",
                "INDISPONÍVEL",
                Color.FromArgb(177, 55, 55));

            MessageBox.Show(
                this,
                "Não foi possível carregar os indicadores do PostgreSQL.\n\n" +
                ex.Message,
                "Dashboard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void DrawStatusChart(
        object? sender,
        PaintEventArgs e)
    {
        var g = e.Graphics;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var titleFont =
            new Font("Segoe UI", 10, FontStyle.Bold);

        using var labelFont =
            new Font("Segoe UI", 9);

        using var textBrush =
            new SolidBrush(Color.FromArgb(55, 65, 81));

        using var trackBrush =
            new SolidBrush(Color.FromArgb(235, 239, 244));

        using var onlineBrush =
            new SolidBrush(Color.FromArgb(31, 133, 92));

        using var offlineBrush =
            new SolidBrush(Color.FromArgb(211, 139, 35));

        using var failBrush =
            new SolidBrush(Color.FromArgb(177, 55, 55));

        using var maintenanceBrush =
            new SolidBrush(Color.FromArgb(126, 91, 166));

        g.DrawString(
            "Distribuição de sensores por status",
            titleFont,
            textBrush,
            18,
            8);

        string[] labels =
        {
            "Online",
            "Offline",
            "Falha",
            "Manutenção"
        };

        Brush[] brushes =
        {
            onlineBrush,
            offlineBrush,
            failBrush,
            maintenanceBrush
        };

        int max = Math.Max(1, _chartValues.Max());

        int left = 125;

        int maxWidth =
            Math.Max(
                100,
                _statusChart.ClientSize.Width - left - 100);

        for (int i = 0; i < labels.Length; i++)
        {
            int y = 38 + i * 30;

            g.DrawString(
                labels[i],
                labelFont,
                textBrush,
                18,
                y);

            g.FillRectangle(
                trackBrush,
                left,
                y + 2,
                maxWidth,
                16);

            int width =
                (int)(
                    maxWidth *
                    (_chartValues[i] / (double)max));

            if (width > 0)
            {
                g.FillRectangle(
                    brushes[i],
                    left,
                    y + 2,
                    width,
                    16);
            }

            g.DrawString(
                _chartValues[i].ToString(),
                labelFont,
                textBrush,
                left + maxWidth + 10,
                y);
        }
    }

    private void AddMetric(
        string label,
        string value,
        Color accent)
    {
        var card = new Panel
        {
            Width = 205,
            Height = 91,
            Margin = new Padding(7),
            BackColor = Color.White,
            Padding = new Padding(12),
            BorderStyle = BorderStyle.FixedSingle
        };

        card.Controls.Add(new Panel
        {
            Dock = DockStyle.Left,
            Width = 5,
            BackColor = accent
        });

        card.Controls.Add(new Label
        {
            Text = value,
            Font = new Font(
                "Segoe UI",
                21,
                FontStyle.Bold),
            ForeColor = accent,
            AutoSize = true,
            Location = new Point(20, 7)
        });

        card.Controls.Add(new Label
        {
            Text = label,
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(75, 85, 99),
            AutoSize = true,
            Location = new Point(21, 57)
        });

        _metrics.Controls.Add(card);
    }

    private static void LogarAcao(
        string tipo,
        string entidade,
        string descricao)
    {
        // Mantido por compatibilidade com outras chamadas.
        // A operação de banco não bloqueia mais a thread da interface.
        _ = RegistrarAcaoAsync(
            tipo,
            entidade,
            descricao);
    }

    private static async Task RegistrarAcaoAsync(
        string tipo,
        string entidade,
        string descricao)
    {
        try
        {
            await AuditService.RegistrarAsync(
                tipo,
                entidade,
                null,
                "SUCESSO",
                descricao,
                "FormDashboard");
        }
        catch
        {
            // A auditoria não pode travar a navegação,
            // abertura de módulos ou encerramento do sistema.
        }
    }
}