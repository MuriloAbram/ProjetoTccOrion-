using System.Text.Json;
using Npgsql;
using OrionInc.Data;

namespace OrionInc.Services;

/// <summary>
/// Centraliza eventos explícitos de auditoria da aplicação desktop.
/// Operações CRUD também são protegidas pelos triggers PostgreSQL da migração de auditoria.
/// </summary>
public static class AuditService
{
    public static async Task RegistrarAsync(
        string tipoEvento,
        string entidade,
        string? entidadeId,
        string resultado,
        string descricao,
        string modulo,
        object? detalhes = null,
        string? origem = null,
        CancellationToken cancellationToken = default)
    {
        string? detalhesJson = detalhes is null
            ? null
            : JsonSerializer.Serialize(detalhes, new JsonSerializerOptions
            {
                WriteIndented = false
            });

        try
        {
            using var conn = DatabaseHelper.GetConnection();
            await RegistrarNaConexaoAsync(
                conn,
                tipoEvento,
                entidade,
                entidadeId,
                resultado,
                descricao,
                modulo,
                detalhesJson,
                origem,
                cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Falha ao registrar auditoria '{tipoEvento}': {ex}");
        }

        try
        {
            await TxtAuditLogger.WriteAsync(
                "auditoria",
                tipoEvento,
                entidade,
                entidadeId,
                string.IsNullOrWhiteSpace(Sessao.NomeUsuario)
                    ? "sistema/desconhecido"
                    : Sessao.NomeUsuario,
                origem ?? "DESKTOP",
                resultado,
                descricao,
                null,
                modulo,
                detalhesJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Falha ao registrar auditoria TXT '{tipoEvento}': {ex}");
        }
    }

    public static async Task RegistrarNaConexaoAsync(
        NpgsqlConnection conn,
        string tipoEvento,
        string entidade,
        string? entidadeId,
        string resultado,
        string descricao,
        string modulo,
        string? detalhesJson = null,
        string? origem = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO public.auditoria_eventos
            (
                tipo_evento,
                entidade,
                entidade_id,
                id_usuario,
                responsavel,
                origem,
                resultado,
                descricao,
                modulo,
                detalhes,
                hostname
            )
            VALUES
            (
                @tipo,
                @entidade,
                @entidadeId,
                @idUsuario,
                @responsavel,
                @origem,
                @resultado,
                @descricao,
                @modulo,
                CASE
                    WHEN @detalhes IS NULL THEN NULL
                    ELSE CAST(@detalhes AS jsonb)
                END,
                @hostname
            );
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("tipo", tipoEvento);
        cmd.Parameters.AddWithValue("entidade", entidade);
        cmd.Parameters.Add("entidadeId", NpgsqlTypes.NpgsqlDbType.Text).Value =
            (object?)entidadeId ?? DBNull.Value;
        cmd.Parameters.Add("idUsuario", NpgsqlTypes.NpgsqlDbType.Integer).Value =
            Sessao.IdUsuario > 0 ? Sessao.IdUsuario : DBNull.Value;
        cmd.Parameters.AddWithValue(
            "responsavel",
            string.IsNullOrWhiteSpace(Sessao.NomeUsuario)
                ? "sistema/desconhecido"
                : Sessao.NomeUsuario);
        cmd.Parameters.AddWithValue("origem", origem ?? "DESKTOP");
        cmd.Parameters.AddWithValue("resultado", resultado);
        cmd.Parameters.AddWithValue("descricao", descricao);
        cmd.Parameters.AddWithValue("modulo", modulo);
        cmd.Parameters.Add("detalhes", NpgsqlTypes.NpgsqlDbType.Text).Value =
            (object?)detalhesJson ?? DBNull.Value;
        cmd.Parameters.AddWithValue(
            "hostname",
            Environment.MachineName);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
