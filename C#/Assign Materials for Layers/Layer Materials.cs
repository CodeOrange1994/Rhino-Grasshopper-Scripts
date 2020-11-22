using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino.DocObjects;
using System.Drawing;
using Rhino.Render;


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
  private void RunScript(bool activate, bool randomLayerColor, bool followLayerColor)
  {
    System.Array colorsArray = Enum.GetValues(typeof(KnownColor));
    KnownColor[] allColors = new KnownColor[colorsArray.Length];

    Array.Copy(colorsArray, allColors, colorsArray.Length);

    if (activate){
      RhinoDoc doc = RhinoDoc.ActiveDoc;
      for(int i = 0;i < doc.Layers.Count;i++){
        Layer layer = doc.Layers[i];
        if(layer.IsValid){
          if(randomLayerColor){
            if(layer.Color == Color.Black){
              currentColorIndex = colorIndex.Count;
              Print("So dull...Changing the color to " + currentColorIndex.ToString()
                + " " + allColors[currentColorIndex].ToString());
              layer.Color = Color.FromKnownColor(allColors[currentColorIndex]);
              colorIndex.Add(currentColorIndex);
              currentColorIndex++;
            }
          }

          if(layer.RenderMaterialIndex == -1){
            Print("Creating new material for " + layer.FullPath);
            Material newMat = new Material();
            newMat.Name = layer.FullPath;
            if(followLayerColor){
              newMat.DiffuseColor = layer.Color;
            }
            else{
              newMat.DiffuseColor = Color.White;
            }

            RenderMaterial newRenderMat = RenderMaterial.CreateBasicMaterial(newMat, doc);
            newRenderMat.Name = layer.FullPath;
            layer.RenderMaterial = newRenderMat;
          }
        }
      }

    }
  }

  // <Custom additional code> 
  private int currentColorIndex = 0;
  private List<int> colorIndex = new List<int>();
  // </Custom additional code> 
}