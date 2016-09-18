import os
import struct
import sys
import subprocess

OUTPUT_HTML = True
OUTPUT_TEXT = True

BASIC_CHARSET_BASE = 0x81
CHARSET_BASE = 0x0391

def read_byte(file):
    return struct.unpack("<B", file.read(1))[0]

def read_short(file):
    return struct.unpack("<H", file.read(2))[0]

def read_int(file):
    return struct.unpack("<I", file.read(4))[0]

def read_long(file):
    return struct.unpack("<Q", file.read(8))[0]

def read_charmap(charmap_filename):
    charmap = {}
    
    if charmap_filename == None:
        return None
    
    if os.path.exists(charmap_filename):
        with open(charmap_filename, "r") as file:        
            offset = 0
            i = 0
            for line in file:
                line = line.strip('\n').strip('\r')
                
                # I can't remember why I have this code. Sorry
                if (i + 1) % 0x80 == 0:
                    offset += 0x80
                    
                charmap[i + CHARSET_BASE + offset] = line
                i += 1
            
    return charmap

def parse_script(input_filename, charmap_filename=None):
    basename = os.path.splitext(input_filename)[0]

    with open(input_filename, "rb") as file:
        if file.read(4) != " DMR":
            print "Not a DMR file"
            exit(-1)
            
        file.seek(0x0c)
        font_file_offset = read_int(file)
        
        file.seek(0x30)
        string_entry_table_offset = read_int(file) + 0x10
        string_table_offset = read_int(file) + 0x10
        font_table_offset = read_int(file) + 0x10
        
        file.seek(0x44)
        charset_size = read_int(file)
        
        file.seek(0x64)
        string_entries_entries = read_int(file)
        
        file.seek(0x70)
        font_image_width = read_int(file)
        font_image_height = read_int(file)
        
        print "%08x %08x %08x" % (string_entry_table_offset, string_table_offset, font_table_offset)
        
        charmap = read_charmap(charmap_filename)
        
        if not charmap:
            OUTPUT_TEXT = False
        
        charset = {}
        # Parse string_entries
        file.seek(string_entry_table_offset)
        string_entries = []
        offset = 0
        for i in range(0, string_entries_entries):
            id = read_long(file)
            offset = read_int(file)
            unk2 = read_int(file)
            unk3 = read_int(file)
            unk4 = read_int(file)
        
            string_entries.append({'id': id,
             'offset': offset,
             'unk2': unk2, 
             'unk3': unk3, 
             'unk4': unk4 })
            
            # I can't remember why I have this code still. Sorry again
            if (i + 1) % 0x80 == 0:
                offset += 0x80
            
            charset[i + CHARSET_BASE + offset] = i
            
            
            
        # Read string table
        file.seek(string_table_offset)
        strings = []
        while file.tell() < font_table_offset:
            string = []
            
            while True:
                c = read_byte(file)
            
                if c == 0:
                    break
                else: 
                    string.append(c)
            
            if len(string) > 0:
                strings.append(string)
                
                
        
        # Parse individual strings
        # HTML output is for when you don't have a charset build already
        if OUTPUT_HTML:
            outfile = open("%s.html" % basename, "w")
            
            # Generate CSS
            outfile.write("<style>")
            width = 25
            height = 25
            left = 0
            top = 0
            chars = 0
            
            # TODO: Where does the ruby font map to??
            
            # Main font
            offset = 0
            for i in range(BASIC_CHARSET_BASE, CHARSET_BASE):
                if (i + 1) % 0x80 == 0:
                    offset += 0x80
                    
                filename = "BasicCharSet_font_0000.png"
                outfile.write(".glyph%d { background: url('%s') no-repeat; background-position: -%dpx -%dpx; height: %dpx; width: %dpx; display: inline-block; }\r\n" % (i + offset, filename, left, top, width, height))
                
                left += width
                
                if left >= 500:
                    left = 0
                    top += height
                    
                    if top > 500:
                        break
                
                
            # Script-specific font
            left = 0
            top = 0
            chars = 0
            offset = 0             
            for i in range(CHARSET_BASE, charset_size + CHARSET_BASE):
                if (i + 1) % 0x80 == 0:
                    offset += 0x80
                    
                filename = input_filename[:input_filename.index('.')] + "_font_0000.png"
                outfile.write(".glyph%d { background: url('%s') no-repeat; background-position: -%dpx -%dpx; height: %dpx; width: %dpx; display: inline-block; }\r\n" % (i + offset, filename, left, top, width, height))
                
                left += width
                chars += 1
                
                if left > font_image_width:
                    left = 0
                    top += height
                    
                    if top > font_image_height:
                        break
                
            outfile.write("</style>")
            
            outfile.write("<body bgcolor='#000000' text='white'>")
            
            # Uncomment to see entire available charset
            #for i in range(BASIC_CHARSET_BASE, chars + CHARSET_BASE):
            #    outfile.write('%04x <div class="glyph%d" width="26" height="26"></div>' % (i, i))
                
                
        if OUTPUT_TEXT:
            outfile2 = open("%s.txt" % basename, "wb")
            
        for i in range(0, len(string_entries)):
            entry = string_entries[i]
            block = []
            
            file.seek(string_table_offset + entry['offset'])
            
            if OUTPUT_HTML:
                outfile.write("<font color='white'>%08x:</font>" % entry['id'])
            if OUTPUT_TEXT:
                outfile2.write("<%08x> " % entry['id'])
            
            c = read_byte(file)
            while c != 0:
                a = read_byte(file)
                c = (a << 8) | c
                
                if c >= 0x8080:
                    # Control codes - Fix for Phantasy Star Nova
                    
                    txt = ""
                    if c == 0x8080: # Newline
                        if OUTPUT_HTML:
                            txt = "<br>"
                        if OUTPUT_TEXT:
                            txt = "[n]"
                    elif c == 0x8081: # Color
                        color = read_byte(file)
                        txt = "[color=%02x]" % color
                    elif c == 0x8082: # End color
                        txt = "[/color]"
                    elif c == 0x8087: # End box 2?
                        txt = "[end]"
                    elif c == 0x8093: # Name
                        name = read_byte(file)
                        txt = "[name=%02x]" % name
                    elif c == 0x8099: # Graphic button
                        icon = read_byte(file)
                        txt = "[sysicon=%02x]" % icon
                    else:
                        print "Unknown command: %08x: %04x" % (file.tell() - 2, c)
                        exit(1)
                        
                    if OUTPUT_HTML:
                        outfile.write(txt)
                    if OUTPUT_TEXT:
                        outfile2.write(txt)
                else:
                    try:
                        if OUTPUT_HTML:
                            #outfile.write("<img src='%s\\font_%d.png'>" % (basename, charset[c]))
                            outfile.write('<div class="glyph%d"></div>' % (c))
                        if OUTPUT_TEXT:
                            outfile2.write("%s" % charmap[c])
                    except:
                        print "Unknown character: %04x @ %08x" % (c, file.tell() - 2)
                        
                        if OUTPUT_TEXT:
                            outfile2.write("[%04x]" % c)
                            
                        #exit(-1)
                
                
                if c < 0:
                    print "Invalid character: %d" % c
                    exit(1)
                
                block.append(c)
                c = read_byte(file)
            
            if OUTPUT_HTML:
                outfile.write("<br>")
            if OUTPUT_TEXT:
                outfile2.write("\r\n")
            
            
        if OUTPUT_HTML:
            outfile.close()
        if OUTPUT_TEXT:
            outfile2.close()
        
        
        # Charset information table
        # Probably contains offsets to the actual glyphs within the image file if you squint hard enough
        file.seek(font_table_offset)
        charset_info = []
        for i in range(0, charset_size):
            # Width and height of glyph
            # These can be tweaked for creating a nicer looking variable width font
            # No idea about the other values. I imagine somewhere is X/Y offsets in the image file
            w, h, unk1, unk2, unk3, unk4, unk5, unk6, unk7, unk8, unk9 = struct.unpack("<BBHHHHHHHHH", file.read(0x14))
            
            
        # Dump raw font image
        file.seek(font_file_offset)
        font_filename = input_filename[:input_filename.index('.')]  + "_font.aif"
        open(font_filename,"wb").write(file.read())
        
        subprocess.call(["aif_to_gxt.py", font_filename], shell=True)
        os.remove(font_filename)

parse_script(sys.argv[1])