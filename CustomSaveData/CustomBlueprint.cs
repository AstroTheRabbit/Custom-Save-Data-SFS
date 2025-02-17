using System;
using System.Linq;
using System.Reflection.Emit;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using Newtonsoft.Json;
using SFS;
using SFS.IO;
using SFS.World;
using SFS.Parts;
using SFS.Builds;
using SFS.Parsers.Json;
using SFS.Translations;

namespace CustomSaveData
{
    [Serializable]
    public class CustomBlueprint : Blueprint
    {
        static CustomBlueprint() {}
        /// <summary>
        /// The custom data of a <c>CustomBlueprint</c>. Do not access directly; use <c>CustomBlueprint.AddCustomData</c> and <c>CustomBlueprint.GetCustomData</c> instead.
        /// </summary>
        [JsonProperty(Order = 1)]
        public Dictionary<string, object> customData = new Dictionary<string, object>();

        [JsonConstructor]
        public CustomBlueprint() : base() { }

        public CustomBlueprint(Blueprint blueprint)
        {
            center = blueprint.center;
            parts = blueprint.parts;
            stages = blueprint.stages;
            rotation = blueprint.rotation;
            offset = blueprint.offset;
            interiorView = blueprint.interiorView;
        }

        public void AddCustomData(string id, object data)
        {
            customData.Add(id, data);
        }

        public void RemoveCustomData(string id)
        {
            customData.Remove(id);
        }

        public bool GetCustomData<D>(string id, out D data)
        {
            if (customData.TryGetValue(id, out object retrievedData))
            {
                if (retrievedData is D typedData)
                {
                    data = typedData;
                    return true;
                }
            }
            data = default;
            return false;
        }
    }

    public class CustomBlueprintHelper
    {
        /// <summary>
        /// Called before a <c>CustomBlueprint</c> is saved. Can be used to add custom data to the <c>CustomBlueprint</c> before it is saved.
        /// </summary>
        public event Action<CustomBlueprint> OnSave;

        /// <summary>
        /// Called after <c>CustomBlueprint</c> is loaded. Can be used to load custom data from <c>CustomBlueprint</c>.
        /// </summary>
        public event Action<CustomBlueprint> OnLoad;

        /// <summary>
        /// Called when a <c>CustomBlueprint</c> is launched. Can be used to transfer custom data from a launched <c>CustomBlueprint</c> to its resulting <c>Rocket[]</c> and <c>Part[]</c> arrays.
        /// </summary>
        public event Action<CustomBlueprint, Rocket[], Part[]> OnLaunch;

        internal void Invoke_OnSave(CustomBlueprint blueprint)
        {
            try
            {
                OnSave?.Invoke(blueprint);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal void Invoke_OnLoad(CustomBlueprint blueprint)
        {
            try
            {
                OnLoad?.Invoke(blueprint);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal void Invoke_OnLaunch(CustomBlueprint blueprint, Rocket[] rockets, Part[] parts)
        {
            try
            {
                OnLaunch?.Invoke(blueprint, rockets, parts);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    namespace Patches
    {
        [HarmonyPatch(typeof(BuildState), nameof(BuildState.GetBlueprint))]
        static class BuildState_GetBlueprint
        {
            static void Postfix(ref Blueprint __result)
            {
                CustomBlueprint res = new CustomBlueprint(__result);
                Main.BlueprintHelper.Invoke_OnSave(res);
                __result = res;
            }
        }

        [HarmonyPatch(typeof(Blueprint), nameof(Blueprint.TryLoad))]
        static class Blueprint_TryLoad
        {
            static bool Prefix(FolderPath path, I_MsgLogger errorLogger, ref Blueprint blueprint, out bool __result)
            {
                if (path.FolderExists() && JsonWrapper.TryLoadJson(path.ExtendToFile("Blueprint.txt"), out CustomBlueprint customBlueprint))
                {
                    blueprint = customBlueprint;
                    __result = true;
                }
                else
                {
                    errorLogger.Log(Loc.main.Load_Failed.InjectField(Loc.main.Blueprint, "filetype").Inject(path, "filepath"));
                    blueprint = null;
                    __result = false;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(BuildState), nameof(BuildState.LoadBlueprint))]
        static class BuildState_LoadBlueprint
        {
            static void Postfix(Blueprint blueprint)
            {
                CustomBlueprint bp = blueprint as CustomBlueprint;
                Debug.Log(bp.customData.Count);
                foreach (string key in bp.customData.Keys)
                {
                    Debug.Log(key);
                }
                Main.BlueprintHelper.Invoke_OnLoad(blueprint as CustomBlueprint);
            }
        }

        [HarmonyPatch(typeof(RocketManager), nameof(RocketManager.SpawnBlueprint))]
        static class RocketManager_SpawnBlueprint
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ret)
                    {
                        codes.InsertRange
                        (
                            i,
                            new CodeInstruction[]
                            {
                                // Load `Main.BlueprintHelper`.
                                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Main), nameof(Main.BlueprintHelper))),
                                // Load `Blueprint`.
                                new CodeInstruction(OpCodes.Ldarg_0),
                                // Load `Rocket[]`.
                                new CodeInstruction(OpCodes.Ldloc_3),
                                // Load `Part[]`.
                                new CodeInstruction(OpCodes.Ldloc_0),
                                // Call  `CustomBlueprintHelper.Invoke_OnLaunch`.
                                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(CustomBlueprintHelper), nameof(CustomBlueprintHelper.Invoke_OnLaunch))),
                            }
                        );
                        break;
                    }
                }
                return codes;
            }
        }
    }
}