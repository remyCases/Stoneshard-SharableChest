function scr_mod_playerContainersMapInit()
{{
    if global.is_load_game
        global._mod_playerContainerTagsMap = ds_map_find_value(global.saveDataMap, "_mod_playerContainerTagsMap")
    else
    {{
        global._mod_playerContainerTagsMap = __dsDebuggerMapCreate()
        {0}
        ds_map_add_map(global.saveDataMap, "_mod_playerContainerTagsMap", global._mod_playerContainerTagsMap)
    }}
}}

