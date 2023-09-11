using Infinite_module_test;
using static Infinite_module_test.tag_structs;
using static Infinite_module_test.module_structs;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.IO;

namespace StringID_fetcher
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("attempting to open target module!");

            string target_folder = "D:\\Programs\\Steam\\steamapps\\common\\Halo Infinite\\deploy";
            //string  = "D:\\Programs\\Steam\\steamapps\\common\\Halo Infinite\\deploy\\any\\globals\\forge\\forge_objects-rtx-new.module";
            string plugins_path = "C:\\Users\\Joe bingle\\Downloads\\plugins";

            string output_stringIDs_dir = "C:\\Users\\Joe bingle\\Downloads\\HASHING\\string_puller\\IDs.txt";
            string output_strings_dir = "C:\\Users\\Joe bingle\\Downloads\\HASHING\\string_puller\\strings.txt";





            Dictionary<string, bool> found_strings = new();
            Dictionary<uint, bool> found_stringIDs = new();

            var module_files = System.IO.Directory.GetFiles(target_folder, "*", SearchOption.AllDirectories);

            for (int i = 0; i < module_files.Length; i++){
                string target_file = module_files[i];
                Console.WriteLine(i + "/" + module_files.Length + ": " + target_file);
                if (!target_file.EndsWith(".module")){
                    Console.WriteLine("not a module file!");
                    continue;}
                try{module mod = new module(target_file);
                    int file_index = 0;
                    int total_Files = mod.files.Length;
                    // go through each directory
                    foreach (var dir in mod.file_groups){
                        if (dir.Key == "ÿÿÿÿ") continue; // these are the non-tag files

                        foreach(var file in dir.Value){
                            file_index++;
                            if (file.is_resource) continue;

                            // get the resources from the module
                            List<KeyValuePair<byte[], bool>> resource_list = new();
                            if (dir.Key != "hsc_") try{ // hsc_ tags are so annoying!!! thanks a lot johnathon halo (the resources for these are intentionally cleared by 343, causing oodle issues)
                                List<byte[]> resulting_resources = mod.get_tag_resource_list(file.source_file_header_index);
                                foreach (byte[] resource in resulting_resources) {
                                    bool is_standalone_resource = false;
                                    if (resource.Length > 4)
                                        is_standalone_resource = resource[0..4].SequenceEqual(tag_magic); // test for those 4 chars at the top of the file
                                    resource_list.Add(new KeyValuePair<byte[], bool>(resource, is_standalone_resource));
                            }}catch (Exception ex){
                                Console.WriteLine(file.name + " (" + dir.Key + ") failed to read resources: " + ex.Message);
                                continue;
                            }
                    
                    
                            // for debug purposes // make sure all resources are of the same type // NOTE: resource state should be an int, not a bool!!!
                            if (resource_list.Count > 0){
                                bool inital = resource_list[0].Value;
                                foreach (var resource in resource_list)
                                    if (resource.Value != inital)
                                        Console.WriteLine(file.name + " has resources with mis-matching chunked/non-chunked status!!");
                            }

                            // load & process the tag
                            tag test = new tag(plugins_path, resource_list);
                            try{byte[] tagbytes = mod.get_tag_bytes(file.source_file_header_index);
                                if (!test.Load_tag_file(tagbytes)){
                                    Console.WriteLine(file.name + " was not able to be loaded as a tag");
                                    continue;
                            }} catch (Exception ex){ 
                                Console.WriteLine(file.name + " (" + dir.Key + ") returned an error: " + ex.Message);
                                continue;
                            }

                            // now do whatever we want with the tag
                            // which is, just do a recursive search across all tagdata
                            if (test.root == null){
                                Console.WriteLine(file.name + " returned NO formatted tag data");
                                continue;}
                            if (test.root.blocks.Count != 1) throw new Exception("Debug moment!!! there should only ever be a single root block");
                            tag_crawler tag_thing = new(test);
                            tag_thing.process_structure(test.root.blocks[0], test.root.GUID, 0);


                            foreach (var v in tag_thing.found_strings)
                                found_strings[v.Key] = v.Value;
                            foreach (var v in tag_thing.found_stringIDs)
                                found_stringIDs[v.Key] = v.Value;

                            if (file_index % 1000 == 0) // only print every 1000, to keep it running smooth
                                Console.WriteLine(file_index + "/" + total_Files);
                        }
                    }


                }catch (Exception ex){Console.WriteLine(target_file + " failed: " + ex.Message);}


            }

            TextWriter strings_writer = new StreamWriter(output_strings_dir);
            TextWriter IDs_writer = new StreamWriter(output_stringIDs_dir);

            foreach (var v in found_strings) strings_writer.WriteLine(v.Key);
            foreach (var v in found_stringIDs) IDs_writer.WriteLine(v.Key.ToString("X8"));






        }
        static ulong bytes_thing_count;
        class tag_crawler{
            tag tag; // we'll add some code to 
            public Dictionary<string, bool> found_strings = new();
            public Dictionary<uint, bool> found_stringIDs = new();
            string output_bytecodes_dir = "C:\\Users\\Joe bingle\\Downloads\\HASHING\\string_puller\\bytecodes\\";
            public tag_crawler(tag _tag){
                tag = _tag;}
            private string strip_string(string input){
                int index = input.IndexOf('\0');
                if (index < 0) return input;
                return input.Substring(0, index);
            }
            public void process_structure(tag.thing _struct, string GUID, int struct_offset){
                // get the xml node that holds the data for this structure
                XmlNode? current_structure = tag.reference_root.SelectSingleNode("_" + GUID);
                if (current_structure == null) throw new Exception("failed to find struct node from GUID");

                // now process all the children
                foreach (XmlNode param in current_structure.ChildNodes){
                    int offset = Convert.ToInt32(param.Attributes["Offset"].Value, 16);
                    offset += struct_offset; // this is for structs & array structs, so we can offset to the correct position, even though we're reading a different group of params

                    switch (param.Name) {
                        // /////////////////////////////////// //
                        // the types we're going to dump from //
                        // ///////////////////////////////// //
                        case "_0": { // _field_string
                                string result = strip_string(Encoding.UTF8.GetString(_struct.tag_data, offset, 32));
                                if (!found_strings.ContainsKey(result))
                                    found_strings.Add(result, true);
                            } continue;
                        case "_1": { // _field_long_string
                                string result = strip_string(Encoding.UTF8.GetString(_struct.tag_data, offset, 256));
                                if (!found_strings.ContainsKey(result))
                                    found_strings.Add(result, true);
                            } continue;
                        case "_2":{ // _field_string_id
                                uint stringID = BitConverter.ToUInt32(_struct.tag_data[offset..(offset + 4)]);
                                if (!found_stringIDs.ContainsKey(stringID))
                                    found_stringIDs.Add(stringID, true);
                            } continue;
                        case "_42":{ // data array thing
                                // check if this is the target type
                                if (GUID == "D0FF4560788326B0C497F3C2A248E93A"){
                                    if (!_struct.tag_resource_refs.ContainsKey((ulong)offset)) continue;

                                    File.WriteAllBytes(output_bytecodes_dir + bytes_thing_count.ToString() + ".file", _struct.tag_resource_refs[(ulong)offset]);
                                    bytes_thing_count++;
                                }
                            } continue;
                        // ///////////////////////// //
                        // tagdata navigation types //
                        // /////////////////////// //
                        case "_38":{ // _field_struct 
                                string next_guid = param.Attributes["GUID"].Value;
                                process_structure(_struct, next_guid, offset); // this is inlined with that tag block entry
                            } continue;
                        case "_39":{ // _field_array
                                string next_guid = param.Attributes["GUID"].Value;
                                int array_length = Convert.ToInt32(param.Attributes?["Count"]?.Value);
                                int array_struct_size = Convert.ToInt32(tag.reference_root.SelectSingleNode('_' + next_guid).Attributes["Size"].Value, 16);

                                // iterate through all children of array, which are all inlined with tag block entry
                                for (int i = 0; i < array_length; i++) 
                                    process_structure(_struct, next_guid, offset + (i * array_struct_size)); 
                            } continue;
                        case "_40":{ // _field_block_v2
                                if (!_struct.tag_block_refs.ContainsKey((ulong)offset)) continue; // if theres none, then this tagblock is probably empty
                                tag.tagdata_struct next_struct = _struct.tag_block_refs[(ulong)offset];
                                string next_guid = next_struct.GUID; // param.Attributes["GUID"].Value; // we actually have two ways to get the GUID, im going with the intended method
                                int array_struct_size = Convert.ToInt32(tag.reference_root.SelectSingleNode('_' + next_guid).Attributes["Size"].Value, 16);

                                for (int i = 0; i < next_struct.blocks.Count; i++)
                                    process_structure(next_struct.blocks[i], next_guid, 0); // i thought these were aligned in a giant block, but apparently not, each index is a separate struct
                            } continue;
                        case "_43":{ // tag_resource
                                if (!_struct.resource_file_refs.ContainsKey((ulong)offset)) continue; // if theres none, then this resource is probably empty, although i cant actually remember how we formatted empty resources
                                tag.tagdata_struct? next_struct = _struct.resource_file_refs[(ulong)offset];
                                if (next_struct == null) continue; // ok so thats how we handle null resources
                                string next_guid = next_struct.GUID;

                                if (next_struct.blocks.Count == 0)
                                    continue; // im prettu sure this is for chunked resources maybe?
                                if (next_struct.blocks.Count != 1)
                                    throw new Exception("Debug moment!!! resource references should only have a single block");
                                process_structure(next_struct.blocks[0], next_guid, 0);
                            } continue;
                    }
                    

                }




            }
        }
    }
}