using Npgsql;
using OrionInc.Data;

namespace OrionInc.Forms;

public sealed class FormDashboard : Form
{
    private readonly FlowLayoutPanel _metrics = new() { Dock = DockStyle.Top, Height = 245, Padding = new Padding(18), AutoScroll = true, WrapContents = true, BackColor = Color.FromArgb(243, 246, 250) };
    private readonly Panel _statusChart = new() { Dock = DockStyle.Top, Height = 135, BackColor = Color.White, Padding = new Padding(18) };
    private int[] _chartValues = new int[3];
    private readonly DataGridView _activity = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    private readonly Label _updated = new() { AutoSize = true, ForeColor = Color.DimGray };
    public FormDashboard()
    {
        Text = "Orion Inc | Dashboard operacional"; WindowState = FormWindowState.Maximized; MinimumSize = new Size(950, 650); BackColor = Color.White;
        var header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.FromArgb(20, 36, 59), Padding = new Padding(22, 12, 18, 8) };
        var title = new Label { Text = "ORION INC  /  CENTRAL DE MONITORAMENTO", ForeColor = Color.White, Font = new Font("Segoe UI", 17, FontStyle.Bold), AutoSize = true, Location = new Point(20, 10) };
        var welcome = new Label { Text = $"{Sessao.NomeUsuario}  •  {Sessao.NivelAcesso}", ForeColor = Color.FromArgb(205, 220, 238), AutoSize = true, Location = new Point(23, 47) };
        var btnRefresh = new Button { Text = "Atualizar", Width = 100, Height = 34, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(Width - 145, 20) };
        btnRefresh.Click += (_, _) => LoadDashboard(); header.Controls.AddRange(new Control[] { title, welcome, btnRefresh });
        var activityHeader = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(20, 10, 20, 5) };
        activityHeader.Controls.Add(new Label { Text = "Atividades recentes e eventos operacionais", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, Location = new Point(20, 12) });
        _updated.Location = new Point(500, 15); activityHeader.Controls.Add(_updated);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(16, 8, 8, 8), FlowDirection = FlowDirection.LeftToRight, BackColor = Color.FromArgb(248, 249, 251) };
        var modules = new Button { Text = "Abrir módulos", Width = 130, Height = 34 }; modules.Click += (_, _) => { using var f = new FormMenu(); f.ShowDialog(this); if (Sessao.IdUsuario == 0) { Close(); return; } LoadDashboard(); };
        var sensors = new Button { Text = "Sensores", Width = 110, Height = 34 }; sensors.Click += (_, _) => { using var f = new FormSensores(); f.ShowDialog(this); LoadDashboard(); };
        var audit = new Button { Text = "Auditoria", Width = 110, Height = 34 }; audit.Click += (_, _) => { using var f = new FormAuditoria(); f.ShowDialog(this); };
        var logout = new Button { Text = "Sair", Width = 90, Height = 34, BackColor = Color.FromArgb(170, 56, 56), ForeColor = Color.White }; logout.Click += (_, _) => { Sessao.Limpar(); Close(); };
        footer.Controls.AddRange(new Control[] { modules, sensors, audit, logout });
        _statusChart.Paint += DrawStatusChart;
        Controls.Add(_activity); Controls.Add(activityHeader); Controls.Add(_statusChart); Controls.Add(_metrics); Controls.Add(footer); Controls.Add(header);
        Shown += (_, _) => LoadDashboard();
    }

    private void LoadDashboard()
    {
        try
        {
            using var c = DatabaseHelper.GetConnection();
            using (var cmd = new NpgsqlCommand("SELECT * FROM vw_dashboard_orion", c))
            using (var r = cmd.ExecuteReader())
            {
                _metrics.Controls.Clear();
                if (r.Read())
                {
                    string[] labels = { "Total de usuários", "Funcionários ativos", "Vagões", "Sensores", "Sensores online", "Sensores offline", "Sensores em falha", "Gateways online", "Gateways offline", "Áreas ativas", "Alertas críticos", "Eventos pendentes", "Chamados abertos", "Manutenções pendentes" };
                    for (int i = 0; i < labels.Length && i < r.FieldCount; i++) AddMetric(labels[i], Convert.ToString(r.GetValue(i)) ?? "0", i is 5 or 6 or 8 or 10 ? Color.FromArgb(177, 55, 55) : i == 4 ? Color.FromArgb(31, 133, 92) : Color.FromArgb(35, 86, 139));
                    _chartValues = new[] { Convert.ToInt32(r.GetValue(4)), Convert.ToInt32(r.GetValue(5)), Convert.ToInt32(r.GetValue(6)) };
                    _statusChart.Invalidate();
                }
            }
            var table = new System.Data.DataTable();
            using (var cmd = new NpgsqlCommand("SELECT ocorrido_em AS \"Data/Hora\", tipo_evento AS \"Evento\", entidade AS \"Entidade\", entidade_id AS \"ID\", responsavel AS \"Responsável\", origem AS \"Origem\", descricao AS \"Descrição\" FROM auditoria_eventos ORDER BY ocorrido_em DESC LIMIT 30", c))
            using (var adapter = new NpgsqlDataAdapter(cmd)) adapter.Fill(table);
            _activity.DataSource = table;
            _updated.Text = "Atualizado: " + DateTimeOffset.Now.ToString("dd/MM/yyyy HH:mm:ss zzz");
        }
        catch (Exception ex) { MessageBox.Show(this, "Não foi possível carregar os indicadores. Confira se executou database/03_orion_api_auditoria.sql e se o PostgreSQL está acessível.\n\n" + ex.Message, "Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }


    private void DrawStatusChart(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var titleFont = new Font("Segoe UI", 10, FontStyle.Bold); using var labelFont = new Font("Segoe UI", 9);
        using var onlineBrush = new SolidBrush(Color.FromArgb(31, 133, 92)); using var offlineBrush = new SolidBrush(Color.FromArgb(211, 139, 35)); using var failBrush = new SolidBrush(Color.FromArgb(177, 55, 55)); using var textBrush = new SolidBrush(Color.FromArgb(55, 65, 81)); using var trackBrush = new SolidBrush(Color.FromArgb(235, 239, 244));
        g.DrawString("Distribuição de sensores por status", titleFont, textBrush, 18, 8);
        string[] labels = { "Online", "Offline", "Falha" }; Brush[] brushes = { onlineBrush, offlineBrush, failBrush };
        int max = Math.Max(1, _chartValues.Max()); int left = 125; int maxWidth = Math.Max(100, _statusChart.ClientSize.Width - left - 100);
        for (int i = 0; i < labels.Length; i++) { int y = 37 + i * 29; g.DrawString(labels[i], labelFont, textBrush, 18, y); g.FillRectangle(trackBrush, left, y + 2, maxWidth, 16); int width = (int)(maxWidth * (_chartValues[i] / (double)max)); if (width > 0) g.FillRectangle(brushes[i], left, y + 2, width, 16); g.DrawString(_chartValues[i].ToString(), labelFont, textBrush, left + maxWidth + 10, y); }
    }

    private void AddMetric(string label, string value, Color accent)
    {
        var card = new Panel { Width = 205, Height = 91, Margin = new Padding(7), BackColor = Color.White, Padding = new Padding(12), BorderStyle = BorderStyle.FixedSingle };
        card.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent });
        card.Controls.Add(new Label { Text = value, Font = new Font("Segoe UI", 23, FontStyle.Bold), ForeColor = accent, AutoSize = true, Location = new Point(20, 7) });
        card.Controls.Add(new Label { Text = label, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(75, 85, 99), AutoSize = true, Location = new Point(21, 57) }); _metrics.Controls.Add(card);
    }
}
