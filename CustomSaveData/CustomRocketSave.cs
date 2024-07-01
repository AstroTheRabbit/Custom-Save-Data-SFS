using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using Newtonsoft.Json;
using SFS.Stats;
using SFS.World;
using SFS.Parsers.Json;

namespace CustomSaveData
{
    [Serializable]
    [JsonConverter(typeof(WorldSave.LocationData.LocationConverter))]
    public class CustomRocketSave : RocketSave
    {
        /// <summary>
        /// The custom data of a <c>CustomRocketSave</c>. Do not access directly; use <c>CustomRocketSave.AddCustomData</c> and <c>CustomRocketSave.GetCustomData</c> instead.
        /// </summary>
        [JsonProperty]
        public Dictionary<string, object> customData = new Dictionary<string, object>();

        [JsonConstructor]
        public CustomRocketSave() : base() { }

        public CustomRocketSave(Rocket rocket) : base(rocket) { }

        public void AddCustomData(string id, object data)
        {
            customData.Add(id, data);
        }

        public void RemoveCustomData(string id)
        {
            customData.Remove(id);
        }

        public D GetCustomData<D>(string id, out bool successful)
        {
            successful = false;
            if (customData.TryGetValue(id, out object data))
            {
                if (data is D typedData)
                {
                    successful = true;
                    return typedData;
                }
            }
            return default;
        }
    }

    public class CustomRocketSaveHelper
    {
        /// <summary>
        /// Called when a <c>WorldSave</c> is created. Can be used to tranfer custom data from each <c>Rocket</c> to its respective <c>CustomRocketSave</c>.
        /// </summary>
        public event Action<CustomRocketSave, Rocket> OnSave;

        /// <summary>
        /// Called when a <c>CustomRocketSave</c> is spawned into the world. Can be used to tranfer custom data from each <c>CustomRocketSave</c> to its respective <c>Rocket</c>.
        /// </summary>
        public event Action<CustomRocketSave, Rocket> OnLoad;

        internal void Invoke_OnSave(CustomRocketSave rocketSave, Rocket rocket)
        {
            try
            {
                OnSave?.Invoke(rocketSave, rocket);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal void Invoke_OnLoad(CustomRocketSave rocketSave, Rocket rocket)
        {
            try
            {
                OnLoad?.Invoke(rocketSave, rocket);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    namespace Patches
    {
        [HarmonyPatch(typeof(WorldSave), nameof(WorldSave.TryLoad))]
        static class WorldSave_TryLoad
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                List<CodeInstruction> codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldstr && (string) codes[i].operand == "Rockets.txt")
                    {
                        // * Changes `TryLoadJson<RocketSave[]>` to `TryLoadJson<CustomRocketSave[]>`.
                        codes[i + 3].operand = typeof(JsonWrapper)
                            .GetMethod(nameof(JsonWrapper.TryLoadJson), BindingFlags.Public | BindingFlags.Static)
                            .MakeGenericMethod(typeof(CustomRocketSave[]));
                        break;
                    }
                }
                return codes;
            }
        }

        [HarmonyPatch(typeof(RocketManager), nameof(RocketManager.LoadRocket))]
        static class RocketManager_LoadRocket
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].Calls(typeof(StatsRecorder).GetMethod(nameof(StatsRecorder.Load))))
                    {
                        // * Inserts `Main.RocketSaveHelper.Invoke_OnLoad`.
                        codes.InsertRange
                        (
                            i + 1,
                            new CodeInstruction[]
                            {
                                // Load `Main.RocketSaveHelper`.
                                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Main), nameof(Main.RocketSaveHelper))),
                                // Load `RocketSave`.
                                new CodeInstruction(OpCodes.Ldarg_0),
                                // Load `Rocket`.
                                new CodeInstruction(OpCodes.Ldloc_2),
                                // Call `CustomRocketSaveHelper.Invoke_OnLoad`.
                                CodeInstruction.Call(typeof(CustomRocketSaveHelper), nameof(CustomRocketSaveHelper.Invoke_OnLoad)),
                            }
                        );
                        break;
                    }
                }
                return codes;
            }
        }

        [HarmonyPatch]
        static class GameManager_CreateWorldSave
        {
            static MethodBase TargetMethod()
            {
                // ? Patching a compiler-generated method: https://github.com/pardeike/Harmony/issues/536
                Type type = AccessTools.FirstInner(typeof(GameManager), (Type t) => t.Name == "<>c");
                return AccessTools.FirstMethod(type, (MethodInfo m) => m.Name.Contains("b__24_0"));
            }

            static bool Prefix(Rocket rocket, ref RocketSave __result)
            {
                CustomRocketSave save = new CustomRocketSave(rocket);
                Main.RocketSaveHelper.Invoke_OnSave(save, rocket);
                __result = save;
                return true;
            }
        }
    }
}