#if ENABLE_UNET
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Networking.NetworkSystem;

namespace UnityEngine.Networking
{
    public class NetworkCRC
    {
        internal static NetworkCRC s_Singleton;

        public Dictionary<string, int> scripts = new Dictionary<string, int>(); // vis2k: public instead of get/set wrappers
        bool m_ScriptCRCCheck;

        static internal NetworkCRC singleton
        {
            get
            {
                if (s_Singleton == null)
                {
                    s_Singleton = new NetworkCRC();
                }
                return s_Singleton;
            }
        }

        static public bool scriptCRCCheck
        {
            get
            {
                return singleton.m_ScriptCRCCheck;
            }
            set
            {
                singleton.m_ScriptCRCCheck = value;
            }
        }

        // The NetworkCRC cache contain entries from
        static public void ReinitializeScriptCRCs(Assembly callingAssembly)
        {
            singleton.scripts.Clear();

            var types = callingAssembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (t.GetBaseType() == typeof(NetworkBehaviour))
                {
                    var cctor  = t.GetMethod(".cctor", BindingFlags.Static);
                    if (cctor != null)
                    {
                        cctor.Invoke(null, new object[] {});
                    }
                }
            }
        }

        static public void RegisterBehaviour(string name, int channel)
        {
            singleton.scripts[name] = channel;
        }

        internal static bool Validate(CRCMessageEntry[] scripts, int numChannels)
        {
            return singleton.ValidateInternal(scripts, numChannels);
        }

        bool ValidateInternal(CRCMessageEntry[] remoteScripts, int numChannels)
        {
            // check count against my channels
            if (scripts.Count != remoteScripts.Length)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Network configuration mismatch detected. The number of networked scripts on the client does not match the number of networked scripts on the server. This could be caused by lazy loading of scripts on the client. This warning can be disabled by the checkbox in NetworkManager Script CRC Check."); }
                Dump(remoteScripts);
                return false;
            }

            // check each script
            for (int i = 0; i < remoteScripts.Length; i++)
            {
                var script = remoteScripts[i];
                if (LogFilter.logDebug) { Debug.Log("Script: " + script.name + " Channel: " + script.channel); }

                if (scripts.ContainsKey(script.name))
                {
                    int localChannel = scripts[script.name];
                    if (localChannel != script.channel)
                    {
                        if (LogFilter.logError) { Debug.LogError("HLAPI CRC Channel Mismatch. Script: " + script.name + " LocalChannel: " + localChannel + " RemoteChannel: " + script.channel); }
                        Dump(remoteScripts);
                        return false;
                    }
                }
                if (script.channel >= numChannels)
                {
                    if (LogFilter.logError) { Debug.LogError("HLAPI CRC channel out of range! Script: " + script.name + " Channel: " + script.channel); }
                    Dump(remoteScripts);
                    return false;
                }
            }
            return true;
        }

        void Dump(CRCMessageEntry[] remoteScripts)
        {
            foreach (var script in scripts.Keys)
            {
                Debug.Log("CRC Local Dump " + script + " : " + scripts[script]);
            }

            foreach (var remote in remoteScripts)
            {
                Debug.Log("CRC Remote Dump " + remote.name + " : " + remote.channel);
            }
        }
    }
}
#endif //ENABLE_UNET
