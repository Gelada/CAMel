using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.GUI.Base;
using Rhino.Geometry;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV.XImgproc;
using System.Windows.Forms;

namespace CAMel
{
    public class C_PhotoContours : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateToolPath class.
        /// </summary>
        public C_PhotoContours()
            : base("Photo Surface", "PhotoS",
                "Create a surface from a greyscale image",
                "CAMel", " Photos")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File", "F", "Name of image file", GH_ParamAccess.item);
            pManager.AddNumberParameter("Height", "H", "Height of white", GH_ParamAccess.item,1);
            pManager.AddNumberParameter("Blur", "B", "Radius of Blur (in pixels), negative numbers will sharpen", GH_ParamAccess.item,0);
            pManager.AddBooleanParameter("Renormalize", "R", "If false use grey values, if true renomralise so surface height goes from 0 to 1",GH_ParamAccess.item,false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Height Mesh",GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filename = string.Empty;
            double Blur = 0;
            double H = 1;
            Boolean reNorm = false;

            if (!DA.GetData(0, ref filename)) { return; }
            string filepath = System.IO.Path.GetDirectoryName(filename);

            if (!DA.GetData(1, ref H)) { return; }
            if (!DA.GetData(2, ref Blur)) { return; }
            if (!DA.GetData(3, ref reNorm)) { return; }

            Bitmap BM = (Bitmap)Image.FromFile(filename);
            Image<Gray, Byte> img = new Image<Gray, Byte>(BM);

            if (Blur > 0)
            {
                CvInvoke.GaussianBlur(img, img, new Size(2 * (int)Math.Floor(Blur) + 1, (int)Math.Floor(Blur)), 0, 0);
            } else if ( Blur < 0)
            {
                uSM(img, img, -(int)Math.Ceiling(Blur), 1.5);
            }

            img=img.Resize(.2, Inter.Area);

            double low = 0;
            double range = H;

            Mesh M = new Mesh();
            Bitmap BiMp = img.Bitmap;

            for(int i=0;i<img.Height;i++)
            {
                for(int j=0;j<img.Width;j++)
                {
                    double h = (double)BiMp.GetPixel(j, i).R;
                    h = low + range * h / 255.0;
                    M.Vertices.Add(new Point3d(j / 100.0, i / 100.0, h));
                    if(i>0&&j>0)
                    {
                        int p = i * img.Width + j;
                        MeshFace face = new MeshFace(p,p-1,p-1-img.Width,p-img.Width);
                        M.Faces.AddFace(face);
                    }
                }
            }

            DA.SetData(0, M);
        }

        void uSM(Image<Gray, Byte> img, Image<Gray, Byte> op, int r, double w)
        {
            Image<Gray, Byte> blur = new Image<Gray, byte>(img.Size);
            // create weighted mask
            CvInvoke.GaussianBlur(img, blur,new Size(2 * r + 1, 2 * r + 1),0,0);
            blur = img - blur;
            blur *= w;
            //sum with the original image 
            op = img + blur; 
        }

        Point3d pt2R(System.Drawing.Point p)
        {
            return new Point3d(p.X, -p.Y, 0);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
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
                return Properties.Resources.photocontour;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{043136B0-C887-465A-BC1E-6B1BBCCE5137}"); }
        }
    }
}