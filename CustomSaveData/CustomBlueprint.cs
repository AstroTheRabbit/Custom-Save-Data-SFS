using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SFS.World;
using SFS.Parts;
using SFS.Builds;

namespace CustomSaveData
{
    [Serializable]
    public class CustomBlueprint : Blueprint
    {
        /// <summary>
        /// The custom data of a <c>CustomBlueprint</c>. Do not access directly; use <c>CustomBlueprint.AddCustomData</c> and <c>CustomBlueprint.GetCustomData</c> instead.
        /// </summary>
        [JsonProperty(Order = 1)]
        internal Dictionary<string, JToken> customData = new Dictionary<string, JToken>();

        [JsonConstructor]
        public CustomBlueprint() : base() {}

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
            customData.Add(id, JToken.FromObject(data, Main.jsonSerializer));
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
}