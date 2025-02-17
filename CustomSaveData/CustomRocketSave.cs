using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HarmonyLib;
using UnityEngine;
using SFS.IO;
using SFS.Stats;
using SFS.World;
using SFS.Parsers.Json;

namespace CustomSaveData
{
    [Serializable]
    // ? For some reason the `JsonConverter` attribute below, which is applied to the `RocketSave` base class,
    // ? messes with the JSON serializer in such a way that I have to make the `customData` field public
    // ? (the `[JsonProperty(Order = 1)]` attribute seemingly no longer has any effect?).
    // [JsonConverter(typeof(WorldSave.LocationData.LocationConverter))]
    public class CustomRocketSave : RocketSave
    {
        /// <summary>
        /// The custom data of a <c>CustomRocketSave</c>. Do not access directly; use <c>CustomRocketSave.AddCustomData</c> and <c>CustomRocketSave.GetCustomData</c> instead.
        /// </summary>
        [JsonProperty(Order = 1)]
        public Dictionary<string, JToken> customData = new Dictionary<string, JToken>();

        [JsonConstructor]
        public CustomRocketSave() : base() {}

        public CustomRocketSave(RocketSave save)
        {
            rocketName = save.rocketName;
            location = save.location;
            rotation = save.rotation;
            angularVelocity = save.angularVelocity;
            throttleOn = save.throttleOn;
            throttlePercent = save.throttlePercent;
            RCS = save.RCS;
            parts = save.parts;
            joints = save.joints;
            stages = save.stages;
            staging_EditMode = save.staging_EditMode;
            branch = save.branch;
        }

        public void AddCustomData(string id, object data)
        {
            customData.Add(id, JToken.FromObject(data));
        }

        public void RemoveCustomData(string id)
        {
            customData.Remove(id);
        }

        public bool GetCustomData<D>(string id, out D data)
        {
            if (customData.TryGetValue(id, out JToken token))
            {
                
                if (token.ToObject<D>() is D typedData)
                {
                    data = typedData;
                    return true;
                }
            }
            data = default;
            return false;
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
                // * This patch is specifically targetting `(Rocket rocket) => new RocketSave(rocket)` of `GameManager.CreateWorldSave`.
                // ? Patching a compiler-generated method: https://github.com/pardeike/Harmony/issues/536
                Type type = AccessTools.FirstInner(typeof(GameManager), t => t.Name == "<>c");
                return AccessTools.FirstMethod(type, m => m.Name.Contains("b__24_0"));
            }

            static void Postfix(Rocket rocket, ref RocketSave __result)
            {
                CustomRocketSave save = new CustomRocketSave(__result);
                Main.RocketSaveHelper.Invoke_OnSave(save, rocket);
                __result = save;
            }
        }
    }
}