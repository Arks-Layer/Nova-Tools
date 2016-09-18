# Needs to be tweaked if you want to use it with Phantasy Star Nova
# Will automatically seek to the font table to update glyph width/heights based on a text file

import os
import struct

def read_byte(file):
    return struct.unpack("<B", file.read(1))[0]

def read_short(file):
    return struct.unpack("<H", file.read(2))[0]

def read_int(file):
    return struct.unpack("<I", file.read(4))[0]

def read_long(file):
    return struct.unpack("<Q", file.read(8))[0]

def read_fontsize(fontsize_filename):
    sizes = []
    
    with open(fontsize_filename, "r") as file:        
        offset = 0
        i = 0
        for line in file:
            line = line.strip('\n').strip('\r').split(' ')
            a = int(line[0], 16)
            b = int(line[1], 16)
            sizes.append((a, b))
            
    return sizes

def parse_script(input_filename, fontsize_filename):
    basename = os.path.splitext(input_filename)[0]
    sizes = read_fontsize(fontsize_filename)

    data = None
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
        
        file.seek(0)
        data = file.read()
        
    with open(input_filename,"wb") as outfile:
        outfile.write(data)
    
        outfile.seek(font_table_offset)
        for i in range(0, charset_size):
            outfile.write(struct.pack("<B", sizes[i][0]))
            outfile.write(struct.pack("<B", sizes[i][1]))
            outfile.seek(0x12, 1)
            
        
parse_script("f0006_pack_000a_000a_pack_0000_0003.dmr.out", "f0006_pack_000a_000a_pack_0000_0003-fontsizes.txt") # main
parse_script("f1240_pack_0000_0001.dmr.out", "f1240_pack_0000_0001-fontsizes.txt") # rpg
