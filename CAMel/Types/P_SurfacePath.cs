namespace CAMel.Types
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using CAMel.Types.MaterialForm;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino.Geometry;
    using Rhino.Geometry.Intersect;

    // The type of projection.
    // Parallel is along a single direction
    // Cylindrical points towards a path along a direction
    // Spherical points towards a point
    /// <summary>TODO The surf proj.</summary>
    public enum SurfProj
    {
        /// <summary>TODO The parallel.</summary>
        Parallel,
        /// <summary>TODO The cylindrical.</summary>
        Cylindrical,
        /// <summary>TODO The spherical.</summary>
        Spherical
    }

    // The tool direction for surfacing.
    // Projection is along the projection direction
    // Path Tangent and Path Normal mix the projection and
    // surface normals.
    // Path Tangent gives the normal to the path in the plane given by the path tangent and projection direction
    // Path Normal gives the normal to the path in the plane given by the surface tangent normal to the path and projection direction
    // Normal is surface normal
    /// <summary>TODO The surf tool dir.</summary>
    public enum SurfToolDir
    {
        /// <summary>TODO The projection.</summary>
        Projection,
        /// <summary>TODO The path tangent.</summary>
        PathTangent,
        /// <summary>TODO The path normal.</summary>
        PathNormal,
        /// <summary>TODO The normal.</summary>
        Normal,
        /// <summary>TODO The error.</summary>
        Error
    }

    // A path that will project to a surface for surfacing
    // TODO write a subclass for each projection
    /// <summary>TODO The surface path.</summary>
    public class SurfacePath : IList<Curve>, ICAMelBase
    {
        /// <summary>TODO The paths.</summary>
        [NotNull] private readonly List<Curve> paths; // Curves to project
        /// <summary>Gets the surf proj.</summary>
        private SurfProj surfProj { get; } // Type of projection
        /// <summary>Gets the cyl onto.</summary>
        private Curve cylOnto { get; } // centre line for Cylindrical projection
        /// <summary>Gets the dir.</summary>
        private Vector3d dir { get; } // direction for parallel projection, or line direction for cylindrical
        /// <summary>Gets the cen.</summary>
        private Point3d cen { get; } // centre for spherical projection
        /// <summary>Gets the surf tool dir.</summary>
        private SurfToolDir surfToolDir { get; } // method to calculate tool direction
        /// <summary>Gets the m t.</summary>
        [NotNull]
        public MaterialTool mT { get; }

        // private storage when processing a model
        /// <summary>TODO The m.</summary>
        private Mesh m; // Mesh

        // Parallel constructor
        /// <summary>Initializes a new instance of the <see cref="SurfacePath"/> class.</summary>
        /// <param name="paths">TODO The paths.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="dir">TODO The dir.</param>
        /// <param name="sTd">TODO The s td.</param>
        public SurfacePath([NotNull] List<Curve> paths, [NotNull] MaterialTool mT, Vector3d dir, SurfToolDir sTd)
        {
            this.paths = paths;
            this.mT = mT;
            this.surfProj = SurfProj.Parallel;
            this.dir = dir;
            this.surfToolDir = sTd;
        }

        // Cylindrical constructor
        /// <summary>Initializes a new instance of the <see cref="SurfacePath"/> class.</summary>
        /// <param name="paths">TODO The paths.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="dir">TODO The dir.</param>
        /// <param name="cc">TODO The cc.</param>
        /// <param name="surfToolDir">TODO The surf tool dir.</param>
        public SurfacePath([NotNull] List<Curve> paths, [NotNull] MaterialTool mT, Vector3d dir, [NotNull] Curve cc, SurfToolDir surfToolDir)
        {
            this.paths = paths;
            this.mT = mT;
            this.surfProj = SurfProj.Cylindrical;
            this.dir = dir;
            this.cylOnto = cc;
            this.surfToolDir = surfToolDir;
        }

        // Spherical constructor
        /// <summary>Initializes a new instance of the <see cref="SurfacePath"/> class.</summary>
        /// <param name="paths">TODO The paths.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="cen">TODO The cen.</param>
        /// <param name="surfToolDir">TODO The surf tool dir.</param>
        public SurfacePath([NotNull] List<Curve> paths, [NotNull] MaterialTool mT, Point3d cen, SurfToolDir surfToolDir)
        {
            this.paths = paths;
            this.mT = mT;
            this.surfProj = SurfProj.Spherical;
            this.cen = cen;
            this.surfToolDir = surfToolDir;
        }

        /// <summary>TODO The change finish depth.</summary>
        /// <param name="cutDepth">TODO The cut depth.</param>
        /// <returns>The <see cref="SurfacePath"/>.</returns>
        [NotNull]
        public SurfacePath changeFinishDepth(double cutDepth)
        {
            MaterialTool newMT = MaterialTool.changeFinishDepth(this.mT, cutDepth);
            switch (this.surfProj)
            {
                case SurfProj.Parallel:
                    return new SurfacePath(this.paths, newMT, this.dir, this.surfToolDir);
                case SurfProj.Cylindrical:
                    if (this.cylOnto == null) { Exceptions.nullPanic(); }
                    return new SurfacePath(this.paths, newMT, this.dir, this.cylOnto, this.surfToolDir);
                case SurfProj.Spherical:
                    return new SurfacePath(this.paths, newMT, this.cen, this.surfToolDir);
                default:
                    Exceptions.badSurfacePath();
                    return new SurfacePath(this.paths, newMT, this.cen, this.surfToolDir);
            }
        }

        /// <inheritdoc />
        public string TypeDescription => "Path and projection information to generate a surfacing path";
        /// <inheritdoc />
        public string TypeName => "SurfacePath";

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString()
        {
            string op = "Surfacing:";
            switch (this.surfProj)
            {
                case SurfProj.Parallel:
                    op += " Parallel Projection";
                    break;
                case SurfProj.Cylindrical:
                    op += " Cylindrical Projection";
                    break;
                case SurfProj.Spherical:
                    op += " Spherical Projection";
                    break;
            }

            return op;
        }

        // Different calls to Generate a Machine Operation from different surfaces
        /// <summary>TODO The generate operation.</summary>
        /// <param name="b">TODO The b.</param>
        /// <param name="offset">TODO The offset.</param>
        /// <param name="mF">TODO The m f.</param>
        /// <param name="tPa">TODO The t pa.</param>
        /// <returns>The <see cref="MachineOperation"/>.</returns>
        [NotNull]
        public MachineOperation generateOperation([NotNull] Brep b, double offset, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tPa)
        {
            // Just convert to Mesh
            MeshingParameters mP = MeshingParameters.Smooth;
            if (mP == null) { Exceptions.nullPanic(); }
            mP.MaximumEdgeLength = this.mT.toolWidth / 2.0;
            mP.ComputeCurvature = true;
            mP.MinimumEdgeLength = 0.00001;
            this.m = Mesh.CreateFromBrep(b, mP)?[0];
            this.m?.FaceNormals?.ComputeFaceNormals();

            return this.generateOperation_(offset, mF, tPa);
        }

        /// <summary>TODO The generate operation.</summary>
        /// <param name="mIn">TODO The m in.</param>
        /// <param name="offset">TODO The offset.</param>
        /// <param name="mF">TODO The m f.</param>
        /// <param name="tPa">TODO The t pa.</param>
        /// <returns>The <see cref="MachineOperation"/>.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        [NotNull]
        public MachineOperation generateOperation([NotNull] Mesh mIn, double offset, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tPa)
        {
            if (mIn.FaceNormals == null) { throw new ArgumentNullException(); }
            mIn.FaceNormals.ComputeFaceNormals();
            this.m = mIn;

            return this.generateOperation_(offset, mF, tPa);
        }

        // actual code to generate the operation
        /// <summary>TODO The generate operation_.</summary>
        /// <param name="offset">TODO The offset.</param>
        /// <param name="mF">TODO The m f.</param>
        /// <param name="tPa">TODO The t pa.</param>
        /// <returns>The <see cref="MachineOperation"/>.</returns>
        /// <exception cref="NullReferenceException"></exception>
        [NotNull]
        private MachineOperation generateOperation_(double offset, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tPa)
        {
            if (this.m == null) { throw new NullReferenceException("Trying to generate a surfacing path with no mesh set. "); }

            // create unprojected toolpath (mainly to convert the curve into a list of points)
            List<ToolPath> tPs = new List<ToolPath>();

            foreach (Curve p in this.paths)
            {
                tPs.Add(new ToolPath(string.Empty, this.mT, mF, tPa));
                tPs[tPs.Count - 1]?.convertCurve(p, new Vector3d(0, 0, 1), .5);
            }

            // move points onto surface storing projection direction
            // on the toolpoint and keeping lists of norms
            List<ToolPath> newTPs = new List<ToolPath>();
            List<List<Vector3d>> norms = new List<List<Vector3d>>();

            foreach (ToolPath tP in tPs)
            {
                ConcurrentDictionary<ToolPoint, FirstIntersectResponse> intersectInfo = new ConcurrentDictionary<ToolPoint, FirstIntersectResponse>(Environment.ProcessorCount, tP.Count);

                foreach (ToolPoint tPt in tP) // initialise dictionary
                { intersectInfo[tPt] = new FirstIntersectResponse(); }

                Parallel.ForEach(
                    tP, tPtP =>
                        {
                            if (tPtP != null) { intersectInfo[tPtP] = this.firstIntersect(tPtP); }
                        });

                ToolPath tempTP = new ToolPath(string.Empty, this.mT, mF, tPa);
                List<Vector3d> tempN = new List<Vector3d>();

                bool missed = false;

                foreach (FirstIntersectResponse fIr in tP.Select(tPt => intersectInfo[tPt]))
                {
                    if (fIr.hit)
                    {
                        // Check to see if a new path is needed.
                        if (missed && tempTP.lastP != null)
                        {
                            if (tempTP.lastP.pt.DistanceTo(fIr.tP.pt) > this.mT.pathJump)
                            {
                                if (tempTP.Count > 1)
                                {
                                    newTPs.Add(tempTP);
                                    norms.Add(tempN);
                                }

                                tempTP = new ToolPath(string.Empty, this.mT, mF, tPa);
                                tempN = new List<Vector3d>();
                            }
                        }

                        tempTP.Add(fIr.tP);
                        tempN.Add(fIr.norm);
                        missed = false;
                    }
                    else if (tempTP.Count > 0) { missed = true; }
                }

                if (tempTP.Count <= 1) { continue; }
                newTPs.Add(tempTP);
                norms.Add(tempN);
            }

            for (int j = 0; j < newTPs.Count; j++)
            {
                for (int i = 0; i < newTPs[j]?.Count; i++)
                {
                    // find the tangent vector, assume the curve is not too jagged
                    // this is reasonable for most paths, but not for space
                    // filling curve styles. Though for those this is a bad option.
                    // For the moment we will look 2 points back and one point forward.
                    // Some cases to deal with the start, end and short paths.
                    // TODO Smooth this out by taking into account individual tangencies?
                    int lookBack, lookForward;
                    if (i == newTPs[j].Count - 1)
                    {
                        lookBack = 3;
                        if (i < lookBack) { lookBack = newTPs[j].Count - 1; }
                        lookForward = 0;
                    }
                    else
                    {
                        lookBack = Math.Min(i, 2);
                        lookForward = 3 - lookBack;
                        if (lookForward + i >= newTPs[j].Count) { lookForward = newTPs[j].Count - i - 1; }
                    }

                    Vector3d tangent = newTPs[j][i + lookForward].pt - newTPs[j][i - lookBack].pt;
                    switch (this.surfToolDir)
                    {
                        case SurfToolDir.Projection: // already set
                            break;
                        case SurfToolDir.PathNormal:
                            // get normal to tangent on surface
                            if (norms[j]?[i] == null) { break; }
                            Vector3d stNorm = Vector3d.CrossProduct(norms[j][i], tangent);
                            Vector3d pNplaneN = Vector3d.CrossProduct(newTPs[j][i].dir, stNorm);

                            // find vector normal to the surface in the line orthogonal to the tangent
                            newTPs[j][i].dir = Vector3d.CrossProduct(stNorm, pNplaneN);
                            break;
                        case SurfToolDir.PathTangent:
                            // get normal to proj and tangent
                            Vector3d pTplaneN = Vector3d.CrossProduct(tangent, newTPs[j][i].dir);

                            // find vector normal to tangent and in the plane of tangent and projection
                            newTPs[j][i].dir = Vector3d.CrossProduct(pTplaneN, tangent);

                            break;
                        case SurfToolDir.Normal: // set to Norm
                            if (norms[j]?[i] == null) { break; }
                            newTPs[j][i].dir = norms[j][i];
                            break;
                    }
                }
            }

            // Adjust the tool position based on the surface normal and the tool orientation
            // so that the cutting surface not the tooltip is at the correct point
            for (int j = 0; j < newTPs.Count; j++)
            {
                for (int i = 0; i < newTPs[j]?.Count; i++)
                {
                    if (norms[j]?[i] == null) { break; }
                    newTPs[j][i].pt += this.mT.cutOffset(newTPs[j][i].dir, norms[j][i]);

                    // Move to offset using normal
                    newTPs[j][i].pt += offset * norms[j][i];
                }
            }

            // make the machine operation
            MachineOperation mO = new MachineOperation(this.ToString(), newTPs);
            return mO;
        }

        /// <summary>TODO The first intersect response.</summary>
        private struct FirstIntersectResponse
        {
            /// <summary>Gets or sets the t p.</summary>
            public ToolPoint tP { get; set; }
            /// <summary>Gets or sets the norm.</summary>
            public Vector3d norm { get; set; }
            /// <summary>Gets or sets a value indicating whether hit.</summary>
            public bool hit { get; set; }

            /// <summary>TODO The to string.</summary>
            /// <returns>The <see cref="string"/>.</returns>
            public override string ToString() => this.hit.ToString();
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

        /// <summary>TODO The first intersect.</summary>
        /// <param name="tP">TODO The t p.</param>
        /// <returns>The <see cref="FirstIntersectResponse"/>.</returns>
        private FirstIntersectResponse firstIntersect([NotNull] ToolPoint tP)
        {
            FirstIntersectResponse fIr = new FirstIntersectResponse { hit = false };
            if (this.m?.FaceNormals == null) { return fIr; }

            Vector3d proj = this.projDir(tP.pt);
            Ray3d rayL = new Ray3d(tP.pt, proj);

            double inter = Intersection.MeshRay(this.m, rayL, out int[] faces);

            if (!(inter >= 0)) { return fIr; }

            fIr.hit = true;
            fIr.tP = new ToolPoint(rayL.PointAt(inter), -proj);
            List<int> lFaces = new List<int>();
            lFaces.AddRange(faces);
            MeshFace mF = this.m.Faces[lFaces[0]];

            Vector3d bary = barycentric(
                fIr.tP.pt,
                this.m.Vertices[mF.A], this.m.Vertices[mF.B], this.m.Vertices[mF.C]);

            fIr.norm = this.m.NormalAt(lFaces[0], bary.Z, bary.X, bary.Y, 0.0);

            if (fIr.norm * rayL.Direction > 0) { fIr.norm = -fIr.norm; }

            return fIr;
        }

        // Give the direction of projection for a specific point based on the projection type.
        /// <summary>TODO The proj dir.</summary>
        /// <param name="pt">TODO The pt.</param>
        /// <returns>The <see cref="Vector3d"/>.</returns>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        [Pure]
        private Vector3d projDir(Point3d pt)
        {
            Vector3d pd = new Vector3d();
            switch (this.surfProj)
            {
                case SurfProj.Parallel:
                    pd = this.dir;
                    break;
                case SurfProj.Cylindrical:
                    Plane pl = new Plane(pt, this.dir);
                    if (this.cylOnto == null) { throw new NullReferenceException("Trying a cylindrical but the path to project to is not set."); }
                    if (this.cylOnto.IsLinear()) // if centre is a line treat it as infinite
                    {
                        Line cyLine = new Line(this.cylOnto.PointAtStart, this.cylOnto.PointAtEnd);
                        if (Intersection.LinePlane(cyLine, pl, out double lp))
                        {
                            pd = cyLine.PointAt(lp) - pt;
                        }
                        else
                        {
                            throw new InvalidOperationException("Cylinder Parallel: The projection direction is parallel to cylinder centre.");
                        }
                    }
                    else // Use curve and warn if no intersection
                    {
                        CurveIntersections ci = Intersection.CurvePlane(this.cylOnto, pl, 0.0000001);
                        if (ci == null || ci.Count == 0)
                        {
                            throw new InvalidOperationException("Short Cylinder:  The cylinder centre curve is shorter than the model.");
                        }

                        if (ci.Count > 1 || ci[0]?.IsOverlap != false)
                        {
                            throw new InvalidOperationException("Cylinder double cut: The cylinder centre curve has multiple intersections with a projection plane.");
                        }

                        pd = ci[0].PointA - pt;
                    }

                    break;
                case SurfProj.Spherical:
                    pd = this.cen - pt;
                    break;
            }

            return pd;
        }

        /// <summary>TODO The get curve.</summary>
        /// <returns>The <see cref="Curve"/>.</returns>
        [CanBeNull]
        public Curve getCurve()
        {
            Curve[] jc = Curve.JoinCurves(this, 1000000, true);
            if (jc != null && jc.Length > 0) { return jc[0]; }
            return null;
        }

        #region List Functions

        /// <inheritdoc />
        public int Count => ((IList<Curve>)this.paths).Count;
        /// <inheritdoc />
        public bool IsReadOnly => ((IList<Curve>)this.paths).IsReadOnly;

        /// <inheritdoc />
        [CanBeNull]
        public Curve this[int index]
        {
            get => this.paths[index];
            set => this.paths[index] = value;
        }

        /// <inheritdoc />
        public int IndexOf(Curve item) => this.paths.IndexOf(item);
        /// <inheritdoc />
        public void Insert(int index, Curve item) => this.paths.Insert(index, item);
        /// <inheritdoc />
        public void RemoveAt(int index) => this.paths.RemoveAt(index);
        /// <inheritdoc />
        public void Add(Curve item) => this.paths.Add(item);
        /// <summary>TODO The add range.</summary>
        /// <param name="items">TODO The items.</param>
        [PublicAPI]
        public void AddRange([NotNull] IEnumerable<Curve> items) => this.paths.AddRange(items);
        /// <inheritdoc />
        public void Clear() => this.paths.Clear();
        /// <inheritdoc />
        public bool Contains(Curve item) => this.paths.Contains(item);
        /// <inheritdoc />
        public void CopyTo(Curve[] array, int arrayIndex) => this.paths.CopyTo(array, arrayIndex);
        /// <inheritdoc />
        public bool Remove(Curve item) => ((IList<Curve>)this.paths).Remove(item);
        /// <inheritdoc />
        public IEnumerator<Curve> GetEnumerator() => this.paths.GetEnumerator();
        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => this.paths.GetEnumerator();

        #endregion

        /// <summary>TODO The get surf dir.</summary>
        /// <param name="tD">TODO The t d.</param>
        /// <returns>The <see cref="SurfToolDir"/>.</returns>
        public static SurfToolDir getSurfDir(int tD)
        {
            switch (tD)
            {
                case 0:
                    return SurfToolDir.Projection;
                case 1:
                    return SurfToolDir.PathTangent;
                case 2:
                    return SurfToolDir.PathNormal;
                case 3:
                    return SurfToolDir.Normal;
                default:
                    return SurfToolDir.Error;
            }
        }
    }

    // Grasshopper Type Wrapper
    /// <summary>TODO The g h_ surface path.</summary>
    public sealed class GH_SurfacePath : CAMel_Goo<SurfacePath>, IGH_PreviewData
    {
        /// <inheritdoc />
        public BoundingBox ClippingBox
        {
            get
            {
                BoundingBox bb = BoundingBox.Unset;
                if (this.Value == null) { return bb; }
                foreach (Curve c in this.Value) { bb.Union(c.GetBoundingBox(false)); }
                return bb;
            }
        }

        /// <summary>Initializes a new instance of the <see cref="GH_SurfacePath"/> class.</summary>
        [UsedImplicitly]
        public GH_SurfacePath() => this.Value = null;

        /// <summary>Initializes a new instance of the <see cref="GH_SurfacePath"/> class.</summary>
        /// <param name="sP">TODO The s p.</param>
        public GH_SurfacePath([CanBeNull] SurfacePath sP) => this.Value = sP;

        /// <summary>Initializes a new instance of the <see cref="GH_SurfacePath"/> class.</summary>
        /// <param name="sP">TODO The s p.</param>
        public GH_SurfacePath([CanBeNull] GH_SurfacePath sP) => this.Value = sP?.Value;

        /// <inheritdoc />
        [NotNull]
        public override IGH_Goo Duplicate() => new GH_SurfacePath(this);

        /// <inheritdoc />
        public override bool CastTo<TQ>(ref TQ target)
        {
            if (this.Value == null) { return false; }
            if (typeof(TQ).IsAssignableFrom(typeof(SurfacePath)))
            {
                target = (TQ)(object)this.Value;
                return true;
            }

            if (typeof(TQ).IsAssignableFrom(typeof(Curve)))
            {
                target = (TQ)(object)this.Value.getCurve();
                return true;
            }
            // ReSharper disable once InvertIf
            if (typeof(TQ).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (TQ)(object)new GH_Curve(this.Value.getCurve());
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source) {
                case null: return false;

                // From unwrapped
                case SurfacePath sP:
                    this.Value = sP;
                    return true;
                default: return false;
            }
        }

        /// <inheritdoc />
        public void DrawViewportWires([CanBeNull] GH_PreviewWireArgs args)
        {
            if (this.Value == null || args?.Pipeline == null) { return; }
            foreach (Curve l in this.Value) { args.Pipeline.DrawCurve(l, args.Color); }
        }

        /// <inheritdoc />
        public void DrawViewportMeshes([CanBeNull] GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    /// <summary>TODO The g h_ surface path par.</summary>
    public class GH_SurfacePathPar : GH_Param<GH_SurfacePath>, IGH_PreviewObject
    {
        /// <summary>Initializes a new instance of the <see cref="GH_SurfacePathPar"/> class.</summary>
        public GH_SurfacePathPar()
            : base(
                "Surfacing Path", "SurfacePath",
                "Contains the information to project a path onto a surface",
                "CAMel", "  Params", GH_ParamAccess.item) { }
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("FCB36AFC-195B-4DFA-825B-A986875A3A86");

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
        protected override System.Drawing.Bitmap Icon => Properties.Resources.surfacepath;
    }
}