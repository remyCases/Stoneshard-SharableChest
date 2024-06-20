using ModShardLauncher;
using ModShardLauncher.Mods;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System;
using System.Diagnostics.Tracing;
using UndertaleModLib.Models;
using Serilog;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Compiler;

namespace SharableChest;
public class SharableChest : Mod
{
    public override string Author => "zizani";
    public override string Name => "SharableChest";
    public override string Description => "mod_description";
    public override string Version => "0.1.0.0";
    public override void PatchMod()
    {
        IEnumerable<(string, int, int)> tags = Msl.GetRooms()
            .Where(room => room.GameObjects.Any(u => u.ObjectDefinition.Name.Content == "o_player_chest"))
            .SelectMany(room => room.GameObjects.Where(gameObject => gameObject.ObjectDefinition.Name.Content == "o_player_chest")
                .Select(gameObject => (room.Name.Content, gameObject.X, gameObject.Y)));

        Init(tags);

        DataLoader.data.GameObjects.IndexOf(DataLoader.data.GameObjects.First(x => x.Name.Content == "o_inv_etnarch_mask"));

    }

    public void Init(IEnumerable<(string, int, int)> tags)
    {
        string function = ModFiles.GetCode("scr_mod_playerContainersMapInit.gml");
        string insert = tags.Select(x => $@"ds_map_add(global._mod_playerContainerTagsMap, ""{x.Item1}"", undefined)").Collect();

        /* 
        insert += tags.Select(x => 
            $@"
                coords = scr_glmap_getLocation(""{x.Item1}"");
                if (is_undefined(coords))
                {{
                    scr_msl_log(""undefined coords from room {x.Item1}"");
                }}
                else
                {{
                    scr_msl_log(""room {x.Item1}: coord "" + string(coords));
                    realtag = string_join(""_"", ""{x.Item1}"", string(coords.x), string(coords.y), ""{x.Item2}"", ""{x.Item3}"");
                    ds_list_add(global.tag_player_chest, realtag);
                }}
            "
        )
        .Collect();
        */

        Msl.AddFunction(string.Format(function, insert), "scr_mod_playerContainersMapInit");

        Msl.LoadGML("gml_GlobalScript_scr_load_container")
            .MatchFrom(@"loot_save_tag = argument0")
            .InsertBelow(@"
if (loot_save_tag == ""caravan_stash"")
{
    scr_msl_log(""player chest, tag: "" + loot_save_tag + "" "" + string(id) +  "" "" + object_get_name(object_index))
    var testlist = ds_map_find_value(global.containersLootDataMap, loot_save_tag);
    if (!__is_undefined(testlist))
    {
        for (var i = 0; i < ds_list_size(testlist); i++)
        {
            scr_msl_log(string(ds_list_find_value(testlist, i)));
        }
    }
}
else
{
    scr_msl_log(""not player chest, tag: "" + loot_save_tag + "" "" + string(id) +  "" "" + object_get_name(object_index))
}
            ")
            .Save();

        Msl.LoadGML("gml_Object_o_player_chest_Other_15")
            .MatchAll()
            .ReplaceBy(ModFiles, "gml_Object_o_player_chest_Other_15.gml")
            .Save();

        Msl.LoadGML("gml_Object_o_player_chest_Alarm_1")
            .MatchAll()
            .ReplaceBy(@"with (scr_container_create(o_stash_inventory))
            {
                name = other.name
                parent = other.id
            }")
            .Save();

        Msl.AddNewEvent("o_player_chest", @"scr_load_container(""caravan_stash"")", EventType.Alarm, 3);

        Msl.LoadGML("gml_Object_o_player_KeyPress_115")
            .MatchAll()
            .ReplaceBy(@"if(variable_global_exists(""__debug_on"")) {global.__debug_on = !(global.__debug_on)} else {global.__debug_on = true}")
            .Save();

        Msl.AddFunction(@"
function scr_msl_debug_extrem(argument0)
{
    if(variable_global_exists(""__debug_on"") && global.__debug_on)
    {
        if(variable_global_exists(""containersLootDataMap""))
        {
            scr_msl_log(argument0 + "" "" + string(id) +  "" "" + object_get_name(object_index) + "" : "" + string(ds_list_size(ds_map_find_value(global.containersLootDataMap, ""caravan_stash""))))
        }
        else
        {
            scr_msl_log(""containersLootDataMap does not exist"");
        }
    }
    
}", "scr_msl_debug_extrem");

        int instruction_index = 0;
        uint diff_children = 0;
        GlobalDecompileContext context = new(DataLoader.data, false);
/*
        foreach(UndertaleCode code in DataLoader.data.Code.Where(x => !(x.Name.Content.Contains("Step") || x.Name.Content.Contains("msl") || x.Name.Content.Contains("Draw"))))
        {
            if (code.Instructions.Count > 0 && !(code.Name.Content.Contains("Step") || code.Name.Content.Contains("Draw")))
            {
                if(code.ChildEntries.Exists(x => x.Name.Content.Contains("_struct_") 
                    || x.Name.Content.Contains("_GlobalScript_") 
                    || x.Name.Content.Contains("_anon_") 
                    || x.Name.Content.Contains("Menu")
                    || x.Name.Content.Contains("scr_actionsLogUpdate") 
                    || x.Name.Content.Contains("scr_smoothRoomChange")
                    || x.Name.Content.Contains("scr_cursorUpdate") 
                    || x.Name.Content.Contains("scr_screenPositionCenter") 
                    || x.Name.Content.Contains("draw")
                    || x.Name.Content.Contains("hover")
                    || x.Name.Content.Contains("room")
                    || x.Name.Content.Contains("scr_approach") 
                    || x.Name.Content.Contains("globalmap") 
                    || x.Name.Content.Contains("scr_controlActionKeyLoad")))
                {
                    continue;
                }
                /* instruction_index = 0;
                diff_children = 0;
                foreach (UndertaleCode child in code.ChildEntries)
                {   
                    UndertaleInstruction[] debugAsm = {
                        AssemblyWrapper.PushString(child.Name.Content),
                        AssemblyWrapper.ConvStringVar(),
                        AssemblyWrapper.Call("gml_Script_scr_msl_debug_extrem", 1),
                        AssemblyWrapper.Popz(),
                    };
                    uint diff_adress = (uint)debugAsm.Sum(x => x.CalculateInstructionSize());

                    while (code.Instructions[instruction_index].Address < child.Offset / 4)
                    {
                        instruction_index++;
                        if (instruction_index >= code.Instructions.Count) 
                        {
                            Log.Warning("Out of bound for {0}", child.Name.Content);
                            break;
                        }
                    }

                    if (instruction_index >= code.Instructions.Count) 
                    {
                        Log.Warning("Out of bound for {0}", child.Name.Content);
                        break;
                    }
                    code.Instructions[instruction_index - 1].JumpOffset += (int)diff_adress;
                    code.Instructions.InsertRange(instruction_index, new List<UndertaleInstruction>(debugAsm));
                    child.Offset += diff_children;
                    diff_children += diff_adress * 4;
                }
                code.UpdateAddresses();
                code.AppendGML(string.Format(@"scr_msl_debug_extrem(""{0}"")", code.Name.Content), DataLoader.data);
            }
        } */

        Msl.LoadGML("gml_Object_o_side_inventory_Other_25")
            .MatchAll()
            .InsertAbove(@$"scr_msl_debug_extrem(""gml_Object_o_side_inventory_Other_25 0"")")
            .MatchAll()
            .InsertBelow(@$"scr_msl_debug_extrem(""gml_Object_o_side_inventory_Other_25 1"")")
            .MatchFrom("scr_save_item")
            .InsertAbove(@$"scr_msl_debug_extrem(""gml_Object_o_side_inventory_Other_25 2"")")
            .Save();

        Msl.LoadGML("gml_GlobalScript_scr_save_item")
            .MatchFrom("{")
            .InsertBelow(@$"scr_msl_debug_extrem(""gml_GlobalScript_scr_save_item 0"")")
            .MatchFrom("return")
            .InsertAbove(@$"scr_msl_debug_extrem(""gml_GlobalScript_scr_save_item 1"")")
            .MatchFrom("ds_list_clear")
            .InsertBelow(@$"scr_msl_debug_extrem(""gml_GlobalScript_scr_save_item 2"")")
            .MatchFrom("with (o_inv_slot)\n{")
            .InsertBelow(@$"scr_msl_debug_extrem(""gml_GlobalScript_scr_save_item 3 owner "" + string(owner) + "" "" + object_get_name(object_index))")
            .MatchFrom("owner == argument1\n{")
            .InsertBelow(@$"scr_msl_debug_extrem(""gml_GlobalScript_scr_save_item 4"")")
            .MatchFrom("var _item")
            .InsertAbove(@$"scr_msl_debug_extrem(""gml_GlobalScript_scr_save_item _id_name "" + string(_id_name))")
            .MatchFrom("ds_list_add(argument0, _item)")
            .InsertAbove(@$"scr_msl_debug_extrem(""gml_GlobalScript_scr_save_item _item "" + string(ds_list_find_value(_item, 0)))")
            .Save();

        Msl.LoadGML("gml_GlobalScript_scr_loadContainerContent")
            .MatchFrom("function\n{")
            .InsertBelow(@"scr_msl_debug_extrem(""gml_GlobalScript_scr_loadContainerContent"")")
            .Save();

        int index = DataLoader.data.GameObjects.IndexOf(DataLoader.data.GameObjects.First(x => x.Name.Content == "o_container_backpack"));
        Msl.LoadGML("gml_GlobalScript_scr_adaptiveMenusPositionUpdate")
            .MatchFrom($"case {index}:")
            .InsertBelow("case o_player:")
            .Peek()
            .Save();
    }
}