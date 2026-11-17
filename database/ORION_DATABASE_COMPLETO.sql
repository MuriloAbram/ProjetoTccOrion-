-- ============================================================================
-- ORION INC - BANCO DE DADOS COMPLETO
-- PostgreSQL / pgAdmin
-- Ordem: 01_schema_corrigido -> 02_views -> 03_orion_api_auditoria -> 04_auditoria_integracao
-- Este arquivo consolida os scripts do projeto.
-- ============================================================================


==========================================================================================
-- ARQUIVO: 01_schema_corrigido.sql
==========================================================================================

-- =========================================================================
-- BANCO DE DADOS: ORION INC
-- SCRIPT UNIFICADO PARA POSTGRESQL (pgAdmin) - VERSÃO CORRIGIDA
--
-- CORREÇÕES APLICADAS EM RELAÇÃO AO ARQUIVO ORIGINAL:
-- 1) Tabela "sensores": a FK apontava para "gateways" (tabela não existe,
--    o nome correto é "gateway") e continha "ON DELETE SET NU" (inválido).
--    Corrigido para "REFERENCES gateway(id_gateway) ON DELETE SET NULL".
-- 2) Tabela "registros_sistema": havia marcações "[cite: 98]" etc. soltas
--    no meio do CREATE TABLE (resíduo de citação de um editor/IA), o que
--    quebra a sintaxe do PostgreSQL. Foram removidas.
-- =========================================================================

-- -------------------------------------------------------------------------
-- 1. BLOCO DE USUÁRIOS, PERMISSÕES E AUDITORIA
-- -------------------------------------------------------------------------

CREATE TABLE permissoes_acesso (
    id_permissao SERIAL PRIMARY KEY,
    nivel_acesso VARCHAR(50) UNIQUE NOT NULL,
    pode_criar_alerta BOOLEAN DEFAULT FALSE,
    pode_aprovar_manutencao BOOLEAN DEFAULT FALSE,
    pode_editar_usuarios BOOLEAN DEFAULT FALSE
);

CREATE TABLE usuarios (
    id_usuario SERIAL PRIMARY KEY,
    nome_usuario VARCHAR(150) NOT NULL,
    email_usuario VARCHAR(100) UNIQUE NOT NULL,
    senha_usuario VARCHAR(255) NOT NULL, -- hash (BCrypt) gravado pela aplicação
    nivel_acesso VARCHAR(50) NOT NULL,
    data_criacao DATE DEFAULT CURRENT_DATE,
    usuario_ativo BOOLEAN DEFAULT TRUE,
    FOREIGN KEY (nivel_acesso) REFERENCES permissoes_acesso(nivel_acesso) ON DELETE RESTRICT
);

CREATE TABLE historico_administrativo (
    id_log SERIAL PRIMARY KEY,
    id_usuario INT,
    acao_realizada TEXT NOT NULL,
    data_hora_acao TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ip_origem VARCHAR(45),
    FOREIGN KEY (id_usuario) REFERENCES usuarios(id_usuario) ON DELETE SET NULL
);

-- -------------------------------------------------------------------------
-- 2. BLOCO DE OPERAÇÕES FERROVIÁRIAS E TRABALHADORES
-- -------------------------------------------------------------------------

CREATE TABLE areas_risco (
    id_area SERIAL PRIMARY KEY,
    nome_area VARCHAR(100) NOT NULL,
    descricao_area TEXT,
    nivel_risco VARCHAR(50) NOT NULL, -- baixo, médio, alto, crítico
    raio_monitoramento INT NOT NULL,
    area_ativa BOOLEAN DEFAULT TRUE
);

CREATE TABLE vagoes (
    id_vagao SERIAL PRIMARY KEY,
    codigo_vagao VARCHAR(50) UNIQUE NOT NULL,
    tipo_vagao VARCHAR(100) NOT NULL,
    status_operacao VARCHAR(50) NOT NULL, -- operando, manutencao, parado
    data_ultima_manutencao DATE,
    observacoes TEXT
);

CREATE TABLE funcionarios (
    id_funcionario SERIAL PRIMARY KEY,
    nome_funcionario VARCHAR(150) NOT NULL,
    cpf VARCHAR(14) UNIQUE NOT NULL,
    cargo VARCHAR(100) NOT NULL,
    setor VARCHAR(100) NOT NULL,
    status_funcionario VARCHAR(50) NOT NULL, -- ativo, afastado, desligado
    data_cadastro DATE DEFAULT CURRENT_DATE
);

CREATE TABLE dispositivos (
    id_dispositivo SERIAL PRIMARY KEY,
    codigo_dispositivo VARCHAR(100) UNIQUE NOT NULL,
    tipo_dispositivo VARCHAR(50) NOT NULL, -- Capacete Smart, Pulseira...
    status_dispositivo VARCHAR(50) NOT NULL, -- disponivel, em_uso, defeito
    id_funcionario INT UNIQUE,
    FOREIGN KEY (id_funcionario) REFERENCES funcionarios(id_funcionario)
);

-- -------------------------------------------------------------------------
-- 3. BLOCO DE IoT E TELEMETRIA (GATEWAYS E SENSORES)
-- -------------------------------------------------------------------------

CREATE TABLE gateway (
    id_gateway SERIAL PRIMARY KEY,
    codigo_gateway VARCHAR(100) UNIQUE NOT NULL,
    localizacao_fixa VARCHAR(200),
    status_gateway VARCHAR(20) NOT NULL, -- online, offline
    ultima_comunicacao TIMESTAMP
);

CREATE TABLE sensores (
    id_sensor SERIAL PRIMARY KEY,
    tipo_sensor VARCHAR(100) NOT NULL,
    modelo_sensor VARCHAR(100) NOT NULL,
    id_gateway INT,
    id_area INT,
    id_vagao INT,
    status_sensor VARCHAR(50) NOT NULL,
    data_instalacao DATE DEFAULT CURRENT_DATE,
    FOREIGN KEY (id_gateway) REFERENCES gateway(id_gateway) ON DELETE SET NULL,
    FOREIGN KEY (id_area) REFERENCES areas_risco(id_area) ON DELETE SET NULL,
    FOREIGN KEY (id_vagao) REFERENCES vagoes(id_vagao) ON DELETE SET NULL
);

-- -------------------------------------------------------------------------
-- 4. BLOCO DE REGRAS, EVENTOS E MANUTENÇÃO TÉCNICA
-- -------------------------------------------------------------------------

CREATE TABLE regras_alerta (
    id_regra SERIAL PRIMARY KEY,
    tipo_sensor VARCHAR(100) NOT NULL,
    metrica_comparacao VARCHAR(20) NOT NULL,
    valor_limite NUMERIC(10,2) NOT NULL,
    nivel_urgencia VARCHAR(50) NOT NULL,
    mensagem_alerta TEXT NOT NULL
);

CREATE TABLE registros_sistema (
    id_registro SERIAL PRIMARY KEY,
    data_hora_evento TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    tipo_evento VARCHAR(50) NOT NULL, -- Alerta, risco, falha, leitura
    descricao_evento TEXT NOT NULL,
    nivel_alerta VARCHAR(50) NOT NULL,
    id_funcionario INT,
    id_area INT,
    id_vagao INT,
    id_sensor INT,
    id_usuario INT,
    evento_resolvido BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (id_funcionario) REFERENCES funcionarios(id_funcionario) ON DELETE SET NULL,
    FOREIGN KEY (id_area) REFERENCES areas_risco(id_area) ON DELETE SET NULL,
    FOREIGN KEY (id_vagao) REFERENCES vagoes(id_vagao) ON DELETE SET NULL,
    FOREIGN KEY (id_sensor) REFERENCES sensores(id_sensor) ON DELETE SET NULL,
    FOREIGN KEY (id_usuario) REFERENCES usuarios(id_usuario) ON DELETE SET NULL
);

CREATE TABLE chamados_tecnicos (
    id_chamado SERIAL PRIMARY KEY,
    titulo VARCHAR(150) NOT NULL,
    descricao_problema TEXT NOT NULL,
    status_chamado VARCHAR(50) DEFAULT 'aberto',
    prioridade VARCHAR(30) NOT NULL,
    id_usuario_abertura INT,
    id_sensor_afetado INT,
    data_abertura TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (id_usuario_abertura) REFERENCES usuarios(id_usuario) ON DELETE SET NULL,
    FOREIGN KEY (id_sensor_afetado) REFERENCES sensores(id_sensor) ON DELETE SET NULL
);

CREATE TABLE ordens_manutencao (
    id_ordem SERIAL PRIMARY KEY,
    id_chamado INT UNIQUE,
    tecnico_responsavel VARCHAR(150) NOT NULL,
    descricao_servico TEXT,
    data_programada DATE NOT NULL,
    data_conclusao DATE,
    custo_estimado NUMERIC(10,2),
    status_ordem VARCHAR(50) DEFAULT 'pendente',
    FOREIGN KEY (id_chamado) REFERENCES chamados_tecnicos(id_chamado) ON DELETE CASCADE
);

-- -------------------------------------------------------------------------
-- TRIGGERS
-- -------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION gerenciar_log_configuracao()
RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'UPDATE') THEN
        INSERT INTO historico_administrativo (id_usuario, acao_realizada, data_hora_acao, ip_origem)
        VALUES (NEW.id_usuario, 'ALTERAÇÃO: Dados do usuário ID ' || NEW.id_usuario || ' foram atualizados.', CURRENT_TIMESTAMP, '127.0.0.1');
    ELSIF (TG_OP = 'INSERT') THEN
        INSERT INTO historico_administrativo (id_usuario, acao_realizada, data_hora_acao, ip_origem)
        VALUES (NEW.id_usuario, 'CADASTRO: Novo usuário inserido no sistema administrativo.', CURRENT_TIMESTAMP, '127.0.0.1');
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_auditoria_usuarios
AFTER INSERT OR UPDATE ON usuarios
FOR EACH ROW
EXECUTE FUNCTION gerenciar_log_configuracao();

CREATE OR REPLACE FUNCTION verificar_critico_sensor()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.tipo_evento IN ('falha', 'risco') AND NEW.nivel_alerta = 'crítico' THEN
        UPDATE sensores SET status_sensor = 'falha' WHERE id_sensor = NEW.id_sensor;
        NEW.descricao_evento := '[BLOQUEIO AUTOMÁTICO IoT] ' || NEW.descricao_evento;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_verificar_critico_sensor
BEFORE INSERT ON registros_sistema
FOR EACH ROW
EXECUTE FUNCTION verificar_critico_sensor();

CREATE OR REPLACE FUNCTION validar_dispositivo_funcionario()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.id_funcionario IS NOT NULL AND EXISTS (
        SELECT 1 FROM dispositivos
        WHERE id_funcionario = NEW.id_funcionario AND id_dispositivo <> NEW.id_dispositivo
    ) THEN
        RAISE EXCEPTION 'Operação Cancelada: O funcionário ID % já possui um dispositivo eletrônico vinculado!', NEW.id_funcionario;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tg_validar_dispositivo
BEFORE INSERT OR UPDATE ON dispositivos
FOR EACH ROW
EXECUTE FUNCTION validar_dispositivo_funcionario();

-- -------------------------------------------------------------------------
-- DADOS INICIAIS (necessários para o login funcionar)
-- -------------------------------------------------------------------------

INSERT INTO permissoes_acesso (nivel_acesso, pode_criar_alerta, pode_aprovar_manutencao, pode_editar_usuarios)
VALUES
    ('administrador', TRUE, TRUE, TRUE),
    ('operador', TRUE, FALSE, FALSE),
    ('tecnico', FALSE, TRUE, FALSE);

-- Usuário admin inicial. Senha: Admin@123  (hash BCrypt gerado pela aplicação C#)
-- Gere o hash rodando o sistema (tela de login tem link "Primeiro acesso? Criar usuário")
-- ou substitua o valor abaixo por um hash gerado por você.


==========================================================================================
-- ARQUIVO: 02_views.sql
==========================================================================================

-- =========================================================================
-- VIEWS DO BANCO DE DADOS: ORION INC
-- =========================================================================
-- Descrição geral:
-- Este arquivo contém todas as views criadas para facilitar consultas operacionais, de segurança e de manutenção do sistema ferroviário.
-- As views evitam repetição de JOINs complexos e protegem dados sensíveis (como senhas) ao expor apenas as colunas necessárias para cada contexto.
-- =========================================================================


-- -------------------------------------------------------------------------
-- VIEW 1: vw_funcionarios_dispositivos
-- -------------------------------------------------------------------------
-- Exibe o vínculo entre cada funcionário e seu dispositivo EPI eletrônico.
-- Útil para o painel de controle de equipamentos: mostra quem está com qual
-- dispositivo, o tipo (capacete, pulseira) e o status atual do EPI.
-- Funcionários sem dispositivo vinculado também aparecem (LEFT JOIN), com os campos do dispositivo retornando NULL.
-- -------------------------------------------------------------------------
CREATE OR REPLACE VIEW vw_funcionarios_dispositivos AS
SELECT
    f.id_funcionario,
    f.nome_funcionario,
    f.cpf,
    f.cargo,
    f.setor,
    f.status_funcionario,
    d.id_dispositivo,
    d.codigo_dispositivo,
    d.tipo_dispositivo,
    d.status_dispositivo
FROM funcionarios f
LEFT JOIN dispositivos d ON f.id_funcionario = d.id_funcionario;


-- -------------------------------------------------------------------------
-- VIEW 2: vw_alertas_ativos
-- -------------------------------------------------------------------------
-- Lista todos os eventos de segurança que ainda NÃO foram resolvidos
-- (evento_resolvido = FALSE). Agrega em uma única consulta as informações
-- do funcionário envolvido, da área de risco, do vagão e do sensor que disparou o alerta.
-- É a view principal para o painel de monitoramento em tempo real dos operadores de segurança.
-- -------------------------------------------------------------------------
CREATE OR REPLACE VIEW vw_alertas_ativos AS
SELECT
    rs.id_registro,
    rs.data_hora_evento,
    rs.tipo_evento,
    rs.nivel_alerta,
    rs.descricao_evento,
    -- Dados do funcionário envolvido no evento
    f.nome_funcionario,
    f.cargo,
    -- Dados da área onde o evento ocorreu
    ar.nome_area,
    ar.nivel_risco AS risco_da_area,
    -- Dados do vagão relacionado (se houver)
    v.codigo_vagao,
    v.tipo_vagao,
    -- Dados do sensor que detectou o evento
    s.tipo_sensor,
    s.modelo_sensor,
    s.status_sensor
FROM registros_sistema rs
LEFT JOIN funcionarios   f  ON rs.id_funcionario = f.id_funcionario
LEFT JOIN areas_risco    ar ON rs.id_area        = ar.id_area
LEFT JOIN vagoes         v  ON rs.id_vagao       = v.id_vagao
LEFT JOIN sensores       s  ON rs.id_sensor      = s.id_sensor
WHERE rs.evento_resolvido = FALSE  -- Filtra apenas eventos ainda em aberto
ORDER BY rs.data_hora_evento DESC; -- Mais recentes aparecem primeiro


-- -------------------------------------------------------------------------
-- VIEW 3: vw_sensores_por_area
-- -------------------------------------------------------------------------
-- Mostra todos os sensores fixos instalados nas áreas de risco, junto com as informações do gateway responsável por receber os dados de cada um.
-- Ideal para relatórios de infraestrutura e para identificar rapidamente quais sensores estão com falha em cada local monitorado.
-- Sensores móveis (vinculados a vagões) não aparecem nesta view.
-- -------------------------------------------------------------------------
CREATE OR REPLACE VIEW vw_sensores_por_area AS
SELECT
    ar.id_area,
    ar.nome_area,
    ar.nivel_risco,
    ar.raio_monitoramento,
    ar.area_ativa,
    -- Dados do sensor instalado na área
    s.id_sensor,
    s.tipo_sensor,
    s.modelo_sensor,
    s.status_sensor,
    s.data_instalacao,
    -- Dados do gateway que concentra os dados deste sensor
    g.codigo_gateway,
    g.localizacao_fixa   AS localizacao_gateway,
    g.status_gateway,
    g.ultima_comunicacao AS ultimo_ping_gateway
FROM areas_risco ar
LEFT JOIN sensores s  ON ar.id_area    = s.id_area
LEFT JOIN gateway  g  ON s.id_gateway  = g.id_gateway
WHERE ar.area_ativa = TRUE  -- Mostra apenas áreas que estão em operação
ORDER BY ar.nome_area, s.tipo_sensor;


-- -------------------------------------------------------------------------
-- VIEW 4: vw_sensores_por_vagao
-- -------------------------------------------------------------------------
-- Exibe os sensores embarcados (móveis) instalados em cada vagão, junto com o status operacional do vagão e do próprio sensor.
-- Útil para a equipe de manutenção verificar a cobertura de telemetria de cada composição ferroviária e identificar vagões sem sensores ativos.
-- -------------------------------------------------------------------------
CREATE OR REPLACE VIEW vw_sensores_por_vagao AS
SELECT
    v.id_vagao,
    v.codigo_vagao,
    v.tipo_vagao,
    v.status_operacao       AS status_vagao,
    v.data_ultima_manutencao,
    -- Dados do sensor embarcado no vagão
    s.id_sensor,
    s.tipo_sensor,
    s.modelo_sensor,
    s.status_sensor,
    s.data_instalacao,
    -- Gateway que recebe os dados deste sensor móvel
    g.codigo_gateway,
    g.status_gateway
FROM vagoes v
LEFT JOIN sensores s ON v.id_vagao    = s.id_vagao
LEFT JOIN gateway  g ON s.id_gateway  = g.id_gateway
ORDER BY v.codigo_vagao, s.tipo_sensor;


-- -------------------------------------------------------------------------
-- VIEW 5: vw_chamados_e_manutencao
-- -------------------------------------------------------------------------
-- Consolida em uma única consulta o chamado técnico e sua respectiva ordem de manutenção (quando existir). Mostra o sensor com defeito,
-- quem abriu o chamado, o técnico responsável pelo reparo, datas, custos e o status atual de cada atendimento.
-- Facilita o acompanhamento do ciclo completo: abertura → reparo → conclusão.
-- -------------------------------------------------------------------------
CREATE OR REPLACE VIEW vw_chamados_e_manutencao AS
SELECT
    ct.id_chamado,
    ct.titulo,
    ct.descricao_problema,
    ct.status_chamado,
    ct.prioridade,
    ct.data_abertura,
    -- Usuário operador que abriu o chamado
    u.nome_usuario        AS aberto_por,
    u.email_usuario,
    -- Sensor que apresentou defeito
    s.tipo_sensor         AS tipo_sensor_afetado,
    s.modelo_sensor       AS modelo_sensor_afetado,
    s.status_sensor       AS status_atual_sensor,
    -- Dados da ordem de manutenção vinculada (pode ser NULL se ainda não gerada)
    om.id_ordem,
    om.tecnico_responsavel,
    om.descricao_servico,
    om.data_programada,
    om.data_conclusao,
    om.custo_estimado,
    om.status_ordem
FROM chamados_tecnicos ct
LEFT JOIN usuarios        u  ON ct.id_usuario_abertura = u.id_usuario
LEFT JOIN sensores        s  ON ct.id_sensor_afetado   = s.id_sensor
LEFT JOIN ordens_manutencao om ON ct.id_chamado        = om.id_chamado
ORDER BY ct.data_abertura DESC; -- Chamados mais recentes no topo


-- -------------------------------------------------------------------------
-- VIEW 6: vw_usuarios_sistema
-- -------------------------------------------------------------------------
-- Exibe os dados públicos dos usuários cadastrados no sistema, junto com
-- as permissões do seu nível de acesso.
-- IMPORTANTE: A coluna senha_usuario é OMITIDA intencionalmente por segurança.
-- Esta view deve ser usada em telas administrativas de gestão de usuários.
-- -------------------------------------------------------------------------
CREATE OR REPLACE VIEW vw_usuarios_sistema AS
SELECT
    u.id_usuario,
    u.nome_usuario,
    u.email_usuario,
    -- senha_usuario omitida propositalmente por segurança
    u.nivel_acesso,
    u.data_criacao,
    u.usuario_ativo,
    -- Permissões detalhadas do perfil do usuário
    pa.pode_criar_alerta,
    pa.pode_aprovar_manutencao,
    pa.pode_editar_usuarios
FROM usuarios u
INNER JOIN permissoes_acesso pa ON u.nivel_acesso = pa.nivel_acesso
ORDER BY u.nome_usuario;


-- -------------------------------------------------------------------------
-- VIEW 7: vw_historico_auditoria
-- -------------------------------------------------------------------------
-- Exibe o log completo de ações administrativas do sistema, já com o nome
-- do usuário responsável por cada ação. Útil para auditorias de segurança,
-- rastreamento de alterações e conformidade com políticas internas.
-- Inclui registros de usuários que foram excluídos (id_usuario pode ser NULL
-- devido ao ON DELETE SET NULL definido na tabela).
-- -------------------------------------------------------------------------
CREATE OR REPLACE VIEW vw_historico_auditoria AS
SELECT
    ha.id_log,
    ha.data_hora_acao,
    ha.acao_realizada,
    ha.ip_origem,
    -- Nome do usuário (NULL se o usuário foi removido do sistema)
    u.nome_usuario,
    u.email_usuario,
    u.nivel_acesso
FROM historico_administrativo ha
LEFT JOIN usuarios u ON ha.id_usuario = u.id_usuario
ORDER BY ha.data_hora_acao DESC; -- Ações mais recentes primeiro


-- -------------------------------------------------------------------------
-- VIEW 8: vw_resumo_areas_risco
-- -------------------------------------------------------------------------
-- Apresenta um resumo gerencial de cada área monitorada: quantos sensores estão instalados, quantos estão com falha e quantos eventos não resolvidos
-- existem em cada local. Ideal para dashboards executivos e relatórios de
-- segurança operacional, permitindo identificar rapidamente as áreas mais
-- críticas que precisam de atenção imediata.
-- -------------------------------------------------------------------------
CREATE OR REPLACE VIEW vw_resumo_areas_risco AS
SELECT
    ar.id_area,
    ar.nome_area,
    ar.nivel_risco,
    ar.raio_monitoramento,
    ar.area_ativa,
    -- Contagem total de sensores instalados na área
    COUNT(DISTINCT s.id_sensor)                                          AS total_sensores,
    -- Contagem de sensores que estão em estado de falha
    COUNT(DISTINCT s.id_sensor) FILTER (WHERE s.status_sensor = 'falha') AS sensores_com_falha,
    -- Contagem de eventos de segurança ainda não resolvidos na área
    COUNT(DISTINCT rs.id_registro) FILTER (WHERE rs.evento_resolvido = FALSE) AS alertas_em_aberto
FROM areas_risco ar
LEFT JOIN sensores          s  ON ar.id_area = s.id_area
LEFT JOIN registros_sistema rs ON ar.id_area = rs.id_area
GROUP BY
    ar.id_area,
    ar.nome_area,
    ar.nivel_risco,
    ar.raio_monitoramento,
    ar.area_ativa
ORDER BY
    -- Ordena priorizando áreas críticas e com mais alertas abertos
    CASE ar.nivel_risco
        WHEN 'crítico' THEN 1
        WHEN 'alto'    THEN 2
        WHEN 'médio'   THEN 3
        WHEN 'baixo'   THEN 4
        ELSE 5
    END,
    alertas_em_aberto DESC;

==========================================================================================
-- ARQUIVO: 03_orion_api_auditoria.sql
==========================================================================================

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


==========================================================================================
-- ARQUIVO: 04_auditoria_integracao.sql
==========================================================================================

-- ============================================================================
-- ORION INC - 04 - INTEGRAÇÃO DA AUDITORIA DETALHADA
-- ============================================================================
-- Aplicar DEPOIS de:
--   01_schema_corrigido.sql
--   02_views.sql
--   03_orion_api_auditoria.sql
--
-- Não apaga dados existentes. Apenas adiciona colunas/índices necessários
-- para que o desktop e a API utilizem a mesma estrutura de auditoria.
-- ============================================================================

BEGIN;

ALTER TABLE public.auditoria_eventos
    ADD COLUMN IF NOT EXISTS id_usuario INTEGER,
    ADD COLUMN IF NOT EXISTS nivel_acesso VARCHAR(50),
    ADD COLUMN IF NOT EXISTS modulo VARCHAR(100),
    ADD COLUMN IF NOT EXISTS hostname VARCHAR(255),
    ADD COLUMN IF NOT EXISTS sessao_id UUID,
    ADD COLUMN IF NOT EXISTS detalhes JSONB,
    ADD COLUMN IF NOT EXISTS criado_em TIMESTAMPTZ NOT NULL DEFAULT now(),
    ADD COLUMN IF NOT EXISTS atualizado_em TIMESTAMPTZ NOT NULL DEFAULT now();

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_auditoria_eventos_usuario'
          AND conrelid = 'public.auditoria_eventos'::regclass
    ) THEN
        ALTER TABLE public.auditoria_eventos
            ADD CONSTRAINT fk_auditoria_eventos_usuario
            FOREIGN KEY (id_usuario)
            REFERENCES public.usuarios(id_usuario)
            ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_auditoria_eventos_usuario
    ON public.auditoria_eventos(id_usuario, ocorrido_em DESC);

CREATE INDEX IF NOT EXISTS ix_auditoria_eventos_tipo_resultado
    ON public.auditoria_eventos(tipo_evento, resultado, ocorrido_em DESC);

CREATE INDEX IF NOT EXISTS ix_auditoria_eventos_modulo
    ON public.auditoria_eventos(modulo, ocorrido_em DESC);

CREATE INDEX IF NOT EXISTS ix_auditoria_eventos_sessao
    ON public.auditoria_eventos(sessao_id, ocorrido_em DESC);

CREATE INDEX IF NOT EXISTS ix_auditoria_eventos_detalhes
    ON public.auditoria_eventos USING GIN(detalhes);

CREATE OR REPLACE FUNCTION public.fn_auditoria_orion()
RETURNS TRIGGER AS $$
DECLARE
    old_json JSONB;
    new_json JSONB;
    pk TEXT;
    actor_id_text TEXT;
    actor_name TEXT;
    access_level TEXT;
    source_name TEXT;
    ip_text TEXT;
    module_name TEXT;
    host_name TEXT;
    session_text TEXT;
    operation_name TEXT;
BEGIN
    IF TG_OP = 'INSERT' THEN
        old_json := NULL;
        new_json := to_jsonb(NEW);
    ELSIF TG_OP = 'UPDATE' THEN
        old_json := to_jsonb(OLD);
        new_json := to_jsonb(NEW);
    ELSE
        old_json := to_jsonb(OLD);
        new_json := NULL;
    END IF;

    IF old_json IS NOT NULL THEN
        old_json := old_json - 'senha_usuario';
    END IF;

    IF new_json IS NOT NULL THEN
        new_json := new_json - 'senha_usuario';
    END IF;

    pk := COALESCE(
        new_json->>TG_ARGV[0],
        old_json->>TG_ARGV[0],
        'N/A'
    );

    actor_id_text := NULLIF(current_setting('orion.usuario_id', true), '');
    actor_name := NULLIF(current_setting('orion.usuario_nome', true), '');
    access_level := NULLIF(current_setting('orion.nivel_acesso', true), '');
    source_name := NULLIF(current_setting('orion.origem', true), '');
    ip_text := NULLIF(current_setting('orion.ip', true), '');
    module_name := NULLIF(current_setting('orion.modulo', true), '');
    host_name := NULLIF(current_setting('orion.hostname', true), '');
    session_text := NULLIF(current_setting('orion.sessao_id', true), '');

    operation_name := CASE TG_OP
        WHEN 'INSERT' THEN 'CRIACAO'
        WHEN 'UPDATE' THEN 'ALTERACAO'
        ELSE 'EXCLUSAO'
    END;

    INSERT INTO public.auditoria_eventos
    (
        ocorrido_em,
        tipo_evento,
        entidade,
        entidade_id,
        id_usuario,
        nivel_acesso,
        responsavel,
        origem,
        resultado,
        descricao,
        modulo,
        detalhes,
        valores_anteriores,
        valores_novos,
        ip_origem,
        hostname,
        sessao_id,
        criado_em,
        atualizado_em
    )
    VALUES
    (
        now(),
        operation_name,
        TG_TABLE_NAME,
        pk,
        CASE
            WHEN actor_id_text ~ '^[0-9]+$'
                THEN actor_id_text::INTEGER
            ELSE NULL
        END,
        access_level,
        COALESCE(actor_name, 'sistema/desconhecido'),
        COALESCE(source_name, 'BANCO'),
        'SUCESSO',
        operation_name || ' em ' || TG_TABLE_NAME || ' (ID ' || pk || ')',
        COALESCE(module_name, 'BANCO'),
        jsonb_build_object(
            'operacao', TG_OP,
            'tabela', TG_TABLE_NAME,
            'id', pk
        ),
        old_json,
        new_json,
        CASE
            WHEN ip_text ~ '^[0-9a-fA-F:.]+$'
                THEN ip_text::inet
            ELSE NULL
        END,
        COALESCE(host_name, inet_client_addr()::text),
        CASE
            WHEN session_text ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
                THEN session_text::uuid
            ELSE NULL
        END,
        now(),
        now()
    );

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Atualiza o contexto da conexão para incluir nível/módulo/sessão.
-- A aplicação desktop já define usuário, origem e hostname em DatabaseHelper.
-- O nível/módulo/sessão são opcionais e possuem fallback seguro.
-- Não altera dados existentes.

COMMIT;
