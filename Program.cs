﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
// TODO LIST
/*  
 * 
 *  select the value after the colons?
 *  dictionary for items, so there are no repeats
 *  
 */


namespace Plugins_to_cpp_h
{
    internal class Program{
        const string version = "0.4.0";

        static XmlNode root_node;
        static StreamWriter file;

        static void Main(string[] args){
            string orign_plugins_path = "C:\\Users\\Joe bingle\\Downloads\\plugins\\";
            string output_converted_path = "C:\\Users\\Joe bingle\\Downloads\\plguins_cpp\\";
            if (!Directory.Exists(orign_plugins_path)){
                Console.WriteLine("Failed to find specified input path, retry with the correct path");
                return;
            }
            if (!Directory.Exists(output_converted_path)){
                Console.WriteLine("Failed to find specified output path, retry with the correct path");
                return;
            }
            Console.WriteLine("generating shortguids.txt");

            // generate a list of short guids, to beable to use with the program
            file = new StreamWriter(output_converted_path + "shortguids.txt");

            foreach (var plugin in Directory.GetFiles(orign_plugins_path)){
                if (Path.GetExtension(plugin) != ".xml") continue;
                XmlDocument xml = new();
                xml.Load(plugin);
                XmlNode first_struct = xml.SelectSingleNode("root").ChildNodes[0];
                string guid = first_struct.Name.Substring(1);
                ulong guid_1 = Convert.ToUInt64(guid.Substring(0, 16), 16);
                ulong guid_2 = Convert.ToUInt64(guid.Substring(16, 16), 16);
                ulong short_guid = guid_1 ^ guid_2;
                file.WriteLine(Path.GetFileNameWithoutExtension(plugin) + " : " + short_guid);
            }


            file.Close();
            file.Dispose();

            goto Start;
        Error:
            Console.WriteLine("conversion failed");
        Start:
            Console.WriteLine("Hello, World!\nSelect the plugin to convert\nExample: \"vehi\"\n");
            string plugin_name = Console.ReadLine();
            string chosen_path = orign_plugins_path + plugin_name + ".xml";
            if (!File.Exists(chosen_path)) goto Error;

            Console.WriteLine("beginning conversion from: " + chosen_path);

            XmlDocument xdoc = new();
            xdoc.Load(chosen_path);
            root_node = xdoc.SelectSingleNode("root");

            string output_path = output_converted_path + plugin_name + ".h";
            file = new StreamWriter(output_path);
            // file prelude header

            file.WriteLine("/*");
            file.WriteLine("; CONTENT AUTOGENERATED BY CODENAME ATRIOX: PLUGIN CONVERTOR");
            file.WriteLine("; CONVERTOR VERSION: " + version);
            file.WriteLine("; SOURCE TIMESTAMP: " + "[EXAMPLE]");
            file.WriteLine("; SOURCE GAME VERSION: " + "[EXAMPLE]");
            file.WriteLine("; SOURCE PLUGIN: " + plugin_name);
            file.WriteLine("; GENERATED TIMESTAMP: " + DateTime.Today.Date.ToString("dd/MM/yyyy") + " -> " + DateTime.Now.ToString("h:mm:ss tt"));
            file.WriteLine("*/");
            file.WriteLine("");
            file.WriteLine("#include \"commons.h\"");
            file.WriteLine("#pragma pack(push, 1)");
            file.WriteLine("");
            structs_queue.Add(root_node.ChildNodes[0]); // assume the first struct is the root struct
            for (int i = 0; i < structs_queue.Count; i++) process_node(structs_queue[i]);
            file.WriteLine("");
            file.WriteLine("// /////////////// //");
            file.WriteLine("// FLAG REFERENCES //");
            file.WriteLine("// /////////////// //\n");
            for (int i = 0; i < flags_queue.Count; i++) process_flag(flags_queue[i]);
            file.WriteLine("");
            file.WriteLine("// /////////////// //");
            file.WriteLine("// ENUM REFERENCES //");
            file.WriteLine("// /////////////// //\n");
            for (int i = 0; i < enums_queue.Count; i++) process_enum(enums_queue[i]);
            file.WriteLine("// ///////////////// //");
            file.WriteLine("// STRUCT REFERENCES //");
            file.WriteLine("// ///////////////// //\n");
            // now we actually write the struct, and we do it in the inverse order that we found them in, hopefully to fix all errors
            for (int i = struct_lines.Count-1; i >= 0; i--) for (int c = 0; c < struct_lines[i].Count; c++) file.WriteLine(struct_lines[i][c]);

            file.WriteLine("#pragma pack(pop)");
            file.Close();
            file.Dispose();

            // then output the header file
            //file = new StreamWriter(other_output_path);
            //file.WriteLine("#pragma once");
            //file.WriteLine("#include \"commons.h\"");

            //for (int i = 0; i < structs_queue.Count; i++)   file.WriteLine("struct " + filter_string(structs_queue[i].Attributes?["Name"]?.Value) + ";");
            //for (int i = 0; i < flags_queue.Count; i++)     file.WriteLine("struct " + filter_string(flags_queue[i].Attributes?["StructName1"]?.Value) + ";");
            //for (int i = 0; i < enums_queue.Count; i++)     file.WriteLine("enum " + filter_string(enums_queue[i].Attributes?["StructName1"]?.Value) + ";");

            //file.Close();
            //file.Dispose();

            Console.WriteLine("finished conversion to: " + output_path + "\n");
            goto Start;
        }
        static Dictionary<string, XmlNode> found_structures = new(); //for each of these we need to check if it does actually match
        // if it doesn't match then we probably are going to have an interesting problem
        static List<XmlNode> structs_queue = new();
        static List<List<string>> struct_lines = new();
        static List<XmlNode> flags_queue = new();
        static List<XmlNode> enums_queue = new();
        static bool was_already_written(XmlNode node_that_were_about_to_write, string node_string){
            if (found_structures.ContainsKey(node_string)){
                XmlNode comparison_node = found_structures[node_string];
                if (do_item_params_match(node_that_were_about_to_write, comparison_node)) return true;
                else Debug.Assert(false, "non-matching reference with the same name, very bad");
            }else found_structures.Add(node_string, node_that_were_about_to_write);
            return false;
        }
        static bool do_item_params_match(XmlNode node_A, XmlNode node_B){
            // test what type this is (either a struct, flag or enum)
            byte A_type = 2;
            byte B_type = 2;
            // flags & enums have 3 or less characters for their name // enums: 0, flags: 1, structs: 2
            if (node_A.Name.Length <= 3) A_type = (byte)((byte)(Convert.ToByte(node_A.Name.Substring(1), 16) - 10) /3);
            if (node_B.Name.Length <= 3) B_type = (byte)((byte)(Convert.ToByte(node_B.Name.Substring(1), 16) - 10) /3);
            if (A_type != B_type) return false;

            // here we do the guid matching test, we simply check if the guids match, if they do then they are 100% the same struct
            if (A_type == 2) return node_A.Name == node_B.Name;

            // check if the child count is the same so we dont get any indexing errors, else they're likely not the same
            if (node_A.ChildNodes.Count != node_B.ChildNodes.Count) return false;

            // go through each child node and match their names up, if theres no differences then return because this is a copy
            for (int i = 0; i < node_A.ChildNodes.Count; i++)
                if (node_A.ChildNodes[i].Attributes?["n"]?.Value != node_B.ChildNodes[i].Attributes?["n"]?.Value) return false;

            return true;
        }
        static void process_enum(XmlNode current_param){
            string target_node = filter_string(current_param.Attributes?["StructName1"]?.Value);
            if (was_already_written(current_param, target_node)) return;
            // for writing the exact bit number if inherited enums do not work
            //byte group_id = Convert.ToByte(current_param.Name.Substring(1), 16);
            //int enum_bytes = 1 << (group_id-10); // 1 byte, 2 bytes, 4 bytes 
            //int enum_bits = enum_bytes * 8;
            string class_type = "";
            if      (current_param.Name == "_A") class_type = "uint8_t";
            else if (current_param.Name == "_B") class_type = "uint16_t";
            else if (current_param.Name == "_C") class_type = "uint32_t";
            // idk how to assign the size of an enum, o we'll have t ocome abck to that one
            file.WriteLine("enum " + target_node + " : " + class_type + " {");
            for(int i = 0; i < current_param.ChildNodes.Count; i++){
                XmlNode current = current_param.ChildNodes[i];
                file.WriteLine("   " + filter_string(current.Attributes?["n"]?.Value) + " = " + i +",");
            }

            file.WriteLine("};");
        }
        static void process_flag(XmlNode current_param)
        {
            string target_node = filter_string(current_param.Attributes?["StructName1"]?.Value);
            if (was_already_written(current_param, target_node)) return;


            file.WriteLine("struct " + target_node + "{");
            int bit_count = -1;
            if      (current_param.Name == "_D"){ bit_count = 32; file.WriteLine("   uint32_t content;");}
            else if (current_param.Name == "_E"){ bit_count = 16; file.WriteLine("   uint16_t content;");}
            else if (current_param.Name == "_F"){ bit_count =  8; file.WriteLine("   uint8_t content;");}

            // write the size of thew struct
            // then process all the children into static access methods
            for (int i = 0; i < current_param.ChildNodes.Count; i++)
            {
                XmlNode current = current_param.ChildNodes[i];
                // ulong flag_index = (ulong)1 << i;

                char[] chars = new string('0', bit_count).ToCharArray();
                chars[chars.Length - 1 - i] = '1';
                string bit_mask = new string(chars);

                // this may need fixing tbh
                file.WriteLine("   bool " + filter_string(current.Attributes?["n"]?.Value) + "() { return ( content  & 0b" + bit_mask + "); }");
            }
            file.WriteLine("};");
        }
        static void process_node(XmlNode current_struct)
        {
            if (was_already_written(current_struct, current_struct.Name)) return;
            // struct name
            List<string> current_struct_lines = new();
            current_struct_lines.Add("struct " + filter_string(current_struct.Attributes?["Name"]?.Value) + "{");

            foreach (XmlNode param in current_struct.ChildNodes)
            {
                process_n_param(param, current_struct_lines);
            }



            current_struct_lines.Add("};");
            struct_lines.Add(current_struct_lines);
        }
        static void process_n_param(XmlNode param, List<string> lines)
        {
            byte group_id = Convert.ToByte(param.Name.Substring(1), 16);
            string group_name = group_names[group_id].cpp;
            string param_name = filter_string(param.Attributes?["Name"]?.Value);

            switch (group_id)
            {
                // string types
                case 0:
                    lines.Add("   " + group_name + " " + param_name + "[32];");
                    break;
                case 1:
                    lines.Add("   " + group_name + " " + param_name + "[256];");
                    break;
                case 9:
                    lines.Add("   " + group_name + " " + param_name + "[4];");
                    break;
                // enum types
                case 10:
                case 11:
                case 12:{
                        string target_node = filter_string(param.Attributes?["StructName1"]?.Value);
                        lines.Add("   " + target_node + " " + param_name + ";");
                        enums_queue.Add(param);
                    }break;
                // flags types
                case 13:
                case 14:
                case 15:{
                        string target_node = filter_string(param.Attributes?["StructName1"]?.Value);
                        lines.Add("   " + target_node + " " + param_name + ";");
                        flags_queue.Add(param);
                    }break;
                // spacer types
                case 52:
                case 53:
                    int length = Convert.ToInt32(param.Attributes?["Length"]?.Value);
                    lines.Add("   " + group_name + " " + param_name + "["+ length + "];");
                    break;
                // not to be processed types
                case 54:
                case 55:
                case 59:
                    break; // aka exiting the switch entirely
                // struct types
                case 56:{ 
                    string referenced_struct = param.Attributes?["GUID"]?.Value;
                    XmlNode next_struct = root_node.SelectSingleNode("_"+referenced_struct);
                    string next_struct_name = filter_string(next_struct.Attributes?["Name"]?.Value);
                    lines.Add("   " + next_struct_name + " " + param_name + ";");
                    structs_queue.Add(next_struct);
                   }break;
                case 57:{ 
                    string referenced_struct = param.Attributes?["GUID"]?.Value;
                    XmlNode next_struct = root_node.SelectSingleNode("_"+referenced_struct);
                    string next_struct_name = filter_string(next_struct.Attributes?["Name"]?.Value);
                    string next_struct_count = next_struct.Attributes?["Count"]?.Value;
                    lines.Add("   " + next_struct_name + " " + param_name + "["+ next_struct_count + "];");
                    structs_queue.Add(next_struct);
                   }break;
                // template types
                case 67:
                case 64:{ 
                    string referenced_struct = param.Attributes?["GUID"]?.Value;
                    XmlNode next_struct = root_node.SelectSingleNode("_"+referenced_struct);
                    string next_struct_name = filter_string(next_struct.Attributes?["Name"]?.Value);
                    lines.Add("   " + group_name + "<" + next_struct_name + "> " + param_name + ";");
                    structs_queue.Add(next_struct);
                   }break;

                case 68:
                    lines.Add("   " + group_name + " " + param_name + ";" + " // WARNING: THIS TYPE WILL CAUSE ISSUES //");
                    break;

                // unspecified types
                default:
                    lines.Add("   " + group_name + " " + param_name + ";");
                    break;
            }
        }
        // new pattern to do only the ones we want:: [^a-zA-Z0-9 -]
        static string _reg_pattern = "[^a-zA-Z0-9_]";
        static Regex _reg_regEx = new Regex(_reg_pattern);
        static string filter_string(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "BLANK";
            string output = Regex.Replace(_reg_regEx.Replace(input, "_"), @"\s+", " ");
            if (0x2A < output[0] && output[0] < 0x3A ) return "_" + output;
            return output;
        }

        public struct string_pair{
            public string xml;
            public string cpp;
            public string_pair(string _xml, string _cpp){
                xml = _xml;
                cpp = _cpp;
        }}

        // ##SOR = "special output required"
        // ##SCT = "special case type"
        // ##NIT = "not included type"

        public static string_pair[] group_names = new string_pair[]
        {
            new ("string32", "char" ),          // ##SCT
            new ("string256", "char" ),         // ##SCT
            new ("stringID", "uint32_t" ),
            new ("UNKNOWN", "WWWWWWWWWWW" ),
            new ("sbyte", "int8_t" ),
            new ("short", "int16_t" ),
            new ("int", "int32_t" ),
            new ("long", "int64_t" ),
            new ("angle", "float" ),
            new ("group", "char" ),             // ##SCT
            new ("enum8", "" ),         // ##SOR
            new ("enum16", "" ),        // ##SOR
            new ("enum32", "" ),        // ##SOR
            new ("flags32", "" ),   // ##SOR
            new ("flags16", "" ),   // ##SOR
            new ("flags8", "" ),     // ##SOR
            new ("point2D", "_s_point2d" ),       
            new ("rectangle2D", "WWWWWWWWWWW" ),  
            new ("rgb", "_s_rgb" ),               
            new ("argb", "_s_argb" ),             
            new ("float", "float" ),
            new ("fraction", "float" ),
            new ("float2D", "_s_doublefloat" ),   
            new ("float3D", "_s_triplefloat" ),   
            new ("vector2D", "_s_doublefloat" ),  
            new ("vector3D", "_s_triplefloat" ),  
            new ("quarternion", "_s_quadfloat" ), 
            new ("euler2D", "_s_doublefloat" ),   
            new ("euler3D", "_s_triplefloat" ),   
            new ("plane2D", "_s_triplefloat" ),   
            new ("plane3D", "_s_quadfloat" ),     
            new ("rgb_float", "_s_rgbfloat" ),    
            new ("argb_float", "_s_argbfloat" ),  
            new ("hsv", "WWWWWWWWWWW" ),
            new ("ahsv", "WWWWWWWWWWW" ),
            new ("short_r", "_s_shortrange" ),     
            new ("angle_r", "_s_floatrange" ),     
            new ("float_r", "_s_floatrange" ),     
            new ("fraction_r", "_s_floatrange" ),  
            new ("UNKNOWN", "WWWWWWWWWWW" ),
            new ("UNKNOWN", "WWWWWWWWWWW" ),
            new ("b_flags32", "WWWWWWWWWWW" ),
            new ("b_flags16", "WWWWWWWWWWW" ),
            new ("b_flags8", "WWWWWWWWWWW" ),
            new ("block8", "int8_t" ),
            new ("cblock8", "int8_t" ),
            new ("block16", "int16_t" ),
            new ("cblock16", "int16_t" ),
            new ("block32", "int32_t" ),
            new ("cblock32", "int32_t" ),
            new ("UNKNOWN", "WWWWWWWWWWW" ),
            new ("UNKNOWN", "WWWWWWWWWWW" ),
            new ("padding", "uint8_t" ),           // ##SCT
            new ("skip", "uint8_t" ),              // ##SCT
            new ("comment", "" ),       // ##NIT
            new ("c_comment", "" ),     // ##NIT
            new ("struct", "" ),                // ##SCT
            new ("array", "" ),                 // ##SCT 
            new ("UNKNOWN", "WWWWWWWWWWW" ),
            new ("struct_end", "" ),    // ##NIT
            new ("byte", "uint8_t" ),
            new ("ushort", "uint16_t" ),
            new ("uint", "uint32_t" ),
            new ("ulong", "uint64_t" ),
            new ("tagblock", "_s_tagblock" ),              // ##SCT
            new ("tagref", "_s_tagref" ),                // ##SCT
            new ("data", "_s_data" ),                  // ##SCT
            new ("resource", "_s_resource" ),              // ##SCT
            new ("file_reference", "uint32_t" ),
            new ("UNKNOWN", "WWWWWWWWWWW" ),
        };
        // SPECIAL STRUCTS THAT BELONG IN THE COMMON.H

    }











}