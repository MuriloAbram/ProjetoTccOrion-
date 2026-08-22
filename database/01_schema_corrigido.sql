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
