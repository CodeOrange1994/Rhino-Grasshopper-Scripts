using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;



/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
#region Utility functions
  /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
  /// <param name="text">String to print.</param>
  private void Print(string text) { /* Implementation hidden. */ }
  /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
  /// <param name="format">String format.</param>
  /// <param name="args">Formatting parameters.</param>
  private void Print(string format, params object[] args) { /* Implementation hidden. */ }
  /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj) { /* Implementation hidden. */ }
  /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj, string method_name) { /* Implementation hidden. */ }
#endregion

#region Members
  /// <summary>Gets the current Rhino document.</summary>
  private readonly RhinoDoc RhinoDocument;
  /// <summary>Gets the Grasshopper document that owns this script.</summary>
  private readonly GH_Document GrasshopperDocument;
  /// <summary>Gets the Grasshopper script component that owns this script.</summary>
  private readonly IGH_Component Component;
  /// <summary>
  /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
  /// Any subsequent call within the same solution will increment the Iteration count.
  /// </summary>
  private readonly int Iteration;
#endregion

  /// <summary>
  /// This procedure contains the user code. Input parameters are provided as regular arguments,
  /// Output parameters as ref arguments. You don't have to assign output parameters,
  /// they will have a default value.
  /// </summary>
  private void RunScript(List<Curve> paths, double pathOffset, double glassHeight, double glassGap, double panelDistance, double handrailHeight, Curve handrailProfile, Plane handrailProfilePlane, double balusterWidth, double balusterThickness, ref object glassPanels, ref object handrail, ref object balusters)
  {
    List<Surface> resultPanels = new List<Surface>();
    List<Brep> resultHandrail = new List<Brep>();
    List<Brep> resultBalusters = new List<Brep>();
    List<Curve> test = new List<Curve>();

    Vector3d extrudeVector = new Vector3d(0, 0, glassHeight);
    Vector3d handrailMove = new Vector3d(0, 0, handrailHeight);

    foreach (Curve path in paths){
      Curve[] glassBasePath = path.Offset(Plane.WorldXY, pathOffset,
        doc.ModelAbsoluteTolerance, CurveOffsetCornerStyle.Sharp);

      //Glass Panels
      //Without gap
      if(glassGap < 0.1){
        foreach(Curve gBasePath in glassBasePath){
          Surface glass;
          glass = Surface.CreateExtrusion(gBasePath, extrudeVector);
          resultPanels.Add(glass);
        }
      }
        //With gap
      else{
        foreach(Curve gBasePath in glassBasePath){
          for(double l = panelDistance; l < gBasePath.GetLength();l += panelDistance){
            Curve gPanelBase;
            if(gBasePath.GetLength() - l < panelDistance / 3){
              gPanelBase = TrimCurveAtLength(gBasePath,
                l - panelDistance + glassGap / 2, gBasePath.GetLength() - glassGap / 2);
            }
            else{
              gPanelBase = TrimCurveAtLength(gBasePath,
                l - panelDistance + glassGap / 2, l - glassGap / 2);
            }

            Surface glassPanel = Surface.CreateExtrusion(gPanelBase, extrudeVector);
            resultPanels.Add(glassPanel);

            if(gBasePath.GetLength() - l < panelDistance && gBasePath.GetLength() - l >= panelDistance / 3){
              Curve gPanelBaseEnd = TrimCurveAtLength(gBasePath,
                l + glassGap / 2, gBasePath.GetLength() - glassGap / 2);

              Surface glassPanelEnd = Surface.CreateExtrusion(gPanelBaseEnd, extrudeVector);
              resultPanels.Add(glassPanelEnd);
            }
          }
        }

        // Handrail
        foreach(Curve gBasePath in glassBasePath){
          Curve handrailPath = gBasePath.DuplicateCurve();
          handrailPath.Translate(handrailMove);
          Plane startFrame = GetStartFrameOnCurve(handrailPath);

          Curve hrPathProfile = OrientProfile(handrailProfile, handrailProfilePlane, startFrame);
          Brep[] handrailBreps = Brep.CreateFromSweep(handrailPath, hrPathProfile, false, doc.ModelAbsoluteTolerance);
          //List<Brep> hrBrepList = new List<Brep>();
          foreach(Brep b in handrailBreps){
            //hrBrepList.Add(b);
            resultHandrail.Add(b);
          }
          //resultHandrail.Add(hrBrepList);
        }

        //Balusters

        double balusterHeight = CalculateBalusterHeight(handrailHeight, handrailProfile, handrailProfilePlane);
        Brep balusterBrep = MakeBaluster(balusterWidth, balusterThickness, balusterHeight);

        //Make Block(To Be Update)

        foreach(Curve gBasePath in glassBasePath){
          Plane startHFrame = HorizontalFrameAtCurveLength(gBasePath, 0);
          Brep firstBaluster = OrientBaluster(balusterBrep, Plane.WorldXY, startHFrame);
          resultBalusters.Add(firstBaluster);
          for(double l = panelDistance; l < gBasePath.GetLength();l += panelDistance){
            if(gBasePath.GetLength() - l >= panelDistance / 3){
              Plane hFrame = HorizontalFrameAtCurveLength(gBasePath, l);
              Brep currentBaluster = OrientBaluster(balusterBrep, Plane.WorldXY, hFrame);
              resultBalusters.Add(currentBaluster);
            }
            if(gBasePath.GetLength() - l < panelDistance){
              Plane endHFrame = HorizontalFrameAtCurveLength(gBasePath, gBasePath.GetLength());
              Brep endBaluster = OrientBaluster(balusterBrep, Plane.WorldXY, endHFrame);
              resultBalusters.Add(endBaluster);
            }
          }
        }
      }
    }


    glassPanels = resultPanels;
    handrail = resultHandrail;
    balusters = resultBalusters;

    //A = test;
  }

  // <Custom additional code> 
  private Curve TrimCurveAtLength(Curve curve, double length1, double length2){
    double para1,para2;
    curve.LengthParameter(length1, out para1);
    curve.LengthParameter(length2, out para2);
    Curve trimmedCurve = curve.Trim(para1, para2);
    return trimmedCurve;
  }
  private Plane GetStartFrameOnCurve(Curve curve){
    Plane startFrame = new Plane();
    double startPara;
    curve.NormalizedLengthParameter(0, out startPara);
    curve.PerpendicularFrameAt(startPara, out startFrame);
    return startFrame;
  }

  private Curve OrientProfile(Curve profile, Plane plane1, Plane plane2){
    Transform transform1 = Transform.ChangeBasis(Plane.WorldXY, plane1);
    Transform transform2 = Transform.ChangeBasis(plane2, Plane.WorldXY);
    Curve orientedProfile = profile.DuplicateCurve();
    orientedProfile.Transform(transform1);
    orientedProfile.Transform(transform2);
    return orientedProfile;
  }

  private double CalculateBalusterHeight(double handrailHeight, Curve profile, Plane profilePlane){
    BoundingBox pBBox = profile.GetBoundingBox(false);
    double balusterHeight = handrailHeight - pBBox.Diagonal.Y;
    //Print(balusterHeight.ToString());
    return balusterHeight;
  }

  private Brep MakeBaluster(double width, double thickness, double height){
    Box box = new Box(Plane.WorldXY, new Interval(-width / 2, width), new Interval(10, 10 + thickness), new Interval(0, height));
    Brep baluster = Brep.CreateFromBox(box);
    return baluster;
  }

  private Plane HorizontalFrameAtCurveLength(Curve curve, double length){
    double para;
    curve.LengthParameter(length, out para);
    Plane frame;
    curve.PerpendicularFrameAt(para, out frame);
    Vector3d newX = new Vector3d(-frame.ZAxis.X, -frame.ZAxis.Y, 0);
    Vector3d newY = new Vector3d(-frame.XAxis.X, -frame.XAxis.Y, 0);
    Plane horizontalFrame = new Plane(frame.Origin, newX, newY);
    return horizontalFrame;
  }

  private Brep OrientBaluster(Brep baluster, Plane plane1, Plane plane2){
    Transform transform1 = Transform.ChangeBasis(Plane.WorldXY, plane1);
    Transform transform2 = Transform.ChangeBasis(plane2, Plane.WorldXY);
    Brep orientedbaluster = baluster.DuplicateBrep();
    orientedbaluster.Transform(transform1);
    orientedbaluster.Transform(transform2);
    return orientedbaluster;
  }

  private void MakeBlock(List<Brep> geometries, String blockName){
  }
  // </Custom additional code> 
}