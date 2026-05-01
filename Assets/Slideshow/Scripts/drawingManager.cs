using MiddleVR;
using MiddleVR.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class drawingManager : MonoBehaviour
{
    //state management
    public enum DrawingState { None, Drawing, Idle, Inactive };
    //None = no drawing has started yet
    //Drawing = currently drawing (buttons are held down)
    //Idle = drawings have been made but nothing's happening now
    //Inactive = drawings have been made but this isn't the main slide currently

    public DrawingState state = DrawingState.Inactive;
    private Vector3 mainSlide;

    //colors
    public static int currentColor;
    public static Color32[] lineColors;
    private double lastClick = 0d;

    //line rendering caches
    public static GameObject prefab;
    private Camera cam;
    private Transform hand;

    void Start() {
        state = DrawingState.None;

        cam = GameObject.Find("HeadNode").GetComponentInChildren<Camera>(false);
        hand = GameObject.Find("HandNode").transform;
        mainSlide = GameObject.Find("Slide_Center").transform.position;
        lastClick = MVR.Kernel.GetTime();

        if (drawingManager.prefab == null) {
            drawingManager.prefab = Resources.Load("Drawing") as GameObject;
            drawingManager.currentColor = -1;
            drawingManager.lineColors = new Color32[] { 
                new Color32(255, 248, 043, 0),      //yellow
                new Color32(218, 001, 000, 0),      //red
                new Color32(028, 162, 255, 0),      //light blue
                new Color32(006, 019, 068, 0)       //dark blue
            };
            cycleColor();
        }
    }

    // Update is called once per frame
    void Update() {
        if (state == DrawingState.Inactive) return;
        autoDisable();

        //keyboard        
        if (MVR.DeviceMgr.IsKeyToggled(MVR.VRK_U)) undo();
        if (MVR.DeviceMgr.IsKeyToggled(MVR.VRK_I)) clearDrawings();
        if (MVR.DeviceMgr.IsKeyToggled(MVR.VRK_C)) cycleColor();

        //flystick
        if (MVR.DeviceMgr.IsWandButtonPressed(0) && state != DrawingState.Drawing) StartCoroutine("draw", "flystick");
        if (!MVR.DeviceMgr.IsWandButtonToggled(0) && state == DrawingState.Drawing) state = DrawingState.Idle;
        if (MVR.DeviceMgr.IsWandButtonToggled(1)) undo();
        if (MVR.DeviceMgr.IsWandButtonToggled(3)) cycleColor();        

        //mouse
        if (Application.isEditor) {
            if (Input.GetMouseButtonDown(0) && state != DrawingState.Drawing) StartCoroutine("draw", "mouse");
            if (Input.GetMouseButtonUp(0)) state = DrawingState.Idle;
        }
    }

    IEnumerator draw(string origin) {
        state = DrawingState.Drawing;

        //init the line object
        GameObject newLine = Instantiate(drawingManager.prefab, transform);
        newLine.transform.localPosition = Vector3.zero;
        newLine.name = "Line_" + transform.childCount.ToString("D3");
        LineRenderer lr = newLine.GetComponent<LineRenderer>();
        List<Vector3> points = new List<Vector3>();

        Ray ray;        
        if (origin == "mouse") ray = cam.ScreenPointToRay(Input.mousePosition);
        else ray = new Ray(hand.position, hand.forward);

        RaycastHit hit;
        Physics.Raycast(ray, out hit);
        Vector3 previousPoint = hit.point;

        while (state == DrawingState.Drawing) {
            if (origin == "mouse") ray = cam.ScreenPointToRay(Input.mousePosition);
            else ray = new Ray(hand.position, hand.forward);

            if (Physics.Raycast(ray, out hit)) {
                if (Vector3.Distance(hit.point, previousPoint) > 0.001f) {
                    points.Add(newLine.transform.InverseTransformPoint(hit.point));
                    lr.positionCount = points.Count;
                    lr.SetPosition(points.Count - 1, (Vector3)points[points.Count - 1]);
                }
                previousPoint = hit.point;
            } 
            yield return new WaitForEndOfFrame();
        }

        //todo: network sync
        yield return new WaitForEndOfFrame();
    }

    public void cycleColor() {
        Material m = drawingManager.prefab.GetComponent<LineRenderer>().sharedMaterial;
        drawingManager.currentColor += 1;

        if (drawingManager.currentColor >= drawingManager.lineColors.Length) drawingManager.currentColor = 0;
        m.color = drawingManager.lineColors[drawingManager.currentColor];
        Debug.Log(m.color);
    }
    

    public void undo() {
        if (state == DrawingState.None) return;                         //nothing to undo       
        if (state == DrawingState.Drawing) state = DrawingState.Idle;   //could you click undo while drawing?

        //enable doubleclicking undo
        if (MVR.Kernel.GetTime() - lastClick <= 0.45d) clearDrawings();
        else {
            GameObject lastDrawing = transform.GetChild(transform.childCount - 1).gameObject;
            Destroy(lastDrawing);
        }
        
        lastClick = MVR.Kernel.GetTime();
    }

    public void clearDrawings() {
        if (state == DrawingState.None) return;       

        for (int i = transform.childCount - 1; i >= 0; i--) { 
            Destroy(transform.GetChild(i).gameObject);        
        }
        state = DrawingState.None;
    }

    public void autoDisable() {
        if (Vector3.Distance(transform.position, mainSlide) > 0.1f)
            state = DrawingState.Inactive;
    }

    public void enableDrawing()
    {
        state = DrawingState.None;
    }
}
