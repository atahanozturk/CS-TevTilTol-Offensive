using System;
using System.Collections.Generic;

#if ENABLE_UNET

namespace UnityEngine.Networking
{
    // This can't be an interface because users don't need to implement the
    // serialization functions, we'll code generate it for them when they omit it.
    public abstract class MessageBase
    {
        // De-serialize the contents of the reader into this message
        public virtual void Deserialize(NetworkReader reader) {}

        // Serialize the contents of this message into the writer
        public virtual void Serialize(NetworkWriter writer) {}
    }
}

namespace UnityEngine.Networking.NetworkSystem
{
    // ---------- General Typed Messages -------------------

    public class StringMessage : MessageBase
    {
        public string value;

        public StringMessage()
        {
        }

        public StringMessage(string v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadString();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(value);
        }
    }

    public class IntegerMessage : MessageBase
    {
        public int value;

        public IntegerMessage()
        {
        }

        public IntegerMessage(int v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = (int)reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint)value);
        }
    }

    public class EmptyMessage : MessageBase
    {
        public override void Deserialize(NetworkReader reader)
        {
        }

        public override void Serialize(NetworkWriter writer)
        {
        }
    }

    // ---------- Public System Messages -------------------

    public class ErrorMessage : MessageBase
    {
        // vis2k: NetworkServer uses byte for errors, so no need to use int here
        public byte errorCode;

        public override void Deserialize(NetworkReader reader)
        {
            errorCode = reader.ReadByte();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(errorCode);
        }
    }

    public class ReadyMessage : EmptyMessage
    {
    }

    public class NotReadyMessage : EmptyMessage
    {
    }

    public class AddPlayerMessage : MessageBase
    {
        public short playerControllerId;
        public int msgSize;
        public byte[] msgData;

        public override void Deserialize(NetworkReader reader)
        {
            playerControllerId = reader.ReadInt16(); // vis2k: no need to use UInt16
            msgData = reader.ReadBytesAndSize();
            if (msgData == null)
            {
                msgSize = 0;
            }
            else
            {
                msgSize = msgData.Length;
            }
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(playerControllerId); // vis2k: no need to cast to ushort
            writer.WriteBytesAndSize(msgData, msgSize);
        }
    }

    public class RemovePlayerMessage : MessageBase
    {
        public short playerControllerId;

        public override void Deserialize(NetworkReader reader)
        {
            playerControllerId = reader.ReadInt16(); // vis2k: no need to use UInt16
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(playerControllerId); // vis2k: no need to cast to ushort
        }
    }


    public class PeerAuthorityMessage : MessageBase
    {
        public int connectionId;
        public NetworkInstanceId netId;
        public bool authorityState;

        public override void Deserialize(NetworkReader reader)
        {
            connectionId = (int)reader.ReadPackedUInt32();
            netId = reader.ReadNetworkId();
            authorityState = reader.ReadBoolean();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint)connectionId);
            writer.Write(netId);
            writer.Write(authorityState);
        }
    }

    public struct PeerInfoPlayer
    {
        public NetworkInstanceId netId;
        public short playerControllerId;
    }

    public class PeerInfoMessage : MessageBase
    {
        public int connectionId;
        public string address;
        public int port;
        public bool isHost;
        public bool isYou;
        public PeerInfoPlayer[] playerIds;

        public override void Deserialize(NetworkReader reader)
        {
            connectionId = (int)reader.ReadPackedUInt32();
            address = reader.ReadString();
            port = (int)reader.ReadPackedUInt32();
            isHost = reader.ReadBoolean();
            isYou = reader.ReadBoolean();

            uint numPlayers = reader.ReadPackedUInt32();
            if (numPlayers > 0)
            {
                playerIds = new PeerInfoPlayer[numPlayers]; // vis2k: create array directly instead of separate list
                for (int i = 0; i < playerIds.Length; ++i)
                {
                    playerIds[i].netId = reader.ReadNetworkId();
                    playerIds[i].playerControllerId = (short)reader.ReadPackedUInt32();
                }
            }
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint)connectionId);
            writer.Write(address);
            writer.WritePackedUInt32((uint)port);
            writer.Write(isHost);
            writer.Write(isYou);
            if (playerIds == null)
            {
                writer.WritePackedUInt32(0);
            }
            else
            {
                writer.WritePackedUInt32((uint)playerIds.Length);
                foreach (PeerInfoPlayer playerId in playerIds) // vis2k: foreach
                {
                    writer.Write(playerId.netId);
                    writer.WritePackedUInt32((uint)playerId.playerControllerId);
                }
            }
        }

        public override string ToString()
        {
            return "PeerInfo conn:" + connectionId + " addr:" + address + ":" + port + " host:" + isHost + " isYou:" + isYou;
        }
    }

    public class PeerListMessage : MessageBase
    {
        public PeerInfoMessage[] peers;
        public int oldServerConnectionId;

        public override void Deserialize(NetworkReader reader)
        {
            oldServerConnectionId = (int)reader.ReadPackedUInt32();
            ushort numPeers = reader.ReadUInt16(); // vis2k: changed to ushort to avoid confusion
            peers = new PeerInfoMessage[numPeers];
            for (int i = 0; i < peers.Length; ++i)
            {
                var peerInfo = new PeerInfoMessage();
                peerInfo.Deserialize(reader);
                peers[i] = peerInfo;
            }
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint)oldServerConnectionId);
            writer.Write((ushort)peers.Length);
            for (int i = 0; i < peers.Length; i++)
            {
                peers[i].Serialize(writer);
            }
        }
    }

#if ENABLE_UNET_HOST_MIGRATION
    public class ReconnectMessage : MessageBase
    {
        public int oldConnectionId;
        public short playerControllerId;
        public NetworkInstanceId netId;
        public int msgSize;
        public byte[] msgData;

        public override void Deserialize(NetworkReader reader)
        {
            oldConnectionId = (int)reader.ReadPackedUInt32();
            playerControllerId = (short)reader.ReadPackedUInt32();
            netId = reader.ReadNetworkId();
            msgData = reader.ReadBytesAndSize();
            msgSize = msgData.Length;
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint)oldConnectionId);
            writer.WritePackedUInt32((uint)playerControllerId);
            writer.Write(netId);
            writer.WriteBytesAndSize(msgData, msgSize);
        }
    }
#endif

    // ---------- System Messages requried for code gen path -------------------
    /* These are not used directly but manually serialized, these are here for reference.

    public struct CommandMessage
    {
        public int cmdHash;
        public string cmdName;
        public byte[] payload;
    }
    public struct RPCMessage
    {
        public NetworkId netId;
        public int cmdHash;
        public byte[] payload;
    }
    public struct SyncEventMessage
    {
        public NetworkId netId;
        public int cmdHash;
        public byte[] payload;
    }

    internal class SyncListMessage<T> where T: struct
    {
        public NetworkId netId;
        public int cmdHash;
        public byte operation;
        public int itemIndex;
        public T item;
    }

*/

    // ---------- Internal System Messages -------------------

    class ObjectSpawnMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public NetworkHash128 assetId;
        public Vector3 position;
        public Quaternion rotation;
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            assetId = reader.ReadNetworkHash128();
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion(); // vis2k: always read it, because we always write it.
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.Write(assetId);
            writer.Write(position);
            writer.Write(rotation);
            writer.WriteBytesFull(payload);
        }
    }

    class ObjectSpawnSceneMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public NetworkSceneId sceneId;
        public Vector3 position;
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            sceneId = reader.ReadSceneId();
            position = reader.ReadVector3();
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.Write(sceneId);
            writer.Write(position);
            writer.WriteBytesFull(payload);
        }
    }

    class ObjectSpawnFinishedMessage : MessageBase
    {
        public byte state; // vis2k: changed from uint to byte because it's always either 0 or 1

        public override void Deserialize(NetworkReader reader)
        {
            state = reader.ReadByte();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(state);
        }
    }

    class ObjectDestroyMessage : MessageBase
    {
        public NetworkInstanceId netId;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
        }
    }

    class OwnerMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public short playerControllerId;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            playerControllerId = (short)reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.WritePackedUInt32((uint)playerControllerId);
        }
    }

    class ClientAuthorityMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public bool authority;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            authority = reader.ReadBoolean();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.Write(authority);
        }
    }

    class OverrideTransformMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public byte[] payload;
        public bool teleport;
        public int time;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            payload = reader.ReadBytesAndSize();
            teleport = reader.ReadBoolean();
            time = (int)reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.WriteBytesFull(payload);
            writer.Write(teleport);
            writer.WritePackedUInt32((uint)time);
        }
    }

    class AnimationMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public int      stateHash;      // if non-zero, then Play() this animation, skipping transitions
        public float    normalizedTime;
        public byte[]   parameters;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            stateHash = (int)reader.ReadPackedUInt32();
            normalizedTime = reader.ReadSingle();
            parameters = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.WritePackedUInt32((uint)stateHash);
            writer.Write(normalizedTime);
            writer.WriteBytesAndSize(parameters, parameters != null ? parameters.Length : 0); // vis2k: shorter
        }
    }

    class AnimationParametersMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public byte[] parameters;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            parameters = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.WriteBytesAndSize(parameters, parameters != null ? parameters.Length : 0); // vis2k: shorter
        }
    }

    class AnimationTriggerMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public int hash;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            hash = (int)reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.WritePackedUInt32((uint)hash);
        }
    }

    class LobbyReadyToBeginMessage : MessageBase
    {
        public byte slotId;
        public bool readyState;

        public override void Deserialize(NetworkReader reader)
        {
            slotId = reader.ReadByte();
            readyState = reader.ReadBoolean();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(slotId);
            writer.Write(readyState);
        }
    }

    struct CRCMessageEntry
    {
        public string name;
        public byte channel;
    }

    class CRCMessage : MessageBase
    {
        public CRCMessageEntry[] scripts;

        public override void Deserialize(NetworkReader reader)
        {
            int numScripts = reader.ReadUInt16();
            scripts = new CRCMessageEntry[numScripts];
            for (int i = 0; i < scripts.Length; ++i)
            {
                // vis2k: no need to create new CRCMessageEntry since it's a struct, the array already takes care of it
                scripts[i].name = reader.ReadString();
                scripts[i].channel = reader.ReadByte();
            }
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((ushort)scripts.Length);
            for (int i = 0; i < scripts.Length; ++i)
            {
                writer.Write(scripts[i].name);
                writer.Write(scripts[i].channel);
            }
        }
    }
}
#endif //ENABLE_UNET
