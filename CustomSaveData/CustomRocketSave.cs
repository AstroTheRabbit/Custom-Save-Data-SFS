using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using Newtonsoft.Json;
using SFS;
using SFS.IO;
using SFS.Logs;
using SFS.Stats;
using SFS.World;
using SFS.Parts;
using SFS.WorldBase;
using SFS.Utilities;
using SFS.World.Maps;
using SFS.Translations;
using SFS.Parsers.Json;
using SFS.Parts.Modules;
using static SFS.World.WorldSave;
using System.Reflection.Emit;

namespace CustomSaveData
{
    [Serializable]
    [JsonConverter(typeof(LocationData.LocationConverter))]
    public class CustomRocketSave : RocketSave
    {
        [JsonProperty]
        private Dictionary<string, object> customData = new Dictionary<string, object>() { { "test_id", 12321 } };

        /// <summary>
        /// Called when a <c>WorldSave</c> is created. Can be used to tranfer custom data from each <c>Rocket</c> to its respective <c>CustomRocketSave</c>.
        /// </summary>
        public static OptionalDelegate<(CustomRocketSave, Rocket)> OnSave { get; set; } = new OptionalDelegate<(CustomRocketSave, Rocket)>();

        /// <summary>
        /// Called when a <c>CustomRocketSave</c> is spawned into the world. Can be used to tranfer custom data from each <c>CustomRocketSave</c> to its respective <c>Rocket</c>.
        /// </summary>
        public static OptionalDelegate<(CustomRocketSave, Rocket)> OnLoad { get; set; } = new OptionalDelegate<(CustomRocketSave, Rocket)>();

        [JsonConstructor]
        public CustomRocketSave() : base() { }

        public CustomRocketSave(RocketSave rocketSave)
        {
            rocketName = rocketSave.rocketName;
            location = rocketSave.location;
            rotation = rocketSave.rotation;
            angularVelocity = rocketSave.angularVelocity;
            throttleOn = rocketSave.throttleOn;
            throttlePercent = rocketSave.throttlePercent;
            RCS = rocketSave.RCS;
            parts = rocketSave.parts;
            joints = rocketSave.joints;
            stages = rocketSave.stages;
            staging_EditMode = rocketSave.staging_EditMode;
            branch = rocketSave.branch;
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
        [HarmonyPatch(typeof(WorldSave), nameof(WorldSave.TryLoad))]
        static class WorldSave_TryLoad
        {
            static bool Prefix(FolderPath path, ref bool loadRocketsAndBranches, I_MsgLogger logger, ref WorldSave worldSave, ref bool __result)
            {
                CustomRocketSave[] rocketSaves = null;
                if (loadRocketsAndBranches && !JsonWrapper.TryLoadJson(path.ExtendToFile("Rockets.txt"), out rocketSaves))
                {
                    logger.Log(Loc.main.Load_Failed.InjectField(Loc.main.Quicksave, "filetype").Inject(path, "filepath"));
                    worldSave = null;
                    __result = false;
                }
                else
                {
                    string version = JsonWrapper.TryLoadJson(path.ExtendToFile("Version.txt"), out string out_version) ? out_version : Application.version;
                    WorldState worldState = JsonWrapper.TryLoadJson(path.ExtendToFile("WorldState.txt"), out WorldState out_worldState) ? out_worldState : WorldState.StartState();
                    Dictionary<int, Branch> branches = (!loadRocketsAndBranches) ? null : (JsonWrapper.TryLoadJson(path.ExtendToFile("Branches.txt"), out Dictionary<int, Branch> out_branches) ? out_branches : new Dictionary<int, Branch>());
                    CareerState careerState = JsonWrapper.TryLoadJson(path.ExtendToFile("CareerState.txt"), out CareerState out_careerState) ? out_careerState : new CareerState();
                    Astronauts astronauts = JsonWrapper.TryLoadJson(path.ExtendToFile("Astronauts.txt"), out Astronauts out_astronauts) ? out_astronauts : new Astronauts();
                    HashSet<LogId> achievements = JsonWrapper.TryLoadJson(path.ExtendToFile("Achievements.txt"), out List<LogId> out_achievements) ? out_achievements.ToHashSet() : new HashSet<LogId>();
                    HashSet<string> challenges = JsonWrapper.TryLoadJson(path.ExtendToFile("Challenges.txt"), out List<string> out_challenges) ? out_challenges.ToHashSet() : new HashSet<string>();
                    worldSave = new WorldSave(version, careerState, astronauts, worldState, rocketSaves, branches, achievements, challenges);
                    Debug.Log("WorldSave.TryLoad");
                    Debug.Log(worldSave.rockets != null);
                    Debug.Log(worldSave.rockets?[0]?.GetType().Name);
                    __result = true;
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(RocketManager), nameof(RocketManager.LoadRocket))]
        static class RocketManager_LoadRocket
        {
            static bool Prefix(RocketSave rocketSave, ref bool hasNonOwnedParts)
            {
                Debug.Log("RocketManager.LoadRocket");
                Debug.Log(rocketSave != null);
                Debug.Log(rocketSave?.GetType().Name);

                hasNonOwnedParts = false;
                if (rocketSave.location.address.HasPlanet())
                {
                    Part[] parts = PartsLoader.CreateParts(rocketSave.parts, null, null, OnPartNotOwned.UsePlaceholder, out OwnershipState[] ownershipState);
                    hasNonOwnedParts = ownershipState.Any((OwnershipState a) => a != OwnershipState.OwnedAndUnlocked);
                    Rocket rocket = RocketManager_CreateRocket
                    (
                        new JointGroup
                        (
                            rocketSave.joints.Select((JointSave a) => new PartJoint(parts[a.partIndex_A],
                            parts[a.partIndex_B],
                            parts[a.partIndex_B].Position - parts[a.partIndex_A].Position)).ToList(),
                            parts.ToList()
                        ),
                        rocketSave.rocketName,
                        rocketSave.throttleOn,
                        rocketSave.throttlePercent,
                        rocketSave.RCS,
                        rocketSave.rotation,
                        rocketSave.angularVelocity,
                        (Rocket a) => rocketSave.location.GetSaveLocation(WorldTime.main.worldTime),
                        false
                    );
                    rocket.staging.Load(rocketSave.stages, rocket.partHolder.GetArray(), record: false);
                    rocket.staging.editMode.Value = rocketSave.staging_EditMode;
                    rocket.stats.Load(rocketSave.branch);

                    CustomRocketSave.OnLoad.Invoke(((CustomRocketSave) rocketSave, rocket));
                }
                return false;
            }

            static Rocket RocketManager_CreateRocket(JointGroup jointGroup, string rocketName, bool throttleOn, float throttlePercent, bool RCS, float rotation, float angularVelocity, Func<Rocket, Location> location, bool physicsMode)
            {
                return (Rocket) typeof(RocketManager).GetMethod("CreateRocket", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { jointGroup, rocketName, throttleOn, throttlePercent, RCS, rotation, angularVelocity, location, physicsMode });
            }
        }

        [HarmonyPatch(typeof(GameManager), "CreateWorldSave")]
        static class GameManager_CreateWorldSave
        {
            static bool Prefix(GameManager __instance, ref WorldSave __result)
            {
                MapView.View view = Map.view.view;
	            __result = new WorldSave
                (
                    Application.version,
                    SFS.Career.CareerState.main.state,
                    SFS.Career.AstronautState.main.GetAstronautSave_Game(),
                    new WorldState
                    (
                        WorldTime.main.worldTime,
                        WorldTime.main.timewarpIndex,
                        Map.manager.mapMode.Value,
                        new Double3(view.position.Value.x, view.position.Value.y, view.distance),
                        WorldAddress.GetMapAddress(view.target),
                        WorldAddress.GetMapAddress(Map.navigation.target),
                        WorldAddress.GetPlayerAddress(PlayerController.main.player.Value),
                        PlayerController.main.cameraDistance
                    ),
                    __instance.rockets.Select((Rocket rocket) =>
                    {
                        CustomRocketSave rocketSave = new CustomRocketSave(new RocketSave(rocket));
                        CustomRocketSave.OnSave.Invoke((rocketSave, rocket));
                        return rocketSave;
                        
                    }).ToArray(),
                    LogManager.main.branches,
                    LogManager.main.completeLogs,
                    LogManager.main.completeChallenges
                );
                return false;
            }
        }

        [HarmonyPatch(typeof(WorldSave), nameof(WorldSave.Save))]
        static class WorldSave_Save
        {
            static void Postfix(FolderPath path, bool saveRocketsAndBranches, WorldSave worldSave)
            {
                if (saveRocketsAndBranches)
                {
                    CustomRocketSave[] rockets = (CustomRocketSave[]) worldSave.rockets;
                    JsonWrapper.SaveAsJson(path.ExtendToFile("Rockets.txt"), rockets, pretty: true);
                    JsonWrapper.SaveAsJson(path.ExtendToFile("RocketTest.txt"), (CustomRocketSave) worldSave.rockets[0], pretty: true);
                }
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldstr && ((string) codes[i].operand) == "Rockets.txt")
                    {
                        codes.RemoveRange(i - 1, 7);
                        break;
                    }
                }
                return codes;
            }
        }
    }
}