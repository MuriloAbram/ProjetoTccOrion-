using System.Data;
using System.Drawing.Drawing2D;
using Npgsql;
using OrionInc.Data;

namespace OrionInc.Forms;

public sealed class FormHistoricoLeituras : Form
{
    // ============================================================
    // CONFIGURAÇÃO
    // ============================================================

    private readonly int _sensorId;

    private const int RefreshIntervalMs = 3000;

    // ============================================================
    // COMPONENTES
    // ============================================================

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode =
            DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None,
        SelectionMode =
            DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };

    private readonly ComboBox _metrica = new()
    {
        Width = 150,
        DropDownStyle =
            ComboBoxStyle.DropDownList
    };

    private readonly ComboBox _periodo = new()
    {
        Width = 150,
        DropDownStyle =
            ComboBoxStyle.DropDownList
    };

    private readonly Label _lblTotal = new()
    {
        AutoSize = true,
        Font = new Font(
            "Segoe UI",
            10,
            FontStyle.Bold),
        ForeColor =
            Color.FromArgb(
                35,
                86,
                139)
    };

    private readonly Label _lblUltima = new()
    {
        AutoSize = true,
        Font = new Font(
            "Segoe UI",
            9),
        ForeColor =
            Color.FromArgb(
                71,
                85,
                105)
    };

    private readonly Label _lblMedia = new()
    {
        AutoSize = true,
        Font = new Font(
            "Segoe UI",
            9),
        ForeColor =
            Color.FromArgb(
                71,
                85,
                105)
    };

    private readonly Label _lblStatus = new()
    {
        AutoSize = true,
        Font = new Font(
            "Segoe UI",
            9,
        FontStyle.Bold),
        ForeColor =
            Color.FromArgb(
                31,
                133,
                92)
    };

    private readonly Panel _chartPanel = new()
    {
        Dock = DockStyle.Top,
        Height = 245,
        BackColor = Color.White,
        Padding = new Padding(18)
    };

    private readonly System.Windows.Forms.Timer _timer = new()
    {
        Interval = RefreshIntervalMs
    };

    private bool _carregando;

    private List<LeituraGrafico> _dadosGrafico = new();

    // ============================================================
    // CONSTRUTOR
    // ============================================================

    public FormHistoricoLeituras(int sensorId)
    {
        _sensorId = sensorId;

        Text =
            $"Orion Inc | Histórico do Sensor #{sensorId}";

        Width = 1200;
        Height = 760;

        MinimumSize =
            new Size(1000, 650);

        StartPosition =
            FormStartPosition.CenterParent;

        BackColor = Color.White;

        CriarInterface();

        _chartPanel.Paint +=
            DesenharGrafico;

        Shown += (_, _) =>
        {
            CarregarDados();

            _timer.Tick += (_, _) =>
            {
                if (!IsDisposed &&
                    Visible &&
                    !_carregando)
                {
                    CarregarDados();
                }
            };

            _timer.Start();
        };

        FormClosing += (_, _) =>
        {
            _timer.Stop();
            _timer.Dispose();
        };
    }

    // ============================================================
    // INTERFACE
    // ============================================================

    private void CriarInterface()
    {
        // --------------------------------------------------------
        // HEADER
        // --------------------------------------------------------

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            BackColor =
                Color.FromArgb(
                    20,
                    36,
                    59),
            Padding =
                new Padding(
                    22,
                    12,
                    18,
                    8)
        };

        var titulo = new Label
        {
            Text =
                $"HISTÓRICO DE LEITURAS — SENSOR #{_sensorId}",
            ForeColor = Color.White,
            Font =
                new Font(
                    "Segoe UI",
                    16,
                    FontStyle.Bold),
            AutoSize = true,
            Location =
                new Point(22, 10)
        };

        var subtitulo = new Label
        {
            Text =
                "Monitoramento das leituras recebidas pelo sistema IoT",
            ForeColor =
                Color.FromArgb(
                    205,
                    220,
                    238),
            Font =
                new Font(
                    "Segoe UI",
                    9),
            AutoSize = true,
            Location =
                new Point(25, 47)
        };

        header.Controls.Add(titulo);
        header.Controls.Add(subtitulo);

        // --------------------------------------------------------
        // FILTROS
        // --------------------------------------------------------

        var filtros = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding =
                new Padding(
                    14,
                    10,
                    10,
                    8),
            BackColor =
                Color.FromArgb(
                    243,
                    246,
                    250),
            WrapContents = false
        };

        filtros.Controls.Add(new Label
        {
            Text = "Métrica",
            AutoSize = true,
            Padding =
                new Padding(
                    0,
                    6,
                    0,
                    0)
        });

        _metrica.Items.AddRange(
            new object[]
            {
                "Todas",
                "distancia",
                "peso"
            });

        _metrica.SelectedIndex = 0;

        filtros.Controls.Add(_metrica);

        filtros.Controls.Add(new Label
        {
            Text = "Período",
            AutoSize = true,
            Padding =
                new Padding(
                    12,
                    6,
                    0,
                    0)
        });

        _periodo.Items.AddRange(
            new object[]
            {
                "Últimas 50",
                "Últimas 100",
                "Últimas 250",
                "Últimas 500",
                "Últimas 1000"
            });

        _periodo.SelectedIndex = 0;

        filtros.Controls.Add(_periodo);

        var atualizar =
            CriarBotao(
                "Atualizar",
                95);

        atualizar.Click +=
            (_, _) => CarregarDados();

        filtros.Controls.Add(atualizar);

        var limpar =
            CriarBotao(
                "Limpar filtros",
                115);

        limpar.Click +=
            (_, _) =>
            {
                _metrica.SelectedIndex = 0;
                _periodo.SelectedIndex = 0;

                CarregarDados();
            };

        filtros.Controls.Add(limpar);

        // --------------------------------------------------------
        // RESUMO
        // --------------------------------------------------------

        var resumo = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding =
                new Padding(
                    18,
                    8,
                    18,
                    6),
            BackColor = Color.White,
            WrapContents = false
        };

        resumo.Controls.Add(
            CriarResumo(
                "Leituras",
                _lblTotal));

        resumo.Controls.Add(
            CriarResumo(
                "Última leitura",
                _lblUltima));

        resumo.Controls.Add(
            CriarResumo(
                "Média",
                _lblMedia));

        resumo.Controls.Add(
            CriarResumo(
                "Status",
                _lblStatus));

        // --------------------------------------------------------
        // CONTROLES
        // --------------------------------------------------------

        Controls.Add(_grid);
        Controls.Add(_chartPanel);
        Controls.Add(resumo);
        Controls.Add(filtros);
        Controls.Add(header);
    }

    private static Panel CriarResumo(
        string titulo,
        Label valor)
    {
        var panel = new Panel
        {
            Width = 250,
            Height = 40,
            Margin =
                new Padding(
                    0,
                    0,
                    18,
                    0)
        };

        var label = new Label
        {
            Text = titulo + ":",
            AutoSize = true,
            Font =
                new Font(
                    "Segoe UI",
                    8,
                    FontStyle.Bold),
            ForeColor =
                Color.FromArgb(
                    100,
                    116,
                    139),
            Location =
                new Point(
                    0,
                    2)
        };

        valor.Location =
            new Point(
                0,
                18);

        panel.Controls.Add(label);
        panel.Controls.Add(valor);

        return panel;
    }

    private static Button CriarBotao(
        string texto,
        int largura)
    {
        return new Button
        {
            Text = texto,
            Width = largura,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor =
                Color.FromArgb(
                    20,
                    36,
                    59)
        };
    }

    // ============================================================
    // CARREGAR DADOS
    // ============================================================

    private void CarregarDados()
    {
        if (_carregando)
        {
            return;
        }

        _carregando = true;

        try
        {
            using var connection =
                DatabaseHelper.GetConnection();

            string metrica =
                _metrica.SelectedItem?
                    .ToString()
                    ?.ToLowerInvariant()
                    ?? "todas";

            int limite =
                ObterLimite();

            const string sql = """
                SELECT
                    l.id_leitura,
                    l.id_sensor,
                    l.metrica,
                    l.valor,
                    l.unidade,
                    l.recebido_em,
                    l.validacao
                FROM leituras_sensores l
                WHERE
                    l.id_sensor = @sensor_id
                    AND
                    (
                        @metrica = 'todas'
                        OR LOWER(l.metrica) = @metrica
                    )
                ORDER BY
                    l.recebido_em DESC
                LIMIT @limite;
                """;

            using var command =
                new NpgsqlCommand(
                    sql,
                    connection);

            command.Parameters.AddWithValue(
                "sensor_id",
                _sensorId);

            command.Parameters.AddWithValue(
                "metrica",
                metrica);

            command.Parameters.AddWithValue(
                "limite",
                limite);

            var table = new DataTable();

            using var adapter =
                new NpgsqlDataAdapter(
                    command);

            adapter.Fill(table);

            PreencherTabela(table);

            PrepararGrafico(table);

            AtualizarResumo(table);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Não foi possível carregar o histórico " +
                "de leituras.\n\n" +
                ex.Message,
                "Histórico de leituras",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _carregando = false;
        }
    }

    // ============================================================
    // LIMITE
    // ============================================================

    private int ObterLimite()
    {
        return _periodo.SelectedItem?.ToString()
            switch
        {
            "Últimas 100" => 100,
            "Últimas 250" => 250,
            "Últimas 500" => 500,
            "Últimas 1000" => 1000,
            _ => 50
        };
    }

    // ============================================================
    // TABELA
    // ============================================================

    private void PreencherTabela(
        DataTable table)
    {
        var visualTable = new DataTable();

        visualTable.Columns.Add(
            "ID",
            typeof(int));

        visualTable.Columns.Add(
            "Métrica",
            typeof(string));

        visualTable.Columns.Add(
            "Valor",
            typeof(string));

        visualTable.Columns.Add(
            "Unidade",
            typeof(string));

        visualTable.Columns.Add(
            "Data/Hora",
            typeof(string));

        visualTable.Columns.Add(
            "Validação",
            typeof(string));

        foreach (DataRow row in table.Rows)
        {
            string metrica =
                row["metrica"]?.ToString()
                ?? "";

            double valor =
                Convert.ToDouble(
                    row["valor"]);

            string unidade =
                row["unidade"]?.ToString()
                ?? "";

            DateTimeOffset recebidoEm =
                ConverterData(
                    row["recebido_em"]);

            visualTable.Rows.Add(
                Convert.ToInt32(
                    row["id_leitura"]),

                FormatarMetrica(
                    metrica),

                FormatarValor(
                    valor,
                    metrica),

                unidade,

                recebidoEm
                    .ToLocalTime()
                    .ToString(
                        "dd/MM/yyyy HH:mm:ss"),

                row["validacao"]?.ToString()
                    ?? "");
        }

        _grid.DataSource =
            visualTable;

        AjustarColunas();

        AplicarCoresTabela();
    }

    private void AjustarColunas()
    {
        if (_grid.Columns["ID"] is DataGridViewColumn id)
        {
            id.FillWeight = 45;
        }

        if (_grid.Columns["Métrica"]
            is DataGridViewColumn metrica)
        {
            metrica.FillWeight = 90;
        }

        if (_grid.Columns["Valor"]
            is DataGridViewColumn valor)
        {
            valor.FillWeight = 100;
        }

        if (_grid.Columns["Unidade"]
            is DataGridViewColumn unidade)
        {
            unidade.FillWeight = 70;
        }

        if (_grid.Columns["Data/Hora"]
            is DataGridViewColumn data)
        {
            data.FillWeight = 150;
        }

        if (_grid.Columns["Validação"]
            is DataGridViewColumn validacao)
        {
            validacao.FillWeight = 90;
        }
    }

    // ============================================================
    // CORES DA TABELA
    // ============================================================

    private void AplicarCoresTabela()
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells["Métrica"].Value
                is not string metrica)
            {
                continue;
            }

            string valorTexto =
                row.Cells["Valor"].Value?
                    .ToString()
                    ?? "";

            if (metrica.Equals(
                    "Distância",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(
                        valorTexto
                            .Replace(" cm", "")
                            .Replace(".", "")
                            .Replace(",", "."),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double distancia))
                {
                    if (distancia <= 10)
                    {
                        row.DefaultCellStyle.BackColor =
                            Color.FromArgb(
                                255,
                                235,
                                235);

                        row.DefaultCellStyle.ForeColor =
                            Color.FromArgb(
                                177,
                                55,
                                55);
                    }
                }
            }

            if (row.Cells["Validação"].Value?
                    .ToString()
                    ?.Equals(
                        "valida",
                        StringComparison.OrdinalIgnoreCase)
                == true)
            {
                row.Cells["Validação"]
                    .Style.ForeColor =
                    Color.FromArgb(
                        31,
                        133,
                        92);
            }
        }
    }

    // ============================================================
    // RESUMO
    // ============================================================

    private void AtualizarResumo(
        DataTable table)
    {
        int total =
            table.Rows.Count;

        _lblTotal.Text =
            total.ToString();

        if (total == 0)
        {
            _lblUltima.Text =
                "Nenhuma";

            _lblMedia.Text =
                "--";

            _lblStatus.Text =
                "Sem dados";

            _lblStatus.ForeColor =
                Color.FromArgb(
                    211,
                    139,
                    35);

            return;
        }

        DateTimeOffset ultima =
            ConverterData(
                table.Rows[0]["recebido_em"]);

        _lblUltima.Text =
            ultima
                .ToLocalTime()
                .ToString(
                    "dd/MM/yyyy HH:mm:ss");

        double soma = 0;

        int quantidade = 0;

        foreach (DataRow row in table.Rows)
        {
            if (double.TryParse(
                    row["valor"]?.ToString(),
                    out double valor))
            {
                soma += valor;
                quantidade++;
            }
        }

        if (quantidade > 0)
        {
            double media =
                soma / quantidade;

            _lblMedia.Text =
                $"{media:N2}";
        }
        else
        {
            _lblMedia.Text = "--";
        }

        _lblStatus.Text =
            "● Dados recebidos";

        _lblStatus.ForeColor =
            Color.FromArgb(
                31,
                133,
                92);
    }

    // ============================================================
    // GRÁFICO
    // ============================================================

    private void PrepararGrafico(
        DataTable table)
    {
        _dadosGrafico.Clear();

        foreach (DataRow row in table.Rows)
        {
            string metrica =
                row["metrica"]?.ToString()
                ?.ToLowerInvariant()
                ?? "";

            if (!metrica.Equals("distancia") &&
                !metrica.Equals("peso"))
            {
                continue;
            }

            double valor =
                Convert.ToDouble(
                    row["valor"]);

            DateTimeOffset data =
                ConverterData(
                    row["recebido_em"]);

            _dadosGrafico.Add(
                new LeituraGrafico
                {
                    Metrica = metrica,
                    Valor = valor,
                    Data = data
                });
        }

        _dadosGrafico =
            _dadosGrafico
                .OrderBy(x => x.Data)
                .ToList();

        _chartPanel.Invalidate();
    }

    // ============================================================
    // DESENHAR GRÁFICO
    // ============================================================

    private void DesenharGrafico(
        object? sender,
        PaintEventArgs e)
    {
        Graphics g = e.Graphics;

        g.SmoothingMode =
            SmoothingMode.AntiAlias;

        using var tituloFont =
            new Font(
                "Segoe UI",
                10,
                FontStyle.Bold);

        using var labelFont =
            new Font(
                "Segoe UI",
                8);

        using var textoBrush =
            new SolidBrush(
                Color.FromArgb(
                    55,
                    65,
                    81));

        using var linhaBrush =
            new Pen(
                Color.FromArgb(
                    220,
                    226,
                    232),
                1);

        using var distanciaPen =
            new Pen(
                Color.FromArgb(
                    35,
                    86,
                    139),
                2);

        using var pesoPen =
            new Pen(
                Color.FromArgb(
                    126,
                    91,
                    166),
                2);

        g.DrawString(
            "Histórico das leituras",
            tituloFont,
            textoBrush,
            18,
            8);

        if (_dadosGrafico.Count == 0)
        {
            g.DrawString(
                "Nenhuma leitura disponível para o gráfico.",
                labelFont,
                textoBrush,
                25,
                70);

            return;
        }

        int left = 70;
        int top = 42;
        int right = 25;
        int bottom = 42;

        int width =
            Math.Max(
                200,
                _chartPanel.ClientSize.Width -
                left -
                right);

        int height =
            Math.Max(
                100,
                _chartPanel.ClientSize.Height -
                top -
                bottom);

        Rectangle area =
            new Rectangle(
                left,
                top,
                width,
                height);

        double max =
            _dadosGrafico.Max(
                x => Math.Abs(x.Valor));

        if (max <= 0)
        {
            max = 1;
        }

        // Grade horizontal

        for (int i = 0; i <= 4; i++)
        {
            float y =
                area.Bottom -
                (area.Height *
                 i /
                 4f);

            g.DrawLine(
                linhaBrush,
                area.Left,
                y,
                area.Right,
                y);

            double valor =
                max *
                i /
                4.0;

            g.DrawString(
                valor.ToString("N1"),
                labelFont,
                textoBrush,
                18,
                y - 7);
        }

        // Separar as métricas

        var distancia =
            _dadosGrafico
                .Where(x =>
                    x.Metrica == "distancia")
                .ToList();

        var peso =
            _dadosGrafico
                .Where(x =>
                    x.Metrica == "peso")
                .ToList();

        DesenharLinha(
            g,
            distancia,
            distanciaPen,
            area,
            max);

        DesenharLinha(
            g,
            peso,
            pesoPen,
            area,
            max);

        // Legenda

        int legendaX = area.Left;

        using var distanciaBrush =
            new SolidBrush(
                Color.FromArgb(
                    35,
                    86,
                    139));

        using var pesoBrush =
            new SolidBrush(
                Color.FromArgb(
                    126,
                    91,
                    166));

        g.FillRectangle(
            distanciaBrush,
            legendaX,
            area.Bottom + 12,
            14,
            3);

        g.DrawString(
            "Distância",
            labelFont,
            textoBrush,
            legendaX + 20,
            area.Bottom + 6);

        g.FillRectangle(
            pesoBrush,
            legendaX + 110,
            area.Bottom + 12,
            14,
            3);

        g.DrawString(
            "Peso",
            labelFont,
            textoBrush,
            legendaX + 130,
            area.Bottom + 6);
    }

    private static void DesenharLinha(
        Graphics g,
        List<LeituraGrafico> dados,
        Pen pen,
        Rectangle area,
        double max)
    {
        if (dados.Count < 2)
        {
            return;
        }

        var pontos =
            new List<PointF>();

        for (int i = 0;
             i < dados.Count;
             i++)
        {
            float x;

            if (dados.Count == 1)
            {
                x = area.Left;
            }
            else
            {
                x =
                    area.Left +
                    (
                        area.Width *
                        i /
                        (float)(dados.Count - 1)
                    );
            }

            double proporcao =
                dados[i].Valor / max;

            proporcao =
                Math.Clamp(
                    proporcao,
                    0,
                    1);

            float y =
                area.Bottom -
                (float)(
                    area.Height *
                    proporcao);

            pontos.Add(
                new PointF(
                    x,
                    y));
        }

        if (pontos.Count >= 2)
        {
            g.DrawLines(
                pen,
                pontos.ToArray());
        }

        foreach (PointF ponto in pontos)
        {
            g.FillEllipse(
                new SolidBrush(
                    pen.Color),
                ponto.X - 3,
                ponto.Y - 3,
                6,
                6);
        }
    }

    // ============================================================
    // FORMATAÇÃO
    // ============================================================

    private static string FormatarMetrica(
        string metrica)
    {
        return metrica.ToLowerInvariant()
            switch
        {
            "distancia" => "Distância",
            "peso" => "Peso",
            _ => metrica
        };
    }

    private static string FormatarValor(
        double valor,
        string metrica)
    {
        if (metrica.Equals(
                "distancia",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{valor:N2} cm";
        }

        if (metrica.Equals(
                "peso",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{valor:N0} g";
        }

        return valor.ToString("N2");
    }

    private static DateTimeOffset ConverterData(
        object valor)
    {
        if (valor is DateTimeOffset dto)
        {
            return dto;
        }

        if (valor is DateTime dt)
        {
            return new DateTimeOffset(
                DateTime.SpecifyKind(
                    dt,
                    DateTimeKind.Local));
        }

        return DateTimeOffset.Parse(
            valor.ToString()
            ?? DateTimeOffset.Now.ToString());
    }

    // ============================================================
    // MODELO DO GRÁFICO
    // ============================================================

    private sealed class LeituraGrafico
    {
        public string Metrica { get; set; } = "";

        public double Valor { get; set; }

        public DateTimeOffset Data { get; set; }
    }
}