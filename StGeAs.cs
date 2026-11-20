using StGeAsdesignerwindow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class Openstaad
{
    public class Plate
    {
        public int Number { get; set; }
        public Dictionary<int, double[]> Corners { get; set; } = new Dictionary<int, double[]>();
        public double Xmax { get; set; }
        public double Xmin { get; set; }
        public double Zmax { get; set; }
        public double Zmin { get; set; }

    }

    public static OpenSTAADUI.OSGeometryUI Geometry;
    public static Dictionary<int, double[]> nodeincidence;
    public static int[] Allselectedplates;
    public static List<Plate> plates;
    public static int Noofselectedplates, NOOFSELECTEDNODES, NOOFSELECTEDBEAMS;
    public static List<(string type, List<int>)> Allgroupedplates;
    public static Dictionary<int, List<int>> Plateincidence = new Dictionary<int, List<int>>();
    public static List<int> Undoplates = new List<int>();
    public static List<int> Deleteplates = new List<int>();
    public static HashSet<int> undonodes = new HashSet<int>();
    public static List<int> orphans = new List<int>();
    public static List<int> Newplates = new List<int>();
    public static double[,] Multinodes;
    public static int[,] Multiplates;
    public static List<int> nodelist = new List<int>();
    public static int TargetNode, Splitter;
    public static int NNO, PTN, BMN;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main(string[] Args)
    {
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new StGeAsShowWindow());

    }
    public static bool RefreshData()
    {
        try
        {
            OpenSTAADUI.OpenSTAAD os = Marshal.GetActiveObject("StaadPro.OpenSTAAD") as OpenSTAADUI.OpenSTAAD;
            Geometry = os.Geometry;
            Noofselectedplates = Geometry.GetNoOfSelectedPlates();
            NOOFSELECTEDNODES = Geometry.GetNoOfSelectedNodes();
            NOOFSELECTEDBEAMS = Geometry.GetNoOfSelectedBeams();
            if (Noofselectedplates == 0 && NOOFSELECTEDNODES == 0 && NOOFSELECTEDBEAMS == 0)
            {
                MessageBox.Show("Nothing is selected", "StGeAs Info.");
                return false;
            }
            nodeincidence?.Clear();
            plates?.Clear();
            Allselectedplates = new int[0];
            GeometricCalculations();
            return true;
        }

        catch (Exception ex)
        {
            MessageBox.Show("Cannot Reach STAAD:" + ex.Message, "StGeAs Info.");
            return false;
        }
    }
    public static void GeometricCalculations()
    {
        NNO = Geometry.GetLastNodeNo() + 1;
        BMN = PTN = Math.Max(Geometry.GetLastPlateNo() + 1, Geometry.GetLastBeamNo() + 1);
        nodeincidence = new Dictionary<int, double[]>();
        object slectedplates = new int[Noofselectedplates];
        Geometry.GetSelectedPlates(ref slectedplates, 1);
        Allselectedplates = (int[])slectedplates;
        plates = new List<Plate>();
        Dictionary<int, List<int>> platedevidings = new Dictionary<int, List<int>>();
        int selectednodes = Geometry.GetNoOfSelectedNodes();
        object getnodes = new int[selectednodes];
        Geometry.GetSelectedNodes(ref getnodes, 1);
        HashSet<int> selectednodeslist = new HashSet<int>((int[])getnodes);
        foreach (int node in selectednodeslist)
        {
            object x = 0.0, y = 0.0, z = 0.0;
            Geometry.GetNodeCoordinates(node, ref x, ref y, ref z);
            double coordx = Math.Round(Convert.ToDouble(x), 2);
            double coordy = Math.Round(Convert.ToDouble(y), 2);
            double coordz = Math.Round(Convert.ToDouble(z), 2);
            nodeincidence[node] = new double[] { coordx, coordy, coordz };
            nodelist.Add(node);
        }

        foreach (int selectedplate in Allselectedplates)
        {
            object na = 0, nb = 0, nc = 0, nd = 0;
            Geometry.GetPlateIncidence(selectedplate, ref na, ref nb, ref nc, ref nd);
            int n1 = Convert.ToInt32(na); int n2 = Convert.ToInt32(nb);
            int n4 = Convert.ToInt32(nd); int n3 = Convert.ToInt32(nc);
            foreach (int n in new[] { n1, n2, n3, n4 })
            {
                object x = 0.0, y = 0.0, z = 0.0;
                Geometry.GetNodeCoordinates(n, ref x, ref y, ref z);
                double coordx = Math.Round(Convert.ToDouble(x), 2);
                double coordy = Math.Round(Convert.ToDouble(y), 2);
                double coordz = Math.Round(Convert.ToDouble(z), 2);
                nodeincidence[n] = new double[] { coordx, coordy, coordz };
                Plateincidence[selectedplate] = new List<int> { n1, n2, n3, n4 };
            }
            Dictionary<int, double[]> corners = new Dictionary<int, double[]>
                {
                    { n1, nodeincidence[n1]}, { n2, nodeincidence[n2]},
                    { n3, nodeincidence[n3]}, { n4, nodeincidence[n4]},
                };
            Plate plate = new Plate
            {
                Number = selectedplate,
                Corners = corners,
                Xmax = corners.Values.Max(x => x[0]),
                Xmin = corners.Values.Min(x => x[0]),
                Zmax = corners.Values.Max(z => z[2]),
                Zmin = corners.Values.Min(z => z[2]),
            };
            plates.Add(plate);

        }
        var visited = new HashSet<int>();
        Allgroupedplates = new List<(string type, List<int>)>();
        foreach (Plate plate in plates)
        {
            if (visited.Contains(plate.Number)) continue;
            List<int> Group = new List<int>();
            Queue<Plate> queue = new Queue<Plate>();
            queue.Enqueue(plate);
            visited.Add(plate.Number);
            while (queue.Count > 0)
            {
                Plate curentplate = queue.Dequeue();
                Group.Add(curentplate.Number);

                foreach (var corner in curentplate.Corners)
                {
                    foreach (Plate otherplate in plates)
                    {
                        if (visited.Contains(otherplate.Number)) continue;
                        if (otherplate.Corners.Keys.Contains(corner.Key))
                        {
                            visited.Add(otherplate.Number);
                            queue.Enqueue(otherplate);
                        }
                    }
                }
            }

            var corners = plate.Corners.Values.ToList();
            double dz = Math.Abs(corners[1][2] - corners[0][2]);
            double dx = Math.Abs(corners[1][0] - corners[0][0]);
            bool Inclined = dz > 0 && dx > 0;
            if (!Inclined) { Allgroupedplates.Add(("Horizontal", Group)); }
            if (Inclined) { Allgroupedplates.Add(("Inclined", Group)); }
        }
    }
    public static void DIVIDEBYNUMBER(int NX, int NZ)
    {

        foreach (var group in Allgroupedplates)
        {

            if (group.type == "Horizontal")
            {
                var xzlist = new List<(double, double, int)>();
                foreach (var plate in group.Item2)
                {
                    Plate plt = plates.First(n => n.Number == plate);
                    double yc = plt.Corners.First().Value[1];
                    double xlength = Math.Abs(plt.Xmax - plt.Xmin);
                    double zlength = Math.Abs(plt.Zmax - plt.Zmin);
                    var xs = new List<double>(); var zs = new List<double>();
                    foreach (var c in plt.Corners) { xzlist.Add((c.Value[0], c.Value[2], c.Key)); }
                    if (NX == 0) { xs.Add(plt.Xmin); xs.Add(plt.Xmax); }
                    if (NZ == 0) { zs.Add(plt.Zmin); zs.Add(plt.Zmax); }
                    if (NX != 0)
                    {
                        double xstep = xlength / NX;
                        for (int i = 0; i <= NX; i++) { xs.Add(plt.Xmin + i * xstep); }
                    }
                    if (NZ != 0)
                    {
                        double zstep = zlength / NZ;
                        for (int j = 0; j <= NZ; j++) { zs.Add(plt.Zmin + j * zstep); }
                    }
                    xs.Sort(); zs.Sort();
                    int pindex = 0; int index = 0;
                    Multinodes = new double[xs.Count * zs.Count, 3];
                    int[,] Grid = new int[xs.Count, zs.Count];
                    for (int i = 0; i < xs.Count; i++)
                        for (int j = 0; j < zs.Count; j++)
                        {
                            bool found = false;
                            foreach (var n in xzlist)
                            {
                                if (Math.Abs(xs[i] - n.Item1) < 0.01 && Math.Abs(zs[j] - n.Item2) < 0.01)
                                {
                                    Grid[i, j] = n.Item3;
                                    found = true;
                                    break;
                                }
                            }
                            if (found) continue;
                            xzlist.Add((xs[i], zs[j], NNO));
                            Multinodes[index, 0] = xs[i];
                            Multinodes[index, 1] = yc;
                            Multinodes[index, 2] = zs[j];
                            Grid[i, j] = NNO;
                            undonodes.Add(NNO);
                            NNO++; index++;
                        }
                    var trim = new double[index, 3];
                    Buffer.BlockCopy(Multinodes, 0, trim, 0, index * 3 * sizeof(double));
                    Multinodes = trim;
                    Geometry.AddMultipleNodes(Multinodes);
                    Multiplates = new int[xs.Count * zs.Count, 4];
                    for (int i = 0; i < xs.Count - 1; i++)
                        for (int j = 0; j < zs.Count - 1; j++)
                        {
                            Multiplates[pindex, 0] = Grid[i, j];
                            Multiplates[pindex, 1] = Grid[i, j + 1];
                            Multiplates[pindex, 2] = Grid[i + 1, j + 1];
                            Multiplates[pindex, 3] = Grid[i + 1, j];
                            pindex++; Newplates.Add(PTN);
                            PTN++;

                        }
                    Geometry.AddMultiplePlates(Multiplates);
                    Undoplates.Add(plt.Number); Deleteplates.Add(plt.Number);
                }
            }

            if (group.type == "Inclined")
            {
                foreach (var plate in group.Item2)
                {
                    Plate plt = plates.First(n => n.Number == plate);
                    var corners = plt.Corners.Values.ToList();
                    double dx10 = corners[1][0] - corners[0][0]; double dz10 = corners[1][2] - corners[0][2];
                    double dx30 = corners[3][0] - corners[0][0]; double dz30 = corners[3][2] - corners[0][2];
                    double length10 = Math.Sqrt(dx10 * dx10 + dz10 * dz10);
                    double length30 = Math.Sqrt(dx30 * dx30 + dz30 * dz30);
                    int nx = NX == 0 ? 1 : NX;
                    int nz = NZ == 0 ? 1 : NZ;
                    double step10 = length10 / nx; double step30 = length30 / nz;
                    int[,] Grid = new int[nx + 1, nz + 1];
                    Multinodes = new double[(nx + 1) * (nz + 1), 3];
                    int index = 0; double yc = plt.Corners.First().Value[1];
                    for (int i = 0; i <= nx; i++)
                        for (int j = 0; j <= nz; j++)
                        {
                            double L10 = step10 * i; double L30 = step30 * j;
                            double L10ratio = L10 / length10; double L30ratio = L30 / length30;
                            double x = corners[0][0] + L10ratio * dx10 + L30ratio * dx30;
                            double z = corners[0][2] + L10ratio * dz10 + L30ratio * dz30;
                            bool found = false;
                            foreach (var node in nodeincidence)
                            {
                                if (Math.Abs(node.Value[0] - x) < 0.01 && Math.Abs(node.Value[2] - z) < 0.01)
                                {
                                    Grid[i, j] = node.Key;
                                    found = true;
                                    break;
                                }
                            }
                            if (found) continue;
                            Multinodes[index, 0] = x;
                            Multinodes[index, 1] = yc;
                            Multinodes[index, 2] = z;
                            Grid[i, j] = NNO;
                            undonodes.Add(NNO);
                            NNO++; index++;
                        }
                    var trim = new double[index, 3];
                    Buffer.BlockCopy(Multinodes, 0, trim, 0, index * 3 * sizeof(double));
                    Multinodes = trim;
                    Geometry.AddMultipleNodes(Multinodes);
                    int pindex = 0;
                    Multiplates = new int[nx * nz, 4];
                    for (int i = 0; i < nx; i++)
                        for (int j = 0; j < nz; j++)
                        {
                            Multiplates[pindex, 0] = Grid[i, j];
                            Multiplates[pindex, 1] = Grid[i + 1, j];
                            Multiplates[pindex, 2] = Grid[i + 1, j + 1];
                            Multiplates[pindex, 3] = Grid[i, j + 1];
                            Newplates.Add(PTN);
                            PTN++; pindex++;
                        }
                    Geometry.AddMultiplePlates(Multiplates);
                    Undoplates.Add(plt.Number); Deleteplates.Add(plt.Number);
                }
            }
        }
    }
    public static void DivideBySIZE(double maxsize, double minsize)
    {
        foreach (var Group in Allgroupedplates)
        {
            bool issmalledge = false;
            if (Group.type == "Horizontal")
            {
                var xzlist = new List<(double, double, int)>();
                var xlist = new List<(double val, bool issplitter)>();
                var zlist = new List<(double val, bool issplitter)>();
                Dictionary<int, double[]> splitters = new Dictionary<int, double[]>();
                double yc = 0;
                foreach (var plate in Group.Item2)
                {

                    Plate plt = plates.First(n => n.Number == plate);
                    yc = plt.Corners.First().Value[1];
                    double xlength = Math.Abs(plt.Xmax - plt.Xmin);
                    double zlength = Math.Abs(plt.Zmax - plt.Zmin);
                    int NX = (int)Math.Ceiling(xlength / maxsize);
                    int NZ = (int)Math.Ceiling(zlength / maxsize);
                    double xstep = xlength / NX; double zstep = zlength / NZ;
                    double[] xs = new double[NX + 1], zs = new double[NZ + 1];

                    foreach (var node in nodeincidence)
                    {
                        double x = nodeincidence[node.Key][0];
                        double y = nodeincidence[node.Key][1];
                        double z = nodeincidence[node.Key][2];
                        if (plt.Corners.ContainsKey(node.Key)) continue;
                        if (Math.Abs(yc - y) > 0) continue;
                        if (x < plt.Xmax && x > plt.Xmin && z < plt.Zmax && z > plt.Zmin)
                        {
                            splitters[node.Key] = new double[] { x, y, z };
                        }
                    }
                    foreach (var cor in plt.Corners) { xzlist.Add((cor.Value[0], cor.Value[2], cor.Key)); }
                    foreach (var spliter in splitters)
                    {
                        xzlist.Add((spliter.Value[0], spliter.Value[2], spliter.Key));
                        double dx1 = Math.Abs(plt.Xmax - spliter.Value[0]);
                        if (dx1 < minsize) { MessageBox.Show($"MinSize shall be = {Math.Round(dx1, 2)}", "StGeAs.Info"); issmalledge = true; break; }
                        double dx2 = Math.Abs(plt.Xmin - spliter.Value[0]);                                     
                        if (dx2 < minsize) { MessageBox.Show($"MinSize shall be = {Math.Round(dx2, 2)}", "StGeAs.Info"); issmalledge = true; break; }
                        double dz1 = Math.Abs(plt.Zmax - spliter.Value[2]);                                     
                        if (dz1 < minsize) { MessageBox.Show($"MinSize shall be = {Math.Round(dz1, 2)}", "StGeAs.Info"); issmalledge = true; break; }
                        double dz2 = Math.Abs(plt.Zmin - spliter.Value[2]);                                     
                        if (dz2 < minsize) { MessageBox.Show($"MinSize shall be = {Math.Round(dz2, 2)}", "StGeAs.Info"); issmalledge = true; break; }
                    }
                    if (issmalledge) break;
                    for (int i = 0; i <= NX; i++)
                        for (int j = 0; j <= NZ; j++)
                        {
                            xs[i] = plt.Xmin + i * xstep;
                            zs[j] = plt.Zmin + j * zstep;
                        }

                    foreach (var v in splitters.Values) { xlist.Add((v[0], true)); zlist.Add((v[2], true)); }
                    foreach (var x in xs) { xlist.Add((x, false)); }
                    foreach (var z in zs) { zlist.Add((z, false)); }
                    Undoplates.Add(plt.Number); Deleteplates.Add(plt.Number);
                }

                xlist.Sort(); zlist.Sort();
                for (int i = xlist.Count - 1; i > 0; i--)
                {
                    double dis = Math.Abs(xlist[i].val - xlist[i - 1].val);
                    if (dis < minsize)
                    {
                        if (!xlist[i].issplitter) { xlist.RemoveAt(i); continue; }
                        if (!xlist[i - 1].issplitter) { xlist.RemoveAt(i - 1); }
                    }
                }
                for (int i = zlist.Count - 1; i > 0; i--)
                {
                    double dis = Math.Abs(zlist[i].val - zlist[i - 1].val);
                    if (dis < minsize)
                    {
                        if (!zlist[i].issplitter) { zlist.RemoveAt(i); continue; }
                        if (!zlist[i - 1].issplitter) { zlist.RemoveAt(i - 1); }
                    }
                }
                for (int i = xlist.Count - 1; i > 0; i--)
                {
                    if (Math.Abs(xlist[i].val - xlist[i - 1].val) < 0.03) { xlist.RemoveAt(i); }
                }
                for (int i = zlist.Count - 1; i > 0; i--)
                {
                    if (Math.Abs(zlist[i].val - zlist[i - 1].val) < 0.03) { zlist.RemoveAt(i); }
                }
                xlist.Sort(); zlist.Sort();
                int[,] Grid = new int[xlist.Count, zlist.Count];
                Multinodes = new double[xlist.Count * zlist.Count, 3];
                int index = 0;
                for (int i = 0; i < xlist.Count; i++)
                    for (int j = 0; j < zlist.Count; j++)
                    {
                        bool found = false;
                        foreach (var n in xzlist)
                        {
                            if (Math.Abs(xlist[i].val - n.Item1) < 0.05 && Math.Abs(zlist[j].val - n.Item2) < 0.05)
                            {
                                Grid[i, j] = n.Item3;
                                found = true;
                                break;
                            }
                        }
                        if (found) continue;
                        xzlist.Add((xlist[i].val, zlist[j].val, NNO));
                        Multinodes[index, 0] = xlist[i].val;
                        Multinodes[index, 1] = yc;
                        Multinodes[index, 2] = zlist[j].val;
                        Grid[i, j] = NNO;
                        undonodes.Add(NNO);
                        NNO++; index++;
                    }
                var trim = new double[index, 3];
                Buffer.BlockCopy(Multinodes, 0, trim, 0, index * 3 * sizeof(double));
                Multinodes = trim;
                Geometry.AddMultipleNodes(Multinodes);
                int pindex = 0;
                Multiplates = new int[(xlist.Count - 1) * (zlist.Count - 1), 4];
                for (int i = 0; i < xlist.Count - 1; i++)
                    for (int j = 0; j < zlist.Count - 1; j++)
                    {

                        Multiplates[pindex, 0] = Grid[i, j];
                        Multiplates[pindex, 1] = Grid[i, j + 1];
                        Multiplates[pindex, 2] = Grid[i + 1, j + 1];
                        Multiplates[pindex, 3] = Grid[i + 1, j];
                        Newplates.Add(PTN);
                        PTN++; pindex++;

                    }
                Geometry.AddMultiplePlates(Multiplates);

            }
            if (Group.type == "Inclined")
            {
                Dictionary<int, double[]> splitters = new Dictionary<int, double[]>();
                var xzlist = new List<(double, double, int)>();
                Dictionary<int, double[]> groupcorners = new Dictionary<int, double[]>();
                foreach (var plat in Group.Item2)
                {
                    Plate newplate = plates.First(N => N.Number == plat);
                    foreach (var corner in newplate.Corners)
                    {
                        groupcorners[corner.Key] = (nodeincidence[corner.Key]);
                    }

                    Plate plate = plates.First(N => N.Number == plat);
                    double yc = plate.Corners.First().Value[1];
                    foreach (var node in nodeincidence)
                    {
                        double x = nodeincidence[node.Key][0];
                        double y = nodeincidence[node.Key][1];
                        double z = nodeincidence[node.Key][2];
                        if (plate.Corners.ContainsKey(node.Key)) continue;
                        var xz = plate.Corners.Values.Select(xv => (xv[0], xv[2])).ToList();
                        if (Math.Abs(yc - y) > 0) continue;
                        if (x < plate.Xmin || x > plate.Xmax) continue;
                        if (z < plate.Zmin || z > plate.Zmax) continue;
                        bool pos = false, neg = false;
                        for (int i = 0; i < 4; i++)
                        {
                            int j = (i + 1) % 4;
                            double f = Math.Round((xz[j].Item2 - xz[i].Item2) * (x - xz[i].Item1) -
                                                   (xz[j].Item1 - xz[i].Item1) * (z - xz[i].Item2), 2);

                            if (f > 0) { pos = true; }
                            if (f < 0) { neg = true; }
                        }
                        if (pos && neg) continue;
                        splitters[node.Key] = new double[] { x, y, z };
                    }
                    var corners = plate.Corners.Values.ToList();
                    double dx10 = corners[1][0] - corners[0][0]; double dz10 = corners[1][2] - corners[0][2];
                    double dx30 = corners[3][0] - corners[0][0]; double dz30 = corners[3][2] - corners[0][2];
                    double length10 = Math.Sqrt(dx10 * dx10 + dz10 * dz10);
                    double length30 = Math.Sqrt(dx30 * dx30 + dz30 * dz30);
                    int NX = (int)Math.Ceiling(length10 / maxsize);
                    int NZ = (int)Math.Ceiling(length30 / maxsize);
                    double step10 = length10 / NX; double step30 = length30 / NZ;
                    int[,] Grid = new int[NX == 0 ? 2 : NX + 1, NZ == 0 ? 2 : NZ + 1];

                    var xlist = new List<(double val, bool issplitter)>();
                    var zlist = new List<(double val, bool issplitter)>();
                    double area = Math.Abs(dx10 * dz30 - dz10 * dx30);
                    double d10 = area / length10;
                    double d30 = area / length30;
                    foreach (var cor in plate.Corners) { xzlist.Add((cor.Value[0], cor.Value[2], cor.Key)); }
                    foreach (var splitter in splitters)
                    {
                        xzlist.Add((splitter.Value[0], splitter.Value[2], splitter.Key));
                        double dx = splitter.Value[0] - corners[0][0];
                        double dz = splitter.Value[2] - corners[0][2];
                        double l10ratio = (dx * dz30 - dz * dx30) / (dx10 * dz30 - dx30 * dz10);
                        double l30ratio = (dz * dx10 - dz10 * dx) / (dx10 * dz30 - dx30 * dz10);
                        xlist.Add((l10ratio, true));
                        zlist.Add((l30ratio, true));
                        double d = Math.Min(Math.Min(l10ratio, 1 - l10ratio) * d30, Math.Min(l30ratio, 1 - l30ratio) * d10);
                        d = Math.Abs(d);
                        if (d < minsize)
                        { MessageBox.Show($"MinSize shall be = {Math.Round(d, 2)}", "StGeAs Info"); return; }
                    }
                    for (int i = 0; i <= NX; i++)
                    {
                        double L10 = step10 * i;
                        double L10ratio = L10 / length10;
                        xlist.Add((L10ratio, false));
                    }
                    for (int j = 0; j <= NZ; j++)
                    {
                        double L30 = step30 * j;
                        double L30ratio = L30 / length30;
                        zlist.Add((L30ratio, false));

                    }
                    xlist.Sort();
                    for (int i = xlist.Count - 1; i > 0; i--)
                    {
                        double dis = Math.Abs(xlist[i].val - xlist[i - 1].val) * length10;
                        if (dis < minsize)
                        {
                            if (!xlist[i].issplitter) { xlist.RemoveAt(i); continue; }
                            if (!xlist[i - 1].issplitter) { xlist.RemoveAt(i - 1); }
                        }
                    }
                    zlist.Sort();
                    for (int i = zlist.Count - 1; i > 0; i--)
                    {
                        double dis = Math.Abs(zlist[i].val - zlist[i - 1].val) * length30;
                        if (dis < minsize)
                        {
                            if (!zlist[i].issplitter) { zlist.RemoveAt(i); continue; }
                            if (!zlist[i - 1].issplitter) { zlist.RemoveAt(i - 1); }
                        }
                    }

                    int[,] grid = new int[xlist.Count, zlist.Count];
                    xlist.Sort(); zlist.Sort(); int index = 0;
                    Multinodes = new double[xlist.Count * zlist.Count, 3];
                    for (int i = 0; i < xlist.Count; i++)
                        for (int j = 0; j < zlist.Count; j++)
                        {
                            double x = corners[0][0] + xlist[i].val * dx10 + zlist[j].val * dx30;
                            double z = corners[0][2] + xlist[i].val * dz10 + zlist[j].val * dz30;
                            bool found = false;
                            foreach (var n in xzlist)
                            {
                                if (Math.Abs(x - n.Item1) < 0.05 && Math.Abs(z - n.Item2) < 0.05)
                                {
                                    grid[i, j] = n.Item3;
                                    found = true;
                                    break;
                                }
                            }
                            if (found) continue;
                            xzlist.Add((x, z, NNO));
                            Multinodes[index, 0] = x;
                            Multinodes[index, 1] = yc;
                            Multinodes[index, 2] = z;
                            grid[i, j] = NNO;
                            undonodes.Add(NNO);
                            NNO++; index++;
                        }
                    var trim = new double[index, 3];
                    Buffer.BlockCopy(Multinodes, 0, trim, 0, index * 3 * sizeof(double));
                    Multinodes = trim;
                    Geometry.AddMultipleNodes(Multinodes);
                    int pindex = 0;
                    Multiplates = new int[(xlist.Count - 1) * (zlist.Count - 1), 4];
                    for (int i = 0; i < xlist.Count - 1; i++)
                        for (int j = 0; j < zlist.Count - 1; j++)
                        {
                            Multiplates[pindex, 0] = grid[i, j];
                            Multiplates[pindex, 1] = grid[i, j + 1];
                            Multiplates[pindex, 2] = grid[i + 1, j + 1];
                            Multiplates[pindex, 3] = grid[i + 1, j];
                            Newplates.Add(PTN);
                            PTN++; pindex++;
                        }
                    Geometry.AddMultiplePlates(Multiplates);
                    Undoplates.Add(plate.Number); Deleteplates.Add(plate.Number);
                }
            }
        }
    }
    public static void DivideWithSelectedNodes()
    {
        foreach (var group in Allgroupedplates)
        {
            Dictionary<int, double[]> dividingnode = new Dictionary<int, double[]>();
            Dictionary<int, double[]> groupcorners = new Dictionary<int, double[]>();
            if (group.type == "Horizontal")
            {
                int index = 0; int pindex = 0; double yc = 0;
                var GRd = new Dictionary<(double, double), int>();
                foreach (int plate1 in group.Item2)
                {
                    Plate plate = plates.First(N => N.Number == plate1);
                    Dictionary<int, double[]> newnodes = new Dictionary<int, double[]>();
                    yc = plate.Corners.First().Value[1];
                    foreach (var corner in plate.Corners)
                    {
                        groupcorners[corner.Key] = (nodeincidence[corner.Key]);
                    }
                    foreach (var node in nodeincidence)
                    {
                        double x = nodeincidence[node.Key][0];
                        double y = nodeincidence[node.Key][1];
                        double z = nodeincidence[node.Key][2];
                        if (plate.Corners.ContainsKey(node.Key)) continue;
                        if (x < plate.Xmin || x > plate.Xmax) continue;
                        if (z < plate.Zmin || z > plate.Zmax) continue;
                        if (Math.Abs(yc - y) > 0) continue;

                        dividingnode[node.Key] = new double[] { x, y, z };
                    }
                    Undoplates.Add(plate.Number); Deleteplates.Add(plate.Number);
                }
                var grid = groupcorners.Concat(dividingnode).ToDictionary(v => v.Key, v => v.Value);
                var xs = grid.Values.Select(v => v[0]).Distinct().ToList();
                var zs = grid.Values.Select(v => v[2]).Distinct().ToList();
                xs.Sort(); zs.Sort();
                int[,] Grid = new int[xs.Count, zs.Count];
                Multinodes = new double[xs.Count * zs.Count, 3];
                for (int i = 0; i < xs.Count; i++)
                    for (int j = 0; j < zs.Count; j++)
                    {
                        bool found = false;
                        foreach (var node in grid)
                        {
                            if (Math.Abs(xs[i] - node.Value[0]) < 0.01 && Math.Abs(zs[j] - node.Value[2]) < 0.01)
                            {
                                Grid[i, j] = node.Key;
                                found = true;
                                break;
                            }
                        }
                        if (found) continue;
                        bool orphan = true;
                        foreach (int plt in group.Item2)
                        {
                            Plate plate = plates.First(N => N.Number == plt);
                            if (xs[i] < plate.Xmin || xs[i] > plate.Xmax) continue;
                            if (zs[j] < plate.Zmin || zs[j] > plate.Zmax) continue;
                            orphan = false; break;
                        }
                        if (orphan) continue;
                        Multinodes[index, 0] = xs[i];
                        Multinodes[index, 1] = yc;
                        Multinodes[index, 2] = zs[j];
                        Grid[i, j] = NNO;
                        undonodes.Add(NNO);
                        NNO++; index++;
                    }
                var trim = new double[index, 3];
                Buffer.BlockCopy(Multinodes, 0, trim, 0, index * 3 * sizeof(double));
                Multinodes = trim;
                Geometry.AddMultipleNodes(Multinodes);
                Multiplates = new int[xs.Count * zs.Count, 4];
                for (int i = 0; i < xs.Count - 1; i++)
                    for (int j = 0; j < zs.Count - 1; j++)
                    {
                        if (Grid[i, j] == 0) continue;
                        if (Grid[i, j + 1] == 0) continue;
                        if (Grid[i + 1, j + 1] == 0) continue;
                        if (Grid[i + 1, j] == 0) continue;
                        Multiplates[pindex, 0] = Grid[i, j];
                        Multiplates[pindex, 1] = Grid[i, j + 1];
                        Multiplates[pindex, 2] = Grid[i + 1, j + 1];
                        Multiplates[pindex, 3] = Grid[i + 1, j];
                        Newplates.Add(PTN++);
                        pindex++;
                    }
                Geometry.AddMultiplePlates(Multiplates);
            }

            if (group.type == "Inclined")
            {
                var xzlist = new List<(double, double, int)>();
                foreach (int plt in group.Item2)
                {
                    Plate plate = plates.First(N => N.Number == plt);
                    double yc = plate.Corners.First().Value[1];
                    foreach (var corner in plate.Corners)
                    {
                        groupcorners[corner.Key] = (nodeincidence[corner.Key]);
                        xzlist.Add((corner.Value[0], corner.Value[2], corner.Key));
                    }
                    foreach (var node in nodeincidence)
                    {
                        double x = nodeincidence[node.Key][0];
                        double y = nodeincidence[node.Key][1];
                        double z = nodeincidence[node.Key][2];
                        if (plate.Corners.ContainsKey(node.Key)) continue;
                        var xz = plate.Corners.Values.Select(xv => (xv[0], xv[2])).ToList();
                        if (Math.Abs(yc - y) > 0) continue;
                        if (x < plate.Xmin || x > plate.Xmax) continue;
                        if (z < plate.Zmin || z > plate.Zmax) continue;
                        bool pos = false, neg = false;

                        for (int i = 0; i < 4; i++)
                        {
                            int j = (i + 1) % 4;
                            double f = Math.Round((xz[j].Item2 - xz[i].Item2) * (x - xz[i].Item1) - (xz[j].Item1 - xz[i].Item1) * (z - xz[i].Item2), 2);
                            if (f > 0) { pos = true; }
                            if (f < 0) { neg = true; }
                        }
                        if (pos && neg) continue;
                        dividingnode[node.Key] = new double[] { x, y, z };
                        xzlist.Add((node.Value[0], node.Value[2], node.Key));
                    }

                    //for any intersection of two nodes we will use the intersection of two line equasions.
                    //(z1-z3)/(x1-x3)= slope of line13. and (z2-z3)/(x2-x3)=slope of line23.
                    // / then store the known values in the lists to solve the equasions
                    List<double> xs = new List<double>(); List<double> zs = new List<double>();
                    var corners = plate.Corners.Values.ToList();
                    double dx10 = corners[1][0] - corners[0][0];
                    double dz10 = corners[1][2] - corners[0][2];
                    double dx30 = corners[3][0] - corners[0][0];
                    double dz30 = corners[3][2] - corners[0][2];
                    double S10 = dz10 / dx10; double S30 = dz30 / dx30;
                    xs.Add(corners[0][2] - S30 * corners[0][0]);
                    xs.Add(corners[1][2] - S30 * corners[1][0]);
                    zs.Add(corners[0][2] - S10 * corners[0][0]);
                    zs.Add(corners[3][2] - S10 * corners[3][0]);
                    foreach (var d in dividingnode.Values)
                    {
                        xs.Add(d[2] - d[0] * S30);
                        zs.Add(d[2] - d[0] * S10);
                    }
                    int[,] Grid = new int[xs.Count, zs.Count];
                    xs.Sort(); zs.Sort();
                    Multinodes = new double[xs.Count * zs.Count, 3]; int index = 0;
                    for (int i = 0; i < xs.Count; i++)
                        for (int j = 0; j < zs.Count; j++)
                        {
                            double x = (zs[j] - xs[i]) / (S30 - S10);
                            double z = xs[i] + x * S30;
                            bool found = false;
                            foreach (var n in xzlist)
                            {
                                if (Math.Abs(x - n.Item1) < 0.05 && Math.Abs(z - n.Item2) < 0.05)
                                {
                                    Grid[i, j] = n.Item3;
                                    found = true;
                                    break;
                                }
                            }
                            if (found) continue;
                            xzlist.Add((x, z, NNO));
                            Multinodes[index, 0] = x;
                            Multinodes[index, 1] = yc;
                            Multinodes[index, 2] = z;
                            Grid[i, j] = NNO;
                            undonodes.Add(NNO);
                            NNO++; index++;
                        }
                    var trim = new double[index, 3];
                    Buffer.BlockCopy(Multinodes, 0, trim, 0, index * 3 * sizeof(double));
                    Multinodes = trim;
                    Geometry.AddMultipleNodes(Multinodes);
                    Multiplates = new int[xs.Count * zs.Count, 4]; int pindex = 0;
                    for (int i = 0; i < xs.Count - 1; i++)
                        for (int j = 0; j < zs.Count - 1; j++)
                        {
                            Multiplates[pindex, 0] = Grid[i, j];
                            Multiplates[pindex, 1] = Grid[i, j + 1];
                            Multiplates[pindex, 2] = Grid[i + 1, j + 1];
                            Multiplates[pindex, 3] = Grid[i + 1, j];
                            Newplates.Add(PTN++);
                            pindex++;
                        }
                    Geometry.AddMultiplePlates(Multiplates);
                    Undoplates.Add(plate.Number); Deleteplates.Add(plate.Number);
                }
            }
        }
    }
    public static void UNDO()
    {
        foreach (int plate in Newplates) { Geometry.DeletePlate(plate); }
        foreach (int node in undonodes) { Geometry.DeleteNode(node); }
        foreach (int plt in Undoplates)
        {
            Plate plate = plates.FirstOrDefault(n => n.Number == plt);
            if (plate == null) continue;
            foreach (var cor in plate.Corners)
            {
                Geometry.CreateNode(cor.Key, cor.Value[0], cor.Value[1], cor.Value[2]);
            }
            var keys = plate.Corners.Keys.ToList();
            Geometry.CreatePlate(plate.Number, keys[0], keys[1], keys[2], keys[3]);
        }
        Newplates.Clear(); undonodes.Clear(); Undoplates.Clear();
    }
    public static void DeletePlates()
    {
        foreach (var plt in Deleteplates)
        {
            Geometry.DeletePlate(plt);
            Plate plate = plates.First(n => n.Number == plt);
            foreach (var cor in plate.Corners)
            {
                dynamic orphan = Geometry.IsOrphanNode(cor.Key);
                if (orphan) { Geometry.DeleteNode(cor.Key); }
            }
        }
        Deleteplates.Clear();
    }
    public static void MERGEPLATES()
    {
        var overlapped = new List<(int, int)>();
        foreach (var Group in Allgroupedplates)
        {
            if (Group.Item2.Count <= 0) continue;
            double tol = 0.01;
            if (NOOFSELECTEDNODES > 0 || NOOFSELECTEDBEAMS > 0) { MessageBox.Show("Please select plates only", "StGeAs.Info"); return; }
            bool isoverlapped = false;
            foreach (var G in Group.Item2)
            {
                Plate plate = plates.First(N => N.Number == G);

                foreach (var G1 in Group.Item2)
                {
                    if (G == G1) continue;
                    Plate plate1 = plates.First(N => N.Number == G1);
                    bool check1 = plate.Xmin > plate1.Xmin - tol && plate.Xmax < plate1.Xmax + tol;
                    bool check2 = plate.Zmin > plate1.Zmin - tol && plate.Zmax < plate1.Zmax + tol;
                    if (check1 && check2)
                    {
                        isoverlapped = true;
                        overlapped.Add((plate.Number, plate1.Number));
                    }
                }
                if (isoverlapped) break;
            }
            if (isoverlapped) continue;

            var corners = new Dictionary<int, double[]>();
            int P = 0;
            foreach (var G in Group.Item2)
            {
                P = Group.Item2[0];
                Plate plate = plates.FirstOrDefault(N => N.Number == G);
                if (plate == null) continue;
                foreach (var corner in plate.Corners)
                {
                    if (corners.ContainsKey(corner.Key))
                    {
                        corners.Remove(corner.Key);
                        continue;
                    }
                    corners[corner.Key] = corner.Value;
                }
                if (plate.Number != P) { Deleteplates.Add(plate.Number); }
                Undoplates.Add(plate.Number);
            }
            if (corners.Count != 4) continue;
            var xs = corners.Values.Select(x => Math.Round(x[0], 2)).Distinct().OrderBy(v => v).ToList();
            var zs = corners.Values.Select(z => Math.Round(z[2], 2)).Distinct().OrderBy(v => v).ToList();
            xs.Sort(); zs.Sort(); int[,] Grid = new int[xs.Count, zs.Count];
            var Ns = new List<int>();
            for (int i = 0; i < xs.Count; i++)
                for (int j = 0; j < zs.Count; j++)
                {
                    foreach (var n in corners)
                    {
                        if (Math.Abs(xs[i] - n.Value[0]) < 0.01 && Math.Abs(zs[j] - n.Value[2]) < 0.01)
                        {
                            Ns.Add(n.Key);
                        }
                    }
                }
            if (Group.type == "Horizontal") { Geometry.CreatePlate(P, Ns[0], Ns[1], Ns[3], Ns[2]); }
            if (Group.type == "Inclined") { Geometry.CreatePlate(P, Ns[0], Ns[1], Ns[3], Ns[2]); }
        }

        Geometry.ClearPlateSelection(); Geometry.ClearNodeSelection();
        foreach (var plate in overlapped)
        {
            Geometry.SelectPlate(plate.Item1);
            Geometry.SelectPlate(plate.Item2);
        }
        if (overlapped.Count > 0)
        {
            MessageBox.Show("Can not merge the selected because of overlaps", "StGeAs.Info");
            overlapped.Clear();
        }

    }
    public static void DELETEDUPLICATESANDORPHANNODES(double tol)
    {

        Dictionary<(long, long, long), List<int>> Duplicates = new Dictionary<(long, long, long), List<int>>();
        foreach (var n in nodeincidence)
        {
            var k = ((long)Math.Round(n.Value[0] / tol),
                    (long)Math.Round(n.Value[1] / tol),
                    (long)Math.Round(n.Value[2] / tol));
            if (!Duplicates.ContainsKey(k))
            {
                Duplicates[k] = new List<int>(n.Key);
            }
            Duplicates[k].Add(n.Key);
        }
        var set = Duplicates.Values.ToList();
        foreach (var v in set)
        {
            for (int i = 0; i < v.Count; i++)
            {
                if (v.Count < 2) continue;
                dynamic orphan = Geometry.IsOrphanNode(v[i]);
                if (orphan is false) continue; orphans.Add(v[i]);
                Geometry.DeleteNode(v[i]);
            }
        }
        if (orphans.Count == 0)
        {
            MessageBox.Show("No duplicates", "StGeAs.Info");
        }
    }
    public static void Reversetheplate()
    {
        foreach (int plt in Allselectedplates)
        {
            Plate plate = plates.First(n => n.Number == plt);
            var cor = plate.Corners.Keys.ToList();
            int n1, n2, n3, n4, ptn; ptn = plt;
            n1 = cor[3]; n2 = cor[2]; n3 = cor[1]; n4 = cor[0];
            Geometry.CreatePlate(plt, n1, n2, n3, n4);
        }
    }
    public static void CheckOverlapped()
    {
        var overlapped = new List<(int, int)>();
        double tol = 0.01;
        if (NOOFSELECTEDNODES > 0 || NOOFSELECTEDBEAMS > 0) { MessageBox.Show("Please select plates only", "StGeAs.Info"); return; }
        for (int i = 0; i < plates.Count; i++)
        {
            var plate = plates[i];
            for (int j = i + 1; j < plates.Count; j++)
            {
                var plate1 = plates[j];
                bool check1 = plate.Xmin > plate1.Xmin - tol && plate.Xmax < plate1.Xmax + tol;
                bool check2 = plate.Zmin > plate1.Zmin - tol && plate.Zmax < plate1.Zmax + tol;
                if (check1 && check2)
                {
                    overlapped.Add((plate.Number, plate1.Number));
                }
            }
        }
        Geometry.ClearPlateSelection();
        Geometry.ClearNodeSelection();
        if (overlapped.Count > 0)
        {
            MessageBox.Show(string.Join("  Overlapped\n", overlapped) + "  Overlapped", "StGeAs Info");
        }
        foreach (var plate in overlapped)
        {
            Geometry.SelectPlate(plate.Item1);
            Geometry.SelectPlate(plate.Item2);
        }
        if (overlapped.Count == 0) { MessageBox.Show("No overlaps ", "StGeAs Info"); }
    }
    public static void Getcountofselected()
    {
        MessageBox.Show($"Nodes = {NOOFSELECTEDNODES}\nPlates = {Noofselectedplates}\nBeams = {NOOFSELECTEDBEAMS}", "StGeAs Info");
    }
    public static void Align(bool Keepx, bool Keepy, bool Keepz)
    {
        object x = 0.0, y = 0.0, z = 0.0;
        Geometry.GetNodeIncidence(TargetNode, ref x, ref y, ref z);
        foreach (int node in nodelist)
        {
            if (Keepx) { x = nodeincidence[node][0]; }
            if (Keepy) { y = nodeincidence[node][1]; }
            if (Keepz) { z = nodeincidence[node][2]; }
            Geometry.CreateNode(node, x, y, z);
        }
    }
    public static void Plateoffset(double xoffset, double zoffset)
    {
        foreach (int selectedplate in Allselectedplates)
        {
            Plate plate = plates.First(N => N.Number == selectedplate);

            foreach (var n in plate.Corners)
            {

                if (xoffset != 0)
                {
                    if (n.Value[0] == plate.Xmin) { n.Value[0] -= xoffset; }
                    if (n.Value[0] == plate.Xmax) { n.Value[0] += xoffset; }
                }
                if (zoffset != 0)
                {
                    if (n.Value[2] == plate.Zmin) { n.Value[2] -= zoffset; }
                    if (n.Value[2] == plate.Zmax) { n.Value[2] += zoffset; }
                }
                if (xoffset == 0 && zoffset == 0) continue;
                Geometry.CreateNode(n.Key, n.Value[0], n.Value[1], n.Value[2]);
            }
            Undoplates.Add(plate.Number);
        }
        MessageBox.Show("Offset Succedded", "StGeAs Info");
    }
    public static void Cutter(bool Atx, bool Atz)
    {
        object x = 0.0, y = 0.0, z = 0.0;
        Geometry.GetNodeIncidence(Splitter, ref x, ref y, ref z);

        foreach (var Group in Allgroupedplates)
        {
            if (Group.type == "Horizontal")
            {
                foreach (var P in Group.Item2)
                {
                    Plate plate = plates.First(N => N.Number == P);
                    List<double> xs = plate.Corners.Values.Select(v => v[0]).Distinct().ToList();
                    List<double> zs = plate.Corners.Values.Select(v => v[2]).Distinct().ToList();
                    if (Atx) { xs.Add(Math.Round(Convert.ToDouble(x), 4)); }
                    if (Atz) { zs.Add(Math.Round(Convert.ToDouble(z), 4)); }
                    xs.Sort(); zs.Sort();
                    int[,] Grid = new int[xs.Count, zs.Count];
                    double yc = plate.Corners.Values.First()[1];

                    for (int i = 0; i < xs.Count; i++)
                        for (int j = 0; j < zs.Count; j++)
                        {
                            bool found = false;
                            foreach (var node in nodeincidence)
                            {
                                if (Math.Abs(node.Value[0] - xs[i]) < 0.001 && Math.Abs(node.Value[2] - zs[j]) < 0.001)
                                {
                                    Grid[i, j] = node.Key;
                                    found = true;
                                    break;
                                }
                            }
                            if (found) continue;
                            Geometry.CreateNode(NNO, xs[i], yc, zs[j]);
                            Grid[i, j] = NNO;
                            undonodes.Add(NNO);
                            NNO++;
                        }
                    for (int i = 0; i < xs.Count - 1; i++)
                        for (int j = 0; j < zs.Count - 1; j++)
                        {
                            Geometry.CreatePlate(PTN, Grid[i, j], Grid[i + 1, j], Grid[i + 1, j + 1], Grid[i, j + 1]);
                            Newplates.Add(PTN);
                            PTN++;
                        }
                    Undoplates.Add(plate.Number); Deleteplates.Add(plate.Number);
                }
            }
        }
    }
}
