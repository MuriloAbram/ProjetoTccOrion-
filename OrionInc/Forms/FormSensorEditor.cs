using Npgsql;
using NpgsqlTypes;
using OrionInc.Data;

namespace OrionInc.Forms;

public sealed class FormSensorEditor : Form
{
    private readonly int? _id;
    private readonly TextBox _type = new() { Width = 260 };
    private readonly TextBox _model = new() { Width = 260 };
    private readonly TextBox _gateway = new() { Width = 100, PlaceholderText = "Opcional" };
    private readonly TextBox _area = new() { Width = 100, PlaceholderText = "Opcional" };
    private readonly TextBox _wagon = new() { Width = 100, PlaceholderText = "Opcional" };
    private readonly ComboBox _status = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    public FormSensorEditor(int? id = null)
    {
        _id = id; Text = id.HasValue ? $"Editar sensor #{id}" : "Cadastrar sensor"; Width = 460; Height = 400; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        _status.Items.AddRange(new object[] { "online", "offline", "falha", "manutencao" }); _status.SelectedItem = "offline";
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, 0, "Tipo do sensor", _type); AddRow(layout, 1, "Modelo", _model); AddRow(layout, 2, "Status", _status); AddRow(layout, 3, "ID Gateway", _gateway); AddRow(layout, 4, "ID Área de risco", _area); AddRow(layout, 5, "ID Vagão", _wagon);
        var save = new Button { Text = "Salvar", Width = 100, Height = 32 }; save.Click += (_, _) => Save(); var cancel = new Button { Text = "Cancelar", Width = 100, Height = 32 }; cancel.Click += (_, _) => Close(); var buttons = new FlowLayoutPanel { AutoSize = true }; buttons.Controls.AddRange(new Control[] { save, cancel }); layout.Controls.Add(buttons, 1, 6); Controls.Add(layout);
        if (_id.HasValue) LoadSensor();
    }
    private static void AddRow(TableLayoutPanel layout, int row, string label, Control input) { layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft }, 0, row); input.Anchor = AnchorStyles.Left; layout.Controls.Add(input, 1, row); }
    private void LoadSensor()
    {
        try { using var c = DatabaseHelper.GetConnection(); using var cmd = new NpgsqlCommand("SELECT tipo_sensor,modelo_sensor,status_sensor,id_gateway,id_area,id_vagao FROM sensores WHERE id_sensor=@id", c); cmd.Parameters.AddWithValue("id", _id!.Value); using var r = cmd.ExecuteReader(); if (!r.Read()) { MessageBox.Show("Sensor não encontrado."); Close(); return; } _type.Text = r.GetString(0); _model.Text = r.GetString(1); _status.SelectedItem = _status.Items.Cast<string>().FirstOrDefault(x => x.Equals(r.GetString(2), StringComparison.OrdinalIgnoreCase)) ?? "offline"; _gateway.Text = r.IsDBNull(3) ? "" : r.GetInt32(3).ToString(); _area.Text = r.IsDBNull(4) ? "" : r.GetInt32(4).ToString(); _wagon.Text = r.IsDBNull(5) ? "" : r.GetInt32(5).ToString(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Sensor", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private static object NullableId(string text, string label) { if (string.IsNullOrWhiteSpace(text)) return DBNull.Value; if (int.TryParse(text, out var id) && id > 0) return id; throw new ArgumentException($"{label} deve ser um ID inteiro positivo ou ficar vazio."); }
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_type.Text) || string.IsNullOrWhiteSpace(_model.Text)) { MessageBox.Show(this, "Tipo e modelo são obrigatórios."); return; }
        try { using var c = DatabaseHelper.GetConnection(); using var cmd = new NpgsqlCommand(_id.HasValue ? "UPDATE sensores SET tipo_sensor=@type,modelo_sensor=@model,status_sensor=@status,id_gateway=@gateway,id_area=@area,id_vagao=@wagon WHERE id_sensor=@id" : "INSERT INTO sensores(tipo_sensor,modelo_sensor,status_sensor,id_gateway,id_area,id_vagao) VALUES(@type,@model,@status,@gateway,@area,@wagon)", c);
            cmd.Parameters.AddWithValue("type", _type.Text.Trim()); cmd.Parameters.AddWithValue("model", _model.Text.Trim()); cmd.Parameters.AddWithValue("status", _status.SelectedItem?.ToString() ?? "offline"); cmd.Parameters.Add("gateway", NpgsqlDbType.Integer).Value = NullableId(_gateway.Text, "Gateway"); cmd.Parameters.Add("area", NpgsqlDbType.Integer).Value = NullableId(_area.Text, "Área"); cmd.Parameters.Add("wagon", NpgsqlDbType.Integer).Value = NullableId(_wagon.Text, "Vagão"); if (_id.HasValue) cmd.Parameters.AddWithValue("id", _id.Value); cmd.ExecuteNonQuery(); DialogResult = DialogResult.OK; Close(); }
        catch (Exception ex) { MessageBox.Show(this, "Não foi possível salvar. Confirme se os IDs de gateway, área e vagão existem.\n" + ex.Message, "Sensor", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
