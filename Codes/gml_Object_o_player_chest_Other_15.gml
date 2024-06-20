
if ((!instance_exists(o_container)) && (!instance_exists(o_stash_inventory)))
{
    if loot_list
    {
        var _container = scr_loadContainerContent(loot_list, o_stash_inventory);
    }
}
