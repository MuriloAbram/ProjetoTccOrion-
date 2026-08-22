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