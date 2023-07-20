namespace CAMel.Types
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using CAMel.Types.Machine;
    using CAMel.Types.MaterialForm;

    using Grasshopper.Kernel;
    using Grasshopper.Kernel.Types;

    using JetBrains.Annotations;

    using Rhino.Geometry;
    using static Emgu.Util.Platform;

    /// <summary>TODO The path label.</summary>
    public enum PathLabel
    {
        /// <summary>TODO The unprocessed.</summary>
        Unprocessed,
        /// <summary>TODO The rough cut.</summary>
        RoughCut,
        /// <summary>TODO The finish cut.</summary>
        FinishCut,
        /// <summary>TODO The insert.</summary>
        Insert,
        /// <summary>TODO The transition.</summary>
        Transition,
        /// <summary>TODO The retract.</summary>
        Retract
    }

    public enum CutSide
    {
        /// <summary>Important cut to the left of tool</summary>
        Left,
        /// <summary>Important cut to the right of tool</summary>
        Right,
        /// <summary>Important cut on both sides of tool</summary>
        Both,
        /// <summary>Important cut on tip tool</summary>
        Tip,
        /// <summary>Important cut not defined</summary>
        Unclear
    }

    // One action of the machine, such as cutting a line
    /// <summary>TODO The tool path.</summary>
    public class ToolPath : IList<ToolPoint>, IToolPointContainer
    {
        /// <summary>The points in the toolpath.</summary>
        [ItemNotNull, NotNull] private List<ToolPoint> pts; // Positions of the machine
        /// <summary>Gets or sets the mat tool.</summary>
        public MaterialTool matTool { get; set; } // Material and tool to cut it with
        /// <summary>Gets or sets the mat form.</summary>
        public IMaterialForm matForm { get; set; } // Shape of the material
        /// <summary>Additions to the toolpath when it is processed ready for cutting.</summary>
        [NotNull] public ToolPathAdditions additions; // Features we might add to the path

        /// <summary>Gets the label for the role the toolpath plays</summary>
        public PathLabel label { get; internal set; }

        /// <summary>Gets the important cutting side.</summary>
        public CutSide side { get; internal set; }

        /// <inheritdoc />
        public ToolPoint firstP => this.Count > 0 ? this[0] : null;

        /// <inheritdoc />
        public ToolPoint lastP => this.Count > 0 ? this[this.Count - 1] : null;

        // Default Constructor, set everything to empty
        /// <summary>Initializes a new instance of the <see cref="ToolPath"/> class.</summary>
        public ToolPath()
        {
            this.name = string.Empty;
            this.label = PathLabel.Unprocessed;
            this.side = CutSide.Unclear;
            this.pts = new List<ToolPoint>();
            this.matTool = null;
            this.matForm = null;
            this.additions = ToolPathAdditions.temp;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // Just a MaterialTool
        /// <summary>Initializes a new instance of the <see cref="ToolPath"/> class.</summary>
        /// <param name="mT">TODO The m t.</param>
        public ToolPath([NotNull] MaterialTool mT)
        {
            this.name = string.Empty;
            this.label = PathLabel.Unprocessed;
            this.side = CutSide.Unclear;
            this.pts = new List<ToolPoint>();
            this.matTool = mT;
            this.matForm = null;
            this.additions = ToolPathAdditions.temp;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // Just a MaterialForm
        /// <summary>Initializes a new instance of the <see cref="ToolPath"/> class.</summary>
        /// <param name="mf">TODO The mf.</param>
        public ToolPath([NotNull] IMaterialForm mf)
        {
            this.name = string.Empty;
            this.label = PathLabel.Unprocessed;
            this.side = CutSide.Unclear;
            this.pts = new List<ToolPoint>();
            this.matTool = null;
            this.matForm = mf;
            this.additions = ToolPathAdditions.temp;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // MaterialTool and Form
        /// <summary>Initializes a new instance of the <see cref="ToolPath"/> class.</summary>
        /// <param name="name">TODO The name.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="mF">TODO The m f.</param>
        public ToolPath([NotNull] string name, [CanBeNull] MaterialTool mT, [CanBeNull] IMaterialForm mF)
        {
            this.name = name;
            this.label = PathLabel.Unprocessed;
            this.side = CutSide.Unclear;
            this.pts = new List<ToolPoint>();
            this.matTool = mT;
            this.matForm = mF;
            this.additions = ToolPathAdditions.temp;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // MaterialTool, Form and features
        /// <summary>Initializes a new instance of the <see cref="ToolPath"/> class.</summary>
        /// <param name="name">TODO The name.</param>
        /// <param name="mT">TODO The m t.</param>
        /// <param name="mF">TODO The m f.</param>
        /// <param name="tpa">TODO The tpa.</param>
        public ToolPath([NotNull] string name, [CanBeNull] MaterialTool mT, [CanBeNull] IMaterialForm mF, [NotNull] ToolPathAdditions tpa)
        {
            this.name = name;
            this.label = PathLabel.Unprocessed;
            this.side = CutSide.Unclear;
            this.pts = new List<ToolPoint>();
            this.matTool = mT;
            this.matForm = mF;
            this.additions = tpa;
            this.preCode = string.Empty;
            this.postCode = string.Empty;
        }

        // Copy Constructor
        /// <summary>Initializes a new instance of the <see cref="ToolPath"/> class.</summary>
        /// <param name="tP">TODO The t p.</param>
        private ToolPath([NotNull] ToolPath tP)
        {
            this.name = string.Copy(tP.name);
            this.label = tP.label;
            this.side = tP.side;
            this.pts = new List<ToolPoint>();
            foreach (ToolPoint pt in tP) { this.Add(pt?.deepClone()); }
            this.matTool = tP.matTool;
            this.matForm = tP.matForm;
            this.preCode = string.Copy(tP.preCode);
            this.postCode = string.Copy(tP.postCode);
            this.additions = tP.additions.deepClone();
        }
        /// <summary>Transform the toolpath in place, WARNING: for non rigid transforms will lose information beyond tool position and direction.</summary>
        /// <param name="transform">Transform to apply</param>
        public void transform(Transform transform) { foreach (ToolPoint tPt in this) { tPt.transform(transform); } }

        /// <summary>TODO The deep clone.</summary>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull, Pure]
        public ToolPath deepClone() => new ToolPath(this);

        // create a lifted path
        /// <summary>TODO The deep clone.</summary>
        /// <param name="h">TODO The h.</param>
        /// <param name="m">TODO The m.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull, Pure]
        public ToolPath deepClone(double h, [NotNull] IMachine m)
        {
            if (Math.Abs(h) < CAMel_Goo.Tolerance) { return this.deepClone(); }
            ToolPath tP = this.deepCloneWithNewPoints(new List<ToolPoint>());
            foreach (ToolPoint tPt in this)
            {
                ToolPoint newTPt = tPt.deepClone();
                newTPt.pt += h * m.toolDir(tPt);
                tP.Add(newTPt);
            }

            return tP;
        }

        /// <summary>TODO The deep clone with new points.</summary>
        /// <param name="newPts">TODO The new pts.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull, Pure]
        public ToolPath deepCloneWithNewPoints([CanBeNull] List<ToolPoint> newPts)
        {
            ToolPath newTP = new ToolPath
            {
                name = string.Copy(this.name),
                label = this.label,
                side = this.side,
                matTool = this.matTool,
                matForm = this.matForm,
                preCode = string.Copy(this.preCode),
                postCode = string.Copy(this.postCode),
                additions = this.additions.deepClone(),
                pts = newPts ?? new List<ToolPoint>()
            };
            return newTP;
        }

        // Copy in features from the valid ToolPath if this does not yet have its own.
        // TODO create an additions.Unset;
        /// <summary>TODO The validate.</summary>
        /// <param name="valid">TODO The valid.</param>
        /// <param name="m">TODO The m.</param>
        public void validate([NotNull] ToolPath valid, [NotNull] IMachine m)
        {
            this.matTool = this.matTool ?? valid.matTool;
            this.matForm = this.matForm ?? valid.matForm;
            this.additions.replace(m.defaultTPA);
        }

        /// <inheritdoc />
        public string TypeDescription => "An action of the machine, for example cutting a single line";
        /// <inheritdoc />
        public string TypeName => "ToolPath";

        /// <inheritdoc />
        public string name { get; set; }

        /// <inheritdoc />
        public string preCode { get; set; }
        /// <inheritdoc />
        public string postCode { get; set; }

        /// <summary>TODO The to string.</summary>
        /// <returns>The <see cref="string"/>.</returns>
        public override string ToString() => "ToolPath: " + this.name + " with " + this.Count + " points.";

        // Main functions

        // Process any additions to the path and return
        // list of list of toolpaths (for stepdown)
        /// <summary>TODO The process additions.</summary>
        /// <param name="m">TODO The m.</param>
        /// <param name="fP">TODO The f p.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull, Pure]
        public List<List<ToolPath>> processAdditions([NotNull] IMachine m, [CanBeNull] out List<ToolPath> fP)
        {
            if (this.matTool == null) { Exceptions.matToolException(); }
            if (this.matForm == null) { Exceptions.matFormException(); }

            ToolPath refined = m.refine(this);

            // offset Path
            List<ToolPath> useTP = m.offSet(refined);

            // adjust path for three axis (or index three axis)
            for (int i = 0; i < useTP.Count; i++)
            {
                if (useTP[i] == null) { Exceptions.nullPanic(); }
                useTP[i] = this.additions.threeAxisHeightOffset ? m.threeAxisHeightOffset(useTP[i]) : useTP[i];
            }

            // add steps into material
            List<List<ToolPath>> roughPaths = new List<List<ToolPath>>();
            if (Math.Abs(this.additions.stepDown) > CAMel_Goo.Tolerance)
            {
                foreach (List<List<ToolPath>> rPs in useTP.Select(m.stepDown))
                {
                    for (int i = 0; i < rPs.Count; i++)
                    {
                        if (roughPaths.Count <= i) { roughPaths.Add(new List<ToolPath>()); }
                        if (roughPaths[i] != null && rPs[i] != null) { roughPaths[i].AddRange(rPs[i]); }
                    }
                }
            }
            // TODO: Cleanup without insert/retract
            // add finishing paths, processing onion
            List<ToolPath> tempFp = new List<ToolPath>();
            foreach (ToolPath tP in useTP) { tempFp.AddRange(m.finishPaths(tP)); }

            // add insert and retract moves

            /*List<List<ToolPath>> newRp = new List<List<ToolPath>>();

            foreach (List<ToolPath> tPs in roughPaths)
            {
                if (tPs == null) { continue; }
                List<ToolPath> newTP = new List<ToolPath>();
                foreach (ToolPath tP in tPs.Where(tP => tP != null)) { newTP.AddRange(m.insertRetract(tP)); }
                newRp.Add(newTP);
            }*/

            // Delay insert and retract for toolpaths
            fP = tempFp.Where(tP => tP != null).ToList();

            //return newRp;

            return roughPaths;
        }

        /// <summary>Create Toolpath from a curve</summary>
        /// <param name="c">Curve for toolpath</param>
        /// <param name="d">Tool direction for toolpath</param>
        /// <param name="maxStep">Max step between toolpoints as multiple of toolwidth.</param>
        /// <returns>True on success, False on failure.<see cref="bool"/>.</returns>
        public bool convertCurve([CanBeNull] Curve c, Vector3d d, double maxStep = 20)
        {
            if (c?.IsValid != true) { return false; }
            if (this.matTool == null) { Exceptions.matToolException(); }

            Polyline pL;
            if (!c.TryGetPolyline(out pL))
            {
                Curve c2 = c.ToPolyline(
                    0, 0, Math.PI, 0, 0, this.matTool.tolerance, this.matTool.minStep,
                    maxStep * this.matTool.toolWidth, true);
                if (c2 == null) { return false; }
                c2.TryGetPolyline(out pL);
            }

            this.pts = new List<ToolPoint>();

            // Add the points to the Path
            foreach (ToolPoint tPt in pL.Select(pt => new ToolPoint(pt, d))) { this.Add(tPt); }

            // ReSharper disable once InvertIf
            //if (c.IsClosed)
            //{
            //    ToolPoint tPt = new ToolPoint(pL[0], d);
            //    this.Add(tPt);
            //}

            return true;
        }

        public double simplify(double tolerance = 1)
        {
            if (this.matTool == null) { Exceptions.matToolException(); }

            int ptCount = this.Count;
            double uTolerance = this.matTool.tolerance * tolerance;

            IList<ToolPoint> pts = this.pts;

            pts = SimplifyCSharp.SimplificationHelpers.Simplify<ToolPoint>(
                pts,
                (p1, p2) => p1 == p2,
                (p) => p.pt.X,
                (p) => p.pt.Y,
                (p) => p.pt.Z,
                (p) => p.dir.X,
                (p) => p.dir.Y,
                (p) => p.dir.Z,
                uTolerance,
                true
                );
            this.pts = new List<ToolPoint>(pts);
            return (double)this.Count / (double)ptCount;
        }

        /// <summary>TODO The acc tol.</summary>
        private const double AccTol = 0.0001;
        /// <summary>TODO The convert accurate.</summary>
        /// <param name="c">TODO The c.</param>
        /// <returns>The <see cref="PolylineCurve"/>.</returns>
        [NotNull, PublicAPI]
        public static PolylineCurve convertAccurate([NotNull] Curve c)
        {
            // Check if already a polyline, otherwise make one
            PolylineCurve plC =
                c.TryGetPolyline(out Polyline p)
                    ? new PolylineCurve(p)
                    : c.ToPolyline(0, 0, Math.PI, 0, 0, AccTol * 5.0, 0, 0, true);

            return plC ?? new PolylineCurve();
        }

        /// <summary>TODO The to path.</summary>
        /// <param name="scraps">TODO The scraps.</param>
        /// <returns>The <see cref="ToolPath"/>.</returns>
        [NotNull]
        internal static ToolPath toPath([CanBeNull] object scraps)
        {
            ToolPath oP = new ToolPath();
            switch (scraps) {
                case IToolPointContainer container:
                    oP.AddRange(container.getSinglePath());
                    break;
                case Point3d pt:
                    oP.Add(pt);
                    break;
                case IEnumerable li:
                    {
                        foreach (object oB in li)
                        {
                            switch (oB) {
                                case IToolPointContainer tPc:
                                    oP.AddRange(tPc.getSinglePath());
                                    break;
                                case Point3d pti:
                                    oP.Add(pti);
                                    break;
                            }
                        }

                        break;
                    }
            }

            return oP;
        }

        /// <summary>Creates new toolpath from longest component without self intersection</summary>
        /// <param name="tP">ToolPath to clean</param>
        /// <param name="dir">Plane to project to and clean on</param>
        /// <param name="toolDir">Tool direction to use in finished closed path (note tooldirection in original path will be lost for closed paths)</param>
        /// <returns>Offset path</returns>
        public static ToolPath Clean(ToolPath tP, Vector3d dir, Vector3d toolDir)
        {
            // if the path is open just leave it
            // What to do with self-intersecting cloased paths? 
            if (!tP.isClosed()) { return tP; }

            PolylineCurve uC = tP.getLine();

            // Shift curve to XY plane
            Plane p = new Plane(Point3d.Origin, dir);
            uC.Transform(Transform.PlaneToPlane(p, Plane.WorldXY));

            // record the average Z location of the curve
            BoundingBox bb = uC.GetBoundingBox(true);
            double useZ = (bb.Max.Z + bb.Min.Z) / 2.0;

            // clean the curve
            uC = Offsetting.CleanToLongest(uC);
            // return to original orientation
            uC.Translate(new Vector3d(0, 0, useZ));
            uC.Transform(Transform.PlaneToPlane(Plane.WorldXY, p));

            ToolPath clTp = tP.deepCloneWithNewPoints(new List<ToolPoint>());
            clTp.convertCurve(uC, toolDir);


            return clTp;

        }

        /// <summary>Projects a ToolPath to a plane and offsets on the plane</summary>
        /// <param name="tP">ToolPath (might have unexpected results with self-intersecting path)</param>
        /// <param name="dir">Plane to offset on</param>
        /// <param name="toolDir">Tool direction to use in finished closed path (note tooldirection in original path will be lost for closed paths)</param>
        /// <returns>Offset path</returns>
        public static List<ToolPath> planeOffset(ToolPath tP, Vector3d dir, Vector3d toolDir, bool offsetOut = false)
        {
            // if offset is zero length no work to do (longest is ignored as do not have a plane to offset on)
            if (dir.Length < CAMel_Goo.Tolerance) { return new List<ToolPath> { tP }; }

            // if the path is open localOffset will do well enough
            if (!tP.isClosed()) { return localOffset(tP, dir); }

            // Shift curve to XY plane
            double uOS = dir.Length;
            Plane p = new Plane(Point3d.Origin, dir);

            PolylineCurve uC = tP.getLine();

            uC.Transform(Transform.PlaneToPlane(p, Plane.WorldXY));
            bool reversed = false;

            // ensure the curve is anticlockwise for inward offsetting or clocwise for outward
            // note this will just ignore selfintersecting curves
            CurveOrientation cO = uC.ClosedCurveOrientation(Transform.Identity);
            if (cO == CurveOrientation.Clockwise && !offsetOut || cO == CurveOrientation.CounterClockwise && offsetOut)
            {
                uC.Reverse();
                reversed = true;
                uOS = -uOS;
            }

            // record the average Z location of the curve
            BoundingBox bb = uC.GetBoundingBox(true);
            double useZ = (bb.Max.Z + bb.Min.Z) / 2.0;

            // offSet
            List<PolylineCurve> osC = Offsetting.Offset(uC, uOS);

            if (reversed) { foreach (PolylineCurve osPl in osC) { osPl.Reverse(); } }

            // create Operation
            List<ToolPath> tPs = new List<ToolPath>();

            int i = 1;
            foreach (PolylineCurve osPl in osC)
            {
                // Create and add name, material/tool and material form
                ToolPath osTP = tP.deepCloneWithNewPoints(new List<ToolPoint>());

                // return to original orientation
                osPl.Translate(new Vector3d(0, 0, useZ));
                osPl.Transform(Transform.PlaneToPlane(Plane.WorldXY, p));

                // Add to Operation
                osTP.convertCurve(osPl, toolDir);
                tPs.Add(osTP);
            }

            return tPs;
        }

        /// <summary>Offset a path locally</summary>
        /// <param name="tP">ToolPath to offset</param>
        /// <returns>Offset paths.</returns>
        [NotNull]
        public static List<ToolPath> localOffset([NotNull] ToolPath tP, Vector3d dir)
        {
            List<ToolPoint> oTPts = new List<ToolPoint>();
            double dirL = dir.Length;
            Vector3d uDir = dir;
            uDir.Unitize();

            // Check if there is enough to offset
            if (dirL < CAMel_Goo.Tolerance || tP.Count < 2 || tP.firstP == null || tP.lastP == null) { return new List<ToolPath> { tP }; }

            // Start with first point unless the ToolPath is closed.
            ToolPoint lPt = tP.firstP, uTPt;
            if (tP.firstP.pt.DistanceTo(tP.lastP.pt) < CAMel_Goo.Tolerance) { lPt = tP[tP.Count - 2]; }
            Vector3d osD;

            for (int i = 0; i < tP.Count - 1; i++)
            {
                uTPt = tP[i].deepClone();

                // offset direction given by tangent and offset Plane
                osD = Vector3d.CrossProduct(uDir, tP[i + 1].pt - lPt.pt);
                osD.Unitize();
                uTPt.pt += dirL * osD;
                oTPts.Add(uTPt);
                lPt = tP[i];
            }

            // Loop back to start if closed.
            ToolPoint nP = tP.lastP;
            if (tP.firstP.pt.DistanceTo(tP.lastP.pt) < CAMel_Goo.Tolerance) { nP = tP[2]; }

            uTPt = tP[tP.Count - 1].deepClone();
            osD = Vector3d.CrossProduct(uDir, nP.pt - lPt.pt);
            osD.Unitize();
            uTPt.pt += dirL * osD;
            oTPts.Add(uTPt);

            ToolPath oTp = tP.deepCloneWithNewPoints(oTPts);

            return new List<ToolPath> { oTp };
        }

        #region Point extraction and previews

        /// <inheritdoc />
        public ToolPath getSinglePath() => this.deepClone();

        // Get the list of tooltip locations
        /// <summary>TODO The get points.</summary>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull, Pure]
        public List<Point3d> getPoints()
        {
            // ReSharper disable once PossibleNullReferenceException
            return this.Select(tP => tP.pt).ToList();
        }

        // Get the list of tool directions
        /// <summary>TODO The get dirs.</summary>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull, Pure]
        public List<Vector3d> getDirs()
        {
            return this.Select(tP => tP.dir).ToList();
        }

        // Create a path with the points
        /// <summary>TODO The get points and dirs.</summary>
        /// <param name="dirs">TODO The dirs.</param>
        /// <returns>The <see>
        ///         <cref>List</cref>
        ///     </see>
        /// .</returns>
        [NotNull, Pure]
        public List<Point3d> getPointsAndDirs([CanBeNull] out List<Vector3d> dirs)
        {
            List<Point3d> ptsOut = new List<Point3d>();
            dirs = new List<Vector3d>();
            foreach (ToolPoint tPt in this)
            {
                ptsOut.Add(tPt.pt);
                dirs.Add(tPt.dir);
            }

            return ptsOut;
        }

        // Get the list of speeds and feeds (a vector with speed in X and feed in Y)
        /// <summary>TODO The get speed feed.</summary>
        /// <returns>The <see cref="IEnumerable"/>.</returns>
        [NotNull, Pure]
        public IEnumerable<Vector3d> getSpeedFeed()
        {
            return this.Select(tP => new Vector3d(tP.speed, tP.feed, 0)).ToList();
        }

        // Bounding Box for previews
        /// <inheritdoc />
        [Pure]
        public BoundingBox getBoundingBox()
        {
            BoundingBox bb = BoundingBox.Unset;
            for (int i = 0; i < this.Count; i++)
            { bb.Union(this[i].getBoundingBox()); }
            return bb;
        }

        /// <summary>TODO The get line.</summary>
        /// <returns>The <see cref="PolylineCurve"/>.</returns>
        [NotNull, Pure]
        public PolylineCurve getLine() => new PolylineCurve(this.getPoints());

        // Lines for each toolpoint
        /// <summary>TODO The tool lines.</summary>
        /// <returns>The <see cref="IEnumerable"/>.</returns>
        [NotNull, Pure]
        public IEnumerable<Line> toolLines()
        {
            return this.Select(tP => tP.toolLine()).ToList();
        }

        /// <summary>Check if path can be offset on a plane</summary>
        /// <param name="dir">Tool Direction</param>
        /// <returns>True if path can be offset.</returns>
        [Pure]
        public bool planarOffset(out Vector3d dir)
        {
            if (this.Count == 0)
            {
                dir = new Vector3d();
                return false;
            }

            dir = this[0].dir;

            // Check that there is a unique directions
            foreach (ToolPoint tPt in this)
            { if ((dir - tPt.dir).SquareLength > CAMel_Goo.Tolerance) { return false; } }

            // Check for planarity
            return this.getLine().IsPlanar();
        }

        /// <summary>TODO The is closed.</summary>
        /// <returns>The <see cref="bool"/>.</returns>
        [Pure]
        public bool isClosed()
        {
            if (this.firstP == null || this.lastP == null) { return false; }
            return this.firstP.pt.DistanceTo(this.lastP.pt) < CAMel_Goo.Tolerance;
        }

        #endregion

        #region List Functions

        /// <inheritdoc />
        public int Count => this.pts.Count;
        /// <inheritdoc />
        public bool IsReadOnly => ((IList<ToolPoint>)this.pts).IsReadOnly;

        /// <inheritdoc />
        [NotNull]
        public ToolPoint this[int index] { get => this.pts[index]; set => this.pts[index] = value; }

        /// <inheritdoc />
        public int IndexOf(ToolPoint item) => this.pts.IndexOf(item);
        /// <inheritdoc />
        public void Insert(int index, ToolPoint item)
        {
            if (item != null) { this.pts.Insert(index, item); }
        }

        /// <summary>TODO The insert range.</summary>
        /// <param name="index">TODO The index.</param>
        /// <param name="items">TODO The items.</param>
        [UsedImplicitly]
        public void InsertRange(int index, [NotNull] IEnumerable<ToolPoint> items) =>
            this.pts.InsertRange(index, items.Where(x => x != null));
        /// <inheritdoc />
        public void RemoveAt(int index) => this.pts.RemoveAt(index);
        /// <summary>TODO The remove last.</summary>
        public void removeLast() { this.pts.RemoveAt(this.Count - 1); }
        /// <inheritdoc />
        public void Add(ToolPoint item)
        {
            if (item != null) { this.pts.Add(item); }
        }

        /// <summary>TODO The add.</summary>
        /// <param name="item">TODO The item.</param>
        [PublicAPI]
        public void Add(Point3d item) => this.pts.Add(new ToolPoint(item));
        /// <summary>TODO The add range.</summary>
        /// <param name="items">TODO The items.</param>
        public void AddRange([NotNull] IEnumerable<ToolPoint> items) => this.pts.AddRange(items.Where(x => x != null));
        /// <summary>TODO The add range.</summary>
        /// <param name="items">TODO The items.</param>
        public void AddRange([NotNull] IEnumerable<Point3d> items)
        {
            foreach (Point3d pt in items) { this.Add(pt); }
        }

        /// <inheritdoc />
        public void Clear() => this.pts.Clear();
        /// <inheritdoc />
        public bool Contains(ToolPoint item) => this.pts.Contains(item);
        /// <inheritdoc />
        public void CopyTo(ToolPoint[] array, int arrayIndex) => this.pts.CopyTo(array, arrayIndex);
        /// <inheritdoc />
        public bool Remove(ToolPoint item) => this.pts.Remove(item);
        /// <inheritdoc />
        public IEnumerator<ToolPoint> GetEnumerator() => this.pts.GetEnumerator();
        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => this.pts.GetEnumerator();

        #endregion
    }

    // Grasshopper Type Wrapper
    /// <summary>TODO The g h_ tool path.</summary>
    public sealed class GH_ToolPath : CAMel_Goo<ToolPath>, IGH_PreviewData
    {
        // Default Constructor
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPath"/> class.</summary>
        [UsedImplicitly]
        public GH_ToolPath() => this.Value = new ToolPath();

        // Create from unwrapped version
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPath"/> class.</summary>
        /// <param name="tP">TODO The t p.</param>
        public GH_ToolPath([CanBeNull] ToolPath tP) => this.Value = tP;

        // Copy Constructor
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPath"/> class.</summary>
        /// <param name="tP">TODO The t p.</param>
        public GH_ToolPath([CanBeNull] GH_ToolPath tP) => this.Value = tP?.Value?.deepClone();

        /// <inheritdoc />
        [NotNull]
        public override IGH_Goo Duplicate() => new GH_ToolPath(this);

        /// <inheritdoc />
        public override bool CastTo<T>(ref T target)
        {
            if (this.Value == null) { return false; }

            // Cast from unwrapped ToolPath
            if (typeof(T).IsAssignableFrom(typeof(ToolPath)))
            {
                target = (T)(object)this.Value;
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(MachineOperation)))
            {
                target = (T)(object)new MachineOperation(this.Value);
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(GH_MachineOperation)))
            {
                target = (T)(object)new GH_MachineOperation(new MachineOperation(this.Value));
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(Curve)))
            {
                target = (T)(object)this.Value.getLine();
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(GH_Curve)))
            {
                target = (T)(object)new GH_Curve(this.Value.getLine());
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(IMaterialForm)))
            {
                target = (T)this.Value.matForm;
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(GH_MaterialForm)))
            {
                target = (T)(object)new GH_MaterialForm(this.Value.matForm);
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(MaterialTool)))
            {
                target = (T)(object)this.Value.matTool;
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(GH_MaterialTool)))
            {
                target = (T)(object)new GH_MaterialTool(this.Value.matTool);
                return true;
            }

            if (typeof(T).IsAssignableFrom(typeof(ToolPathAdditions)))
            {
                target = (T)(object)this.Value.additions;
                return true;
            }
            // ReSharper disable once InvertIf
            if (typeof(T).IsAssignableFrom(typeof(GH_ToolPathAdditions)))
            {
                target = (T)(object)new GH_ToolPathAdditions(this.Value.additions);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public override bool CastFrom([CanBeNull] object source)
        {
            switch (source)
            {
                case null: return false;
                case ToolPath sTP:
                    this.Value = sTP;
                    return true;
                case Curve curve:
                    {
                        if (!curve.TryGetPolyline(out Polyline pl)) { return false; }
                        ToolPath tP = new ToolPath();
                        tP.AddRange(pl);
                        this.Value = tP;
                        return true;
                    }

                case GH_Curve ghCurve:
                    {
                        if (ghCurve.Value == null || !ghCurve.Value.TryGetPolyline(out Polyline pl)) { return false; }
                        ToolPath tP = new ToolPath();
                        tP.AddRange(pl);
                        this.Value = tP;
                        return true;
                    }

                default: return false;
            }
        }

        /// <inheritdoc />
        public BoundingBox ClippingBox => this.Value?.getBoundingBox() ?? BoundingBox.Unset;

        /// <inheritdoc />
        public void DrawViewportWires([CanBeNull] GH_PreviewWireArgs args)
        {
            if (this.Value == null || args?.Pipeline == null) { return; }
            args.Pipeline.DrawCurve(this.Value.getLine(), args.Color);
            args.Pipeline.DrawArrows(this.Value.toolLines(), args.Color);
        }

        /// <inheritdoc />
        public void DrawViewportMeshes([CanBeNull] GH_PreviewMeshArgs args) { }
    }

    // Grasshopper Parameter Wrapper
    /// <summary>TODO The g h_ tool path par.</summary>
    public class GH_ToolPathPar : GH_Param<GH_ToolPath>, IGH_PreviewObject
    {
        /// <summary>Initializes a new instance of the <see cref="GH_ToolPathPar"/> class.</summary>
        public GH_ToolPathPar()
            : base(
                "ToolPath", "ToolPath",
                "Contains a collection of Tool Paths",
                "CAMel", "  Params", GH_ParamAccess.item) { }

        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("4ea6da38-c19f-43e7-85d4-ada4716c06ac");

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
        protected override System.Drawing.Bitmap Icon => Properties.Resources.toolpath;
    }
}