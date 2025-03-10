using System;
using System.Collections.Generic;
using UnityEngine;
using SFS.World;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace CustomSaveData
{
    public class CustomWorldSave : WorldSave
    {
        /// <summary>
        /// The custom data of a <c>CustomWorldSave</c>. Do not access directly; use <c>CustomWorldSave.AddCustomData</c> and <c>CustomWorldSave.GetCustomData</c> instead.
        /// </summary>
        internal Dictionary<string, JToken> customData = new Dictionary<string, JToken>();

        internal static readonly string[] ExcludeFiles = new string[]
        {
            ".DS_Store",
            "Achievements.txt",
            "Branches.txt",
            "Challenges.txt",
            "Rockets.txt",
            "Version.txt",
            "WorldState.txt",
        };

        public CustomWorldSave(WorldSave save) : base(save.version, save.career, save.astronauts, save.state, save.rockets, save.branches, save.completeLogs, save.completeChallenges) {}

        public void AddCustomData(string fileName, object data)
        {
            if (!ExcludeFiles.Contains(fileName))
                customData.Add(fileName, JToken.FromObject(data, Main.jsonSerializer));
        }

        public void RemoveCustomData(string fileName)
        {
            customData.Remove(fileName);
        }

        public bool GetCustomData<D>(string fileName, out D data)
        {
            if (customData.TryGetValue(fileName, out JToken token))
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

    public class CustomWorldSaveHelper
    {
        /// <summary>
        /// Called when an empty <c>CustomWorldSave</c> is created. This occurs when a new world's 'Persistent' save is first created, and can be used to initialise your custom data.
        /// </summary>
        public event Action<CustomWorldSave> OnEmpty;
        /// <summary>
        /// Called before a <c>CustomWorldSave</c> is saved. Can be used to add custom data to the <c>CustomWorldSave</c> before it is saved.
        /// </summary>
        public event Action<CustomWorldSave> OnSave;

        /// <summary>
        /// Called after <c>CustomWorldSave</c> is loaded. Can be used to load custom data from <c>CustomWorldSave</c>.
        /// </summary>
        public event Action<CustomWorldSave> OnLoad;

        internal void Invoke_OnEmpty(CustomWorldSave worldSave)
        {
            try
            {
                OnEmpty?.Invoke(worldSave);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal void Invoke_OnSave(CustomWorldSave worldSave)
        {
            try
            {
                OnSave?.Invoke(worldSave);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal void Invoke_OnLoad(CustomWorldSave worldSave)
        {
            try
            {
                OnLoad?.Invoke(worldSave);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}