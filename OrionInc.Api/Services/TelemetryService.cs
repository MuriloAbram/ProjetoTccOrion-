using Npgsql;
using NpgsqlTypes;

namespace OrionInc.Api.Services;

public sealed class TelemetryService(OrionDb db, TxtAuditLogger audit)
{
    private static readonly HashSet<string> AllowedMetrics = new(StringComparer.OrdinalIgnoreCase)
    {
        "peso", "distancia"
    };

    private static readonly HashSet<string> AllowedUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "kg", "g", "cm", "m"
    };

    public async Task<TelemetryResult> ProcessarLeituraAsync(
        HttpContext ctx,
        int sensorId,
        ReadingRequest req,
        CancellationToken ct = default)
    {
        var device = await db.AuthenticateDevice(ctx, sensorId);
        if (device is null)
        {
            await audit.WriteAsync(
                "integracao-esp32",
                "LEITURA_NAO_AUTORIZADA",
                "SENSOR",
                sensorId.ToString(),
                "dispositivo-desconhecido",
                "ESP32/API",
                "FALHA",
                "Credencial inválida ou sensor não associado",
                ctx.Connection.RemoteIpAddress?.ToString());

            return TelemetryResult.Unauthorized();
        }

        var metric = req.Metric?.Trim().ToLowerInvariant();
        var unit = req.Unit?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(metric) || !AllowedMetrics.Contains(metric) ||
            string.IsNullOrWhiteSpace(unit) || !AllowedUnits.Contains(unit) ||
            !double.IsFinite(req.Value) || Math.Abs(req.Value) > 1_000_000_000 ||
            (req.Timestamp.HasValue && req.Timestamp.Value < DateTimeOffset.UtcNow.AddDays(-30)))
        {
            await audit.WriteAsync(
                "integracao-esp32",
                "LEITURA_INVALIDA",
                "SENSOR",
                sensorId.ToString(),
                device,
                "ESP32/API",
                "FALHA",
                $"Métrica/unidade inválidas ou valor fora do limite. Métrica={metric ?? "N/A"}; Unidade={unit ?? "N/A"}",
                ctx.Connection.RemoteIpAddress?.ToString());

            return TelemetryResult.BadRequest("Payload inválido. Métrica permitida: peso ou distancia; unidade permitida: kg, g, cm ou m.");
        }

        var received = DateTimeOffset.UtcNow;
        var eventId = string.IsNullOrWhiteSpace(req.EventId)
            ? Guid.NewGuid().ToString("N")
            : req.EventId.Trim();

        if (eventId.Length > 100)
            return TelemetryResult.BadRequest("EventId deve possuir no máximo 100 caracteres.");

        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);

        try
        {
            bool wasOffline;
            await using (var prior = new NpgsqlCommand(
                "SELECT lower(status_sensor)='offline' FROM sensores WHERE id_sensor=@id FOR UPDATE",
                c,
                tx))
            {
                prior.Parameters.AddWithValue("id", sensorId);
                var result = await prior.ExecuteScalarAsync(ct);
                if (result is null)
                    return TelemetryResult.NotFound("Sensor não encontrado.");

                wasOffline = (bool)result;
            }

            long? insertedId;
            await using (var cmd = new NpgsqlCommand(@"
                INSERT INTO leituras_sensores
                    (id_sensor, codigo_dispositivo, metrica, valor, unidade,
                     horario_dispositivo, recebido_em, validacao, id_evento)
                VALUES
                    (@sensor, @device, @metric, @value, @unit,
                     @deviceTime, @received, 'valida', @event)
                ON CONFLICT (id_evento) DO NOTHING
                RETURNING id_leitura;", c, tx))
            {
                cmd.Parameters.AddWithValue("sensor", sensorId);
                cmd.Parameters.AddWithValue("device", device);
                cmd.Parameters.AddWithValue("metric", metric);
                cmd.Parameters.AddWithValue("value", req.Value);
                cmd.Parameters.AddWithValue("unit", unit);
                cmd.Parameters.Add("deviceTime", NpgsqlDbType.TimestampTz).Value =
                    (object?)req.Timestamp?.UtcDateTime ?? DBNull.Value;
                cmd.Parameters.AddWithValue("received", received.UtcDateTime);
                cmd.Parameters.AddWithValue("event", eventId);

                insertedId = (long?)await cmd.ExecuteScalarAsync(ct);
            }

            await using (var hb = new NpgsqlCommand(@"
                UPDATE sensores
                   SET ultima_comunicacao=@now,
                       status_sensor=CASE
                           WHEN lower(status_sensor)='offline' THEN 'online'
                           ELSE status_sensor
                       END
                 WHERE id_sensor=@id;", c, tx))
            {
                hb.Parameters.AddWithValue("now", received.UtcDateTime);
                hb.Parameters.AddWithValue("id", sensorId);
                await hb.ExecuteNonQueryAsync(ct);
            }

            if (wasOffline)
            {
                await using var recovery = new NpgsqlCommand(@"
                    UPDATE historico_disponibilidade_sensor
                       SET fim_indisponibilidade=@now
                     WHERE id_sensor=@id
                       AND fim_indisponibilidade IS NULL;", c, tx);
                recovery.Parameters.AddWithValue("now", received.UtcDateTime);
                recovery.Parameters.AddWithValue("id", sensorId);
                await recovery.ExecuteNonQueryAsync(ct);

                await using var ev = new NpgsqlCommand(@"
                    INSERT INTO registros_sistema
                        (tipo_evento, descricao_evento, nivel_alerta, id_sensor)
                    VALUES
                        ('recuperacao', 'Sensor voltou a se comunicar com a API', 'baixo', @id);", c, tx);
                ev.Parameters.AddWithValue("id", sensorId);
                await ev.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);

            if (wasOffline)
            {
                await audit.WriteAsync(
                    "sensores",
                    "SENSOR_RECUPERADO",
                    "SENSOR",
                    sensorId.ToString(),
                    device,
                    "ESP32/API",
                    "SUCESSO",
                    "Sensor voltou a se comunicar",
                    ctx.Connection.RemoteIpAddress?.ToString());
            }

            await audit.WriteAsync(
                "integracao-esp32",
                "LEITURA_RECEBIDA",
                "SENSOR",
                sensorId.ToString(),
                device,
                "ESP32/API",
                "SUCESSO",
                $"Métrica {metric}; valor={req.Value}; unidade={unit}; idempotente={insertedId is null}",
                ctx.Connection.RemoteIpAddress?.ToString());

            return TelemetryResult.Ok(new
            {
                accepted = true,
                duplicate = insertedId is null,
                sensorId,
                receivedAt = received
            });
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task HeartbeatAsync(int sensorId, string device, CancellationToken ct = default)
    {
        await using var c = await db.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);

        try
        {
            bool wasOffline;
            await using (var prior = new NpgsqlCommand(
                "SELECT lower(status_sensor)='offline' FROM sensores WHERE id_sensor=@id FOR UPDATE",
                c,
                tx))
            {
                prior.Parameters.AddWithValue("id", sensorId);
                wasOffline = (bool?)await prior.ExecuteScalarAsync(ct) ?? false;
            }

            var now = DateTime.UtcNow;
            await using (var cmd = new NpgsqlCommand(@"
                UPDATE sensores
                   SET ultima_comunicacao=@now,
                       status_sensor=CASE
                           WHEN lower(status_sensor)='offline' THEN 'online'
                           ELSE status_sensor
                       END
                 WHERE id_sensor=@id;", c, tx))
            {
                cmd.Parameters.AddWithValue("now", now);
                cmd.Parameters.AddWithValue("id", sensorId);
                if (await cmd.ExecuteNonQueryAsync(ct) == 0)
                    throw new InvalidOperationException("Sensor não encontrado.");
            }

            if (wasOffline)
            {
                await using var recovery = new NpgsqlCommand(@"
                    UPDATE historico_disponibilidade_sensor
                       SET fim_indisponibilidade=@now
                     WHERE id_sensor=@id
                       AND fim_indisponibilidade IS NULL;", c, tx);
                recovery.Parameters.AddWithValue("now", now);
                recovery.Parameters.AddWithValue("id", sensorId);
                await recovery.ExecuteNonQueryAsync(ct);

                await using var ev = new NpgsqlCommand(@"
                    INSERT INTO registros_sistema
                        (tipo_evento, descricao_evento, nivel_alerta, id_sensor)
                    VALUES
                        ('recuperacao', 'Sensor voltou a se comunicar com a API', 'baixo', @id);", c, tx);
                ev.Parameters.AddWithValue("id", sensorId);
                await ev.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}

public sealed record TelemetryResult(int StatusCode, object? Body)
{
    public static TelemetryResult Ok(object body) => new(StatusCodes.Status200OK, body);
    public static TelemetryResult Unauthorized() => new(StatusCodes.Status401Unauthorized, null);
    public static TelemetryResult NotFound(string message) => new(StatusCodes.Status404NotFound, new { error = message });
    public static TelemetryResult BadRequest(string message) => new(StatusCodes.Status400BadRequest, new { error = message });
}
