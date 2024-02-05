using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapData
{
    public List<NodeData> data = new List<NodeData>();

    public void AddShape(NodeData data)
    {

        this.data.Add(data);

    }
}
