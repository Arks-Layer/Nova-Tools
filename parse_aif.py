# Really hacky code, ignore the globals

import os
import sys
import struct

data = open(sys.argv[1],"rb").read()

idx = 0
buffer_size = 0
images = []
addrs = []

def read_header():
    global idx
    header = data[idx:idx+4][::-1]
    section_size, unk, bytes_to_next_section = struct.unpack("<III", data[idx+4:idx+16])
    idx += 16
    return header, section_size, bytes_to_next_section

def read_aif():
    global idx, w, h
    read = True
    while read:
        header, section_size, bytes_to_next_section = read_header()
        
        read = bytes_to_next_section != 0
        
        if header == "aRF ":
            idx += section_size - 0x10 # Skip to next section
        elif header == "imgX":
            size = struct.unpack("<HH", data[idx+0x18:idx+0x1c])
            format = struct.unpack("<H", data[idx+0x1e:idx+0x20])[0]
            images.append({'size': size, 'format': format})
            idx += section_size - 0x10 # Skip to next section
        else:
            print "Unknown section in AIF: %s @ %08x" % (header, idx)
            exit(1)

def read_amf():
    global idx, buffer_size
    read = True
    while read:
        header, section_size, bytes_to_next_section = read_header()
        
        read = bytes_to_next_section != 0
        
        if header == "head":
            idx += section_size - 0x10 # Skip to next section
        elif header == "buff":
            buffer_size = struct.unpack("<I", data[idx:idx+4])[0]
            idx += section_size - 0x10 # Skip to next section
        elif header == "addr":
            addrs.append(struct.unpack("<I", data[idx+0x10:idx+0x14])[0])
            idx += section_size - 0x10 # Skip to next section
        else:
            print "Unknown section in AMF: %s @ %08x" % (header, idx)
            exit(1)

def read_archive(data):
    global idx
    read = True
    while read:
        header, section_size, bytes_to_next_section = read_header()
        
        read = bytes_to_next_section != 0
        
        if header == "AIF ":
            read_aif()
        elif header == "AMF ":
            read_amf()
        else:
            print "Unknown section in archive: %s @ %08x" % (header, idx)
            exit(1)
            

            
read_archive(data)


offset = idx
for i in range(0, len(images)):
    h, w = images[i]['size']
    format = images[i]['format']
    addr = addrs[i]
    
    format_output = 0
    if format == 0x04:
        format_output = 0x85000000 # DXT1??
    elif format == 0x08:
        format_output = 0x81000000 # PVRTC or PVRTC2??
    elif format == 0x20:
        format_output = 0x0c001000 # U8U8U8U8_RGBA
    elif format == 0x40:
        format_output = 0x1c001000 # U16U16U16U16_RGBA??
    else:
        print "Unknown image format: %08x" % format
        exit(-1)
    
    img = data[offset:offset+addr]
    offset += addr

    # Write gxt file so it can be converted (temporary)
    output = sys.argv[1].replace(".aif","") + ("_%04d" % (i)) + ".gxt"    
    
    gxt = open(output,"wb")
    gxt.write(bytearray([0x47, 0x58, 0x54, 0x00, 0x03, 0x00, 0x00, 0x10, 0x01, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00]))
    gxt.write(struct.pack("<I", len(img)))
    gxt.write(bytearray([0x00] * 0x0c))
    gxt.write(struct.pack("<I", 0x40))
    gxt.write(struct.pack("<I", len(img)))
    gxt.write(struct.pack("<I", 0xffffffff))
    gxt.write(bytearray([0x00] * 0x04))
    gxt.write(struct.pack("<I", 0x60000000)) # Linear
    gxt.write(struct.pack("<I", format_output)) # PVRT4BPP_AGRB
    gxt.write(struct.pack("<H", w))
    gxt.write(struct.pack("<H", h))
    gxt.write(struct.pack("<I", 1))
    gxt.write(img)
    gxt.close()

    os.system("gxtconvert.exe %s" % output)
    os.remove(output)
