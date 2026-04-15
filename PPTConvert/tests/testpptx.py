from pptx import Presentation

"""Can we use the Python-PPTX library for layer separation?"""

#setup
prs = Presentation("..\\sample.pptx")
slide = prs.slides[0]

#test 1
#https://python-pptx.readthedocs.io/en/latest/api/slides.html?highlight=background#pptx.slide.Slide.follow_master_background
slide.follow_master_background = False
#AttributeError: property 'follow_master_background' of 'Slide' object has no setter

#test 2


