using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using CAMel.Types.MaterialForm;
using System.Collections;

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

    public enum SurfaceType {
        Brep,
        Mesh
    }

    // A path that will project to a surface for surfacing
    // TODO write a subclass for each projection
    public class SurfacePath : IList<Curve>, ICAMel_Base
    {
        private List<Curve> Paths; // Curves to project
        public SurfProj surfProj { get; set;} // Type of projection
        public Curve cylOnto { get; set;}// centre line for Cylindrical projection
        public Vector3d dir { get; set;}// direction for parallel projection, or line direction for cylindrical
        public Point3d cen { get; set; } // centre for spherical projection
        public SurfToolDir surfToolDir { get; set; } // method to calculate tool direction

        // private storage when processing a model

        private SurfaceType ST; // Type of surface being processed
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
            this.surfProj = SurfProj.Parallel;
            this.dir = dir;
            this.surfToolDir = STD;
        }
        // Cylindrical constructor
        public SurfacePath(List<Curve> Paths, Vector3d dir, Curve CC, SurfToolDir surfToolDir)
        {
            this.Paths = Paths;
            this.surfProj = SurfProj.Cylindrical;
            this.dir = dir;
            this.cylOnto = CC;
            this.surfToolDir = surfToolDir;
        }
        // Spherical constructor
        public SurfacePath(List<Curve> Paths, Point3d Cen, SurfToolDir surfToolDir)
        {
            this.Paths = Paths;
            this.surfProj = SurfProj.Spherical;
            this.cen = Cen;
            this.surfToolDir = surfToolDir;
        }
        // Copy Constructor
        public SurfacePath(SurfacePath Os)
        {
            this.Paths = Os.Paths;
            this.surfProj = Os.surfProj;
            this.cylOnto = Os.cylOnto;
            this.dir = Os.dir;
            this.cen = Os.cen;
            this.surfToolDir = Os.surfToolDir;
        }
        // Duplicate
        public SurfacePath duplicate()
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

        public int Count => ((IList<Curve>)this.Paths).Count;

        public bool IsReadOnly => ((IList<Curve>)this.Paths).IsReadOnly;

        public Curve this[int index] { get => ((IList<Curve>)this.Paths)[index]; set => ((IList<Curve>)this.Paths)[index] = value; }

        public override string ToString()
        {
            string op= "Surfacing:";
            switch (this.surfProj)
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
        public MachineOperation generateOperation(Surface S, double offset, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            this.ST = SurfaceType.Brep;
            this.B = S.ToBrep();

            return this.generateOperation_(offset, MT, MF, TPA);
        }

        public MachineOperation generateOperation(Brep B, double offset, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            // Mesh is so much faster
            this.ST = SurfaceType.Mesh;
            this.B = B;
            MeshingParameters mP = MeshingParameters.Smooth;
            this.M = Mesh.CreateFromBrep(B, mP)[0];

            return this.generateOperation_(offset, MT, MF, TPA);
        }

        public MachineOperation generateOperation(Mesh M, double offset, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            this.ST = SurfaceType.Mesh;
            this.M = M;
            this.M.FaceNormals.ComputeFaceNormals();

            return this.generateOperation_(offset, MT, MF, TPA);
        }

        // actual code to generate the operation
        private MachineOperation generateOperation_(double offset, MaterialTool MT, IMaterialForm MF, ToolPathAdditions TPA)
        {
            // create unprojected toolpath (mainly to convert the curve into a list of points)
            List<ToolPath> TPs = new List<ToolPath>();

            foreach(Curve P in this.Paths)
            {
                TPs.Add(new ToolPath("", MT, MF, TPA));
                TPs[TPs.Count - 1].convertCurve(P,new Vector3d(0,0,1));
            }

            // move points onto surface storing projection direction
            // on the toolpoint and keeping lists of norms
            List<ToolPath> newTPs = new List<ToolPath>();
            List<List<Vector3d>> Norms = new List<List<Vector3d>>();
            List<Vector3d> tempN;

            ToolPath tempTP;


            foreach(ToolPath TP in TPs)
            {
                var intersectInfo = new ConcurrentDictionary<ToolPoint, FirstIntersectResponse>(Environment.ProcessorCount, TP.Count);

                foreach (ToolPoint TPt in TP) //initialise dictionary
                { intersectInfo[TPt] = new FirstIntersectResponse(); }

                Parallel.ForEach(TP, TPtP =>
                 {
                     intersectInfo[TPtP] = firstIntersect(TPtP);
                 }
                );
                
                tempTP = new ToolPath("", MT, MF, TPA);
                tempN = new List<Vector3d>();
                FirstIntersectResponse fIR;

                foreach( ToolPoint TPt in TP)
                {

                    fIR = intersectInfo[TPt];
                    if (fIR.hit)
                    {
                        tempTP.Add(fIR.tP);
                        tempN.Add(fIR.norm); 
                    }
                    else if(tempTP.Count > 0 )
                    {
                        if (tempTP.Count > 1)
                        {
                            newTPs.Add(tempTP);
                            Norms.Add(tempN);
                        }
                        tempTP = new ToolPath("", MT, MF, TPA);
                        tempN = new List<Vector3d>();
                    }
                }
                if (tempTP.Count > 1)
                {
                    newTPs.Add(tempTP);
                    Norms.Add(tempN);
                }
            }

            Vector3d tangent, PTplaneN, STNorm, PNplaneN;
 
            for(int j=0;j<newTPs.Count;j++)
            {
                for (int i = 0; i < newTPs[j].Count; i++)
                {
                    // find the tangent vector, assume the curve is not too jagged
                    // this is reasonable for most paths, but not for space 
                    // filling curve styles. Though for those this is a bad option.
                    // For the moment we will look 2 points back and one point forward.
                    // Some cases to deal with the start, end and short paths. 
                    // TODO Smooth this out by taking into account individual tangencies?
                    int lookback, lookforward;
                    if (i == newTPs[j].Count - 1)
                    {
                        lookback = 3;
                        if( i < lookback) { lookback = newTPs[j].Count - 1; }
                        lookforward = 0;
                    }
                    else
                    {
                        lookback = Math.Min(i, 2);
                        lookforward = 3-lookback;
                        if(lookforward + i >= newTPs[j].Count) { lookforward = newTPs[j].Count - i - 1; }
                    }

                    tangent = newTPs[j][i+lookforward].pt - newTPs[j][i - lookback].pt;
                    switch (this.surfToolDir)
                    {
                        case SurfToolDir.Projection: // already set
                            break; 
                        case SurfToolDir.PathNormal:
                            // get normal to tangent on surface
                            STNorm = Vector3d.CrossProduct(Norms[j][i], tangent);
                            PNplaneN = Vector3d.CrossProduct(newTPs[j][i].dir, STNorm);
                            // find vector normal to the surface in the line orthogonal to the tangent
                            newTPs[j][i].dir = Vector3d.CrossProduct(STNorm,PNplaneN);
                            break;
                        case SurfToolDir.PathTangent:
                            // get normal to proj and tangent
                            PTplaneN = Vector3d.CrossProduct(tangent,newTPs[j][i].dir);
                            PNplaneN = newTPs[j][i].dir;
                            // find vector normal to tangent and in the plane of tangent and projection
                            newTPs[j][i].dir = Vector3d.CrossProduct(PTplaneN, tangent);
                           
                            break;
                        case SurfToolDir.Normal: // set to Norm
                            newTPs[j][i].dir = Norms[j][i]; 
                            break;
                    }
                    // Adjust the tool position based on the surface normal and the tool orientation
                    // so that the cutting surface not the tooltip is at the correct point

                    newTPs[j][i].pt = newTPs[j][i].pt + MT.cutOffset(newTPs[j][i].dir,Norms[j][i]);

                    // Move to offset using normal

                    newTPs[j][i].pt = newTPs[j][i].pt + offset*Norms[j][i];
                }
            }

            // make the machine operation
            MachineOperation MO = new MachineOperation(this.ToString(), newTPs);
            return MO;
        }

        private struct FirstIntersectResponse
        {
            public ToolPoint tP { get; set; }
            public Vector3d norm { get; set; }
            public bool hit { get; set; }

            public override string ToString()
            {
                return this.hit.ToString();
            }
        }

        private FirstIntersectResponse firstIntersect(ToolPoint TP)
        {
            FirstIntersectResponse fIR = new FirstIntersectResponse();
            Vector3d Norm = new Vector3d();
   
            Vector3d proj = this.projDir(TP.pt);
            Ray3d RayL = new Ray3d(TP.pt, proj);

            fIR.hit = false;
            switch (this.ST)
            {
                case SurfaceType.Brep:
                    List<Brep> LB = new List<Brep> { this.B };
                    Point3d[] interP = Intersection.RayShoot(RayL, LB,1);
                    List<Point3d> LinterP = new List<Point3d>();
                    if(interP != null) {LinterP.AddRange(interP);}
                    if( LinterP.Count > 0)
                    {
                        fIR.hit = true;
                        fIR.tP = new ToolPoint(interP[0],proj);
                        Point3d cp = new Point3d(); ComponentIndex ci; double s, t; // catching info we won't use;
                        // call closestpoint to find norm
                        this.B.ClosestPoint(fIR.tP.pt, out cp, out ci, out s, out t, 0.5, out Norm);
                        fIR.norm = Norm;
                    }
                    break;
                case SurfaceType.Mesh:
                    
                    int[] faces;
                    double inter = Intersection.MeshRay(this.M, RayL, out faces);
                    if (inter>=0)
                    {
                        fIR.hit = true;
                        fIR.tP= new ToolPoint(RayL.PointAt(inter),-proj);
                        List<int> Lfaces = new List<int>();
                        Lfaces.AddRange(faces);
                        fIR.norm = (Vector3d)this.M.FaceNormals[Lfaces[0]];

                        if (fIR.norm * RayL.Direction > 0) { fIR.norm = -fIR.norm; }
                    }
                    break;
            }
            return fIR;
        }
        // Give the direction of projection for a specific point based on the projection type.
        private Vector3d projDir(Point3d Pt)
        {
            Vector3d pd = new Vector3d();
            switch (this.surfProj)
            {
                case SurfProj.Parallel:
                    pd = this.dir;
                    break;
                case SurfProj.Cylindrical:
                    Plane Pl = new Plane(Pt,this.dir);
                    
                    if(this.cylOnto.IsLinear()) // if centre is a line treat it as infinite
                    {
                        double lp;
                        Line cyline = new Line(this.cylOnto.PointAtStart, this.cylOnto.PointAtEnd);
                        if(Intersection.LinePlane(cyline, Pl, out lp))
                        {
                            pd = cyline.PointAt(lp)-Pt;
                        } else
                        {
                            throw new System.ArgumentOutOfRangeException("Cylinder Parallel","The projection direction is parallel to cyliner centre.");
                        }
                    } else // Use curve and warn if no intersection
                    {
                        CurveIntersections CI = Intersection.CurvePlane(this.cylOnto,Pl,0.0000001);
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
                    pd = this.cen-Pt;
                    break;
                default:
                    break;
            }
            return pd;
        }

        public int IndexOf(Curve item)
        {
            return ((IList<Curve>)this.Paths).IndexOf(item);
        }

        public void Insert(int index, Curve item)
        {
            ((IList<Curve>)this.Paths).Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            ((IList<Curve>)this.Paths).RemoveAt(index);
        }

        public void Add(Curve item)
        {
            ((IList<Curve>)this.Paths).Add(item);
        }

        public void Clear()
        {
            ((IList<Curve>)this.Paths).Clear();
        }

        public bool Contains(Curve item)
        {
            return ((IList<Curve>)this.Paths).Contains(item);
        }

        public void CopyTo(Curve[] array, int arrayIndex)
        {
            ((IList<Curve>)this.Paths).CopyTo(array, arrayIndex);
        }

        public bool Remove(Curve item)
        {
            return ((IList<Curve>)this.Paths).Remove(item);
        }

        public IEnumerator<Curve> GetEnumerator()
        {
            return ((IList<Curve>)this.Paths).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<Curve>)this.Paths).GetEnumerator();
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
                target = (Q)(object)this.Value;
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