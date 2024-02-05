using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NodeData
{
    public float[] position;
    public float[] rotation;
    public string nodeType;
    public string name;

   public NodeData(GameObject node, string nodename)
    {
        position = new float[3];
        position[0] = node.transform.localPosition.x;
        position[1] = node.transform.localPosition.y;
        position[2] = node.transform.localPosition.z;
        rotation = new float[3];
        rotation[0] = node.transform.localEulerAngles.x;
        rotation[1] = node.transform.localEulerAngles.y;
        rotation[2] = node.transform.localEulerAngles.z;
        nodeType = node.tag;
        name = nodename;
    }
}
