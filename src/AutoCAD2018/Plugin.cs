using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using ClosedXML.Excel;

using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Excel2GuanLiDe.AutoCAD2018.Plugin))]

namespace Excel2GuanLiDe.AutoCAD2018
{
    public class Plugin
    {
        private static double HorizontalScale = 2000;
        private static double VerticalScale = 200;
        private static double TextHeight = 3;
        private static double YOffset = 5;

        [CommandMethod("EXCEL2GLD", CommandFlags.Modal)]
        public void Excel2GuanLiDe()
        {
            Document doc = AcadApplication.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            try
            {
                string file = SelectFile();
                if (string.IsNullOrEmpty(file)) return;
                List<NodeData> rows = Normalize(DataReader.Read(file));
                if (rows.Count == 0) { ed.WriteMessage("\n未读取到有效数据，请检查 Excel 表头和桩号。\n"); return; }
                HorizontalScale = GetDouble(ed, "输入水平出图比例 <2000>: ", HorizontalScale);
                TextHeight = GetDouble(ed, "输入文字高度 <3>: ", TextHeight);
                YOffset = GetDouble(ed, "输入文字偏移 <5>: ", YOffset);
                GenerateNodes(doc, rows);
                GenerateTopology(doc, rows, HorizontalScale, rows.Any(x => !string.IsNullOrWhiteSpace(x.Start) && !string.IsNullOrWhiteSpace(x.End)));
                ed.WriteMessage("\nExcel2GuanLiDe：已完成节点、管线、桩号、高程和管径/管材标注。\n");
            }
            catch (System.Exception ex) { ed.WriteMessage("\nExcel2GuanLiDe 错误：" + ex.Message + "\n"); }
        }

        [CommandMethod("GLDPIPE", CommandFlags.Modal)]
        public void GeneratePipe()
        {
            Document doc = AcadApplication.DocumentManager.MdiActiveDocument; Editor ed = doc.Editor;
            try
            {
                string file = SelectFile(); if (string.IsNullOrEmpty(file)) return;
                var rows = Normalize(DataReader.Read(file)); if (rows.Count < 2) { ed.WriteMessage("\n至少需要2个有效节点。\n"); return; }
                HorizontalScale = GetDouble(ed, "输入水平出图比例 <2000>: ", HorizontalScale);
                GenerateTopology(doc, rows, HorizontalScale, rows.Any(x => !string.IsNullOrWhiteSpace(x.Start) && !string.IsNullOrWhiteSpace(x.End)));
                ed.WriteMessage("\nGLDPIPE：管线生成完成。\n");
            }
            catch (System.Exception ex) { ed.WriteMessage("\nGLDPIPE 错误：" + ex.Message + "\n"); }
        }

        [CommandMethod("GLDTOPO", CommandFlags.Modal)]
        public void GenerateTopologyCommand()
        {
            Document doc = AcadApplication.DocumentManager.MdiActiveDocument; Editor ed = doc.Editor;
            try
            {
                string file = SelectFile(); if (string.IsNullOrEmpty(file)) return;
                var rows = Normalize(DataReader.Read(file)); if (rows.Count < 2) { ed.WriteMessage("\n至少需要2个有效节点。\n"); return; }
                HorizontalScale = GetDouble(ed, "输入水平出图比例 <2000>: ", HorizontalScale);
                GenerateNodes(doc, rows);
                bool explicitTopology = rows.Any(x => !string.IsNullOrWhiteSpace(x.Start) && !string.IsNullOrWhiteSpace(x.End));
                GenerateTopology(doc, rows, HorizontalScale, explicitTopology);
                ed.WriteMessage(explicitTopology ? "\nGLDTOPO：按起点/终点生成拓扑，支持分支和环路。\n" : "\nGLDTOPO：按桩号顺序生成管线。\n");
            }
            catch (System.Exception ex) { ed.WriteMessage("\nGLDTOPO 错误：" + ex.Message + "\n"); }
        }

        [CommandMethod("GLDPROFILE", CommandFlags.Modal)]
        public void GenerateProfile()
        {
            Document doc = AcadApplication.DocumentManager.MdiActiveDocument; Editor ed = doc.Editor;
            try
            {
                string file = SelectFile(); if (string.IsNullOrEmpty(file)) return;
                var rows = Normalize(DataReader.Read(file)); if (rows.Count < 2) { ed.WriteMessage("\n至少需要2个有效节点。\n"); return; }
                HorizontalScale = GetDouble(ed, "输入水平比例 <2000>: ", HorizontalScale);
                VerticalScale = GetDouble(ed, "输入纵向比例 <200>: ", VerticalScale);
                TextHeight = GetDouble(ed, "输入文字高度 <3>: ", TextHeight);
                GenerateProfile(doc, rows, HorizontalScale, VerticalScale);
                ed.WriteMessage("\nGLDPROFILE：纵断面生成完成。\n");
            }
            catch (System.Exception ex) { ed.WriteMessage("\nGLDPROFILE 错误：" + ex.Message + "\n"); }
        }

        [CommandMethod("GLDSETTINGS", CommandFlags.Modal)]
        public void Settings()
        {
            Editor ed = AcadApplication.DocumentManager.MdiActiveDocument.Editor;
            HorizontalScale = GetDouble(ed, "水平比例 <" + HorizontalScale.ToString("0") + ">: ", HorizontalScale);
            VerticalScale = GetDouble(ed, "纵向比例 <" + VerticalScale.ToString("0") + ">: ", VerticalScale);
            TextHeight = GetDouble(ed, "文字高度 <" + TextHeight.ToString("0.###") + ">: ", TextHeight);
            YOffset = GetDouble(ed, "文字偏移 <" + YOffset.ToString("0.###") + ">: ", YOffset);
            ed.WriteMessage("\nGLDSETTINGS：参数已保存到当前 CAD 会话。\n");
        }

        private static string SelectFile()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "选择管网 Excel / CSV 数据";
                dlg.Filter = "Excel/CSV|*.xlsx;*.xlsm;*.csv|Excel|*.xlsx;*.xlsm|CSV|*.csv";
                return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
            }
        }

        private static double GetDouble(Editor ed, string prompt, double def)
        {
            PromptDoubleOptions opt = new PromptDoubleOptions("\n" + prompt) { DefaultValue = def, AllowNone = true, AllowZero = false, AllowNegative = false };
            PromptDoubleResult r = ed.GetDouble(opt);
            return r.Status == PromptStatus.OK ? r.Value : def;
        }

        private static List<NodeData> Normalize(List<NodeData> rows)
        {
            return rows.Where(x => x != null).OrderBy(x => x.Station).GroupBy(x => Math.Round(x.Station, 3)).Select(g => Merge(g.ToList())).ToList();
        }

        private static NodeData Merge(List<NodeData> list)
        {
            NodeData x = new NodeData { Station = list[0].Station };
            foreach (NodeData y in list)
            {
                if (string.IsNullOrWhiteSpace(x.Node)) x.Node = y.Node;
                if (!x.Elevation.HasValue) x.Elevation = y.Elevation;
                if (!x.BottomElevation.HasValue) x.BottomElevation = y.BottomElevation;
                if (!x.Pressure.HasValue) x.Pressure = y.Pressure;
                if (string.IsNullOrWhiteSpace(x.Diameter)) x.Diameter = y.Diameter;
                if (string.IsNullOrWhiteSpace(x.Material)) x.Material = y.Material;
                if (string.IsNullOrWhiteSpace(x.Start)) x.Start = y.Start;
                if (string.IsNullOrWhiteSpace(x.End)) x.End = y.End;
            }
            return x;
        }

        private static void GenerateNodes(Document doc, List<NodeData> rows)
        {
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ObjectId nodeLayer = EnsureLayer(db, tr, "Excel2GLD_NODE");
                ObjectId textLayer = EnsureLayer(db, tr, "Excel2GLD_TEXT");
                for (int i = 0; i < rows.Count; i++)
                {
                    NodeData row = rows[i];
                    double x = row.Station / HorizontalScale;
                    Point3d p = new Point3d(x, 0, 0);
                    using (Circle c = new Circle(p, Vector3d.ZAxis, Math.Max(TextHeight * 0.35, 0.8))) { c.LayerId = nodeLayer; ms.AppendEntity(c); tr.AddNewlyCreatedDBObject(c, true); }
                    string node = string.IsNullOrWhiteSpace(row.Node) ? "N" + (i + 1).ToString("000") : row.Node;
                    AddText(ms, tr, textLayer, FormatStation(row.Station), new Point3d(x, YOffset, 0), TextHeight, Math.PI / 2.0);
                    if (row.Elevation.HasValue) AddText(ms, tr, textLayer, row.Elevation.Value.ToString("0.000", CultureInfo.InvariantCulture), new Point3d(x, -YOffset, 0), TextHeight, Math.PI / 2.0);
                    AddText(ms, tr, textLayer, node, new Point3d(x, YOffset * 2, 0), TextHeight, 0);
                    if (row.Pressure.HasValue) AddText(ms, tr, textLayer, "P=" + row.Pressure.Value.ToString("0.00", CultureInfo.InvariantCulture), new Point3d(x, YOffset * 3, 0), TextHeight, 0);
                }
                tr.Commit();
            }
        }

        private static void GenerateTopology(Document doc, List<NodeData> rows, double scale, bool explicitTopology)
        {
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ObjectId pipeLayer = EnsureLayer(db, tr, "Excel2GLD_PIPE");
                ObjectId labelLayer = EnsureLayer(db, tr, "Excel2GLD_PIPE_TEXT");
                Dictionary<string, NodeData> map = BuildNodeMap(rows);
                if (explicitTopology)
                {
                    foreach (NodeData r in rows)
                    {
                        NodeData a = ResolveNode(r.Start, map, rows); NodeData b = ResolveNode(r.End, map, rows);
                        if (a == null || b == null || Math.Abs(a.Station - b.Station) < 0.001) continue;
                        AddPipe(ms, tr, pipeLayer, a.Station / scale, b.Station / scale);
                        string label = string.Join(" ", new[] { r.Diameter, r.Material }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        if (!string.IsNullOrWhiteSpace(label)) AddText(ms, tr, labelLayer, label, new Point3d((a.Station + b.Station) / (2 * scale), YOffset, 0), TextHeight, 0);
                    }
                }
                else
                {
                    for (int i = 0; i < rows.Count - 1; i++)
                    {
                        AddPipe(ms, tr, pipeLayer, rows[i].Station / scale, rows[i + 1].Station / scale);
                        string label = string.Join(" ", new[] { rows[i].Diameter, rows[i].Material }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        if (!string.IsNullOrWhiteSpace(label)) AddText(ms, tr, labelLayer, label, new Point3d((rows[i].Station + rows[i + 1].Station) / (2 * scale), YOffset, 0), TextHeight, 0);
                    }
                }
                tr.Commit();
            }
        }

        private static void AddPipe(BlockTableRecord ms, Transaction tr, ObjectId layer, double x1, double x2)
        {
            using (Line line = new Line(new Point3d(x1, 0, 0), new Point3d(x2, 0, 0))) { line.LayerId = layer; ms.AppendEntity(line); tr.AddNewlyCreatedDBObject(line, true); }
        }

        private static Dictionary<string, NodeData> BuildNodeMap(List<NodeData> rows)
        {
            var d = new Dictionary<string, NodeData>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows.Count; i++)
            {
                NodeData n = rows[i];
                d["N" + (i + 1).ToString("000")] = n;
                if (!string.IsNullOrWhiteSpace(n.Node)) d[n.Node.Trim()] = n;
                d[FormatStation(n.Station)] = n;
                d[n.Station.ToString("0.###", CultureInfo.InvariantCulture)] = n;
            }
            return d;
        }

        private static NodeData ResolveNode(string key, Dictionary<string, NodeData> map, List<NodeData> rows)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            NodeData n; if (map.TryGetValue(key.Trim(), out n)) return n;
            double s; if (TryStation(key, out s)) return rows.OrderBy(x => Math.Abs(x.Station - s)).FirstOrDefault(x => Math.Abs(x.Station - s) < 0.01);
            return null;
        }

        private static void GenerateProfile(Document doc, List<NodeData> rows, double hScale, double vScale)
        {
            var valid = rows.Where(x => x.Elevation.HasValue || x.BottomElevation.HasValue).ToList();
            if (valid.Count < 2) throw new InvalidOperationException("缺少地面高程或管底高程数据。");
            double minElev = valid.SelectMany(x => new[] { x.Elevation ?? double.MaxValue, x.BottomElevation ?? double.MaxValue }).Min();
            double baseY = -Math.Floor(minElev / 10.0) * 10.0 - 20.0;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ObjectId ground = EnsureLayer(db, tr, "Excel2GLD_PROFILE_GROUND");
                ObjectId invert = EnsureLayer(db, tr, "Excel2GLD_PROFILE_INVERT");
                ObjectId text = EnsureLayer(db, tr, "Excel2GLD_PROFILE_TEXT");
                for (int i = 0; i < rows.Count - 1; i++)
                {
                    if (rows[i].Elevation.HasValue && rows[i + 1].Elevation.HasValue) AddLine(ms, tr, ground, ProfilePoint(rows[i], rows[i].Elevation.Value, hScale, vScale, baseY), ProfilePoint(rows[i + 1], rows[i + 1].Elevation.Value, hScale, vScale, baseY));
                    if (rows[i].BottomElevation.HasValue && rows[i + 1].BottomElevation.HasValue) AddLine(ms, tr, invert, ProfilePoint(rows[i], rows[i].BottomElevation.Value, hScale, vScale, baseY), ProfilePoint(rows[i + 1], rows[i + 1].BottomElevation.Value, hScale, vScale, baseY));
                }
                foreach (NodeData r in rows)
                {
                    double x = r.Station / hScale;
                    AddText(ms, tr, text, FormatStation(r.Station), new Point3d(x, baseY - YOffset, 0), TextHeight, Math.PI / 2.0);
                    if (r.Elevation.HasValue) AddText(ms, tr, text, "地 " + r.Elevation.Value.ToString("0.000", CultureInfo.InvariantCulture), ProfilePoint(r, r.Elevation.Value, hScale, vScale, baseY), TextHeight, 0);
                    if (r.BottomElevation.HasValue) AddText(ms, tr, text, "底 " + r.BottomElevation.Value.ToString("0.000", CultureInfo.InvariantCulture), new Point3d(x, ProfilePoint(r, r.BottomElevation.Value, hScale, vScale, baseY).Y - TextHeight * 1.5, 0), TextHeight, 0);
                    if (r.Pressure.HasValue) AddText(ms, tr, text, "P=" + r.Pressure.Value.ToString("0.00", CultureInfo.InvariantCulture), new Point3d(x, baseY + YOffset, 0), TextHeight, 0);
                }
                tr.Commit();
            }
        }

        private static Point3d ProfilePoint(NodeData r, double elev, double hs, double vs, double baseY) { return new Point3d(r.Station / hs, baseY + elev / vs, 0); }
        private static void AddLine(BlockTableRecord ms, Transaction tr, ObjectId layer, Point3d a, Point3d b) { using (Line l = new Line(a, b)) { l.LayerId = layer; ms.AppendEntity(l); tr.AddNewlyCreatedDBObject(l, true); } }
        private static void AddText(BlockTableRecord ms, Transaction tr, ObjectId layer, string value, Point3d p, double h, double rotation) { using (DBText t = new DBText()) { t.TextString = value ?? ""; t.Height = h; t.Position = p; t.Rotation = rotation; t.LayerId = layer; ms.AppendEntity(t); tr.AddNewlyCreatedDBObject(t, true); } }
        private static ObjectId EnsureLayer(Database db, Transaction tr, string name) { LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead); if (lt.Has(name)) return lt[name]; lt.UpgradeOpen(); LayerTableRecord rec = new LayerTableRecord { Name = name }; ObjectId id = lt.Add(rec); tr.AddNewlyCreatedDBObject(rec, true); return id; }
        private static string FormatStation(double value) { int km = (int)Math.Floor(value / 1000.0); double m = value - km * 1000.0; return "K" + km.ToString(CultureInfo.InvariantCulture) + "+" + m.ToString("000.###", CultureInfo.InvariantCulture); }
        private static bool TryStation(string s, out double value) { value = 0; s = (s ?? "").Trim().ToUpperInvariant(); if (s.StartsWith("K")) s = s.Substring(1); if (s.Contains("+")) { string[] a = s.Split('+'); double km, m; if (a.Length == 2 && double.TryParse(a[0], NumberStyles.Any, CultureInfo.InvariantCulture, out km) && double.TryParse(a[1], NumberStyles.Any, CultureInfo.InvariantCulture, out m)) { value = km * 1000 + m; return true; } } return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value); }
    }

    public class NodeData
    {
        public double Station;
        public string Node;
        public double? Elevation;
        public double? BottomElevation;
        public double? Pressure;
        public string Diameter;
        public string Material;
        public string Start;
        public string End;
    }

    internal static class DataReader
    {
        public static List<NodeData> Read(string path) { return Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase) ? ReadCsv(path) : ReadExcel(path); }

        private static List<NodeData> ReadExcel(string path)
        {
            var result = new List<NodeData>();
            using (XLWorkbook wb = new XLWorkbook(path))
            {
                IXLWorksheet ws = wb.Worksheets.First();
                Dictionary<string, int> h = Headers(ws.Row(1));
                int last = ws.LastRowUsed() == null ? 1 : ws.LastRowUsed().RowNumber();
                for (int r = 2; r <= last; r++)
                {
                    IXLRow row = ws.Row(r);
                    NodeData n = ParseRow(i => row.Cell(i).GetString(), h);
                    if (n != null) result.Add(n);
                }
            }
            return result;
        }

        private static List<NodeData> ReadCsv(string path)
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length < 2) return new List<NodeData>();
            Dictionary<string, int> h = Headers(Split(lines[0]));
            var result = new List<NodeData>();
            for (int r = 1; r < lines.Length; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r])) continue;
                string[] cells = Split(lines[r]);
                NodeData n = ParseRow(i => i < cells.Length ? cells[i] : "", h);
                if (n != null) result.Add(n);
            }
            return result;
        }

        private static string[] Split(string line)
        {
            var cells = new List<string>(); var b = new StringBuilder(); bool quote = false;
            foreach (char c in line) { if (c == '"') { quote = !quote; continue; } if (c == ',' && !quote) { cells.Add(b.ToString().Trim()); b.Clear(); } else b.Append(c); }
            cells.Add(b.ToString().Trim()); return cells.ToArray();
        }

        private static Dictionary<string, int> Headers(IXLRow row) { var d = new Dictionary<string, int>(); foreach (IXLCell c in row.CellsUsed()) d[Norm(c.GetString())] = c.Address.ColumnNumber; return d; }
        private static Dictionary<string, int> Headers(string[] heads) { var d = new Dictionary<string, int>(); for (int i = 0; i < heads.Length; i++) d[Norm(heads[i])] = i; return d; }
        private static int Find(Dictionary<string, int> h, params string[] names) { foreach (string n in names) { int i; if (h.TryGetValue(Norm(n), out i)) return i; } return -1; }

        private static NodeData ParseRow(Func<int, string> get, Dictionary<string, int> h)
        {
            int si = Find(h, "桩号", "里程", "桩号(米)", "station", "chainage");
            if (si < 0) return null;
            double station; if (!TryStation(get(si), out station)) return null;
            var n = new NodeData { Station = station };
            int ni = Find(h, "节点", "节点编号", "node", "nodeid");
            int ei = Find(h, "地面高程", "高程", "地面标高", "elevation", "ground elevation");
            int bi = Find(h, "管底高程", "管底标高", "invert", "invert elevation");
            int pi = Find(h, "节点水压", "水压", "pressure", "node pressure");
            int di = Find(h, "管径", "直径", "diameter");
            int mi = Find(h, "管材", "材质", "material");
            int ai = Find(h, "起点", "起点节点", "起始节点", "from", "start", "startnode");
            int zi = Find(h, "终点", "终点节点", "终止节点", "to", "end", "endnode");
            n.Node = ni >= 0 ? get(ni) : ""; n.Elevation = Number(ei >= 0 ? get(ei) : ""); n.BottomElevation = Number(bi >= 0 ? get(bi) : ""); n.Pressure = Number(pi >= 0 ? get(pi) : ""); n.Diameter = di >= 0 ? get(di) : ""; n.Material = mi >= 0 ? get(mi) : ""; n.Start = ai >= 0 ? get(ai) : ""; n.End = zi >= 0 ? get(zi) : "";
            return n;
        }

        private static string Norm(string s) { return (s ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("_", ""); }
        private static double? Number(string s) { double v; return double.TryParse((s ?? "").Replace("，", ","), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? (double?)v : null; }
        private static bool TryStation(string s, out double value) { value = 0; s = (s ?? "").Trim().ToUpperInvariant(); if (s.StartsWith("K")) s = s.Substring(1); if (s.Contains("+")) { string[] a = s.Split('+'); double km, m; if (a.Length == 2 && double.TryParse(a[0], NumberStyles.Any, CultureInfo.InvariantCulture, out km) && double.TryParse(a[1], NumberStyles.Any, CultureInfo.InvariantCulture, out m)) { value = km * 1000 + m; return true; } } return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value); }
    }
}