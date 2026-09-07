using System.Globalization;
using System.Text;

namespace Excel2GuanLiDe;

public static class CsvExporter
{
    public static void ExportNodes(string path, IEnumerable<NodeRow> nodes)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine("NodeId,Station,StationText,GroundElevation,PipeBottomElevation,Pressure");
        foreach (var n in nodes)
            sw.WriteLine(string.Join(",", Q(n.NodeId), n.Station.ToString("0.###", CultureInfo.InvariantCulture), Q(n.StationText),
                Num(n.GroundElevation), Num(n.PipeBottomElevation), Q(n.Pressure)));
    }

    public static void ExportRows(string path, IEnumerable<PipeRow> rows)
    {
        using var sw = new StreamWriter(path, false, new UTF8Encoding(true));
        sw.WriteLine("SourceRow,NodeId,Station,StationText,GroundElevation,PipeBottomElevation,Diameter,Material,StartNode,EndNode,Pressure,Remark");
        foreach (var x in rows)
            sw.WriteLine(string.Join(",", x.SourceRow, Q(x.NodeId), x.Station.ToString("0.###", CultureInfo.InvariantCulture), Q(x.StationText),
                Num(x.GroundElevation), Num(x.PipeBottomElevation), Num(x.Diameter), Q(x.Material), Q(x.StartNode), Q(x.EndNode), Q(x.Pressure), Q(x.Remark)));
    }

    private static string Num(double? v) => v.HasValue ? v.Value.ToString("0.###", CultureInfo.InvariantCulture) : "";
    private static string Q(string? s) => $"\"{(s ?? "").Replace("\"", "\"\"")}";
}
