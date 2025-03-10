using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using HarmonyLib;
using SFS;
using SFS.IO;
using SFS.Stats;
using SFS.World;
using SFS.Builds;
using SFS.WorldBase;
using SFS.Translations;
using SFS.Parsers.Json;

namespace CustomSaveData
{
    public static class Patches
    {
        /// <summary>
        /// Invokes the blueprint helper's `OnSave` event when a blueprint is created.
        /// </summary>
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

        /// <summary>
        /// Converts vanilla blueprints into custom blueprints when they are loaded.
        /// </summary>
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

        /// <summary>
        /// Invokes the blueprint helper's `OnLoad` event when a blueprint is loaded.
        /// </summary>
        [HarmonyPatch(typeof(BuildState), nameof(BuildState.LoadBlueprint))]
        static class BuildState_LoadBlueprint
        {
            static void Postfix(Blueprint blueprint)
            {
                Main.BlueprintHelper.Invoke_OnLoad(blueprint as CustomBlueprint);
            }
        }

        /// <summary>
        /// Invokes the blueprint helper's `OnLaunch` event when a blueprint is launched.
        /// </summary>
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
                                // Call `CustomBlueprintHelper.Invoke_OnLaunch`.
                                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(CustomBlueprintHelper), nameof(CustomBlueprintHelper.Invoke_OnLaunch))),
                            }
                        );
                        break;
                    }
                }
                return codes;
            }
        }

        /// <summary>
        /// Invokes the rocket save helper's `OnLoad` event when a rocket save is loaded.
        /// </summary>
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

        /// <summary>
        /// Invokes the rocket save helper's `OnSave` event when a rocket save is saved.
        /// </summary>
        [HarmonyPatch]
        static class GameManager_CreateWorldSave_RocketSave
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

        /// <summary>
        /// Converts vanilla rocket saves into custom rocket saves when they are loaded.
        /// Also loads saved custom data into the custom world save.
        /// </summary>
        [HarmonyPatch(typeof(WorldSave), nameof(WorldSave.TryLoad))]
        static class WorldSave_TryLoad
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
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

            static void Postfix(FolderPath path, bool loadRocketsAndBranches, I_MsgLogger logger, ref WorldSave worldSave)
            {
                CustomWorldSave save = new CustomWorldSave(worldSave);
                foreach (BasePath child in path.EnumerateContents())
                {
                    if (child is FilePath file && !CustomWorldSave.ExcludeFiles.Contains(file.FileName) && JsonWrapper.TryLoadJson(file, out JToken json))
                    {
                        save.customData.Add(file.FileName, json);
                    }
                }
                worldSave = save;
            }
        }

        /// <summary>
        /// Invokes the world save helper's `OnEmpty` event when an empty save is created.
        /// </summary>
        [HarmonyPatch(typeof(WorldSave), nameof(WorldSave.CreateEmptyQuicksave))]
        public class WorldSave_CreateEmptyQuicksave
        {
            public static void Postfix(ref WorldSave __result)
            {
                CustomWorldSave save = new CustomWorldSave(__result);
                __result = save;
                Main.WorldSaveHelper.Invoke_OnEmpty(save);
            }
        }

        /// <summary>
        /// Invokes the world save helper's `OnSave` event when a world save is created, and saves the world save's custom data to their respective files.
        /// </summary>
        [HarmonyPatch(typeof(WorldSave), nameof(WorldSave.Save))]
        public class WorldSave_Save
        {
            public static void Postfix(FolderPath path, WorldSave worldSave)
            {
                CustomWorldSave save = worldSave as CustomWorldSave;
                foreach (KeyValuePair<string, JToken> kvp in save.customData)
                {
                    FilePath fullPath = path.ExtendToFile(kvp.Key);
                    JsonWrapper.SaveAsJson(fullPath, kvp.Value, false);
                }
            }
        }

        /// <summary>
        /// Invokes the world save helper's `OnLoad` event when a world save is loaded.
        /// </summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadSave))]
        public class GameManager_LoadSave
        {
            public static void Postfix(WorldSave save)
            {
                Main.WorldSaveHelper.Invoke_OnLoad(save as CustomWorldSave);
            }
        }

        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.GetCopy), typeof(WorldSave))]
        public class SavingCache_GetCopy
        {
            public static void Postfix(WorldSave a, ref WorldSave __result)
            {
                CustomWorldSave cws = new CustomWorldSave(__result);
                if (a is CustomWorldSave b)
                    cws.customData = b.customData;
                __result = cws;
            }
        }

        [HarmonyPatch(typeof(GameManager), "CreateWorldSave")]
        public class GameManager_CreateWorldSave_WorldSave
        {
            public static void Postfix(ref WorldSave __result)
            {
                CustomWorldSave save = new CustomWorldSave(__result);
                Main.WorldSaveHelper.Invoke_OnSave(save);
                __result = save;
            }
        }
    }
}