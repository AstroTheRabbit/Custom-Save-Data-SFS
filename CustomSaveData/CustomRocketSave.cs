using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SFS.World;

namespace CustomSaveData
{
    // ? For some reason the `JsonConverter` attribute below, which is applied to the `RocketSave` base class,
    // ? messes with the JSON serializer in such a way that I have to make the `customData` field public.
    // ? (also the `[JsonProperty(Order = 1)]` attribute seemingly no longer has any effect?).
    // [JsonConverter(typeof(WorldSave.LocationData.LocationConverter))]
    // * Regarding ^^^, the problem arises from the fact that the converter seemingly applies itself "recursively" to every
    // * field under the `JsonConverter` attribute. (I'm assuming that it was intended to only be applied to `LocationData`)
    // * and functions by only serializing public fields in the order given by `Type.GetFields()`.
    [Serializable]
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
}