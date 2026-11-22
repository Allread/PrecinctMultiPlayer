using System.Collections.Generic;
using LiteNetLib.Utils;
using UnityEngine;

namespace PrecinctMP
{
    /// <summary>Un crime / appel en cours chez l'hôte.</summary>
    public struct IncidentInfo
    {
        public ushort Id;
        public short CrimeType;     // Il2Cpp.AmbientCrimeManager.AmbientCrimeType
        public byte State;          // Il2Cpp.AmbientCrimeState : Inactive / Active_NoObjective / Active_Objective
        public Vector3 Pos;
        public bool HostOnIt;       // c'est l'appel actif de l'hôte
        public byte Suspects, Resolved;
    }

    /// <summary>Un suspect (cible d'un crime) chez l'hôte.</summary>
    public struct SuspectState
    {
        public ushort Id;
        public ushort IncidentId;
        public byte Status;         // Il2Cpp.ObjectiveTarget.Status : Normal, Surrendered, Arrested, Dead, Escaped, Restrained
        public Vector3 Pos;
        public Quaternion Rot;
        public string Model;        // nom du modèle (apparence) chez l'hôte
        public bool InVehicle;
        public Vector3 VehPos;
        public Quaternion VehRot;
        public Vector3 VehVel;
        public int VehicleType;
        public float[] Anim;
        public byte AnimParamCount;
        public AnimatorSync.LayerState[] AnimStates;
    }

    public static class SharedWorldProtocol
    {
        public const int MaxIncidents = 16;
        public const int MaxSuspects = 12;

        // ---------- Incidents (hôte -> clients, toutes les secondes) ----------

        public static void WriteIncidents(NetDataWriter w, List<IncidentInfo> list)
        {
            w.Put((byte)MsgType.Incidents);
            int n = Mathf.Min(list.Count, MaxIncidents);
            w.Put((byte)n);
            for (int i = 0; i < n; i++)
            {
                var c = list[i];
                w.Put(c.Id);
                w.Put(c.CrimeType);
                w.Put(c.State);
                Net.PutVec(w, c.Pos);
                w.Put(c.HostOnIt);
                w.Put(c.Suspects);
                w.Put(c.Resolved);
            }
        }

        public static List<IncidentInfo> ReadIncidents(NetDataReader r)
        {
            int n = r.GetByte();
            if (n > MaxIncidents) throw new System.IO.InvalidDataException("trop d'incidents");
            var list = new List<IncidentInfo>(n);
            for (int i = 0; i < n; i++)
            {
                list.Add(new IncidentInfo
                {
                    Id = r.GetUShort(),
                    CrimeType = r.GetShort(),
                    State = r.GetByte(),
                    Pos = Net.GetVec(r),
                    HostOnIt = r.GetBool(),
                    Suspects = r.GetByte(),
                    Resolved = r.GetByte(),
                });
            }
            return list;
        }

        // ---------- Suspects (hôte -> clients, 10 fois par seconde) ----------

        /// <summary>Taille max d'un paquet non fiable (sous le MTU, LiteNetLib ne fragmente pas ces paquets).</summary>
        public const int MaxPacketBytes = 1000;

        /// <summary>
        /// Écrit autant de suspects que possible à partir de <paramref name="start"/> sans
        /// dépasser MaxPacketBytes. Renvoie l'index du premier suspect non écrit.
        /// Le client retire les suspects qui ne sont plus mis à jour (voir RemoteSuspects).
        /// </summary>
        public static int WriteSuspects(NetDataWriter w, List<SuspectState> list, int start)
        {
            w.Put((byte)MsgType.Suspects);
            int countPos = w.Length;
            w.Put((byte)0);
            int end = Mathf.Min(list.Count, MaxSuspects);
            int written = 0;
            int i = start;
            for (; i < end; i++)
            {
                int before = w.Length;
                WriteSuspect(w, list[i]);
                if (w.Length > MaxPacketBytes && written > 0)
                {
                    w.SetPosition(before); // ne rentre pas : ira dans le paquet suivant
                    break;
                }
                written++;
            }
            w.Data[countPos] = (byte)written;
            return i;
        }

        private static void WriteSuspect(NetDataWriter w, SuspectState s)
        {
            {
                w.Put(s.Id);
                w.Put(s.IncidentId);
                w.Put(s.Status);
                Net.PutVec(w, s.Pos);
                Net.PutQuat(w, s.Rot);
                w.Put(s.Model ?? "", 64);
                w.Put(s.InVehicle);
                if (s.InVehicle)
                {
                    Net.PutVec(w, s.VehPos);
                    Net.PutQuat(w, s.VehRot);
                    Net.PutVec(w, s.VehVel);
                    w.Put((short)s.VehicleType);
                }
                else
                {
                    PutAnim(w, s.Anim, s.AnimParamCount, s.AnimStates);
                }
            }
        }

        public static List<SuspectState> ReadSuspects(NetDataReader r)
        {
            int n = r.GetByte();
            if (n > MaxSuspects) throw new System.IO.InvalidDataException("trop de suspects");
            var list = new List<SuspectState>(n);
            for (int i = 0; i < n; i++)
            {
                var s = new SuspectState
                {
                    Id = r.GetUShort(),
                    IncidentId = r.GetUShort(),
                    Status = r.GetByte(),
                    Pos = Net.GetVec(r),
                    Rot = Net.GetQuat(r),
                    Model = r.GetString(64),
                    InVehicle = r.GetBool(),
                };
                if (s.InVehicle)
                {
                    s.VehPos = Net.GetVec(r);
                    s.VehRot = Net.GetQuat(r);
                    s.VehVel = Net.GetVec(r);
                    s.VehicleType = r.GetShort();
                }
                else
                {
                    GetAnim(r, out s.Anim, out s.AnimParamCount, out s.AnimStates);
                }
                list.Add(s);
            }
            return list;
        }

        // ---------- Animation (même format que pour les joueurs) ----------

        public static void PutAnim(NetDataWriter w, float[] anim, byte paramCount, AnimatorSync.LayerState[] states)
        {
            int n = anim == null ? 0 : Mathf.Min(anim.Length, AnimatorSync.MaxValues);
            w.Put((byte)n);
            w.Put(paramCount);
            for (int i = 0; i < n; i++) w.Put(anim[i]);
            int ls = states == null ? 0 : Mathf.Min(states.Length, AnimatorSync.MaxLayers);
            w.Put((byte)ls);
            for (int i = 0; i < ls; i++) { w.Put(states[i].Hash); w.Put(states[i].Time); }
        }

        public static void GetAnim(NetDataReader r, out float[] anim, out byte paramCount, out AnimatorSync.LayerState[] states)
        {
            int n = r.GetByte();
            if (n > AnimatorSync.MaxValues) throw new System.IO.InvalidDataException("anim trop long");
            paramCount = r.GetByte();
            anim = n > 0 ? new float[n] : null;
            for (int i = 0; i < n; i++) anim[i] = r.GetFloat();
            int ls = r.GetByte();
            if (ls > AnimatorSync.MaxLayers) throw new System.IO.InvalidDataException("trop de calques");
            states = ls > 0 ? new AnimatorSync.LayerState[ls] : null;
            for (int i = 0; i < ls; i++) { states[i].Hash = r.GetInt(); states[i].Time = r.GetFloat(); }
        }
    }
}
