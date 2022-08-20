﻿using HarmonyLib;
using OCBNET;

class HarmonyFirePatch
{
    [HarmonyCondition("HasConfig(SCore;Fire)")]
    [HarmonyCondition("HasConfig(SCore;Base)")]
    [HarmonyPatch(typeof(EntityPlayerLocal))]
    [HarmonyPatch("Update")]
    public class EntityPlayerLocalUpdate
    {
        static void Prefix()
        {
            Log.Out("Update");
        }
    }
}