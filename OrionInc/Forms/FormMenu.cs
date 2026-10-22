using OrionInc.Data;

namespace OrionInc.Forms
{
    public class FormMenu : Form
    {
        public FormMenu()
        {
            Text = "ORION INC - Menu Principal";
            Width = 760;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(243, 246, 250);

            var lblBemVindo = new Label
            {
                Text = $"Bem-vindo, {Sessao.NomeUsuario}  ({Sessao.NivelAcesso})",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, 20)
            };

            Controls.Add(lblBemVindo);

            var botoes = new List<(string texto, Action acao)>
            {
                ("Sensores", () => AbrirTela(new FormSensores())),
                ("Auditoria", () => AbrirTela(new FormAuditoria())),
                ("Funcionários", () => AbrirTela(new FormFuncionarios())),
                ("Áreas de Risco", () => AbrirTela(new FormAreasRisco())),
                ("Vagões", () => AbrirTela(new FormVagoes())),
                ("Dispositivos (EPI)", () => AbrirTela(new FormDispositivos())),
                ("Alertas Ativos", () => AbrirTela(new FormAlertas()))
            };

            // Somente quem possui permissão pode acessar
            // o módulo de gerenciamento de usuários.
            if (Sessao.PodeEditarUsuarios)
            {
                botoes.Add(
                    ("Usuários do Sistema",
                    () => AbrirTela(new FormUsuarios())));
            }

            int x = 30;
            int y = 70;

            const int largura = 220;
            const int altura = 52;
            const int espacoX = 240;
            const int espacoY = 62;

            int col = 0;

            foreach (var (texto, acao) in botoes)
            {
                var btn = new Button
                {
                    Text = texto,
                    Width = largura,
                    Height = altura,
                    Location = new Point(
                        x + col * espacoX,
                        y),
                    Font = new Font("Segoe UI", 10),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    ForeColor = Color.FromArgb(20, 36, 59)
                };

                btn.FlatAppearance.BorderColor =
                    Color.FromArgb(210, 220, 232);

                btn.Click += (_, _) => acao();

                Controls.Add(btn);

                col++;

                if (col == 2)
                {
                    col = 0;
                    y += espacoY;
                }
            }

            // Exibe o perfil atual e suas permissões.
            var lblPermissoes = new Label
            {
                Text =
                    $"Criar alertas: {SimNao(Sessao.PodeCriarAlerta)}   |   " +
                    $"Aprovar manutenção: {SimNao(Sessao.PodeAprovarManutencao)}   |   " +
                    $"Editar usuários: {SimNao(Sessao.PodeEditarUsuarios)}",
                AutoSize = false,
                Width = 680,
                Height = 40,
                Location = new Point(30, 455),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(75, 85, 99)
            };

            Controls.Add(lblPermissoes);

            var btnSair = new Button
            {
                Text = "Sair",
                Width = 120,
                Height = 36,
                Location = new Point(30, 530),
                BackColor = Color.IndianRed,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btnSair.Click += (_, _) =>
            {
                Sessao.Limpar();
                Close();
            };

            Controls.Add(btnSair);
        }

        private static string SimNao(bool permitido)
        {
            return permitido ? "Sim" : "Não";
        }

        private static void AbrirTela(Form tela)
        {
            using (tela)
            {
                tela.StartPosition = FormStartPosition.CenterScreen;
                tela.ShowDialog();
            }
        }
    }
}
