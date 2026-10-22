using Npgsql;

namespace OrionInc.Api.Services;

public sealed class SensorAvailabilityMonitor(OrionDb db, TxtAuditLogger audit, IConfiguration config, ILogger<SensorAvailabilityMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = Math.Clamp(config.GetValue("Telemetry:MonitorIntervalSeconds", 30), 5, 3600);
        var timeout = Math.Clamp(config.GetValue("Telemetry:OfflineAfterSeconds", 180), 15, 86400);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var c = await db.OpenAsync(stoppingToken); await using var tx = await c.BeginTransactionAsync(stoppingToken);
                const string sql = @"WITH changed AS (UPDATE sensores SET status_sensor='offline' WHERE (lower(status_sensor) NOT IN ('offline','falha','manutencao','manutenção') OR (ultima_comunicacao IS NULL AND NOT EXISTS (SELECT 1 FROM historico_disponibilidade_sensor h WHERE h.id_sensor=sensores.id_sensor AND h.fim_indisponibilidade IS NULL))) AND (ultima_comunicacao IS NULL OR ultima_comunicacao < now() - (COALESCE(timeout_comunicacao_segundos,@timeout) * interval '1 second')) RETURNING id_sensor, ultima_comunicacao), downtime AS (INSERT INTO historico_disponibilidade_sensor(id_sensor,inicio_indisponibilidade,motivo) SELECT id_sensor,now(),CASE WHEN ultima_comunicacao IS NULL THEN 'Sensor nunca se comunicou com a API' ELSE 'Timeout de comunicação; última comunicação: ' || ultima_comunicacao::text END FROM changed RETURNING id_sensor), events AS (INSERT INTO registros_sistema(tipo_evento,descricao_evento,nivel_alerta,id_sensor) SELECT 'falha', CASE WHEN ultima_comunicacao IS NULL THEN 'Sensor nunca se comunicou com a API' ELSE 'Sensor perdeu comunicação; última comunicação: ' || ultima_comunicacao::text END, 'alto', id_sensor FROM changed RETURNING id_sensor) SELECT changed.id_sensor, CASE WHEN changed.ultima_comunicacao IS NULL THEN 'Sensor nunca se comunicou com a API' ELSE 'Sensor perdeu comunicação; última comunicação: ' || changed.ultima_comunicacao::text END FROM changed";
                await using var cmd = new NpgsqlCommand(sql, c, tx); cmd.Parameters.AddWithValue("timeout", timeout); var changed = new List<(int id,string desc)>(); await using(var r=await cmd.ExecuteReaderAsync(stoppingToken)) while(await r.ReadAsync(stoppingToken)) changed.Add((r.GetInt32(0),r.GetString(1)));
                await tx.CommitAsync(stoppingToken);
                foreach(var item in changed) await audit.WriteAsync("sensores","SENSOR_OFFLINE","SENSOR",item.id.ToString(),"monitor-de-disponibilidade","SISTEMA","SUCESSO",item.desc,null);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(Exception ex) { logger.LogError(ex,"Falha ao monitorar disponibilidade dos sensores"); try { await audit.WriteAsync("erros","MONITOR_SENSORES_ERRO","SISTEMA",null,"monitor-de-disponibilidade","SISTEMA","FALHA",ex.Message,null); } catch(Exception logEx) { logger.LogCritical(logEx,"Também falhou a persistência TXT do erro do monitor"); } }
            await Task.Delay(TimeSpan.FromSeconds(interval),stoppingToken);
        }
    }
}
