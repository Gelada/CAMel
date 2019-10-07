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

namespace CAMel.Types
{
    // The type of projection.
    // Parallel is along a single direction
    // Cylindrical points towards a path along a direction
    // Spherical points towards a point

    public enum SurfProj
    {
        Parallel,
        Cylindrical,
        Spherical
    }

    // The tool direction for surfacing.
    // Projection is along the projection direction
    // Path Tangent and Path Normal mix the projection and
    // surface normals.
    // Path Tangent gives the normal to the path in the plane given by the path tangent and projection direction
    // Path Normal gives the normal to the path in the plane given by the surface tangent normal to the path and projection direction
    // Normal is surface normal
    public enum SurfToolDir
    {
        Projection,
        PathTangent,
        PathNormal,
        Normal
    }

    // A path that will project to a surface for surfacing
    // TODO write a subclass for each projection
    public class SurfacePath : IList<Curve>, ICAMelBase
    {
        [NotNull] private readonly List<Curve> _paths; // Curves to project
        private SurfProj surfProj { get; } // Type of projection
        private Curve cylOnto { get; } // centre line for Cylindrical projection
        private Vector3d dir { get; } // direction for parallel projection, or line direction for cylindrical
        private Point3d cen { get; } // centre for spherical projection
        private SurfToolDir surfToolDir { get; } // method to calculate tool direction
        [NotNull] public MaterialTool mT { get; }

        // private storage when processing a model

        private Mesh _m; // Mesh

        // Parallel constructor
        public SurfacePath([NotNull] List<Curve> paths, [NotNull] MaterialTool mT, Vector3d dir, SurfToolDir sTd)
        {
            this._paths = paths;
            this.mT = mT;
            this.surfProj = SurfProj.Parallel;
            this.dir = dir;
            this.surfToolDir = sTd;
        }
        // Cylindrical constructor
        public SurfacePath([NotNull] List<Curve> paths, [NotNull] MaterialTool mT, Vector3d dir, [NotNull] Curve cc, SurfToolDir surfToolDir)
        {
            this._paths = paths;
            this.mT = mT;
            this.surfProj = SurfProj.Cylindrical;
            this.dir = dir;
            this.cylOnto = cc;
            this.surfToolDir = surfToolDir;
        }
        // Spherical constructor
        public SurfacePath([NotNull] List<Curve> paths, [NotNull] MaterialTool mT, Point3d cen, SurfToolDir surfToolDir)
        {
            this._paths = paths;
            this.mT = mT;
            this.surfProj = SurfProj.Spherical;
            this.cen = cen;
            this.surfToolDir = surfToolDir;
        }
        [NotNull]
        public SurfacePath changeFinishDepth(double cutDepth)
        {
            MaterialTool newMT = MaterialTool.changeFinishDepth(this.mT, cutDepth);
            switch (this.surfProj)
            {
                case SurfProj.Parallel:
                    return new SurfacePath(this._paths, newMT, this.dir, this.surfToolDir);
                case SurfProj.Cylindrical:
                    if (this.cylOnto == null) { Exceptions.nullPanic(); }
                    return new SurfacePath(this._paths, newMT, this.dir, this.cylOnto, this.surfToolDir);
                case SurfProj.Spherical:
                    return new SurfacePath(this._paths, newMT, this.cen, this.surfToolDir);
                default:
                    Exceptions.badSurfacePath();
                    return new SurfacePath(this._paths, newMT, this.cen, this.surfToolDir);
            }
        }

        public string TypeDescription => "Path and projection information to generate a surfacing path";
        public string TypeName => "SurfacePath";

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

        [NotNull]
        public MachineOperation generateOperation([NotNull] Brep b, double offset, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tPa)
        {
            // Just convert to Mesh
            MeshingParameters mP = MeshingParameters.Smooth;
            if (mP == null) { Exceptions.nullPanic(); }
            mP.MaximumEdgeLength = this.mT.toolWidth / 2.0;
            mP.ComputeCurvature = true;
            mP.MinimumEdgeLength = 0.00001;
            this._m = Mesh.CreateFromBrep(b, mP)?[0];
            this._m?.FaceNormals?.ComputeFaceNormals();

            return generateOperation_(offset, mF, tPa);
        }
        [NotNull]
        public MachineOperation generateOperation([NotNull] Mesh m, double offset, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tPa)
        {
            if (m.FaceNormals == null) { throw new ArgumentNullException(); }
            m.FaceNormals.ComputeFaceNormals();
            this._m = m;

            return generateOperation_(offset, mF, tPa);
        }
        // actual code to generate the operation
        [NotNull]
        private MachineOperation generateOperation_(double offset, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tPa)
        {
            if (this._m == null) { throw new NullReferenceException("Trying to generate a surfacing path with no mesh set. "); }
            // create unprojected toolpath (mainly to convert the curve into a list of points)
            List<ToolPath> tPs = new List<ToolPath>();

            foreach (Curve p in this._paths)
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

                foreach (ToolPoint tPt in tP) //initialise dictionary
                { intersectInfo[tPt] = new FirstIntersectResponse(); }

                Parallel.ForEach(tP, tPtP =>
                    {
                        if (tPtP != null) { intersectInfo[tPtP] = firstIntersect(tPtP); }
                    }
                );

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
            MachineOperation mO = new MachineOperation(ToString(), newTPs);
            return mO;
        }

        private struct FirstIntersectResponse
        {
            public ToolPoint tP { get; set; }
            public Vector3d norm { get; set; }
            public bool hit { get; set; }

            public override string ToString() => this.hit.ToString();
        }

        // Transcribed from Christer Ericson's Real-Time Collision Detection
        // Compute barycentric coordinates (u, v, w) for
        // point p with respect to triangle (a, b, c)
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

        private FirstIntersectResponse firstIntersect([NotNull] ToolPoint tP)
        {
            FirstIntersectResponse fIr = new FirstIntersectResponse {hit = false};
            if (this._m?.FaceNormals == null) { return fIr; }

            Vector3d proj = projDir(tP.pt);
            Ray3d rayL = new Ray3d(tP.pt, proj);

            double inter = Intersection.MeshRay(this._m, rayL, out int[] faces);

            if (!(inter >= 0)) { return fIr; }

            fIr.hit = true;
            fIr.tP = new ToolPoint(rayL.PointAt(inter), -proj);
            List<int> lFaces = new List<int>();
            lFaces.AddRange(faces);
            MeshFace mF = this._m.Faces[lFaces[0]];

            Vector3d bary = barycentric(fIr.tP.pt,
                this._m.Vertices[mF.A], this._m.Vertices[mF.B], this._m.Vertices[mF.C]);

            fIr.norm = this._m.NormalAt(lFaces[0], bary.Z, bary.X, bary.Y, 0.0);

            if (fIr.norm * rayL.Direction > 0) { fIr.norm = -fIr.norm; }

            return fIr;
        }
        // Give the direction of projection for a specific point based on the projection type.
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

        [CanBeNull]
        public Curve getCurve()
        {
            Curve[] jc = Curve.JoinCurves(this, 1000000, true);
            if (jc != null && jc.Length > 0) { return jc[0]; }
            return null;
        }

        #region List Functions

        public int Count => ((IList<Curve>) this._paths).Count;
        public bool IsReadOnly => ((IList<Curve>) this._paths).IsReadOnly;

        [CanBeNull]
        public Curve this[int index]
        {
            get => this._paths[index];
            set => this._paths[index] = value;
        }

        public int IndexOf(Curve item) => this._paths.IndexOf(item);
        public void Insert(int index, Curve item) => this._paths.Insert(index, item);
        public void RemoveAt(int index) => this._paths.RemoveAt(index);
        public void Add(Curve item) => this._paths.Add(item);
        [PublicAPI]
        public void AddRange([NotNull] IEnumerable<Curve> items) => this._paths.AddRange(items);
        public void Clear() => this._paths.Clear();
        public bool Contains(Curve item) => this._paths.Contains(item);
        public void CopyTo(Curve[] array, int arrayIndex) => this._paths.CopyTo(array, arrayIndex);
        public bool Remove(Curve item) => ((IList<Curve>) this._paths).Remove(item);
        public IEnumerator<Curve> GetEnumerator() => this._paths.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this._paths.GetEnumerator();

        #endregion
    }

    // Grasshopper Type Wrapper
    public sealed class GH_SurfacePath : CAMel_Goo<SurfacePath>, IGH_PreviewData
    {
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

        // Default Constructor
        [UsedImplicitly]
        public GH_SurfacePath() { this.Value = null; }
        // From Unwrapped
        public GH_SurfacePath([CanBeNull] SurfacePath sP) { this.Value = sP; }
        // Copy Constructor (just reference as SurfacePath is Immutable)
        public GH_SurfacePath([CanBeNull] GH_SurfacePath sP) { this.Value = sP?.Value; }
        // Duplicate
        [NotNull]
        public override IGH_Goo Duplicate() => new GH_SurfacePath(this);

        public override bool CastTo<TQ>(ref TQ target)
        {
            if (this.Value == null) { return false; }
            if (typeof(TQ).IsAssignableFrom(typeof(SurfacePath)))
            {
                target = (TQ) (object) this.Value;
                return true;
            }
            if (typeof(TQ).IsAssignableFrom(typeof(Curve)))
            {
                target = (TQ) (object) this.Value.getCurve();
                return true;
            }
            // ReSharper disable once InvertIf
            if (typeof(TQ).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (TQ) (object) new GH_Curve(this.Value.getCurve());
                return true;
            }
            return false;
        }
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

        public void DrawViewportWires([CanBeNull] GH_PreviewWireArgs args)
        {
            if (this.Value == null || args?.Pipeline == null) { return; }
            foreach (Curve l in this.Value) { args.Pipeline.DrawCurve(l, args.Color); }
        }
        public void DrawViewportMeshes([CanBeNull] GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    public class GH_SurfacePathPar : GH_Param<GH_SurfacePath>, IGH_PreviewObject
    {
        public GH_SurfacePathPar() :
            base("Surfacing Path", "SurfacePath", "Contains the information to project a path onto a surface", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid => new Guid("FCB36AFC-195B-4DFA-825B-A986875A3A86");

        public bool Hidden { get; set; }
        public bool IsPreviewCapable => true;
        public BoundingBox ClippingBox => Preview_ComputeClippingBox();
        public void DrawViewportWires([CanBeNull] IGH_PreviewArgs args) => Preview_DrawWires(args);
        public void DrawViewportMeshes([CanBeNull] IGH_PreviewArgs args) => Preview_DrawMeshes(args);

        /// <inheritdoc />
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        [CanBeNull]
        protected override System.Drawing.Bitmap Icon => Properties.Resources.surfacepath;
    }
}