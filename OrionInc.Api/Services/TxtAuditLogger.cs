using System.Text;

namespace OrionInc.Api.Services;

public sealed class TxtAuditLogger(ILogger<TxtAuditLogger> logger)
{
    private static readonly SemaphoreSlim Gate = new(1,1);
    public async Task WriteAsync(string category,string type,string entity,string? id,string actor,string origin,string result,string description,string? ip)
    {
        var now=DateTimeOffset.Now; var dir=Path.Combine(AppContext.BaseDirectory,"logs"); Directory.CreateDirectory(dir);
        string Safe(string s)=>s.Replace("\r"," ").Replace("\n"," ").Replace("\0"," ").Trim();
        var text=string.Join(Environment.NewLine,new[]{new string('=',68),$"DATA/HORA: {now:yyyy-MM-dd'T'HH:mm:ss.fffzzz}",$"TIPO: {Safe(type)}",$"ENTIDADE: {Safe(entity)}",$"ID: {Safe(id??"N/A")}",$"RESPONSAVEL: {Safe(actor)}",$"ORIGEM: {Safe(origin)}",$"RESULTADO: {Safe(result)}",$"IP: {Safe(ip??"N/A")}",$"DESCRICAO: {Safe(description)}",new string('=',68),""});
        await Gate.WaitAsync(); try { await File.AppendAllTextAsync(Path.Combine(dir,$"{Safe(category)}-{now:yyyy-MM-dd}.txt"),text,new UTF8Encoding(false)); } catch(Exception ex) { logger.LogError(ex,"Falha ao gravar log de auditoria TXT"); throw; } finally { Gate.Release(); }
    }
}
