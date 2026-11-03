using Godot;
using Meridian.Protocol;

namespace Meridian.Client;

/// <summary>Bewegliche Teile eines Raums: Hebel per Linksklick betätigen (BP_REQ_ACTIVATE), Hebelstellung (BP_CHANGE)
/// und Sektorbewegungen (BP_SECTOR_MOVE: Tür und Säule im Mausoleum von Raza).</summary>
public partial class Main
{
    /// <summary>Reichweite zum Betätigen (gstlever.kod GUESTLEVER_RANGE 1 Feld, großzügig gerechnet).</summary>
    private const float ActivateReach = 3.5f;

    private bool HandleMechanicsEvent(ClientEvent e)
    {
        switch (e)
        {
            case ClientEvent.ObjectChanged changed:
                if (_avatars.TryGetValue(changed.Object.Id, out var node))
                    PropModels.SetLever(node, changed.Object.Group == 2);
                if (_npcObjects.ContainsKey(changed.Object.Id))
                    _npcObjects[changed.Object.Id] = changed.Object;
                return true;
            case ClientEvent.SectorMoved sector:
                _layout?.SetSector(sector.Sector, sector.Animation, sector.Height);
                if (_roomGeometry is not null)
                    RebuildRoomGeometry();
                return true;
        }
        return false;
    }

    /// <summary>Linksklick auf ein betätigbares Objekt (Hebel); true, wenn eines getroffen wurde.</summary>
    private bool TryActivateAt(Vector2 screen)
    {
        if (_session is not { InWorld: true } || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return false;
        var origin = _camera.ProjectRayOrigin(screen);
        var dir = _camera.ProjectRayNormal(screen);
        foreach (var (id, o) in _npcObjects)
        {
            if (!o.IsActivatable || !_avatars.TryGetValue(id, out var node))
                continue;
            var center = node.Position + new Vector3(0, 0.6f, 0);
            float along = (center - origin).Dot(dir);
            if (along <= 0 || (origin + dir * along).DistanceTo(center) > 0.8f)
                continue;
            if (new Vector2(center.X - self.Position.X, center.Z - self.Position.Z).Length() > ActivateReach)
            {
                _status.Text = "Das ist zu weit weg.";
                return true;
            }
            _ = ActivateAsync(id);
            return true;
        }
        return false;
    }

    private async System.Threading.Tasks.Task ActivateAsync(uint id)
    {
        await SendOwnPosition();   // der Server prüft die Entfernung
        await _session!.ActivateAsync(id);
    }
}
