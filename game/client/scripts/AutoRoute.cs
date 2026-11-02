using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Meridian.Client;

/// <summary>
/// Test-Automatik „--route Raum1,Raum2,…“: läuft wie ein Spieler durch die Räume – Weg per Breitensuche auf dem
/// Raster (dieselben Stufenregeln wie die Bewegung), Lenken über die normale Eingabe, Türen per Linksklick auf den
/// Bildschirmpunkt der Tür. Meldet Fortschritt und Hängenbleiben auf der Konsole, macht am Ende ein Bild.
/// </summary>
public partial class Main
{
    private List<string>? _route;
    private int _routeStep;
    private List<Vector3>? _waypoints;
    private Vector2 _routeInput;
    private double _routeWait, _stuckTime, _routeTotal, _routeShot;
    private Vector3 _lastRoutePos;
    private string? _routeRoom;

    /// <summary>Lenkt die Figur; liefert die Eingabe (seitlich, vor) für <see cref="ReadMovement"/>.</summary>
    private void UpdateAutoRoute(double delta)
    {
        _routeInput = Vector2.Zero;
        if (_route is null || _session is not { InWorld: true } || _layout is null
            || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;
        _routeTotal += delta;
        if (_routeTotal > 180)
        {
            FinishRoute($"ROUTE ABGEBROCHEN nach 180 s in {_routeRoom} auf Feld {Cell(self)}");
            return;
        }
        if (_routeStep >= _route.Count)
        {
            if ((_routeShot += delta) > 2)
                FinishRoute($"ROUTE FERTIG: {_routeRoom} erreicht nach {_routeTotal:0} s");
            return;
        }
        if ((_routeWait -= delta) > 0)
            return;
        string target = _route[_routeStep];
        var exit = _layout.Exits.Where(e => e.Target == target).ToList();
        if (exit.Count == 0)
        {
            FinishRoute($"ROUTE: kein Ausgang nach „{target}“ in {_routeRoom}");
            return;
        }
        var (row, col) = CellAt(self.Position);
        if (_waypoints is null)
        {
            _waypoints = FindRoute(row, col, exit.Select(e => (e.Row, e.Col)).ToHashSet());
            if (_waypoints is null)
            {
                FinishRoute($"ROUTE: kein Weg von Feld {Cell(self)} zum Ausgang nach „{target}“ in {_routeRoom}");
                return;
            }
            GD.Print($"Route in {_routeRoom}: {_waypoints.Count} Felder bis zum Ausgang nach „{target}“");
            _lastRoutePos = self.Position;
            _stuckTime = 0;
        }
        // Am Ausgang: Tür anklicken wie ein Spieler (Bildschirmpunkt der Tür).
        var door = exit.OrderBy(e => new Vector2(e.Col - col, e.Row - row).LengthSquared()).First();
        var doorPos = new Vector3((door.Col + 1.5f) * CellSize, 0, (door.Row + 1.5f) * CellSize);
        doorPos.Y = GroundHeightAt(doorPos) + 1.2f;
        if (_waypoints.Count == 0 || new Vector2(doorPos.X - self.Position.X, doorPos.Z - self.Position.Z).Length() < DoorReach - 1.5f)
        {
            _yaw = Mathf.Atan2(-(doorPos.X - self.Position.X), -(doorPos.Z - self.Position.Z));
            if (_camera.IsPositionBehind(doorPos))
                return;
            var screen = _camera.UnprojectPosition(doorPos);
            GD.Print($"Klick auf die Tür nach „{target}“ bei Feld ({door.Row + 1},{door.Col + 1}), Bildschirm {screen}");
            TryOpenDoorAt(screen);
            _routeWait = 2;
            return;
        }
        // Zum nächsten Wegpunkt lenken.
        var next = _waypoints[0];
        var to = new Vector2(next.X - self.Position.X, next.Z - self.Position.Z);
        if (to.Length() < 0.8f)
        {
            _waypoints.RemoveAt(0);
            return;
        }
        _yaw = Mathf.Atan2(-to.X, -to.Y);
        _routeInput = new Vector2(0, -1);
        // Hängenbleiben erkennen.
        if (self.Position.DistanceTo(_lastRoutePos) > 0.5f)
        {
            _lastRoutePos = self.Position;
            _stuckTime = 0;
        }
        else if ((_stuckTime += delta) > 3)
        {
            GD.Print($"HÄNGT in {_routeRoom} auf Feld {Cell(self)} (Ziel Feld {CellAt(next).Row + 1},{CellAt(next).Col + 1})");
            if (_screenshotPath is not null)
                GetViewport().GetTexture().GetImage().SavePng(_screenshotPath.Replace(".png", $"-haengt{_routeStep}.png"));
            _waypoints = null;   // neu planen
            _stuckTime = -5;
        }
    }

    private string Cell(Node3D self)
    {
        var (r, c) = CellAt(self.Position);
        return $"({r + 1},{c + 1}) Höhe {_layout!.FloorAt(r, c)}";
    }

    /// <summary>Breitensuche über Felder (8 Richtungen) mit denselben Regeln wie die Bewegung.</summary>
    private List<Vector3>? FindRoute(int row, int col, HashSet<(int, int)> goals)
    {
        var from = new Dictionary<(int, int), (int, int)>();
        var queue = new Queue<(int, int)>();
        queue.Enqueue((row, col));
        from[(row, col)] = (row, col);
        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            if (goals.Contains((r, c)))
            {
                var path = new List<Vector3>();
                for (var p = (r, c); p != (row, col); p = from[p])
                    path.Add(new Vector3((p.Item2 + 1.5f) * CellSize, 0, (p.Item1 + 1.5f) * CellSize));
                path.Reverse();
                return path;
            }
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    var n = (r + dr, c + dc);
                    if ((dr == 0 && dc == 0) || from.ContainsKey(n) || !_layout!.CanStep(r, c, n.Item1, n.Item2))
                        continue;
                    from[n] = (r, c);
                    queue.Enqueue(n);
                }
        }
        return null;
    }

    private void RouteEnteredRoom(string room)
    {
        _routeRoom = room;
        if (_route is null)
            return;
        GD.Print($"Raum: {room}");
        if (_routeStep < _route.Count && room == _route[_routeStep])
            _routeStep++;
        _waypoints = null;
        _routeWait = 1.5;
    }

    private void FinishRoute(string message)
    {
        GD.Print(message);
        if (_screenshotPath is not null)
            GetViewport().GetTexture().GetImage().SavePng(_screenshotPath);
        _route = null;
        GetTree().Quit();
    }
}
