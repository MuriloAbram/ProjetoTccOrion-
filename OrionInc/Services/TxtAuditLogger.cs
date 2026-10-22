using System.Text;

namespace OrionInc.Services;

/// <summary>Escrita serializada de auditoria em arquivos TXT com rotação diária.</summary>
public static class TxtAuditLogger
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "logs");

    public static async Task WriteAsync(string category, string type, string entity, string? entityId,
        string actor, string origin, string result, string description, string? ip = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.Now;
        string safeCategory = Sanitize(category).ToLowerInvariant();
        string path = Path.Combine(Root, $"{safeCategory}-{now:yyyy-MM-dd}.txt");
        string entry = string.Join(Environment.NewLine, new[] {
            new string('=', 68), $"DATA/HORA: {now:yyyy-MM-dd'T'HH:mm:ss.fffzzz}",
            $"TIPO: {Sanitize(type)}", $"ENTIDADE: {Sanitize(entity)}", $"ID: {Sanitize(entityId ?? "N/A")}",
            $"RESPONSAVEL: {Sanitize(actor)}", $"ORIGEM: {Sanitize(origin)}", $"RESULTADO: {Sanitize(result)}",
            $"IP: {Sanitize(ip ?? "N/A")}", $"DESCRICAO: {Sanitize(description)}", new string('=', 68), ""
        });
        await Gate.WaitAsync(cancellationToken);
        try { await File.AppendAllTextAsync(path, entry, new UTF8Encoding(false), cancellationToken); }
        catch (Exception ex) { throw new IOException($"Não foi possível persistir o log em '{path}'.", ex); }
        finally { Gate.Release(); }
    }

    private static string Sanitize(string value) => value.Replace("\r", " ").Replace("\n", " ").Replace("\0", " ").Trim();
}
