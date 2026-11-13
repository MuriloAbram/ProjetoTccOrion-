namespace OrionInc.Api.Services;

public sealed record ProvisionDeviceRequest(string DeviceId, int SensorId);
public sealed record ReadingRequest(string Metric, double Value, string Unit, DateTimeOffset? Timestamp, string? EventId);
public sealed record DeviceEventRequest(string Type, string Description, string? Level);
