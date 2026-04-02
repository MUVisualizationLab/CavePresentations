from os import listdir, makedirs, stat
import os.path
import sys
import json
import argparse

# Powerpoint API
import pythoncom
import win32com.client

# Graphics processing
from PIL import Image, ImageChops

class Settings:
    def __init__(self, pptFile, outDir, layers = False, debug = False):
        self.pptFile = os.path.abspath(pptFile)
        if not os.path.isfile(self.pptFile):
            raise FileNotFoundError(f"PowerPoint file not found: {self.pptFile}")

        self.timestamp = stat(self.pptFile).st_mtime
        self.outDir = os.path.abspath(outDir)
        self.layers = layers
        self.debug = debug
        if debug:
            print("Debug text enabled.")
        self.metadata = None    #will be filled later
        self.slideCount = 0     #will be filled later

    #This method is an optimization that prevents redundant processing.
    def avoidRedundancy(self):
        if self.debug:
            return
            
        #If the metafile doesn't exit, then the PPT file has never been processed.
        jsonfile = os.path.join(self.outDir, "metadata.json")
        if os.path.isfile(jsonfile) == False:        
            return
    
        #If the metadata refers to a different PPT, we need to redo the conversion               
        with open(jsonfile, mode="r", encoding="utf-8") as read_file:
            jsondata = json.load(read_file)        
        
        if self.pptFile != jsondata[0]['source']:        
            return

        # If the 3D setting changed, we need to redo the conversion
        if self.layers != jsondata[0]['layers']:        
            return 
        
        #If the timestamps changed, we need to redo the conversion
        if self.timestamp != jsondata[0]['timestamp']:        
            return 

        #If we made it this far, then...
        print("PPT conversion is not needed.", flush=True)
        sys.exit(0) 

    def addMetadata(self, num, imgPath, note = "", transition = 0):
        #initialize header if it hasn't been made yet
        if self.metadata == None:
            self.metadata = [{
                "source" : self.pptFile,
                "count" : self.slideCount,
                "timestamp" : self.timestamp,
                "layers" : self.layers
            }]

        # Structure the gathered data into a dictionary
        metadataItem = {
            "num"        : num,
            "path"       : imgPath,
            "note"       : note,
            "transition" : transition
        }
        self.metadata.append(metadataItem)
        
    # Save metadata to a JSON file
    def closeMetadata(self):
        print("Saving metadata...", flush=True)
        metadata_path = os.path.join(self.outDir, "metadata.json")
        with open(metadata_path, "w", encoding="utf-8") as f:
            json.dump(self.metadata, f, indent=2, ensure_ascii=True)

class PPT:
    def __init__(self):
        global settings
        print("Opening PowerPoint...", flush=True)   
        makedirs(settings.outDir, exist_ok=True)
    
        #remove old images from output directory
        for filename in listdir(settings.outDir):
            if filename.lower().endswith(".png") or filename.lower().endswith(".dds"):
                if settings.debug:
                    print(f"Deleting old temp file: {filename}")
                os.remove(os.path.join(settings.outDir, filename))  

        # Initialize COM for this thread
        pythoncom.CoInitialize()    

        # Create PowerPoint COM application
        self.ppt_app = win32com.client.Dispatch("PowerPoint.Application")
        try:
            self.presentation = self.ppt_app.Presentations.Open(settings.pptFile, True, False, 0)
        except Exception as com_err:
            raise RuntimeError(f"Failed to open presentation '{settings.pptFile}': {com_err}") from com_err

        self.slides = self.presentation.Slides
        settings.slideCount = self.presentation.Slides.Count

        if settings.debug:
            print("Powerpoint Presentation initialized.")
    
    #utilities
    def _getNotes(slide):
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
    
    def _filterShapes(slide, keep_text=False, keep_images=False):
        for shape in range(slide.Shapes.Count, 0, -1):
            s = slide.Shapes.Item(shape)
            if keep_text and getattr(s, 'HasTextFrame', False):
                continue
            if keep_images and getattr(s, 'Type', None) == 13:  # msoPicture?
                continue
            s.Delete()
    
    def close(self):
        global settings
        print("Closing PowerPoint...", flush=True)
        try:
            self.presentation.Close()
            self.ppt_app.Quit()
        except Exception:
            pass
        
        settings.closeMetadata()
        pythoncom.CoUninitialize()


    #the simple 2D version        
    def render2DSlides(self):
        global settings
        for i in range(1, settings.slideCount + 1):            
            print(f"Rendering slide {i} of {settings.slideCount}...", flush=True)
            
            # Export slide as PNG
            slide = self.slides.Item(i)
            out_path = os.path.abspath(os.path.join(settings.outDir, f"{i:03d}.png"))
            slide.Export(out_path, "PNG", 4096, 2048)   #powers of 2. Aspect ratio is corrected in Unity.            
        
            # Store metadata for the gathered data into a dictionary for this slide
            settings.addMetadata(i, out_path,  PPT._getNotes(slide), slide.SlideShowTransition.EntryEffect)
    
    def expandSlides(self):
        #get metadata in place before we mix things up 
        global settings   
        for i in range(1, settings.slideCount + 1):
            slide = self.slides.Item(i)            
            settings.addMetadata(i, "", PPT._getNotes(slide), slide.SlideShowTransition.EntryEffect)
        
        #triple the amount of slides in the ppt, creating placeholders for the filtering to occur
        print(f"Expanding slide layers...", flush=True)
        repeat_count = 3  # original + 2 duplicates
        for i in range(settings.slideCount, 0, -1):
            slide = self.slides.Item(i)                        
            for n in range(repeat_count - 1):
                newSlide = slide.Duplicate()
        

    #filter the slide contents and save the modified slides to PNG
    def render3DSlides(self):
        global settings
        count = self.presentation.Slides.Count
        loopCounter = 1

        for i in range(1, count, 3):
            print(f"Separating slide {loopCounter} of {int(count / 3)}...", flush=True)
            #0 = bg. 
            slide = self.presentation.Slides.Item(i)
            for shape in range(slide.Shapes.Count, 0, -1):
                slide.Shapes.Item(shape).Delete()

            out_path = os.path.join(settings.outDir, f"{loopCounter:03d}_0.png")
            slide.Export(out_path, "PNG", 4096, 2048)

            #1 = text only. Clear the background and delete all non-text elements.
            slide = self.presentation.Slides.Item(i + 1)            
            #see https://learn.microsoft.com/en-us/office/vba/api/powerpoint.slide.background
            #slide.FollowMasterBackground = 0;
            #slide.Background.Fill.Transparency = 1.0
            PPT._filterShapes(slide, keep_text=True, keep_images=False)          
            out_path = os.path.join(settings.outDir, f"{loopCounter:03d}_1.png")
            slide.Export(out_path, "PNG", 4096, 2048)

            #2 = photos only. Clear the background and delete all non-text elements.
            slide = self.presentation.Slides.Item(i + 2)
            PPT._filterShapes(slide, keep_text=False, keep_images=True)
            out_path = os.path.join(settings.outDir, f"{loopCounter:03d}_2.png")
            slide.Export(out_path, "PNG", 4096, 2048)
            
            loopCounter += 1

    def postprocess(self):
        global settings
        count = int(self.presentation.Slides.Count / 3)
        for i in range(1, count + 1):
            print(f"Post-processing slide {i} of {count}...", flush=True)
            #we have to convert to RGB because Powerpoint will use an indexed pallete if the image is simple
            img0 = Image.open(os.path.join(settings.outDir, f"{i:03d}_0.png")).convert('RGB')     #bg 
            img1 = Image.open(os.path.join(settings.outDir, f"{i:03d}_1.png")).convert('RGB')     #text
            img2 = Image.open(os.path.join(settings.outDir, f"{i:03d}_2.png")).convert('RGB')     #photos
            bigImg = Image.new(mode='RGBA', size=[img1.width, img0.height + img1.height], color="#00000000")
    
            #Generate alpha channels with difference masks
            img1.putalpha(ImageChops.difference(img1, img0).convert("L"))
            img2.putalpha(ImageChops.difference(img2, img0).convert("L"))
    
            #make the collage            
            bigImg.paste(img1, (0,0))                       #text takes up the top half
            img0 = img0.resize((2048, 2048))
            bigImg.paste(img0, (0, img1.height))            #bottom left is the background, half res           
            img2 = img2.resize((2048, 2048))
            bigImg.paste(img2, (img0.width, img1.height))   #bottom right is the photos, half res

            #save the file as DDS, which imports easily into Unity
            finalpath = os.path.join(settings.outDir, f"{i:03d}.dds")
            settings.metadata[i]["path"] = finalpath
            bigImg.save(finalpath, pixel_format="DXT5")

        #remove temp PNGs
        for filename in listdir(settings.outDir):
            if "_" in filename and filename[-3:].lower() == 'png':
                os.remove(os.path.join(settings.outDir, filename)) 

if __name__ == "__main__":
    #process the input arguments
    parser = argparse.ArgumentParser(description="Export PowerPoint slides to high quality PNG or 3D DDS for use in Unity.") 
    parser.add_argument("-d", "--debug", action='store_true', help="Enable verbose debug mode")
    parser.add_argument("-3d", "--layers", action='store_true', help="Enable automatic 3D layer conversion")
    parser.add_argument("-o", "--out", default="slides", help="Specify texture output directory")
    parser.add_argument("ppt", help="Path to PowerPoint file")
    args = parser.parse_args()

    global settings
    settings = Settings(args.ppt, args.out, args.layers, args.debug)    
    settings.avoidRedundancy()           

    #main conversion pipeline starts here
    ppt = PPT()
    if settings.layers == False:
        ppt.render2DSlides() 
    else:
        ppt.expandSlides()
        ppt.render3DSlides()
        ppt.postprocess()
      
    ppt.close()
        
    print("Conversion complete.", flush=True)
    print()
    sys.exit(0)
