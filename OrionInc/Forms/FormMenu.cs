using OrionInc.Data;
using OrionInc.Services;

namespace OrionInc.Forms;

public class FormMenu : Form
{
    private readonly FlowLayoutPanel _modules = new();

    public FormMenu()
    {
        Text = "ORION INC - Menu Principal";
        Width = 900;
        Height = 680;
        MinimumSize = new Size(820, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = OrionUi.Background;
        Font = new Font("Segoe UI", 9.5f);

        BuildInterface();
    }

    private void BuildInterface()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 128,
            BackColor = OrionUi.Navy,
            Padding = new Padding(30, 18, 30, 12)
        };

        header.Controls.Add(new Label
        {
            Text = "ORION INC",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(30, 14)
        });

        header.Controls.Add(new Label
        {
            Text = "Central de Monitoramento Ferroviário",
            ForeColor = Color.FromArgb(205, 220, 238),
            Font = new Font("Segoe UI", 10.5f),
            AutoSize = true,
            Location = new Point(33, 52)
        });

        header.Controls.Add(new Label
        {
            Text = $"{Sessao.NomeUsuario}  •  {Sessao.NivelAcesso}",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(33, 83)
        });

        var status = new Label
        {
            Text = "● SISTEMA OPERACIONAL",
            ForeColor = Color.FromArgb(120, 220, 160),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(header.ClientSize.Width - 220, 22)
        };
        header.Controls.Add(status);
        header.Resize += (_, _) => status.Left = header.ClientSize.Width - status.Width - 30;

        var section = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = OrionUi.Background,
            Padding = new Padding(30, 15, 30, 8)
        };

        section.Controls.Add(new Label
        {
            Text = "Módulos do sistema",
            ForeColor = OrionUi.Text,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(30, 12)
        });

        section.Controls.Add(new Label
        {
            Text = "Selecione uma área para consultar ou administrar os dados.",
            ForeColor = OrionUi.Muted,
            AutoSize = true,
            Location = new Point(30, 36)
        });

        _modules.Dock = DockStyle.Fill;
        _modules.Padding = new Padding(24, 8, 24, 18);
        _modules.BackColor = OrionUi.Background;
        _modules.FlowDirection = FlowDirection.LeftToRight;
        _modules.WrapContents = true;
        _modules.AutoScroll = true;

        AddModule("Sensores", "Monitoramento, status e histórico", () => AbrirTela(new FormSensores()));
        AddModule("Auditoria", "Eventos, filtros e rastreabilidade", () => AbrirTela(new FormAuditoria()));
        AddModule("Funcionários", "Cadastro e situação operacional", () => AbrirTela(new FormFuncionarios()));
        AddModule("Áreas de Risco", "Regiões e níveis de risco", () => AbrirTela(new FormAreasRisco()));
        AddModule("Vagões", "Frota, status e manutenção", () => AbrirTela(new FormVagoes()));
        AddModule("Dispositivos (EPI)", "Rastreamento de dispositivos", () => AbrirTela(new FormDispositivos()));
        AddModule("Alertas Ativos", "Ocorrências que precisam de atenção", () => AbrirTela(new FormAlertas()));

        if (Sessao.PodeEditarUsuarios)
            AddModule("Usuários do Sistema", "Acessos, níveis e contas", () => AbrirTela(new FormUsuarios()));

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 76,
            BackColor = Color.White,
            Padding = new Padding(30, 12, 30, 10)
        };

        footer.Controls.Add(new Label
        {
            Text = $"Permissões  •  Alertas: {SimNao(Sessao.PodeCriarAlerta)}   |   " +
                   $"Manutenção: {SimNao(Sessao.PodeAprovarManutencao)}   |   " +
                   $"Usuários: {SimNao(Sessao.PodeEditarUsuarios)}",
            ForeColor = OrionUi.Muted,
            AutoSize = true,
            Location = new Point(30, 14)
        });

        var btnSair = new Button
        {
            Text = "Sair da sessão",
            Width = 135,
            Height = 34,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(footer.ClientSize.Width - 165, 12)
        };
        OrionUi.StyleButton(btnSair, danger: true);
        btnSair.Click += async (_, _) =>
        {
            btnSair.Enabled = false;
            try
            {
                await AuditService.RegistrarAsync(
                    "LOGOUT",
                    "usuarios",
                    Sessao.IdUsuario > 0 ? Sessao.IdUsuario.ToString() : null,
                    "SUCESSO",
                    "Sessão encerrada pelo usuário através do menu principal.",
                    "FormMenu");
            }
            catch { }

            Sessao.Limpar();
            Close();
        };
        footer.Controls.Add(btnSair);
        footer.Resize += (_, _) => btnSair.Left = footer.ClientSize.Width - btnSair.Width - 30;

        Controls.Add(_modules);
        Controls.Add(section);
        Controls.Add(footer);
        Controls.Add(header);
    }

    private void AddModule(string title, string description, Action action)
    {
        var card = new Panel
        {
            Width = 390,
            Height = 104,
            Margin = new Padding(8),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand
        };

        var accent = new Panel
        {
            Dock = DockStyle.Left,
            Width = 6,
            BackColor = OrionUi.Primary
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = OrionUi.Text,
            AutoSize = true,
            Location = new Point(24, 18),
            Cursor = Cursors.Hand
        };

        var lblDescription = new Label
        {
            Text = description,
            Font = new Font("Segoe UI", 9),
            ForeColor = OrionUi.Muted,
            AutoSize = true,
            Location = new Point(24, 48),
            Cursor = Cursors.Hand
        };

        var arrow = new Label
        {
            Text = "›",
            Font = new Font("Segoe UI", 22),
            ForeColor = OrionUi.Primary,
            AutoSize = true,
            Location = new Point(340, 32),
            Cursor = Cursors.Hand
        };

        void Open()
        {
            try
            {
                _ = RegistrarAberturaModuloAsync(title);
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Não foi possível abrir o módulo '{title}'.\n\n{ex.Message}",
                    "ORION INC",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        card.Click += (_, _) => Open();
        lblTitle.Click += (_, _) => Open();
        lblDescription.Click += (_, _) => Open();
        arrow.Click += (_, _) => Open();

        card.MouseEnter += (_, _) =>
        {
            card.BackColor = Color.FromArgb(247, 250, 253);
            accent.BackColor = Color.FromArgb(29, 72, 117);
        };
        card.MouseLeave += (_, _) =>
        {
            card.BackColor = Color.White;
            accent.BackColor = OrionUi.Primary;
        };

        card.Controls.Add(arrow);
        card.Controls.Add(lblDescription);
        card.Controls.Add(lblTitle);
        card.Controls.Add(accent);
        _modules.Controls.Add(card);
    }

    private static string SimNao(bool value) => value ? "Sim" : "Não";

    private static async Task RegistrarAberturaModuloAsync(string modulo)
    {
        try
        {
            await AuditService.RegistrarAsync(
                "MODULO_ABERTO",
                "modulo",
                modulo,
                "SUCESSO",
                $"Módulo '{modulo}' aberto pelo usuário.",
                "FormMenu");
        }
        catch { }
    }

    private static void AbrirTela(Form tela)
    {
        using (tela)
        {
            tela.StartPosition = FormStartPosition.CenterParent;
            tela.ShowDialog();
        }
    }
}
