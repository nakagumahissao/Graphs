using HelixToolkit.Wpf;
using System;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Graphs
{
    public partial class MainWindow : Window
    {
        public enum PlotType { Function, Parametric, Polar, Point, Vector, Plane };
        private PlotType CurrentPlotType = PlotType.Function;
        private double _time = 0;

        public MainWindow()
        {
            InitializeComponent();
            View.Children.Clear();
            SetupScene();
        }

        // ==========================================
        // THE FIX: Centralized Math Context
        // ==========================================
        private Flee.PublicTypes.ExpressionContext GetContext()
        {
            var context = new Flee.PublicTypes.ExpressionContext();
            context.Imports.AddType(typeof(Math));

            // Pre-define ALL variables so Flee never throws a "variable not found" error
            // regardless of which mode you are in.
            context.Variables["x"] = 0.0;
            context.Variables["y"] = 0.0;
            context.Variables["u"] = 0.0;
            context.Variables["v"] = 0.0;
            context.Variables["r"] = 0.0;
            context.Variables["theta"] = 0.0;
            context.Variables["t"] = _time; // Link the engine to our animation clock

            return context;
        }

        // ==========================================
        // COLOR & BRUSH LOGIC (Preserved)
        // ==========================================
        private Color GetRandomBrightColor()
        {
            Random timeRng = new Random((int)(DateTime.Now.Ticks & 0x0000FFFF));
            int skipChannel = timeRng.Next(0, 3);
            byte r = (byte)(skipChannel == 0 ? timeRng.Next(0, 101) : timeRng.Next(160, 256));
            byte g = (byte)(skipChannel == 1 ? timeRng.Next(0, 101) : timeRng.Next(160, 256));
            byte b = (byte)(skipChannel == 2 ? timeRng.Next(0, 101) : timeRng.Next(160, 256));
            return Color.FromRgb(r, g, b);
        }

        private Brush GetVibrantBrush()
        {
            Random timeRng = new Random((int)(DateTime.Now.Ticks & 0x0000FFFF));
            int forcedDarkChannel = timeRng.Next(0, 3);
            byte r = (byte)(forcedDarkChannel == 0 ? timeRng.Next(0, 80) : timeRng.Next(160, 256));
            byte g = (byte)(forcedDarkChannel == 1 ? timeRng.Next(0, 80) : timeRng.Next(160, 256));
            byte b = (byte)(forcedDarkChannel == 2 ? timeRng.Next(0, 80) : timeRng.Next(160, 256));
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private Brush GetSolidNavyBrush()
        {
            Random timeRng = new Random((int)(DateTime.Now.Ticks & 0x0000FFFF));
            Color color = Color.FromRgb((byte)timeRng.Next(0, 41), (byte)timeRng.Next(0, 41), (byte)timeRng.Next(80, 151));
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        // ==========================================
        // SCENE SETUP
        // ==========================================
        private void SetupScene()
        {
            View.Children.Add(new ModelVisual3D { Content = new AmbientLight(Color.FromRgb(80, 80, 80)) });
            View.Children.Add(new ModelVisual3D { Content = new DirectionalLight { Color = Colors.White, Direction = new Vector3D(-1, -1, -2) } });
            View.Children.Add(new ModelVisual3D { Content = new DirectionalLight { Color = Colors.White, Direction = new Vector3D(1, 1, 2) } });

            View.Camera.Position = new Point3D(10, -15, 10);
            View.Camera.LookDirection = new Vector3D(-10, 15, -10);
            View.Camera.UpDirection = new Vector3D(0, 0, 1);

            View.Children.Add(new LinesVisual3D { Color = ((SolidColorBrush)GetVibrantBrush()).Color, Points = new Point3DCollection { new Point3D(-10, 0, 0), new Point3D(10, 0, 0) } });
            View.Children.Add(new LinesVisual3D { Color = ((SolidColorBrush)GetVibrantBrush()).Color, Points = new Point3DCollection { new Point3D(0, -10, 0), new Point3D(0, 10, 0) } });
            View.Children.Add(new LinesVisual3D { Color = ((SolidColorBrush)GetVibrantBrush()).Color, Points = new Point3DCollection { new Point3D(0, 0, -10), new Point3D(0, 0, 10) } });

            View.Children.Add(new GridLinesVisual3D { Fill = GetVibrantBrush(), Width = 0.02, Length = 0.02 });
        }

        private void RenderFinalModel(HelixToolkit.Geometry.MeshBuilder builder)
        {
            var geoMesh = builder.ToMesh();
            MeshGeometry3D wpfMesh = geoMesh.ToWndMeshGeometry3D();
            Color mainColor = GetRandomBrightColor();
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(mainColor)));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.LightSteelBlue), 110));

            View.Children.Add(new ModelVisual3D
            {
                Content = new GeometryModel3D { Geometry = wpfMesh, Material = material, BackMaterial = material }
            });
        }

        // ==========================================
        // PLOTTING METHODS
        // ==========================================
        private void Plot_Function()
        {
            try
            {
                var context = GetContext();
                var expression = context.CompileGeneric<double>(EquationBox.Text);
                double min = -5, max = 5, step = 0.1;
                int nx = (int)((max - min) / step) + 1;
                double[,] zValues = new double[nx, nx];

                for (int i = 0; i < nx; i++)
                {
                    context.Variables["x"] = min + i * step;
                    for (int j = 0; j < nx; j++)
                    {
                        context.Variables["y"] = min + j * step;
                        try { zValues[i, j] = expression.Evaluate(); } catch { zValues[i, j] = double.NaN; }
                    }
                }

                var builder = new HelixToolkit.Geometry.MeshBuilder();
                for (int i = 0; i < nx - 1; i++)
                {
                    for (int j = 0; j < nx - 1; j++)
                    {
                        if (IsInvalid(zValues[i, j], zValues[i + 1, j], zValues[i, j + 1], zValues[i + 1, j + 1])) continue;
                        builder.AddQuad(
                            new Vector3((float)(min + i * step), (float)(min + j * step), (float)zValues[i, j]),
                            new Vector3((float)(min + (i + 1) * step), (float)(min + j * step), (float)zValues[i + 1, j]),
                            new Vector3((float)(min + (i + 1) * step), (float)(min + (j + 1) * step), (float)zValues[i + 1, j + 1]),
                            new Vector3((float)(min + i * step), (float)(min + (j + 1) * step), (float)zValues[i, j + 1]));
                    }
                }
                RenderFinalModel(builder);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void Plot_Polar()
        {
            try
            {
                var context = GetContext();
                var expression = context.CompileGeneric<double>(EquationBox.Text);
                double rMax = 5, rStep = 0.2, tStep = Math.PI / 20;
                var builder = new HelixToolkit.Geometry.MeshBuilder();

                for (double r = 0; r < rMax; r += rStep)
                {
                    for (double t = 0; t < 2 * Math.PI; t += tStep)
                    {
                        context.Variables["r"] = r; context.Variables["theta"] = t;
                        double z1 = expression.Evaluate();
                        context.Variables["r"] = r + rStep; context.Variables["theta"] = t;
                        double z2 = expression.Evaluate();
                        context.Variables["r"] = r + rStep; context.Variables["theta"] = t + tStep;
                        double z3 = expression.Evaluate();
                        context.Variables["r"] = r; context.Variables["theta"] = t + tStep;
                        double z4 = expression.Evaluate();

                        if (IsInvalid(z1, z2, z3, z4)) continue;
                        builder.AddQuad(
                            new Vector3((float)(r * Math.Cos(t)), (float)(r * Math.Sin(t)), (float)z1),
                            new Vector3((float)((r + rStep) * Math.Cos(t)), (float)((r + rStep) * Math.Sin(t)), (float)z2),
                            new Vector3((float)((r + rStep) * Math.Cos(t + tStep)), (float)((r + rStep) * Math.Sin(t + tStep)), (float)z3),
                            new Vector3((float)(r * Math.Cos(t + tStep)), (float)(r * Math.Sin(t + tStep)), (float)z4));
                    }
                }
                RenderFinalModel(builder);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void Plot_Parametric()
        {
            try
            {
                string[] parts = EquationBox.Text.Split(',');
                var context = GetContext();
                var exX = context.CompileGeneric<double>(parts[0]);
                var exY = context.CompileGeneric<double>(parts[1]);
                var exZ = context.CompileGeneric<double>(parts[2]);

                var points = new Point3DCollection();
                for (double tVal = -10; tVal <= 10; tVal += 0.05)
                {
                    context.Variables["t"] = tVal; // Note: This uses t as the parameter
                    points.Add(new Point3D(exX.Evaluate(), exY.Evaluate(), exZ.Evaluate()));
                }
                View.Children.Add(new LinesVisual3D { Points = points, Color = GetRandomBrightColor(), Thickness = 1 });
            }
            catch { MessageBox.Show("Ensure format: x(t), y(t), z(t)"); }
        }

        private void Plot_Point()
        {
            try
            {
                var vals = EquationBox.Text.Split(',').Select(double.Parse).ToArray();
                View.Children.Add(new SphereVisual3D { Center = new Point3D(vals[0], vals[1], vals[2]), Radius = 0.05, Fill = GetVibrantBrush() });
            }
            catch { MessageBox.Show("Use format: x, y, z"); }
        }

        private void Plot_Vector()
        {
            try
            {
                var vals = EquationBox.Text.Split(',').Select(double.Parse).ToArray();
                View.Children.Add(new ArrowVisual3D { Point2 = new Point3D(vals[0], vals[1], vals[2]), Diameter = 0.03, Fill = GetVibrantBrush() });
            }
            catch { MessageBox.Show("Use format: x, y, z"); }
        }

        private void Plot_Plane()
        {
            try
            {
                string[] parts = EquationBox.Text.Split(',');
                var context = GetContext();
                var exX = context.CompileGeneric<double>(parts[0]);
                var exY = context.CompileGeneric<double>(parts[1]);
                var exZ = context.CompileGeneric<double>(parts[2]);

                double uMin = double.Parse(UMinBox.Text), uMax = double.Parse(UMaxBox.Text);
                double vMin = double.Parse(VMinBox.Text), vMax = double.Parse(VMaxBox.Text);
                double step = double.Parse(StepBox.Text);

                int nU = (int)((uMax - uMin) / step) + 1;
                int nV = (int)((vMax - vMin) / step) + 1;
                Vector3[,] points = new Vector3[nU, nV];

                for (int i = 0; i < nU; i++)
                {
                    context.Variables["u"] = uMin + i * step;
                    for (int j = 0; j < nV; j++)
                    {
                        context.Variables["v"] = vMin + j * step;
                        points[i, j] = new Vector3((float)exX.Evaluate(), (float)exY.Evaluate(), (float)exZ.Evaluate());
                    }
                }

                var builder = new HelixToolkit.Geometry.MeshBuilder();
                for (int i = 0; i < nU - 1; i++)
                    for (int j = 0; j < nV - 1; j++)
                        builder.AddQuad(points[i, j], points[i + 1, j], points[i + 1, j + 1], points[i, j + 1]);

                RenderFinalModel(builder);
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        // ==========================================
        // EVENTS & ANIMATION
        // ==========================================
        private void Plot_Click(object sender, RoutedEventArgs e)
        {
            switch (CurrentPlotType)
            {
                case PlotType.Function: Plot_Function(); break;
                case PlotType.Parametric: Plot_Parametric(); break;
                case PlotType.Polar: Plot_Polar(); break;
                case PlotType.Point: Plot_Point(); break;
                case PlotType.Vector: Plot_Vector(); break;
                case PlotType.Plane: Plot_Plane(); break;
            }
        }

        private void OnRendering(object sender, EventArgs e)
        {
            _time += 0.1;
            View.Children.Clear();
            SetupScene();
            Plot_Click(null, null);
        }

        private void AnimateCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (AnimateCheck.IsChecked == true) CompositionTarget.Rendering += OnRendering;
            else CompositionTarget.Rendering -= OnRendering;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e) { View.Children.Clear(); SetupScene(); }

        private void rbFunction_Click(object sender, RoutedEventArgs e) { CurrentPlotType = PlotType.Function; EquationBox.Text = "Sin(Sqrt(x*x + y*y) - t)"; }
        private void rbPolar_Click(object sender, RoutedEventArgs e) { CurrentPlotType = PlotType.Polar; EquationBox.Text = "Sin(5*theta + t)"; }
        private void rbParametric_Click(object sender, RoutedEventArgs e) { CurrentPlotType = PlotType.Parametric; EquationBox.Text = "Sin(t), Cos(t), t/10"; }
        private void Point_Click(object sender, RoutedEventArgs e) { CurrentPlotType = PlotType.Point; EquationBox.Text = "1, 2, 1"; }
        private void rbVector_Click(object sender, RoutedEventArgs e) { CurrentPlotType = PlotType.Vector; EquationBox.Text = "-2, 2, 3"; }
        private void rbPlane_Click(object sender, RoutedEventArgs e) { CurrentPlotType = PlotType.Plane; EquationBox.Text = "u, v, Sin(u+t)*Cos(v+t)"; }

        private bool IsInvalid(params double[] values) => values.Any(v => double.IsNaN(v) || double.IsInfinity(v));
    }
}