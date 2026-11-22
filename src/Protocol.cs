using LiteNetLib.Utils;
using UnityEngine;

namespace PrecinctMP
{
    /// <summary>Types de messages échangés sur le réseau.</summary>
    public enum MsgType : byte
    {
        Hello = 1,        // client -> hôte : version + pseudo
        Welcome = 2,      // hôte -> client : id attribué
        PlayerJoined = 3, // hôte -> tous : un joueur arrive
        PlayerLeft = 4,   // hôte -> tous : un joueur part
        State = 5,        // tous : position du joueur / de sa voiture (20 Hz)
        Chat = 6,         // tous : message texte
        World = 7,        // hôte -> clients : heure + météo (toutes les 2 s)
    }

    /// <summary>État d'un joueur à un instant donné.</summary>
    public struct PlayerState
    {
        public byte Id;
        public uint Seq;
        public Vector3 Pos;
        public Quaternion Rot;
        public bool InVehicle;
        public Vector3 VehPos;
        public Quaternion VehRot;
        public Vector3 VehVel;
        public int VehicleType;  // Il2Cpp.VehicleManager.VehicleType
        public int Livery;       // Il2Cpp.CustomisationManager.VehicleLiveryCustomisation
        public byte Siren;       // Il2Cpp.Siren.SirenState (0 = éteinte)
        public float[] Anim;     // à pied : paramètres Animator + poids des calques (voir AnimatorSync)
        public byte AnimParamCount;
        public AnimatorSync.LayerState[] AnimStates; // état joué par calque (actions ponctuelles)

        public void Write(NetDataWriter w)
        {
            w.Put((byte)MsgType.State);
            w.Put(Id);
            w.Put(Seq);
            Net.PutVec(w, Pos);
            Net.PutQuat(w, Rot);
            bool hasAnim = !InVehicle && Anim != null && Anim.Length > 0;
            byte flags = (byte)((InVehicle ? 1 : 0) | (hasAnim ? 2 : 0));
            w.Put(flags);
            if (InVehicle)
            {
                Net.PutVec(w, VehPos);
                Net.PutQuat(w, VehRot);
                Net.PutVec(w, VehVel);
                w.Put((short)VehicleType);
                w.Put((byte)Livery);
                w.Put(Siren);
            }
            if (hasAnim)
            {
                int n = System.Math.Min(Anim.Length, AnimatorSync.MaxValues);
                w.Put((byte)n);
                w.Put(AnimParamCount);
                for (int i = 0; i < n; i++) w.Put(Anim[i]);
                int ls = AnimStates == null ? 0 : System.Math.Min(AnimStates.Length, AnimatorSync.MaxLayers);
                w.Put((byte)ls);
                for (int i = 0; i < ls; i++) { w.Put(AnimStates[i].Hash); w.Put(AnimStates[i].Time); }
            }
        }

        public static PlayerState Read(NetDataReader r)
        {
            var s = new PlayerState
            {
                Id = r.GetByte(),
                Seq = r.GetUInt(),
                Pos = Net.GetVec(r),
                Rot = Net.GetQuat(r),
            };
            byte flags = r.GetByte();
            s.InVehicle = (flags & 1) != 0;
            if (s.InVehicle)
            {
                s.VehPos = Net.GetVec(r);
                s.VehRot = Net.GetQuat(r);
                s.VehVel = Net.GetVec(r);
                s.VehicleType = r.GetShort();
                s.Livery = r.GetByte();
                s.Siren = r.GetByte();
            }
            if ((flags & 2) != 0)
            {
                int n = r.GetByte();
                if (n > AnimatorSync.MaxValues) throw new System.IO.InvalidDataException("anim trop long");
                s.AnimParamCount = r.GetByte();
                s.Anim = new float[n];
                for (int i = 0; i < n; i++) s.Anim[i] = r.GetFloat();
                int ls = r.GetByte();
                if (ls > AnimatorSync.MaxLayers) throw new System.IO.InvalidDataException("trop de calques");
                s.AnimStates = new AnimatorSync.LayerState[ls];
                for (int i = 0; i < ls; i++) { s.AnimStates[i].Hash = r.GetInt(); s.AnimStates[i].Time = r.GetFloat(); }
            }
            return s;
        }
    }

    public static class Net
    {
        public const int ProtocolVersion = 5;
        public const string ConnectionKey = "PrecinctMP";

        public static void PutVec(NetDataWriter w, Vector3 v) { w.Put(v.x); w.Put(v.y); w.Put(v.z); }
        public static Vector3 GetVec(NetDataReader r) => new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
        public static void PutQuat(NetDataWriter w, Quaternion q) { w.Put(q.x); w.Put(q.y); w.Put(q.z); w.Put(q.w); }
        public static Quaternion GetQuat(NetDataReader r) => new Quaternion(r.GetFloat(), r.GetFloat(), r.GetFloat(), r.GetFloat());
    }
}
