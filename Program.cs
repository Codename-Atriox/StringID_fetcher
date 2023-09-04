using Infinite_module_test;
using static Infinite_module_test.tag_structs;
using static Infinite_module_test.module_structs;
using System.Xml;
using System.Xml.Linq;

namespace StringID_fetcher
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("attempting to open target module!");


            string target_file = "";
            string plugins_path = "C:\\Users\\Joe bingle\\Downloads\\plugins";

            module mod = new module(target_file);

            // go through each directory
            foreach (var dir in mod.file_groups){
                foreach(var file in dir.Value){

                    // get the resources from the module
                    List<KeyValuePair<byte[], bool>> resource_list = new();
                    try{ // idk why we have a try catch here
                        List<byte[]> resulting_resources = mod.get_tag_resource_list(file.source_file_header_index);
                        foreach (byte[] resource in resulting_resources) {
                            bool is_standalone_resource = resource[0..4].SequenceEqual(new byte[] { 0x75, 0x63, 0x73, 0x68 }); // test for those 4 chars at the top of the file
                            resource_list.Add(new KeyValuePair<byte[], bool>(resource, is_standalone_resource));
                    }}catch (Exception ex){
                        Console.WriteLine(file.name + " failed to read resources: " + ex.Message);
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
                        Console.WriteLine(file.name + " returned an error: " + ex.Message);
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
            }}
        }
        class tag_crawler
        {
            tag tag;
            List<string> found_strings = new();
            List<uint> found_stringIDs = new();
            public tag_crawler(tag _tag){
                tag = _tag;}

            public void process_structure(tag.thing _struct, string GUID, uint struct_offset){
                // get the xml node that holds the data for this structure
                XmlNode? current_structure = tag.reference_root.SelectSingleNode("_" + GUID);
                if (current_structure == null) throw new Exception("failed to find struct node from GUID");

                // now process all the children
                foreach (XmlNode param in current_structure.ChildNodes){
                    uint offset = Convert.ToUInt32(param.Attributes["Offset"].Value, 16);
                    offset += struct_offset; // this is for structs & array structs, so we can offset to the correct position, even though we're reading a different group of params

                    switch (param.Name){ // maybe just do the switch on the name?
                        case "_38":{ // _field_struct 
                                string next_guid = param.Attributes["GUID"].Value;
                                process_structure(_struct, next_guid, offset); // this is inlined with that tag block entry
                            } continue;
                        case "_39":{ // _field_array
                                string next_guid = param.Attributes["GUID"].Value;
                                uint array_length = Convert.ToUInt32(param.Attributes?["Count"]?.Value);
                                uint array_struct_size = Convert.ToUInt32(tag.reference_root.SelectSingleNode('_' + next_guid).Attributes["Size"].Value, 16);

                                // iterate through all children of array, which are all inlined with tag block entry
                                for (uint i = 0; i < array_length; i++) 
                                    process_structure(_struct, next_guid, offset + (i * array_struct_size)); 
                            } continue;
                        case "_40":{ // _field_block_v2
                                if (!_struct.tag_block_refs.ContainsKey((ulong)offset)) continue; // if theres none, then this tagblock is probably empty
                                tag.tagdata_struct next_struct = _struct.tag_block_refs[offset];
                                string next_guid = next_struct.GUID; // param.Attributes["GUID"].Value; // we actually have two ways to get the GUID, im going with the intended method
                                uint array_struct_size = Convert.ToUInt32(tag.reference_root.SelectSingleNode('_' + next_guid).Attributes["Size"].Value, 16);

                                for (uint i = 0; i < next_struct.blocks.Count; i++)
                                    process_structure(next_struct.blocks[(int)i], next_guid, i * array_struct_size);
                            } continue;
                        case "_43":{ // tag_resource
                                //ResourceParam param = new(param_name, _struct.resource_file_refs[(ulong)offset], struct_link);
                                
                                if (!_struct.resource_file_refs.ContainsKey((ulong)offset)) continue; // if theres none, then this tagblock is probably empty

                                string next_guid = param.Attributes["GUID"].Value;
                                process_structure(struct_link, next_guid, current_line);
                            } continue;
                    }
                    

                }




            }
        }
    }
}