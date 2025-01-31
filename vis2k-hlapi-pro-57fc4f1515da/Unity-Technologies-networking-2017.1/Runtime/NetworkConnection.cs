#if ENABLE_UNET
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace UnityEngine.Networking
{
    //
    // wire protocol is a list of :   size   |  msgType     | payload
    //                               (short)  (variable)     (buffer)
    //
    public class NetworkConnection : IDisposable
    {
        ChannelBuffer[] m_Channels;
        public List<PlayerController> playerControllers = new List<PlayerController>(); // vis2k: public instead of get/set wrapper
        internal HashSet<NetworkIdentity> visList = new HashSet<NetworkIdentity>(); // vis2k: internal instead of get/set wrapper
        NetworkWriter m_Writer = new NetworkWriter(); // vis2k: create immediately, no more constructor needed

        Dictionary<short, NetworkMessageDelegate> m_MessageHandlersDict;
        NetworkMessageHandlers m_MessageHandlers;

        public  HashSet<NetworkInstanceId> clientOwnedObjects; // vis2k: public instead of get/set wrapper
        NetworkMessage m_MessageInfo = new NetworkMessage();

        const int k_MaxMessageLogSize = 150;
        public NetworkError lastError { get; internal set; } // vis2k: no need for extra variable and wrapper

        public int hostId = -1;
        public int connectionId = -1;
        public bool isReady;
        public string address;
        public float lastMessageTime;
        public bool logNetworkMessages = false;
        public bool isConnected { get { return hostId != -1; }}


        public class PacketStat
        {
            public PacketStat()
            {
                msgType = 0;
                count = 0;
                bytes = 0;
            }

            public PacketStat(PacketStat s)
            {
                msgType = s.msgType;
                count = s.count;
                bytes = s.bytes;
            }

            public short msgType;
            public int count;
            public int bytes;

            public override string ToString()
            {
                return MsgType.MsgTypeToString(msgType) + ": count=" + count + " bytes=" + bytes;
            }
        }


        internal Dictionary<short, PacketStat> packetStats = new Dictionary<short, PacketStat>(); // vis2k: internal instead of get/set wrappers

#if UNITY_EDITOR
        static int s_MaxPacketStats = 255;//the same as maximum message types
#endif

        public virtual void Initialize(string networkAddress, int networkHostId, int networkConnectionId, HostTopology hostTopology)
        {
            m_Writer = new NetworkWriter();
            address = networkAddress;
            hostId = networkHostId;
            connectionId = networkConnectionId;

            int numChannels = hostTopology.DefaultConfig.ChannelCount;
            int packetSize = hostTopology.DefaultConfig.PacketSize;

            if ((hostTopology.DefaultConfig.UsePlatformSpecificProtocols) && (Application.platform != RuntimePlatform.PS4) && (Application.platform != RuntimePlatform.PSP2))
                throw new ArgumentOutOfRangeException("Platform specific protocols are not supported on this platform");

            m_Channels = new ChannelBuffer[numChannels];
            for (int i = 0; i < numChannels; i++)
            {
                var qos = hostTopology.DefaultConfig.Channels[i];
                int actualPacketSize = packetSize;
                if (qos.QOS == QosType.ReliableFragmented || qos.QOS == QosType.UnreliableFragmented)
                {
                    actualPacketSize = hostTopology.DefaultConfig.FragmentSize * 128;
                }
                m_Channels[i] = new ChannelBuffer(this, actualPacketSize, (byte)i, IsReliableQoS(qos.QOS), IsSequencedQoS(qos.QOS));
            }
        }

        ~NetworkConnection()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            // Take yourself off the Finalization queue
            // to prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (clientOwnedObjects != null)
            {
                foreach (var netId in clientOwnedObjects)
                {
                    var obj = NetworkServer.FindLocalObject(netId);
                    if (obj != null)
                    {
                        obj.GetComponent<NetworkIdentity>().ClearClientOwner();
                    }
                }
            }
            clientOwnedObjects = null;
        }

        public static bool IsSequencedQoS(QosType qos)
        {
            return (qos == QosType.ReliableSequenced || qos == QosType.UnreliableSequenced);
        }

        public static bool IsReliableQoS(QosType qos)
        {
            return (qos == QosType.Reliable || qos == QosType.ReliableFragmented || qos == QosType.ReliableSequenced || qos == QosType.ReliableStateUpdate);
        }

        public static bool IsUnreliableQoS(QosType qos)
        {
            return (qos == QosType.Unreliable || qos == QosType.UnreliableFragmented || qos == QosType.UnreliableSequenced || qos == QosType.StateUpdate);
        }

        public bool SetChannelOption(int channelId, ChannelOption option, int value)
        {
            if (m_Channels == null)
                return false;

            if (channelId < 0 || channelId >= m_Channels.Length)
                return false;

            return m_Channels[channelId].SetOption(option, value);
        }

        public void Disconnect()
        {
            //vis2k: don't clear address so we can still access it in NetworkManager.OnServerDisconnect
            // => it's reset in Initialize anyway and there is no address empty check anywhere either
            //address = "";
            isReady = false;
            ClientScene.HandleClientDisconnect(this);
            if (hostId != -1) // vis2k: != instead of == and early return
            {
                byte error;
                NetworkTransport.Disconnect(hostId, connectionId, out error);
                RemoveObservers();
            }
        }

        internal void SetHandlers(NetworkMessageHandlers handlers)
        {
            m_MessageHandlers = handlers;
            m_MessageHandlersDict = handlers.GetHandlers();
        }

        public bool CheckHandler(short msgType)
        {
            return m_MessageHandlersDict.ContainsKey(msgType);
        }

        public bool InvokeHandlerNoData(short msgType)
        {
            return InvokeHandler(msgType, null, 0);
        }

        public bool InvokeHandler(short msgType, NetworkReader reader, int channelId)
        {
            if (m_MessageHandlersDict.ContainsKey(msgType))
            {
                m_MessageInfo.msgType = msgType;
                m_MessageInfo.conn = this;
                m_MessageInfo.reader = reader;
                m_MessageInfo.channelId = channelId;

                NetworkMessageDelegate msgDelegate = m_MessageHandlersDict[msgType];
                if (msgDelegate == null)
                {
                    if (LogFilter.logError) { Debug.LogError("NetworkConnection InvokeHandler no handler for " + msgType); }
                    return false;
                }
                msgDelegate(m_MessageInfo);
                return true;
            }
            return false;
        }

        public bool InvokeHandler(NetworkMessage netMsg)
        {
            if (m_MessageHandlersDict.ContainsKey(netMsg.msgType))
            {
                NetworkMessageDelegate msgDelegate = m_MessageHandlersDict[netMsg.msgType];
                msgDelegate(netMsg);
                return true;
            }
            return false;
        }

        internal void HandleFragment(NetworkReader reader, int channelId)
        {
            if (0 <= channelId && channelId < m_Channels.Length) // vis2k: instead of early return
            {
                ChannelBuffer channel = m_Channels[channelId];
                if (channel.HandleFragment(reader))
                {
                    // vis2k: no more ArraySegment because of the new NetworkReader/Writer
                    NetworkReader msgReader = new NetworkReader(channel.fragmentBuffer.ToArray());
                    msgReader.ReadInt16(); // size
                    short msgType = msgReader.ReadInt16();
                    InvokeHandler(msgType, msgReader, channelId);
                }
            }
        }

        public void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            m_MessageHandlers.RegisterHandler(msgType, handler);
        }

        public void UnregisterHandler(short msgType)
        {
            m_MessageHandlers.UnregisterHandler(msgType);
        }

        internal void SetPlayerController(PlayerController player)
        {
            while (player.playerControllerId >= playerControllers.Count)
            {
                playerControllers.Add(new PlayerController());
            }

            playerControllers[player.playerControllerId] = player;
        }

        internal void RemovePlayerController(short playerControllerId)
        {
            int count = playerControllers.Count;
            while (count >= 0)
            {
                if (playerControllerId == count && playerControllerId == playerControllers[count].playerControllerId)
                {
                    playerControllers[count] = new PlayerController();
                    return;
                }
                count -= 1;
            }
            if (LogFilter.logError) { Debug.LogError("RemovePlayer player at playerControllerId " + playerControllerId + " not found"); }
        }

        // Get player controller from connection's list
        internal bool GetPlayerController(short playerControllerId, out PlayerController playerController)
        {
            // vis2k: linq instead of for loop
            playerController = playerControllers.Find(pc => pc.IsValid && pc.playerControllerId == playerControllerId);
            return playerController != null;
        }

        public void FlushChannels()
        {
            if (m_Channels != null) // vis2k: != instead of == and early return
            {
                for (int channelId = 0; channelId < m_Channels.Length; channelId++)
                {
                    m_Channels[channelId].CheckInternalBuffer();
                }
            }
        }

        public void SetMaxDelay(float seconds)
        {
            if (m_Channels != null) // vis2k: != instead of == and early return
            {
                for (int channelId = 0; channelId < m_Channels.Length; channelId++)
                {
                    m_Channels[channelId].maxDelay = seconds;
                }
            }
        }

        public virtual bool Send(short msgType, MessageBase msg)
        {
            return SendByChannel(msgType, msg, Channels.DefaultReliable);
        }

        public virtual bool SendUnreliable(short msgType, MessageBase msg)
        {
            return SendByChannel(msgType, msg, Channels.DefaultUnreliable);
        }

        public virtual bool SendByChannel(short msgType, MessageBase msg, int channelId)
        {
            m_Writer.StartMessage(msgType);
            msg.Serialize(m_Writer);
            m_Writer.FinishMessage();
            return SendWriter(m_Writer, channelId);
        }

        public virtual bool SendBytes(byte[] bytes, int numBytes, int channelId)
        {
            if (logNetworkMessages)
            {
                LogSend(bytes);
            }
            return CheckChannel(channelId) && m_Channels[channelId].SendBytes(bytes, numBytes);
        }

        public virtual bool SendWriter(NetworkWriter writer, int channelId)
        {
            if (logNetworkMessages)
            {
                LogSend(writer.ToArray());
            }
            return CheckChannel(channelId) && m_Channels[channelId].SendWriter(writer);
        }

        void LogSend(byte[] bytes)
        {
            NetworkReader reader = new NetworkReader(bytes);
            var msgSize = reader.ReadUInt16();
            var msgId = reader.ReadUInt16();

            const int k_PayloadStartPosition = 4;

            StringBuilder msg = new StringBuilder();
            for (int i = k_PayloadStartPosition; i < k_PayloadStartPosition + msgSize; i++)
            {
                msg.AppendFormat("{0:X2}", bytes[i]);
                if (i > k_MaxMessageLogSize) break;
            }
            Debug.Log("ConnectionSend con:" + connectionId + " bytes:" + msgSize + " msgId:" + msgId + " " + msg);
        }

        bool CheckChannel(int channelId)
        {
            if (m_Channels == null)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Channels not initialized sending on id '" + channelId); }
                return false;
            }
            if (channelId < 0 || channelId >= m_Channels.Length)
            {
                if (LogFilter.logError) { Debug.LogError("Invalid channel when sending buffered data, '" + channelId + "'. Current channel count is " + m_Channels.Length); }
                return false;
            }
            return true;
        }

        public void ResetStats()
        {
#if UNITY_EDITOR
            for (short i = 0; i < s_MaxPacketStats; i++)
            {
                if (packetStats.ContainsKey(i))
                {
                    var value = packetStats[i];
                    value.count = 0;
                    value.bytes = 0;
                    NetworkTransport.SetPacketStat(0, i, 0, 0);
                    NetworkTransport.SetPacketStat(1, i, 0, 0);
                }
            }
#endif
        }

        protected void HandleBytes(byte[] buffer, int receivedSize, int channelId)
        {
            // build the stream form the buffer passed in
            NetworkReader reader = new NetworkReader(buffer);
            HandleReader(reader, receivedSize, channelId);
        }

        protected void HandleReader(NetworkReader reader, int receivedSize, int channelId)
        {
            // read until size is reached.
            // NOTE: stream.Capacity is 1300, NOT the size of the available data
            while (reader.Position < receivedSize)
            {
                // the reader passed to user code has a copy of bytes from the real stream. user code never touches the real stream.
                // this ensures it can never get out of sync if user code reads less or more than the real amount.
                ushort sz = reader.ReadUInt16();
                short msgType = reader.ReadInt16();

                // create a reader just for this message
                byte[] msgBuffer = reader.ReadBytes(sz);
                NetworkReader msgReader = new NetworkReader(msgBuffer);

                if (logNetworkMessages)
                {
                    StringBuilder msg = new StringBuilder();
                    for (int i = 0; i < sz; i++)
                    {
                        msg.AppendFormat("{0:X2}", msgBuffer[i]);
                        if (i > k_MaxMessageLogSize) break;
                    }
                    Debug.Log("ConnectionRecv con:" + connectionId + " bytes:" + sz + " msgId:" + msgType + " " + msg);
                }

                NetworkMessageDelegate msgDelegate = null;
                if (m_MessageHandlersDict.ContainsKey(msgType))
                {
                    msgDelegate = m_MessageHandlersDict[msgType];
                }
                if (msgDelegate != null)
                {
                    // vis2k: create message here instead of caching it. so we can add it to queue more easily.
                    NetworkMessage msg = new NetworkMessage();
                    msg.msgType = msgType;
                    msg.reader = msgReader;
                    msg.conn = this;
                    msg.channelId = channelId;

                    // vis2k: add to queue while paused, otherwise process directly
                    if (pauseQueue != null)
                    {
                        pauseQueue.Enqueue(msg);
                        if (LogFilter.logWarn) { Debug.LogWarning("HandleReader: added message to pause queue: " + msgType + " str=" + MsgType.MsgTypeToString(msgType) + " queue size=" + pauseQueue.Count); }
                    }
                    else
                    {
                        msgDelegate(msg);
                    }
                    lastMessageTime = Time.time;

#if UNITY_EDITOR
                    UnityEditor.NetworkDetailStats.IncrementStat(
                        UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                        MsgType.HLAPIMsg, "msg", 1);

                    if (msgType > MsgType.Highest)
                    {
                        UnityEditor.NetworkDetailStats.IncrementStat(
                            UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                            MsgType.UserMessage, msgType.ToString() + ":" + msgType.GetType().Name, 1);
                    }
#endif

#if UNITY_EDITOR
                    if (packetStats.ContainsKey(msgType))
                    {
                        PacketStat stat = packetStats[msgType];
                        stat.count += 1;
                        stat.bytes += sz;
                    }
                    else
                    {
                        PacketStat stat = new PacketStat();
                        stat.msgType = msgType;
                        stat.count += 1;
                        stat.bytes += sz;
                        packetStats[msgType] = stat;
                    }
#endif
                }
                else
                {
                    //NOTE: this throws away the rest of the buffer. Need moar error codes
                    if (LogFilter.logError) { Debug.LogError("Unknown message ID " + msgType + " connId:" + connectionId); }
                    break;
                }
            }
        }

        public virtual void GetStatsOut(out int numMsgs, out int numBufferedMsgs, out int numBytes, out int lastBufferedPerSecond)
        {
            numMsgs = 0;
            numBufferedMsgs = 0;
            numBytes = 0;
            lastBufferedPerSecond = 0;

            foreach (ChannelBuffer channel in m_Channels) // vis2k: foreach
            {
                numMsgs += channel.numMsgsOut;
                numBufferedMsgs += channel.numBufferedMsgsOut;
                numBytes += channel.numBytesOut;
                lastBufferedPerSecond += channel.lastBufferedPerSecond;
            }
        }

        public virtual void GetStatsIn(out int numMsgs, out int numBytes)
        {
            numMsgs = 0;
            numBytes = 0;

            foreach (ChannelBuffer channel in m_Channels) // vis2k: foreach
            {
                numMsgs += channel.numMsgsIn;
                numBytes += channel.numBytesIn;
            }
        }

        public override string ToString()
        {
            return string.Format("hostId: {0} connectionId: {1} isReady: {2} channel count: {3}", hostId, connectionId, isReady, (m_Channels != null ? m_Channels.Length : 0));
        }

        internal void AddToVisList(NetworkIdentity uv)
        {
            visList.Add(uv);

            // spawn uv for this conn
            NetworkServer.ShowForConnection(uv, this);
        }

        internal void RemoveFromVisList(NetworkIdentity uv, bool isDestroyed)
        {
            visList.Remove(uv);

            if (!isDestroyed)
            {
                // hide uv for this conn
                NetworkServer.HideForConnection(uv, this);
            }
        }

        internal void RemoveObservers()
        {
            foreach (var uv in visList)
            {
                uv.RemoveObserverInternal(this);
            }
            visList.Clear();
        }

        public virtual void TransportReceive(byte[] bytes, int numBytes, int channelId)
        {
            HandleBytes(bytes, numBytes, channelId);
        }

        [Obsolete("TransportRecieve has been deprecated. Use TransportReceive instead (UnityUpgradable) -> TransportReceive(*)", false)]
        public virtual void TransportRecieve(byte[] bytes, int numBytes, int channelId)
        {
            TransportReceive(bytes, numBytes, channelId);
        }

        public virtual bool TransportSend(byte[] bytes, int numBytes, int channelId, out byte error)
        {
            return NetworkTransport.Send(hostId, connectionId, channelId, bytes, numBytes, out error);
        }

        internal void AddOwnedObject(NetworkIdentity obj)
        {
            if (clientOwnedObjects == null)
            {
                clientOwnedObjects = new HashSet<NetworkInstanceId>();
            }
            clientOwnedObjects.Add(obj.netId);
        }

        internal void RemoveOwnedObject(NetworkIdentity obj)
        {
            if (clientOwnedObjects != null) // vis2k: != instead of == and early return
            {
                clientOwnedObjects.Remove(obj.netId);
            }
        }

        internal static void OnFragment(NetworkMessage netMsg)
        {
            netMsg.conn.HandleFragment(netMsg.reader, netMsg.channelId);
        }

        // vis2k: pause mode
        // problem: if we handle packets (calling the msgDelegates) while a scene load is in progress, then all the
        //          handled data and state will be lost as soon as the scene load is finished, causing state bugs.
        // solution: call Pause, message handling keeps messages in a queue, Resume handles them all.
        //
        // this is the only safe way to do it. otherwise all delegate functions have to check if a scene is loading,
        // which is way too complicated and risky.
        Queue<NetworkMessage> pauseQueue;

        internal void PauseHandling()
        {
            pauseQueue = new Queue<NetworkMessage>();
        }

        internal void ResumeHandling()
        {
            // pauseQueue is null if Resume called without pausing, make sure to only do something if paused before.
            if (pauseQueue != null)
            {
                foreach (NetworkMessage msg in pauseQueue)
                {
                    if (LogFilter.logWarn) { Debug.LogWarning("processing queued message: " + msg.msgType + " str=" + MsgType.MsgTypeToString(msg.msgType)); }
                    var msgDelegate = m_MessageHandlersDict[msg.msgType];
                    msgDelegate(msg);
                }
                pauseQueue = null;
            }
        }
    }
}
#endif //ENABLE_UNET
