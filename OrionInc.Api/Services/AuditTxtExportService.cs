using System.Text;
using Npgsql;

namespace OrionInc.Api.Services;

/// <summary>Exporta auditoria do PostgreSQL para TXT, incluindo operações do desktop e banco direto.</summary>
public sealed class AuditTxtExportService(OrionDb db, TxtAuditLogger audit, ILogger<AuditTxtExportService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "logs"); Directory.CreateDirectory(directory);
        var cursorPath = Path.Combine(directory, "auditoria.cursor"); long lastId;
        if (File.Exists(cursorPath) && long.TryParse(await File.ReadAllTextAsync(cursorPath, stoppingToken), out var saved)) lastId = saved;
        else
        {
            // Primeira execução começa no topo atual; não despeja todo o histórico legado em TXT.
            try { await using var c = await db.OpenAsync(stoppingToken); await using var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(id_evento),0) FROM auditoria_eventos", c); lastId = Convert.ToInt64(await cmd.ExecuteScalarAsync(stoppingToken)); await File.WriteAllTextAsync(cursorPath, lastId.ToString(), stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Não foi possível inicializar o cursor de auditoria TXT"); return; }
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var c = await db.OpenAsync(stoppingToken);
                await using var cmd = new NpgsqlCommand("SELECT id_evento,ocorrido_em,tipo_evento,entidade,entidade_id,responsavel,origem,resultado,descricao,ip_origem::text FROM auditoria_eventos WHERE id_evento>@last AND origem NOT IN ('API','DESKTOP_LOGIN') ORDER BY id_evento LIMIT 200", c); cmd.Parameters.AddWithValue("last", lastId);
                var events = new List<(long id, DateTimeOffset at, string type, string entity, string? entityId, string actor, string origin, string result, string description, string? ip)>();
                await using (var r = await cmd.ExecuteReaderAsync(stoppingToken)) while (await r.ReadAsync(stoppingToken)) events.Add((r.GetInt64(0), new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(1), DateTimeKind.Utc)), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)));
                foreach (var e in events) { await audit.WriteAsync("auditoria", e.type, e.entity, e.entityId, e.actor, e.origin, e.result, e.description + $" | ocorridoNoBanco={e.at:O}", e.ip); lastId = e.id; await File.WriteAllTextAsync(cursorPath, lastId.ToString(), stoppingToken); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Falha ao exportar auditoria do banco para TXT"); }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
