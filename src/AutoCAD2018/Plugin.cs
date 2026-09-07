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

[assembly: CommandClass(typeof(Excel2GuanLiDe.AutoCAD2018.Plugin))]

namespace Excel2GuanLiDe.AutoCAD2018
{
    public class Plugin
    {
        [CommandMethod("EXCEL2GLD", CommandFlags.Modal)]
        public void Excel2GuanLiDe()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            try
            {
                string file = SelectFile();
                if (string.IsNullOrEmpty(file)) return;

                List<NodeData> rows = DataReader.Read(file);
                if (rows.Count == 0)
                {
                    ed.WriteMessage("\n未读取到有效数据。请检查表头和桩号。\n");
                    return;
                }

                rows = rows.OrderBy(x => x.Station).GroupBy(x => Math.Round(x.Station, 3))
                    .Select(g => Merge(g.ToList())).ToList();

                double scale = GetDouble(ed, "输入出图比例 <2000>: ", 2000);
                double textHeight = GetDouble(ed, "输入文字高度 <3>: ", 3);
                double yOffset = GetDouble(ed, "输入文字上下偏移 <5>: ", 5);

                Generate(doc, rows, scale, textHeight, yOffset);
                ed.WriteMessage($"\nExcel2GuanLiDe 完成：生成 {rows.Count} 个节点。\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nExcel2GuanLiDe 错误：" + ex.Message + "\n");
            }
        }

        [CommandMethod("GLDPIPE", CommandFlags.Modal)]
        public void GeneratePipe()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            try
            {
                string file = SelectFile();
                if (string.IsNullOrEmpty(file)) return;
                List<NodeData> rows = DataReader.Read(file).OrderBy(x => x.Station).ToList();
                if (rows.Count < 2) { ed.WriteMessage("\n至少需要2个节点。\n"); return; }
                double scale = GetDouble(ed, "输入出图比例 <2000>: ", 2000);
                GeneratePipeEntities(doc, rows, scale);
                ed.WriteMessage($"\n已按桩号顺序生成 {rows.Count - 1} 段管线。\n");
            }
            catch (System.Exception ex) { ed.WriteMessage("\nGLDPIPE 错误：" + ex.Message + "\n"); }
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

        private static NodeData Merge(List<NodeData> list)
        {
            NodeData x = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(x.Node)) x.Node = list[i].Node;
                if (!x.Elevation.HasValue) x.Elevation = list[i].Elevation;
                if (!x.BottomElevation.HasValue) x.BottomElevation = list[i].BottomElevation;
                if (!x.Pressure.HasValue) x.Pressure = list[i].Pressure;
                if (string.IsNullOrWhiteSpace(x.Diameter)) x.Diameter = list[i].Diameter;
                if (string.IsNullOrWhiteSpace(x.Material)) x.Material = list[i].Material;
            }
            return x;
        }

        private static void Generate(Document doc, List<NodeData> rows, double scale, double textHeight, double yOffset)
        {
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ObjectId nodeLayer = EnsureLayer(db, tr, "Excel2GLD_NODE");
                ObjectId textLayer = EnsureLayer(db, tr, "Excel2GLD_TEXT");
                int index = 1;
                foreach (NodeData row in rows)
                {
                    double x = row.Station / scale;
                    Point3d p = new Point3d(x, 0, 0);
                    using (Circle c = new Circle(p, Vector3d.ZAxis, Math.Max(textHeight * 0.35, 0.8)))
                    { c.LayerId = nodeLayer; ms.AppendEntity(c); tr.AddNewlyCreatedDBObject(c, true); }
                    string node = string.IsNullOrWhiteSpace(row.Node) ? "N" + index.ToString("000") : row.Node;
                    AddText(ms, tr, textLayer, FormatStation(row.Station), new Point3d(x, yOffset, 0), textHeight, Math.PI / 2.0);
                    if (row.Elevation.HasValue) AddText(ms, tr, textLayer, row.Elevation.Value.ToString("0.000", CultureInfo.InvariantCulture), new Point3d(x, -yOffset, 0), textHeight, Math.PI / 2.0);
                    AddText(ms, tr, textLayer, node, new Point3d(x, yOffset * 2.0, 0), textHeight, 0);
                    index++;
                }
                tr.Commit();
            }
        }

        private static void GeneratePipeEntities(Document doc, List<NodeData> rows, double scale)
        {
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ObjectId layer = EnsureLayer(db, tr, "Excel2GLD_PIPE");
                for (int i = 0; i < rows.Count - 1; i++)
                {
                    Point3d a = new Point3d(rows[i].Station / scale, 0, 0);
                    Point3d b = new Point3d(rows[i + 1].Station / scale, 0, 0);
                    using (Line line = new Line(a, b)) { line.LayerId = layer; ms.AppendEntity(line); tr.AddNewlyCreatedDBObject(line, true); }
                }
                tr.Commit();
            }
        }

        private static void AddText(BlockTableRecord ms, Transaction tr, ObjectId layer, string value, Point3d p, double h, double rotation)
        {
            using (DBText t = new DBText())
            {
                t.TextString = value ?? "";
                t.Height = h;
                t.Position = p;
                t.Rotation = rotation;
                t.LayerId = layer;
                ms.AppendEntity(t);
                tr.AddNewlyCreatedDBObject(t, true);
            }
        }

        private static ObjectId EnsureLayer(Database db, Transaction tr, string name)
        {
            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return lt[name];
            lt.UpgradeOpen();
            LayerTableRecord rec = new LayerTableRecord { Name = name };
            ObjectId id = lt.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
            return id;
        }

        private static string FormatStation(double value)
        {
            int km = (int)Math.Floor(value / 1000.0);
            double m = value - km * 1000.0;
            return "K" + km.ToString(CultureInfo.InvariantCulture) + "+" + m.ToString("000.###", CultureInfo.InvariantCulture);
        }
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
    }

    internal static class DataReader
    {
        public static List<NodeData> Read(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".csv") return ReadCsv(path);
            return ReadExcel(path);
        }

        private static List<NodeData> ReadExcel(string path)
        {
            List<NodeData> result = new List<NodeData>();
            using (XLWorkbook wb = new XLWorkbook(path))
            {
                IXLWorksheet ws = wb.Worksheets.First();
                Dictionary<string, int> h = Headers(ws.Row(1));
                for (int r = 2; r <= ws.LastRowUsed().RowNumber(); r++)
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
            string[] heads = Split(lines[0]);
            Dictionary<string, int> h = Headers(heads);
            List<NodeData> result = new List<NodeData>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cells = Split(lines[i]);
                NodeData n = ParseRow(x => x < cells.Length ? cells[x] : "", h);
                if (n != null) result.Add(n);
            }
            return result;
        }

        private static string[] Split(string line)
        {
            List<string> cells = new List<string>();
            StringBuilder b = new StringBuilder();
            bool quote = false;
            foreach (char c in line)
            {
                if (c == '"') { quote = !quote; continue; }
                if (c == ',' && !quote) { cells.Add(b.ToString().Trim()); b.Clear(); } else b.Append(c);
            }
            cells.Add(b.ToString().Trim());
            return cells.ToArray();
        }

        private static Dictionary<string, int> Headers(IXLRow row)
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            foreach (IXLCell c in row.CellsUsed()) d[Norm(c.GetString())] = c.Address.ColumnNumber;
            return d;
        }
        private static Dictionary<string, int> Headers(string[] heads)
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            for (int i = 0; i < heads.Length; i++) d[Norm(heads[i])] = i;
            return d;
        }
        private static int Find(Dictionary<string, int> h, params string[] names)
        {
            foreach (string n in names) if (h.ContainsKey(Norm(n))) return h[Norm(n)];
            return -1;
        }
        private static NodeData ParseRow(Func<int, string> get, Dictionary<string, int> h)
        {
            int si = Find(h, "桩号", "里程", "桩号(米)", "station", "chainage");
            if (si < 0) return null;
            double station;
            if (!TryStation(get(si), out station)) return null;
            NodeData n = new NodeData { Station = station };
            int ni = Find(h, "节点", "节点编号", "node", "nodeid");
            int ei = Find(h, "地面高程", "高程", "地面标高", "elevation", "ground elevation");
            int bi = Find(h, "管底高程", "管底标高", "invert", "invert elevation");
            int pi = Find(h, "节点水压", "水压", "pressure", "node pressure");
            int di = Find(h, "管径", "直径", "diameter");
            int mi = Find(h, "管材", "材质", "material");
            n.Node = ni >= 0 ? get(ni) : "";
            n.Elevation = Number(ei >= 0 ? get(ei) : "");
            n.BottomElevation = Number(bi >= 0 ? get(bi) : "");
            n.Pressure = Number(pi >= 0 ? get(pi) : "");
            n.Diameter = di >= 0 ? get(di) : "";
            n.Material = mi >= 0 ? get(mi) : "";
            return n;
        }
        private static string Norm(string s) => (s ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "");
        private static double? Number(string s) { double v; return double.TryParse((s ?? "").Replace("，", ","), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? (double?)v : null; }
        private static bool TryStation(string s, out double value)
        {
            value = 0; s = (s ?? "").Trim().ToUpperInvariant().Replace("+", "+");
            if (s.StartsWith("K")) s = s.Substring(1);
            if (s.Contains("+")) { string[] a = s.Split('+'); double km, m; if (double.TryParse(a[0], NumberStyles.Any, CultureInfo.InvariantCulture, out km) && double.TryParse(a[1], NumberStyles.Any, CultureInfo.InvariantCulture, out m)) { value = km * 1000 + m; return true; } }
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
    }
}
