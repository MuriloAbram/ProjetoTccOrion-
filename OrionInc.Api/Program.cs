using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Npgsql;
using NpgsqlTypes;
using OrionInc.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var configured = builder.Configuration.GetConnectionString("Orion");
var envConnection = Environment.GetEnvironmentVariable("ORION_CONNECTION_STRING");
var connectionString = !string.IsNullOrWhiteSpace(envConnection) ? envConnection : configured;
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = "Host=localhost;Port=5432;Database=orion_inc;Username=postgres;Password=" + (Environment.GetEnvironmentVariable("ORION_DB_PASSWORD") ?? "");
builder.Services.AddSingleton(new OrionDb(connectionString));
builder.Services.AddSingleton<TxtAuditLogger>();
builder.Services.AddHostedService<SensorAvailabilityMonitor>();
builder.Services.AddHostedService<AuditTxtExportService>();
var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseRateLimiter();

app.MapGet("/api/v1/health", async (OrionDb db) => {
    try { await using var c = await db.OpenAsync(); await using var cmd = new NpgsqlCommand("SELECT 1", c); await cmd.ExecuteScalarAsync(); return Results.Ok(new { status = "healthy", database = "connected", serverTime = DateTimeOffset.Now }); }
    catch { return Results.Json(new { status = "degraded", database = "unavailable" }, statusCode: 503); }
});

app.MapPost("/api/v1/devices/register", async (HttpContext ctx, OrionDb db, IConfiguration cfg, ProvisionDeviceRequest req, TxtAuditLogger log) => {
    var expected = Environment.GetEnvironmentVariable("ORION_PROVISIONING_KEY") ?? cfg["Provisioning:ApiKey"];
    if (string.IsNullOrWhiteSpace(expected) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(ctx.Request.Headers["X-Provisioning-Key"].ToString()))) {
        await log.WriteAsync("integracao-esp32", "AUTENTICACAO_INVALIDA", "DISPOSITIVO", req.DeviceId, "desconhecido", "API", "FALHA", "Tentativa de provisionamento não autorizada", ctx.Connection.RemoteIpAddress?.ToString());
        return Results.Unauthorized();
    }
    if (string.IsNullOrWhiteSpace(req.DeviceId) || req.DeviceId.Length > 100 || req.SensorId <= 0) return Results.BadRequest(new { error = "deviceId e sensorId válidos são obrigatórios." });
    var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    await using var c = await db.OpenAsync();
    await using var tx = await c.BeginTransactionAsync();
    await using (var check = new NpgsqlCommand("SELECT 1 FROM sensores WHERE id_sensor=@id", c, tx)) { check.Parameters.AddWithValue("id", req.SensorId); if (await check.ExecuteScalarAsync() is null) return Results.NotFound(new { error = "Sensor não encontrado." }); }
    await using (var cmd = new NpgsqlCommand("INSERT INTO dispositivos_iot(codigo_dispositivo,id_sensor,token_hash,ativo) VALUES(@device,@sensor,@hash,TRUE) ON CONFLICT(codigo_dispositivo) DO UPDATE SET id_sensor=EXCLUDED.id_sensor,token_hash=EXCLUDED.token_hash,ativo=TRUE,atualizado_em=now()", c, tx)) {
        cmd.Parameters.AddWithValue("device", req.DeviceId); cmd.Parameters.AddWithValue("sensor", req.SensorId); cmd.Parameters.AddWithValue("hash", hash); await cmd.ExecuteNonQueryAsync(); }
    await tx.CommitAsync();
    await log.WriteAsync("integracao-esp32", "DISPOSITIVO_PROVISIONADO", "DISPOSITIVO_IOT", req.DeviceId, "administrador", "API", "SUCESSO", $"Dispositivo associado ao sensor {req.SensorId}", ctx.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(new { deviceId = req.DeviceId, sensorId = req.SensorId, token, warning = "Guarde o token agora; ele não será exibido novamente." });
});

app.MapPost("/api/v1/devices/{deviceId}/revoke", async (string deviceId, HttpContext ctx, OrionDb db, IConfiguration cfg, TxtAuditLogger log) => {
    var expected = Environment.GetEnvironmentVariable("ORION_PROVISIONING_KEY") ?? cfg["Provisioning:ApiKey"];
    var supplied = ctx.Request.Headers["X-Provisioning-Key"].ToString();
    if (string.IsNullOrWhiteSpace(expected) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied))) return Results.Unauthorized();
    await using var c = await db.OpenAsync(); await using var cmd = new NpgsqlCommand("UPDATE dispositivos_iot SET ativo=FALSE,atualizado_em=now() WHERE codigo_dispositivo=@device", c); cmd.Parameters.AddWithValue("device", deviceId); var count = await cmd.ExecuteNonQueryAsync();
    await log.WriteAsync("integracao-esp32","DISPOSITIVO_REVOGADO","DISPOSITIVO_IOT",deviceId,"administrador","API",count > 0 ? "SUCESSO" : "NAO_ENCONTRADO","Revogação de credencial do dispositivo",ctx.Connection.RemoteIpAddress?.ToString());
    if (count > 0) return Results.Ok(new { deviceId, revoked = true });
    return Results.NotFound(new { error = "Dispositivo não encontrado." });
});

app.MapPost("/api/v1/sensors/{id:int}/readings", async (int id, HttpContext ctx, OrionDb db, ReadingRequest req, TxtAuditLogger log) => {
    var device = await db.AuthenticateDevice(ctx, id);
    if (device is null) { await log.WriteAsync("integracao-esp32", "LEITURA_NAO_AUTORIZADA", "SENSOR", id.ToString(), "dispositivo-desconhecido", "ESP32/API", "FALHA", "Credencial inválida ou sensor não associado", ctx.Connection.RemoteIpAddress?.ToString()); return Results.Unauthorized(); }
    if (string.IsNullOrWhiteSpace(req.Metric) || req.Metric.Length > 100 || string.IsNullOrWhiteSpace(req.Unit) || req.Unit.Length > 30 || !double.IsFinite(req.Value) || Math.Abs(req.Value) > 1_000_000_000 || (req.Timestamp.HasValue && req.Timestamp.Value < DateTimeOffset.UtcNow.AddDays(-30))) return Results.BadRequest(new { error = "Payload de leitura inválido." });
    var received = DateTimeOffset.UtcNow;
    await using var c = await db.OpenAsync();
    await using var cmd = new NpgsqlCommand("INSERT INTO leituras_sensores(id_sensor,codigo_dispositivo,metrica,valor,unidade,horario_dispositivo,recebido_em,validacao,id_evento) VALUES(@sensor,@device,@metric,@value,@unit,@deviceTime,@received,'valida',@event) ON CONFLICT(id_evento) DO NOTHING RETURNING id_leitura", c);
    cmd.Parameters.AddWithValue("sensor", id); cmd.Parameters.AddWithValue("device", device); cmd.Parameters.AddWithValue("metric", req.Metric.Trim()); cmd.Parameters.AddWithValue("value", req.Value); cmd.Parameters.AddWithValue("unit", req.Unit.Trim()); cmd.Parameters.Add("deviceTime", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = (object?)req.Timestamp?.UtcDateTime ?? DBNull.Value; cmd.Parameters.AddWithValue("received", received.UtcDateTime); cmd.Parameters.AddWithValue("event", (object?)req.EventId ?? Guid.NewGuid().ToString("N"));
    var inserted = await cmd.ExecuteScalarAsync();
    bool wasOffline;
    await using (var prior = new NpgsqlCommand("SELECT lower(status_sensor)='offline' FROM sensores WHERE id_sensor=@id", c)) { prior.Parameters.AddWithValue("id", id); wasOffline = (bool?)await prior.ExecuteScalarAsync() ?? false; }
    await using (var hb = new NpgsqlCommand("UPDATE sensores SET ultima_comunicacao=@now, status_sensor=CASE WHEN lower(status_sensor)='offline' THEN 'online' ELSE status_sensor END WHERE id_sensor=@id", c)) { hb.Parameters.AddWithValue("now", received.UtcDateTime); hb.Parameters.AddWithValue("id", id); await hb.ExecuteNonQueryAsync(); }
    if (wasOffline) { await using var recovery = new NpgsqlCommand("UPDATE historico_disponibilidade_sensor SET fim_indisponibilidade=@now WHERE id_sensor=@id AND fim_indisponibilidade IS NULL", c); recovery.Parameters.AddWithValue("now", received.UtcDateTime); recovery.Parameters.AddWithValue("id", id); await recovery.ExecuteNonQueryAsync(); await using var ev = new NpgsqlCommand("INSERT INTO registros_sistema(tipo_evento,descricao_evento,nivel_alerta,id_sensor) VALUES('recuperacao','Sensor voltou a se comunicar com a API','baixo',@id)", c); ev.Parameters.AddWithValue("id", id); await ev.ExecuteNonQueryAsync(); await log.WriteAsync("sensores","SENSOR_RECUPERADO","SENSOR",id.ToString(),device,"ESP32/API","SUCESSO","Sensor voltou a se comunicar",ctx.Connection.RemoteIpAddress?.ToString()); }
    await log.WriteAsync("integracao-esp32", "LEITURA_RECEBIDA", "SENSOR", id.ToString(), device, "ESP32/API", "SUCESSO", $"Métrica {req.Metric}; valor={req.Value}; unidade={req.Unit}; idempotente={(inserted is null)}", ctx.Connection.RemoteIpAddress?.ToString());
    return Results.Ok(new { accepted = true, duplicate = inserted is null, sensorId = id, receivedAt = received });
});

app.MapPost("/api/v1/sensors/{id:int}/heartbeat", async (int id, HttpContext ctx, OrionDb db, TxtAuditLogger log) => {
    var device = await db.AuthenticateDevice(ctx, id); if (device is null) return Results.Unauthorized();
    await using var c = await db.OpenAsync();
    bool wasOffline; await using (var prior = new NpgsqlCommand("SELECT lower(status_sensor)='offline' FROM sensores WHERE id_sensor=@id", c)) { prior.Parameters.AddWithValue("id", id); wasOffline = (bool?)await prior.ExecuteScalarAsync() ?? false; }
    await using var cmd = new NpgsqlCommand("UPDATE sensores SET ultima_comunicacao=@now, status_sensor=CASE WHEN lower(status_sensor)='offline' THEN 'online' ELSE status_sensor END WHERE id_sensor=@id", c);
    cmd.Parameters.AddWithValue("now", DateTime.UtcNow); cmd.Parameters.AddWithValue("id", id); var count = await cmd.ExecuteNonQueryAsync(); if (count == 0) return Results.NotFound(new { error = "Sensor não encontrado." });
    if (wasOffline) { await using var recovery = new NpgsqlCommand("UPDATE historico_disponibilidade_sensor SET fim_indisponibilidade=@now WHERE id_sensor=@id AND fim_indisponibilidade IS NULL", c); recovery.Parameters.AddWithValue("now", DateTime.UtcNow); recovery.Parameters.AddWithValue("id", id); await recovery.ExecuteNonQueryAsync(); await using var ev = new NpgsqlCommand("INSERT INTO registros_sistema(tipo_evento,descricao_evento,nivel_alerta,id_sensor) VALUES('recuperacao','Sensor voltou a se comunicar com a API','baixo',@id)", c); ev.Parameters.AddWithValue("id", id); await ev.ExecuteNonQueryAsync(); await log.WriteAsync("sensores","SENSOR_RECUPERADO","SENSOR",id.ToString(),device,"ESP32/API","SUCESSO","Sensor voltou a se comunicar",ctx.Connection.RemoteIpAddress?.ToString()); }
    await log.WriteAsync("sensores", "HEARTBEAT", "SENSOR", id.ToString(), device, "ESP32/API", "SUCESSO", "Heartbeat recebido", ctx.Connection.RemoteIpAddress?.ToString()); return Results.Ok(new { sensorId = id, status = "online", receivedAt = DateTimeOffset.UtcNow });
});

app.MapGet("/api/v1/sensors/{id:int}/status", async (int id, HttpContext ctx, OrionDb db) => {
    var device = await db.AuthenticateDevice(ctx, id); if (device is null) return Results.Unauthorized();
    await using var c = await db.OpenAsync(); await using var cmd = new NpgsqlCommand("SELECT id_sensor,status_sensor,ultima_comunicacao FROM sensores WHERE id_sensor=@id", c); cmd.Parameters.AddWithValue("id", id); await using var r = await cmd.ExecuteReaderAsync();
    if (!await r.ReadAsync()) return Results.NotFound(); return Results.Ok(new { sensorId = r.GetInt32(0), status = r.GetString(1), lastCommunication = r.IsDBNull(2) ? null : r.GetDateTime(2) });
});

app.MapGet("/api/v1/sensors/{id:int}/readings", async (int id, HttpContext ctx, OrionDb db, int page = 1, int pageSize = 50, string? metric = null) => {
    if (await db.AuthenticateDevice(ctx, id) is null) return Results.Unauthorized(); page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
    await using var c = await db.OpenAsync(); await using var cmd = new NpgsqlCommand("SELECT id_leitura,metrica,valor,unidade,horario_dispositivo,recebido_em,validacao FROM leituras_sensores WHERE id_sensor=@id AND (@metric IS NULL OR metrica=@metric) ORDER BY recebido_em DESC LIMIT @limit OFFSET @offset", c);
    cmd.Parameters.AddWithValue("id", id); cmd.Parameters.Add("metric", NpgsqlTypes.NpgsqlDbType.Varchar).Value = (object?)metric ?? DBNull.Value; cmd.Parameters.AddWithValue("limit", pageSize); cmd.Parameters.AddWithValue("offset", (page-1)*pageSize); var rows = new List<object>(); await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync()) rows.Add(new { id = r.GetInt64(0), metric = r.GetString(1), value = r.GetDouble(2), unit = r.GetString(3), deviceTimestamp = r.IsDBNull(4) ? null : r.GetDateTime(4), receivedAt = r.GetDateTime(5), validation = r.GetString(6) }); return Results.Ok(new { page, pageSize, items = rows });
});

app.MapPost("/api/v1/sensors/{id:int}/events", async (int id, HttpContext ctx, OrionDb db, DeviceEventRequest req, TxtAuditLogger log) => {
    var device = await db.AuthenticateDevice(ctx, id); if (device is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(req.Type) || req.Type.Length > 50 || string.IsNullOrWhiteSpace(req.Description) || req.Description.Length > 1000) return Results.BadRequest(new { error = "Evento inválido." });
    await using var c = await db.OpenAsync(); await using var cmd = new NpgsqlCommand("INSERT INTO registros_sistema(tipo_evento,descricao_evento,nivel_alerta,id_sensor) VALUES(@type,@description,@level,@sensor) RETURNING id_registro", c);
    cmd.Parameters.AddWithValue("type", req.Type); cmd.Parameters.AddWithValue("description", req.Description.Replace("\r", " ").Replace("\n", " ")); cmd.Parameters.AddWithValue("level", string.IsNullOrWhiteSpace(req.Level) ? "medio" : req.Level); cmd.Parameters.AddWithValue("sensor", id); var eventId = await cmd.ExecuteScalarAsync();
    await log.WriteAsync("sensores", "EVENTO_DISPOSITIVO", "SENSOR", id.ToString(), device, "ESP32/API", "SUCESSO", $"{req.Type}: {req.Description}", ctx.Connection.RemoteIpAddress?.ToString()); return Results.Accepted(value: new { id = eventId });
});
app.Run();

record ProvisionDeviceRequest(string DeviceId, int SensorId);
record ReadingRequest(string Metric, double Value, string Unit, DateTimeOffset? Timestamp, string? EventId);
record DeviceEventRequest(string Type, string Description, string? Level);
