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
