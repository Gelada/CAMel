﻿using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;

using Rhino.Geometry;

using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV.XImgproc;

namespace CAMel
{
    public static class ReadPhoto
    {
        public static  List<Curve> trace(string filename, int Blur, int Jump, bool debug, out List<string> times)
        {
            Stopwatch watch = Stopwatch.StartNew();

            string filepath = System.IO.Path.GetDirectoryName(filename);

            List<Curve> curves = new List<Curve>();
            times = new List<string>();

            Mat imgMat = new Mat(filename);

            CvInvoke.CvtColor(imgMat, imgMat, ColorConversion.Bgr2Gray);

            CvInvoke.GaussianBlur(imgMat, imgMat, new Size(2 * Blur + 1, 2 * Blur + 1), 0, 0);

            if (debug)
            {
                watch.Stop();
                times.Add("Open File: " + watch.ElapsedMilliseconds + " ms");
                watch = Stopwatch.StartNew();
            }

            Mat Thresh = new Mat();
            CvInvoke.AdaptiveThreshold(imgMat, Thresh, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 51, 15);
            imgMat.Dispose();

            CvInvoke.BitwiseNot(Thresh, Thresh);

            if (debug)
            {
                watch.Stop();
                times.Add("Threshold: " + watch.ElapsedMilliseconds + " ms");
                Thresh.Save(System.IO.Path.Combine(filepath, "CAMelTrace_Thresholded.png"));
                watch = Stopwatch.StartNew();
            }

            // Find the outer contour, this will fill in any holes in the path
            // It does assume that no paths are loops. 
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(Thresh, contours, null, RetrType.External, ChainApproxMethod.ChainApproxNone);

            VectorOfVectorOfPoint usecontours = new VectorOfVectorOfPoint();
            for (int i = 0; i < contours.Size; i++)
            {
                if (contours[i].Size > 10)
                {
                    usecontours.Push(contours[i]);
                }
            }

            Mat ContourBM = new Mat(Thresh.Size, Thresh.Depth, 1);
            ContourBM.SetTo(new MCvScalar(0));
            Thresh.Dispose();

            CvInvoke.DrawContours(ContourBM, usecontours, -1, new MCvScalar(255), -1);

            if (debug)
            {
                watch.Stop();
                times.Add("Redraw Main paths: " + watch.ElapsedMilliseconds + " ms");
                ContourBM.Save(System.IO.Path.Combine(filepath, "CAMelTrace_Redrawn.png"));
                watch = Stopwatch.StartNew();
            }

            // Thin the region to get the center of the curve, with some small branches

            Mat thin = new Mat();
            XImgprocInvoke.Thinning(ContourBM, thin, ThinningTypes.GuoHall);
            ContourBM.Dispose();

            if (debug)
            {
                watch.Stop();
                times.Add("Thin: " + watch.ElapsedMilliseconds + " ms");
                thin.Save(System.IO.Path.Combine(filepath, "CAMelTrace_Thinned.png"));
                watch = Stopwatch.StartNew();
            }

            // Now remove triple points to cut off branches
            Mat thinf = new Mat();
            System.Drawing.Point an = new System.Drawing.Point(-1, -1);
            ElementShape sh = ElementShape.Rectangle;
            Mat element = CvInvoke.GetStructuringElement(sh, new Size(3, 3), an);

            Mat thrpts = new Mat(thin.Size, DepthType.Cv8U, 1);

            CvInvoke.Threshold(thin, thinf, 128, 64, ThresholdType.Binary);
            CvInvoke.Filter2D(thinf, thrpts, element, an); // counts neighbours of a point
            CvInvoke.Threshold(thrpts, thrpts, 200, 255, ThresholdType.Binary); // selects points with 3 or more neighbours
            CvInvoke.BitwiseNot(thrpts, thrpts);
            CvInvoke.BitwiseAnd(thin, thrpts, thin);

            if (debug)
            {
                watch.Stop();
                times.Add("Remove 3 points: " + watch.ElapsedMilliseconds + " ms");
                thin.Save(System.IO.Path.Combine(filepath, "CAMelTrace_JustLines.png"));
                watch = Stopwatch.StartNew();
            }

            // Now we have a collection of 1 pixel thick curves, we can vectorize

            contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(thin, contours, null, RetrType.External, ChainApproxMethod.ChainApproxNone);
            VectorOfPoint cont;

            // The contours are loops and there are small paths we want to ignore.
            // We run ApproxPolyDP to simplify the curve
            // Finally we convert to a Rhino curve

            for (int i = 0; i < contours.Size; i++)
            {
                VectorOfPoint sco = new VectorOfPoint();
                if (contours[i].Size > 4)
                {
                    cont = new VectorOfPoint();
                    int j = 1;
                    while (contours[i][j - 1] != contours[i][j + 1] && j < contours[i].Size - 1) { j++; }
                    System.Drawing.Point[] pt = new System.Drawing.Point[2] { contours[i][j], contours[i][j + 1] };
                    cont.Push(pt);
                    j += 2;
                    while (j < contours[i].Size && contours[i][j - 2] != contours[i][j])
                    {
                        pt = new System.Drawing.Point[1] { contours[i][j] };
                        cont.Push(pt);
                        j++;
                    }
                    CvInvoke.ApproxPolyDP(cont, sco, 1, false);
                    //sco = cont;
                    List<Point3d> c = new List<Point3d>();
                    for (j = 0; j < sco.Size; j++) { c.Add(pt2R(sco[j])); }
                    curves.Add(new PolylineCurve(c));
                }
            }

            if (debug)
            {
                watch.Stop();
                times.Add("Find contours 2: " + watch.ElapsedMilliseconds + " ms");
                watch = Stopwatch.StartNew();
            }

            // In Rhino we join the remaining curves, healing the triple points we removed
            // Hopefully ending up with something close to the intended result. 
            // This should be replaced with an algorithm that creates the longest 
            // possible curves, then deletes everything under a threshold. 

            curves.Sort(delegate (Curve x, Curve y)
            {
                return y.GetLength().CompareTo(x.GetLength());
            });

            List<Curve> Jcurves = new List<Curve>();
            List<Curve> Tcurves = new List<Curve>();

            if (curves.Count > 0) { Tcurves.Add(curves[0]); }
            Jcurves = Tcurves;
            for (int i = 1; i < curves.Count; i++)
            {
                Tcurves.Add(curves[i]);
                Jcurves = new List<Curve>();
                Jcurves.AddRange(Curve.JoinCurves(Tcurves, 10, false));
                Tcurves = Jcurves;
            }

            // Find centre at 0, remove short curves and do final join.

            Tcurves = Jcurves;
            Jcurves = new List<Curve>();

            BoundingBox BB = new BoundingBox();
            for (int i = 0; i < Tcurves.Count; i++)
            {
                if (Tcurves[i].GetLength() > Jump * 4)
                {
                    Jcurves.Add(Tcurves[i]);
                    BB.Union(Tcurves[i].GetBoundingBox(false));
                }
            }
            Tcurves = Jcurves;
            Jcurves = new List<Curve>();
            Jcurves.AddRange(Curve.JoinCurves(Tcurves, Jump, false));

            // Move to centre.
            for (int i = 0; i < Jcurves.Count; i++)
            { Jcurves[i].Translate(-(Vector3d)BB.Center); }

            if (debug)
            {
                watch.Stop();
                times.Add("Join Curves: " + watch.ElapsedMilliseconds + " ms");
            }
            watch.Stop();

            return Jcurves;
        }

        private static Point3d pt2R(System.Drawing.Point p)
        {
            return new Point3d(p.X, -p.Y, 0);
        }
    }
}