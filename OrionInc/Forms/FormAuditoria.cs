using System.Data;
using System.Text;
using Npgsql;
using OrionInc.Data;

namespace OrionInc.Forms;

public sealed class FormAuditoria : Form
{
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, RowHeadersVisible = false };
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(-7) };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    private readonly TextBox _entity = new() { Width = 130, PlaceholderText = "Entidade" };
    private readonly TextBox _actor = new() { Width = 140, PlaceholderText = "Responsável" };
    private DataTable _current = new();
    public FormAuditoria()
    {
        Text = "Orion Inc | Auditoria"; Width = 1250; Height = 700; StartPosition = FormStartPosition.CenterParent;
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, Padding = new Padding(10), BackColor = Color.FromArgb(243,246,250) };
        var search = new Button { Text = "Filtrar", Width = 85, Height = 28 }; search.Click += (_, _) => LoadAudit(); var export = new Button { Text = "Exportar TXT", Width = 100, Height = 28 }; export.Click += (_, _) => ExportTxt();
        top.Controls.AddRange(new Control[] { new Label { Text = "De", AutoSize = true, Padding = new Padding(0,7,0,0) }, _from, new Label { Text = "Até", AutoSize = true, Padding = new Padding(0,7,0,0) }, _to, _entity, _actor, search, export });
        Controls.Add(_grid); Controls.Add(top); Shown += (_, _) => LoadAudit();
    }
    private void LoadAudit()
    {
        try { using var c = DatabaseHelper.GetConnection(); using var cmd = new NpgsqlCommand("SELECT ocorrido_em AS \"Data/Hora\",tipo_evento AS \"Evento\",entidade AS \"Entidade\",entidade_id AS \"ID\",responsavel AS \"Responsável\",origem AS \"Origem\",resultado AS \"Resultado\",descricao AS \"Descrição\",valores_anteriores::text AS \"Antes\",valores_novos::text AS \"Depois\",ip_origem::text AS \"IP\" FROM auditoria_eventos WHERE ocorrido_em >= @from AND ocorrido_em < @to AND (@entity='' OR entidade ILIKE @entityLike) AND (@actor='' OR responsavel ILIKE @actorLike) ORDER BY ocorrido_em DESC LIMIT 5000", c); cmd.Parameters.AddWithValue("from", DateTime.SpecifyKind(_from.Value.Date, DateTimeKind.Utc)); cmd.Parameters.AddWithValue("to", DateTime.SpecifyKind(_to.Value.Date.AddDays(1), DateTimeKind.Utc)); cmd.Parameters.AddWithValue("entity", _entity.Text.Trim()); cmd.Parameters.AddWithValue("entityLike", "%"+_entity.Text.Trim()+"%"); cmd.Parameters.AddWithValue("actor", _actor.Text.Trim()); cmd.Parameters.AddWithValue("actorLike", "%"+_actor.Text.Trim()+"%"); _current = new DataTable(); using var da = new NpgsqlDataAdapter(cmd); da.Fill(_current); _grid.DataSource = _current; }
        catch (Exception ex) { MessageBox.Show(this, "Não foi possível consultar auditoria. Execute a migração 03.\n" + ex.Message, "Auditoria", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void ExportTxt()
    {
        using var dialog = new SaveFileDialog { Filter = "Arquivo TXT (*.txt)|*.txt", FileName = $"auditoria-{DateTime.Now:yyyy-MM-dd-HHmm}.txt" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var sb = new StringBuilder(); foreach (DataRow row in _current.Rows) { sb.AppendLine(new string('=',68)); foreach (DataColumn col in _current.Columns) sb.AppendLine($"{col.ColumnName}: {Convert.ToString(row[col])?.Replace("\r"," ").Replace("\n"," ")}"); sb.AppendLine(new string('=',68)); }
        File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(false)); MessageBox.Show(this, $"{_current.Rows.Count} registro(s) exportado(s).", "Auditoria", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
