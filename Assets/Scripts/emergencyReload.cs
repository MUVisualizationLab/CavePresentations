using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MiddleVR;
using UnityEngine.SceneManagement;

public class emergencyReload : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (MVR.DeviceMgr.IsKeyToggled(MVR.VRK_R)) {
            StartCoroutine("reload");
        }
    }

    IEnumerator reload() {
        slideMaster sm = GameObject.Find("Slides").GetComponent<slideMaster>();
        int cacheSlide = sm.currentSlide;
        SceneManager.LoadScene(0, LoadSceneMode.Single);
        yield return new WaitForEndOfFrame();
        sm = GameObject.Find("Slides").GetComponent<slideMaster>();
        sm.requestSlide(cacheSlide);

    }

}
