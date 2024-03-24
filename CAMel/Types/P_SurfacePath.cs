using Eto.Forms;

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
        /// <summary>Project toolpath to surface parallel to a vector. </summary>
        Parallel,
        /// <summary>Project toolpath to surface around a cylinder. </summary>
        Cylindrical,
        /// <summary>Project toolpath to surface via a sphere. </summary>
        Spherical
    }

    /// <summary>TODO The surf tool dir.</summary>
    public enum SurfToolDir
    {
        /// <summary>Tool Direction should be equal to projection. </summary>
        Projection,
        /// <summary>Tool Direction should be normal along path tangent. </summary>
        PathTangent,
        /// <summary>Tool Direction should be normal orthogonal to tangent. </summary>
        PathNormal,
        /// <summary>Tool Direction should be the surface normal. </summary>
        Normal,
        /// <summary>Tool Direction should be normal to material. </summary>
        Material,
        /// <summary>An error has occured. </summary>
        Error
    }

    /// <summary>Add some extra surfacing information to a curve</summary>
    public class SurfaceCurve
    {
        public Curve C { get; set; }
        public bool lifted { get; set; }

        public SurfaceCurve(Curve C, bool lifted)
        {
            this.C = C;
            this.lifted = lifted;
        }
    }

    // TODO write a subclass for each projection
    /// <summary>A path that will project to a surface for surfacing</summary>
    public class SurfacePath : IList<SurfaceCurve>, ICAMelBase
    {
        /// <summary>Curves to project. </summary>
        [NotNull] private readonly List<SurfaceCurve> paths;
        /// <summary>Gets the type of projection.</summary>
        private SurfProj surfProj { get; }
        /// <summary>Gets the centre line for Cylindrical projection</summary>
        private Curve cylOnto { get; }
        /// <summary>Gets the direction for parallel projection, or line direction for cylindrical.</summary>
        private Vector3d dir { get; }
        /// <summary>Gets the centre for spherical projection.</summary>
        private Point3d cen { get; }
        /// <summary>Gets the method to calculate tool direction.</summary>
        private SurfToolDir surfToolDir { get; }
        /// <summary>Gets the Material Tool.</summary>
        [NotNull]
        public MaterialTool mT { get; }

        /// <summary>Privately stored mesh when processing a model</summary>
        private Mesh m;

        // Parallel constructor
        /// <summary>Initializes a new instance of the <see cref="SurfacePath"/> class.</summary>
        /// <param name="paths">TODO The paths.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="dir">TODO The dir.</param>
        /// <param name="sTd">TODO The s td.</param>
        public SurfacePath([NotNull] List<SurfaceCurve> paths, [NotNull] MaterialTool mT, Vector3d dir, SurfToolDir sTd)
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
        public SurfacePath([NotNull] List<SurfaceCurve> paths, [NotNull] MaterialTool mT, Vector3d dir, [NotNull] Curve cc, SurfToolDir surfToolDir)
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
        public SurfacePath([NotNull] List<SurfaceCurve> paths, [NotNull] MaterialTool mT, Point3d cen, SurfToolDir surfToolDir)
        {
            this.paths = paths;
            this.mT = mT;
            this.surfProj = SurfProj.Spherical;
            this.cen = cen;
            this.surfToolDir = surfToolDir;
        }
        public void setMesh(Mesh m) => this.m = m;
        public void clearMesh(Mesh m) => this.m = null;

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
            op = op + " with " + this.Count.ToString() + " paths.";

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
            MeshingParameters mP = MeshingParameters.QualityRenderMesh;
            if (mP == null) { Exceptions.nullPanic(); }
            mP.MaximumEdgeLength = this.mT.toolWidth / 2.0;
            mP.ComputeCurvature = true;
            mP.MinimumEdgeLength = 0.00001;
            this.m = Mesh.CreateFromBrep(b, mP)?[0];
            this.m?.FaceNormals?.ComputeFaceNormals();
            this.m?.Weld(Math.PI);

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

        /// <summary>Generate surfacing operation from path data.</summary>
        /// <param name="offset">Offset Distance to finish from surface</param>
        /// <param name="mF">MaterialForm being cut</param>
        /// <param name="tPa">ToolPathAdditions for the finished toolpaths</param>
        /// <returns>Machine Operation performing the surfacing</returns>
        /// <exception cref="NullReferenceException"></exception>
        [NotNull]
        private MachineOperation generateOperation_(double offset, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tPa)
        {
            if (this.m == null) { throw new NullReferenceException("Trying to generate a surfacing path with no mesh set. "); }

            List<ToolPath> newTPs = new List<ToolPath>();

            foreach (SurfaceCurve p in this.paths)
            {
                // create unprojected toolpath (mainly to convert the curve into a list of points)
                ToolPath tP = new ToolPath(string.Empty, this.mT, mF, tPa);

                tP.convertCurve(p.C, new Vector3d(0, 0, 1), 10);

                // move points onto surface storing projection direction
                // and norm on the toolpoint

                ToolPoint[] tPinter = this.firstIntersect(tP, p.lifted);

                ToolPath tempTP = new ToolPath(string.Empty, this.mT, mF, tPa);

                int missed = 0;

                // ReSharper disable once AssignNullToNotNullAttribute
                for(int i = 0; i<tP.Count;i++)
                {
                    ToolPoint tPt = tPinter[i];
                    if (tPt.normalSet)
                    {
                        // If the step between toolPoints is too long project a line to the surface to catch details
                        if(missed == 0 && tempTP.lastP != null && tempTP.lastP.pt.DistanceTo(tPt.pt) > this.mT.minStep*2.0)
                        {
                            tempTP.AddRange(this.lineIntersect(tP[i - 1], tPinter[i - 1], tP[i], tPt, mT, p.lifted));
                        }
                        // Check to see if a new path is needed.
                        if (missed > 5 && tempTP.lastP != null)
                        {
                            if (tempTP.lastP.pt.DistanceTo(tPt.pt) > this.mT.pathJump)
                            {
                                if (tempTP.Count > 1) { newTPs.Add(tempTP); }
                                tempTP = new ToolPath(string.Empty, this.mT, mF, tPa);
                            }
                        }

                        tempTP.Add(tPt);
                        missed = 0;
                    }
                    else if (tempTP.Count > 0) { missed++; }
                }

                if (tempTP.Count <= 1) { continue; }
                newTPs.Add(tempTP);
            }

            foreach (ToolPath tP in newTPs)
            {
                for (int i = 0; i < tP?.Count; i++)
                {
                    // find the tangent vector, assume the curve is not too jagged
                    // this is reasonable for most paths, but not for space
                    // filling curve styles. Though for those this is a bad option.
                    // For the moment we will look 2 points back and one point forward.
                    // Some cases to deal with the start, end and short paths.
                    // TODO Smooth this out by taking into account individual tangencies?
                    int lookBack, lookForward;
                    if (i == tP.Count - 1)
                    {
                        lookBack = 3;
                        if (i < lookBack) { lookBack = tP.Count - 1; }
                        lookForward = 0;
                    }
                    else
                    {
                        lookBack = Math.Min(i, 2);
                        lookForward = 3 - lookBack;
                        if (lookForward + i >= tP.Count) { lookForward = tP.Count - i - 1; }
                    }

                    Vector3d tangent = tP[i + lookForward].pt - tP[i - lookBack].pt;

                    tP[i].dir = this.ToolDir(tP[i], tangent, mF);

                    // Adjust the tool position based on the surface normal and the tool orientation
                    // so that the cutting surface not the tooltip is at the correct point

                    if (tP[i].lifted) { tP[i] = this.liftOff(tP[i]); }
                    else { tP[i].pt += this.mT.cutOffset(tP[i]); }

                    // If the whole path is being offset move away from the surface using normal
                    // TODO work out how to make the surface offset be correct (distance from surface) and work with pocketing.

                    tP[i].pt += offset * tP[i].dir;
                }
            }

            // make the machine operation
            MachineOperation mO = new MachineOperation(this.ToString(), newTPs);
            return mO;
        }

        Vector3d ToolDir(ToolPoint tP, Vector3d tangent, IMaterialForm mF)
        {
            switch (this.surfToolDir)
            {
                case SurfToolDir.Projection:
                    // assume the direction is the projected direction
                    return tP.dir;
                case SurfToolDir.PathNormal:
                    // get normal to tangent on surface
                    Vector3d stNorm = Vector3d.CrossProduct(tP.norm, tangent);
                    Vector3d pNplaneN = Vector3d.CrossProduct(tP.dir, stNorm);

                    // find vector normal to the surface in the line orthogonal to the tangent
                    return Vector3d.CrossProduct(stNorm, pNplaneN);
                case SurfToolDir.PathTangent:
                    // get normal to proj and tangent
                    Vector3d pTplaneN = Vector3d.CrossProduct(tangent, tP.dir);

                    // find vector normal to tangent and in the plane of tangent and projection
                    return Vector3d.CrossProduct(pTplaneN, tangent);
                case SurfToolDir.Normal: // set to Norm
                    return tP.norm;
                case SurfToolDir.Material: // set to Material Norm
                    MFintersects mat = mF.intersect(tP, 0);

                    // leave projection direction if miss material
                    if (mat.hits) { return -mat.first.away; }
                    else { return tP.dir; }
            }
            return tP.dir;
        }

        /// <summary>Get the first intersections between a projected toolpath and the mesh</summary>
        /// <param name="tP">ToolPath to project</param>
        /// <param name="lift">Set true to lift the tool so it just touches the surface</param>
        /// <returns>Projected toolpoints</returns>
        private ToolPoint[] firstIntersect([NotNull] ToolPath tP, bool lift)
        {
            ToolPoint[] tPinter = new ToolPoint[tP.Count];
            Parallel.For((int)0, tP.Count, i => tPinter[(int)i] = this.firstIntersect(tP[(int)i], lift) );

            return tPinter;
        }

        /// <summary>Get the first intersection between a projected toolpoint and the mesh</summary>
        /// <param name="tPin">ToolPoint to project</param>
        /// <param name="lift">Set true to lift the tool so it just touches the surface</param>
        /// <returns>Projected toolpoint</returns>
        private ToolPoint firstIntersect([NotNull] ToolPoint tPin, bool lift)
        {
            ToolPoint fIr = new ToolPoint();
            if (this.m?.FaceNormals == null) { return fIr; }

            Vector3d proj = this.projDir(tPin.pt);
            Ray3d rayL = new Ray3d(tPin.pt, proj);

            double inter = Intersection.MeshRay(this.m, rayL, out int[] faces);

            if (inter < 0) { return fIr; }

            ToolPoint tP = new ToolPoint(rayL.PointAt(inter), -proj);
            tP.meshface = faces[0];
            tP.setNorm(this.m);

            if (tP.norm * rayL.Direction > 0) { tP.norm = -tP.norm; }

            tP.lifted = lift;

            return tP;
        }

        /// <summary>Lift a toolpoint so it touches the surface at the correct normal</summary>
        /// <param name="tPt">ToolPoint on surface to lift</param>
        /// <returns>Lifted ToolPoint</returns>
        [NotNull]
        private ToolPoint liftOff([NotNull] ToolPoint tPt)
        {
            ToolPoint lifted = tPt.deepClone();
            ToolPoint startPt = tPt.deepClone();

            double faceLift = this.mT.liftOffset(lifted, startPt); // Lift needed to touch plane through startPt
            if (faceLift < CAMel_Goo.Tolerance) { return lifted; } // nothing needed

            // get things started
            Vector3d n = Vector3d.CrossProduct(lifted.dir,startPt.norm);
            Plane pl = new Plane(startPt.pt, n);

            this.startMeshStep(ref startPt, pl, tPt.dir, out ToolPoint endPt); // TODO check for surface edge

            if (this.mT.liftOnEdge(startPt, endPt, lifted, faceLift))
            {
                lifted.pt += faceLift * lifted.dir;
                return lifted;
            }
            
            double edgeLift = this.mT.liftOffset(lifted, endPt.pt); // Lift needed to touch point
            lifted.pt += edgeLift * lifted.dir; // lift and continue

            int j = 0;
            while (j < 50)
            {
                n = Vector3d.CrossProduct(lifted.dir,endPt.norm);
                pl = new Plane(endPt.pt, n);
                this.meshStep(ref startPt, endPt, pl, out ToolPoint nextPt); // TODO check for surface edge
                endPt = nextPt;

                // if already missing the next face can finish
                if (this.mT.clearNextFace(startPt, endPt, lifted)) { return lifted; }

                faceLift = this.mT.liftOffset(lifted, startPt); // Lift needed to touch plane through startPt

                // TODO Generalise, currently only works for ball end mills

                if (this.mT.liftOnEdge(startPt, endPt, lifted, faceLift))
                {
                    lifted.pt += faceLift * lifted.dir;
                    return lifted;
                }

                edgeLift = this.mT.liftOffset(lifted, endPt.pt); // Lift needed to touch point
                lifted.pt += edgeLift * lifted.dir;

                j++;
            }
            return lifted;
        }



        /// <summary>Project a line to the mesh, knowing projections of start and end points</summary>
        /// <param name="tP1">ToolPoint at line start</param>
        /// <param name="tP1p">Projection of line start</param>
        /// <param name="tP2">ToolPoint at line end</param>
        /// <param name="tP2p">Projection of line end</param>
        /// <param name="mT">Material and Tool Information for the path</param>
        /// <param name="lift">Set true to lift the tool so it just touches the surface</param>
        /// <returns>Projected toolpoint</returns>
        private ToolPath lineIntersect([NotNull] ToolPoint tP1, [NotNull] ToolPoint tP1p, [NotNull] ToolPoint tP2, [NotNull] ToolPoint tP2p, [NotNull] MaterialTool mT, bool lift)
        {
            // use the triangle between the middle of the line to project and the line
            // between the projected points to create the plane to intersect the surface

            Point3d c = (tP1.pt + tP2.pt) / 2.0;
            Vector3d e1 = tP1p.pt - c;
            Vector3d e2 = tP2p.pt - c;

            Plane Pl = new Plane(tP1p.pt, Vector3d.CrossProduct(e1, e2));

            ToolPath tP = this.followMesh(tP1p, tP2p, Pl);

            tP.matTool = mT;

            foreach (ToolPoint tPt in tP)
            {
                tPt.dir = -this.projDir(tPt.pt);
                tPt.lifted = lift;
            }

            tP.simplify(0.2);

            return tP;
        }
        [NotNull]
        public List<ToolPath> outerLoops(bool lift)
        {
            // get mesh boundaries
            List<List<int>> boundaries = this.meshBoundary(out List<Vector3d> norms);

            // for each edge find the path away into the mesh,
            // find the place where the tool will touch but not intersect the surface

            List<ToolPath> tPs = new List<ToolPath>();

            if (lift)
            {
                for (int i = 0; i < boundaries.Count; i++)
                {
                    ToolPath tP = new ToolPath { matTool = this.mT };

                    for (int j = 1; j<boundaries[i].Count - 1; j++)
                    {
                        int ptN = boundaries[i][j];
                        int ptNmo = boundaries[i][j - 1];
                        ToolPoint tPt = new ToolPoint();
                        tPt.pt = (Point3d)this.m.Vertices[ptN] * 0.9 + (Point3d)this.m.Vertices[ptNmo] * 0.1;
                        int edge = this.m.TopologyEdges.GetEdgeIndex(ptN, ptNmo);
                        tPt.meshface = this.m.TopologyEdges.GetConnectedFaces(edge)[0];
                        tPt.setNorm(this.m);
                        tPt.dir = this.projDir(tPt.pt);
                        tP.Add(tPt);
                    }
                    tP.Add(tP[0].deepClone()); // close curve
                    tPs.Add(tP);
                }

                return tPs;
            }

            for (int i = 0; i < boundaries.Count; i++)
            {
                ToolPath tP = new ToolPath{ matTool = this.mT };

                for (int j = 1; j<boundaries[i].Count - 1; j++)
                {
                    if (!badEdge(boundaries[i][j - 1], boundaries[i][j], 8))
                    {
                        ToolPath push = this.pushAlong(boundaries[i][j - 1], boundaries[i][j], out Vector3d tangent,
                            out Plane avoidPlane);
                        tP.Add(this.avoidOffset(push, avoidPlane, tangent));
                    }
                }
                tP.Add(tP[0].deepClone()); // close curve
                tPs.Add(tP);
            }
            return tPs;
        }

        private bool badEdge(int pt1, int pt2, double maxAspect)
        {
            int edge = this.m.TopologyEdges.GetEdgeIndex(pt1, pt2);
            int[] faces = this.m.TopologyEdges.GetConnectedFaces(edge);
            double aspect = 1;
            foreach (int face in faces)
            {
                double ar =this.m.Faces.GetFaceAspectRatio(face);
                if (ar > aspect) { aspect = ar; }
            }
            return aspect > maxAspect;
        }

        /// <summary>Find the first place on the toolpath where the offset tool in more than a half toolwidth from the planes</summary>
        /// <param name="push">ToolPath to move along</param>
        /// <param name="avoid1">First Plane to avoid</param>
        /// <param name="avoid2">Second Plane to avoid</param>
        /// <returns>Intersected path</returns>
        [NotNull]
        private ToolPoint avoidOffset(ToolPath push, Plane avoid, Vector3d tangent)
        {
            // when creating the surfacepath we do not yet know the materialForm,
            // so need to throw an error if the tool direction uses that
            // TODO does removal of that method make sense? 
            // Can be replaced with a change to the stepdown for example
            if(this.surfToolDir == SurfToolDir.Material) { Exceptions.materialDirectionPreciseException(); }
            int i;

            Vector3d edge = new Vector3d();
            // step along to find first point on path sufficiently far away
            for (i = 1; i < push.Count; i++)
            {
                edge = push[i].pt - push[i - 1].pt;
                edge.Unitize();
                this.approachPlane(push[i], avoid, edge, tangent, out double distance);
                if (distance > 0) { break; }
            }
            ToolPoint osTpt;
            if (i == push.Count)
            {
                osTpt = push[i - 1].deepClone();
                osTpt.dir = this.projDir(osTpt.pt);
                osTpt.addError("Offsetting avoiding edge (for precise edging) failed. ");
            }
            else
            {
                osTpt = this.approachPlane(push[i], avoid, edge, tangent, out double distance);
                osTpt = this.approachPlane(osTpt, avoid, edge, tangent, out distance);
                if (distance > 0.001)
                {
                    osTpt = this.approachPlane(osTpt, avoid, edge, tangent, out distance);
                }
            }

            //osTp.pt -= this.projDir(osTp.pt);

            return osTpt;
        }

        [NotNull]
        private ToolPoint approachPlane([NotNull] ToolPoint tPt, Plane avoid, Vector3d edge, Vector3d tangent, out double distance)
        {
            MFBox mFBlank = new MFBox(new Box(), .1, 1); // hopefully unused placeholder

            ToolPoint newTPt = tPt.deepClone();

            newTPt.dir = this.ToolDir(tPt, tangent, mFBlank);
            Point3d toolPos = newTPt.pt + this.mT.cutOffset(newTPt);

            distance = avoid.DistanceTo(toolPos) - this.mT.toolWidth / 2.0;

            double move = distance / (avoid.ZAxis * edge); // Length along path on face that projects to move.

            newTPt.pt -= move * edge;

            if (this.m?.Faces == null || this.m?.Vertices == null) { return newTPt; }

            newTPt.setNorm(this.m);

            return newTPt;
        }

        /// <summary>Follow the intersection between the mesh and a plane, between points on the intersection.</summary>
        /// <param name="tPt1">ToolPoint to start at</param>
        /// <param name="tPt2">ToolPoint to finish at</param>
        /// <param name="pl">The plane to intersect</param>
        /// <returns>Intersected path</returns>
        private ToolPath followMesh(ToolPoint tPt1, ToolPoint tPt2, Plane pl)
        {
            ToolPath tp = new ToolPath();
            ToolPoint oldTpt = tPt1.deepClone();
            Vector3d dir = tPt2.pt - tPt1.pt; // direction of move
            ToolPoint utPt1 = tPt1.deepClone();
            if (!startMeshStep(ref utPt1, pl, dir, out ToolPoint newTpt)) 
            {
                if (utPt1.meshface != tPt2.meshface) { newTpt.addWarning("Edge of surface hit in the middle of toolpath"); }
                return tp;
            }

            int j = 0;
            while(j<1000 && (tPt2.pt - newTpt.pt)*dir > 0)
            {
                tp.Add(newTpt);
                if(!meshStep(ref oldTpt, newTpt, pl, out ToolPoint nextTpt))
                {
                    if (newTpt.meshface != tPt2.meshface) { newTpt.addWarning("Edge of surface hit in the middle of toolpath"); }
                    break;
                }
                newTpt = nextTpt;
                j++;
            }

            return tp;
        }

        /// <summary>Move along the mesh intersection with a plane further than a given vector</summary>
        /// <param name="tPt">ToolPoint to start at</param>
        /// <param name="pl">The plane to intersect</param>
        /// <param name="push">The vector to go beyond</param>
        /// <returns>Intersected path</returns>
        private ToolPath pushAlong(ToolPoint tPt, Plane pl, Vector3d push)
        {
            ToolPoint utPt = tPt.deepClone();
            ToolPath tp = new ToolPath { utPt };

            if (!startMeshStep(ref utPt, pl, push, out ToolPoint newTpt))
            {
                newTpt.addWarning("Edge of surface hit in the middle of toolpath");
                return tp;
            }

            ToolPoint oldTpt = utPt;
            int j = 0;
            while (j < 1000 && (newTpt.pt - tPt.pt - push) * push < 0 )
            {
                if (!meshStep(ref oldTpt, newTpt, pl, out ToolPoint nextTpt))
                {
                    newTpt.addWarning("Edge of surface hit in the middle of toolpath");
                    break;
                }
                tp.Add(newTpt);
                newTpt = nextTpt;
                j++;
            }

            return tp;
        }
        /// <summary>Use three vertices of the mesh, the tool and projection information to push along the mesh</summary>
        /// <param name="tPt">ToolPoint to start at</param>
        /// <param name="pl">The plane to intersect</param>
        /// <param name="push">The vector to go beyond</param>
        /// <returns>Intersected path</returns>
        private ToolPath pushAlong(int pt1, int pt2, out Vector3d tangent, out Plane plane)
        {
            Point3d point1 = this.m.Vertices[pt1];
            Point3d point2 = this.m.Vertices[pt2];
            ToolPoint tPt = new ToolPoint();
            tPt.pt = point2*.5+point1*.5; // Place point at the midpoint of the edge
            int edge = this.m.TopologyEdges.GetEdgeIndex(pt1, pt2);
            tPt.meshface = this.m.TopologyEdges.GetConnectedFaces(edge)[0];
            tPt.setNorm(this.m);

            tangent = point2 - point1;
            tangent.Unitize();

            Vector3d dir = this.projDir(point1) + this.projDir(point2);
            dir.Unitize();

            plane = new Plane(point2, tangent, dir);

            Vector3d push = Vector3d.CrossProduct(tangent, dir);
            push.Unitize();
            push = push * this.mT.toolWidth;
            Plane pl = new Plane(tPt.pt, tangent);
            ToolPath tP = this.pushAlong(tPt, pl, push);
            tP.Insert(0, tPt);

            return tP;
        }

        private List<List<int>> meshBoundary(out List<Vector3d> norms)
        {
            HashSet<int> naked = new HashSet<int>();
            List<List<int>> bounds = new List<List<int>>();
            norms = new List<Vector3d>();
            for (int i=0; i<m.TopologyEdges.Count; i++) 
            { 
                if(m.TopologyEdges.GetConnectedFaces(i).Count() < 2) { 
                    naked.Add(i);
                }
            }
            if (naked.Count == 0) { return bounds; } 

            List<int> temp = new List<int>();
            int curE = naked.FirstOrDefault();
            int firstE = curE;
            int curV = this.m.TopologyEdges.GetTopologyVertices(curE)[1];
            temp.Add(curV);
            naked.Remove(curE);

            int face = this.m.TopologyEdges.GetConnectedFaces(curE, out bool[] orient)[0];
            MeshFace mf = this.m.Faces[face];
            Vector3d norm = Vector3d.CrossProduct(this.m.Vertices[mf.B] - this.m.Vertices[mf.A], this.m.Vertices[mf.B] - this.m.Vertices[mf.A]);
            if(orient[0]) { norm = -norm; }
            norms.Add(norm);

            while (naked.Count > 0)
            {
                //find next edge along loop
                if (!this.nextNakedEdge(ref curE, ref curV)) { Exceptions.badSurfacePathMesh(); }

                if(curE == firstE) // returned to start
                {
                    temp.Add(curV); // close loop
                    temp.Add(temp[0]);
                    bounds.Add(temp);
                    // start new loop
                    temp = new List<int>();
                    curE = naked.FirstOrDefault();
                    firstE = curE;
                    curV = this.m.TopologyEdges.GetTopologyVertices(curE)[0]; 
                    face = this.m.TopologyEdges.GetConnectedFaces(curE, out orient)[0];
                    mf = this.m.Faces[face];
                    norm = Vector3d.CrossProduct(this.m.Vertices[mf.B] - this.m.Vertices[mf.A], this.m.Vertices[mf.B] - this.m.Vertices[mf.A]);
                    if (!orient[0]) { norm = -norm; }
                    norms.Add(norm);
                }
                temp.Add(curV);
                naked.Remove(curE);
            }
            this.nextNakedEdge(ref curE, ref curV);
            if (curE == firstE) { temp.Add(temp[0]); } // close loop if back at start
            
            bounds.Add(temp);
            return bounds;
        }

        private bool nextNakedEdge(ref int curE, ref int curV)
        {
            Rhino.IndexPair verts = this.m.TopologyEdges.GetTopologyVertices(curE);
            curV = curV == verts[0] ? verts[1] : verts[0]; // set curV to new vertex
            int[] edges = this.m.TopologyVertices.ConnectedEdges(curV);
            int edC = edges.Count();
            for (int i = 0; i < edC; i++)
            {
                int edge = edges[i];
                if(edge != curE && this.m.TopologyEdges.GetConnectedFaces(edge).Count() == 1) 
                {
                    curE = edge;
                    return true;
                }
            }
            return false;

        }

        private bool startMeshStep(ref ToolPoint tp, Plane pl, Vector3d dir, out ToolPoint newTp)
        {
            newTp = new ToolPoint();
            bool found = false;
            // intersect pl with each edge of the face
            foreach (int edge in this.m.TopologyEdges.GetEdgesForFace(tp.meshface))
            {
                Line el = this.m.TopologyEdges.EdgeLine(edge);
                if(Intersection.LinePlane(el, pl, out double ip) && ip >= 0 && ip <= 1 && (el.PointAt(ip) - tp.pt) * dir > CAMel_Goo.Tolerance)
                {
                    newTp.pt = el.PointAt(ip);
                    int[] faces = this.m.TopologyEdges.GetConnectedFaces(edge);
                    if (faces.Count() > 1)
                    {
                        found = true;
                        if (faces[0] == tp.meshface) { newTp.meshface = faces[1]; }
                        else { newTp.meshface = faces[0]; }
                        newTp.setNorm(this.m);

                        break;
                    }
                }
            }
            if (!found) // in case the point is on an edge try neighboring faces
            {
                // find closest edge
                int joinEdge = -1;
                double dist = double.PositiveInfinity;

                foreach (int edge in this.m.TopologyEdges.GetEdgesForFace(tp.meshface))
                {
                    Line el = this.m.TopologyEdges.EdgeLine(edge);
                    double thisDist = el.DistanceTo(tp.pt, true);
                    if (thisDist < dist)
                    {
                        dist = thisDist;
                        joinEdge = edge;
                    }
                }
                int[] joinFaces = this.m.TopologyEdges.GetConnectedFaces(joinEdge);
                if (joinFaces.Length < 2) { return false; }
                int newFace = joinFaces[0];
                if (newFace == tp.meshface) { newFace = joinFaces[1]; }

                foreach (int edge in this.m.TopologyEdges.GetEdgesForFace(newFace))
                {
                    Line el = this.m.TopologyEdges.EdgeLine(edge);
                    if(Intersection.LinePlane(el, pl, out double ip) && ip >= 0 && ip <= 1 && (el.PointAt(ip) - tp.pt) * dir > CAMel_Goo.Tolerance)
                    {
                        newTp.pt = el.PointAt(ip);
                        int[] faces = this.m.TopologyEdges.GetConnectedFaces(edge);
                        if (faces.Count() > 1)
                        {
                            found = true;
                            if (faces[0] == newFace) { newTp.meshface = faces[1]; }
                            else { newTp.meshface = faces[0]; }
                            newTp.setNorm(this.m);
                            tp.meshface = newFace;

                            break;
                        }
                    }
                }
            }

            return found;
        }

        private bool meshStep(ref ToolPoint oldTp, ToolPoint tP, Plane pl, out ToolPoint newTp)
        {
            newTp = new ToolPoint();
            bool found = false;
            // intersect pl with each edge of the face
            foreach (int edge in this.m.TopologyEdges.GetEdgesForFace(tP.meshface))
            {
                int[] faces = this.m.TopologyEdges.GetConnectedFaces(edge);
                if ((faces[0] != oldTp.meshface && faces[0] != tP.meshface) ||
                    faces.Count() > 1 && faces[1] != oldTp.meshface && faces[1] != tP.meshface)
                {
                    Line el = this.m.TopologyEdges.EdgeLine(edge);
       
                    if (Intersection.LinePlane(el, pl, out double ip) && ip >=0 && ip<=1)
                    {
                        found = true;
                        oldTp = tP;
                        newTp.pt = el.PointAt(ip);

                        if (faces[0] == tP.meshface) { newTp.meshface = faces[1]; }
                        else { newTp.meshface = faces[0]; }
                        newTp.setNorm(this.m);
                        break;
                    }
                }
            }
            return found;
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
            if(this.Count == 0) { return null; }
            PolyCurve jc = new PolyCurve();
            for(int i=0;i<this.Count;i++)
            {
                Curve c = this[i].C.DuplicateCurve();
                if (c.IsClosed) { c = c.Trim(c.Domain.T0 + CAMel_Goo.Tolerance, c.Domain.T1); }
                jc.AppendSegment(c);
            }
            return jc;
        }

        #region List Functions

        /// <inheritdoc />
        public int Count => ((IList<SurfaceCurve>)this.paths).Count;
        /// <inheritdoc />
        public bool IsReadOnly => ((IList<SurfaceCurve>)this.paths).IsReadOnly;

        /// <inheritdoc />
        [CanBeNull]
        public SurfaceCurve this[int index]
        {
            get => this.paths[index];
            set => this.paths[index] = value;
        }

        /// <inheritdoc />
        public int IndexOf(SurfaceCurve item) => this.paths.IndexOf(item);
        /// <inheritdoc />
        public void Insert(int index, SurfaceCurve item) => this.paths.Insert(index, item);
        /// <inheritdoc />
        public void RemoveAt(int index) => this.paths.RemoveAt(index);
        /// <inheritdoc />
        public void Add(SurfaceCurve item) => this.paths.Add(item);
        public void Add(Curve item) => this.paths.Add(new SurfaceCurve(item, false));
        /// <summary>TODO The add range.</summary>
        /// <param name="items">TODO The items.</param>
        [PublicAPI]
        public void AddRange([NotNull] IEnumerable<SurfaceCurve> items) => this.paths.AddRange(items);
        /// <inheritdoc />
        public void Clear() => this.paths.Clear();
        /// <inheritdoc />
        public bool Contains(SurfaceCurve item) => this.paths.Contains(item);
        /// <inheritdoc />
        public void CopyTo(SurfaceCurve[] array, int arrayIndex) => this.paths.CopyTo(array, arrayIndex);
        /// <inheritdoc />
        public bool Remove(SurfaceCurve item) => ((IList<SurfaceCurve>)this.paths).Remove(item);
        /// <inheritdoc />
        public IEnumerator<SurfaceCurve> GetEnumerator() => this.paths.GetEnumerator();
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
                case 4:
                    return SurfToolDir.Material;
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
                foreach (SurfaceCurve c in this.Value) { bb.Union(c.C.GetBoundingBox(false)); }
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
            foreach (SurfaceCurve l in this.Value) { args.Pipeline.DrawCurve(l.C, args.Color); }
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