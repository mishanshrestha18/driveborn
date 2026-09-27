using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Driveborn.Core.Model;
using Driveborn.Core.Rifts;
using Driveborn.Core.Run;

namespace Driveborn.App;

public partial class MainWindow : Window
{
    private const double Cell = 34;

    private readonly GameSession _session = new();
    private Entity? _selected;
    private IReadOnlyList<Rift> _rifts = Array.Empty<Rift>();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshTerritoryList();
        if (_session.HasTerritories) ShowHub();
        else ShowSetup();
        Focus();
    }

    // ================= view switching =================

    private void ShowSetup()
    {
        SetupView.Visibility = Visibility.Visible;
        HubView.Visibility = Visibility.Collapsed;
        DescentView.Visibility = Visibility.Collapsed;
        SummaryOverlay.Visibility = Visibility.Collapsed;
        BtnEnterWorld.IsEnabled = _session.HasTerritories;
        RefreshHeader();
    }

    private void ShowHub()
    {
        SetupView.Visibility = Visibility.Collapsed;
        HubView.Visibility = Visibility.Visible;
        DescentView.Visibility = Visibility.Collapsed;
        SummaryOverlay.Visibility = Visibility.Collapsed;
        RefreshHub();
        RefreshHeader();
        Focus();
    }

    private void ShowDescent()
    {
        SetupView.Visibility = Visibility.Collapsed;
        HubView.Visibility = Visibility.Collapsed;
        DescentView.Visibility = Visibility.Visible;
        SummaryOverlay.Visibility = Visibility.Collapsed;
        RefreshAll();
        Focus();
    }

    private void RefreshHeader()
    {
        var save = _session.Save;
        HeaderProfile.Text = $"Reader lv{save.Level}  ·  {save.Shards} shards  ·  floor record {save.DeepestFloor}";

        if (save.StreakDays > 0)
        {
            HeaderStreak.Text = save.StreakAtRisk(DateTime.Now)
                ? $"streak {save.StreakDays}d — at risk today"
                : $"streak {save.StreakDays}d";
        }
        else
        {
            HeaderStreak.Text = "no streak yet";
        }
    }

    // ================= setup =================

    private void RefreshTerritoryList()
    {
        TerritoryList.Items.Clear();
        foreach (var t in _session.Policy.Territories) TerritoryList.Items.Add(t);
        BtnEnterWorld.IsEnabled = _session.HasTerritories;
    }

    private void BtnAddTerritory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Pick a folder to turn into a territory",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true) return;

        if (_session.AddTerritory(dialog.FolderName, out var reason))
        {
            SetupNote.Text = string.Empty;
            RefreshTerritoryList();
        }
        else
        {
            SetupNote.Text = reason ?? "That folder cannot be used.";
        }
    }

    private void BtnRemoveTerritory_Click(object sender, RoutedEventArgs e)
    {
        if (TerritoryList.SelectedItem is not string path) return;
        _session.RemoveTerritory(path);
        RefreshTerritoryList();
    }

    private void BtnEnterWorld_Click(object sender, RoutedEventArgs e)
    {
        if (!_session.HasTerritories)
        {
            SetupNote.Text = "Add at least one folder first.";
            return;
        }
        ShowHub();
    }

    private void BtnManage_Click(object sender, RoutedEventArgs e)
    {
        RefreshTerritoryList();
        ShowSetup();
    }

    // ================= hub =================

    private void RefreshHub()
    {
        var save = _session.Save;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            _rifts = _session.Rifts.Discover();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        var daily = _rifts.FirstOrDefault(r => r.IsDaily);
        if (daily is null)
        {
            RiftHeadline.Text = "No Rift today";
            RiftDetail.Text = "Nothing in your territories changed in the last 24 hours. " +
                              "Work, download or save something and the world will open a Rift there.";
            BtnEnterRift.IsEnabled = false;
        }
        else
        {
            var claimed = _session.RiftAlreadyClaimed(daily);
            RiftHeadline.Text = claimed ? $"Rift: {daily.Name} (claimed)" : $"Rift: {daily.Name}";
            RiftDetail.Text =
                $"{daily.FolderPath}{Environment.NewLine}" +
                $"{daily.EntryCount} entries · floor {daily.Depth} · touched {GameSession.FormatAge(daily.LastWriteUtc)}" +
                $"{Environment.NewLine}Loot is worth double here. It closes at midnight.";
            BtnEnterRift.IsEnabled = true;
            BtnEnterRift.Tag = daily.FolderPath;
        }

        EntranceList.Items.Clear();
        foreach (var t in _session.Policy.Territories)
        {
            var riftsInside = _rifts.Count(r =>
                r.FolderPath.StartsWith(t, StringComparison.OrdinalIgnoreCase));
            var explored = save.ExploredFolders.Count(f =>
                f.StartsWith(t, StringComparison.OrdinalIgnoreCase));

            EntranceList.Items.Add(new ListBoxItem
            {
                Content = $"{t}    [{explored} mapped · {riftsInside} active rifts]",
                Tag = t,
                Padding = new Thickness(6, 5, 6, 5)
            });
        }

        HubStats.Text = string.Join(Environment.NewLine, new[]
        {
            $"Level        {save.Level}  ({save.Xp} xp)",
            $"Integrity    {save.MaxIntegrity}",
            $"Cache slots  {save.CacheSlots}",
            $"Shards       {save.Shards}",
            $"Parsers      {string.Join(", ", save.Parsers.Select(p => $"{p.Name} lv{p.Level}"))}"
        });

        HubRecords.Text = string.Join(Environment.NewLine, new[]
        {
            $"Deepest floor      {save.DeepestFloor}",
            $"Runs / extractions {save.TotalRuns} / {save.SuccessfulExtractions}",
            $"Entities felled    {save.TotalFelled}",
            $"Best streak        {save.BestStreakDays}d",
            $"Largest felled     {(save.LargestFelledName is null ? "-" : save.LargestFelledName + " (" + GameSession.FormatBytes(save.LargestFelledBytes) + ")")}"
        });

        CollectionList.Items.Clear();
        foreach (var relic in save.Collection.OrderByDescending(r => r.Value).Take(120))
        {
            var mark = relic.FromRift ? "R" : " ";
            CollectionList.Items.Add(
                $"[{mark}] {relic.Value,5}  {relic.Name}  ({GameSession.FormatBytes(relic.SizeBytes)})");
        }

        RefreshHeader();
    }

    private void BtnRescan_Click(object sender, RoutedEventArgs e)
    {
        _session.Scanner.Invalidate();
        RefreshHub();
    }

    private void BtnEnterRift_Click(object sender, RoutedEventArgs e)
    {
        if (BtnEnterRift.Tag is not string path) return;

        var daily = _rifts.FirstOrDefault(r => r.IsDaily);
        if (daily is not null) _session.ClaimRift(daily);

        // Rifts are entered from the territory root that owns them, so the walk
        // back out - and the decision to cash in - still has to be earned.
        var root = _session.Policy.Territories
            .FirstOrDefault(t => path.StartsWith(t, StringComparison.OrdinalIgnoreCase)) ?? path;

        StartDescent(root);
    }

    private void EntranceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntranceList.SelectedItem is ListBoxItem { Tag: string root }) StartDescent(root);
    }

    private void StartDescent(string root)
    {
        var state = _session.BeginDescent(root);
        if (state is null)
        {
            MessageBox.Show(this, "That territory cannot be read right now.", "Driveborn",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _selected = null;
        ShowDescent();
    }

    // ================= descent =================

    private void RefreshAll()
    {
        if (_session.Run is not { } run) return;

        RenderRoom();
        RenderHud();

        RoomTitle.Text = run.CurrentRoom is { } room
            ? (room.IsRift ? $"RIFT · {room.Title}" : room.Title)
            : "-";

        RoomMeta.Text = run.CurrentRoom is { } r2
            ? $"floor {r2.Depth} · turn {run.Turn} · {r2.Entities.Count(x => x.IsAlive && x.IsHostile)} hostile"
            : string.Empty;

        LogText.Text = string.Join(Environment.NewLine, run.Log.TakeLast(60));
        LogScroll.ScrollToEnd();

        BtnExtract.IsEnabled = run.CanExtractHere;

        RefreshDoors();

        if (run.IsOver) ShowSummary(run);
    }

    private void RenderHud()
    {
        if (_session.Run is not { } run) return;
        var p = run.Player;

        var ratio = Math.Clamp(p.Integrity / (double)p.MaxIntegrity, 0, 1);
        IntegrityBar.Width = 244 * ratio;
        IntegrityBar.Background = ratio switch
        {
            > 0.6 => (Brush)FindResource("Cyan"),
            > 0.3 => (Brush)FindResource("Amber"),
            _ => (Brush)FindResource("Ember")
        };
        IntegrityText.Text = $"{p.Integrity} / {p.MaxIntegrity}";

        CyclePips.Children.Clear();
        for (var i = 0; i < p.MaxCycles; i++)
        {
            CyclePips.Children.Add(new Border
            {
                Width = 26,
                Height = 10,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 5, 0),
                Background = i < p.Cycles
                    ? (Brush)FindResource("Cyan")
                    : new SolidColorBrush(Color.FromRgb(0x1A, 0x22, 0x2C))
            });
        }

        ParserPanel.Children.Clear();
        for (var i = 0; i < p.Parsers.Count; i++)
        {
            var parser = p.Parsers[i];
            var matches = _selected is not null && parser.Matches(_selected.Class);

            ParserPanel.Children.Add(new TextBlock
            {
                Text = $"{i + 1}  {parser.Name} lv{parser.Level}{(matches ? "   MATCH" : string.Empty)}",
                FontFamily = (FontFamily)FindResource("Mono"),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 3),
                Foreground = matches ? (Brush)FindResource("Cyan") : (Brush)FindResource("Ink")
            });
        }

        CacheText.Text = $"{p.Carried.Count} / {p.CacheSlots} · worth {run.CarriedValue}";
    }

    private void RefreshDoors()
    {
        DoorList.Items.Clear();
        if (_session.Run?.CurrentRoom is not { } room) return;

        foreach (var door in room.Doors())
        {
            DoorList.Items.Add(new ListBoxItem
            {
                Content = $"{door.LinkLabel}   [{door.LinkEntryCount} entries]",
                Tag = door.LinkPath,
                Padding = new Thickness(4, 4, 4, 4)
            });
        }

        if (room.ParentPath is not null)
        {
            DoorList.Items.Add(new ListBoxItem
            {
                Content = ".. climb toward the surface",
                Tag = room.ParentPath,
                Padding = new Thickness(4, 4, 4, 4)
            });
        }
    }

    private void DoorList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DoorList.SelectedItem is not ListBoxItem { Tag: string path }) return;
        if (_session.Run is not { CurrentRoom: not null }) return;

        // Walking a door from the list is the same act as stepping onto it.
        var room = _session.Run.CurrentRoom!;
        for (var y = 0; y < room.Height; y++)
            for (var x = 0; x < room.Width; x++)
            {
                var tile = room.Tiles[x, y];
                if (tile.LinkPath != path) continue;

                var dx = Math.Sign(x - _session.Run.Player.Position.X);
                var dy = Math.Sign(y - _session.Run.Player.Position.Y);
                _session.Run.Player.Position = new GridPoint(
                    Math.Clamp(x - dx, 1, room.Width - 2),
                    Math.Clamp(y - dy, 1, room.Height - 2));

                Act(_session.Engine.Move(dx, dy));
                return;
            }
    }

    // ================= board rendering =================

    private void RenderRoom()
    {
        BoardCanvas.Children.Clear();
        if (_session.Run?.CurrentRoom is not { } room) return;

        BoardCanvas.Width = room.Width * Cell;
        BoardCanvas.Height = room.Height * Cell;

        for (var y = 0; y < room.Height; y++)
            for (var x = 0; x < room.Width; x++)
                DrawTile(room, x, y);

        foreach (var entity in room.Entities.Where(e => e.IsAlive && !e.Hidden))
            DrawEntity(entity);

        DrawReader(_session.Run.Player.Position);
    }

    private void DrawTile(Room room, int x, int y)
    {
        var tile = room.Tiles[x, y];

        var fill = tile.Kind switch
        {
            TileKind.Wall => Color.FromRgb(0x0B, 0x0F, 0x16),
            TileKind.DoorDown => Color.FromRgb(0x3A, 0x2A, 0x10),
            TileKind.StairsUp => Color.FromRgb(0x10, 0x2E, 0x2C),
            _ => Color.FromRgb(0x10, 0x15, 0x1E)
        };

        var rect = new Rectangle
        {
            Width = Cell - 1.5,
            Height = Cell - 1.5,
            RadiusX = 2,
            RadiusY = 2,
            Fill = new SolidColorBrush(fill),
            Stroke = tile.Kind switch
            {
                TileKind.DoorDown => (Brush)FindResource("Amber"),
                TileKind.StairsUp => (Brush)FindResource("Cyan"),
                _ => new SolidColorBrush(Color.FromRgb(0x15, 0x1B, 0x25))
            },
            StrokeThickness = tile.Kind is TileKind.DoorDown or TileKind.StairsUp ? 1.2 : 0.6
        };

        Canvas.SetLeft(rect, x * Cell);
        Canvas.SetTop(rect, y * Cell);
        BoardCanvas.Children.Add(rect);

        if (tile.Kind is TileKind.DoorDown or TileKind.StairsUp)
        {
            var label = new TextBlock
            {
                Text = tile.Kind == TileKind.StairsUp ? "^" : "+",
                FontFamily = (FontFamily)FindResource("Mono"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = tile.Kind == TileKind.StairsUp
                    ? (Brush)FindResource("Cyan")
                    : (Brush)FindResource("Amber"),
                ToolTip = tile.LinkLabel is null
                    ? null
                    : $"{tile.LinkLabel}  ({tile.LinkEntryCount} entries)"
            };
            Canvas.SetLeft(label, x * Cell + 11);
            Canvas.SetTop(label, y * Cell + 5);
            BoardCanvas.Children.Add(label);
        }
    }

    private void DrawEntity(Entity entity)
    {
        var size = entity.Footprint * Cell - 4;

        var body = new Rectangle
        {
            Width = size,
            Height = size,
            RadiusX = 3,
            RadiusY = 3,
            Fill = new SolidColorBrush(FillFor(entity)),
            Stroke = StrokeFor(entity),
            StrokeThickness = entity == _selected ? 2.4 : 1.2,
            Tag = entity,
            ToolTip = DescribeShort(entity)
        };

        body.MouseLeftButtonDown += (_, _) => SelectEntity(entity);
        body.MouseEnter += (_, _) => InspectorText.Text = Describe(entity);

        Canvas.SetLeft(body, entity.Position.X * Cell + 2);
        Canvas.SetTop(body, entity.Position.Y * Cell + 2);
        BoardCanvas.Children.Add(body);

        var glyph = new TextBlock
        {
            Text = GlyphFor(entity),
            FontFamily = (FontFamily)FindResource("Mono"),
            FontSize = Math.Min(20, 11 + entity.Footprint * 3),
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xEE, 0xFA)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(glyph, entity.Position.X * Cell + size / 2 - 5);
        Canvas.SetTop(glyph, entity.Position.Y * Cell + size / 2 - 12);
        BoardCanvas.Children.Add(glyph);

        // Health strip along the bottom of the body.
        var ratio = Math.Clamp(entity.Hp / (double)entity.MaxHp, 0, 1);
        var bar = new Rectangle
        {
            Width = Math.Max(2, size * ratio),
            Height = 3,
            Fill = ratio > 0.5
                ? (Brush)FindResource("Cyan")
                : ratio > 0.2 ? (Brush)FindResource("Amber") : (Brush)FindResource("Ember"),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(bar, entity.Position.X * Cell + 2);
        Canvas.SetTop(bar, entity.Position.Y * Cell + size - 1);
        BoardCanvas.Children.Add(bar);
    }

    private void DrawReader(GridPoint at)
    {
        var dot = new Ellipse
        {
            Width = Cell - 12,
            Height = Cell - 12,
            Fill = (Brush)FindResource("Cyan"),
            Stroke = Brushes.White,
            StrokeThickness = 1.4,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(dot, at.X * Cell + 6);
        Canvas.SetTop(dot, at.Y * Cell + 6);
        BoardCanvas.Children.Add(dot);
    }

    private static Color FillFor(Entity e) => e.Kind switch
    {
        EntityKind.Treasure => Color.FromRgb(0x4A, 0x39, 0x10),
        EntityKind.Vault => Color.FromRgb(0x2C, 0x2C, 0x38),
        EntityKind.Boss => Color.FromRgb(0x4A, 0x14, 0x22),
        EntityKind.Undead => Color.FromRgb(0x20, 0x2A, 0x3C),
        EntityKind.Swarm => Color.FromRgb(0x2A, 0x24, 0x34),
        _ => Color.FromRgb(0x23, 0x2B, 0x38)
    };

    private Brush StrokeFor(Entity e) => e.Temperament switch
    {
        Temperament.Hot => (Brush)FindResource("Ember"),
        Temperament.Dormant => (Brush)FindResource("Violet"),
        _ => (Brush)FindResource("Muted")
    };

    private static string GlyphFor(Entity e) => e.Kind switch
    {
        EntityKind.Treasure => "$",
        EntityKind.Vault => "#",
        EntityKind.Boss => "W",
        EntityKind.Swarm => "*",
        EntityKind.Undead => "u",
        EntityKind.Scribe => "s",
        EntityKind.Wraith => "?",
        _ => "O"
    };

    private static string DescribeShort(Entity e) =>
        $"{e.Name} · {e.Hp}/{e.MaxHp} hp · armour {e.Armour} · {e.Temperament}";

    private static string Describe(Entity e)
    {
        var sb = new StringBuilder();
        sb.AppendLine(e.Name);
        sb.AppendLine();
        sb.AppendLine($"kind        {e.Kind}");
        sb.AppendLine($"family      {e.Class}   (counter: {ParserTool.DisplayFor(e.Class)})");
        sb.AppendLine($"health      {e.Hp} / {e.MaxHp}");
        sb.AppendLine($"armour      {e.Armour}");
        sb.AppendLine($"power       {e.Power}");
        sb.AppendLine($"footprint   {e.Footprint}x{e.Footprint}");
        sb.AppendLine($"read cost   {e.ReadCost} cycles");
        sb.AppendLine($"state       {e.Temperament}{(e.Awake ? string.Empty : " (asleep)")}");
        if (e.Stack > 1) sb.AppendLine($"stack       {e.Stack} bodies");
        sb.AppendLine();
        sb.AppendLine("-- the real file --");
        sb.AppendLine(e.SourcePath);
        sb.AppendLine($"{GameSession.FormatBytes(e.SizeBytes)} · {GameSession.FormatAge(e.LastWriteUtc)}");

        if (!string.IsNullOrWhiteSpace(e.Lore))
        {
            sb.AppendLine();
            sb.AppendLine("-- inscription --");
            sb.AppendLine(e.Lore);
        }

        return sb.ToString();
    }

    private void SelectEntity(Entity entity)
    {
        _selected = entity;
        InspectorText.Text = Describe(entity);
        RenderRoom();
        RenderHud();
    }

    // ================= input =================

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DescentView.Visibility != Visibility.Visible) return;
        if (_session.Run is not { } run || run.IsOver) return;

        switch (e.Key)
        {
            case Key.W or Key.Up: Act(_session.Engine.Move(0, -1)); break;
            case Key.S or Key.Down: Act(_session.Engine.Move(0, 1)); break;
            case Key.A or Key.Left: Act(_session.Engine.Move(-1, 0)); break;
            case Key.D or Key.Right: Act(_session.Engine.Move(1, 0)); break;

            case Key.D1: StrikeWith(0); break;
            case Key.D2: StrikeWith(1); break;
            case Key.D3: StrikeWith(2); break;

            case Key.E:
                if (_selected is not null) Act(_session.Engine.Open(_selected));
                break;

            case Key.Space: Act(_session.Engine.EndTurn()); break;
            case Key.X: Act(_session.Engine.Extract()); break;
            case Key.Tab: CycleTarget(); e.Handled = true; break;
        }
    }

    private void StrikeWith(int slot)
    {
        if (_selected is null)
        {
            CycleTarget();
            if (_selected is null) return;
        }
        Act(_session.Engine.Strike(slot, _selected!));
    }

    /// <summary>Selects the nearest live hostile, then walks through the rest.</summary>
    private void CycleTarget()
    {
        if (_session.Run?.CurrentRoom is not { } room) return;

        var candidates = room.Entities
            .Where(x => x.IsAlive && !x.Hidden)
            .OrderBy(x => RunEngine.DistanceTo(_session.Run.Player.Position, x))
            .ToList();

        if (candidates.Count == 0) return;

        var index = _selected is null ? 0 : (candidates.IndexOf(_selected) + 1) % candidates.Count;
        SelectEntity(candidates[index]);
    }

    private void Act(ActionResult result)
    {
        if (!result.Ok && !string.IsNullOrWhiteSpace(result.Message))
            _session.Run?.Say(result.Message);

        if (_selected is { IsAlive: false }) _selected = null;

        RefreshAll();
    }

    private void BtnEndTurn_Click(object sender, RoutedEventArgs e) => Act(_session.Engine.EndTurn());

    private void BtnExtract_Click(object sender, RoutedEventArgs e) => Act(_session.Engine.Extract());

    private void BtnAbandon_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(this,
            "Abandon the descent? Everything you are carrying is lost. XP and mapping are kept.",
            "Abandon run", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes) Act(_session.Engine.Abandon());
    }

    private void BtnHub_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Run is { IsOver: false })
        {
            var confirm = MessageBox.Show(this,
                "A descent is still running. Leaving abandons it and loses the haul. Continue?",
                "Leave descent", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;
            _session.Engine.Abandon();
            _session.ConcludeRun();
        }

        ShowHub();
    }

    // ================= summary =================

    private void ShowSummary(RunState run)
    {
        _session.ConcludeRun();

        SummaryTitle.Text = run.Outcome switch
        {
            RunOutcome.Extracted => "Extracted",
            RunOutcome.Died => "The Reader fell",
            _ => "Descent abandoned"
        };

        SummaryTitle.Foreground = run.Outcome == RunOutcome.Extracted
            ? (Brush)FindResource("Cyan")
            : (Brush)FindResource("Ember");

        var lines = new List<string>
        {
            $"Floors reached     {run.DeepestFloor}",
            $"Rooms cleared      {run.RoomsCleared}",
            $"Entities felled    {run.EntitiesFelled}",
            $"Turns              {run.Turn}",
            string.Empty
        };

        lines.Add(run.Outcome == RunOutcome.Extracted
            ? $"Banked {run.Player.Carried.Count} relics. Shards now {_session.Save.Shards}."
            : "The haul is gone. XP and everything you mapped are kept.");

        if (run.Outcome == RunOutcome.Extracted && _session.Save.StreakDays > 0)
            lines.Add($"Streak is now {_session.Save.StreakDays} days.");

        SummaryBody.Text = string.Join(Environment.NewLine, lines);
        SummaryOverlay.Visibility = Visibility.Visible;
    }

    private void BtnSummaryClose_Click(object sender, RoutedEventArgs e) => ShowHub();
}
