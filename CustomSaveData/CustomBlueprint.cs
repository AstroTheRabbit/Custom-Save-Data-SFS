using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SFS;
using SFS.IO;
using SFS.World;
using SFS.Parts;
using SFS.Builds;
using SFS.Parsers.Json;
using SFS.Translations;
using SFS.Parts.Modules;

namespace CustomSaveData
{
    [Serializable]
    public class CustomBlueprint : Blueprint
    {
        static CustomBlueprint() {}
        /// <summary>
        /// The custom data of a <c>CustomBlueprint</c>. Do not access directly; use <c>CustomBlueprint.AddCustomData</c> and <c>CustomBlueprint.GetCustomData</c> instead.
        /// </summary>
        [JsonProperty]
        public Dictionary<string, JToken> customData = new Dictionary<string, JToken>();

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
            customData.Add(id, JToken.FromObject(data));
        }

        public void RemoveCustomData(string id)
        {
            customData.Remove(id);
        }

        public D GetCustomData<D>(string id, out bool successful)
        {
            successful = false;
            if (customData.TryGetValue(id, out JToken data))
            {
                if (data.ToObject<D>() is D typedData)
                {
                    successful = true;
                    return typedData;
                }
            }
            return default;
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
                Main.BlueprintHelper.Invoke_OnLoad(blueprint as CustomBlueprint);
            }
        }

        [HarmonyPatch(typeof(RocketManager), nameof(RocketManager.SpawnBlueprint))]
        static class RocketManager_SpawnBlueprint
        {
            static bool Prefix(Blueprint blueprint)
            {
                WorldView.main.SetViewLocation(Base.planetLoader.spaceCenter.LaunchPadLocation);
                if (blueprint.rotation != 0f)
                {
                    PartSave[] partSaves = blueprint.parts;
                    foreach (PartSave partSave in partSaves)
                    {
                        partSave.orientation += new Orientation(1f, 1f, blueprint.rotation);
                        partSave.position *= new Orientation(1f, 1f, blueprint.rotation);
                    }
                }
                Part[] parts = PartsLoader.CreateParts(blueprint.parts, null, null, OnPartNotOwned.Delete, out OwnershipState[] ownershipState);
                Part[] successfulParts = parts.Where((Part a) => a == null).ToArray();
                if (blueprint.rotation != 0f)
                {
                    PartSave[] partSaves = blueprint.parts;
                    foreach (PartSave partSave in partSaves)
                    {
                        partSave.orientation += new Orientation(1f, 1f, 0f - blueprint.rotation);
                        partSave.position *= new Orientation(1f, 1f, 0f - blueprint.rotation);
                    }
                }
                Part_Utility.PositionParts(WorldView.ToLocalPosition(Base.planetLoader.spaceCenter.LaunchPadLocation.position), new Vector2(0.5f, 0f), round: true, useLaunchBounds: true, successfulParts);
                new JointGroup(RocketManager.GenerateJoints(successfulParts), successfulParts.ToList()).RecreateGroups(out var newGroups);
                Rocket[] rockets = RocketManager_SpawnRockets(newGroups);
                Staging.CreateStages(blueprint.stages, parts);
                Rocket rocket = rockets.FirstOrDefault((Rocket a) => a.hasControl.Value);
                PlayerController.main.player.Value = rocket ?? ((rockets.Length != 0) ? rockets[0] : null);

                Main.BlueprintHelper.Invoke_OnLaunch((CustomBlueprint) blueprint, rockets, parts);
                return false;
            }

            static Rocket[] RocketManager_SpawnRockets(List<JointGroup> groups)
            {
                return (Rocket[]) typeof(RocketManager).GetMethod("SpawnRockets", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { groups });
            }
        }
    }
}