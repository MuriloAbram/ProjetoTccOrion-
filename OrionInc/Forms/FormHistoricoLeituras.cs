using OrionInc.Data;

namespace OrionInc.Forms;

public sealed class FormHistoricoLeituras : Form
{
    private readonly int _sensorId;
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, RowHeadersVisible = false };
    public FormHistoricoLeituras(int sensorId)
    {
        _sensorId = sensorId; Text = $"Histórico de leituras | Sensor #{sensorId}"; Width = 1000; Height = 600; StartPosition = FormStartPosition.CenterParent; Controls.Add(_grid); OrionUi.StyleModuleControls(this); Shown += (_, _) => LoadReadings();
    }
    private void LoadReadings()
    {
        try { _grid.DataSource = DatabaseHelper.GetLeiturasSensor(_sensorId); }
        catch (Exception ex) { MessageBox.Show(this, "Não foi possível consultar leituras. A API precisa já ter recebido dados e a migração 03 precisa estar aplicada.\n" + ex.Message, "Histórico", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
