namespace CAMel.Types
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    using GH_IO.Serialization;
    using GH_IO.Types;

    using Grasshopper;
    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;
    using Grasshopper.Kernel.Utility;

    using JetBrains.Annotations;

    using Rhino.Commands;
    using Rhino.Geometry;
    using Rhino.Input;
    using Rhino.Input.Custom;

    using GH_Plane = GH_IO.Types.GH_Plane;

    // One position of the machine
    /// <summary>TODO The tool point.</summary>
    public class ToolPoint : IToolPointContainer, IEquatable<ToolPoint>
    {
        /// <summary>The Tool position and Orientation</summary>
        private Plane tDir1;

        /// <summary>Gets or sets the Tool position and Orientation</summary>
        public Plane tDir { get => this.tDir1; set => this.tDir1 = value; }

        /// <summary>Gets or sets the Material position and Orientation</summary>
        public Plane mDir { get; set; } // Material position and Orientation

        /// <summary>Gets or sets the pt.</summary>
        public Point3d pt // Tool Tip position
        {
            get => this.tDir.Origin;
            set => this.tDir1.Origin = value;
        }
        
        public int meshface { get; set; } // the face on a mesh this point corresponds to (not fully implemented)

        /// <summary>Gets or sets the dir.</summary>
        public Vector3d dir // Tool Direction (away from position)
        {
            get => this.tDir.ZAxis;
            set => this.tDir = new Plane(this.pt, value);
        }

        /// <summary>Does the toolpoint know the surface normal?</summary>
        public bool normalSet { get => this.norm != Vector3d.Zero; }

        /// <summary>If applicable, the normal to the surface currently being cut.</summary>
        [NotNull]
        public Vector3d norm { get; set; }

        /// <inheritdoc />
        [NotNull]
        public ToolPoint firstP => this;
        /// <inheritdoc />
        [NotNull]
        public ToolPoint lastP => this;

        /// <summary>Gets or sets the speed for the toolpoint. Considered unset for negative values.</summary>
        public double speed { get; set; }
        /// <summary>Gets or sets the feed for the toolpoint. Considered unset for negative values</summary>
        public double feed { get; set; }
        /// <summary>Set if the toolpoint has been lifted off surface so does not need to be further offset. </summary>
        public bool lifted { get; set; }
        /// <summary>Gets the list of errors.</summary>
        [NotNull]
        private List<string> error { get; }
        /// <summary>Gets the list of warnings.</summary>
        [NotNull]
        private List<string> warning { get; }
        /// <inheritdoc />
        public string name { get; set; }
        /// <inheritdoc />
        public string preCode { get; set; }
        /// <inheritdoc />
        public string postCode { get; set; }

        // Adding anything here needs significant support:
        //  Add serialization and deserialization (reader and writer below)
        //  Add to the proxy editor
        //  Add to Constructors

        // Default Constructor, set up at the origin with direction set to 0 vector.
        /// <summary>Initializes a new instance of the <see cref="ToolPoint"/> class.</summary>
        public ToolPoint()
        {
            this.tDir = Plane.WorldXY;
            this.mDir = Plane.WorldXY;
            this.norm = Vector3d.Zero;
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
            this.meshface = -1;
            this.lifted = false;
        }

        // Just a point, set direction to Z vector.
        /// <summary>Initializes a new instance of the <see cref="ToolPoint"/> class.</summary>
        /// <param name="pt">TODO The pt.</param>
        public ToolPoint(Point3d pt)
        {
            this.tDir = Plane.WorldXY;
            this.pt = pt;
            this.mDir = Plane.WorldXY;
            this.norm = Vector3d.Zero;
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
            this.meshface = -1;
            this.lifted = false;
        }

        // Use point and direction
        /// <summary>Initializes a new instance of the <see cref="ToolPoint"/> class.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <param name="d">TODO The d.</param>
        public ToolPoint(Point3d pt, Vector3d d)
        {
            this.tDir = new Plane(pt, d);
            this.mDir = Plane.WorldXY;
            this.norm = Vector3d.Zero;
            this.speed = -1;
            this.feed = -1;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
            this.meshface = -1;
            this.lifted = false;
        }

        // Use point direction and override speed and feed
        /// <summary>Initializes a new instance of the <see cref="ToolPoint"/> class.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <param name="d">TODO The d.</param>
        /// <param name="speed">TODO The speed.</param>
        /// <param name="feed">TODO The feed.</param>
        public ToolPoint(Point3d pt, Vector3d d, double speed, double feed)
        {
            this.tDir = new Plane(pt, d);
            this.mDir = Plane.WorldXY;
            this.norm = Vector3d.Zero;
            this.speed = speed;
            this.feed = feed;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
            this.meshface = -1;
            this.lifted = false;
        }
        /// <summary>Initializes a new instance of the <see cref="ToolPoint"/> class.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <param name="d">TODO The d.</param>
        /// <param name="mDir">TODO</param>
        /// <param name="speed">TODO The speed.</param>
        /// <param name="feed">TODO The feed.</param>
        public ToolPoint(Point3d pt, Vector3d d, Plane mDir, double speed, double feed)
        {
            this.tDir = new Plane(pt, d);
            this.mDir = mDir;
            this.norm = Vector3d.Zero;
            this.speed = speed;
            this.feed = feed;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
            this.meshface = -1;
            this.lifted = false;
        }

        /// <summary>Transform the toolpoint in place, WARNING: for non rigid transforms will lose information beyond tool position and direction.</summary>
        /// <param name="transform">Transform to apply</param>
        public void transform(Transform transform) 
        {
            if(transform.IsRigid(CAMel_Goo.Tolerance) == TransformRigidType.NotRigid) // Just transform position and direction
            {
                Point3d nO = this.pt;
                Vector3d nZ = this.dir;
                nO.Transform(transform);
                nZ.Transform(transform);
                if (nZ.Length < CAMel_Goo.Tolerance) { nZ = this.dir; } // if direction projects to 0, leave it fixed
                this.tDir = new Plane(nO, nZ);

                nO = this.mDir.Origin;
                nZ = this.mDir.ZAxis;
                nO.Transform(transform);
                nZ.Transform(transform);
                if (nZ.Length < CAMel_Goo.Tolerance) { nZ = this.dir; } // if direction projects to 0, leave it fixed
                this.mDir = new Plane(nO, nZ);
            }
            else
            {
                Plane ntDir = this.tDir.Clone();
                Plane nmDir = this.mDir.Clone();

                ntDir.Transform(transform);
                nmDir.Transform(transform);

                this.tDir = ntDir;
                this.mDir = nmDir;
            }
        }

        /// <summary>Initializes a new instance of the <see cref="ToolPoint"/> class.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <param name="mD">TODO The m d.</param>
        /// <param name="speed">TODO The speed.</param>
        /// <param name="feed">TODO The feed.</param>
        public ToolPoint(Plane pt, Plane mD, double speed, double feed)
        {
            this.tDir = pt;
            this.mDir = mD;
            this.norm = Vector3d.Zero;
            this.speed = speed;
            this.feed = feed;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
            this.meshface = -1;
            this.lifted = false;
        }

        // Use point direction, override speed and feed and add extra Code
        /// <summary>Initializes a new instance of the <see cref="ToolPoint"/> class.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <param name="mD">TODO The d.</param>
        /// <param name="preCode">TODO The pre code.</param>
        /// <param name="postCode">TODO The post code.</param>
        /// <param name="speed">TODO The speed.</param>
        /// <param name="feed">TODO The feed.</param>
        public ToolPoint(Plane pt, Plane mD, [CanBeNull] string preCode, [CanBeNull] string postCode, double speed, double feed)
        {
            this.tDir = pt;
            this.mDir = mD;
            this.norm = Vector3d.Zero;
            this.preCode = preCode ?? string.Empty;
            this.postCode = postCode ?? string.Empty;
            this.speed = speed;
            this.feed = feed;
            this.error = new List<string>();
            this.warning = new List<string>();
            this.name = string.Empty;
            this.meshface = -1;
            this.lifted = false;
        }
        // Copy Constructor
        /// <summary>Initializes a new instance of the <see cref="ToolPoint"/> class.</summary>
        /// <param name="tP">TODO The t p.</param>
        private ToolPoint([NotNull] ToolPoint tP)
        {
            this.tDir = tP.tDir;
            this.mDir = tP.mDir;
            this.norm = tP.norm;
            this.preCode = string.Copy(tP.preCode);
            this.postCode = string.Copy(tP.postCode);
            this.speed = tP.speed;
            this.feed = tP.feed;
            this.name = string.Copy(tP.name);
            this.error = new List<string>();
            foreach (string s in tP.error) { this.error.Add(string.Copy(s)); }
            this.warning = new List<string>();
            foreach (string s in tP.warning) { this.warning.Add(string.Copy(s)); }
            this.meshface = tP.meshface;
            this.lifted = tP.lifted;
        }

        /// <summary>Deep Clone the ToolPoint</summary>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
        [NotNull]
        public ToolPoint deepClone() => new ToolPoint(this);

        /// <summary>Add an error to the toolpoint. </summary>
        /// <param name="err">Error to add.</param>
        public void addError([CanBeNull] string err)
        {
            if (err != null) { this.error.Add(err); }
        }

        /// <summary>Add a warning to the toolpoint. </summary>
        /// <param name="warn">Warning to add.</param>
        public void addWarning([CanBeNull] string warn)
        {
            if (warn != null) { this.warning.Add(warn); }
        }

        /// <summary>TODO The write error and warnings.</summary>
        /// <param name="co">TODO The co.</param>
        public void writeErrorAndWarnings([NotNull] ref CodeInfo co)
        {
            foreach (string err in this.error) { co.addError(err); }
            foreach (string warn in this.warning) { co.addWarning(warn); }
        }

        /// <inheritdoc />
        public string TypeDescription => "Information about a position of the machine";
        /// <inheritdoc />
        public string TypeName => "ToolPoint";

        /// <summary>To String.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString()
        {
            string outP = this.name;
            if (outP != string.Empty) { outP += " "; }
            outP = outP + "Pt: (" +
                   this.pt.X.ToString("0.000") + ", " + this.pt.Y.ToString("0.000") + ", " +
                   this.pt.Z.ToString("0.000") +
                   ") Dir: (" +
                   this.dir.X.ToString("0.000") + ", " + this.dir.Y.ToString("0.000") + ", " +
                   this.dir.Z.ToString("0.000") +
                   ")";
            return outP;
        }

        public void setNorm(Mesh m)
        {
            // TODO check for failure on not having or bad number for meshface
            MeshFace mF = m.Faces[this.meshface];
            Vector3d bary = barycentric(
                this.pt,
                m.Vertices[mF.A], m.Vertices[mF.B], m.Vertices[mF.C]);
            this.norm = m.NormalAt(this.meshface, bary.Z, bary.X, bary.Y, 0.0);
        }

        // Transcribed from Christer Ericson's Real-Time Collision Detection
        // Compute barycentric coordinates (u, v, w) for
        // point p with respect to triangle (a, b, c)
        /// <summary>TODO The barycentric.</summary>
        /// <param name="p">TODO The p.</param>
        /// <param name="a">TODO The a.</param>
        /// <param name="b">TODO The b.</param>
        /// <param name="c">TODO The c.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        private static Vector3d barycentric(Point3d p, Point3d a, Point3d b, Point3d c)
        {
            Vector3d v0 = b - a, v1 = c - a, v2 = p - a;
            double d00 = v0 * v0;
            double d01 = v0 * v1;
            double d11 = v1 * v1;
            double d20 = v2 * v0;
            double d21 = v2 * v1;
            double denom = d00 * d11 - d01 * d01;
            if (Math.Abs(denom) < CAMel_Goo.Tolerance) { return new Vector3d(1, 0, 0); }
            double u = (d11 * d20 - d01 * d21) / denom;
            double v = (d00 * d21 - d01 * d20) / denom;

            return new Vector3d(u, v, 1.0 - u - v);
        }

        /// <summary>Length of lines for toolpoint preview. </summary>
        private const double PreviewLength = .5;
        /// <summary>A line to represent the toolpoint position and direction. </summary>
        /// <returns>The <see cref="Line"/>.</returns>
        internal Line toolLine() => new Line(this.pt, this.pt + this.dir * PreviewLength);
        /// <inheritdoc />
        public ToolPath getSinglePath() => new ToolPath { this };

        /// <inheritdoc />
        public BoundingBox getBoundingBox() => new BoundingBox(new List<Point3d> { this.pt, this.pt + this.dir * PreviewLength });

        bool IEquatable<ToolPoint>.Equals(ToolPoint other) => this.tDir == other.tDir && this.mDir == other.mDir;
        public override int GetHashCode() => this.tDir.GetHashCode() ^ this.mDir.GetHashCode();

    }

    // Grasshopper Type Wrapper
    /// <summary>TODO The g h_ tool point.</summary>
    public sealed class GH_ToolPoint : CAMel_Goo<ToolPoint>, IGH_PreviewData
    {
        // Default Constructor, set up at the origin with direction set to 0 vector.
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPoint"/> class.</summary>
        [UsedImplicitly]
        public GH_ToolPoint() => this.Value = new ToolPoint();

        // Create from unwrapped version
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPoint"/> class.</summary>
        /// <param name="tP">TODO The t p.</param>
        public GH_ToolPoint([CanBeNull] ToolPoint tP) => this.Value = tP?.deepClone();

        // Copy Constructor
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPoint"/> class.</summary>
        /// <param name="tP">TODO The t p.</param>
        public GH_ToolPoint([CanBeNull] GH_ToolPoint tP) => this.Value = tP?.Value?.deepClone();

        /// <inheritdoc />
        [NotNull]
        public override IGH_Goo Duplicate() => new GH_ToolPoint(this);

        /// <inheritdoc />
        [NotNull]
        public override IGH_GooProxy EmitProxy() => new GH_ToolPointProxy(this);

        /// <inheritdoc />
        public override bool Write([CanBeNull] GH_IWriter writer)
        {
            if (this.Value == null || writer == null) { return base.Write(writer); }

            writer.SetString("name", this.Value.name);
            writer.SetPlane("tDir", CAMel_Goo.toIO(this.Value.tDir));
            writer.SetPlane("mDir", CAMel_Goo.toIO(this.Value.mDir));
            writer.SetPoint3D("norm", CAMel_Goo.toIO(this.Value.norm));
            writer.SetDouble("speed", this.Value.speed);
            writer.SetDouble("feed", this.Value.feed);
            writer.SetString("preCode", this.Value.preCode);
            writer.SetString("postCode", this.Value.postCode);
            writer.SetBoolean("lifted", this.Value.lifted);

            return base.Write(writer);
        }

        /// <inheritdoc />
        public override bool Read([CanBeNull] GH_IReader reader)
        {
            if (reader == null) { return false; }
            try
            {
                ToolPoint tPt = new ToolPoint();
                if (reader.ItemExists("name")) { tPt.name = reader.GetString("name") ?? string.Empty; }
                if (reader.ItemExists("pt"))
                {
                    GH_Point3D pt = reader.GetPoint3D("pt");
                    tPt.pt = CAMel_Goo.fromIO(pt);
                }

                if (reader.ItemExists("dir"))
                {
                    GH_Point3D pt = reader.GetPoint3D("dir");
                    tPt.tDir = new Plane(tPt.pt, (Vector3d)CAMel_Goo.fromIO(pt));
                }

                if (reader.ItemExists("tDir"))
                {
                    GH_Plane pl = reader.GetPlane("tDir");
                    tPt.tDir = CAMel_Goo.fromIO(pl);
                }

                if (reader.ItemExists("mDir"))
                {
                    GH_Plane pl = reader.GetPlane("mDir");
                    tPt.mDir = CAMel_Goo.fromIO(pl);
                }

                if (reader.ItemExists("norm"))
                {
                    GH_Point3D v = reader.GetPoint3D("norm");
                    tPt.norm = (Vector3d)CAMel_Goo.fromIO(v);
                }

                if (reader.ItemExists("feed")) { tPt.feed = reader.GetDouble("feed"); }
                if (reader.ItemExists("speed")) { tPt.speed = reader.GetDouble("speed"); }
                if (reader.ItemExists("preCode")) { tPt.preCode = reader.GetString("preCode") ?? string.Empty; }
                if (reader.ItemExists("postCode")) { tPt.postCode = reader.GetString("postCode") ?? string.Empty; }
                if (reader.ItemExists("lifted")) { tPt.lifted = reader.GetBoolean("lifted"); }

                this.Value = tPt;
                return base.Read(reader);
            }
            catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException || ex is NullReferenceException)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override bool CastTo<T>(ref T target)
        {
            if (this.Value == null) { return false; }
            if (typeof(T).IsAssignableFrom(typeof(ToolPoint)))
            {
                object ptr = this.Value;
                target = (T)ptr;
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(Point3d)))
            {
                object ptr = this.Value.pt;
                target = (T)ptr;
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(GH_Point)))
            {
                object ptr = new GH_Point(this.Value.pt);
                target = (T)ptr;
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(Vector3d)))
            {
                object ptr = this.Value.dir;
                target = (T)ptr;
                return true;
            }
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(GH_Vector)))
            {
                object ptr = new GH_Vector(this.Value.dir);
                target = (T)ptr;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;
                case GH_Point pointGoo:
                    this.Value = new ToolPoint(pointGoo.Value);
                    return true;
            }

            Point3d pt = Point3d.Unset;
            if (!GH_Convert.ToPoint3d(source, ref pt, GH_Conversion.Both)) { return false; }
            this.Value = new ToolPoint(pt);
            return true;
        }

        /// <inheritdoc />
        public BoundingBox ClippingBox => this.Value?.toolLine().BoundingBox ?? BoundingBox.Unset;

        /// <inheritdoc />
        public void DrawViewportWires([CanBeNull] GH_PreviewWireArgs args)
        {
            if (this.Value == null || args?.Pipeline == null) { return; }
            args.Pipeline.DrawPoint(this.Value.pt, args.Color);
            args.Pipeline.DrawArrow(this.Value.toolLine(), args.Color);
        }

        /// <inheritdoc />
        public void DrawViewportMeshes([CanBeNull] GH_PreviewMeshArgs args) { }
    }

    /// <summary>Grasshopper Parameter Wrapper for a ToolPoint. </summary>
    public class GH_ToolPointPar : GH_PersistentParam<GH_ToolPoint>, IGH_PreviewObject
    {
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPointPar"/> class.</summary>
        public GH_ToolPointPar()
            : base(
                "ToolPoint", "ToolPt",
                "Contains a collection of Tool Points",
                "CAMel", "  Params") { }
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("0bbed7c1-88a9-4d61-b7cb-e0dfe82b1b86");

        /// <inheritdoc />
        public bool Hidden { get; set; }
        /// <inheritdoc />
        public bool IsPreviewCapable => true;
        /// <inheritdoc />
        public BoundingBox ClippingBox => this.Preview_ComputeClippingBox();
        /// <inheritdoc />
        public void DrawViewportWires([CanBeNull] IGH_PreviewArgs args) => this.Preview_DrawWires(args);

        /// <inheritdoc />
        public void DrawViewportMeshes([CanBeNull] IGH_PreviewArgs args) => this.Preview_DrawMeshes(args);

        /// <inheritdoc />
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.toolpoint;

        /// <inheritdoc />
        protected override GH_GetterResult Prompt_Singular([CanBeNull] ref GH_ToolPoint value)
        {
            bool notUsed = false;
            ToolPoint tPt = getToolPoint(false, ref notUsed);
            if (tPt == null) { return GH_GetterResult.cancel; }
            value = new GH_ToolPoint(tPt);
            return GH_GetterResult.success;
        }

        /// <inheritdoc />
        protected override GH_GetterResult Prompt_Plural([NotNull] ref List<GH_ToolPoint> values)
        {
            if (values == null) { values = new List<GH_ToolPoint>(); }
            bool useForRemainder = false;
            Vector3d dir = Vector3d.ZAxis;

            using (GH_PreviewUtil preView = new GH_PreviewUtil(true))
            {
                foreach (GH_ToolPoint gHtPt in values)
                {
                    preView.AddPoint(gHtPt.Value?.pt ?? Point3d.Unset);
                    preView.AddVector(gHtPt.Value?.toolLine() ?? Line.Unset);
                }

                while (true)
                {
                    ToolPoint tPt;
                    preView.Redraw();
                    if (useForRemainder)
                    {
                        if (RhinoGet.GetPoint("Tooltip Position", true, out Point3d point) != Result.Success)
                        {
                            return GH_GetterResult.success;
                        }

                        tPt = new ToolPoint(point, dir);
                        values.Add(new GH_ToolPoint(tPt));
                        preView.AddPoint(tPt.pt);
                        preView.AddVector(tPt.toolLine());
                        continue;
                    }

                    tPt = getToolPoint(true, ref useForRemainder);
                    if (tPt == null) { return GH_GetterResult.success; }
                    preView.AddPoint(tPt.pt);
                    preView.AddVector(tPt.toolLine());
                    dir = tPt.dir;
                    values.Add(new GH_ToolPoint(tPt));
                }
            }
        }

        /// <summary>Create a toolPoint in Rhino. </summary>
        /// <param name="multiple">Create multiple points/ </param>
        /// <param name="useForRemainder">TODO The use for remainder.</param>
        /// <returns>The <see cref="ToolPoint"/>.</returns>
        [CanBeNull]
        private static ToolPoint getToolPoint(bool multiple, ref bool useForRemainder)
        {
            if (RhinoGet.GetPoint("Tooltip Position", true, out Point3d point) != Result.Success) { return null; }

            GetToolDir gTd = new GetToolDir(point, multiple);
            Vector3d dir = Vector3d.ZAxis;
            useForRemainder = false;
            while (true)
            {
                switch (gTd.Get())
                {
                    case GetResult.Option:
                        string name = gTd.Option()?.EnglishName;
                        if (name == "X") { dir = Vector3d.XAxis; }
                        else if (name == "Y") { dir = Vector3d.YAxis; }
                        else if (name == "Z") { dir = Vector3d.ZAxis; }
                        else if (name == "-X") { dir = -Vector3d.XAxis; }
                        else if (name == "-Y") { dir = -Vector3d.YAxis; }
                        else if (name == "-Z") { dir = -Vector3d.ZAxis; }
                        else if (name == "Use") { continue; }
                        break;
                    case GetResult.Point:
                        dir = gTd.Point() - point;
                        break;
                    default:
                        useForRemainder = gTd.useForRemainder;
                        return null;
                }

                useForRemainder = gTd.useForRemainder;
                return new ToolPoint(point, dir);
            }
        }

        /// <summary>TODO The get tool dir.</summary>
        private class GetToolDir : GetPoint
        {
            /// <summary>TODO The start.</summary>
            private readonly Point3d start;
            /// <summary>TODO The dir.</summary>
            private Line dir;
            /// <summary>TODO The use toggle.</summary>
            private readonly OptionToggle useToggle;

            /// <summary>TODO The use for remainder.</summary>
            public bool useForRemainder => this.useToggle?.CurrentValue ?? false;

            /// <summary>Initializes a new instance of the <see cref="T:CAMel.Types.GH_ToolPointPar.GetToolDir" /> class.</summary>
            /// <param name="startP">TODO The start p.</param>
            /// <param name="multiple">TODO The multiple.</param>
            public GetToolDir(Point3d startP, bool multiple)
            {
                this.SetCommandPrompt("Tool Direction");
                this.AcceptNothing(true);
                this.start = !startP.IsValid ? Point3d.Origin : startP;
                this.SetBasePoint(startP, true);
                if (multiple)
                {
                    this.useToggle = new OptionToggle(false, "Once", "ForRemainder");
                    this.AddOptionToggle("Use", ref this.useToggle);
                }

                this.AddOption("X");
                this.AddOption("Y");
                this.AddOption("Z");
                this.AddOption("-X");
                this.AddOption("-Y");
                this.AddOption("-Z");
                this.MouseMove += this.moveMouse;
                this.DynamicDraw += this.draw;
            }

            /// <summary>TODO The move mouse.</summary>
            /// <param name="sender">TODO The sender.</param>
            /// <param name="e">TODO The e.</param>
            private void moveMouse([CanBeNull] object sender, [CanBeNull] GetPointMouseEventArgs e)
            {
                if (e != null) { this.dir = new Line(this.start, e.Point); }
            }

            /// <summary>TODO The draw.</summary>
            /// <param name="sender">TODO The sender.</param>
            /// <param name="e">TODO The e.</param>
            private void draw([CanBeNull] object sender, [CanBeNull] GetPointDrawEventArgs e)
            {
                e?.Display?.DrawPoint(this.start, CentralSettings.PreviewPointStyle, 3, System.Drawing.Color.Black);
                if (this.dir.IsValid) { e?.Display?.DrawArrow(this.dir, System.Drawing.Color.Black); }
            }
        }
    }

    /// <summary>TODO The g h_ tool point proxy.</summary>
    public class GH_ToolPointProxy : GH_GooProxy<GH_ToolPoint>
    {
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPointProxy"/> class.</summary>
        /// <param name="obj">TODO The obj.</param>
        public GH_ToolPointProxy([CanBeNull] GH_ToolPoint obj)
            : base(obj) { }

        /// <summary>Gets or sets the name.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [CanBeNull, Category(" General"), Description("Optional Name attached to point."), DisplayName(" Name"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public string name
        {
            get => this.Owner?.Value?.name ?? string.Empty;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                this.Owner.Value.name = value ?? string.Empty;
            }
        }

        /// <summary>Gets the pt.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [CanBeNull, Category(" General"), Description("Position of tool tip."), DisplayName(" Point"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public GH_Point3d_Wrapper pt
        {
            get
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                Point3d rPt = this.Owner.Value.pt;
                GH_Point3d_Wrapper ghPoint3DWrapper = new GH_Point3d_Wrapper(ref rPt, this.pointChanged);
                if (this.Owner == null) { throw new NullReferenceException(); }
                this.Owner.Value.pt = rPt;
                return ghPoint3DWrapper;
            }
        }

        /// <summary>TODO The point changed.</summary>
        /// <param name="sender">TODO The sender.</param>
        /// <param name="point">TODO The point.</param>
        /// <exception cref="NullReferenceException"></exception>
        private void pointChanged([CanBeNull] GH_Point3d_Wrapper sender, Point3d point)
        {
            if (this.Owner == null) { throw new NullReferenceException(); }
            if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
            this.Owner.Value.pt = point;
        }

        /// <summary>Gets the dir.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [CanBeNull, Category(" General"), Description("Direction of tool (for rotary and 5-axis) (from tip down shaft)."), DisplayName("Direction"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public GH_Vector3d_Wrapper dir
        {
            get
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                Vector3d rDir = this.Owner.Value.dir;
                GH_Vector3d_Wrapper ghVector3DWrapper = new GH_Vector3d_Wrapper(ref rDir, this.dirChanged);
                if (this.Owner == null) { throw new NullReferenceException(); }
                this.Owner.Value.dir = rDir;
                return ghVector3DWrapper;
            }
        }

        /// <summary>TODO The dir changed.</summary>
        /// <param name="sender">TODO The sender.</param>
        /// <param name="rDir">TODO The r dir.</param>
        /// <exception cref="NullReferenceException"></exception>
        private void dirChanged([CanBeNull] GH_Vector3d_Wrapper sender, Vector3d rDir)
        {
            if (this.Owner == null) { throw new NullReferenceException(); }
            if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
            this.Owner.Value.dir = rDir;
        }



        /// <summary>Gets or sets the speed.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [Category(" Settings"), Description("Spindle rotation Speed (if machine needs it)."), DisplayName("Speed"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public double speed
        {
            get => this.Owner?.Value?.speed ?? -1;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                this.Owner.Value.speed = value;
            }
        }

        /// <summary>Gets or sets the feed.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [Category(" Settings"), Description("Feed Rate (if machine needs it)."), DisplayName("Feed"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public double feed
        {
            get => this.Owner?.Value?.feed ?? -1;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                this.Owner.Value.feed = value;
            }
        }

        /// <summary>Gets or sets the pre code.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [CanBeNull, Category("Code"), Description("Extra Code to run before point"), DisplayName("preCode"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public string preCode
        {
            get => this.Owner?.Value?.preCode ?? string.Empty;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                this.Owner.Value.preCode = value ?? string.Empty;
            }
        }

        /// <summary>Gets or sets the post code.</summary>
        /// <exception cref="NullReferenceException"></exception>
        [CanBeNull, Category("Code"), Description("Extra Code to run after point"), DisplayName("postCode"), RefreshProperties(RefreshProperties.All), UsedImplicitly]
        public string postCode
        {
            get => this.Owner?.Value?.postCode ?? string.Empty;
            set
            {
                if (this.Owner == null) { throw new NullReferenceException(); }
                if (this.Owner.Value == null) { this.Owner.Value = new ToolPoint(); }
                this.Owner.Value.postCode = value ?? string.Empty;
            }
        }
    }
}