using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System.IO;
using Rhino.DocObjects;
using System.Linq;

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
  private void RunScript(List<Plane> blockInstancePlanes, string libFilePath, string layerName, bool scatter, int scatterSeed, ref object preview)
  {
    if(libFilePath == null){
      RaiseWarning("No library file!");
      return;
    }
    else if(!libFilePath.EndsWith(".3dm")){
      RaiseWarning("Library file is not supported!");
      return;
    }
    using (Rhino.FileIO.File3dm lib = Rhino.FileIO.File3dm.Read(libFilePath))
    {
      List<DefinitionData> blockDefinitionDataList = LoadLibFile(lib, layerName);
      Print("Lib file loaded...");

      if(blockDefinitionDataList.Count == 0)
      {
        this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Error: No blocks Created");
        Print("Error: No blocks Created");
        return;
      }

      GetScatterInfo(blockDefinitionDataList, scatterSeed, blockInstancePlanes);

      if(scatter)
      {
        CreateDefinitions(blockDefinitionDataList, lib);
        Scatter(blockDefinitionDataList);
      }
      else
      {
        Print("Will create {0} Blocks From Layer: \"{1}\"", blockDefinitionDataList.Count, layerName);
        preview = Preview(blockDefinitionDataList);
      }
    }

  }

  // <Custom additional code> 
  private void RaiseWarning(String message){
    Print(message);
    this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, message);
  }

  private List<DefinitionData> LoadLibFile(Rhino.FileIO.File3dm lib, string layerName){

    Rhino.FileIO.File3dmObject[] objs = lib.Objects.FindByLayer(layerName);

    int sourceCount = objs.Length;
    Dictionary<string, DefinitionData> definitionDatas = new Dictionary<string, DefinitionData>();

    if(objs.Length == 0){
      this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No layer/objs in lib file!");
      Print("No layer/objs in lib file!");
      //return new List<DefinitionData>();
    }

    if(objs.Length != 0){
      Print("Way to go! Found {0} objs!", objs.Length.ToString());
      foreach (Rhino.FileIO.File3dmObject obj in objs)
      {
        if (obj.ComponentType != ModelComponentType.ModelGeometry) continue;
        ObjectAttributes refAttrs = obj.Attributes.Duplicate();
        if (obj.Geometry.GetType() == typeof(InstanceReferenceGeometry))
        {
          //Print("{0}", obj.Geometry.GetType());
          InstanceReferenceGeometry refGeo = (InstanceReferenceGeometry) obj.Geometry;
          InstanceDefinitionGeometry defGeo = lib.AllInstanceDefinitions.FindId(refGeo.ParentIdefId);

          Transform trans = refGeo.Xform;
          Line li = new Line(0, 0, 0, 0, 0, 1);
          li.Transform(trans);
          double scaleFactor = li.Length;

          trans = Transform.Scale(Point3d.Origin, scaleFactor);

          if (!definitionDatas.ContainsKey(defGeo.Name))
          {
            //Add Definition
            List<GeometryBase> defGeos = new List<GeometryBase>();
            List<ObjectAttributes> defAtts = new List<ObjectAttributes>();
            foreach (Guid id in defGeo.GetObjectIds())
            {
              Rhino.FileIO.File3dmObject obj2 = lib.Objects.FindId(id);
              if (obj2.Geometry.GetType() == typeof(InstanceReferenceGeometry))
              {
                this.Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not support block in block yet");
                Print("Not support block in block yet");
                continue;
              }
              GeometryBase subGeo = obj2.Geometry.Duplicate();
              defGeos.Add(subGeo);
              ObjectAttributes attr2 = obj2.Attributes.Duplicate();

              //Change Attr Layer Info Here
              CreateSubLayerAndAddToAttributes(ref attr2, lib);

              defAtts.Add(attr2);
            }
            if (defGeos.Count > 0)
            {
              definitionDatas[defGeo.Name] = new DefinitionData(defGeo.Name, defGeo.Description, refAttrs, Point3d.Origin, trans, defGeos, defAtts);
              //Print("Create a block definition \"{0}\" in layer {1}", defGeo.Name, createdLayerName);
            }
          }
        }
        else if (!(obj.IsSystemComponent || obj.Attributes.IsInstanceDefinitionObject))
        {
          //Print("OK2");
          //continue;
          GeometryBase geo = obj.Geometry.Duplicate();
          geo.Transform(Transform.Scale(Point3d.Origin, 1));
          BoundingBox box = geo.GetBoundingBox(true);
          if (box.Diagonal.Length != 0)
          {
            string blockName = layerName + " " + obj.Index.ToString();

            if (!definitionDatas.ContainsKey(blockName))
            {
              ObjectAttributes attr = obj.Attributes.Duplicate();
              definitionDatas[blockName] = new DefinitionData(blockName, "", refAttrs, box.PointAt(0.5, 0.5, 0), obj.Geometry, attr);
            }
          }
        }

        if (definitionDatas.Count >= sourceCount)
        {
          //Already Create SourceCount Blocks
          Print(string.Format("Get {0} blocks and pause read new block from libs", sourceCount));
        }
      }
      Print(string.Format("Get All {0} blocks from libs", definitionDatas.Values.Count));
      List<DefinitionData> blockDefinitionDataList = definitionDatas.Values.ToList();
      definitionDatas.Clear();
      return blockDefinitionDataList;
    }
    return  null;
  }

  private void GetScatterInfo(List<DefinitionData> blockDefinitionDataList, int scatterSeed, List<Plane> blockInstancePlanes){
    foreach(DefinitionData data in blockDefinitionDataList)
    {
      data.transforms = new List<Transform>();
    }
    Random rand = new Random(scatterSeed + blockInstancePlanes.Count);
    int maxValue = blockDefinitionDataList.Count;
    foreach(Plane plane in blockInstancePlanes)
    {
      DefinitionData data = blockDefinitionDataList[rand.Next(maxValue)];

      Transform trans;
      Transform basicScale = data.basicTrans;
      trans = Transform.PlaneToPlane(Plane.WorldXY, plane) * basicScale;
      data.transforms.Add(trans);
    }
  }

  private void CreateDefinitions(List<DefinitionData> blockDefinitionDataList, Rhino.FileIO.File3dm lib)
  {
    for(int j = 0;j < blockDefinitionDataList.Count;j++)
    {
      DefinitionData data = blockDefinitionDataList[j];
      InstanceDefinition def = RhinoDoc.ActiveDoc.InstanceDefinitions.Find(data.name);
      if(def == null)
      {
        CreateSubLayerAndAddToAttributes(ref data.refAttrs, lib);
        for(int i = 0;i < data.defAttrs.Count;i++)
        {
          ObjectAttributes attr2 = data.defAttrs[i];
          CreateSubLayerAndAddToAttributes(ref attr2, lib);
        }
        int index = RhinoDoc.ActiveDoc.InstanceDefinitions.Add(data.name, data.description, data.position, data.defGeos, data.defAttrs);
        data.index = index;
        Print("Create a block definition \"{0}\"", data.name);
      }
      else
      {
        CreateSubLayerAndAddToAttributes(ref data.refAttrs, lib);//already exist also neet to change bake attrs to current file layers
        data.index = def.Index;
        Print("Block definition \"{0}\" already exist", def.Name);
      }
    }
  }

  private void CreateSubLayerAndAddToAttributes(ref ObjectAttributes attr, Rhino.FileIO.File3dm lib)
  {
    Layer fileLayer = lib.AllLayers.FindIndex(attr.LayerIndex);
    Layer[] currentChileLayers = RhinoDoc.ActiveDoc.Layers.CurrentLayer.GetChildren();
    if(currentChileLayers != null)
    {
      foreach(Layer childLayer in currentChileLayers)
      {
        if(fileLayer.Name == childLayer.Name)
        {
          attr.LayerIndex = childLayer.Index;
          return;
        }
      }
    }
    //not exist layer so create a new layer
    fileLayer.ParentLayerId = RhinoDoc.ActiveDoc.Layers.CurrentLayer.Id;
    int index = RhinoDoc.ActiveDoc.Layers.Add(fileLayer);
    attr.LayerIndex = index;
    return;
  }

  private void Scatter(List<DefinitionData> blockDefinitionDataList)
  {
    foreach(DefinitionData data in blockDefinitionDataList)
    {
      foreach(Transform trans in data.transforms)
      {
        RhinoDoc.ActiveDoc.Objects.AddInstanceObject(data.index, trans, data.refAttrs);
      }
    }
  }

  private List<GeometryBase> Preview(List<DefinitionData> blockDefinitionDataList)
  {
    List<GeometryBase> preview = new List<GeometryBase>();
    foreach(DefinitionData data in blockDefinitionDataList)
    {
      foreach(Transform trans in data.transforms)
      {
        foreach(GeometryBase geo in data.defGeos)
        {
          GeometryBase geo2 = geo.Duplicate();
          geo2.Transform(trans);
          preview.Add(geo2);
        }
      }
    }
    return preview;
  }


  class DefinitionData
  {

    public string name;
    public string description;
    public Point3d position;
    public List<GeometryBase> defGeos;
    public List<ObjectAttributes> defAttrs;
    public ObjectAttributes refAttrs;
    public int index;
    public List<Transform> transforms;
    public Transform basicTrans;

    public DefinitionData(string name, string desc, ObjectAttributes refAttrs, Point3d position, Transform trans, List<GeometryBase> defGeos, List<ObjectAttributes> defAtts)
    {
      this.name = name;
      this.description = desc;
      this.position = position;
      this.defGeos = defGeos;
      this.defAttrs = defAtts;
      this.refAttrs = refAttrs;
      this.basicTrans = trans;
    }
    public DefinitionData(string name, string desc, ObjectAttributes refAttrs, Point3d position, GeometryBase defGeo, ObjectAttributes defAttr)
    {
      this.name = name;
      this.description = desc;
      this.position = position;
      this.defGeos = new List<GeometryBase>(){defGeo};
      this.defAttrs = new List<ObjectAttributes>(){defAttr};
      this.refAttrs = refAttrs;
      this.basicTrans = Transform.Identity;
    }
  }

  // </Custom additional code> 
}