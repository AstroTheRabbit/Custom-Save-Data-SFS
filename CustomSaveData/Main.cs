using HarmonyLib;
using ModLoader;

namespace CustomSaveData
{
    public class Main : Mod
    {
        public static Main main;
        public override string ModNameID => "customsavedata";
        public override string DisplayName => "Custom Save Data";
        public override string Author => "Astro The Rabbit";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "1.0";
        public override string Description => "A helper mod for saving and loading custom data to blueprints and rockets.";
        
        CustomBlueprintHelper blueprintHelper;
        public static CustomBlueprintHelper BlueprintHelper
        {
            get
            {
                if (main.blueprintHelper == null)
                    main.blueprintHelper = new CustomBlueprintHelper();
                return main.blueprintHelper;
            }
        }

        CustomRocketSaveHelper rocketSaveHelper;
        public static CustomRocketSaveHelper RocketSaveHelper
        {
            get
            {
                if (main.rocketSaveHelper == null)
                    main.rocketSaveHelper = new CustomRocketSaveHelper();
                return main.rocketSaveHelper;
            }
        }

        public override void Early_Load()
        {
            new Harmony(ModNameID).PatchAll();
            main = this;
        }
    }
}
