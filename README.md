# CavePresentations
Open Powerpoint Presentations in Unity, and view them in VR. 

The VR setup uses MiddleVR, for use in large scale visualization environments such as the Marquette University Visualization Lab's Cave. This repository contains two parts: PPTConvert is a Python script that uses Powerpoint's API to convert a slideshow into a series of PNG images. The Unity project portion imports those images and applies them to prefab 3D objects which can be viewed like a powerpoint presentation in a 3D VR space. This project is in alpha quality, and in the future will support 3D layers and more features.

PPTConvert requirements:
  * Python 3.12
  * [Python for Window Extensions](https://pypi.org/project/pywin32/) 311  
  * Pillow 12.x 
  * Nuitka 4.x (for compiling)
  * Microsoft Office / PowerPoint must be installed

CavePresentation requirements:
  * Unity 6000.0.68 or later
  * MiddleVR 3.1.4

Documentation coming later.

