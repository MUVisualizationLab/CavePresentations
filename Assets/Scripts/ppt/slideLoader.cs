using MiddleVR;
using MiddleVR.Unity;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Diagnostics.Eventing.Reader;


public class slideLoader : MonoBehaviour {
    public bool layeredSlides = true;
    public string PPTFile = "";
    private Texture2D[] slides;
    private string[] notes;
    public TextMeshProUGUI outputText;    

    private Process process;
    private SynchronizationContext unityContext;
    private JArray jsondata;    

    // Start is called before the first frame update
    void Start() {        
        Cluster.AddMessageHandler(this, slidesReady, channel: 11);
        slides = new Texture2D[0];

        if (MVR.ClusterMgr.IsServerOrNoClusterConfig()) {
            UnityEngine.Debug.Log("Starting server PPT conversion...");
            if (PPTFile == "") {
                string[] pptFiles = Directory.GetFiles(Application.streamingAssetsPath, "*.ppt*");
                PPTFile = pptFiles[0];                
            }
            ConvertPPT(PPTFile);
        } else {
            UnityEngine.Debug.Log("Client instance; waiting for metadata trigger...");
        }
    }

    #region ServerPPTConversion
    public void ConvertPPT(string filename) { 
        unityContext = SynchronizationContext.Current;
        string exePath = Path.Combine(Application.streamingAssetsPath, "PPTConvert", "PPTConvert.exe");

        string args = $"-o {Application.temporaryCachePath} {filename}";
        if (layeredSlides) args = "-3d " + args;

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process = new Process();
        process.StartInfo = psi;
        process.EnableRaisingEvents = true;

        process.OutputDataReceived += OnOutputDataReceived;
        process.ErrorDataReceived += OnOutputDataReceived;
        process.Exited += OnProcessExited;

        process.Start();

        process.BeginOutputReadLine();
        //process.BeginErrorReadLine();
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Data))
            return;

        unityContext.Post(_ => {
            if (outputText != null) {
                //outputText.text += e.Data + "\n";
                outputText.text = e.Data;
            }
        }, null);
    }

    private void OnProcessExited(object sender, EventArgs e) {
        unityContext.Post(_ => {
            //load the JSON
            outputText.text = "Broadcasting Metadata...";
            string path = Path.Combine(Application.temporaryCachePath, "metadata.json");
            string metadata = File.ReadAllText(path);

            //Send the metadata to the cluster clients
            Cluster.BroadcastMessage(this, metadata, channel: 11);

        }, null);

        process.Dispose();
    }
    #endregion

    #region LoadSlides
    public void slidesReady(string json)
    {
        //UnityEngine.Debug.Log("JSON Count = " + json.Length);
        jsondata = JArray.Parse(json);

        outputText.text = "Starting Import...";
        StartCoroutine(importSlides());
    }

    private IEnumerator importSlides() {
        yield return null;
        // First entry contains slide count
        int slideCount = jsondata[0]["count"].Value<int>();
        
        slides = new Texture2D[slideCount];
        notes = new string[slideCount];

        for (int i = 1; i < jsondata.Count; i++) {
            outputText.text = $"Loading Texture {i} of {jsondata.Count - 1}...";
            JObject slide = (JObject)jsondata[i];

            //int num = slide["num"].Value<int>();
            string imagePath = slide["path"].Value<string>();
            string note = slide["note"]?.Value<string>() ?? "";
            //int transition = slide["transition"]?.Value<int>() ?? 0;  //for future use

            //load the texture
            string fileUri = new System.Uri(imagePath, System.UriKind.Absolute).AbsoluteUri;

            using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(fileUri)) {
                yield return webRequest.SendWebRequest();

                if (webRequest.result != UnityWebRequest.Result.Success) {
                    UnityEngine.Debug.LogError($"PPTConverter: Failed to load texture from {imagePath}: {webRequest.error}");
                    continue;
                }

                Texture2D tempTexture = DownloadHandlerTexture.GetContent(webRequest);
                Texture2D texture = new Texture2D(tempTexture.width, tempTexture.height, TextureFormat.RGB24, false);
                texture.SetPixels(tempTexture.GetPixels());
                texture.Compress(false);
                texture.Apply();
                texture.name = Path.GetFileNameWithoutExtension(imagePath);
                slides[i - 1] = texture;
            }

            //load the note
            notes[i - 1] = note;
            yield return null;
        }

        //generate the prefabs
        GameObject slidePrefab;
        if (layeredSlides) 
            slidePrefab = Resources.Load("SlidePrefab3D", typeof(GameObject)) as GameObject;
        else 
            slidePrefab = Resources.Load("SlidePrefab", typeof(GameObject)) as GameObject;
        
        List<GameObject> slidesGO = new List<GameObject>();

        for (int i = 0; i < this.slides.Length; i++) {
            //create slide object
            GameObject newSlide = GameObject.Instantiate(slidePrefab, transform);
            newSlide.transform.localPosition = Vector3.zero;
            newSlide.transform.localEulerAngles = Vector3.zero;
            newSlide.name = this.slides[i].name;
            slidesGO.Add(newSlide);

            //apply the texture
            Material layer1 = newSlide.GetComponent<MeshRenderer>().material;
            layer1.name = "T_" + this.slides[i].name;
            layer1.mainTexture = this.slides[i];
        }

        //done, cleanup
        outputText.text = "Complete.";        
        yield return new WaitForEndOfFrame();
        if (Cluster.IsServer) Cluster.RemoveMessageHandler(this, 11);
        
        slideMaster sm = GetComponent<slideMaster>();
        sm.enabled = true;
        sm.applyNotes(notes);

        yield return new WaitForEndOfFrame();
        Destroy(outputText);
        Destroy(this);
    }
    #endregion
}
