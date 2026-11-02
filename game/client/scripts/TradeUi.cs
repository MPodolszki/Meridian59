using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Meridian.Protocol;

namespace Meridian.Client;

/// <summary>
/// Inventar, Geld und Handel im Client: I = Inventar, B = beim nächsten Händler kaufen, G = Gegenstand aufheben.
/// Im Inventar: Benutzen (Essen, Waffe/Schild/Rüstung/Fackel an- und ablegen), Fallenlassen oder dem nächsten
/// Händler anbieten (Verkauf per Gegenangebot wie im Original).
/// Bank per Chat: „einzahlen 100“, „abheben 100“, „kontostand“ (oder deposit/withdraw/balance).
/// </summary>
public partial class Main
{
    private readonly HashSet<uint> _floorItems = new();
    private PanelContainer _inventoryPanel = null!;
    private Label _moneyLabel = null!;
    private ItemList _inventoryList = null!;
    private SpinBox _inventoryAmount = null!;
    private List<ClientObject> _inventory = new();

    private PanelContainer _shopPanel = null!;
    private Label _shopTitle = null!;
    private ItemList _shopList = null!;
    private SpinBox _shopAmount = null!;
    private uint _shopSeller;
    private List<BuyOption> _shopOptions = new();

    private PanelContainer _offerPanel = null!;
    private Label _offerText = null!;

    private void BuildTradeUi(CanvasLayer ui)
    {
        // Inventar rechts oben
        _inventoryPanel = new PanelContainer { Position = new Vector2(940, 20), Visible = false };
        var inv = new VBoxContainer { CustomMinimumSize = new Vector2(320, 360) };
        _inventoryPanel.AddChild(inv);
        inv.AddChild(new Label { Text = "Inventar (I)" });
        _moneyLabel = new Label();
        inv.AddChild(_moneyLabel);
        _inventoryList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _inventoryList.ItemSelected += _ => UpdateInventoryAmount();
        inv.AddChild(_inventoryList);
        var invButtons = new HBoxContainer();
        _inventoryAmount = new SpinBox { MinValue = 1, MaxValue = 1, Value = 1, TooltipText = "Menge" };
        invButtons.AddChild(_inventoryAmount);
        var use = new Button { Text = "Benutzen" };
        use.Pressed += UseSelected;
        invButtons.AddChild(use);
        var drop = new Button { Text = "Fallen lassen" };
        drop.Pressed += DropSelected;
        invButtons.AddChild(drop);
        var offer = new Button { Text = "Anbieten" };
        offer.Pressed += OfferSelected;
        invButtons.AddChild(offer);
        inv.AddChild(invButtons);
        ui.AddChild(_inventoryPanel);

        // Ladenliste
        _shopPanel = new PanelContainer { Position = new Vector2(420, 80), Visible = false };
        var shop = new VBoxContainer { CustomMinimumSize = new Vector2(440, 340) };
        _shopPanel.AddChild(shop);
        _shopTitle = new Label();
        shop.AddChild(_shopTitle);
        _shopList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        shop.AddChild(_shopList);
        var shopButtons = new HBoxContainer();
        shopButtons.AddChild(new Label { Text = "Menge" });
        _shopAmount = new SpinBox { MinValue = 1, MaxValue = 500, Value = 1 };
        shopButtons.AddChild(_shopAmount);
        var buy = new Button { Text = "Kaufen" };
        buy.Pressed += BuySelected;
        shopButtons.AddChild(buy);
        var close = new Button { Text = "Schließen" };
        close.Pressed += () => _shopPanel.Visible = false;
        shopButtons.AddChild(close);
        shop.AddChild(shopButtons);
        ui.AddChild(_shopPanel);

        // Gegenangebot eines Händlers
        _offerPanel = new PanelContainer { Position = new Vector2(420, 260), Visible = false };
        var offerBox = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0) };
        _offerPanel.AddChild(offerBox);
        _offerText = new Label { AutowrapMode = TextServer.AutowrapMode.Word };
        offerBox.AddChild(_offerText);
        var offerButtons = new HBoxContainer();
        var accept = new Button { Text = "Annehmen" };
        accept.Pressed += async () =>
        {
            _offerPanel.Visible = false;
            if (_session is not null)
                await _session.AcceptOfferAsync();
        };
        offerButtons.AddChild(accept);
        var decline = new Button { Text = "Ablehnen" };
        decline.Pressed += async () =>
        {
            _offerPanel.Visible = false;
            if (_session is not null)
                await _session.CancelOfferAsync();
        };
        offerButtons.AddChild(decline);
        offerBox.AddChild(offerButtons);
        ui.AddChild(_offerPanel);
    }

    /// <summary>Handels-Ereignisse aus der Session; false = nicht zuständig.</summary>
    private bool HandleTradeEvent(ClientEvent e)
    {
        switch (e)
        {
            case ClientEvent.InventoryChanged inv:
                _inventory = inv.Items.ToList();
                RefreshInventory();
                UpdateTorchLight();
                return true;
            case ClientEvent.BuyList list:
                _shopSeller = list.Seller.Id;
                _shopOptions = list.Items.ToList();
                _shopTitle.Text = $"Waren von {_session!.Resolve(list.Seller.NameRsc)}";
                _shopList.Clear();
                foreach (var o in _shopOptions)
                    _shopList.AddItem($"{_session.Describe(o.Item)} – {o.Price} Schillinge{(o.Item.Amount is not null ? " pro Stück" : "")}");
                _shopPanel.Visible = true;
                return true;
            case ClientEvent.CounterOffer counter:
                _offerText.Text = $"Dafür bekommst du: {string.Join(", ", counter.Items.Select(_session!.Describe))}.";
                _offerPanel.Visible = true;
                return true;
            case ClientEvent.Offered:
                return true;
            case ClientEvent.OfferCanceled:
                _offerPanel.Visible = false;
                return true;
        }
        return false;
    }

    private void RefreshInventory()
    {
        if (_session is null)
            return;
        int? selected = _inventoryList.GetSelectedItems() is { Length: > 0 } sel ? sel[0] : null;
        _inventoryList.Clear();
        var money = _inventory.FirstOrDefault(o => o.Icon == "coin.bgf");
        _moneyLabel.Text = $"Geld: {money?.Amount ?? 0} Schillinge";
        foreach (var o in _inventory)
            _inventoryList.AddItem(_session.Describe(o) + (o.Used ? "  (benutzt)" : ""));
        if (selected is { } s && s < _inventory.Count)
            _inventoryList.Select(s);
        UpdateInventoryAmount();
    }

    private ClientObject? SelectedInventoryItem =>
        _inventoryList.GetSelectedItems() is { Length: > 0 } sel && sel[0] < _inventory.Count ? _inventory[sel[0]] : null;

    private void UpdateInventoryAmount()
    {
        var item = SelectedInventoryItem;
        _inventoryAmount.MaxValue = Math.Max(1, item?.Amount ?? 1);
        _inventoryAmount.Editable = item?.Amount is not null;
        _inventoryAmount.Value = item?.Amount is { } n ? Math.Min(_inventoryAmount.Value, n) : 1;
    }

    private ObjectRef? SelectedRef() =>
        SelectedInventoryItem is { } item ? new ObjectRef(item.Id, item.Amount is null ? null : (uint)_inventoryAmount.Value) : null;

    /// <summary>Benutzen bzw. bei benutzten Gegenständen wieder ablegen.</summary>
    private async void UseSelected()
    {
        if (_session is not { InWorld: true } || SelectedInventoryItem is not { } item)
            return;
        if (item.Used)
            await _session.UnuseAsync(item.Id);
        else
            await _session.UseAsync(item.Id);
    }

    private OmniLight3D? _torchLight;

    /// <summary>Benutzte Fackel leuchtet am eigenen Zwerg (KOD Fackel: LIGHT_FIRE).</summary>
    private void UpdateTorchLight()
    {
        if (_session is null || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;
        bool lit = _inventory.Any(o => o.Used && o.Icon == "torch.bgf");
        if (lit && !IsInstanceValid(_torchLight))
        {
            _torchLight = new OmniLight3D { LightColor = new Color(1f, 0.65f, 0.3f), LightEnergy = 2.5f, OmniRange = 9f, Position = new Vector3(0.35f, 1.4f, 0) };
            self.AddChild(_torchLight);
        }
        else if (!lit && IsInstanceValid(_torchLight))
        {
            _torchLight!.QueueFree();
            _torchLight = null;
        }
    }

    private async void DropSelected()
    {
        if (_session is { InWorld: true } && SelectedRef() is { } r)
        {
            await SendOwnPosition();
            await _session.DropAsync([r]);
        }
    }

    private async void OfferSelected()
    {
        if (_session is not { InWorld: true } || SelectedRef() is not { } r)
            return;
        uint npc = NearestNpc(o => o.IsOfferable);
        if (npc == 0)
        {
            _status.Text = "Niemand in der Nähe, der etwas annimmt.";
            return;
        }
        await _session.OfferAsync(npc, [r]);
    }

    private async void BuySelected()
    {
        if (_session is not { InWorld: true } || _shopList.GetSelectedItems() is not { Length: > 0 } sel)
            return;
        var option = _shopOptions[sel[0]];
        await _session.BuyItemsAsync(_shopSeller, [new ObjectRef(option.Item.Id, option.Item.Amount is null ? null : (uint)_shopAmount.Value)]);
    }

    /// <summary>B: Ladenliste des nächsten Händlers.</summary>
    private async void OpenShop()
    {
        if (_session is not { InWorld: true })
            return;
        uint npc = NearestNpc(o => o.IsSeller);
        if (npc == 0)
            _status.Text = "Kein Händler in der Nähe.";
        else
            await _session.RequestBuyAsync(npc);
    }

    /// <summary>G: nächsten Gegenstand auf dem Boden aufheben.</summary>
    private async void PickUpNearest()
    {
        if (_session is not { InWorld: true } || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;
        uint best = 0;
        float bestDistance = 5f;   // Server erlaubt 2 Felder = 5 m
        foreach (uint id in _floorItems)
        {
            if (_avatars.TryGetValue(id, out var item) && item.Position.DistanceTo(self.Position) < bestDistance)
            {
                best = id;
                bestDistance = item.Position.DistanceTo(self.Position);
            }
        }
        if (best == 0)
        {
            _status.Text = "Hier liegt nichts.";
            return;
        }
        await SendOwnPosition();
        await _session.GetAsync(best);
    }

    private readonly Dictionary<uint, ClientObject> _npcObjects = new();

    private uint NearestNpc(Func<ClientObject, bool> filter)
    {
        if (_session is null || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return 0;
        uint best = 0;
        float bestDistance = 6f;
        foreach (uint id in _npcs)
        {
            if (_npcObjects.TryGetValue(id, out var o) && filter(o)
                && _avatars.TryGetValue(id, out var npc) && npc.Position.DistanceTo(self.Position) < bestDistance)
            {
                best = id;
                bestDistance = npc.Position.DistanceTo(self.Position);
            }
        }
        return best;
    }

    private async System.Threading.Tasks.Task SendOwnPosition()
    {
        if (_session is null || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;
        _lastMoveSent = DateTime.UtcNow;
        _lastSentPos = self.Position;
        var (row, col) = ToKod(self.Position);
        await _session.MoveAsync(row, col, running: false);
    }

    /// <summary>Bankbefehle im Chat wie die Befehle des Original-Clients (deposit/withdraw/balance).</summary>
    private async System.Threading.Tasks.Task<bool> TryBankCommand(string text)
    {
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || _session is null)
            return false;
        string cmd = parts[0].ToLowerInvariant();
        uint amount = parts.Length > 1 && uint.TryParse(parts[1], out var a) ? a : 0;
        switch (cmd)
        {
            case "einzahlen" or "deposit" when parts.Length > 1:
                await _session.DepositAsync(amount);
                return true;
            case "abheben" or "withdraw" when parts.Length > 1:
                await _session.WithdrawAsync(amount);
                return true;
            case "kontostand" or "balance" when parts.Length == 1:
                await _session.BalanceAsync();
                return true;
        }
        return false;
    }

    /// <summary>Gegenstand auf dem Boden: kleiner Würfel bzw. Münzstapel mit Namen.</summary>
    private Node3D CreateFloorItem(ClientObject o)
    {
        var root = new Node3D();
        bool coins = o.Icon == "coin.bgf";
        root.AddChild(new MeshInstance3D
        {
            Mesh = coins ? new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.15f, Height = 0.08f } : new BoxMesh { Size = new Vector3(0.3f, 0.2f, 0.3f) },
            Position = new Vector3(0, coins ? 0.04f : 0.1f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = coins ? new Color(0.85f, 0.85f, 0.9f) : new Color(0.55f, 0.4f, 0.25f),
                Metallic = coins ? 0.8f : 0f,
            },
        });
        root.AddChild(new Label3D
        {
            Text = _session!.Describe(o),
            Modulate = new Color(0.7f, 0.9f, 1f),
            Position = new Vector3(0, 0.5f, 0),
            FontSize = 20,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        });
        return root;
    }
}
