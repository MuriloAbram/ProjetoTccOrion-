using System.Text;

namespace OrionInc.Services;

/// <summary>
/// Escrita serializada de auditoria em TXT com rotação diária.
/// O formato é propositalmente detalhado para permitir rastreabilidade fora do PostgreSQL.
/// </summary>
public static class TxtAuditLogger
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string Root =
        Path.Combine(AppContext.BaseDirectory, "logs");

    public static async Task WriteAsync(
        string category,
        string type,
        string entity,
        string? entityId,
        string actor,
        string origin,
        string result,
        string description,
        string? ip = null,
        string? module = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Root);

        var now = DateTimeOffset.Now;
        string safeCategory = Sanitize(category).ToLowerInvariant();
        string path = Path.Combine(
            Root,
            $"{safeCategory}-{now:yyyy-MM-dd}.txt");

        var lines = new List<string>
        {
            new string('=', 78),
            "EVENTO DE AUDITORIA",
            new string('=', 78),
            $"ID DO EVENTO LOCAL: {Guid.NewGuid():N}",
            $"DATA/HORA: {now:yyyy-MM-dd HH:mm:ss.fff zzz}",
            $"DATA/HORA UTC: {now.UtcDateTime:yyyy-MM-dd HH:mm:ss.fff}Z",
            $"TIPO DE EVENTO: {Sanitize(type)}",
            $"ENTIDADE: {Sanitize(entity)}",
            $"ID DA ENTIDADE: {Sanitize(entityId ?? "N/A")}",
            $"USUÁRIO RESPONSÁVEL: {Sanitize(actor)}",
            $"ORIGEM: {Sanitize(origin)}",
            $"MÓDULO/TELA: {Sanitize(module ?? "N/A")}",
            $"RESULTADO: {Sanitize(result)}",
            $"COMPUTADOR: {Sanitize(Environment.MachineName)}",
            $"IP: {Sanitize(ip ?? "N/A")}",
            $"DESCRIÇÃO: {Sanitize(description)}"
        };

        if (!string.IsNullOrWhiteSpace(details))
        {
            lines.Add("DETALHES:");
            lines.Add(Sanitize(details));
        }

        lines.Add(new string('=', 78));
        lines.Add(string.Empty);

        string entry = string.Join(Environment.NewLine, lines);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(
                path,
                entry,
                new UTF8Encoding(false),
                cancellationToken);
        }
        catch (Exception ex)
        {
            throw new IOException(
                $"Não foi possível persistir o log em '{path}'.",
                ex);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string Sanitize(string value)
    {
        return value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\0", " ")
            .Trim();
    }
}
