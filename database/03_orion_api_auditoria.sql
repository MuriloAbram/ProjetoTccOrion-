-- Orion Inc - extensão incremental para telemetria, auditoria e API ESP32.
-- Executar no banco orion_inc DEPOIS de 01_schema_corrigido.sql e 02_views.sql.
-- Faça backup antes de aplicar em ambiente com dados importantes.

ALTER TABLE sensores ADD COLUMN IF NOT EXISTS ultima_comunicacao TIMESTAMP WITH TIME ZONE;
ALTER TABLE sensores ADD COLUMN IF NOT EXISTS timeout_comunicacao_segundos INTEGER NOT NULL DEFAULT 180 CHECK (timeout_comunicacao_segundos BETWEEN 15 AND 86400);
ALTER TABLE gateway ADD COLUMN IF NOT EXISTS timeout_comunicacao_segundos INTEGER NOT NULL DEFAULT 180 CHECK (timeout_comunicacao_segundos BETWEEN 15 AND 86400);

CREATE TABLE IF NOT EXISTS dispositivos_iot (
    id_dispositivo_iot BIGSERIAL PRIMARY KEY,
    codigo_dispositivo VARCHAR(100) NOT NULL UNIQUE,
    id_sensor INTEGER NOT NULL REFERENCES sensores(id_sensor) ON DELETE CASCADE,
    token_hash CHAR(64) NOT NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    criado_em TIMESTAMPTZ NOT NULL DEFAULT now(),
    atualizado_em TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_dispositivos_iot_sensor ON dispositivos_iot(id_sensor);

CREATE TABLE IF NOT EXISTS leituras_sensores (
    id_leitura BIGSERIAL PRIMARY KEY,
    id_sensor INTEGER NOT NULL REFERENCES sensores(id_sensor) ON DELETE CASCADE,
    codigo_dispositivo VARCHAR(100) NOT NULL,
    metrica VARCHAR(100) NOT NULL,
    valor DOUBLE PRECISION NOT NULL,
    unidade VARCHAR(30) NOT NULL,
    horario_dispositivo TIMESTAMPTZ,
    recebido_em TIMESTAMPTZ NOT NULL DEFAULT now(),
    validacao VARCHAR(30) NOT NULL DEFAULT 'valida',
    id_evento VARCHAR(100) NOT NULL UNIQUE
);
CREATE INDEX IF NOT EXISTS ix_leituras_sensor_recebido ON leituras_sensores(id_sensor, recebido_em DESC);
CREATE INDEX IF NOT EXISTS ix_leituras_metrica_recebido ON leituras_sensores(metrica, recebido_em DESC);

CREATE TABLE IF NOT EXISTS historico_disponibilidade_sensor (
    id_historico BIGSERIAL PRIMARY KEY,
    id_sensor INTEGER NOT NULL REFERENCES sensores(id_sensor) ON DELETE CASCADE,
    inicio_indisponibilidade TIMESTAMPTZ NOT NULL,
    fim_indisponibilidade TIMESTAMPTZ,
    motivo TEXT NOT NULL,
    CONSTRAINT ck_periodo_disponibilidade CHECK (fim_indisponibilidade IS NULL OR fim_indisponibilidade >= inicio_indisponibilidade)
);
CREATE INDEX IF NOT EXISTS ix_disponibilidade_sensor_inicio ON historico_disponibilidade_sensor(id_sensor, inicio_indisponibilidade DESC);

CREATE TABLE IF NOT EXISTS auditoria_eventos (
    id_evento BIGSERIAL PRIMARY KEY,
    ocorrido_em TIMESTAMPTZ NOT NULL DEFAULT now(),
    tipo_evento VARCHAR(80) NOT NULL,
    entidade VARCHAR(100) NOT NULL,
    entidade_id TEXT,
    responsavel TEXT NOT NULL DEFAULT 'sistema/desconhecido',
    origem VARCHAR(40) NOT NULL DEFAULT 'BANCO',
    resultado VARCHAR(20) NOT NULL DEFAULT 'SUCESSO',
    descricao TEXT NOT NULL,
    valores_anteriores JSONB,
    valores_novos JSONB,
    ip_origem INET
);
CREATE INDEX IF NOT EXISTS ix_auditoria_ocorrido ON auditoria_eventos(ocorrido_em DESC);
CREATE INDEX IF NOT EXISTS ix_auditoria_entidade ON auditoria_eventos(entidade, ocorrido_em DESC);
CREATE INDEX IF NOT EXISTS ix_auditoria_responsavel ON auditoria_eventos(responsavel, ocorrido_em DESC);

-- Remove o trigger antigo que atribuía a operação ao usuário alterado e fixava o IP em 127.0.0.1.
DROP TRIGGER IF EXISTS tg_auditoria_usuarios ON usuarios;
DROP FUNCTION IF EXISTS gerenciar_log_configuracao();

CREATE OR REPLACE FUNCTION fn_auditoria_orion() RETURNS TRIGGER AS $$
DECLARE
    old_json JSONB;
    new_json JSONB;
    pk TEXT;
    actor TEXT;
    source_name TEXT;
    ip_text TEXT;
    operation_name TEXT;
BEGIN
    IF TG_OP = 'INSERT' THEN old_json := NULL; new_json := to_jsonb(NEW);
    ELSIF TG_OP = 'UPDATE' THEN old_json := to_jsonb(OLD); new_json := to_jsonb(NEW);
    ELSE old_json := to_jsonb(OLD); new_json := NULL;
    END IF;
    -- Não armazenar hashes de senha nem qualquer valor do campo de senha na auditoria.
    IF old_json IS NOT NULL THEN old_json := old_json - 'senha_usuario'; END IF;
    IF new_json IS NOT NULL THEN new_json := new_json - 'senha_usuario'; END IF;
    pk := COALESCE(new_json->>TG_ARGV[0], old_json->>TG_ARGV[0], 'N/A');
    actor := NULLIF(current_setting('orion.usuario_id', true), '');
    source_name := NULLIF(current_setting('orion.origem', true), '');
    ip_text := NULLIF(current_setting('orion.ip', true), '');
    operation_name := CASE TG_OP WHEN 'INSERT' THEN 'CRIACAO' WHEN 'UPDATE' THEN 'ALTERACAO' ELSE 'EXCLUSAO' END;
    INSERT INTO auditoria_eventos(tipo_evento, entidade, entidade_id, responsavel, origem, resultado, descricao, valores_anteriores, valores_novos, ip_origem)
    VALUES (operation_name, TG_TABLE_NAME, pk, COALESCE(actor, 'sistema/desconhecido'), COALESCE(source_name, 'BANCO'), 'SUCESSO', operation_name || ' em ' || TG_TABLE_NAME || ' (ID ' || pk || ')', old_json, new_json, CASE WHEN ip_text ~ '^[0-9a-fA-F:.]+$' THEN ip_text::inet ELSE NULL END);
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_auditoria_usuarios_orion ON usuarios;
CREATE TRIGGER tg_auditoria_usuarios_orion AFTER INSERT OR UPDATE OR DELETE ON usuarios FOR EACH ROW EXECUTE FUNCTION fn_auditoria_orion('id_usuario');
DROP TRIGGER IF EXISTS tg_auditoria_funcionarios_orion ON funcionarios;
CREATE TRIGGER tg_auditoria_funcionarios_orion AFTER INSERT OR UPDATE OR DELETE ON funcionarios FOR EACH ROW EXECUTE FUNCTION fn_auditoria_orion('id_funcionario');
DROP TRIGGER IF EXISTS tg_auditoria_sensores_orion ON sensores;
CREATE TRIGGER tg_auditoria_sensores_orion AFTER INSERT OR UPDATE OR DELETE ON sensores FOR EACH ROW EXECUTE FUNCTION fn_auditoria_orion('id_sensor');
DROP TRIGGER IF EXISTS tg_auditoria_gateway_orion ON gateway;
CREATE TRIGGER tg_auditoria_gateway_orion AFTER INSERT OR UPDATE OR DELETE ON gateway FOR EACH ROW EXECUTE FUNCTION fn_auditoria_orion('id_gateway');
DROP TRIGGER IF EXISTS tg_auditoria_vagoes_orion ON vagoes;
CREATE TRIGGER tg_auditoria_vagoes_orion AFTER INSERT OR UPDATE OR DELETE ON vagoes FOR EACH ROW EXECUTE FUNCTION fn_auditoria_orion('id_vagao');
DROP TRIGGER IF EXISTS tg_auditoria_areas_orion ON areas_risco;
CREATE TRIGGER tg_auditoria_areas_orion AFTER INSERT OR UPDATE OR DELETE ON areas_risco FOR EACH ROW EXECUTE FUNCTION fn_auditoria_orion('id_area');
DROP TRIGGER IF EXISTS tg_auditoria_dispositivos_orion ON dispositivos;
CREATE TRIGGER tg_auditoria_dispositivos_orion AFTER INSERT OR UPDATE OR DELETE ON dispositivos FOR EACH ROW EXECUTE FUNCTION fn_auditoria_orion('id_dispositivo');
DROP TRIGGER IF EXISTS tg_auditoria_registros_orion ON registros_sistema;
CREATE TRIGGER tg_auditoria_registros_orion AFTER INSERT OR UPDATE OR DELETE ON registros_sistema FOR EACH ROW EXECUTE FUNCTION fn_auditoria_orion('id_registro');
DROP TRIGGER IF EXISTS tg_auditoria_chamados_orion ON chamados_tecnicos;
CREATE TRIGGER tg_auditoria_chamados_orion AFTER INSERT OR UPDATE OR DELETE ON chamados_tecnicos FOR EACH ROW EXECUTE FUNCTION fn_auditoria_orion('id_chamado');
DROP TRIGGER IF EXISTS tg_auditoria_ordens_orion ON ordens_manutencao;
CREATE TRIGGER tg_auditoria_ordens_orion AFTER INSERT OR UPDATE OR DELETE ON ordens_manutencao FOR EACH ROW EXECUTE FUNCTION fn_auditoria_orion('id_ordem');

-- Views são recriadas pela migração para incluir telemetria atual.
CREATE OR REPLACE VIEW vw_dashboard_orion AS
SELECT
 (SELECT count(*) FROM usuarios) AS total_usuarios,
 (SELECT count(*) FROM funcionarios WHERE lower(status_funcionario)='ativo') AS funcionarios_ativos,
 (SELECT count(*) FROM vagoes) AS total_vagoes,
 (SELECT count(*) FROM sensores) AS total_sensores,
 (SELECT count(*) FROM sensores WHERE lower(status_sensor)='online') AS sensores_online,
 (SELECT count(*) FROM sensores WHERE lower(status_sensor)='offline') AS sensores_offline,
 (SELECT count(*) FROM sensores WHERE lower(status_sensor)='falha') AS sensores_falha,
 (SELECT count(*) FROM gateway WHERE lower(status_gateway)='online') AS gateways_online,
 (SELECT count(*) FROM gateway WHERE lower(status_gateway)='offline') AS gateways_offline,
 (SELECT count(*) FROM areas_risco WHERE area_ativa=TRUE) AS areas_ativas,
 (SELECT count(*) FROM registros_sistema WHERE evento_resolvido=FALSE AND lower(nivel_alerta) IN ('critico','crítico','alto')) AS alertas_criticos,
 (SELECT count(*) FROM registros_sistema WHERE evento_resolvido=FALSE) AS eventos_pendentes,
 (SELECT count(*) FROM chamados_tecnicos WHERE lower(status_chamado) NOT IN ('encerrado','concluido','concluído')) AS chamados_abertos,
 (SELECT count(*) FROM ordens_manutencao WHERE lower(status_ordem) NOT IN ('concluida','concluída','concluido','concluído')) AS ordens_pendentes;
