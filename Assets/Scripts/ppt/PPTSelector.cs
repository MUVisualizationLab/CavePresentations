using MiddleVR;
using MiddleVR.Unity;
using SimpleFileBrowser;
using System.Collections;
using System.IO;
using UnityEngine;

public class PPTSelector : MonoBehaviour
{
    public slideLoader SlideLoader;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!MVR.ClusterMgr.IsServerOrNoClusterConfig()) {
            UnityEngine.Debug.Log("The filebrowser will not appear on this client.");
            return;
        }

        //See Documentation at https://github.com/yasirkula/UnitySimpleFileBrowser 
        //set some browser default settings
        FileBrowser.SetFilters(false, new FileBrowser.Filter("Presentations", ".ppt", ".pptx", ".odp"));
        FileBrowser.AddQuickLink("Streaming Assets", Application.streamingAssetsPath, null);

        StartCoroutine(ShowLoadDialogCoroutine());


    }
    IEnumerator ShowLoadDialogCoroutine() {        
        yield return FileBrowser.WaitForLoadDialog(FileBrowser.PickMode.Files, false, Application.streamingAssetsPath, null, "Select A PowerPoint File", "Load");

        // Dialog is closed
        // Print whether the user has selected some files or cancelled the operation (FileBrowser.Success)
        Debug.Log(FileBrowser.Success);

        if (FileBrowser.Success)
            OnFilesSelected(FileBrowser.Result); // FileBrowser.Result is null, if FileBrowser.Success is false
    }

    void OnFilesSelected(string[] filePaths) {
        SlideLoader.PPTFile = filePaths[0];
        SlideLoader.enabled = true;

        Destroy(this.gameObject);
    }
}
