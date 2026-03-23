using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MiddleVR;
using MiddleVR.Unity;
using TMPro;
//using System;

public class slideMaster : MonoBehaviour {
	//0 = off, left. 1 = left side. 2 = center. 3 = right. 4 = off, right.
	public Transform[] positions;
	public int currentSlide = 0;
	public int[] jumpSlides;
	public TextMeshProUGUI notesText;

    //for cluster syncronization
    //private VRSharedValue<int> currentSlideSync;        //synced mirror of 'mainSlide'
    private int currentSlideSync;        //synced mirror of 'mainSlide'
    private double joystickTimer = 0d;					//used to prevents overruns

    public float transitionSpeed = 0.6f;
	public static bool slidesOn = true;
	private string[] notes;

	// Use this for initialization
	void Start () {
        //initilize these variables on the MiddleVR server
        currentSlideSync = 0;

		notesText.text = "";

        //use the common method to initialize the slides
        requestSlide(0);
    }

	public void applyNotes(string[] newNotes)
	{
		notes = newNotes; 
	}

	// Update is called once per frame
	void Update () {
        //wand controls
		//if (slidesOn) {
		//	if (MVR.DeviceMgr.IsWandButtonToggled(1) || MVR.DeviceMgr.IsKeyToggled(MVR.VRK_SPACE))
		//		deactivate();
		//} else {
		//	if (MVR.DeviceMgr.IsWandButtonToggled(5) || MVR.DeviceMgr.IsKeyToggled(MVR.VRK_SPACE))
		//		reactivate();
		//	return;
		//}

        if (MVR.DeviceMgr.GetWandHorizontalAxisValue () > 0.4f || MVR.DeviceMgr.IsKeyPressed(MVR.VRK_RIGHT)) 
			nextSlide ();
		else if (MVR.DeviceMgr.GetWandHorizontalAxisValue () < -0.4f || MVR.DeviceMgr.IsKeyPressed(MVR.VRK_LEFT))
			previousSlide ();

            //jump keys
        if (MVR.DeviceMgr.IsKeyToggled(MVR.VRK_1) && jumpSlides.Length > 0) 
			requestSlide(jumpSlides[0]);
		else if (MVR.DeviceMgr.IsKeyToggled(MVR.VRK_2) && jumpSlides.Length > 1) 
			requestSlide(jumpSlides[1]);
        else if(MVR.DeviceMgr.IsKeyToggled(MVR.VRK_3) && jumpSlides.Length > 2) 
			requestSlide(jumpSlides[2]);
        else if(MVR.DeviceMgr.IsKeyToggled(MVR.VRK_4) && jumpSlides.Length > 3) 
			requestSlide(jumpSlides[3]);
        else if(MVR.DeviceMgr.IsKeyToggled(MVR.VRK_5) && jumpSlides.Length > 4) 
			requestSlide(jumpSlides[4]);
        else if(MVR.DeviceMgr.IsKeyToggled(MVR.VRK_6) && jumpSlides.Length > 5) 
			requestSlide(jumpSlides[5]);
        else if(MVR.DeviceMgr.IsKeyToggled(MVR.VRK_7) && jumpSlides.Length > 6) 
			requestSlide(jumpSlides[6]);
        else if(MVR.DeviceMgr.IsKeyToggled(MVR.VRK_8) && jumpSlides.Length > 7) 
			requestSlide(jumpSlides[7]);
        else if(MVR.DeviceMgr.IsKeyToggled(MVR.VRK_9) && jumpSlides.Length > 8) 
			requestSlide(jumpSlides[8]);
        else if(MVR.DeviceMgr.IsKeyToggled(MVR.VRK_0) && jumpSlides.Length > 9) 
			requestSlide(jumpSlides[9]);
	}

	public void deactivate() {		
		if (!slidesOn) return;

        //disable yourself and children
        for (int i = 0; i < transform.childCount; i++) {
            transform.GetChild(i).gameObject.SetActive(false);
        }
        slidesOn = false;
    }

	//this gets called when returning from sequence animation, not on start
	public void reactivate() {
		slidesOn = true;
		if (MVR.Kernel.GetTime() < 5000d) return;
		currentSlideSync = currentSlide;


        for (int i = 0; i < transform.childCount; i++) {
            transform.GetChild(i).gameObject.SetActive(true);
        }
        requestSlide(currentSlide);
    }

	public void previousSlide(){
        double currentTime = MVR.Kernel.GetTime();
        if (currentTime - joystickTimer <= 750d) 
			return;
		else 
			joystickTimer = currentTime;

        currentSlide = currentSlideSync;
		if (currentSlide > 0) {
			currentSlide--;
			currentSlideSync = currentSlide;
			requestSlide (currentSlide);
		}
	}

	public void nextSlide(){
        double currentTime = MVR.Kernel.GetTime();
        if (currentTime - joystickTimer <= 750d)
            return;
        else
            joystickTimer = currentTime;

        currentSlide = currentSlideSync;
		if (currentSlide < transform.childCount)
			currentSlide++;
		else if (currentSlide == transform.childCount)
			currentSlide = 0;

		currentSlideSync = currentSlide;

		requestSlide(currentSlide);
	}

	//this method is capable of jumping to any slide in any order.
	public void requestSlide(int newMainSlide){
		if (newMainSlide > transform.childCount) return;

		//housekeeping. put the camera back.
		//startNode.transform.position = startNodePosition;
		//startNode.transform.rotation = startNodeDirection;

		//StopCoroutine("setStatusAnim");
		StopAllCoroutines();

		currentSlide = newMainSlide;
		currentSlideSync = newMainSlide;

        //update the notes text
        if (!Cluster.IsClient) {
            notesText.text = notes[newMainSlide];
        }

        //make sure EVERY slide is in the appropriate position
        for (int i = 0; i < transform.childCount; i++) {
			if (i == newMainSlide - 2 && i >= 0)
				setStatus(i, 0);
			else if (i == newMainSlide - 1 && i >= 0)
				StartCoroutine(setStatusAnim(i, 1));
			else if (i == newMainSlide)
				StartCoroutine(setStatusAnim(i, 2));
			else if (i == newMainSlide + 1 && i < transform.childCount)
				StartCoroutine(setStatusAnim(i, 3));
			else 
				setStatus(i, 4);
		}
	}

	//helper method. This method is for instantly setting a slide's transform.
	void setStatus(int slideNum, int newPosition){
		Transform slide = transform.GetChild(slideNum);
		slide.position = positions[newPosition].position; 
		slide.rotation = positions[newPosition].rotation;
		slide.localScale = positions [newPosition].localScale;

		//disable the object if it's supposed to be off-screen
		if (newPosition == 0 || newPosition == 4)
			slide.gameObject.SetActive (false);
		else 
			slide.gameObject.SetActive (true);
		
		if (newPosition == 2)
			currentSlide = slideNum;
	}

	//helper method, for animating into a slide's transform.
	IEnumerator setStatusAnim(int slideNum, int newPosition){
        yield return new WaitForEndOfFrame();
        Transform slide = transform.GetChild(slideNum);

		//cache the old position
		Vector3 startPosition = slide.position;
		Quaternion startRotation = slide.rotation;
		Vector3 startScale = slide.localScale;

		//set up the new position
		//Vector3 targetPosition = positions[newPosition].position; 
		//Quaternion targetRotation = positions[newPosition].rotation;
		Vector3 targetScale = positions [newPosition].localScale;

		if (newPosition == 0 || newPosition == 4)
			slide.gameObject.SetActive (false);
		else 
			slide.gameObject.SetActive (true);
				
		if (newPosition == 2)
			currentSlide = slideNum;

		//animate
		float animCounter = 0f;
		float smoothed;

		while (animCounter < transitionSpeed) {
			animCounter += (float)MVR.Kernel.GetDeltaTime();

			smoothed = Mathf.SmoothStep(0f, 1f, animCounter / transitionSpeed);
			slide.position = Vector3.Lerp(startPosition, positions[newPosition].position, smoothed);
			slide.rotation = Quaternion.Slerp(startRotation, positions[newPosition].rotation, smoothed);
			slide.localScale = Vector3.Slerp(startScale, targetScale, smoothed);
			yield return new WaitForEndOfFrame();
		}
	}
}
