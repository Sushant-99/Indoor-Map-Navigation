using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Runtime.Serialization.Formatters.Binary;

public class SaveLoadSystem : MonoBehaviour
{
    /*public static MapData SaveShapes(string filename)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        string path = Application.persistentDataPath + "/" + filename + ".go";
        FileStream stream = new FileStream(path, FileMode.Create);
        Node[] sha = (Node[])GameObject.FindObjectsOfType(typeof(Node));
        Debug.Log("Count " + sha.Length);
        MapData data = new MapData();
        foreach (Node sa in sha)
        {
            NodeData sd = new NodeData(sa);
            data.AddShape(sd);
        }
        formatter.Serialize(stream, data);
        stream.Close();
        return data;
    }
    public static MapData LoadShapes(string filename)
    {
        string path = Application.persistentDataPath + "/" + filename;
        if (File.Exists(path))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(path, FileMode.Open);
            MapData data = formatter.Deserialize(stream) as MapData;
            stream.Close();
            return data;

        }
        else
        {
            Debug.Log(" Shapen file not found in " + path);
            return null;
        }
    }


    public static void DeleteData(string filename)
    {
        string path = Application.persistentDataPath + "/" + filename;
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }*/
}
