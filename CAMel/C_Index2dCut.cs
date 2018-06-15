using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using CAMel.Types;
using CAMel.Types.MaterialForm;
using ClipperLib;

namespace CAMel
{
    public class C_Index2dCut : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the WriteCode class.
        /// </summary>
        public C_Index2dCut()
            : base("Two D Operation", "Two D",
                "Create a Machine Operations cutting out 2D shapes.",
                "CAMel", " Operations")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Cut Path", "C", "Outline of object to cut out.", GH_ParamAccess.item);
            pManager.AddVectorParameter("Direction", "D", "Direction of the tool.", GH_ParamAccess.item, new Vector3d(0, 0, 1));
            pManager.AddNumberParameter("Offset", "O", "Offset Curve, 0 for none, -1 for interior, 1 for exterior. Actually uses half tool thickness scaled by this value, so variation by toolpath is possible.", GH_ParamAccess.item,0);
            pManager.AddNumberParameter("Lead in/out", "L", "Lead in and out factor, 0 for none, -1 for interior, 1 for exterior. Actually uses machines' standard lead in and out scaled by this value, so variation by toolpath is possible.", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Tabbing","T", "Add tabs if the machine requires. (Not yet implemented)",GH_ParamAccess.item, false);
            pManager.AddGenericParameter("Material/Tool", "MT", "The material to cut and the tool to do it", GH_ParamAccess.item);
            pManager.AddGenericParameter("MaterialForm", "MF","The shape of the material to cut", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Operation", "O", "Machine Operation", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve C = null;
            Vector3d D = new Vector3d();
            double Os = 0;
            double leadInOut = 0;
            bool tabs = false;

            MaterialTool MT = null;
            IMaterialForm MF = null;

            if (!DA.GetData(0, ref C)) return;
            if (!DA.GetData(1, ref D)) return;
            if (!DA.GetData(2, ref Os)) return;
            if (!DA.GetData(3, ref leadInOut)) return;
            if (!DA.GetData(4, ref tabs)) return;
            if (!DA.GetData(5, ref MT)) return;
            if (!DA.GetData(6, ref MF)) return;

            if(!C.IsClosed && Os != 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Curves that are not closed will not be offset.");
                Os = 0;
            }
            // Note multiplication will give negative only if one if positive and the other negative.
            if(Os*leadInOut < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Offsetting and Lead in/out on different sides of the curve.");
            }

            // Shift curve to XY plane

            Plane P = new Plane(Point3d.Origin, D);
            C.Transform(Transform.PlaneToPlane(P, Plane.WorldXY));

            // ensure the curve is anticlockwise
            if (Os != 0) {
                if (C.ClosedCurveOrientation(Transform.Identity) == CurveOrientation.Clockwise)
                {
                    C.Reverse();
                }
            }

            // record the average Z location of the curve
            BoundingBox BB = C.GetBoundingBox(true);
            double useZ = (BB.Max.Z + BB.Min.Z) / 2.0;

            // turn the curve into a ToolPath

            ToolPath TP = new ToolPath("", MT, MF);
            TP.ConvertCurve(C, D);

            // offSet
            List<PolylineCurve> osC = new List<PolylineCurve>();
            if (Os == 0) { osC.Add(TP.GetLine()); }
            else
            {
                if (Os > 0) { osC = Offsetting.offset(TP.GetLine(), MT.toolWidth / 2.0); }
                else { osC = Offsetting.offset(TP.GetLine(), -MT.toolWidth / 2.0); }
            }

            // create Operation

            MachineOperation Op = new MachineOperation();

            Op.name = "2d Cut Path";

            int i = 1;
            foreach (PolylineCurve c in osC)
            {
                // Create and add name, material/tool and material form
                TP = new ToolPath("Cut", MT, MF);
                if (osC.Count > 1) { TP.name = TP.name + " " + i.ToString(); }
                i++;

                // Additions for toolpath
                TP.Additions.insert = true;
                TP.Additions.retract = true;
                TP.Additions.stepDown = true;
                TP.Additions.sdDropStart = true;
                TP.Additions.sdDropMiddle = 8 * MF.safeDistance;
                TP.Additions.sdDropEnd = true;
                TP.Additions.threeAxisHeightOffset = true;

                // return to original orientation

                c.Translate(new Vector3d(0, 0, -useZ));
                c.Transform(Transform.PlaneToPlane(Plane.WorldXY, P));

                // Add to Operation
                TP.ConvertCurve(c, D);
                Op.Add(TP);              
            }

            DA.SetData(0, Op);
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
                return Properties.Resources.drilloperations;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{D5C2A2B0-D57C-45D6-BF96-90536C726A04}"); }
        }
    }
}