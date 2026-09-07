using System.Text;

namespace Excel2GuanLiDe;

public sealed class MainForm : Form
{
    private readonly TextBox fileBox = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = true, AllowUserToAddRows = false };
    private readonly TextBox logBox = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };
    private ImportResult? current;

    public MainForm()
    {
        Text = "Excel2GuanLiDe｜Excel → 管立得辅助工具";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;

        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 76, ColumnCount = 4, Padding = new Padding(10) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        var open = new Button { Text = "① 导入 Excel", Dock = DockStyle.Fill };
        var export = new Button { Text = "② 导出节点 CSV", Dock = DockStyle.Fill, Enabled = false };
        var exportRows = new Button { Text = "③ 导出完整数据", Dock = DockStyle.Fill, Enabled = false };
        open.Click += (_, _) => ImportExcel(export, exportRows);
        export.Click += (_, _) => ExportNodes();
        exportRows.Click += (_, _) => ExportRows();

        top.Controls.Add(open, 0, 0);
        top.Controls.Add(fileBox, 1, 0);
        top.Controls.Add(export, 2, 0);
        top.Controls.Add(exportRows, 3, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var dataPage = new TabPage("节点数据");
        dataPage.Controls.Add(grid);
        var logPage = new TabPage("检查结果");
        logPage.Controls.Add(logBox);
        tabs.TabPages.Add(dataPage);
        tabs.TabPages.Add(logPage);

        Controls.Add(tabs);
        Controls.Add(top);
    }

    private void ImportExcel(Button export, Button exportRows)
    {
        using var dialog = new OpenFileDialog { Filter = "Excel 文件|*.xlsx;*.xlsm|所有文件|*.*" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            current = ExcelImporter.Import(dialog.FileName);
            fileBox.Text = dialog.FileName;
            grid.DataSource = current.Nodes.Select(x => new
            {
                x.NodeId, x.StationText, x.Station,
                GroundElevation = x.GroundElevation?.ToString("0.###") ?? "",
                PipeBottomElevation = x.PipeBottomElevation?.ToString("0.###") ?? "",
                x.Pressure
            }).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"识别成功：{current.Rows.Count} 行，生成节点：{current.Nodes.Count} 个。\r\n");
            sb.AppendLine("字段映射：");
            foreach (var x in current.Mapping) sb.AppendLine($"  {x.Key} ← {x.Value}");
            if (current.Warnings.Count > 0)
            {
                sb.AppendLine("\r\n检查/提示：");
                foreach (var w in current.Warnings) sb.AppendLine("  ⚠ " + w);
            }
            else sb.AppendLine("\r\n✓ 未发现明显数据问题。");
            logBox.Text = sb.ToString();
            export.Enabled = current.Nodes.Count > 0;
            exportRows.Enabled = current.Rows.Count > 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportNodes()
    {
        if (current is null) return;
        using var dialog = new SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = "管立得_节点数据.csv" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        CsvExporter.ExportNodes(dialog.FileName, current.Nodes);
        MessageBox.Show("节点 CSV 已导出，可交给 AutoLISP 继续生成 CAD 节点。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportRows()
    {
        if (current is null) return;
        using var dialog = new SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = "管立得_标准化数据.csv" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        CsvExporter.ExportRows(dialog.FileName, current.Rows);
        MessageBox.Show("标准化数据已导出。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
