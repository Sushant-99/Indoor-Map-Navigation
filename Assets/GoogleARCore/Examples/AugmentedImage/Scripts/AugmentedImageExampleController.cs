//-----------------------------------------------------------------------
// <copyright file="AugmentedImageExampleController.cs" company="Google">
//
// Copyright 2018 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace GoogleARCore.Examples.AugmentedImage
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using GoogleARCore;
    using UnityEngine;
    using UnityEngine.UI;
    using UnityEngine.EventSystems;
    using System.IO;
    using System;
    using FantomLib;
    using System.Runtime.Serialization.Formatters.Binary;


    /// <summary>
    /// Controller for AugmentedImage example.
    /// </summary>
    /// <remarks>
    /// In this sample, we assume all images are static or moving slowly with
    /// a large occupation of the screen. If the target is actively moving,
    /// we recommend to check <see cref="AugmentedImage.TrackingMethod"/> and
    /// render only when the tracking method equals to
    /// <see cref="AugmentedImageTrackingMethod"/>.<c>FullTracking</c>.
    /// See details in <a href="https://developers.google.com/ar/develop/c/augmented-images/">
    /// Recognize and Augment Images</a>
    /// </remarks>
    public class AugmentedImageExampleController : MonoBehaviour
    {
        /// <summary>
        /// A prefab for visualizing an AugmentedImage.
        /// </summary>
        public AugmentedImageVisualizer AugmentedImageVisualizerPrefab;

        /// <summary>
        /// The overlay containing the fit to scan user guide.
        /// </summary>
     
         //To maintain the states of the app
         enum appState
        {
            findingSurface, //floor reference and point cloud
            findingMarker, //refrence to room direction. 
            mapping,  // creating the map
            navigating 

        }
  
        private Dictionary<int, AugmentedImageVisualizer> m_Visualizers
            = new Dictionary<int, AugmentedImageVisualizer>();

        private List<AugmentedImage> m_TempAugmentedImages = new List<AugmentedImage>();



        ///All variables for planes
        public GameObject pathNode;
        public GameObject destinationNode;

       
        public Camera FirstPersonCamera; //reference to user pose(position and orientation)


        //UI Elements
        public GameObject addDestination;
        public GameObject FitToScanOverlay;
        public Text feedbackText;
        public Text distanceText;
        public Text pathdata;
        public InputField destinationName;
        public GameObject destinationSpace;
        public GameObject buttonPrefab;
        public GameObject arrow;
        public GameObject navigationpanel;
        public GameObject mappingpanel;
        public GameObject navigationlist;
        public GameObject mainMenu;
        public GameObject nodatamsg;
        public TextToSpeechController textToSpeechControl;

        ///States 
        appState currentState = appState.findingSurface;
        appState lastKnownState = appState.findingSurface;
        bool startNodePlaced = false;
        float planeHeight = 0 ;
        GameObject tempNextNode;
        Quaternion planeRotation;
        private int nextNodeIndex = 0;
        int counter = 0;
        bool dataloaded = false;
        bool reloaddata= false;
        private List<GameObject> allNodes = new List<GameObject>(); //maintain list of all nodes(path and destination rendered on the real world)
        private List<NodeData> NodeDatas = new List<NodeData>();
        Dictionary<String, MapData> loadedmapdata = new Dictionary<string, MapData>();

        private List<GameObject> FinalPath = new List<GameObject>();
        private GameObject nextNode;  // Navigation - to keep track of the next closest node to the user for directing the arrow towards it.

        private GameObject currentNode;
        private Anchor imageAnchor; //reference to image anchor
        private string markername ;

        private Anchor PlaneAnchor; // Navigation - keep reference to a plane

        /// <summary>
        /// The Unity Awake() method.
        /// </summary>
        public void Awake()
        {
            // Enable ARCore to target 60fps camera capture frame rate on supported devices.
            // Note, Application.targetFrameRate is ignored when QualitySettings.vSyncCount != 0.
            Application.targetFrameRate = 60;
        }

        public void StartTTS(String speech)
        {
            if (textToSpeechControl != null)
                textToSpeechControl.StartSpeech(speech);
        }
        /// <summary>
        /// The Unity Update method.
        /// </summary>
        public void Update()
        {


            //Handle UI changes according to the app state.
            if (currentState != lastKnownState) {
                switch (currentState)
                {

                    case appState.findingSurface:
                        navigationpanel.SetActive(false);
                        mappingpanel.SetActive(false);
                        arrow.SetActive(false);
                        FitToScanOverlay.SetActive(false);
                        navigationlist.SetActive(false);
                        mainMenu.SetActive(false);
                        break;
                    case appState.findingMarker:
                        navigationpanel.SetActive(false);
                        mappingpanel.SetActive(false);
                        arrow.SetActive(false);
                        navigationlist.SetActive(false);
                        FitToScanOverlay.SetActive(true);
                        mainMenu.SetActive(false);
                        feedbackText.text = "Scan a marker";
                        break;
                    case appState.mapping:
                        navigationpanel.SetActive(false);
                        mappingpanel.SetActive(true);
                        arrow.SetActive(false);
                        navigationlist.SetActive(false);
                        FitToScanOverlay.SetActive(false);
                        mainMenu.SetActive(false);
                        feedbackText.text = "Place starting path node.";
                        break;
                    case appState.navigating:
                        navigationpanel.SetActive(true);
                        mappingpanel.SetActive(false);
                        arrow.SetActive(true);
                        FitToScanOverlay.SetActive(false);
                        mainMenu.SetActive(false);
                      
                        break;
                    default: Debug.Log("Broken system");
                        break;

                }
                lastKnownState = currentState;
            }


            // Exit the app when the 'back' button is pressed.
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            // Only allow the screen to sleep when not tracking.
            if (Session.Status != SessionStatus.Tracking)
            {
                Screen.sleepTimeout = SleepTimeout.SystemSetting;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            // Get updated augmented images for this frame.
            Session.GetTrackables<AugmentedImage>(
                m_TempAugmentedImages, TrackableQueryFilter.Updated);

            // Create visualizers and anchors for updated augmented images that are tracking and do
            // not previously have a visualizer. Remove visualizers for stopped images.
            foreach (var image in m_TempAugmentedImages)
            {
                AugmentedImageVisualizer visualizer = null;
                m_Visualizers.TryGetValue(image.DatabaseIndex, out visualizer);



                if (image.TrackingState == TrackingState.Tracking && visualizer == null)
                {
                    //  AugmentedImageTrackingMethod.
                    // Create an anchor to ensure that ARCore keeps tracking this augmented image.

                         //   m_Visualizers.Clear();
                            Anchor anchor = image.CreateAnchor(image.CenterPose);
                            imageAnchor = anchor; //take reference of the detected image
                            markername = image.Name; //getting name of the detected marker
                            visualizer = (AugmentedImageVisualizer)Instantiate(
                                AugmentedImageVisualizerPrefab, anchor.transform); // takes care of rendering the photo frame around the image
                            visualizer.Image = image;
                            m_Visualizers.Add(image.DatabaseIndex, visualizer);
                            getData(markername);
                            FitToScanOverlay.SetActive(false);
                  

                }
                else if (image.TrackingState == TrackingState.Stopped && visualizer != null)
                {  
                    m_Visualizers.Remove(image.DatabaseIndex);
                    GameObject.Destroy(visualizer.gameObject);
                }
            

                if(image.TrackingState == TrackingState.Tracking )
                {
                    if ((AugmentedImageTrackingMethod.FullTracking == image.TrackingMethod))
                    {
                        markername = image.Name;
                        imageAnchor = image.CreateAnchor(image.CenterPose);
                       

                        if (loadedmapdata.ContainsKey(image.Name) && reloaddata)
                        {
                            Anchor anchor = image.CreateAnchor(image.CenterPose);
                            imageAnchor = anchor;
                           

                            if (currentState == appState.navigating)
                            {

                                if (markername == image.Name)
                                {
                                    ReplaceAllNodes(image.Name);
                                }
                                else
                                {
                                    markername = image.Name;
                                    renderAllNodes(markername);
                                    populateDestinationMenu(markername);
                                    mainMenu.SetActive(true);


                                }

                            }
                                //  feedbackText.text = "Replaced Nodes again!";
                            reloaddata = false;
                        }
                        
                    }
                    else
                    {
                        reloaddata = true;
                      //  feedbackText.text = "No image in focus";
                    }
                }
            }


            dropPathNodes();


            //Code block to rotate arrow towards the next closest node to the user and also upate the nextnode.
            //Also to update the distance to the destination from the user.
            if (nextNode != null && currentState == appState.navigating)
            {

                /*  Transform nodeTransform = nextNode.transform;
                  nodeTransform.position = new Vector3(nextNode.transform.position.x,arrow.transform.position.y, nextNode.transform.position.z);
                  arrow.transform.LookAt(nodeTransform);*/
                Vector3 updatedPos = new Vector3(nextNode.transform.position.x, arrow.transform.position.y, nextNode.transform.position.z);
                Vector3 relativePos = arrow.transform.position - updatedPos ;
                Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.down);
                arrow.transform.rotation = rotation;

                //adjusting position so that they are at the same height so as to ensure the ditance is calculated properly and not based on the users height. 
                Vector3 NewNodePos = new Vector3(nextNode.transform.position.x, FirstPersonCamera.transform.position.y ,nextNode.transform.position.z); 

                float distance = distanceBetweenNodes(FirstPersonCamera.transform.position,NewNodePos); //calculating distance between user and the next closest node

                float dist = getDistanceFromDestination(nextNode, nextNodeIndex); //get total distance of user from destination

                //checking if the user is  < 0.3 meters to the nextnode so as to update nextnode .
                if (distance < 0.5)
                {
                     //checking if the path has nodes to update nextnode
                        if (nextNodeIndex < FinalPath.Count - 1)
                        {
                            nextNodeIndex = nextNodeIndex + 1;
                            nextNode = FinalPath[nextNodeIndex];
                        updatedPos = new Vector3(nextNode.transform.position.x, arrow.transform.position.y, nextNode.transform.position.z);
                        relativePos = arrow.transform.position - updatedPos;
                        rotation = Quaternion.LookRotation(relativePos, Vector3.down);
                        arrow.transform.rotation = rotation;
                        distanceText.text = "" + arrow.transform.localEulerAngles;
                        var roty = arrow.transform.localEulerAngles.y;
                        if(roty < 160)
                        {
                            var lefang = (int)Math.Abs(180 - roty);
                            StartTTS("Go Left "+ lefang + " degrees");
                        }
                        else if (roty > 200)
                        {
                            var lefang = (int)Math.Abs(roty - 180);
                            StartTTS("Go Right "+ lefang + " degrees");
                        }
                        else
                        {
                            StartTTS("Go Straight");
                        }


                    }
                        //condition if its the last node , which means we reached our destination
                    if (nextNodeIndex == FinalPath.Count - 1)
                    {
                        distanceText.text = "You have reached your destination";
                        StartTTS("You have reached " + lastDestinationSelected);
                        arrow.SetActive(false);
                    }

                }
                else
                { //update total distance from user to destination
                    if (!arrow.activeSelf)
                    {
                        arrow.SetActive(true);
                        
                    }
                   
                    // distanceText.text = "Distance : " + dist.ToString();
                }


                //Counter logic to recall DFS 
                /*       counter += 1;
                       if (counter > 2000)
                       {
                           RenderPathforSelectedDestination(lastDestinationSelected);
                           counter = 0;
                       } */




                //Code block to recall DFS if the user walks away from required path
                /*   float shortestDist = dist;
                   int updatednextnode = 0;
                   //GameObject closestGameObject = nextNode; 
                   for(int i= 0; i< FinalPath.Count-1; i++)
                   {
                       float totdist = getDistanceFromDestination(FinalPath[i],i); 
                       if(totdist< shortestDist)
                       {
                            shortestDist = totdist;
                           updatednextnode = i;
                          // closestGameObject = FinalPath.
                       }
                   }

                   if (updatednextnode != nextNodeIndex ||( nextNodeIndex == FinalPath.Count-1))
                   {
                       RenderPathforSelectedDestination(lastDestinationSelected);
                   }*/


                if (distance > 0.8f)
                {
                   
                        RenderPathforSelectedDestination(lastDestinationSelected);
                  
                }

            }

          
           
      

            //LOGIC TO HANDLE PLANE TOUCHES
            Touch touch;
            if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
            {
                //feedbackText.text = "Returning at no input";
                  return;
               
            }

            // Should not handle input if the player is pointing on UI.
            if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                //feedbackText.text = "Returninng over UI TOUCH";
                return;
            }


            TrackableHit hit;
            TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
                TrackableHitFlags.FeaturePointWithSurfaceNormal;

            if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit))
            {
                // Use hit pose and camera pose to check if hittest is from the
                // back of the plane, if it is, no need to create the anchor.
                if ((hit.Trackable is DetectedPlane) &&
                    Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
                        hit.Pose.rotation * Vector3.up) < 0)
                {
                    Debug.Log("Hit at back of the current DetectedPlane");
                }
                else
                {
                    // Choose the prefab based on the Trackable that got hit.
                    GameObject prefab;
                    if (hit.Trackable is DetectedPlane)
                    {
                        DetectedPlane detectedPlane = hit.Trackable as DetectedPlane;
                        //looking for only vertical planes.
                        if (detectedPlane.PlaneType != DetectedPlaneType.Vertical)
                        {

                            //FIRST STEP : getting reference to planeanchor and then updating state to findingMarker.  This is important so as to get more reference points for stability. 
                            if (currentState == appState.findingSurface)
                            {
                                PlaneAnchor = hit.Trackable.CreateAnchor(hit.Pose); 
                              
                                currentState = appState.findingMarker;
                                return;

                            }

                            if (currentState != appState.mapping)
                            {
                                return;
                            }

                            //New map : check if the first node is placed and set the prefab to path node
                            if (!startNodePlaced)
                            {
                                prefab = pathNode;
                                startNodePlaced = true;
                                planeHeight = hit.Pose.position.y;
                                
                                feedbackText.text = "START NODE PLACED";
                                StartTTS("Start node placed");


                            }
                            else
                            {
                                // New map : set prefab to destination node and enable UI to take in destination name. 
                                prefab = destinationNode;
                                addDestination.SetActive(true);
                            }

                            // Instantiate prefab at the hit pose.
                            var gameObject = Instantiate(prefab, hit.Pose.position, hit.Pose.rotation);

                            // Compensate for the hitPose rotation facing away from the raycast (i.e.
                            // camera).
                            gameObject.transform.Rotate(-90, 0, 0, Space.Self);

                            // Create an anchor to allow ARCore to track the hitpoint as understanding of
                            // the physical world evolves.
                            var anchor = hit.Trackable.CreateAnchor(hit.Pose);

                            // Make game object a child of the anchor.
                            gameObject.transform.parent = anchor.transform;
                            allNodes.Add(gameObject); //pushing new node to list 
                            currentNode = gameObject; 
                            planeRotation = gameObject.transform.rotation;

                            //updating NodeData with the new node data . NodeData is what is saved internally for later use.
                            if(prefab == pathNode)
                            {
                               // gameObject.tag = "step";
                                NodeData node = new NodeData(gameObject,"step");
                              
                                NodeDatas.Add(node);
                                
                            }
                            else
                            {
                               // gameObject.tag = "destination";
                                NodeData node = new NodeData(gameObject, "destination");
                      
                                NodeDatas.Add(node);
                            }
                         

                        }
                    }
                  

                    
                }
            }

        



        }

        //method for calulating distance between two nodes. 
        public float distanceBetweenNodes(Vector3 source, Vector3 destination)
        {
            float distance = Vector3.Distance(source, destination);
            return distance;
        }


        //method to update name property of last destination node.
        public void AddDestination()
        {
            string name = destinationName.text;
          
           
            NodeData destinationNode = NodeDatas[NodeDatas.Count - 1];
            if(destinationNode.nodeType == "destination")
            {
                destinationNode.name = name;
                feedbackText.text = "Added Destination " + name;
              
            }
          
            addDestination.SetActive(false);
        }

        //New Map : Change parent to reference image 
        //So that we get local position of nodes and update NodesData with the new position values. 
        public void ChangeParent()
        {
            /*MapData data = new MapData();
            foreach (NodeData sa in NodeDatas)
            {

                data.AddShape(sa);
            }
            printData(data, "BEFORE PARENTING");
*/
          //  distanceText.text = "PARENT";
            for (int i = 0; i < allNodes.Count; i++)
            {

                // distanceText.text = "transform :" + i.ToString();

                //GET PARENT 
                Transform oriparent = allNodes[i].transform.parent;
                


               // change parent to imageanchor.
                allNodes[i].transform.parent = imageAnchor.transform;
                
              //  distanceText.text = "pos :" + i.ToString();
                //Update nodedata with local position 
                Vector3 pos = allNodes[i].transform.localPosition;

              //  distanceText.text = "pos Update :" + i.ToString();
                NodeDatas[i].position =  new float[] { pos.x, pos.y, pos.z };

             //   distanceText.text = "rot :" + i.ToString();
                Vector3 rot = allNodes[i].transform.localEulerAngles;

              //  distanceText.text = "rot Update :" + i.ToString();
              //update nodedata with local rotation
                NodeDatas[i].rotation = new float[] { rot.x, rot.y, rot.z };

                allNodes[i].transform.parent = oriparent;
               // distanceText.text = "cHILD :" + i.ToString();
            }
          //  distanceText.text = "changed parent of :" + allNodes.Count.ToString();


       /*     MapData dataP = new MapData();
            foreach (NodeData sa in NodeDatas)
            {

                dataP.AddShape(sa);
            }
            printData(dataP, "AFTER PARENTING"); */
            
            SaveData();  // calling method to save nodedata to internal storage
        }
        
        //Method to saveData
        public void SaveData()
        {
            BinaryFormatter formatter = new BinaryFormatter();

            //saving data in the name of the detected marker.
            string path = Application.persistentDataPath + "/" + markername;
            FileStream stream = new FileStream(path, FileMode.Create);
            MapData data = new MapData();
            foreach (NodeData sa in NodeDatas )
            {
              
                data.AddShape(sa);
            }
            printData(data, "SAVED DATA");
            formatter.Serialize(stream, data);
            stream.Close();
            feedbackText.text = "Map successfully saved : " + markername;
            //  distanceText.text = "Saved data" +data.ToString();
        }

        //method to load data from internal storage
        public MapData LoadData(String markerName)
        {
           //get data from marker name
                string path = Application.persistentDataPath + "/" + markerName;
            mainMenu.SetActive(true);
                if (File.Exists(path))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    FileStream stream = new FileStream(path, FileMode.Open);
                    MapData data = formatter.Deserialize(stream) as MapData;
                    stream.Close();
               // distanceText.text = "Got data";
                //printData(data,"loaded data");
              
                //if there is data hide no data msg and show list of all destinations available; 
                nodatamsg.SetActive(false);
                navigationlist.SetActive(true);
                return data;

                }
                else
                {
                  //  distanceText.text = "file not found";
                    Debug.Log(" Shapen file not found in " + path);
                //if there is no data assocaited with the image show no data msg and hide destination list.
                nodatamsg.SetActive(true);
                navigationlist.SetActive(false);
                    return null;
                }
            
        }


        // UTILL method to print loaded data. 
        public void printData(MapData mapdata,string msg)
        {
            Debug.Log(msg);
            for (int i = 0; i < mapdata.data.Count; i++)
            {
                //get all data;
                // distanceText.text = i.ToString();
                float[] pos = mapdata.data[i].position;
                float[] rot = mapdata.data[i].rotation;
                string nodeType = mapdata.data[i].nodeType;
                string nodeName = mapdata.data[i].name;
                //Debug.Log("Node :"+i+"||("+pos[0]+","+pos[1] + "," + pos[2]+")||("+ rot[0] + "," + rot[1] + "," + rot[2] + ")||"+nodeType+"||"+nodeName);
              //  distanceText.text = msg + " Node :" + i + "||(" + pos[0] + "," + pos[1] + "," + pos[2] + ")||(" + rot[0] + "," + rot[1] + "," + rot[2] + ")||" + nodeType + "||" + nodeName;
            }
        }

        //called when image is detected. To check if there is data
        public void getData(String imageName)
        {
            MapData mapdata = LoadData(imageName);
            printData(mapdata, "getdata");
            loadedmapdata.Add(imageName, mapdata);
          
            // distanceText.text = "Load complete "+mapdata.data[0].position[0].ToString();
            if (mapdata != null)
            {

                renderAllNodes(imageName);
                populateDestinationMenu(imageName);
            }

            dataloaded = true; 
        }

        //method to read Mapdata and populate all the destination on a UI 
        public void populateDestinationMenu(String imageName)
        {

            MapData mapdata = loadedmapdata[imageName];

            foreach (Transform child in destinationSpace.transform)
            {
                GameObject.Destroy(child.gameObject);
            }

            for (int i = 0; i < mapdata.data.Count; i++)
            {
           
                string nodeType = mapdata.data[i].nodeType;
                string nodeName = mapdata.data[i].name;
                if (nodeType == "destination")
                {
                    GameObject go = Instantiate(buttonPrefab);
                    go.transform.SetParent(destinationSpace.transform);
                    var button = go.GetComponent<UnityEngine.UI.Button>();
                    button.GetComponentInChildren<Text>().text = nodeName; //set the button text to the destination name.\
                    feedbackText.text = "Selected " + nodeName;

                    button.onClick.AddListener(() => selectDestination());
                    distanceText.text = "Added "+nodeName;
                    Debug.Log("Button added " + nodeName);
                }
            }
         
          //  feedbackText.text = "Select the destination";
             
       }

        String lastDestinationSelected; 

        // method called when a destination is selected
        public void selectDestination()
        {

           // feedbackText.text = "SelectedDestination called";
            var currentEventSystem = EventSystem.current;

            var currentSelectedGameObject = currentEventSystem.currentSelectedGameObject;
            if (currentSelectedGameObject == null) { return; }
            string LoadName = currentSelectedGameObject.GetComponentInChildren<Text>().text; //get name of the destination selected. 
            lastDestinationSelected = LoadName;
           // feedbackText.text = "Renderpathforeselecteddestination called";
            RenderPathforSelectedDestination(LoadName); //get path to the destination and render it. 

            //feedbackText.text = "Renderpathforeselecteddestination done";
            currentState = appState.navigating;
            feedbackText.text = "Follow the path to: "+LoadName;
            //handling UI changes
            mainMenu.SetActive(false);


            // distanceText.text = "You selected : " + LoadName;
            Debug.Log("Button is being clicked : " + LoadName);
            Debug.Log(currentSelectedGameObject.name);
        }

        List<GameObject> allLoadedNodes = new List<GameObject>();

        //method to instantiate all loaded nodes from the MapData.
        public void renderAllNodes(String imageName)
        {

            DestroyAllNodes();
            MapData mapdata = loadedmapdata[imageName];

            string datastring = " ";
            feedbackText.text = "rendering nodes";
            for (int i = 0; i < mapdata.data.Count; i++)
            {
                //get all data;
               // distanceText.text = i.ToString();
                float[] pos = mapdata.data[i].position;
                float[] rot = mapdata.data[i].rotation;
                string nodeType = mapdata.data[i].nodeType;
                string nodeName = mapdata.data[i].name;
                datastring = datastring + " pos " + pos.ToString() + " rot " + rot.ToString() + " type " + nodeType + " name " + nodeName;

                GameObject prefab;
               if(nodeType == "destination")
                {
                    prefab = destinationNode;
                }
                else
                {
                    prefab = pathNode;
                }
                    var gameObject= Instantiate(prefab, Vector3.zero, Quaternion.identity);
                // Make game object a child of the anchor.
                gameObject.transform.parent = imageAnchor.transform;
               
                //changing the y value of gameobject to align with the plane.
                gameObject.transform.localPosition = new Vector3(pos[0], pos[1] ,pos[2]);
                gameObject.transform.localEulerAngles = new Vector3(rot[0], rot[1], rot[2]);
                // gameObject.tag = nodeType;
                gameObject.name = nodeName;
                gameObject.SetActive(false);

                //Changes to parent.
                Vector3 worldPos = gameObject.transform.position;
                Vector3 worldRot = gameObject.transform.eulerAngles;
                gameObject.transform.parent = PlaneAnchor.transform;

                gameObject.transform.position = worldPos;
                gameObject.transform.localEulerAngles = new Vector3(-90,0,0);

                gameObject.transform.localPosition = new Vector3(gameObject.transform.localPosition.x, 0 , gameObject.transform.localPosition.z);

                //Need to change position and rotation according to the plane.
                /*   Vector3 worldPos = gameObject.transform.position;
                   gameObject.transform.parent = PlaneAnchor.transform;
                   gameObject.transform.position = worldPos;*/
                // gameObject.transform.rotation = PlaneAnchor.transform.rotation;



                allLoadedNodes.Add(gameObject);
            

            }
            feedbackText.text = "DONE RENDERING";
           // distanceText.text = datastring;
        }


        public void ReplaceAllNodes(String imageName)
        {
            MapData mapdata = loadedmapdata[imageName];
            for (int i = 0; i < mapdata.data.Count; i++)
            {
                float[] pos = mapdata.data[i].position;
                float[] rot = mapdata.data[i].rotation;

                GameObject gameObject = allLoadedNodes[i];
                // Make game object a child of the anchor.
                gameObject.transform.parent = imageAnchor.transform;

                //changing the y value of gameobject to align with the plane.
                gameObject.transform.localPosition = new Vector3(pos[0], pos[1], pos[2]);
                gameObject.transform.localEulerAngles = new Vector3(rot[0], rot[1], rot[2]);
                // gameObject.tag = nodeType;
              
                gameObject.SetActive(true);

                //Changes to parent.
                Vector3 worldPos = gameObject.transform.position;
                Vector3 worldRot = gameObject.transform.eulerAngles;
                gameObject.transform.parent = PlaneAnchor.transform;

                gameObject.transform.position = worldPos;
                gameObject.transform.localEulerAngles = new Vector3(-90, 0, 0);

                gameObject.transform.localPosition = new Vector3(gameObject.transform.localPosition.x, 0, gameObject.transform.localPosition.z);

            }
        }


        public void DestroyAllNodes()
        {
            for(int i=0;i < allLoadedNodes.Count; i++)
            {
                Destroy(allLoadedNodes[i]);
            }

            allLoadedNodes.Clear();
        }

        //method to automatically drop at path nodes when the user walks to the destination  .
        public void dropPathNodes()
        {
            //check if starting path node is placed.
            if (startNodePlaced)
            {
                //feedbackText.text = "inside path";

                bool dropPathNode = true;
                //  distanceText.text = " allnodes " + allNodes.Count.ToString();

                //Go through all nodes to check if they are all 1.6m away from user . IF yes render another node at the position of the user. 
                for (int i = 0; i < allNodes.Count; i++)
                {

                    float distance = distanceBetweenNodes(FirstPersonCamera.gameObject.transform.position, allNodes[i].transform.position);
                    //  distanceText.text = " NEW Distance " + distance.ToString();
                    if (distance < 1.6f)
                    {
                        dropPathNode = false;
                        break;
                    }

                }

                if (dropPathNode)
                {

                    Vector3 cameraPos = FirstPersonCamera.transform.position;
                    Quaternion cameraRot = FirstPersonCamera.transform.rotation;
                    var gameObject = Instantiate(pathNode, cameraPos, cameraRot);

                    // Compensate for the hitPose rotation facing away from the raycast (i.e.
                    // camera).
                    gameObject.transform.Rotate(-90, 0, 0, Space.Self);

                    // Create an anchor to allow ARCore to track the hitpoint as understanding of
                    // the physical world evolves.
                    Pose pose = new Pose();
                    pose.position = cameraPos;
                    pose.rotation = cameraRot;
                    var anchor = Session.CreateAnchor(pose);



                    //Need to change position and rotation according to the plane.
                    gameObject.transform.rotation = planeRotation;
                    Vector3 position = gameObject.transform.position;
                    gameObject.transform.position = new Vector3(position.x, planeHeight, position.z);

                    // Make game object a child of the anchor.
                    gameObject.transform.parent = anchor.transform;
                    allNodes.Add(gameObject);
                    currentNode = gameObject;

                    // gameObject.tag = "step";
                    NodeData node = new NodeData(gameObject, "step");
                    NodeDatas.Add(node);

                }

                //feedbackText.text = "path :" + dropPathNode.ToString() + ":c:" + allNodes.Count.ToString();


            }
        }

        //Method to calculate shortest path to destination and render it.
        public void RenderPathforSelectedDestination(string destinationName)
        {
            List<GameObject> visitedNodes = new List<GameObject>();
            List<GameObject> Path = new List<GameObject>();

            GameObject closestNode = getClosestNodeFromUser(); //get closest node from user to use as the starting point 

            GameObject desinaticonObject = getDestination(destinationName); //get destination gameobject(node) from its name. 

           // feedbackText.text = "Got destination Node";
            if (desinaticonObject != null)
            {
                resultPaths.Clear();
                FinalPath.Clear();
              //  feedbackText.text = "Called DFS";
                DFS(closestNode, desinaticonObject, visitedNodes, Path); //call DFS to calculate all paths
                                                                         // distanceText.text = "DFS SUCCESSFULL";
               // feedbackText.text = "DFS DONE";
                //check if there is a path 
                if (resultPaths.Count > 0)
                {
                 
                  //  feedbackText.text = "Path found ";
                    renderPath(resultPaths); // render the shortest path to destination.
                 
                   // feedbackText.text = "Follow the path to: " + lastDestinationSelected;
                    //  feedbackText.text = "Path rendered" + resultPaths.ToString();
                }
                else
                {
                    feedbackText.text = "No path found";
                }
            }
            else
            {
              //  distanceText.text = "DESTINATION NULL";
            }
        }


        //get destiantion gameobject from its name
        public GameObject getDestination(string name)
        {
            for (int i = 0; i < allLoadedNodes.Count; i++)
            {
                if(allLoadedNodes[i].name == name)
                {
                    return allLoadedNodes[i];
                }

            }

            return null;
        }

        //UTIL to render all loaded nodes
        public void RenderAllNodes()
        {
            for(int  i=0; i< allLoadedNodes.Count; i++)
            {
                allLoadedNodes[i].SetActive(true);
            }
        }

        //Method to find the closest node from user .
        public GameObject getClosestNodeFromUser()
        {
            float mindistance = 1000;
            int index = 0;
            for(int i = 0; i < allLoadedNodes.Count; i++)
            {
                float distance = distanceBetweenNodes(FirstPersonCamera.gameObject.transform.position, allLoadedNodes[i].transform.position);
                if (distance < mindistance)
                {
                    mindistance = distance;
                    index = i;
                }

            }

            return allLoadedNodes[index];
        }


        private List<List<GameObject>> resultPaths = new List<List<GameObject>>();

        //DFS ALGO to find all paths to destination from the closest node to user.
        public Boolean DFS(GameObject node,GameObject destinationNode, List<GameObject> visitedNodes, List<GameObject> Path)
        {
            if (visitedNodes.Contains(node)||(node.tag=="destination" && !node.Equals(destinationNode)))
            {
                Debug.Log("false");
                return false;
            }

            if (node.Equals(destinationNode))
            {

              
                pathdata.text = "Path" + Path.ToString();
                visitedNodes.Remove(node);
                //Deep copy of Path list
                List<GameObject> copy = new List<GameObject>();

                foreach (var elt in Path)
                {
                    copy.Add(elt);
                }
                resultPaths.Add(copy); //update list of avialable pahs
                return true;
            }

            visitedNodes.Add(node);

            List<GameObject> neighbouringNodes = new List<GameObject>();
            for(int i=0;i<allLoadedNodes.Count;i++)
            {
                if (!visitedNodes.Contains(allLoadedNodes[i]))
                {
                    float distance = distanceBetweenNodes(node.transform.position, allLoadedNodes[i].transform.position);
                    if (distance < 1.4)
                    {
                        neighbouringNodes.Add(allLoadedNodes[i]);
                    }
                }

            }


            for(int i=0;i<neighbouringNodes.Count;i++)
            {
                Path.Add(neighbouringNodes[i]);
                DFS(neighbouringNodes[i],destinationNode, visitedNodes, Path);
                Path.Remove(neighbouringNodes[i]);
            }


            visitedNodes.Remove(node);

            return false;

        }


       

        //Method to render the shortest path avialable.
        public void renderPath(List<List<GameObject>> resultPaths)
        {
            for(int i = 0; i < allLoadedNodes.Count; i++)
            {
                allLoadedNodes[i].SetActive(false);
            }


            int min = 1000;
            int minPosition = 0;
            for(int i = 0; i < resultPaths.Count; i++)
            {
                if (resultPaths[i].Count < min)
                {
                    min = resultPaths[i].Count;
                    minPosition = i;
                }
            }

            for(int i=0;i<resultPaths[minPosition].Count;i++)
            {
            
                resultPaths[minPosition][i].SetActive(true);
                FinalPath.Add(resultPaths[minPosition][i]); // holds the shortest path 
            }

            //Initializing the first node for arrow 
            nextNodeIndex = 0;
            nextNode = FinalPath[nextNodeIndex]; //get first node from the final path
           
           // feedbackText.text = "Enabling "+ minPosition +"||"+  resultPaths.Count +" : " + resultPaths[minPosition].Count;
        }

        //Change state to mapping when the user wants to create a new map
        public void CreateNewMap()
        {
            currentState = appState.mapping;
        }
       

        //Method to enable and disable navigation list UI to switch destination.
        public void EnableDestinationList()
        {
            if (!mainMenu.activeSelf)
            {
                mainMenu.SetActive(true);
                navigationlist.SetActive(true);
                nodatamsg.SetActive(false);
            }
            else
            {
                mainMenu.SetActive(false);
                navigationlist.SetActive(false);
                nodatamsg.SetActive(false);
            }
        }

        //Method to calculate distance from user to destination ( Distance from user to closest node +  distance from closest node to destination)
        public float getDistanceFromDestination(GameObject node, int nodeIndex)
        {
            float distance = 0;

            //get distance of user from the closestnonde
        

            Vector3 NewNodePos = new Vector3(FirstPersonCamera.transform.position.x, node.transform.position.y, FirstPersonCamera.transform.position.z);

            float dist = distanceBetweenNodes(NewNodePos, node.transform.position);
            distance = distance + dist;

            //get distance from the closest node to the destination
            for (int i = nodeIndex; i < FinalPath.Count - 1; i++)
            {
                float dista = distanceBetweenNodes(FinalPath[i].transform.position, FinalPath[i+1].transform.position);
                distance = distance + dista;
            }

            return distance;

        }

    }
}
