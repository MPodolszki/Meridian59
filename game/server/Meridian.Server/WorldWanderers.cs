using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>Zustand eines wandernden NPCs (wanderer.kod): aktueller NPC, Ziel, nächster Sprung, Fehlversuche.</summary>
public sealed class Wanderer(WandererDefinition definition)
{
    public WandererDefinition Definition => definition;
    public Npc? Npc { get; set; }
    public Room? Room { get; set; }
    public int Destination { get; set; } = -1;
    public DateTime Due { get; set; }
    /// <summary>piTeleport_Counter: Versuche, bei denen Spieler im Raum waren; ab fünf springt er trotzdem.</summary>
    public int Blocked { get; set; }
}

/// <summary>
/// Wandernde NPCs (towns/wanderer.kod): nach 20 s und dann alle Random(30, 90) Minuten zu einem anderen Ziel aus
/// plDestinations; sind Spieler im Raum, wartet er bis zum nächsten Mal (nach dem fünften Mal springt er trotzdem).
/// </summary>
public sealed partial class World
{
    private readonly List<Wanderer> _wanderers = new();

    private void SetUpWanderers(IEnumerable<WandererDefinition> definitions, ShopFile shops, DialogFile dialogs)
    {
        var now = DateTime.UtcNow;
        foreach (var d in definitions)
        {
            var w = new Wanderer(d) { Due = now.AddSeconds(20) };
            _wanderers.Add(w);
            MoveWanderer(w, shops, dialogs, now);
        }
        _wanderShops = shops;
        _wanderDialogs = dialogs;
    }

    private ShopFile _wanderShops = ShopFile.Empty;
    private DialogFile _wanderDialogs = DialogFile.Empty;
    private uint _nextWandererId = Npc.ObjectIdBase + 0x80000;

    private void TickWanderers(DateTime now)
    {
        foreach (var w in _wanderers)
        {
            if (now < w.Due)
                continue;
            w.Due = now.AddMinutes(_random.Next(30, 91));
            if (w.Room is { } room && room.Avatars.Count > 0 && w.Blocked <= 4)
            {
                w.Blocked++;
                continue;
            }
            MoveWanderer(w, _wanderShops, _wanderDialogs, now);
        }
    }

    /// <summary>NextDest: ein anderes Ziel (nur vorhandene Räume) auswürfeln und den NPC dorthin setzen.</summary>
    private void MoveWanderer(Wanderer w, ShopFile shops, DialogFile dialogs, DateTime now)
    {
        var options = Enumerable.Range(0, w.Definition.Destinations.Count)
            .Where(i => i != w.Destination && _rooms.ContainsKey(w.Definition.Destinations[i][0])).ToList();
        if (options.Count == 0)
            return;
        w.Blocked = 0;
        w.Destination = options[_random.Next(options.Count)];
        var dest = w.Definition.Destinations[w.Destination];
        if (w.Room is { } old && w.Npc is { } gone)
        {
            old.Npcs.Remove(gone);
            BroadcastRoom(old, GameReplies.Remove(gone.Id));
        }
        var room = _rooms[dest[0]];
        var def = new NpcDefinition(w.Definition.KodClass, dest[1], dest[2])
        {
            Name = w.Definition.Name, Icon = w.Definition.Icon,
            FineRow = dest.Length > 3 ? dest[3] : 32, FineCol = dest.Length > 4 ? dest[4] : 32,
        };
        var npc = new Npc(w.Npc?.Id ?? ++_nextWandererId, def, dialogs.Npcs.GetValueOrDefault(def.KodClass));
        if (shops.Shops.TryGetValue(def.KodClass, out var shop))
            npc.Merchant = w.Npc?.Merchant ?? new Merchant(shop);
        room.Npcs.Add(npc);
        w.Npc = npc;
        w.Room = room;
        foreach (var a in room.Avatars.Values)
        {
            a.Session.EnsureResource(npc.DisplayName);
            a.Session.EnsureResource(NpcIcon(npc));
            a.Session.Post(GameReplies.Create(ToRoomObject(npc)));
        }
    }
}
