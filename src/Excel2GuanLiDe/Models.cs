namespace Excel2GuanLiDe;

public sealed class PipeRow
{
    public int SourceRow { get; set; }
    public string NodeId { get; set; } = "";
    public string StationText { get; set; } = "";
    public double Station { get; set; }
    public double? GroundElevation { get; set; }
    public double? PipeBottomElevation { get; set; }
    public double? Diameter { get; set; }
    public string Material { get; set; } = "";
    public string StartNode { get; set; } = "";
    public string EndNode { get; set; } = "";
    public string Pressure { get; set; } = "";
    public string Remark { get; set; } = "";
}

public sealed class NodeRow
{
    public string NodeId { get; set; } = "";
    public string StationText { get; set; } = "";
    public double Station { get; set; }
    public double? GroundElevation { get; set; }
    public double? PipeBottomElevation { get; set; }
    public string Pressure { get; set; } = "";
}

public sealed class ImportResult
{
    public List<PipeRow> Rows { get; } = [];
    public List<NodeRow> Nodes { get; } = [];
    public List<string> Warnings { get; } = [];
    public Dictionary<string, string> Mapping { get; } = new();
}
