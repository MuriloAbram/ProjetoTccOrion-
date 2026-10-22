using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace OrionInc.Api.Services;

public sealed class OrionDb(string connectionString)
{
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var c = new NpgsqlConnection(connectionString); await c.OpenAsync(ct);
        await using var context = new NpgsqlCommand("SELECT set_config('orion.origem','API',false), set_config('orion.usuario_id','servico-api',false), set_config('orion.ip','',false)", c);
        await context.ExecuteNonQueryAsync(ct); return c;
    }
    public async Task<string?> AuthenticateDevice(HttpContext ctx, int sensorId)
    {
        var token = ctx.Request.Headers["X-Device-Token"].ToString(); if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await using var c = await OpenAsync(); await using var cmd = new NpgsqlCommand("SELECT codigo_dispositivo FROM dispositivos_iot WHERE token_hash=@hash AND id_sensor=@sensor AND ativo=TRUE", c);
        cmd.Parameters.AddWithValue("hash", hash); cmd.Parameters.AddWithValue("sensor", sensorId); return (string?)await cmd.ExecuteScalarAsync();
    }
}
