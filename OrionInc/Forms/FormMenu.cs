using OrionInc.Data;

namespace OrionInc.Forms
{
    public class FormMenu : Form
    {
        public FormMenu()
        {
            Text = "ORION INC - Menu Principal";
            Width = 560;
            Height = 480;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

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
                ("Funcionários", () => AbrirTela(new FormFuncionarios())),
                ("Áreas de Risco", () => AbrirTela(new FormAreasRisco())),
                ("Vagões", () => AbrirTela(new FormVagoes())),
                ("Dispositivos (EPI)", () => AbrirTela(new FormDispositivos())),
                ("Alertas Ativos", () => AbrirTela(new FormAlertas())),
            };

            if (Sessao.PodeEditarUsuarios)
                botoes.Add(("Usuários do Sistema", () => AbrirTela(new FormUsuarios())));

            int x = 30, y = 70;
            const int largura = 230, altura = 60, espacoX = 250, espacoY = 75;
            int col = 0;

            foreach (var (texto, acao) in botoes)
            {
                var btn = new Button
                {
                    Text = texto,
                    Width = largura,
                    Height = altura,
                    Location = new Point(x + col * espacoX, y),
                    Font = new Font("Segoe UI", 10)
                };
                btn.Click += (_, _) => acao();
                Controls.Add(btn);

                col++;
                if (col == 2)
                {
                    col = 0;
                    y += espacoY;
                }
            }

            var btnSair = new Button
            {
                Text = "Sair",
                Width = 120,
                Height = 32,
                Location = new Point(30, 400),
                BackColor = Color.IndianRed,
                ForeColor = Color.White
            };
            btnSair.Click += (_, _) =>
            {
                Sessao.Limpar();
                Close();
            };
            Controls.Add(btnSair);
        }

        private static void AbrirTela(Form tela)
        {
            tela.StartPosition = FormStartPosition.CenterScreen;
            tela.ShowDialog();
        }
    }
}
