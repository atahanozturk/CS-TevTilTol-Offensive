// vis2k: replaced NetworkBuffer with MemoryStream. Saves code, virtually bug free forever.
#if ENABLE_UNET
using System;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_METRO_8_1 || UNITY_WP_8_1
using WinRTLegacy.Text;
using Encoding = WinRTLegacy.Text.Encoding;
#endif

namespace UnityEngine.Networking
{
    public class NetworkReader
    {
        MemoryStream stream;
        public long Position { get { return stream.Position; } } // changed to long for best support
        public long Length { get { return stream.Length; } } // changed to long for best support

        const int k_MaxStringLength = 1024 * 32;
        const int k_InitialStringBufferSize = 1024;
        static byte[] s_StringReaderBuffer = new byte[k_InitialStringBufferSize];
        static Encoding s_Encoding = new UTF8Encoding();

        public NetworkReader()
        {
            stream = new MemoryStream();
        }

        public NetworkReader(NetworkWriter writer)
        {
            stream = new MemoryStream(writer.ToArray());
        }

        public NetworkReader(byte[] buffer)
        {
            stream = new MemoryStream(buffer);
        }

        public void SeekZero()
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        internal void Replace(byte[] buffer)
        {
            // better than seeking zero and writing buffer since this way the size is still fixed
            stream = new MemoryStream(buffer);
        }

        public byte ReadByte()
        {
            return (byte)stream.ReadByte();
        }

        public sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }

        // http://sqlite.org/src4/doc/trunk/www/varint.wiki
        // NOTE: big endian.

        public UInt32 ReadPackedUInt32()
        {
            byte a0 = ReadByte();
            if (a0 < 241)
            {
                return a0;
            }
            byte a1 = ReadByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return (UInt32)(240 + 256 * (a0 - 241) + a1);
            }
            byte a2 = ReadByte();
            if (a0 == 249)
            {
                return (UInt32)(2288 + 256 * a1 + a2);
            }
            byte a3 = ReadByte();
            if (a0 == 250)
            {
                return a1 + (((UInt32)a2) << 8) + (((UInt32)a3) << 16);
            }
            byte a4 = ReadByte();
            if (a0 >= 251)
            {
                return a1 + (((UInt32)a2) << 8) + (((UInt32)a3) << 16) + (((UInt32)a4) << 24);
            }
            throw new IndexOutOfRangeException("ReadPackedUInt32() failure: " + a0);
        }

        public UInt64 ReadPackedUInt64()
        {
            byte a0 = ReadByte();
            if (a0 < 241)
            {
                return a0;
            }

            byte a1 = ReadByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return 240 + 256 * (a0 - ((UInt64)241)) + a1;
            }

            byte a2 = ReadByte();
            if (a0 == 249)
            {
                return 2288 + (((UInt64)256) * a1) + a2;
            }

            byte a3 = ReadByte();
            if (a0 == 250)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16);
            }

            byte a4 = ReadByte();
            if (a0 == 251)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24);
            }

            byte a5 = ReadByte();
            if (a0 == 252)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32);
            }

            byte a6 = ReadByte();
            if (a0 == 253)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40);
            }

            byte a7 = ReadByte();
            if (a0 == 254)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40) + (((UInt64)a7) << 48);
            }

            byte a8 = ReadByte();
            if (a0 == 255)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40) + (((UInt64)a7) << 48)  + (((UInt64)a8) << 56);
            }

            throw new IndexOutOfRangeException("ReadPackedUInt64() failure: " + a0);
        }

        public NetworkInstanceId ReadNetworkId()
        {
            return new NetworkInstanceId(ReadPackedUInt32());
        }

        public NetworkSceneId ReadSceneId()
        {
            return new NetworkSceneId(ReadPackedUInt32());
        }

        public short ReadInt16()
        {
            ushort value = 0;
            value |= ReadByte();
            value |= (ushort)(ReadByte() << 8);
            return (short)value;
        }

        public ushort ReadUInt16()
        {
            ushort value = 0;
            value |= ReadByte();
            value |= (ushort)(ReadByte() << 8);
            return value;
        }

        public int ReadInt32()
        {
            uint value = 0;
            value |= ReadByte();
            value |= (uint)(ReadByte() << 8);
            value |= (uint)(ReadByte() << 16);
            value |= (uint)(ReadByte() << 24);
            return (int)value;
        }

        public uint ReadUInt32()
        {
            uint value = 0;
            value |= ReadByte();
            value |= (uint)(ReadByte() << 8);
            value |= (uint)(ReadByte() << 16);
            value |= (uint)(ReadByte() << 24);
            return value;
        }

        public long ReadInt64()
        {
            ulong value = 0;

            ulong other = ReadByte();
            value |= other;

            other = ((ulong)ReadByte()) << 8;
            value |= other;

            other = ((ulong)ReadByte()) << 16;
            value |= other;

            other = ((ulong)ReadByte()) << 24;
            value |= other;

            other = ((ulong)ReadByte()) << 32;
            value |= other;

            other = ((ulong)ReadByte()) << 40;
            value |= other;

            other = ((ulong)ReadByte()) << 48;
            value |= other;

            other = ((ulong)ReadByte()) << 56;
            value |= other;

            return (long)value;
        }

        public ulong ReadUInt64()
        {
            ulong value = 0;
            ulong other = ReadByte();
            value |= other;

            other = ((ulong)ReadByte()) << 8;
            value |= other;

            other = ((ulong)ReadByte()) << 16;
            value |= other;

            other = ((ulong)ReadByte()) << 24;
            value |= other;

            other = ((ulong)ReadByte()) << 32;
            value |= other;

            other = ((ulong)ReadByte()) << 40;
            value |= other;

            other = ((ulong)ReadByte()) << 48;
            value |= other;

            other = ((ulong)ReadByte()) << 56;
            value |= other;
            return value;
        }

        public decimal ReadDecimal()
        {
            Int32[] bits = new Int32[4];

            bits[0] = ReadInt32();
            bits[1] = ReadInt32();
            bits[2] = ReadInt32();
            bits[3] = ReadInt32();

            return new decimal(bits);
        }

        public float ReadSingle()
        {
            byte[] bytes = ReadBytes(sizeof(float));
            return BitConverter.ToSingle(bytes, 0);
        }

        public double ReadDouble()
        {
            byte[] bytes = ReadBytes(sizeof(double));
            return BitConverter.ToDouble(bytes, 0);
        }

        public string ReadString()
        {
            UInt16 numBytes = ReadUInt16();
            if (numBytes == 0)
                return "";

            if (numBytes >= k_MaxStringLength)
            {
                throw new IndexOutOfRangeException("ReadString() too long: " + numBytes);
            }

            while (numBytes > s_StringReaderBuffer.Length)
            {
                s_StringReaderBuffer = new byte[s_StringReaderBuffer.Length * 2];
            }

            int read = stream.Read(s_StringReaderBuffer, 0, numBytes);
            if (read != numBytes)
            {
                throw new Exception("ReadString() read result mismatch: " + numBytes + " => " + read);
            }

            char[] chars = s_Encoding.GetChars(s_StringReaderBuffer, 0, numBytes);
            return new string(chars);
        }

        public char ReadChar()
        {
            return (char)ReadByte();
        }

        public bool ReadBoolean()
        {
            return ReadByte() == 1;
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new IndexOutOfRangeException("NetworkReader ReadBytes " + count);
            }

            byte[] value = new byte[count];
            int read = stream.Read(value, 0, count);
            if (read != count)
            {
                throw new Exception("ReadBytes() read result mismatch: " + count + " => " + read);
            }
            return value;
        }

        public byte[] ReadBytesAndSize()
        {
            ushort sz = ReadUInt16();
            if (sz == 0)
            {
                return new byte[0];
            }

            return ReadBytes(sz);
        }

        public Vector2 ReadVector2()
        {
            return new Vector2(ReadSingle(), ReadSingle());
        }

        public Vector3 ReadVector3()
        {
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Vector4 ReadVector4()
        {
            return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Color ReadColor()
        {
            return new Color(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Color32 ReadColor32()
        {
            return new Color32(ReadByte(), ReadByte(), ReadByte(), ReadByte());
        }

        public Quaternion ReadQuaternion()
        {
            return new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Rect ReadRect()
        {
            return new Rect(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Plane ReadPlane()
        {
            return new Plane(ReadVector3(), ReadSingle());
        }

        public Ray ReadRay()
        {
            return new Ray(ReadVector3(), ReadVector3());
        }

        public Matrix4x4 ReadMatrix4x4()
        {
            Matrix4x4 m = new Matrix4x4();
            m.m00 = ReadSingle();
            m.m01 = ReadSingle();
            m.m02 = ReadSingle();
            m.m03 = ReadSingle();
            m.m10 = ReadSingle();
            m.m11 = ReadSingle();
            m.m12 = ReadSingle();
            m.m13 = ReadSingle();
            m.m20 = ReadSingle();
            m.m21 = ReadSingle();
            m.m22 = ReadSingle();
            m.m23 = ReadSingle();
            m.m30 = ReadSingle();
            m.m31 = ReadSingle();
            m.m32 = ReadSingle();
            m.m33 = ReadSingle();
            return m;
        }

        public NetworkHash128 ReadNetworkHash128()
        {
            NetworkHash128 hash;
            hash.i0 = ReadByte();
            hash.i1 = ReadByte();
            hash.i2 = ReadByte();
            hash.i3 = ReadByte();
            hash.i4 = ReadByte();
            hash.i5 = ReadByte();
            hash.i6 = ReadByte();
            hash.i7 = ReadByte();
            hash.i8 = ReadByte();
            hash.i9 = ReadByte();
            hash.i10 = ReadByte();
            hash.i11 = ReadByte();
            hash.i12 = ReadByte();
            hash.i13 = ReadByte();
            hash.i14 = ReadByte();
            hash.i15 = ReadByte();
            return hash;
        }

        public Transform ReadTransform()
        {
            NetworkInstanceId netId = ReadNetworkId();
            if (netId.IsEmpty())
            {
                return null;
            }
            GameObject go = ClientScene.FindLocalObject(netId);
            if (go == null)
            {
                if (LogFilter.logDebug) { Debug.Log("ReadTransform netId:" + netId); }
                return null;
            }

            return go.transform;
        }

        public GameObject ReadGameObject()
        {
            NetworkInstanceId netId = ReadNetworkId();
            if (netId.IsEmpty())
            {
                return null;
            }

            GameObject go;
            if (NetworkServer.active)
            {
                go = NetworkServer.FindLocalObject(netId);
            }
            else
            {
                go = ClientScene.FindLocalObject(netId);
            }
            if (go == null)
            {
                if (LogFilter.logDebug) { Debug.Log("ReadGameObject netId:" + netId + "go: null"); }
            }

            return go;
        }

        public NetworkIdentity ReadNetworkIdentity()
        {
            NetworkInstanceId netId = ReadNetworkId();
            if (netId.IsEmpty())
            {
                return null;
            }
            GameObject go;
            if (NetworkServer.active)
            {
                go = NetworkServer.FindLocalObject(netId);
            }
            else
            {
                go = ClientScene.FindLocalObject(netId);
            }
            if (go == null)
            {
                if (LogFilter.logDebug) { Debug.Log("ReadNetworkIdentity netId:" + netId + "go: null"); }
                return null;
            }

            return go.GetComponent<NetworkIdentity>();
        }

        public override string ToString()
        {
            return stream.ToString();
        }

        public TMsg ReadMessage<TMsg>() where TMsg : MessageBase, new()
        {
            var msg = new TMsg();
            msg.Deserialize(this);
            return msg;
        }
    };
}
#endif //ENABLE_UNET
