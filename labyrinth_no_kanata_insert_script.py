# SIMILAR TO PHANTASY STAR NOVA BUT NEEDS TO BE TWEAKED!

import struct

BASIC_CHARSET_BASE = 0x81
CHARSET_BASE = 0x0391

WORDWRAP_LENGTH = 24

def read_byte(file):
    return struct.unpack("<B", file.read(1))[0]

def read_short(file):
    return struct.unpack("<H", file.read(2))[0]

def read_int(file):
    return struct.unpack("<I", file.read(4))[0]

def read_long(file):
    return struct.unpack("<Q", file.read(8))[0]

def get_string_data(file):
    data = bytearray()

    c = read_byte(file)
    while c != 0:
        a = read_byte(file)

        data.append(c)
        data.append(a)

        c = (a << 8) | c
        if c >= 0x8080:
            if c == 0x8081: # Color
                color = read_byte(file)
                data.append(color)
            elif c == 0x8093: # Name
                name = read_byte(file)
                data.append(name)
            elif c == 0x8099: # Graphic button
                icon = read_byte(file)
                data.append(icon)

        c = read_byte(file)

    data.append(c)

    #print ' '.join('{:02x}'.format(x) for x in data)

    return data

def insert_dmr(input_filename, script_data):
    if len(script_data) == 0:
        with open(input_filename, "rb") as file:
            with open(input_filename + ".out", "wb") as outfile:
                outfile.write(file.read())        
        return

    saved = 0
    ids = []
    for v in script_data:
        ids.append(v)

    delkeys = {}
    for v in range(0, len(ids)):
        for x in range(v + 1, len(ids)):
            if x != v and script_data[ids[x]] == script_data[ids[v]]:
                delkeys[ids[x]] = ids[v]
                #print "%08x == %08x" % (x, v)
                saved += len(script_data[ids[x]])
    
    #print delkeys
    #print "Will save %08x (%d) bytes" % (saved, saved)

    #print string_data
    print "Inserting %d strings into %s" % (len(script_data), input_filename)
    
    input_file_data = None
    string_count = 0
    with open(input_filename, "rb") as file:
        file.seek(0x04)
        font_file_offset = read_int(file)
        
        file.seek(0x30)        
        index_table_offset = read_int(file) + 0x10
        string_table_offset = read_int(file) + 0x10
        charset_table_offset = read_int(file) + 0x10
        
        file.seek(0x64)
        string_count = read_int(file)
        
        #if len(script_data) != string_count:
        #    print "%s: String count mismatch! %d != %d" % (input_filename, len(script_data), string_count)
        #    exit(-1)
            
        file.seek(0)
        input_file_data = file.read(string_table_offset)
        
        file.seek(charset_table_offset)
        input_file_data_2 = file.read(font_file_offset - charset_table_offset)
        
        file.seek(font_file_offset)
        input_file_data_3 = file.read()

        offset_table = {}


        orig_offsets_positions = {}
        orig_offsets = {}
        offset_order = []
        for i in range(0, string_count):
            file.seek(index_table_offset + (i * 0x10))
            index = read_long(file)
            orig_offsets_positions[index] = file.tell()
            orig_offsets[index] = read_int(file)
            offset_order.append(index)

        # Write code here to read all of the strings individually from the string table and place them into a list
        # Then overwrite the data parts for the translated lines
        # Then write out the entire new string table
        string_data = {}
        file.seek(string_table_offset)
        for string_id in offset_order:
            file.seek(string_table_offset + orig_offsets[string_id])
            string_data[string_id] = get_string_data(file)

        for string_id in script_data:
            if string_id not in string_data:
                print "Could not find string id %s" % string_id
            string_data[string_id] = script_data[string_id]

        fix_offsets = []
        string_data_block = bytearray()
        for string_id in offset_order:
            if string_id in delkeys:
                #offset_table[string_id] = offset_table[delkeys[string_id]]
                offset_table[string_id] = -1
                fix_offsets.append(string_id)
            else:    
                offset_table[string_id] = len(string_data_block)
                string_data_block += bytearray(string_data[string_id])
        
        while True:
            found_neg = False
            for string_id in fix_offsets:
                offset_table[string_id] = offset_table[delkeys[string_id]]
                #print "%08x %08x %08x" % (string_id, offset_table[string_id], delkeys[string_id])
                
                if offset_table[string_id] == -1:
                    found_neg = True
                    
            if not found_neg:
                break
        
        with open(input_filename + ".out", "wb") as outfile:
            outfile.write(input_file_data)
            outfile.write(string_data_block)

            #outfile.write(bytearray(string_data))

            for string_id in offset_order:
                outfile.seek(orig_offsets_positions[string_id])
                #print "%08x %08x" % (string_id, offset_table[string_id])
                outfile.write(struct.pack("<I", offset_table[string_id]))
            
            #outfile.seek(0x34)
            #outfile.write(struct.pack("<I", len(input_file_data) - 0x10))
            #outfile.seek(0, 2)
            #outfile.write(bytearray(string_data))
            
            outfile.seek(0, 2)
            while outfile.tell() % 0x100 != 0:
                outfile.write('\0')
            
            offset = outfile.tell()
            outfile.seek(0x38)
            outfile.write(struct.pack("<I", offset - 0x10))
            outfile.seek(0, 2)
            outfile.write(input_file_data_2)
            
            while outfile.tell() % 0x100 != 0:
                outfile.write('\0')
            
            offset = outfile.tell()            
            outfile.seek(0x04)
            outfile.write(struct.pack("<I", offset))
            outfile.seek(0x0c)
            outfile.write(struct.pack("<I", offset))
            outfile.seek(0x40)
            outfile.write(struct.pack("<I", offset - 0x10))
            outfile.seek(0, 2)
            outfile.write(input_file_data_3)

def read_charmap(charmap_filename):
    charmap = { " ": 0x06eb,
                u"\u3000": 0x06eb,
                u"\u30dd": 0x01f2, # po
                u"\u30ad": 0x01f3, # ki
                u"\u30fc": 0x01f4, # -
                u"\u30e9": 0x01f5, # ra
              }
    
    with open(charmap_filename, "r") as file:        
        offset = 0
        i = 0
        for line in file:
            line = line.strip('\n').strip('\r')
            
            if (i + 1) % 0x80 == 0:
                offset += 0x80
                
            #charmap[i + CHARSET_BASE + offset] = line
            line = line.decode("shift-jis")
            
            if line not in charmap:
                charmap[line] = i + CHARSET_BASE + offset
                
            i += 1
            
    return charmap
    
def translate_command(command):
    output = -1
    size = 0
    
    op = ""
    if '=' in command:
        eq = command.index('=')
        op = command[eq+1:]
        command = command[:eq]
    
    if command == "n": # Newline
        output = 0x8080
        size = 2
    elif command == "color": # Color
        output = 0x008081 | (int(op, 16) << 16)
        size = 3
    elif command == "/color": # End color
        output = 0x8082
        size = 2
    elif command == "end": # End something?
        output = 0x008087 | (int(op, 16) << 16)
        size = 6
    elif command == "name": # Party member's name
        output = 0x008093 | (int(op, 16) << 16)
        size = 3
    elif command == "sysicon": # Display a system icon in text
        output = 0x008099 | (int(op, 16) << 16)
        size = 3
    elif command == "emit": # Emit a byte
        output = int(op, 16) & 0xff
        size = 1
        
    return output, size
    
def parse_string(input_string, charmap):
    output_data = []
    
    do_wordwrapping = False
    if " " in input_string:
        do_wordwrapping = True
    
    if len(charmap) == 0:
        print "Please load a charmap"
        exit(-1)
    
    had_command = False
    input_string = input_string.decode("shift-jis")
    i = 0
    wrap_len = 0
    last_space = 0
    while i < len(input_string):
        c = input_string[i]
        
        output = -1
        size = 2
        if c == '[' and ']' in input_string:
            command_start = i + 1
            command_end = input_string.index(']', command_start)
            command = input_string[command_start:command_end]
            
            output, size = translate_command(command)
            
            if size != 0:
                i = command_end
            else:
                output = -1
                size = 2
            
        if output == -1:
            # Handle as text
            #print "'%s'" % (c)
            output = charmap[c]
            #print "'%s' = %04x" % (c, charmap[c])
            wrap_len += 1
            
            if c == " " or output == 0x06eb:
                last_space = len(output_data)
                
            
        elif output & 0xffff == 0x8099:  # Icon
            wrap_len += 1
        elif output & 0xffff == 0x8093: # Name
            wrap_len += 4
        elif output & 0xffff == 0x8080: # Newline
            wrap_len = 0
            
        #if wrap_len > WORDWRAP_LENGTH and do_wordwrapping:
        #    # Newline at last space
        #    # Don't attempt to newline at a new line
        #    
        #    if last_space == len(output_data):
        #        size = 0
        #    elif last_space != -1:
        #        output_data[last_space] = 0x80
        #        output_data[last_space + 1] = 0x80
        #    else:
        #        output_data.append(0x80)
        #        output_data.append(0x80)
        #        
        #    wrap_len = 0
        #    last_space = -1
            
        if size == 3:
            had_command = True
            
        while size > 0:
            output_data.append(output & 0xff)
            output >>= 8
            size -= 1
            
        i += 1
            
    output_data.append(0x00) # null-terminated string
    return output_data

def insert_script(input_filename):
    charmap_file = ""
    charmap = {}
    
    output_filenames = []
    current_output = -1
    
    script_data = []
    
    instanttext = False

    with open(input_filename, "r") as file:
        for line in file:
            line = line.strip("\n").strip("\r").lstrip()
            
            if "//" in line:
                line = line[:line.index("//")]
            
            if line.strip() == "":
                continue
            
            #print line
            if line.startswith("#"):
                command_end = line.index(" ")
                
                if command_end == -1:
                    print "Invalid command"
                    exit(-1)
                
                command = line[1:command_end]
                #print "Command: %s" % command
                
                if command == "charmap":
                    # Handle charmap command
                    
                    if '"' not in line:
                        print "Invalid charmap command"
                        exit(-1)
                    
                    charmap_start = line.index('"') + 1
                    charmap_end = line.index('"', charmap_start)
                    charmap_file = line[charmap_start:charmap_end]
                    charmap = read_charmap(charmap_file)
                
                elif command == "altcharmap":
                    # Handle altcharmap command
                    # Same thing as charmap, except it tries to merge the new charmap with the old to provide alternative mappings
                    
                    if '"' not in line:
                        print "Invalid altcharmap command"
                        exit(-1)
                    
                    altcharmap_start = line.index('"') + 1
                    altcharmap_end = line.index('"', altcharmap_start)
                    altcharmap_file = line[altcharmap_start:altcharmap_end]
                    altcharmap = read_charmap(altcharmap_file)
                    
                    # Merge the two charmaps
                    charmap = dict(altcharmap.items() + charmap.items())
                
                elif command == "save":
                    # Handle save command
                    
                    if '"' not in line:
                        print "Invalid save command"
                        exit(-1)
                    
                    save_start = line.index('"') + 1
                    save_end = line.index('"', save_start)
                    save = line[save_start:save_end]
                    output_filenames.append(save)
                    
                    script_data.append({})
                    
                elif command == "output":
                    # Handle output
                    output = line[command_end+1:].strip()

                    if output == "all":
                        current_output = -1
                    else:
                        current_output = int(output) - 1
                        
                    if current_output != -1 and current_output > len(script_data):
                        print "Invalid output index: %d" % current_output
                        exit(-1)
                    
                    #print line
                    #print "Output set to %d" % current_output
                    
                elif command == "instanttext":
                    # Read instant text flag
                    output = line[command_end+1:].strip()

                    if output == "on":
                        instanttext = True
                    else:
                        instanttext = False
                    
                else:
                    print "Unknown command"
                    exit(-1)
                    
            elif "<" not in line or ">" not in line:
                print "Not a valid line"
                exit(1)
            
            else:            
                id_start = line.index("<") + 1
                id_end = line.index(">", id_start)
                id = line[id_start:id_end]
                text = line[id_end+1:].strip()
                
                if len(id) > 0 and id[0:2] == '**':
                    # Mark ** lines as finished but don't insert them
                    id = id[2:]
                    
                elif len(id) > 0 and id[0] == '*':
                    # Only strings with a * before the ID will be inserted
                    id = id[1:]
                
                    # Get ID of string
                    id = int(id, 16)
                    
                    if instanttext:
                        # Add code to make text instant for English translation
                        text = "[emit=88][emit=80][emit=5f][emit=00][emit=5f][emit=00]" + text
                    
                    # Translate string data into the correct charset and handle any embedded commands
                    text = parse_string(text, charmap)
                    
                    if current_output == -1:
                        for script in script_data:
                            script[id] = text
                    else:
                        script_data[current_output][id] = text

    for i in range(0, len(output_filenames)):
        insert_dmr(output_filenames[i], script_data[i])
                    
insert_script("lnk-script.txt")