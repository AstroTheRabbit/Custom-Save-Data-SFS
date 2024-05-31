using System;
using System.Linq;
using System.Reflection;
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
using SFS.Parts.Modules;

namespace CustomSaveData
{
    [Serializable]
    public class CustomBlueprint : Blueprint
    {
        [JsonProperty]
        private Dictionary<string, object> customData = new Dictionary<string, object>();
        
        /// <summary>
        /// Called before a <c>CustomBlueprint</c> is saved. Can be used to add custom data to the <c>CustomBlueprint</c> before it is saved.
        /// </summary>
        public static OptionalDelegate<CustomBlueprint> OnSave { get; set; } = new OptionalDelegate<CustomBlueprint>();

        /// <summary>
        /// Called after <c>CustomBlueprint</c> is loaded. Can be used to load custom data from <c>CustomBlueprint</c>.
        /// </summary>
        public static OptionalDelegate<CustomBlueprint> OnLoad { get; set; } = new OptionalDelegate<CustomBlueprint>();

        /// <summary>
        /// Called when a <c>CustomBlueprint</c> is launched. Can be used to transfer custom data from a launched <c>CustomBlueprint</c> to its resulting <c>Rocket[]</c>.
        /// </summary>
        public static OptionalDelegate<(CustomBlueprint, Rocket[])> OnLaunch { get; set; } = new OptionalDelegate<(CustomBlueprint, Rocket[])>();

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

    namespace Patches
    {
        [HarmonyPatch(typeof(BuildState), nameof(BuildState.GetBlueprint))]
        static class BuildState_GetBlueprint
        {
            static void Postfix(ref Blueprint __result)
            {
                CustomBlueprint res = new CustomBlueprint(__result);
                CustomBlueprint.OnSave.Invoke(res);
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
                    CustomBlueprint.OnLoad.Invoke(customBlueprint);
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
                Part[] successfulParts = parts.Where((Part a) => a != null).ToArray();
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

                CustomBlueprint.OnLaunch.Invoke(((CustomBlueprint) blueprint, rockets));
                return false;
            }

            static Rocket[] RocketManager_SpawnRockets(List<JointGroup> groups)
            {
                return (Rocket[]) typeof(RocketManager).GetMethod("SpawnRockets", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { groups });
            }
        }
    }
}