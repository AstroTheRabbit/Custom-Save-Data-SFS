using HarmonyLib;
using ModLoader;
using UITools;
using SFS.IO;
using System.Collections.Generic;

namespace CustomSaveData
{
    public class Main : Mod, IUpdatable
    {
        public override string ModNameID => "customsavedata";
        public override string DisplayName => "Custom Save Data";
        public override string Author => "Astro The Rabbit";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "v1.0";
        public override string Description => "A helper mod for saving and loading custom data to blueprints and rockets.";

        public Dictionary<string, FilePath> UpdatableFiles => new Dictionary<string, FilePath>() { { "https://github.com/AstroTheRabbit/Custom-Save-Data-SFS/releases/latest/download/CustomSaveData.dll", new FolderPath(ModFolder).ExtendToFile("CustomSaveData.dll") } };

        public override void Early_Load()
        {
            new Harmony(ModNameID).PatchAll();
        }
    }
}
