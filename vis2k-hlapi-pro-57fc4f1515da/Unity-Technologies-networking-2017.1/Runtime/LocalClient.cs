// vis2k: removed free message buffering. UNET is complex enough as it is.
#if ENABLE_UNET
using System;
using System.Collections.Generic;

namespace UnityEngine.Networking
{
    sealed class LocalClient : NetworkClient
    {
        struct InternalMsg
        {
            internal byte[] buffer;
            internal int channelId;
        }

        List<InternalMsg> m_InternalMsgs = new List<InternalMsg>();
        List<InternalMsg> m_InternalMsgs2 = new List<InternalMsg>();

        NetworkServer m_LocalServer;
        bool m_Connected;
        NetworkMessage s_InternalMessage = new NetworkMessage();

        public override void Disconnect()
        {
            ClientScene.HandleClientDisconnect(m_Connection);
            if (m_Connected)
            {
                PostInternalMessage(MsgType.Disconnect);
                m_Connected = false;
            }
            m_AsyncConnect = ConnectState.Disconnected;
            m_LocalServer.RemoveLocalClient(m_Connection);
        }

        internal void InternalConnectLocalServer(bool generateConnectMsg)
        {
            m_LocalServer = NetworkServer.instance;
            m_Connection = new ULocalConnectionToServer(m_LocalServer);
            SetHandlers(m_Connection);
            m_Connection.connectionId = m_LocalServer.AddLocalClient(this);
            m_AsyncConnect = ConnectState.Connected;

            SetActive(true);
            RegisterSystemHandlers(true);

            if (generateConnectMsg)
            {
                PostInternalMessage(MsgType.Connect);
            }
            m_Connected = true;
        }

        internal override void Update()
        {
            ProcessInternalMessages();
        }

        // Called by the server to set the LocalClient's LocalPlayer object during NetworkServer.AddPlayer()
        internal void AddLocalPlayer(PlayerController localPlayer)
        {
            if (LogFilter.logDev) Debug.Log("Local client AddLocalPlayer " + localPlayer.gameObject.name + " conn=" + m_Connection.connectionId);
            m_Connection.isReady = true;
            m_Connection.SetPlayerController(localPlayer);
            var uv = localPlayer.unetView;
            if (uv != null)
            {
                ClientScene.SetLocalObject(uv.netId, localPlayer.gameObject);
                uv.SetConnectionToServer(m_Connection);
            }
            // there is no SystemOwnerMessage for local client. add to ClientScene here instead
            ClientScene.InternalAddPlayer(uv, localPlayer.playerControllerId);
        }

        private void PostInternalMessage(byte[] buffer, int channelId)
        {
            InternalMsg msg = new InternalMsg();
            msg.buffer = buffer;
            msg.channelId = channelId;
            m_InternalMsgs.Add(msg);
        }

        private void PostInternalMessage(short msgType)
        {
            NetworkWriter writer = new NetworkWriter();
            writer.StartMessage(msgType);
            writer.FinishMessage();

            PostInternalMessage(writer.ToArray(), 0);
        }

        private void ProcessInternalMessages()
        {
            if (m_InternalMsgs.Count == 0)
            {
                return;
            }

            // new msgs will get put in m_InternalMsgs2
            List<InternalMsg> tmp = m_InternalMsgs;
            m_InternalMsgs = m_InternalMsgs2;

            // iterate through existing set
            foreach (InternalMsg msg in tmp) // vis2k: foreach
            {
                if (s_InternalMessage.reader == null)
                {
                    s_InternalMessage.reader = new NetworkReader(msg.buffer);
                }
                else
                {
                    s_InternalMessage.reader.Replace(msg.buffer);
                }
                s_InternalMessage.reader.ReadInt16(); //size
                s_InternalMessage.channelId = msg.channelId;
                s_InternalMessage.conn = connection;
                s_InternalMessage.msgType = s_InternalMessage.reader.ReadInt16();

                m_Connection.InvokeHandler(s_InternalMessage);
                connection.lastMessageTime = Time.time;
            }

            // put m_InternalMsgs back and clear it
            m_InternalMsgs = tmp;
            m_InternalMsgs.Clear();

            // add any newly generated msgs in m_InternalMsgs2 and clear it
            m_InternalMsgs.AddRange(m_InternalMsgs2); // vis2k: AddRange instead of for loop
            m_InternalMsgs2.Clear();
        }

        // called by the server, to bypass network
        internal void InvokeHandlerOnClient(short msgType, MessageBase msg, int channelId)
        {
            // write the message to a local buffer
            NetworkWriter writer = new NetworkWriter();
            writer.StartMessage(msgType);
            msg.Serialize(writer);
            writer.FinishMessage();

            InvokeBytesOnClient(writer.ToArray(), channelId);
        }

        // called by the server, to bypass network
        internal void InvokeBytesOnClient(byte[] buffer, int channelId)
        {
            PostInternalMessage(buffer, channelId);
        }
    }
}
#endif //ENABLE_UNET
