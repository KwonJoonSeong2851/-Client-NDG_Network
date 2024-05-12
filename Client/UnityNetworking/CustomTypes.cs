using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG.UnityNet
{
    using NDG.Realtime;
    using UnityEngine;

    /// <summary>
    /// 커스텀 타입의 Serialize 및 deSerialize를 구현합니다.
    /// </summary>
    internal static class CustomTypes
    {
        /// <summary>Register</summary>
        internal static void Register()
        {
            Peer.RegisterType(typeof(Vector2), (byte)'W', SerializeVector2, DeserializeVector2);
            Peer.RegisterType(typeof(Vector3), (byte)'V', SerializeVector3, DeserializeVector3);
            Peer.RegisterType(typeof(Quaternion), (byte)'Q', SerializeQuaternion, DeserializeQuaternion);
            Peer.RegisterType(typeof(Player), (byte)'P', SerializeNetPlayer,DeserializeNetPlayer);
        }


        #region Custom De/Serializer Methods

        public static readonly byte[] memVector3 = new byte[3 * 4];

        private static short SerializeVector3(StreamBuffer outStream, object customobject)
        {
            Vector3 vo = (Vector3)customobject;

            int index = 0;
            lock (memVector3)
            {
                byte[] bytes = memVector3;
                Protocol.Serialize(vo.x, bytes, ref index);
                Protocol.Serialize(vo.y, bytes, ref index);
                Protocol.Serialize(vo.z, bytes, ref index);
                outStream.Write(bytes, 0, 3 * 4);
            }

            return 3 * 4;
        }

        private static object DeserializeVector3(StreamBuffer inStream, short length)
        {
            Vector3 vo = new Vector3();
            lock (memVector3)
            {
                inStream.Read(memVector3, 0, 3 * 4);
                int index = 0;
                Protocol.Deserialize(out vo.x, memVector3, ref index);
                Protocol.Deserialize(out vo.y, memVector3, ref index);
                Protocol.Deserialize(out vo.z, memVector3, ref index);
            }

            return vo;
        }


        public static readonly byte[] memVector2 = new byte[2 * 4];

        private static short SerializeVector2(StreamBuffer outStream, object customobject)
        {
            Vector2 vo = (Vector2)customobject;
            lock (memVector2)
            {
                byte[] bytes = memVector2;
                int index = 0;
                Protocol.Serialize(vo.x, bytes, ref index);
                Protocol.Serialize(vo.y, bytes, ref index);
                outStream.Write(bytes, 0, 2 * 4);
            }

            return 2 * 4;
        }

        private static object DeserializeVector2(StreamBuffer inStream, short length)
        {
            Vector2 vo = new Vector2();
            lock (memVector2)
            {
                inStream.Read(memVector2, 0, 2 * 4);
                int index = 0;
                Protocol.Deserialize(out vo.x, memVector2, ref index);
                Protocol.Deserialize(out vo.y, memVector2, ref index);
            }

            return vo;
        }


        public static readonly byte[] memQuarternion = new byte[4 * 4];

        private static short SerializeQuaternion(StreamBuffer outStream, object customobject)
        {
            Quaternion o = (Quaternion)customobject;

            lock (memQuarternion)
            {
                byte[] bytes = memQuarternion;
                int index = 0;
                Protocol.Serialize(o.w, bytes, ref index);
                Protocol.Serialize(o.x, bytes, ref index);
                Protocol.Serialize(o.y, bytes, ref index);
                Protocol.Serialize(o.z, bytes, ref index);
                outStream.Write(bytes, 0, 4 * 4);
            }

            return 4 * 4;
        }

        private static object DeserializeQuaternion(StreamBuffer inStream, short length)
        {
            Quaternion o = new Quaternion();

            lock (memQuarternion)
            {
                inStream.Read(memQuarternion, 0, 4 * 4);
                int index = 0;
                Protocol.Deserialize(out o.w, memQuarternion, ref index);
                Protocol.Deserialize(out o.x, memQuarternion, ref index);
                Protocol.Deserialize(out o.y, memQuarternion, ref index);
                Protocol.Deserialize(out o.z, memQuarternion, ref index);
            }

            return o;
        }

        public static readonly byte[] memPlayer = new byte[4];

        private static short SerializeNetPlayer(StreamBuffer outStream, object customobject)
        {
            int ID = ((Player)customobject).ActorNumber;

            lock (memPlayer)
            {
                byte[] bytes = memPlayer;
                int off = 0;
                Protocol.Serialize(ID, bytes, ref off);
                outStream.Write(bytes, 0, 4);
                return 4;
            }
        }

        private static object DeserializeNetPlayer(StreamBuffer inStream, short length)
        {
            int ID;
            lock (memPlayer)
            {
                inStream.Read(memPlayer, 0, length);
                int off = 0;
                Protocol.Deserialize(out ID, memPlayer, ref off);
            }

            if (NDG_Network.CurrentRoom != null)
            {
                Player player = NDG_Network.CurrentRoom.GetPlayer(ID);
                return player;
            }
            return null;
        }

        #endregion
    }
}
