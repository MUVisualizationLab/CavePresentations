python -m nuitka --mode=standalone --windows-console-mode=attach --msvc=latest PPTConvert.py
robocopy PPTConvert.dist ..\Assets\StreamingAssets\PPTConvert /MIR /XF *.meta
