using OrionInc.Data;
using OrionInc.Services;

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

            if (Sessao.PodeEditarUsuarios)
            {
                botoes.Add(
                    (
                        "Usuários do Sistema",
                        () => AbrirTela(new FormUsuarios())
                    )
                );
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

                btn.Click += (_, _) =>
                {
                    try
                    {
                        acao();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            this,
                            "Não foi possível abrir o módulo.\n\n" +
                            ex.Message,
                            "ORION INC",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                };

                Controls.Add(btn);

                col++;

                if (col == 2)
                {
                    col = 0;
                    y += espacoY;
                }
            }

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

            btnSair.Click += async (_, _) =>
            {
                int idUsuario = Sessao.IdUsuario;

                try
                {
                    // Não bloqueia a interface com GetResult().
                    await AuditService.RegistrarAsync(
                        "LOGOUT",
                        "usuarios",
                        idUsuario > 0 ? idUsuario.ToString() : null,
                        "SUCESSO",
                        "Sessão encerrada pelo usuário através do menu principal.",
                        "FormMenu");
                }
                catch
                {
                    // Auditoria nunca deve impedir o logout.
                }

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
            // IMPORTANTE:
            // Não usar .GetAwaiter().GetResult() aqui.
            //
            // Esse método era o responsável pelo travamento da interface.
            // O log será disparado sem bloquear a thread do WinForms.

            _ = RegistrarAberturaModuloAsync(tela);

            using (tela)
            {
                tela.StartPosition = FormStartPosition.CenterScreen;
                tela.ShowDialog();
            }
        }

        private static async Task RegistrarAberturaModuloAsync(Form tela)
        {
            try
            {
                await AuditService.RegistrarAsync(
                    "MODULO_ABERTO",
                    "modulo",
                    tela.GetType().Name,
                    "SUCESSO",
                    $"Módulo {tela.Text} aberto pelo usuário.",
                    "FormMenu");
            }
            catch
            {
                // O log não pode impedir a abertura do módulo.
            }
        }
    }
}