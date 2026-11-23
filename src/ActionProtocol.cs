using LiteNetLib.Utils;
using UnityEngine;

namespace PrecinctMP
{
    /// <summary>Actions qu'un client peut demander à l'hôte sur un suspect.</summary>
    public enum SuspectAction : byte
    {
        OrderSurrender = 1,   // « Police ! Les mains en l'air ! »
        Tackle = 2,           // plaquage
        Restrain = 3,         // menotter
        Search = 4,           // fouiller
        Shoot = 5,            // tir
    }

    /// <summary>Client -> hôte : « je fais X sur le suspect N ».</summary>
    public struct ActionRequest
    {
        public SuspectAction Action;
        public ushort SuspectId;
        public Vector3 HitPos;      // point d'impact (tir) ou position du client
        public Vector3 Dir;         // direction du tir / du plaquage
        public float Damage;        // dégâts (tir)

        public void Write(NetDataWriter w)
        {
            w.Put((byte)MsgType.Action);
            w.Put((byte)Action);
            w.Put(SuspectId);
            Net.PutVec(w, HitPos);
            Net.PutVec(w, Dir);
            w.Put(Damage);
        }

        public static ActionRequest Read(NetDataReader r) => new ActionRequest
        {
            Action = (SuspectAction)r.GetByte(),
            SuspectId = r.GetUShort(),
            HitPos = Net.GetVec(r),
            Dir = Net.GetVec(r),
            Damage = Mathf.Clamp(r.GetFloat(), 0f, 200f), // borné : un client ne peut pas one-shot tout le monde
        };
    }

    /// <summary>Hôte -> client demandeur : résultat (accepté/refusé + message).</summary>
    public struct ActionResult
    {
        public SuspectAction Action;
        public ushort SuspectId;
        public bool Ok;
        public string Message;

        public void Write(NetDataWriter w)
        {
            w.Put((byte)MsgType.ActionResult);
            w.Put((byte)Action);
            w.Put(SuspectId);
            w.Put(Ok);
            w.Put(Message ?? "", 120);
        }

        public static ActionResult Read(NetDataReader r) => new ActionResult
        {
            Action = (SuspectAction)r.GetByte(),
            SuspectId = r.GetUShort(),
            Ok = r.GetBool(),
            Message = r.GetString(120),
        };
    }
}
