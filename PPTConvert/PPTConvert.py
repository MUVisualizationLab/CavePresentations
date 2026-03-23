from os import listdir, makedirs, stat
import os.path
import sys
import json
import argparse

# Powerpoint API
import pythoncom
import win32com.client

# Graphics processing
#from svglib.svglib import svg2rlg
#from reportlab.graphics import renderPM
from PIL import Image, ImageChops

def initPPT(ppt_path, out_dir):
    print("Opening PowerPoint...", flush=True)
    global ppt_app, presentation, metadata

    if not os.path.isfile(ppt_path):
        raise FileNotFoundError(f"PowerPoint file not found: {ppt_path}")

    makedirs(out_dir, exist_ok=True)
    
    #remove old images from output directory
    for filename in listdir(out_dir):
        if filename.lower().endswith(".png") or filename.lower().endswith(".dds"):
            os.remove(os.path.join(out_dir, filename))  

    # Initialize COM for this thread
    pythoncom.CoInitialize()
    
    ppt_app = None
    presentation = None

    # Create PowerPoint COM application
    ppt_app = win32com.client.Dispatch("PowerPoint.Application")

    # Ensure absolute path and use positional args for Presentations.Open
    ppt_path_abs = os.path.abspath(ppt_path)
    if not os.path.exists(ppt_path_abs):
        raise FileNotFoundError(f"PowerPoint file not found: {ppt_path_abs}")

    try:
        presentation = ppt_app.Presentations.Open(ppt_path_abs, True, False, 0)
    except Exception as com_err:
        # Re-raise with a clearer message including COM error details
        raise RuntimeError(f"Failed to open presentation '{ppt_path_abs}': {com_err}") from com_err

    slides = presentation.Slides
    count = slides.Count
        
    #initalize the metadata with some header info
    metadata = [{
        "count" : count,
        "source" : ppt_path_abs,
        "timestamp" : stat(ppt_path_abs).st_mtime,
        "layers" : False
    }]

    return slides, count

def getNotes(slide):
    # Get slide notes (if any)
    notes_text_parts = []
    try:
        noteShapes = slide.NotesPage.Shapes
        for shape in noteShapes:
            try:
                if getattr(shape, 'HasTextFrame', False):
                    tf = shape.TextFrame
                    if tf and getattr(tf, 'HasText', False):
                        text = tf.TextRange.Text
                        if text:
                            notes_text_parts.append(str(text))
            except Exception:
                continue
    except Exception:
        # no notes page or other COM error
        notes_text_parts = []

    notetext = " ".join(notes_text_parts[:-1]).strip()  
    return notetext

def closePPT():
    # Close presentation and quit PowerPoint
    print("Closing PowerPoint...", flush=True)
    try:
        if presentation:
            presentation.Close()
    except Exception:
        pass
    try:
        if ppt_app:
            ppt_app.Quit()
    except Exception:
        pass
    pythoncom.CoUninitialize()

def export_slides(ppt_path: str, out_dir: str):
    slides, count = initPPT(ppt_path, out_dir)     

    for i in range(1, count + 1):            
        slide = slides.Item(i)
        out_path = os.path.abspath(os.path.join(out_dir, f"{i:03d}.png"))

        # Export slide as PNG
        print(f"Rendering slide {i} of {count}...", flush=True)
        slide.Export(out_path, "PNG", 4096, 2048)   #powers of 2. Aspect ratio is corrected in Unity.            
        
        # Structure the gathered data into a dictionary for this slide
        metadataItem = {
            "num" : i,
            "path" : out_path,
            "note" : getNotes(slide),
            "transition" : slide.SlideShowTransition.EntryEffect
        }
        metadata.append(metadataItem)

    # Save metadata to a JSON file    
    print("Saving metadata...", flush=True)
    metadata_path = os.path.join(out_dir, "metadata.json")
    with open(metadata_path, "w", encoding="utf-8") as f:
        json.dump(metadata, f, indent=2, ensure_ascii=False)

    closePPT()

def export_slides_layered(ppt_path: str, out_dir: str):
    def expandSlides():
        #start up metadata
        for i in range(1, count + 1):
            slide = slides.Item(i)
            # Structure the gathered data into a dictionary for this slide
            metadataItem = {
                "num" : i,
                "path" : "",
                "note" : getNotes(slide),
                "transition" : slide.SlideShowTransition.EntryEffect
            }
            metadata.append(metadataItem)
        
            #triple the amount of slides in the ppt
        print(f"Expanding slide layers...", flush=True)
        repeat_count = 3  # original + 2 duplicates
        for i in range(count, 0, -1):
            slide = slides.Item(i)                        
            for n in range(repeat_count - 1):
                newslide = slide.Duplicate()
                
    def filterShapes(slide, keep_text=False, keep_images=False):
        for shape in range(slide.Shapes.Count, 0, -1):
            s = slide.Shapes.Item(shape)
            if keep_text and getattr(s, 'HasTextFrame', False):
                continue
            if keep_images and getattr(s, 'Type', None) == 13:  # msoPicture
                continue
            s.Delete()

    def exportLayers(out_dir):
        count = presentation.Slides.Count
        loopCounter = 1
        for i in range(1, count, 3):
            print(f"Separating slide {loopCounter} of {int(count / 3)}...", flush=True)
            #0 = bg. 
            slide = presentation.Slides.Item(i)
            for shape in range(slide.Shapes.Count, 0, -1):
                slide.Shapes.Item(shape).Delete()

            out_path = os.path.abspath(os.path.join(out_dir, f"{loopCounter:03d}_0.png"))
            slide.Export(out_path, "PNG", 4096, 2048)

            #1 = text only. Clear the background and delete all non-text elements.
            #https://learn.microsoft.com/en-us/office/vba/api/powerpoint.slide.background
            slide = presentation.Slides.Item(i + 1)
            #slide.FollowMasterBackground = 0;
            #slide.Background.Fill.Transparency = 1.0
            filterShapes(slide, keep_text=True, keep_images=False)          
            out_path = os.path.abspath(os.path.join(out_dir, f"{loopCounter:03d}_1.png"))
            slide.Export(out_path, "PNG", 4096, 2048)

            #2 = photos only. Clear the background and delete all non-text elements.
            slide = presentation.Slides.Item(i + 2)
            filterShapes(slide, keep_text=False, keep_images=True)
            out_path = os.path.abspath(os.path.join(out_dir, f"{loopCounter:03d}_2.png"))
            slide.Export(out_path, "PNG", 4096, 2048)
            
            loopCounter += 1

    def combineLayers(out_dir):        
        #combine images
        count = int(presentation.Slides.Count / 3)
        for i in range(1, count + 1):
            print(f"Processing slide {i} of {count}...", flush=True)
            img0 = Image.open(os.path.abspath(os.path.join(out_dir, f"{i:03d}_0.png"))).convert('RGB')     #bg 
            img1 = Image.open(os.path.abspath(os.path.join(out_dir, f"{i:03d}_1.png"))).convert('RGB')     #text
            img2 = Image.open(os.path.abspath(os.path.join(out_dir, f"{i:03d}_2.png"))).convert('RGB')     #photos
            bigImg = Image.new(mode='RGBA', size=[img1.width, img0.height + img1.height], color="#00000000")
    
            #drawing = svg2rlg("002.svg")
            #png_bytes = renderPM.drawToString(drawing, fmt="PNG", dpi=30

            #difference masks
            img1.putalpha(ImageChops.difference(img1, img0).convert("L"))
            img2.putalpha(ImageChops.difference(img2, img0).convert("L"))
    
            #make the collage            
            bigImg.paste(img1, (0,0))
            img0 = img0.resize((2048, 2048))
            bigImg.paste(img0, (0, img1.height))            
            img2 = img2.resize((2048, 2048))
            bigImg.paste(img2, (img0.width, img1.height))

            #save the file as PNG.
            finalpath = os.path.abspath(os.path.join(out_dir, f"{i:03d}.png"))
            metadata[i]["path"] = finalpath
            bigImg.save(finalpath)

        #remove temp PNGs
        for filename in listdir(out_dir):
            if "_" in filename:
                os.remove(os.path.join(out_dir, filename))  

    slides, count = initPPT(ppt_path, out_dir)
    
    expandSlides()
    exportLayers(out_dir)
    combineLayers(out_dir) 
    
    # Save metadata to a JSON file    
    print("Saving metadata...", flush=True)
    metadata[0]["layers"] = True    #overriding this value
    metadata_path = os.path.join(out_dir, "metadata.json")
    with open(metadata_path, "w", encoding="utf-8") as f:
        json.dump(metadata, f, indent=2, ensure_ascii=False)

    closePPT()
        
def checkForDupe(ppt_path: str, out_dir: str, layers: bool):  
    #If the metafile doesn't exit, then the PPT file has never been processed.
    jsonfile = os.path.join(out_dir, "metadata.json")
    if os.path.isfile(jsonfile) == False:        
        return True
    
    #Lets examine the metadata file
    with open(jsonfile, mode="r", encoding="utf-8") as read_file:
        jsondata = json.load(read_file)
        
    #If the metadata refers to a different PPT, we need to redo the conversion
    ppt_path_abs = os.path.abspath(ppt_path)
    if ppt_path_abs != jsondata[0]['source']:        
        return True

    # If the 3D layers settings does not match, we need to redo the conversion
    if layers != jsondata[0]['layers']:        
        return True
        
    #If the timestamps match, then the existing images are in sync.
    if stat(ppt_path_abs).st_mtime == jsondata[0]['timestamp']:        
        print("Conversion is not needed.", flush=True)
        return False
    else:
        return True

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Export PowerPoint slides to PNG using pywin32 COM")    
    parser.add_argument("-3d", "--layers", action='store_true', help="Enable automatic 3D layer conversion")
    parser.add_argument("-o", "--out", default="slides", help="Output directory for PNG files")
    parser.add_argument("ppt", help="Path to PowerPoint file")

    args = parser.parse_args()
    
    #Optimization: Can we skip the conversion process?
    if checkForDupe(args.ppt, args.out, args.layers) == False:
        sys.exit(0)
    
    try:
        if args.layers:
            export_slides_layered(args.ppt, args.out)
        else:
            export_slides(args.ppt, args.out)           
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)
        
    print("Conversion complete.", flush=True)
    print()
    sys.exit(0)

