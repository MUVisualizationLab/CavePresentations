using MiddleVR.Unity;
using MiddleVR;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class drawingManager : MonoBehaviour
{
    //state management
    public enum DrawingState { None, Drawing, Idle, Inactive };
    //None = no drawing has started yet
    //Drawing = currently drawing (buttons are held down)
    //Idle = drawings have been made but nothing's happening now
    //Inactive = drawings have been made but this isn't the main slide currently

    public DrawingState state = DrawingState.Inactive;

    //colors
    public static int currentColor;
    public static Color32[] lineColors;

    //line rendering
    public static GameObject prefab;

    void Start() {
        state = DrawingState.None;
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
    void Update()
    {
        if (state == DrawingState.Inactive) return;

        //keyboard
        if (MVR.DeviceMgr.IsKeyToggled(MVR.VRK_C)) cycleColor();
        if (MVR.DeviceMgr.IsKeyToggled(MVR.VRK_U)) undo();
        if (MVR.DeviceMgr.IsKeyToggled(MVR.VRK_I)) clearDrawings();

        //mouse
        if (Input.GetMouseButtonDown(0) && state != DrawingState.Drawing) StartCoroutine("drawWithMouse");
        if (Input.GetMouseButtonUp(0)) state = DrawingState.Idle;

        //flystick
    }

    IEnumerator drawWithMouse()
    {
        state = DrawingState.Drawing;

        //init the line object
        GameObject newLine = Instantiate(drawingManager.prefab, transform);
        newLine.transform.localPosition = Vector3.zero;
        newLine.name = "Line_" + transform.childCount.ToString("D3");
        LineRenderer lr = newLine.GetComponent<LineRenderer>();
        List<Vector3> points = new List<Vector3>();
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Physics.Raycast(ray, out hit);
        Vector3 previousPoint = hit.point;
        Debug.Log(previousPoint);

        while (state == DrawingState.Drawing) {
            ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit)) {
                if (Vector3.Distance(hit.point, previousPoint) > 0.001f) {
                    points.Add(newLine.transform.InverseTransformPoint(hit.point));
                    lr.positionCount = points.Count;
                    lr.SetPosition(points.Count - 1, (Vector3)points[points.Count - 1]);
                }
                previousPoint = hit.point;
                Debug.Log(previousPoint);

                yield return new WaitForEndOfFrame();
            }
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
        if (state == DrawingState.None) return;        
        if (state  == DrawingState.Drawing) state = DrawingState.Idle;

        GameObject lastDrawing = transform.GetChild(transform.childCount - 1).gameObject;
        Destroy(lastDrawing);
    }

    public void clearDrawings() {
        state = DrawingState.None;
        for (int i = transform.childCount; i >= 0; i--) { 
            Destroy(transform.GetChild(i).gameObject);        
        }
    }
}
