# Powerpoint API
import pythoncom
import win32com.client
import os.path

pythoncom.CoInitialize()
    
ppt_app = None
presentation = None

# Create PowerPoint COM application
ppt_app = win32com.client.Dispatch("PowerPoint.Application")

# Ensure absolute path and use positional args for Presentations.Open
ppt_path_abs = os.path.abspath("sample.pptx")
presentation = ppt_app.Presentations.Open(ppt_path_abs, True, False, 0)

slides = presentation.Slides
count = slides.Count
slide = slides.Item(8)

#https://learn.microsoft.com/en-us/office/vba/api/powerpoint.slide.background
#slide.Export("test.png", "PNG", 2048, 2048)