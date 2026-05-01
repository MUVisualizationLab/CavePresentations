using UnityEngine;
using System.Collections;
using MiddleVR;

public class clickOnSides : MonoBehaviour {

	public slideMaster manager;
	public bool reverseDirection = false;

    protected void OnMVRWandButtonReleased(MVRSelection iSelection) { 
		//if (button != 0) return;

        if (reverseDirection) 
			manager.previousSlide();
		else
			manager.nextSlide();
	}
}
