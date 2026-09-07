using ClosedXML.Excel;
using System.Globalization;

namespace Excel2GuanLiDe;

public static class ExcelImporter
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NodeId"] = ["节点", "节点编号", "节点号", "井号", "检查井", "node", "nodeid"],
        ["Station"] = ["桩号", "桩号/里程", "里程", "管线桩号", "station", "chainage"],
        ["GroundElevation"] = ["地面高程", "地面标高", "地坪高程", "原地面高程", "高程", "ground", "groundelevation"],
        ["PipeBottomElevation"] = ["管底高程", "管底标高", "管底", "设计管底高程", "bottom", "invert", "invertelevation"],
        ["Diameter"] = ["管径", "管道直径", "公称直径", "dn", "diameter"],
        ["Material"] = ["管材", "材质", "管道材质", "material"],
        ["StartNode"] = ["起点", "起点节点", "起点编号", "start", "startnode"],
        ["EndNode"] = ["终点", "终点节点", "终点编号", "end", "endnode"],
        ["Pressure"] = ["节点水压", "水压", "自由水头", "压力", "pressure"],
        ["Remark"] = ["备注", "说明", "remark", "note"]
    };

    public static ImportResult Import(string path, int sheetIndex = 1)
    {
        var result = new ImportResult();
        using var wb = new XLWorkbook(path);
        if (wb.Worksheets.Count < sheetIndex) throw new InvalidOperationException("Excel中不存在指定工作表。");
        var ws = wb.Worksheet(sheetIndex);
        var used = ws.RangeUsed() ?? throw new InvalidOperationException("Excel没有有效数据。");
        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = firstCol; c <= lastCol; c++)
        {
            var h = Normalize(ws.Cell(firstRow, c).GetString());
            if (!string.IsNullOrEmpty(h) && !headers.ContainsKey(h)) headers[h] = c;
        }

        foreach (var alias in Aliases)
        {
            var col = headers.FirstOrDefault(x => alias.Value.Any(a => Normalize(a) == x.Key)).Value;
            if (col > 0) result.Mapping[alias.Key] = ws.Cell(firstRow, col).GetString();
        }

        if (!result.Mapping.ContainsKey("Station"))
            throw new InvalidOperationException("未识别到“桩号/里程”列。请检查表头。");

        for (var r = firstRow + 1; r <= lastRow; r++)
        {
            var stationCell = Get(ws, r, result.Mapping, "Station");
            if (string.IsNullOrWhiteSpace(stationCell)) continue;
            if (!StationParser.TryParse(stationCell, out var station))
            {
                result.Warnings.Add($"第{r}行桩号无法识别：{stationCell}");
                continue;
            }

            var row = new PipeRow
            {
                SourceRow = r,
                Station = station,
                StationText = StationParser.Format(station),
                NodeId = Get(ws, r, result.Mapping, "NodeId"),
                GroundElevation = ToNullableDouble(Get(ws, r, result.Mapping, "GroundElevation")),
                PipeBottomElevation = ToNullableDouble(Get(ws, r, result.Mapping, "PipeBottomElevation")),
                Diameter = ToNullableDouble(Get(ws, r, result.Mapping, "Diameter")),
                Material = Get(ws, r, result.Mapping, "Material"),
                StartNode = Get(ws, r, result.Mapping, "StartNode"),
                EndNode = Get(ws, r, result.Mapping, "EndNode"),
                Pressure = Get(ws, r, result.Mapping, "Pressure"),
                Remark = Get(ws, r, result.Mapping, "Remark")
            };
            result.Rows.Add(row);
        }

        result.Rows.Sort((a, b) => a.Station.CompareTo(b.Station));
        BuildNodes(result);
        return result;
    }

    private static void BuildNodes(ImportResult result)
    {
        var groups = result.Rows.GroupBy(x => Math.Round(x.Station, 3)).OrderBy(x => x.Key).ToList();
        var index = 1;
        foreach (var g in groups)
        {
            var first = g.First();
            result.Nodes.Add(new NodeRow
            {
                NodeId = string.IsNullOrWhiteSpace(first.NodeId) ? $"N{index:000}" : first.NodeId,
                Station = g.Key,
                StationText = StationParser.Format(g.Key),
                GroundElevation = g.Select(x => x.GroundElevation).FirstOrDefault(x => x.HasValue),
                PipeBottomElevation = g.Select(x => x.PipeBottomElevation).FirstOrDefault(x => x.HasValue),
                Pressure = g.Select(x => x.Pressure).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? ""
            });
            index++;
        }

        foreach (var duplicate in result.Rows.GroupBy(x => Math.Round(x.Station, 3)).Where(x => x.Count() > 1))
            result.Warnings.Add($"桩号 {StationParser.Format(duplicate.Key)} 出现 {duplicate.Count()} 次，已合并为一个节点。");
    }

    private static string Get(IXLWorksheet ws, int row, Dictionary<string, string> mapping, string field)
    {
        if (!mapping.TryGetValue(field, out var header)) return "";
        var cell = ws.Row(1).CellsUsed().FirstOrDefault(c => Normalize(c.GetString()) == Normalize(header));
        if (cell is null) return "";
        return ws.Cell(row, cell.Address.ColumnNumber).GetFormattedString().Trim();
    }

    private static double? ToNullableDouble(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Replace("DN", "", StringComparison.OrdinalIgnoreCase).Trim();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string Normalize(string s) => s.Trim().Replace(" ", "").Replace("　", "").Replace("/", "").Replace("\\", "");
}
