using System.Data;
using Npgsql;
using OrionInc.Data;

namespace OrionInc.Forms;

public sealed class FormHistoricoLeituras : Form
{
    private readonly int _sensorId;
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, RowHeadersVisible = false };
    public FormHistoricoLeituras(int sensorId)
    {
        _sensorId = sensorId; Text = $"Histórico de leituras | Sensor #{sensorId}"; Width = 1000; Height = 600; StartPosition = FormStartPosition.CenterParent; Controls.Add(_grid); Shown += (_, _) => LoadReadings();
    }
    private void LoadReadings()
    {
        try { using var c = DatabaseHelper.GetConnection(); using var cmd = new NpgsqlCommand("SELECT recebido_em AS \"Recebido em\",horario_dispositivo AS \"Horário do dispositivo\",metrica AS \"Métrica\",valor AS \"Valor\",unidade AS \"Unidade\",codigo_dispositivo AS \"Dispositivo\",validacao AS \"Validação\" FROM leituras_sensores WHERE id_sensor=@id ORDER BY recebido_em DESC LIMIT 2000", c); cmd.Parameters.AddWithValue("id", _sensorId); using var da = new NpgsqlDataAdapter(cmd); var dt = new DataTable(); da.Fill(dt); _grid.DataSource = dt; }
        catch (Exception ex) { MessageBox.Show(this, "Não foi possível consultar leituras. A API precisa já ter recebido dados e a migração 03 precisa estar aplicada.\n" + ex.Message, "Histórico", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
