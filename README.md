# ORION INC — Sistema de Monitoramento Ferroviário

WinForms (.NET 10) + PostgreSQL (via Npgsql), no mesmo estilo do seu projeto
`Formul-rio_Sistema_Teste` no GitHub: `FormLogin` → `FormMenu` → telas de
cadastro/listagem por entidade.

## ⚠️ Bugs corrigidos no seu SQL original

Seu arquivo `BANCO_DE_DADOS_ORION.sql` não rodava no pgAdmin por dois motivos
(ambos corrigidos em `database/01_schema_corrigido.sql`):

1. Na tabela `sensores`, a FK apontava para a tabela `gateways` (não existe —
   o nome certo é `gateway`) e tinha `ON DELETE SET NU` (sintaxe inválida).
2. Na tabela `registros_sistema`, havia marcações `[cite: 98]` soltas dentro
   do `CREATE TABLE` (resíduo de um editor/IA que gerou o script) — isso
   quebra a sintaxe do PostgreSQL.

O arquivo de views que você mandou (`02_views.sql`) já estava correto.

## 1. Criar o banco no pgAdmin

1. No pgAdmin, crie um banco chamado `orion_inc` (ou o nome que preferir).
2. Abra o Query Tool nesse banco e rode, **nesta ordem**:
   - `database/01_schema_corrigido.sql`
   - `database/02_views.sql`

Isso cria as tabelas, triggers, dados iniciais de `permissoes_acesso`
(`administrador`, `operador`, `tecnico`) e todas as views do painel.

## 2. Criar o primeiro usuário administrador

O login exige senha com hash BCrypt — não dá para inserir a senha em texto
puro direto no pgAdmin. O jeito mais simples:

1. Rode a aplicação (passo 4).
2. Na tela de login, gere um hash rodando este snippet em qualquer projeto
   C# com o pacote `BCrypt.Net-Next`, ou use o próprio app: crie
   temporariamente um usuário via `FormCadastroUsuario` **depois** de logar
   — ou, para o primeiro acesso, insira manualmente pelo pgAdmin usando um
   hash já gerado:

```sql
-- Senha: Admin@123
INSERT INTO usuarios (nome_usuario, email_usuario, senha_usuario, nivel_acesso)
VALUES (
  'Administrador',
  'admin@orioninc.com',
  '$2a$11$K9m8N3z3s0m0f8m8Vq1UUeQKq0m3p9s2Y3m4rB0v0f8oQvR8m9m9O', -- ajuste, veja nota abaixo
  'administrador'
);
```

> O hash acima é só um placeholder de formato. Gere o real: abra um projeto
> de console `dotnet new console`, adicione `BCrypt.Net-Next`, e rode
> `Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("Admin@123"));` — cole o
> resultado no `INSERT` acima. Depois do primeiro login, cadastre os demais
> usuários direto pela tela "Usuários do Sistema".

## 3. Configurar a conexão

Edite `OrionInc/appsettings.json`:

```json
{
  "ConnectionSettings": {
    "Host": "localhost",
    "Port": 5432,
    "Database": "orion_inc",
    "Username": "postgres",
    "Password": "sua_senha_aqui"
  }
}
```

Na tela de login existe um botão **"Testar conexão"** para validar isso
antes de tentar entrar.

## 4. Rodar o projeto

Requisitos: .NET SDK 10 (ou o LTS mais recente disponível) e Windows
(WinForms só roda em Windows).

```bash
cd OrionInc
dotnet restore
dotnet build
dotnet run --project OrionInc
```

Ou abra `OrionInc.sln` no Visual Studio 2022+ e aperte F5.

## O que está implementado

- **Login** (`FormLogin`) — autentica contra `usuarios`, com BCrypt e
  checagem de `usuario_ativo`; botão de teste de conexão.
- **Menu** (`FormMenu`) — mostra só os módulos que o `nivel_acesso` do
  usuário permite (via `permissoes_acesso`).
- **Funcionários** — listar/buscar/criar/editar/excluir.
- **Áreas de Risco** — listar/criar/editar/excluir.
- **Vagões** — listar/criar/editar/excluir.
- **Dispositivos (EPI)** — cadastro com vínculo 1-para-1 ao funcionário;
  trata o erro do trigger `tg_validar_dispositivo` (funcionário já tem
  dispositivo) com uma mensagem amigável.
- **Usuários do Sistema** — só aparece para quem tem
  `pode_editar_usuarios = true`; usa `vw_usuarios_sistema` para nunca expor
  a senha na listagem.
- **Alertas Ativos** — dashboard sobre `vw_alertas_ativos`, com botão para
  marcar um evento como resolvido (`evento_resolvido = TRUE`).

## Próximos passos sugeridos (não implementados ainda)

- Telas para `sensores`, `gateway`, `chamados_tecnicos` e
  `ordens_manutencao` (o banco e as views já têm tudo pronto —
  `vw_sensores_por_area`, `vw_sensores_por_vagao`, `vw_chamados_e_manutencao`).
- Histórico de eventos por vagão (`FormHistoricoVagao` no seu repo original).
- Tela de auditoria usando `vw_historico_auditoria`.
- Dashboard gerencial usando `vw_resumo_areas_risco`.

Essas telas seguem exatamente o mesmo padrão das que já existem (uma tela de
lista + uma tela de formulário), então é só replicar o modelo.
