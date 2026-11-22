using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using MelonLoader;

namespace PrecinctMP
{
    /// <summary>
    /// Session réseau hôte/client (UDP, LiteNetLib).
    /// L'hôte a l'id 0 et relaie les états des clients vers les autres.
    /// </summary>
    public class NetSession
    {
        public const int MaxPlayers = 4;

        public bool IsHost { get; private set; }
        public bool IsActive => _net != null && _net.IsRunning;
        public bool IsConnected => IsHost || _server != null && _server.ConnectionState == ConnectionState.Connected;
        public byte LocalId { get; private set; } = 255;

        public event Action<PlayerState> StateReceived;
        public event Action<byte, string> PlayerJoined;
        public event Action<byte> PlayerLeft;
        public event Action<byte, string> ChatReceived;
        public event Action<WorldState> WorldReceived;
        public event Action<List<IncidentInfo>> IncidentsReceived;
        public event Action<List<SuspectState>> SuspectsReceived;
        /// <summary>Hôte : un client vient d'être accepté (pour lui envoyer l'état du monde).</summary>
        public event Action<byte> ClientAccepted;

        private NetManager _net;
        private NetPeer _server; // côté client
        private readonly EventBasedNetListener _listener = new EventBasedNetListener();
        private readonly NetDataWriter _w = new NetDataWriter();
        private readonly Dictionary<NetPeer, byte> _peerToId = new Dictionary<NetPeer, byte>();
        private readonly Dictionary<byte, string> _names = new Dictionary<byte, string>();
        private string _localName;

        public NetSession()
        {
            _listener.ConnectionRequestEvent += OnConnectionRequest;
            _listener.PeerConnectedEvent += OnPeerConnected;
            _listener.PeerDisconnectedEvent += OnPeerDisconnected;
            _listener.NetworkReceiveEvent += OnReceive;
        }

        // ---------- Démarrage / arrêt ----------

        public void Host(int port, string name)
        {
            Stop();
            _localName = name;
            _net = new NetManager(_listener) { AutoRecycle = true, UpdateTime = 15 };
            if (!_net.Start(port))
            {
                MelonLogger.Error($"[MP] Impossible d'ouvrir le port {port}.");
                _net = null;
                return;
            }
            IsHost = true;
            LocalId = 0;
            _names[0] = name;
            MelonLogger.Msg($"[MP] Partie hébergée sur le port {port}.");
        }

        public void Join(string ip, int port, string name)
        {
            Stop();
            _localName = name;
            _net = new NetManager(_listener) { AutoRecycle = true, UpdateTime = 15 };
            _net.Start();
            IsHost = false;
            _server = _net.Connect(ip, port, Net.ConnectionKey);
            MelonLogger.Msg($"[MP] Connexion à {ip}:{port}...");
        }

        public void Stop()
        {
            if (_net == null) return;
            _net.Stop();
            _net = null;
            _server = null;
            _peerToId.Clear();
            _names.Clear();
            IsHost = false;
            LocalId = 255;
            MelonLogger.Msg("[MP] Session arrêtée.");
        }

        public void Poll() => _net?.PollEvents();

        public IReadOnlyDictionary<byte, string> Names => _names;

        // ---------- Envoi ----------

        public void SendState(PlayerState s)
        {
            if (!IsConnected || LocalId == 255) return;
            s.Id = LocalId;
            _w.Reset();
            s.Write(_w);
            if (IsHost) _net.SendToAll(_w, DeliveryMethod.Sequenced);
            else _server.Send(_w, DeliveryMethod.Sequenced);
        }

        /// <summary>Hôte uniquement : diffuse l'heure et la météo.</summary>
        public void SendWorld(WorldState ws)
        {
            if (!IsHost || _net == null || _peerToId.Count == 0) return;
            _w.Reset();
            ws.Write(_w);
            _net.SendToAll(_w, DeliveryMethod.ReliableSequenced);
        }

        /// <summary>Hôte uniquement : crimes en cours.</summary>
        public void SendIncidents(List<IncidentInfo> list)
        {
            if (!IsHost || _net == null || _peerToId.Count == 0) return;
            _w.Reset();
            SharedWorldProtocol.WriteIncidents(_w, list);
            _net.SendToAll(_w, DeliveryMethod.ReliableSequenced);
        }

        /// <summary>Hôte uniquement : suspects (positions, animations).</summary>
        public void SendSuspects(List<SuspectState> list)
        {
            if (!IsHost || _net == null || _peerToId.Count == 0) return;
            int next = 0, guard = 0;
            int total = System.Math.Min(list.Count, SharedWorldProtocol.MaxSuspects);
            while (next < total && guard++ < SharedWorldProtocol.MaxSuspects)
            {
                _w.Reset();
                next = SharedWorldProtocol.WriteSuspects(_w, list, next);
                _net.SendToAll(_w, DeliveryMethod.Unreliable);
            }
        }

        public bool HasClients => IsHost && _peerToId.Count > 0;

        public void SendChat(string text)
        {
            if (!IsConnected || LocalId == 255) return;
            _w.Reset();
            _w.Put((byte)MsgType.Chat);
            _w.Put(LocalId);
            _w.Put(text);
            if (IsHost) _net.SendToAll(_w, DeliveryMethod.ReliableOrdered);
            else _server.Send(_w, DeliveryMethod.ReliableOrdered);
            ChatReceived?.Invoke(LocalId, text);
        }

        // ---------- Événements LiteNetLib ----------

        private void OnConnectionRequest(ConnectionRequest req)
        {
            if (IsHost && _peerToId.Count < MaxPlayers - 1) req.AcceptIfKey(Net.ConnectionKey);
            else req.Reject();
        }

        private void OnPeerConnected(NetPeer peer)
        {
            if (IsHost) return; // l'hôte attend le Hello
            _w.Reset();
            _w.Put((byte)MsgType.Hello);
            _w.Put(Net.ProtocolVersion);
            _w.Put(_localName);
            peer.Send(_w, DeliveryMethod.ReliableOrdered);
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            if (IsHost)
            {
                if (!_peerToId.TryGetValue(peer, out byte id)) return;
                _peerToId.Remove(peer);
                _names.Remove(id);
                PlayerLeft?.Invoke(id);
                _w.Reset();
                _w.Put((byte)MsgType.PlayerLeft);
                _w.Put(id);
                _net.SendToAll(_w, DeliveryMethod.ReliableOrdered);
            }
            else
            {
                MelonLogger.Warning($"[MP] Déconnecté de l'hôte ({info.Reason}).");
                foreach (var id in new List<byte>(_names.Keys)) PlayerLeft?.Invoke(id);
                Stop();
            }
        }

        private void OnReceive(NetPeer peer, NetPacketReader r, byte channel, DeliveryMethod method)
        {
            try
            {
                var type = (MsgType)r.GetByte();
                switch (type)
                {
                    case MsgType.Hello when IsHost: HandleHello(peer, r); break;
                    case MsgType.Welcome when !IsHost: LocalId = r.GetByte(); MelonLogger.Msg($"[MP] Connecté, id {LocalId}."); break;
                    case MsgType.PlayerJoined when !IsHost:
                    {
                        byte id = r.GetByte(); string n = r.GetString();
                        _names[id] = n;
                        if (id != LocalId) PlayerJoined?.Invoke(id, n);
                        break;
                    }
                    case MsgType.PlayerLeft when !IsHost:
                    {
                        byte id = r.GetByte();
                        _names.Remove(id);
                        PlayerLeft?.Invoke(id);
                        break;
                    }
                    case MsgType.State:
                    {
                        var s = PlayerState.Read(r);
                        if (IsHost)
                        {
                            if (!_peerToId.TryGetValue(peer, out byte realId)) return;
                            s.Id = realId; // anti-usurpation
                            _w.Reset(); s.Write(_w);
                            _net.SendToAll(_w, DeliveryMethod.Sequenced, peer); // relais
                        }
                        if (s.Id != LocalId) StateReceived?.Invoke(s);
                        break;
                    }
                    case MsgType.World when !IsHost:
                        WorldReceived?.Invoke(WorldState.Read(r));
                        break;
                    case MsgType.Incidents when !IsHost:
                        IncidentsReceived?.Invoke(SharedWorldProtocol.ReadIncidents(r));
                        break;
                    case MsgType.Suspects when !IsHost:
                        SuspectsReceived?.Invoke(SharedWorldProtocol.ReadSuspects(r));
                        break;
                    case MsgType.Chat:
                    {
                        byte id = r.GetByte(); string text = r.GetString();
                        if (IsHost)
                        {
                            if (!_peerToId.TryGetValue(peer, out id)) return;
                            if (text.Length > 200) text = text.Substring(0, 200);
                            _w.Reset(); _w.Put((byte)MsgType.Chat); _w.Put(id); _w.Put(text);
                            _net.SendToAll(_w, DeliveryMethod.ReliableOrdered, peer);
                        }
                        ChatReceived?.Invoke(id, text);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[MP] Paquet invalide ignoré : {e.Message}");
            }
        }

        private void HandleHello(NetPeer peer, NetPacketReader r)
        {
            int version = r.GetInt();
            string name = r.GetString();
            if (name.Length > 24) name = name.Substring(0, 24);
            if (version != Net.ProtocolVersion)
            {
                MelonLogger.Warning($"[MP] {name} refusé : version {version} (attendu {Net.ProtocolVersion}).");
                peer.Disconnect();
                return;
            }

            byte id = 1;
            while (_names.ContainsKey(id)) id++;
            _peerToId[peer] = id;
            _names[id] = name;

            // Id attribué au nouveau
            _w.Reset(); _w.Put((byte)MsgType.Welcome); _w.Put(id);
            peer.Send(_w, DeliveryMethod.ReliableOrdered);

            // Liste des joueurs déjà présents -> nouveau
            foreach (var kv in _names)
            {
                _w.Reset(); _w.Put((byte)MsgType.PlayerJoined); _w.Put(kv.Key); _w.Put(kv.Value);
                peer.Send(_w, DeliveryMethod.ReliableOrdered);
            }

            // Annonce du nouveau -> les autres
            _w.Reset(); _w.Put((byte)MsgType.PlayerJoined); _w.Put(id); _w.Put(name);
            _net.SendToAll(_w, DeliveryMethod.ReliableOrdered, peer);

            PlayerJoined?.Invoke(id, name);
            ClientAccepted?.Invoke(id);
            MelonLogger.Msg($"[MP] {name} a rejoint (id {id}).");
        }
    }
}
