using System.Drawing.Drawing2D;

namespace OrionInc.Forms;

/// <summary>
/// Padronização visual dos módulos do Orion Inc.
/// Não contém acesso ao banco e pode ser usado por qualquer Form.
/// </summary>
public static class OrionUi
{
    public static readonly Color Navy = Color.FromArgb(20, 36, 59);
    public static readonly Color Background = Color.FromArgb(243, 246, 250);
    public static readonly Color Card = Color.White;
    public static readonly Color Border = Color.FromArgb(214, 222, 232);
    public static readonly Color Text = Color.FromArgb(30, 41, 59);
    public static readonly Color Muted = Color.FromArgb(100, 116, 139);
    public static readonly Color Primary = Color.FromArgb(35, 86, 139);
    public static readonly Color Danger = Color.FromArgb(178, 61, 61);
    public static readonly Color Success = Color.FromArgb(35, 128, 88);

    public static void ApplyFormTheme(Form form, string? subtitle = null)
    {
        form.BackColor = Background;
        form.Font = new Font("Segoe UI", 9.5f);
        form.StartPosition = FormStartPosition.CenterParent;
        form.KeyPreview = true;

        form.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                form.Close();
                e.Handled = true;
            }
        };

        if (string.IsNullOrWhiteSpace(subtitle))
            return;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Navy,
            Padding = new Padding(20, 10, 20, 8)
        };

        var title = new Label
        {
            Text = form.Text,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 8)
        };

        var desc = new Label
        {
            Text = subtitle,
            ForeColor = Color.FromArgb(205, 220, 238),
            Font = new Font("Segoe UI", 9),
            AutoSize = true,
            Location = new Point(22, 40)
        };

        header.Controls.Add(title);
        header.Controls.Add(desc);
        form.Controls.Add(header);
        form.Controls.SetChildIndex(header, 0);
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Card;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Color.FromArgb(230, 235, 241);
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Navy,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Padding = new Padding(8, 6, 8, 6),
            SelectionBackColor = Navy,
            SelectionForeColor = Color.White
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Text,
            Font = new Font("Segoe UI", 9),
            SelectionBackColor = Color.FromArgb(224, 236, 249),
            SelectionForeColor = Text,
            Padding = new Padding(7, 5, 7, 5)
        };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Text,
            SelectionBackColor = Color.FromArgb(224, 236, 249),
            SelectionForeColor = Text
        };
        grid.RowTemplate.Height = 34;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.AllowUserToResizeRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.RowHeadersVisible = false;
    }

    public static void StyleButton(Button button, bool primary = false, bool danger = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = danger ? Danger : Border;
        button.BackColor = danger ? Danger : (primary ? Primary : Card);
        button.ForeColor = danger || primary ? Color.White : Text;
        button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.Height = Math.Max(button.Height, 34);

        button.MouseEnter += (_, _) =>
        {
            button.BackColor = danger
                ? Color.FromArgb(154, 48, 48)
                : primary
                    ? Color.FromArgb(29, 72, 117)
                    : Color.FromArgb(235, 240, 247);
        };

        button.MouseLeave += (_, _) =>
        {
            button.BackColor = danger ? Danger : (primary ? Primary : Card);
        };
    }

    public static void StyleInputs(params Control[] controls)
    {
        foreach (var control in controls)
        {
            if (control is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = Color.White;
                textBox.ForeColor = Text;
                textBox.Font = new Font("Segoe UI", 9.5f);
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.BackColor = Color.White;
                comboBox.ForeColor = Text;
                comboBox.Font = new Font("Segoe UI", 9.5f);
            }
        }
    }

    public static void StyleModuleControls(Form form)
    {
        foreach (var button in FindControls<Button>(form))
            StyleButton(button);

        foreach (var input in FindControls<Control>(form)
                     .Where(c => c is TextBox or ComboBox))
            StyleInputs(input);

        foreach (var grid in FindControls<DataGridView>(form))
            StyleGrid(grid);
    }

    private static IEnumerable<T> FindControls<T>(Control root)
        where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T typed)
                yield return typed;

            foreach (var nested in FindControls<T>(child))
                yield return nested;
        }
    }
}
