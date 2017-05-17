using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

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
    public class SurfacePath : CA_base
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


        public override string TypeDescription
        {
            get { return "Path and projection information to generate a surfacing path"; }
        }

        public override string TypeName
        {
            get { return "SurfacePath"; }
        }

        public override string ToString()
        {
            return "Surfacing Path";
        }

        // Different calls to Generate a Machine Operation from different surfaces
        public MachineOperation GenerateOperation(Surface S, double offset, MaterialTool MT,MaterialForm MF, ToolPathAdditions TPA)
        {
            this.ST = surfaceType.Brep;
            this.B = S.ToBrep();

            return this.GenerateOperation_(offset, MT, MF, TPA);
        }

        public MachineOperation GenerateOperation(Brep B, double offset, MaterialTool MT, MaterialForm MF, ToolPathAdditions TPA)
        {
            this.ST = surfaceType.Brep;
            this.B = B;

            return this.GenerateOperation_(offset, MT, MF, TPA);
        }

        public MachineOperation GenerateOperation(Mesh M, double offset, MaterialTool MT, MaterialForm MF, ToolPathAdditions TPA)
        {
            this.ST = surfaceType.Mesh;
            this.M = M;

            return this.GenerateOperation_(offset, MT, MF, TPA);
        }

        // actual code to generate the operation
        private MachineOperation GenerateOperation_(double offset, MaterialTool MT, MaterialForm MF, ToolPathAdditions TPA)
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
            Vector3d proj;
            Line Ray;
            Vector3d Norm = new Vector3d(0, 0, 0);
            ToolPoint outPt;
            ToolPath tempTP;
            bool hit;

            foreach(ToolPath TP in TPs)
            {
                tempTP = new ToolPath("", MT, MF, TPA);
                tempN = new List<Vector3d>();
                for(int i=0;i<TP.Pts.Count;i++)
                {
                    proj = ProjDir(TP.Pts[i].Pt);
                    Ray = new Line(TP.Pts[i].Pt, proj);
                    outPt = new ToolPoint(this.firstintersect(Ray, out Norm, out hit), proj);
                    tempN.Add(Norm);
                    if (hit)
                    {
                        tempTP.Pts.Add(outPt);
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
                newTPs.Add(tempTP);
                Norms.Add(tempN);
            }
            Vector3d tangent, PTplaneN, STNorm, PNplaneN;
 

            for(int j=0;j<TPs.Count;j++)
            {
                for (int i = 0; i < TPs[j].Pts.Count; i++)
                {           
                    // find the tangent vector;
                    if (i == TPs[j].Pts.Count - 1)
                    {
                        tangent = TPs[j].Pts[i].Pt - TPs[j].Pts[i - 1].Pt;
                    }
                    else
                    {
                        tangent = TPs[j].Pts[i + 1].Pt - TPs[j].Pts[i].Pt;
                    }
                    switch (this.STD)
                    {
                        case SurfToolDir.Projection: // already set
                            break; 
                        case SurfToolDir.PathTangent:
                            // get normal to tangent on surface
                            STNorm = Vector3d.CrossProduct(Norms[j][i], tangent);
                            PNplaneN = Vector3d.CrossProduct(TPs[j].Pts[i].Dir, STNorm);
                            // find vector normal to the surface in the line orthogonal to the tangent
                            Vector3d.CrossProduct(PNplaneN, STNorm);
                            break;
                        case SurfToolDir.PathNormal:
                            // get normal to proj and tangent
                            PTplaneN = Vector3d.CrossProduct(TPs[j].Pts[i].Dir, tangent);
                            // find vector normal to tangent and in the plane of tangent and projection
                            Vector3d.CrossProduct(PTplaneN, tangent);
                            break;
                        case SurfToolDir.Normal: // set to Norm
                            TPs[j].Pts[i].Dir = Norms[j][i];
                            break;
                    }
                    // Adjust the tool position based on the surface normal and the tool orientation
                    // so that the cutting surface not the tooltip is at the correct point

                    TPs[j].Pts[i].Pt = TPs[j].Pts[i].Pt + MT.CutOffset(TPs[j].Pts[i].Dir,Norms[j][i]);

                    // Move to offset using normal

                    TPs[j].Pts[i].Pt = TPs[j].Pts[i].Pt + (MT.toolWidth/2)*Norms[j][i];
                }
            }

            // make the machine operation
            MachineOperation MO = new MachineOperation("Surfacing Path");
            MO.TPs = TPs;
            return MO;
        }

        private Point3d firstintersect(Line Ray, out Vector3d Norm, out bool hit)
        {
            Point3d op = new Point3d(0, 0, 0);
            Norm = new Vector3d(0, 0, 0);
            Ray3d RayL = new Ray3d(Ray.From, Ray.UnitTangent);
            hit = false;
            switch (this.ST)
            {
                case surfaceType.Brep:
                    List<Brep> LB = new List<Brep>();
                    LB.Add(B);
                    Point3d[] interP = Intersection.RayShoot(RayL, LB,1);
                    if( interP.GetLength(1) > 0)
                    {
                        hit = true;
                        op = interP[0];
                        Point3d cp = new Point3d(0,0,0); ComponentIndex ci; double s, t; // catching infor we won't use;
                        // call closestpoint to find norm
                        B.ClosestPoint(op, out cp, out ci, out s, out t, 0.5, out Norm);
                    }
                    break;
                case surfaceType.Mesh:
                    int[] faces;
                    double inter = Intersection.MeshRay(this.M, RayL, out faces);
                    if (inter>=0)
                    {
                        hit = true;
                        op=RayL.PointAt(inter);
                        List<int> Lfaces = new List<int>();
                        Lfaces.AddRange(faces);
                        Norm= new Vector3d(0,0,0);
                        foreach(int F in Lfaces)
                        {
                            Norm = Norm + (Vector3d)this.M.Normals[F]/Lfaces.Count;
                        }
                    }
                    break;
            }
            return op;
        }

        private Vector3d ProjDir(Point3d point3d)
        {
            throw new NotImplementedException();
        }
    }

    // Grasshopper Type Wrapper
    public class GH_SurfacePath : CA_Goo<SurfacePath>
    {
        // Default Constructor
        public GH_SurfacePath()
        {
            this.Value = new SurfacePath();
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