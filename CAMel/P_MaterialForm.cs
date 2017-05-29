﻿using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace CAMel.Types
{
    public enum FormType 
    {
        Plane,
        Brep,
        Cylinder,
        Box
    }

    public enum hs
    {
        MM,
        MP,
        PM,
        PP
    }

    // Where are we safe to move?
    // This handles step down, insert and retract to be (hopefully!) safe
    // At the moment can be defined just be a safe plane for locally 
    // defined safety. Eventually will take account of the material as
    // a brep, a mesh or even list of meshes as material is cut away.
    //
    // Like Machine but less urgent, this should be an interface or abstract class
    // with different ways of describing materials inheriting it. 
    public class MaterialForm : CA_base
    {
        private Plane Pl;
        public double safeDistance;
        public double materialTolerance;
        private Brep Shape;
        private Box Bx;
        private Cylinder Cy;
        private FormType FT; // Track how we are establishing material
        private Mesh cacheMesh;
        private Brep cacheBrep;
        private Cylinder tolerancedCylinder;

        // Default Constructor with XY plane with safe distance 1;
        public MaterialForm()
        {
            this.Pl = Plane.WorldXY;
            FT = FormType.Plane;
            this.Shape = null;
            this.safeDistance = 1;
            this.materialTolerance = 0;
            this.cacheMesh = null;
            this.cacheBrep = null;
        }
        // Plane
        public MaterialForm(Plane surfaceP,double safeD,double matTolerance)
        {
            this.Pl = surfaceP;
            FT = FormType.Plane;
            this.safeDistance = safeD;
            this.materialTolerance = matTolerance;
            this.Shape = null;
        }
        // Brep
        public MaterialForm(Brep S, double sD, double matTolerance)
        {
            this.Shape = S;
            this.FT = FormType.Brep;
            this.Pl = Plane.WorldXY;
            this.safeDistance = sD;
            this.materialTolerance = matTolerance;
        }
        // Box
        public MaterialForm(Box B, double sD, double matTolerance)
        {
            this.Bx = B;
            this.FT = FormType.Box;
            this.Pl = Plane.WorldXY;
            this.safeDistance = sD;
            this.materialTolerance = matTolerance;
            toleranceBox(this.Bx, this.materialTolerance);
        }
        // Cylinder
        public MaterialForm(Cylinder Cy, double sD, double matTolerance)
        {
            this.Cy = Cy;
            this.FT = FormType.Cylinder;
            this.Pl = Plane.WorldXY;
            this.safeDistance = sD;
            this.materialTolerance = matTolerance;
            toleranceCylinder(this.Cy.CircleAt(0), this.materialTolerance);
        }
        // Copy Constructor
        public MaterialForm(MaterialForm MF)
        {
            this.Pl = MF.Pl;
            this.FT = MF.FT;
            this.safeDistance = MF.safeDistance;
            this.materialTolerance = MF.materialTolerance;
            this.Shape = MF.Shape;
            this.Bx = MF.Bx;
            this.Cy = MF.Cy;
            this.cacheMesh = MF.cacheMesh;
            this.tolerancedCylinder = MF.tolerancedCylinder;
            this.cacheBrep = MF.cacheBrep;
        }
        // Duplicate
        public MaterialForm Duplicate()
        {
            return new MaterialForm(this);
        }

        // Change to Plane, replacing Brep if there was one
        public bool ChangePlane(Plane P, double sD, double matTolerance)
        {
            this.Pl = P;
            this.safeDistance = sD;
            this.materialTolerance = matTolerance;
            bool stick = true; //Did the formtype stay the same?
            if( this.FT != FormType.Plane) 
            {
                stick = false;
                this.Shape = null;
            }
            this.FT = FormType.Plane;
            return stick;
        }

        // Change the material form to Brep or Box
        public bool ChangeShape(Brep B, double sD, double matTolerance)
        {
            this.Shape = B;
            this.safeDistance = sD;
            this.materialTolerance = matTolerance;
            bool stick = true; //Did the formtype stay the same?
            if( this.FT != FormType.Brep) 
            {
                stick = false;
            }
            this.FT = FormType.Brep;
            return stick;
        }
        public bool ChangeShape(Box B, double sD, double matTolerance)
        {
            this.Bx = B;
            this.safeDistance = sD;
            this.materialTolerance = matTolerance;
            bool stick = true; //Did the formtype stay the same?
            if (this.FT != FormType.Box)
            {
                stick = false;
            }
            this.FT = FormType.Box;
            toleranceBox(this.Bx, this.materialTolerance);
            return stick;
        }

        public bool ChangeShape(Cylinder Cy, double sD, double matTolerance)
        {
            this.Cy = Cy;
            this.safeDistance = sD;
            this.materialTolerance = matTolerance;
            bool stick = true; //Did the formtype stay the same?
            if (this.FT != FormType.Cylinder)
            {
                stick = false;
            }
            this.FT = FormType.Cylinder;
            toleranceCylinder(this.Cy.CircleAt(0), this.materialTolerance);
            return stick;
        }

        // functions to look at internals
        public FormType GetFormType()
        {
            return this.FT;
        }

        public Brep GetShape()
        {
            if(FT == FormType.Brep)
            {
                return Shape;
            }
            else 
            {
                throw new InvalidOperationException("Material not currently described by a Brep.");
            }
        }
        public Cylinder GetCylinder()
        {
            if (FT == FormType.Cylinder)
            {
                return this.Cy;
            }
            else
            {
                throw new InvalidOperationException("Material not currently described by a Cylinder.");
            }
        }
        public Box GetBox()
        {
            if (FT == FormType.Box)
            {
                return Bx;
            }
            else
            {
                throw new InvalidOperationException("Material not currently described by a Box.");
            }
        }

        public Plane GetPlane()
        {
            if (FT == FormType.Plane)
            {
                return new Plane(this.Pl);
            }
            else
            {
                throw new InvalidOperationException("Material not currently described by a Plane.");
            }
        }

        public override string TypeDescription
        {
            get { return "Information about where material to cut is."; }
        }

        public override string TypeName
        {
            get { return "MaterialForm"; }
        }

        public override string ToString()
        {
            string txt = "";
            switch (this.FT)
            {
                case FormType.Plane:
                    txt = "Material Plane: " + this.Pl.ToString() + ", safe distance: " + this.safeDistance.ToString();
                    break;
                case FormType.Brep:
                    txt = "Material Brep, safe distance: " + this.safeDistance.ToString();
                    break;
                case FormType.Box:
                    txt = "Material Box, safe distance: " + this.safeDistance.ToString();
                    break;
                case FormType.Cylinder:
                    txt = "Material Cylinder, safe distance: " + this.safeDistance.ToString();
                    break;
                default:
                    txt = "Unknown state encountered.";
                    break;
            }
            return txt;
        }

        // Real functions

        // distance along tool direction to material surface
        public double MatDist(ToolPoint TP, Machine M, MaterialTool MT, out Vector3d Dir, out Vector3d Norm)
        {
            Dir = -M.ToolDir(TP);
            Line L = new Line(TP.Pt,TP.Pt+Dir);
            double dist = 0;

            switch (this.FT)
            {
                case FormType.Plane:
                    Plane safePlane = new Plane(this.Pl);
                    safePlane.Origin = this.Pl.Origin + this.Pl.ZAxis * this.materialTolerance;
                    if (!Intersection.LinePlane(L, safePlane, out dist))
                    {
                        throw new InvalidOperationException("Trying to move tool out of material parallel to safe plane.");
                    }
                    // if dist is negative we are outside the material, set to 0;
                    if(dist < 0) dist = 0;

                    Norm = this.Pl.ZAxis;
                    break;
                case FormType.Box:
                    if (this.cacheMesh == null) {
                        throw new FieldAccessException("Mesh should have been cached for Box like Material Forms");
                    }
                    if (this.cacheBrep == null)
                    {
                        throw new FieldAccessException("Brep cache missing for Box like Material Form");
                    }

                    Interval paras = new Interval();
                    // Intersect returns an interval given by the 
                    // parameters on the line that enter and leave box
                    // 0 is the toolpoint and anything positive lies along the tool

                    double interparam; // Intersection param, used to find the normal direction.
 
                    Box thickBx = this.Bx; // thicken box by materialTolerance
                    thickBx.Inflate(this.materialTolerance);

                    if (!Intersection.LineBox(L,thickBx,0.0,out paras))
                    { // line never hits box
                        dist = 0;
                        interparam = 0;
                    } else if(paras.T1 < 0)
                    {  // both points negative, not in material
                        dist = 0;
                        interparam = 0;
                    } else
                    {  // point in material or through material, give distance in positive direction
                        dist = paras.T1;
                        interparam = paras.T1;
                    }
                    // find the normal at the intersection, or closest point on the box
                    // box does not itself give normals so we will look at it as a mesh
                    Point3d PoM = new Point3d();
                    this.cacheMesh.ClosestPoint(L.PointAt(interparam), out PoM, out Norm, 0.0);
                    break;
                case FormType.Cylinder:
                    if (this.cacheMesh == null)
                    {
                        throw new FieldAccessException("Mesh should have been cached for Cylinder like Material Forms");
                    }
                    if (this.cacheBrep == null)
                    {
                        throw new FieldAccessException("Brep cache missing for Cylinder like Material Form");
                    }

                    Point3d inter1 = new Point3d(), inter2=new Point3d();
                    Point3d CP; 
                    LineCylinderIntersection LCI;
                    LCI=Intersection.LineCylinder(L,this.tolerancedCylinder,out inter1, out inter2);
                    switch (LCI)
                    {
                        case LineCylinderIntersection.None: // No intersection
                            dist = 0;
                            CP = inter1;
                            break;
                        case LineCylinderIntersection.Overlap: // on surface but parallel
		                case LineCylinderIntersection.Multiple: 
		                case LineCylinderIntersection.Single: 
                            CP = inter2;
                            dist = L.ClosestParameter(CP);
                            if(dist < 0) { dist = 0; } // tool never reaches material
                            break;
                        default:
                            CP = new Point3d();
                            break;
                    }

                    // find the normal at the intersection, or closest point on the cylinder
                    // cylinder does not itself give normals so we will look at it as a mesh
                    PoM = new Point3d();
                    this.cacheMesh.ClosestPoint(CP, out PoM, out Norm, 0.0);
                    break;
               /* case FormType.Brep:
                    
                    // We now want to ensure that we are at least material tolerance away from
                    // object. To do this we intersect the brep with a cylinder and then find
                    // the bounding box. I hate to think how slow this will be for a complex
                    // brep. Need to test!

                    Plane DPl = new Plane(TP.Pt, Dir);
                    Circle matTolCirc = new Circle(DPl,materialTolerance+MT.toolWidth);

                    Brep cyl = Brep.CreateFromCylinder(new Cylinder(matTolCirc,10000),true,true);

                    Brep[] local = Brep.CreateBooleanIntersection(this.Shape, cyl,materialTolerance/100);

                    BoundingBox BB = new BoundingBox(0,0,0,0,0,0);
                    bool inMaterial = false; // Check to see if at least one Brep returned by intersection
                    foreach (Brep B in local)
                    {
                        BB.Union(B.GetBoundingBox(DPl));
                        inMaterial = true;
                    }
                    if(inMaterial)
                    {
                        dist = BB.Max.Z + materialTolerance;
                    }
                    break;*/
                default:
                    throw new NotImplementedException("Unknown FormType for material Form.");
            }
            return dist; // If this is 0 the point lies on the surface of the material. If negative that distance outside the material.
        }

        private ToolPath Refine_Plane(ToolPath TP, Machine M)
        {
            // with everthing linear we don't need to worry
            return TP;
        }

        private ToolPath Refine_Shape(ToolPath TP, Machine M)
        {
            // TODO currently won't work for 5 axis machines
            // as does not include orientation of tool
            ToolPath refined = new ToolPath(TP);
            refined.Pts = new List<ToolPoint>();

            // Add the first ToolPoint

            refined.Pts.Add(TP.Pts[0]);

            Line L;
            for(int i = 0; i< TP.Pts.Count-1;i++)
            {

                // for every line between points check if we leave the material
                // and add a toolpoint. Also add extra points if the jump is 
                // too great.

                L = new Line(TP.Pts[i].Pt, TP.Pts[i+1].Pt);
                Curve[] olC;
                Point3d[] iPt;
                if(! Intersection.CurveBrep(new LineCurve(L),this.Shape,0.001, out olC, out iPt))
                    throw new InvalidOperationException("There was a problem intersecting the ToolPath with the MaterialForm.");

                // include the final point with the intersects
                List<Point3d> interPt = new List<Point3d>(iPt);
                interPt.Add(TP.Pts[i+1].Pt);

                // step through adding point between when needed
                // if there are no intersections we will just go to the 
                // next point and add it.

                double d;
                Point3d lastPt = TP.Pts[i].Pt;
                double par = 0;
                double step = (TP.MatTool.toolWidth+this.materialTolerance)*2; // maximum allowed step size
                double stepPar = step/L.Length; // the step in terms of the line parameter
                
                foreach(Point3d Pt in interPt)
                {
                    d = lastPt.DistanceTo(Pt);
                    while(d > step)
                    {
                        refined.Pts.Add(M.Interpolate(TP.Pts[i],TP.Pts[i+1],par+stepPar));
                        d -= step;
                        par += stepPar;
                    }
                    par = L.ClosestParameter(Pt);
                    refined.Pts.Add(M.Interpolate(TP.Pts[i],TP.Pts[i+1],par));
                }
            }

            return refined;
        }

        private ToolPath Refine_Box(ToolPath TP, Machine M)
        {
            // for each line check if it intersects 
            // the box and add those points. 
            // For each internal line add a toolpoint at the position
            // deepest into box. The goal is to avoid situations
            // where the end points are close to surface but the 
            // middle is deep. 

            ToolPath refined = new ToolPath(TP);
            refined.Pts = new List<ToolPoint>();

            // Add the first ToolPoint

            if (TP.Pts.Count > 0) { refined.Pts.Add(TP.Pts[0]); }

            Line L;
            ToolPoint newPt;
            for (int i = 0; i < TP.Pts.Count - 1; i++)
            {

                // for every line between points check if we leave or enter the material
                // TODO problem of long lines getting deep 

                L = new Line(TP.Pts[i].Pt, TP.Pts[i + 1].Pt);
                Interval paras;

                if (Intersection.LineBox(L,this.Bx,0.001,out paras))
                { 
                    if(paras.T0>0 && paras.T0<1) // intersection with material
                    {
                        newPt= new ToolPoint(TP.Pts[i]);
                        newPt.Pt = L.PointAt(paras.T0);
                        newPt.Dir = M.Interpolate(TP.Pts[i],TP.Pts[i+1],paras.T0).Dir;
                        refined.Pts.Add(newPt);
                    }
                    if (paras.T1 > 0 && paras.T1 < 1) // intersection with material
                    {
                        newPt = new ToolPoint(TP.Pts[i]);
                        newPt.Pt = L.PointAt(paras.T1);
                        newPt.Dir = M.Interpolate(TP.Pts[i], TP.Pts[i + 1], paras.T1).Dir; 
                        refined.Pts.Add(newPt);
                    }
                }
                refined.Pts.Add(TP.Pts[i + 1]);
            }

            return refined;
        }

        private ToolPath Refine_Cylinder(ToolPath TP, Machine M)
        {
            // for each line check if it intersects 
            // the box and add those points. 
            // For each internal line add a toolpoint at the position
            // deepest into box. The goal is to avoid situations
            // where the end points are close to surface but the 
            // middle is deep. 

            ToolPath refined = new ToolPath(TP);
            refined.Pts = new List<ToolPoint>();

            // Add the first ToolPoint

            if (TP.Pts.Count > 0) { refined.Pts.Add(TP.Pts[0]); }

            Line L;
            ToolPoint newPt;
            for (int i = 0; i < TP.Pts.Count - 1; i++)
            {

                // for every line between points check if we leave or enter the material
                // TODO problem of long lines getting deep 

                L = new Line(TP.Pts[i].Pt, TP.Pts[i + 1].Pt);
                Point3d inter1 = new Point3d(), inter2 = new Point3d();
                LineCylinderIntersection LCI = Intersection.LineCylinder(L, Cy, out inter1, out inter2);

                if (LCI != LineCylinderIntersection.None)
                {
                    double param = L.ClosestParameter(inter1);
                    if (param > 0 && param < 1)
                    {
                        newPt = new ToolPoint(TP.Pts[i]);
                        newPt.Pt = inter1;
                        newPt.Dir = M.Interpolate(TP.Pts[i], TP.Pts[i + 1], param).Dir;
                        refined.Pts.Add(newPt); 
                    }
                    if (LCI != LineCylinderIntersection.Single)
                    {
                        param = L.ClosestParameter(inter2);
                        if (param > 0 && param < 1)
                        {
                            newPt = new ToolPoint(TP.Pts[i]);
                            newPt.Pt = inter2;
                            newPt.Dir = M.Interpolate(TP.Pts[i], TP.Pts[i + 1], param).Dir;
                            refined.Pts.Add(newPt);
                        }
                    }
                }
                refined.Pts.Add(TP.Pts[i + 1]);
            }

            return refined;
        }

        // Add extra points to the toolpath where it intersects with the 
        // material between points. 

        public ToolPath Refine(ToolPath TP, Machine M)
        {
            switch (this.FT)
	            {
		        case FormType.Plane:
                    return this.Refine_Plane(TP, M);
                case FormType.Brep:
                    return this.Refine_Shape(TP, M);
                case FormType.Box:
                    return this.Refine_Box(TP, M);
                case FormType.Cylinder:
                    return this.Refine_Cylinder(TP, M);
                default:
                    throw new NotImplementedException("Unknown FormType for material Form.");
	            }
        }

        // Retract to surface and then move away from it.
        public ToolPath InsertRetract(ToolPath TP, Machine M)
        {
            ToolPath irTP = new ToolPath(TP);
            irTP.Additions.insert = false;
            irTP.Additions.retract = false;

            double dist;
            Vector3d Dir;
            Vector3d Norm;

            // check if we have something to do
            if (TP.Additions.insert && irTP.Pts.Count > 0) // add insert
            {
                //note we do this backwards adding points to the start of the path.

                // get distance to surface and insert direction
                dist = this.MatDist(irTP.Pts[0], M, TP.MatTool, out Dir, out Norm);

                // point on material surface

                ToolPoint tempTP = new ToolPoint(irTP.Pts[0]);
                tempTP.Pt = tempTP.Pt + Dir * dist;
                tempTP.feed = TP.MatTool.feedPlunge;
                irTP.Pts.Insert(0, tempTP);

                // point out at safe distance

                tempTP = new ToolPoint(irTP.Pts[0]);
                tempTP.Pt = tempTP.Pt + Norm * (this.safeDistance);
                tempTP.feed = 0; // we can use a rapid move
                irTP.Pts.Insert(0, tempTP);
            }
            if (TP.Additions.retract && irTP.Pts.Count > 0) // add retract
            {
                // get distance to surface and retract direction
                dist = this.MatDist(irTP.Pts[irTP.Pts.Count - 1], M, TP.MatTool, out Dir, out Norm);

                // set speed to the plunge feed rate.

                irTP.Pts[irTP.Pts.Count - 1].feed = TP.MatTool.feedPlunge;

                // Pull back to surface

                ToolPoint tempTP = new ToolPoint(irTP.Pts[irTP.Pts.Count - 1]);
                tempTP.Pt = tempTP.Pt + Dir * dist;
                tempTP.feed = 0; // we can use a rapid move

                irTP.Pts.Add(tempTP);

                // Pull away to safe distance

                tempTP = new ToolPoint(irTP.Pts[irTP.Pts.Count - 1]);
                tempTP.Pt = tempTP.Pt + Norm * (this.safeDistance);
                tempTP.feed = 0; // we can use a rapid move
                irTP.Pts.Add(tempTP);
            }
            return irTP;
        }

        // Is the toolpoint given in safe space?
        public double SafePoint(ToolPoint toolPoint)
        {
            double safeD;
            switch (this.FT)
            {
                case FormType.Plane:
                    Point3d plPt;
                    this.Pl.RemapToPlaneSpace(toolPoint.Pt, out plPt);
                    safeD = plPt.Z;
                    break;
                case FormType.Box:
                    safeD = this.Bx.ClosestPoint(toolPoint.Pt).DistanceTo(toolPoint.Pt);
                    if(this.Bx.Contains(toolPoint.Pt)) safeD = -1*safeD;
                    break;
                case FormType.Cylinder:
                    Plane Pl = new Plane(this.Cy.Center,this.Cy.Axis);
                    Pl.RemapToPlaneSpace(toolPoint.Pt, out plPt);
                    double r = new Vector3d(plPt.X,plPt.Y,0).Length;
                    double Cr = this.Cy.CircleAt(0).Radius;

                    if(plPt.Z>=this.Cy.Height1 && plPt.Z <= this.Cy.Height2) // closest point on curved surface
                    {
                        safeD = r-Cr;
                    } else // closest point on top or bottom
                    {
                        double useH;
                        if(plPt.Z < this.Cy.Height1) { useH = this.Cy.Height1; } //identify top or bottom
                        else { useH = this.Cy.Height2; }

                        if(r < Cr) { safeD = Math.Abs(plPt.Z - useH); } // closestpoint on end face
                        else { safeD = this.Cy.CircleAt(useH).ClosestPoint(toolPoint.Pt).DistanceTo(toolPoint.Pt); } // closestpoint on edge
                    }
                    break;
                case FormType.Brep:
                    safeD=this.Shape.ClosestPoint(toolPoint.Pt).DistanceTo(toolPoint.Pt);
                    break;
                default:
                    throw new NotImplementedException("Unknown FormType for material Form.");
            }
            return safeD-this.materialTolerance-this.safeDistance;
        }

        static public double closestDangerLinePlanePlane(Line L, Plane P1, Plane P2, out Point3d cPt, out Vector3d away)
        {
            // This assumes that both end points are safe
            // There has to be a more elegant way to do this!

            // Classify the end points of the line.
            // by which side of the two planes they lie 
            // on.

            Point3d start1, start2;
            Point3d end1, end2;

            P1.RemapToPlaneSpace(L.From, out start1);
            P1.RemapToPlaneSpace(L.To, out end1);
            P2.RemapToPlaneSpace(L.From, out start2);
            P2.RemapToPlaneSpace(L.To, out end2);

            hs starths=0, endhs=0;

            if(start1.Z > 0) starths += 2;
            if(start2.Z > 0) starths += 1;
            if(end1.Z > 0) endhs += 2;
            if(end2.Z > 0) endhs += 1;

            if(starths == hs.MM || endhs == hs.MM)
            {
                throw new ArgumentException("closestDanger called for a path with unsafe vertices.","L");
            }

            // Find a line to see distance to 
            Line closeLine;

            if(endhs == hs.PM && starths == hs.PM) // The line is only in the first half space
            {
                closeLine = new Line(L.From, L.To);
                closeLine.Transform(Transform.PlanarProjection(P1));
            }
            else if (endhs == hs.MP && starths == hs.MP) // The line is only in the second half space
            {
                closeLine = new Line(L.From, L.To);
                closeLine.Transform(Transform.PlanarProjection(P2));
            }
            else // we can use the intersection line for the planes, unless they are parallel
            {
                if(!Intersection.PlanePlane(P1,P2,out closeLine))
                {
                    if(start1.Z > start2.Z) // further from first plane
                    {
                        closeLine = new Line(L.From, L.To);
                        closeLine.Transform(Transform.PlanarProjection(P1));
                    }
                    else
                    {
                        closeLine = new Line(L.From, L.To);
                        closeLine.Transform(Transform.PlanarProjection(P2));
                    }
                }
            }

            // The point we want is the closest point of closeLine to L
            double a, b; // parameters of closest points

            if(!Intersection.LineLine(L, closeLine, out a, out b, 0, true)) // lines are parallel
            {
                away = (Vector3d)(L.From - closeLine.From);
                away.Unitize();
                cPt = closeLine.From;
                return (L.From - closeLine.From).Length;
            }
            else
            {
                away = (Vector3d)(L.PointAt(a) - closeLine.PointAt(b));
                away.Unitize();
                cPt = closeLine.PointAt(b);
                return (L.PointAt(a) - closeLine.PointAt(b)).Length;
            } 
        }



        // find the closest danger to a path (not toolpath!) defined by a list of points.
        // For machines beyond 3-axis these points might be pivot points or something else, 
        // rather than the tooltip points toolpath assumes.
        public double closestDanger(List<Point3d> route, MaterialForm toForm, out Point3d cPt, out Vector3d away, out int i)
        {
            double dist = 0;
            cPt = new Point3d(0, 0, 0);
            away = new Vector3d(0, 0, 0);
            switch (this.FT)
            {
                case FormType.Box:
                case FormType.Cylinder:

                    // first check for intersection 
                    // use box as mesh to pull normal info
                    Mesh FormGeom = this.cacheMesh;
                    PolylineCurve C = new PolylineCurve(route);
                    Polyline P = new Polyline(route);
                    int[] fIds;
                    List<Point3d> IPts = new List<Point3d>();
                    IPts.AddRange(Intersection.MeshPolyline(FormGeom,C,out fIds));

                    if(IPts.Count>0)
                    {
                        FormGeom.ClosestPoint((IPts[0] + IPts[1]) / 2.0, out cPt, out away, 0.0);
                        dist = 0;
                        // The integer part of the polyline parameter is the line element
                        i = (int)Math.Floor(P.ClosestParameter(IPts[0]));
                    } else {

                    // Use Rhino's curve closest, possibly slow!
                        List<Brep> GeomList = new List<Brep>();
                        Point3d PoC, PoO; // point on curve and point on object for closest
                        int geo; // geometry for closest (won't be used)

                        GeomList.Add(this.cacheBrep);
                        if (C.ClosestPoints(GeomList, out PoC, out PoO, out geo))
                        {
                            cPt = PoC;
                            away = (Vector3d)(PoC - PoO);
                            dist = away.Length;
                            away.Unitize();

                            // The integer part of the polyline parameter is the line element
                            i = (int)Math.Floor(P.ClosestParameter(PoC));
                        } else
                        {
                            throw new InvalidOperationException("ClosestPoints for Brep and Curve Failed");
                        }
                    }
                    break;
                case FormType.Plane:
                    dist = 10000000000000;
                    i = 0;
                    double localDist;
                    Vector3d localAway;
                    Point3d localcPt;
                    for (int j = 0; j < route.Count - 1; j++)
                    {
                        localDist = closestDangerLinePlanePlane(new Line(route[j], route[j + 1]), this.Pl, toForm.Pl, out localcPt, out localAway);
                        if (localDist < dist)
                        {
                            dist = localDist;
                            cPt = localcPt;
                            away = localAway;
                            i = j;
                        }
                    }
                    break;
                case FormType.Brep:
                    //PolylineCurve C = new PolylineCurve(route);
                    //Point3d CPoint, OPoint;
                    // TODO Brep: closest Danger
                    //C.ClosestPoints(this.Shape,out CPoint,out OPoint,out int);
                    throw new NotImplementedException("Brep form not implemented yet");
                default:
                    throw new NotImplementedException("Unknown FormType for material Form.");
            }

            return dist;
        }
        // Test if the ray given by a point and its tool direction
        // intersects the material or not. 
        public bool TPRayIntersect(ToolPoint TP)
        {
            bool inter = true;
            Line Ray = new Line(TP.Pt,TP.Pt-TP.Dir);
            switch (this.FT)
            {
                case FormType.Box:
                    Interval Val = new Interval();
                    if(Intersection.LineBox(Ray,this.Bx,0,out Val)) { inter = Val.T0>0; } 
                    else { inter = false; }
                    break;
                case FormType.Cylinder:
                    Point3d inter1 = new Point3d(), inter2 = new Point3d();
                    LineCylinderIntersection LCI = Intersection.LineCylinder(Ray, this.Cy, out inter1, out inter2);
                    if (LCI == LineCylinderIntersection.None) { inter = false; }
                    else { inter = (TP.Dir*(Vector3d)TP.Pt > TP.Dir*(Vector3d)inter1); }
                    break;
                case FormType.Plane:
                    double para;
                    inter = Intersection.LinePlane(Ray, this.Pl, out para);
                    if (para < 0) { inter = false; }
                    break;
                default:
                    throw new System.NotImplementedException("TPRayIntersect not implemented for Breps");
            }
            return inter;
        }

        private void toleranceBox(Box B, double matTolerance)
        {
            // Cache the mesh once rather than generate it for every toolpath
            B.Inflate(matTolerance);
            this.cacheMesh = Mesh.CreateFromBox(B, 1, 1, 1);
            this.cacheBrep = B.ToBrep();
        }

        private void toleranceCylinder(Circle C, double matTolerance)
        {
            Cylinder useCy; // Expand cylinder by matTolerance
            C.Radius = C.Radius + matTolerance;
            useCy = new Cylinder(C);
            useCy.Height1 = this.Cy.Height1 - matTolerance;
            useCy.Height2 = this.Cy.Height2 + matTolerance;

            // Cache the Cylinder expanded to material tolerances as specified and a mesh
            this.tolerancedCylinder = useCy;
            this.cacheMesh = Mesh.CreateFromCylinder(this.tolerancedCylinder, 1, 360);
            this.cacheBrep = this.tolerancedCylinder.ToBrep(true, true);
        }
    }

    // Grasshopper Type Wrapper
    public class GH_MaterialForm : CA_Goo<MaterialForm>
    {
        // Default Constructor with XY plane with safe distance 1;
        public GH_MaterialForm()
        {
            this.Value = new MaterialForm();
        }
        // Plane
        public GH_MaterialForm(Plane sP,double sD, double MatTolerance)
        {
            this.Value = new MaterialForm(sP, sD,  MatTolerance);
        }
        // Brep
        public GH_MaterialForm(Brep S, double sD, double MatTolerance)
        {
            this.Value = new MaterialForm(S, sD, MatTolerance);
        }
        // Construct from unwrapped object
        public GH_MaterialForm(MaterialForm MF)
        {
            this.Value = new MaterialForm(MF);
        }
        // Copy Constructor.
        public GH_MaterialForm(GH_MaterialForm MF)
        {
            this.Value = new MaterialForm(MF.Value);
        }
        // Duplicate
        public override IGH_Goo Duplicate()
        {
            return new GH_MaterialForm(this);
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q).IsAssignableFrom(typeof(MaterialForm)))
            {
                object ptr = this.Value;
                target = (Q)ptr;
                return true;
            }
            // Cast to Brep or Plane if that is asked for.
            if (typeof(Q).IsAssignableFrom(typeof(Brep)) && this.Value.GetFormType() == FormType.Brep )
            {
                object ptr = this.Value.GetShape();
                target = (Q)ptr;
                return true;
            }
            if (typeof(Q).IsAssignableFrom(typeof(Plane)) && this.Value.GetFormType() == FormType.Plane)
            {
                object ptr = this.Value.GetPlane();
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
            if (source is MaterialForm)
            {
                this.Value = new MaterialForm((MaterialForm)source);
                return true;
            }
            return false;
        }

    }

    // Grasshopper Parameter Wrapper
    public class GH_MaterialFormPar : GH_Param<GH_MaterialForm>
    {
        public GH_MaterialFormPar() :
            base("Material Form", "MatForm", "Contains a collection of Material Forms", "CAMel", "  Params", GH_ParamAccess.item) { }
        public override Guid ComponentGuid
        {
            get { return new Guid("01d791bb-d6b8-42e3-a1ba-6aec037cacc3"); }
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
                return Properties.Resources.materialform;
            }
        }
    }

}
