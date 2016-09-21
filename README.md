# Nova Tools

A set of tools to modify the files in Nova, so we can get a translation going!


##AIF to GXT (aif_to_gxt.py) [Developed by Nagato]
Converts AIF files to GXT files, for use with GXTConvert. (Only works with Python 2!)

##GXT Convert (GXTConvert.exe) [Developed by [xdaniel](https://twitter.com/xdanieldzd)]
This converts GXT files to PNG.

##psnova-texteditor [Developed by Nagato]
Includes two parts: psnova-texteditor and psnova-textinserter.  
  
psnova-texteditor is a GUI program that lets you view any .rmd file located in the "scripts" folder in the same folder as the EXE. This tool can also be used to dump all strings into separate PNG files for viewing outside of the application. NOTE: You must also have BasicCharSet.rmd and BasicRubySet.rmd available in the scripts folder for maximum compatibility.    
  
psnova-textinserter is a CLI program that uses the translations.json file from psnova-texteditor to generate new .rmd files. The original .rmd files must be located in the "scripts" folder in the same folder as the EXE.
  
