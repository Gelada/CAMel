using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;

namespace CAMel.Types
{
    // The type of projection. 
    // Parallel is along a single direction
    // Cylindrical points towards a path along a direction
    // Spherical points towards a point

    public enum SurfProj {
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
    public enum SurfToolDir {
        Projection,
        PathTangent,
        PathNormal,
        Normal
    }

    public enum surfaceType {
        Brep,
        Mesh
    }

    // A path that will project to a surface for surfacing
    // TODO write a subclass for each projection
    public class SurfacePath : ICAMel_Base
    {
        public List<Curve> Paths; // Curves to project
        public SurfProj SP; // Type of projection
        public Curve CylOnto; // centre line for Cylindrical projection
        public Vector3d dir; // direction for parallel projection, or line direction for cylindrical
        public Point3d Cen; // centre for spherical projection
        public SurfToolDir STD; // method to calculate tool direction

        // private storage when processing a model

        private surfaceType ST; // Type of surface being processed
        private Mesh M; // Mesh
        private Brep B; // Brep

        // Default Constructor
        public SurfacePath()
        {
            this.Paths = new List<Curve>();
        }

        // Parallel constructor
        public SurfacePath(List<Curve> Paths, Vector3d dir, SurfToolDir STD)
        {

            this.Paths = Paths;
            this.SP = SurfProj.Parallel;
            this.dir = dir;
            this.STD = STD;
        }
        // Cylindrical constructor
        public SurfacePath(List<Curve> Paths, Vector3d dir, Curve CC, SurfToolDir STD)
        {
            this.Paths = Paths;
            this.SP = SurfProj.Cylindrical;
            this.dir = dir;
            this.CylOnto = CC;
            this.STD = STD;
        }
        // Spherical constructor
        public SurfacePath(List<Curve> Paths, Point3d Cen, SurfToolDir STD)
        {
            this.Paths = Paths;
            this.SP = SurfProj.Spherical;
            this.Cen = Cen;
            this.STD = STD;
        }
        // Copy Constructor
        public SurfacePath(SurfacePath Os)
        {
            this.Paths = Os.Paths;
            this.SP = Os.SP;
            this.CylOnto = Os.CylOnto;
            this.dir = Os.dir;
            this.Cen = Os.Cen;
            this.STD = Os.STD;
        }
        // Duplicate
        public SurfacePath Duplicate()
        {
            return new SurfacePath(this);
        }


        public string TypeDescription
        {
            get { return "Path and projection information to generate a surfacing path"; }
        }

        public string TypeName
        {
            get { return "SurfacePath"; }
        }

        public bool IsValid
        {
            get
            {
                throw new NotImplementedException("SurfacePath has not implemented IsValid");
            }
        }

        public override string ToString()
        {
            string op= "Surfacing:";
            switch (this.SP)
            {
                case SurfProj.Parallel:
                    op = op + " Parallel Projection";
                    break;
                case SurfProj.Cylindrical:
                    op = op + " Cylindrical Projection";
                    break;
                case SurfProj.Spherical:
                    op = op + " Cylindrical Projection";
                    break;
                default:
                    break;
            }
            return op;
        }

        // Different calls to Generate a Machine Operation from different surfaces
        public MachineOperation GenerateOperation(Surface S, double offset, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            this.ST = surfaceType.Brep;
            this.B = S.ToBrep();

            return this.GenerateOperation_(offset, MT, MF, TPA);
        }

        public MachineOperation GenerateOperation(Brep B, double offset, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            // Mesh is so much faster
            this.ST = surfaceType.Mesh;
            this.B = B;
            MeshingParameters mP = MeshingParameters.Smooth;
            this.M = Mesh.CreateFromBrep(B, mP)[0];

            return this.GenerateOperation_(offset, MT, MF, TPA);
        }

        public MachineOperation GenerateOperation(Mesh M, double offset, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            this.ST = surfaceType.Mesh;
            this.M = M;
            this.M.FaceNormals.ComputeFaceNormals();

            return this.GenerateOperation_(offset, MT, MF, TPA);
        }

        // actual code to generate the operation
        private MachineOperation GenerateOperation_(double offset, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            // create unprojected toolpath (mainly to convert the curve into a list of points)
            List<ToolPath> TPs = new List<ToolPath>();

            foreach(Curve P in this.Paths)
            {
                TPs.Add(new ToolPath("", MT, MF, TPA));
                TPs[TPs.Count - 1].ConvertCurve(P,new Vector3d(0,0,1));
            }

            // move points onto surface storing projection direction
            // on the toolpoint and keeping lists of norms
            List<ToolPath> newTPs = new List<ToolPath>();
            List<List<Vector3d>> Norms = new List<List<Vector3d>>();
            List<Vector3d> tempN;

            ToolPath tempTP;


            foreach(ToolPath TP in TPs)
            {
                var intersectInfo = new ConcurrentDictionary<ToolPoint, firstIntersectResponse>(Environment.ProcessorCount, TP.Pts.Count);

                foreach (ToolPoint TPt in TP.Pts) //initialise dictionary
                    intersectInfo[TPt] = new firstIntersectResponse();

                Parallel.ForEach(TP.Pts, TPtP =>
                 {
                     intersectInfo[TPtP] = firstIntersect(TPtP);
                 }
                );
                
                tempTP = new ToolPath("", MT, MF, TPA);
                tempN = new List<Vector3d>();
                firstIntersectResponse fIR;

                foreach( ToolPoint TPt in TP.Pts)
                {

                    fIR = intersectInfo[TPt];
                    if (fIR.hit)
                    {
                        tempTP.Pts.Add(fIR.TP);
                        tempN.Add(fIR.Norm); 
                    }
                    else if(tempTP.Pts.Count > 0 )
                    {
                        if (tempTP.Pts.Count > 1)
                        {
                            newTPs.Add(tempTP);
                            Norms.Add(tempN);
                        }
                        tempTP = new ToolPath("", MT, MF, TPA);
                        tempN = new List<Vector3d>();
                    }
                }
                if (tempTP.Pts.Count > 1)
                {
                    newTPs.Add(tempTP);
                    Norms.Add(tempN);
                }
            }

            Vector3d tangent, PTplaneN, STNorm, PNplaneN;
 
            for(int j=0;j<newTPs.Count;j++)
            {
                for (int i = 0; i < newTPs[j].Pts.Count; i++)
                {
                    // find the tangent vector, assume the curve is not too jagged
                    // this is reasonable for most paths, but not for space 
                    // filling curve styles. Though for those this is a bad option.
                    // For the moment we will look 2 points back and one point forward.
                    // Some cases to deal with the start, end and short paths. 
                    // TODO Smooth this out by taking into account individual tangencies?
                    int lookback, lookforward;
                    if (i == newTPs[j].Pts.Count - 1)
                    {
                        lookback = 3;
                        if( i < lookback) { lookback = newTPs[j].Pts.Count - 1; }
                        lookforward = 0;
                    }
                    else
                    {
                        lookback = Math.Min(i, 2);
                        lookforward = 3-lookback;
                        if(lookforward + i >= newTPs[j].Pts.Count) { lookforward = newTPs[j].Pts.Count - i - 1; }
                    }

                    tangent = newTPs[j].Pts[i+lookforward].Pt - newTPs[j].Pts[i - lookback].Pt;
                    switch (this.STD)
                    {
                        case SurfToolDir.Projection: // already set
                            break; 
                        case SurfToolDir.PathNormal:
                            // get normal to tangent on surface
                            STNorm = Vector3d.CrossProduct(Norms[j][i], tangent);
                            PNplaneN = Vector3d.CrossProduct(newTPs[j].Pts[i].Dir, STNorm);
                            // find vector normal to the surface in the line orthogonal to the tangent
                            newTPs[j].Pts[i].Dir = Vector3d.CrossProduct(STNorm,PNplaneN);
                            break;
                        case SurfToolDir.PathTangent:
                            // get normal to proj and tangent
                            PTplaneN = Vector3d.CrossProduct(newTPs[j].Pts[i].Dir, tangent);
                            PNplaneN = newTPs[j].Pts[i].Dir;
                            // find vector normal to tangent and in the plane of tangent and projection
                            newTPs[j].Pts[i].Dir = Vector3d.CrossProduct(tangent,PTplaneN);
                            if (Math.Abs(newTPs[j].Pts[i].Dir.Y) > .01)
                            {
                                int u = 17;
                            }
                            break;
                        case SurfToolDir.Normal: // set to negative Norm as we use the direction
                                                 // from pivot to tool
                            newTPs[j].Pts[i].Dir = -Norms[j][i]; 
                            break;
                    }
                    // Adjust the tool position based on the surface normal and the tool orientation
                    // so that the cutting surface not the tooltip is at the correct point

                    newTPs[j].Pts[i].Pt = newTPs[j].Pts[i].Pt + MT.CutOffset(newTPs[j].Pts[i].Dir,Norms[j][i]);

                    // Move to offset using normal

                    newTPs[j].Pts[i].Pt = newTPs[j].Pts[i].Pt + offset*Norms[j][i];
                }
            }

            // make the machine operation
            MachineOperation MO = new MachineOperation(this.ToString());
            MO.TPs = newTPs;
            return MO;
        }

        private struct firstIntersectResponse
        {
            public ToolPoint TP { get; set; }
            public Vector3d Norm { get; set; }
            public bool hit { get; set; }

            public override string ToString()
            {
                return this.hit.ToString();
            }
        }

        private firstIntersectResponse firstIntersect(ToolPoint TP)
        {
            firstIntersectResponse fIR = new firstIntersectResponse();
            Vector3d Norm = new Vector3d();
   
            Vector3d proj = this.ProjDir(TP.Pt);
            Ray3d RayL = new Ray3d(TP.Pt, proj);

            fIR.hit = false;
            switch (this.ST)
            {
                case surfaceType.Brep:
                    List<Brep> LB = new List<Brep>();
                    LB.Add(this.B);
                    Point3d[] interP = Intersection.RayShoot(RayL, LB,1);
                    List<Point3d> LinterP = new List<Point3d>();
                    if(interP != null) {LinterP.AddRange(interP);}
                    if( LinterP.Count > 0)
                    {
                        fIR.hit = true;
                        fIR.TP = new ToolPoint(interP[0],proj);
                        Point3d cp = new Point3d(); ComponentIndex ci; double s, t; // catching info we won't use;
                        // call closestpoint to find norm
                        B.ClosestPoint(fIR.TP.Pt, out cp, out ci, out s, out t, 0.5, out Norm);
                        fIR.Norm = Norm;
                    }
                    break;
                case surfaceType.Mesh:
                    
                    int[] faces;
                    double inter = Intersection.MeshRay(this.M, RayL, out faces);
                    if (inter>=0)
                    {
                        fIR.hit = true;
                        fIR.TP= new ToolPoint(RayL.PointAt(inter),proj);
                        List<int> Lfaces = new List<int>();
                        Lfaces.AddRange(faces);
                        fIR.Norm = (Vector3d)this.M.FaceNormals[Lfaces[0]];

                        if (fIR.Norm * RayL.Direction > 0) { fIR.Norm = -fIR.Norm; }
                    }
                    break;
            }
            return fIR;
        }
        // Give the direction of projection for a specific point based on the projection type.
        private Vector3d ProjDir(Point3d Pt)
        {
            Vector3d pd = new Vector3d();
            switch (this.SP)
            {
                case SurfProj.Parallel:
                    pd = this.dir;
                    break;
                case SurfProj.Cylindrical:
                    Plane Pl = new Plane(Pt,this.dir);
                    
                    if(this.CylOnto.IsLinear()) // if centre is a line treat it as infinite
                    {
                        double lp;
                        Line cyline = new Line(this.CylOnto.PointAtStart, this.CylOnto.PointAtEnd);
                        if(Intersection.LinePlane(cyline, Pl, out lp))
                        {
                            pd = cyline.PointAt(lp)-Pt;
                        } else
                        {
                            throw new System.ArgumentOutOfRangeException("Cylinder Parallel","The projection direction is parallel to cyliner centre.");
                        }
                    } else // Use curve and warn if no intersection
                    {
                        CurveIntersections CI = Intersection.CurvePlane(this.CylOnto,Pl,0.0000001);
                        if(CI.Count == 0)
                        {
                            throw new System.ArgumentOutOfRangeException("Short Cylinder", "The cylinder centre curve is shorter than the model."); 
                        } else 
                        {
                            if(CI.Count >1 || CI[0].IsOverlap)
                            {

                                throw new System.ArgumentOutOfRangeException("Cylinder double cut", "The cylinder centre curve has multiple intersections with a projection plane.");
                            }
                            pd = CI[0].PointA - Pt;
                        }
                    }
                    break;
                case SurfProj.Spherical:
                    pd = this.Cen-Pt;
                    break;
                default:
                    break;
            }
            return pd;
        }

        ICAMel_Base ICAMel_Base.Duplicate()
        {
            throw new NotImplementedException("Surface Path has not implemented Duplicate");
        }
    }

    // Grasshopper Type Wrapper
    public class GH_SurfacePath : CAMel_Goo<SurfacePath>
    {
        // Default Constructor
        public GH_SurfacePath()
        {
            this.Value = new SurfacePath();
        }

        // Default Constructor
        public GH_SurfacePath(SurfacePath SP)
        {
            this.Value = SP;
        }

        // Copy Constructor.
        public GH_SurfacePath(GH_SurfacePath Op)
        {
            this.Value = new SurfacePath(Op.Value);
        }
        // Duplicate
        public override IGH_Goo Duplicate()
        {
            return new GH_SurfacePath(this);
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(SurfacePath)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }

            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null)
            {
                return false;
            }
            if (source is SurfacePath)
            {
                this.Value = new SurfacePath((SurfacePath)source);
                return true;
            }
            return false;
        }
    }

    // Grasshopper Parameter Wrapper
    public class GH_SurfacePathPar : GH_Param<GH_SurfacePath>
    {
        public GH_SurfacePathPar() :
            base("Surfacing Path", "SurfacePath", "Contains the information to project a path onto a surface", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("FCB36AFC-195B-4DFA-825B-A986875A3A86"); }
        }
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return Properties.Resources.surfacepath;
            }
        }
    }

}