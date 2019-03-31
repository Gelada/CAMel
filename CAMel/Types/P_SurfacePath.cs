using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        // private storage when processing a model

        private Mesh _m; // Mesh

        // Parallel constructor
        public SurfacePath([NotNull] List<Curve> paths, Vector3d dir, SurfToolDir sTd)
        {
            this._paths = paths;
            this.surfProj = SurfProj.Parallel;
            this.dir = dir;
            this.surfToolDir = sTd;
        }
        // Cylindrical constructor
        public SurfacePath([NotNull] List<Curve> paths, Vector3d dir, [NotNull] Curve cc, SurfToolDir surfToolDir)
        {
            this._paths = paths;
            this.surfProj = SurfProj.Cylindrical;
            this.dir = dir;
            this.cylOnto = cc;
            this.surfToolDir = surfToolDir;
        }
        // Spherical constructor
        public SurfacePath([NotNull] List<Curve> paths, Point3d cen, SurfToolDir surfToolDir)
        {
            this._paths = paths;
            this.surfProj = SurfProj.Spherical;
            this.cen = cen;
            this.surfToolDir = surfToolDir;
        }

        public string TypeDescription => "Path and projection information to generate a surfacing path";
        public string TypeName => "SurfacePath";

        public override string ToString()
        {
            string op = "Surfacing:";
            switch (this.surfProj)
            {
                case SurfProj.Parallel:
                    op = op + " Parallel Projection";
                    break;
                case SurfProj.Cylindrical:
                    op = op + " Cylindrical Projection";
                    break;
                case SurfProj.Spherical:
                    op = op + " Spherical Projection";
                    break;
            }
            return op;
        }

        // Different calls to Generate a Machine Operation from different surfaces

        [NotNull]
        public MachineOperation generateOperation([NotNull] Brep b, double offset, [NotNull] MaterialTool mT, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tPa)
        {
            // Just convert to Mesh
            MeshingParameters mP = MeshingParameters.Smooth;
            this._m = Mesh.CreateFromBrep(b, mP)?[0];

            return generateOperation_(offset, mT, mF, tPa);
        }
        [NotNull]
        public MachineOperation generateOperation([NotNull] Mesh m, double offset, [NotNull] MaterialTool mT, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tPa)
        {
            if (m.FaceNormals == null) { throw new ArgumentNullException(); }
            m.FaceNormals.ComputeFaceNormals();
            this._m = m;

            return generateOperation_(offset, mT, mF, tPa);
        }
        // actual code to generate the operation
        [NotNull]
        private MachineOperation generateOperation_(double offset, [NotNull] MaterialTool mT, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tPa)
        {
            if (this._m == null) { throw new NullReferenceException("Trying to generate a surfacing path with no mesh set. ");
            }
            // create unprojected toolpath (mainly to convert the curve into a list of points)
            List<ToolPath> tPs = new List<ToolPath>();

            foreach (Curve p in this._paths)
            {
                tPs.Add(new ToolPath(string.Empty, mT, mF, tPa));
                tPs[tPs.Count - 1]?.convertCurve(p, new Vector3d(0, 0, 1));
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

                ToolPath tempTP = new ToolPath(string.Empty, mT, mF, tPa);
                List<Vector3d> tempN = new List<Vector3d>();

                foreach (ToolPoint tPt in tP)
                {
                    FirstIntersectResponse fIr = intersectInfo[tPt];
                    if (fIr.hit)
                    {
                        tempTP.Add(fIr.tP);
                        tempN.Add(fIr.norm);
                    }
                    else if (tempTP.Count > 0)
                    {
                        if (tempTP.Count > 1)
                        {
                            newTPs.Add(tempTP);
                            norms.Add(tempN);
                        }
                        tempTP = new ToolPath(string.Empty, mT, mF, tPa);
                        tempN = new List<Vector3d>();
                    }
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
                    // Adjust the tool position based on the surface normal and the tool orientation
                    // so that the cutting surface not the tooltip is at the correct point

                    if (norms[j]?[i] == null) { break; }
                    newTPs[j][i].pt = newTPs[j][i].pt + mT.cutOffset(newTPs[j][i].dir, norms[j][i]);

                    // Move to offset using normal

                    newTPs[j][i].pt = newTPs[j][i].pt + offset * norms[j][i];
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
            fIr.norm = this._m.FaceNormals[lFaces[0]];

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