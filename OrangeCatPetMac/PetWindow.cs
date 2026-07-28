using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace OrangeCatPetMac;

internal sealed class PetWindow : Window
{
    private enum PetKind
    {
        LiJu,
        Leagle
    }

    private enum IdleActionState
    {
        None,
        Grooming,
        Scratching,
        Sleeping,
        Feeding,
        Petting,
        Recycling
    }

    private enum FoodKind
    {
        DriedFish,
        CannedFood,
        Chicken
    }

    private const double BaseWidth = 280;
    private const double BaseHeight = 330;
    private const double StartFollowingDistance = 155;
    private const double StopFollowingDistance = 105;
    private const int AnimationFrameCount = 8;
    private const double LeagleScratchFramesPerSecond = 5;
    private const int LeagleMinimumScratchCycles = 3;
    private const int LeagleMaximumScratchCyclesExclusive = 5;

    private static readonly double[] LeagleRollFrameDurationsSeconds =
    {
        1.0 / 4.5,
        1.0 / 4.5,
        1.0 / 4.5,
        1.0 / 4.5,
        0.7,
        0.7,
        1.0 / 4.5,
        1.0 / 4.5
    };

    private static readonly string[] LiJuGreetingMessages =
    {
        "\u55e8\uff5e\u4eca\u5929\u4e5f\u8981\u5f00\u5fc3\u5594\uff01",
        "\u563f\uff01\u4f60\u7ec8\u4e8e\u6765\u770b\u6211\u5566\uff5e",
        "\u4eca\u5929\u8fc7\u5f97\u600e\u4e48\u6837\uff1f",
        "\u6211\u4f1a\u4e56\u4e56\u966a\u7740\u4f60\u7684\u3002",
        "\u5fd9\u5b8c\u8bb0\u5f97\u4f11\u606f\u4e00\u4e0b\u54e6\uff01",
        "\u89c1\u5230\u4f60\u771f\u597d\uff5e",
        "\u8981\u4e0d\u8981\u966a\u6211\u73a9\u4e00\u4f1a\u513f\uff1f",
        "\u55b5\u545c\uff5e\u9001\u4f60\u4e00\u4e2a\u597d\u5fc3\u60c5\uff01"
    };

    private static readonly string[] LeagleGreetingMessages =
    {
        "\u6c6a\uff5e\u4eca\u5929\u4e5f\u4e00\u8d77\u73a9\u5427\uff01",
        "\u6211\u521a\u624d\u662f\u4e0d\u662f\u5077\u5077\u659c\u773c\u770b\u4f60\u4e86\uff1f",
        "\u8981\u4e0d\u8981\u966a\u6211\u73a9\u4e00\u4f1a\u513f\uff1f",
        "\u6211\u4f1a\u4e56\u4e56\u5b88\u5728\u4f60\u65c1\u8fb9\u3002",
        "\u5fd9\u5b8c\u8bb0\u5f97\u6765\u6478\u6478\u6211\uff01",
        "\u770b\u5230\u4f60\uff0c\u5c3e\u5df4\u5c31\u60f3\u6447\u8d77\u6765\uff5e",
        "\u4eca\u5929\u4e5f\u5e26\u6211\u4e00\u8d77\u73a9\u5427\uff01",
        "\u9001\u4f60\u4e00\u4e2a\u5c0f\u72d7\u7684\u597d\u5fc3\u60c5\uff01"
    };

    private sealed class PetSpriteSet
    {
        public required Bitmap Idle { get; init; }
        public required Bitmap Blink { get; init; }
        public required Bitmap[] Groom { get; init; }
        public required Bitmap[] Scratch { get; init; }
        public required Bitmap[] Sleep { get; init; }
        public required Bitmap[] Walk { get; init; }
        public required Bitmap[] Pet { get; init; }
        public required Bitmap[] FishFeed { get; init; }
        public required Bitmap[] CannedFeed { get; init; }
        public required Bitmap[] ChickenFeed { get; init; }
    }

    private readonly Random _random = new();
    private readonly Border _speechBubble;
    private readonly TextBlock _speechText;
    private readonly Image _catImage;
    private readonly Border _recycleItemVisual;
    private readonly TextBlock _recycleItemLabel;
    private Bitmap _idleSprite = null!;
    private Bitmap _blinkSprite = null!;
    private Bitmap[] _groomSprites = null!;
    private Bitmap[] _scratchSprites = null!;
    private Bitmap[] _sleepSprites = null!;
    private Bitmap[] _walkingSprites = null!;
    private Bitmap[] _pettingSprites = null!;
    private Bitmap[] _fishFeedingSprites = null!;
    private Bitmap[] _cannedFoodFeedingSprites = null!;
    private Bitmap[] _chickenFeedingSprites = null!;
    private readonly ScaleTransform _facingTransform = new(1, 1);
    private readonly ScaleTransform _breathingTransform = new(1, 1);
    private readonly ScaleTransform _reactionTransform = new(1, 1);
    private readonly RotateTransform _lookTransform = new();
    private readonly TranslateTransform _lookOffsetTransform = new();
    private readonly TranslateTransform _stepTransform = new();
    private readonly ScaleTransform _recycleItemScale = new(1, 1);
    private readonly RotateTransform _recycleItemRotation = new();
    private readonly TranslateTransform _recycleItemOffset = new();
    private readonly DispatcherTimer _motionTimer;
    private readonly DispatcherTimer _roamTimer;
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _blinkRestoreTimer;
    private readonly DispatcherTimer _speechTimer;
    private readonly DispatcherTimer _idleActionTimer;
    private readonly DispatcherTimer _reactionTimer;
    private readonly MenuItem _followMouseMenuItem;
    private readonly MenuItem _roamMenuItem;
    private readonly MenuItem _topmostMenuItem;
    private readonly ContextMenu _foodMenu;
    private readonly Dictionary<PetKind, PetSpriteSet> _petSprites = new();
    private readonly Dictionary<PetKind, MenuItem> _petMenuItems = new();
    private readonly Dictionary<double, MenuItem> _sizeMenuItems = new();
    private readonly Queue<DateTime> _recentFeedings = new();

    private PixelPoint _pointerDownScreen;
    private PixelPoint _windowAtPointerDown;
    private PixelPoint _roamTarget;
    private bool _hasRoamTarget;
    private bool _pointerCaptured;
    private bool _dragging;
    private bool _doubleClick;
    private bool _isWalking;
    private bool _isBlinking;
    private bool _recycleOperationActive;
    private int _walkFrameIndex;
    private double _velocityX;
    private double _velocityY;
    private DateTime _walkCycleStarted = DateTime.MinValue;
    private DateTime _followPausedUntil = DateTime.MinValue;
    private DateTime _roamRestUntil = DateTime.MinValue;
    private DateTime _nextIdleActionAt = DateTime.MaxValue;
    private DateTime _idleActionStarted = DateTime.MinValue;
    private DateTime _idleActionEnds = DateTime.MinValue;
    private IdleActionState _idleAction = IdleActionState.None;
    private PetKind _currentPetKind = PetKind.LiJu;
    private Bitmap[]? _activeInteractionSprites;

    public PetWindow()
    {
        Title = $"\u674e\u6a58 / Leagle \u00b7 {GetPlatformLabel()}";
        Width = BaseWidth;
        Height = BaseHeight;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;

        _petSprites[PetKind.LiJu] = LoadLiJuSprites();
        _petSprites[PetKind.Leagle] = LoadLeagleSprites();
        ApplyPetSprites(_petSprites[_currentPetKind]);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("78,*")
        };

        _speechText = new TextBlock
        {
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("PingFang SC"),
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(83, 54, 34)),
            MaxWidth = 230
        };

        _speechBubble = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(246, 255, 252, 245)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(220, 231, 168, 103)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14, 9),
            Margin = new Thickness(12, 5, 12, 6),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Child = _speechText,
            Opacity = 0,
            IsHitTestVisible = false
        };
        Grid.SetRow(_speechBubble, 0);

        var transforms = new TransformGroup();
        transforms.Children.Add(_facingTransform);
        transforms.Children.Add(_breathingTransform);
        transforms.Children.Add(_reactionTransform);
        transforms.Children.Add(_lookTransform);
        transforms.Children.Add(_lookOffsetTransform);
        transforms.Children.Add(_stepTransform);

        _catImage = new Image
        {
            Source = _idleSprite,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(8, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            RenderTransformOrigin = new RelativePoint(0.5, 0.72, RelativeUnit.Relative),
            RenderTransform = transforms,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(_catImage, "\u5355\u51fb\u629a\u6478 \u00b7 \u53cc\u51fb\u5582\u98df \u00b7 \u62d6\u6587\u4ef6\u7ed9\u6211\u653e\u8fdb\u56de\u6536\u7ad9 \u00b7 \u53f3\u952e\u83dc\u5355");
        Grid.SetRow(_catImage, 1);

        _recycleItemLabel = new TextBlock
        {
            Text = "\u6587\u4ef6",
            FontFamily = new FontFamily("PingFang SC"),
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(74, 105, 132)),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var recycleItemTransforms = new TransformGroup();
        recycleItemTransforms.Children.Add(_recycleItemScale);
        recycleItemTransforms.Children.Add(_recycleItemRotation);
        recycleItemTransforms.Children.Add(_recycleItemOffset);

        _recycleItemVisual = new Border
        {
            Width = 54,
            Height = 58,
            Background = new SolidColorBrush(Color.FromArgb(248, 250, 253, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(231, 168, 103)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(7),
            Child = _recycleItemLabel,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(0, 7, 0, 0),
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = recycleItemTransforms,
            IsVisible = false,
            IsHitTestVisible = false
        };
        Grid.SetRow(_recycleItemVisual, 1);
        _recycleItemVisual.SetValue(Panel.ZIndexProperty, 2);

        root.Children.Add(_speechBubble);
        root.Children.Add(_catImage);
        root.Children.Add(_recycleItemVisual);
        Content = root;

        _followMouseMenuItem = CreateCheckMenuItem("\u8ddf\u968f\u9f20\u6807", false);
        _followMouseMenuItem.Click += (_, _) =>
        {
            _hasRoamTarget = false;
            if (_followMouseMenuItem.IsChecked == true)
            {
                EndIdleAction();
                _followPausedUntil = DateTime.UtcNow.AddSeconds(1);
                Say("\u6211\u6765\u8ffd\u4f60\u5566\uff01");
            }
            else
            {
                StopWalking();
                Say("\u6211\u5148\u5728\u8fd9\u91cc\u5f85\u7740\uff5e");
            }
            UpdateMotionTimerState();
        };

        _roamMenuItem = CreateCheckMenuItem("\u81ea\u52a8\u6563\u6b65\uff08\u8d70\u8d70\u505c\u505c\uff09", false);
        _roamMenuItem.Click += (_, _) =>
        {
            _hasRoamTarget = false;
            if (_roamMenuItem.IsChecked == true)
            {
                _roamRestUntil = DateTime.UtcNow.AddSeconds(2 + _random.NextDouble() * 3);
                ScheduleNextIdleAction(1, 3);
            }
            else
            {
                StopWalking();
            }
            UpdateMotionTimerState();
        };

        _topmostMenuItem = CreateCheckMenuItem("\u59cb\u7ec8\u7f6e\u9876", true);
        _topmostMenuItem.Click += (_, _) => Topmost = _topmostMenuItem.IsChecked == true;

        _catImage.ContextMenu = BuildContextMenu();
        _foodMenu = BuildFoodMenu();
        _catImage.PointerPressed += OnCatPointerPressed;
        _catImage.PointerMoved += OnCatPointerMoved;
        _catImage.PointerReleased += OnCatPointerReleased;
        _catImage.PointerWheelChanged += OnCatPointerWheelChanged;
        DragDrop.SetAllowDrop(_catImage, true);
        DragDrop.AddDragEnterHandler(_catImage, OnCatDragEnter);
        DragDrop.AddDragOverHandler(_catImage, OnCatDragOver);
        DragDrop.AddDragLeaveHandler(_catImage, OnCatDragLeave);
        DragDrop.AddDropHandler(_catImage, OnCatDrop);

        _motionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _motionTimer.Tick += (_, _) => UpdateMotion();
        _roamTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _roamTimer.Tick += (_, _) => SelectRoamTarget();
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
        _blinkTimer.Tick += (_, _) => Blink();
        _blinkRestoreTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _blinkRestoreTimer.Tick += (_, _) => EndBlink();
        _speechTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.3) };
        _speechTimer.Tick += (_, _) => HideSpeech();
        _idleActionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _idleActionTimer.Tick += (_, _) => UpdateIdleAction();
        _reactionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(190) };
        _reactionTimer.Tick += (_, _) => EndReaction();

        Opened += OnOpened;
        Closed += (_, _) => StopAllTimers();
    }

    private static MenuItem CreateCheckMenuItem(string header, bool isChecked)
    {
        return new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isChecked
        };
    }

    private static Bitmap LoadSprite(string assetName)
    {
        var uri = new Uri($"avares://LiJuPet/Assets/{assetName}");
        return new Bitmap(AssetLoader.Open(uri));
    }

    private static Bitmap[] LoadSpriteSequence(string petPrefix, string action)
    {
        return Enumerable.Range(1, AnimationFrameCount)
            .Select(index => LoadSprite($"{petPrefix}-{action}-{index:00}.png"))
            .ToArray();
    }

    private static PetSpriteSet LoadLiJuSprites()
    {
        return new PetSpriteSet
        {
            Idle = LoadSprite("cat-smooth-idle-v2.png"),
            Blink = LoadSprite("cat-smooth-blink-v2.png"),
            Groom = LoadSpriteSequence("cat", "smooth-groom"),
            Scratch = LoadSpriteSequence("cat", "smooth-scratch-v2"),
            Sleep = LoadSpriteSequence("cat", "smooth-sleep-v2"),
            Walk = LoadSpriteSequence("cat", "smooth-walk"),
            Pet = LoadSpriteSequence("cat", "smooth-pat-seated"),
            FishFeed = LoadSpriteSequence("cat", "smooth-feed"),
            CannedFeed = LoadSpriteSequence("cat", "smooth-feed-can"),
            ChickenFeed = LoadSpriteSequence("cat", "smooth-feed-chicken")
        };
    }

    private static PetSpriteSet LoadLeagleSprites()
    {
        var feed = LoadSpriteSequence("dog", "feed");
        return new PetSpriteSet
        {
            Idle = LoadSprite("dog-idle.png"),
            Blink = LoadSprite("dog-blink-04.png"),
            Groom = LoadSpriteSequence("dog", "roll"),
            Scratch = LoadSpriteSequence("dog", "scratch"),
            Sleep = LoadSpriteSequence("dog", "sleep"),
            Walk = LoadSpriteSequence("dog", "walk"),
            Pet = LoadSpriteSequence("dog", "pat"),
            FishFeed = feed,
            CannedFeed = feed,
            ChickenFeed = feed
        };
    }

    private void ApplyPetSprites(PetSpriteSet sprites)
    {
        _idleSprite = sprites.Idle;
        _blinkSprite = sprites.Blink;
        _groomSprites = sprites.Groom;
        _scratchSprites = sprites.Scratch;
        _sleepSprites = sprites.Sleep;
        _walkingSprites = sprites.Walk;
        _pettingSprites = sprites.Pet;
        _fishFeedingSprites = sprites.FishFeed;
        _cannedFoodFeedingSprites = sprites.CannedFeed;
        _chickenFeedingSprites = sprites.ChickenFeed;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu { FontFamily = new FontFamily("PingFang SC") };
        var greetItem = new MenuItem { Header = "\u6253\u4e2a\u62db\u547c" };
        greetItem.Click += (_, _) =>
        {
            React();
            var greetings = _currentPetKind == PetKind.Leagle
                ? LeagleGreetingMessages
                : LiJuGreetingMessages;
            Say(greetings[_random.Next(greetings.Length)]);
        };

        var petMenu = new MenuItem { Header = "\u66f4\u6362\u5ba0\u7269" };
        AddPetItem(petMenu, "\u6a58\u732b \u00b7 \u674e\u6a58", PetKind.LiJu);
        AddPetItem(petMenu, "\u5e7c\u5e74\u6bd4\u683c\u72ac \u00b7 Leagle", PetKind.Leagle);
        _petMenuItems[_currentPetKind].IsChecked = true;

        var feedMenu = new MenuItem { Header = "\u6295\u5582" };
        AddFoodItems(feedMenu);

        var sizeMenu = new MenuItem { Header = "\u5927\u5c0f" };
        AddSizeItem(sizeMenu, "\u5c0f", 0.78);
        AddSizeItem(sizeMenu, "\u4e2d", 1.0);
        AddSizeItem(sizeMenu, "\u5927", 1.28);
        _sizeMenuItems[1.0].IsChecked = true;

        var homeItem = new MenuItem { Header = "\u56de\u5230\u53f3\u4e0b\u89d2" };
        homeItem.Click += (_, _) =>
        {
            _hasRoamTarget = false;
            PositionAtBottomRight();
            Say("\u6211\u56de\u6765\u5566\uff01");
        };

        var exitItem = new MenuItem { Header = "\u9000\u51fa" };
        exitItem.Click += (_, _) => Close();

        menu.Items.Add(greetItem);
        menu.Items.Add(petMenu);
        menu.Items.Add(feedMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(_followMouseMenuItem);
        menu.Items.Add(_roamMenuItem);
        menu.Items.Add(_topmostMenuItem);
        menu.Items.Add(sizeMenu);
        menu.Items.Add(homeItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private void AddPetItem(MenuItem parent, string label, PetKind petKind)
    {
        var item = CreateCheckMenuItem(label, false);
        item.Click += (_, _) => SwitchPet(petKind);
        _petMenuItems.Add(petKind, item);
        parent.Items.Add(item);
    }

    private void SwitchPet(PetKind petKind)
    {
        if (_currentPetKind == petKind)
        {
            return;
        }

        _hasRoamTarget = false;
        EndIdleAction();
        StopWalking();
        _blinkRestoreTimer.Stop();
        _isBlinking = false;

        _currentPetKind = petKind;
        ApplyPetSprites(_petSprites[petKind]);
        _catImage.Source = _idleSprite;

        foreach (var pair in _petMenuItems)
        {
            pair.Value.IsChecked = pair.Key == petKind;
        }

        _followPausedUntil = DateTime.UtcNow.AddSeconds(1.2);
        _roamRestUntil = DateTime.UtcNow.AddSeconds(2);
        ScheduleNextIdleAction(4, 8);
        Say(petKind == PetKind.Leagle
            ? "\u6c6a\uff5e\u6211\u6765\u5566\uff01\u4eca\u5929\u4e5f\u4e00\u8d77\u73a9\u5427\uff01"
            : "\u55e8\uff5e\u674e\u6a58\u56de\u6765\u5566\uff01");
    }

    private ContextMenu BuildFoodMenu()
    {
        var menu = new ContextMenu
        {
            FontFamily = new FontFamily("PingFang SC"),
            Placement = PlacementMode.Pointer
        };
        AddFoodItems(menu);
        return menu;
    }

    private void AddFoodItems(ItemsControl parent)
    {
        AddFoodItem(parent, "\u5c0f\u96f6\u98df", FoodKind.DriedFish);
        AddFoodItem(parent, "\u7f50\u5934", FoodKind.CannedFood);
        AddFoodItem(parent, "\u9e21\u8089", FoodKind.Chicken);
    }

    private void AddFoodItem(ItemsControl parent, string label, FoodKind food)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => BeginFeeding(food);
        parent.Items.Add(item);
    }

    private void AddSizeItem(MenuItem parent, string label, double scale)
    {
        var item = CreateCheckMenuItem(label, false);
        item.Click += (_, _) => SetPetSize(scale);
        _sizeMenuItems.Add(scale, item);
        parent.Items.Add(item);
    }

    private void ShowFoodMenu()
    {
        if (_idleAction == IdleActionState.Feeding || _recycleOperationActive)
        {
            return;
        }

        Say("\u4eca\u5929\u60f3\u5403\u4ec0\u4e48\uff1f", 1.8);
        _foodMenu.Open(_catImage);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        PositionAtBottomRight();
        UpdateMotionTimerState();
        _roamRestUntil = DateTime.UtcNow.AddSeconds(3);
        ScheduleNextIdleAction(5, 10);
        _roamTimer.Start();
        _blinkTimer.Start();
        _idleActionTimer.Start();

        var introTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        introTimer.Tick += (_, _) =>
        {
            introTimer.Stop();
            Say("\u95f2\u7740\u4f1a\u8214\u6bdb\u3001\u6320\u75d2\u548c\u7761\u89c9\uff1b\u62d6\u6587\u4ef6\u7ed9\u6211\u53ef\u4ee5\u653e\u8fdb\u56de\u6536\u7ad9\uff5e", 5.2);
        };
        introTimer.Start();
    }

    private void UpdateMotionTimerState()
    {
        if (_followMouseMenuItem.IsChecked == true || _roamMenuItem.IsChecked == true)
        {
            _motionTimer.Start();
        }
        else
        {
            _motionTimer.Stop();
            StopWalking();
            RelaxLook();
        }
    }

    private void UpdateMotion()
    {
        if (_pointerCaptured ||
            _recycleOperationActive ||
            _foodMenu.IsOpen ||
            _catImage.ContextMenu?.IsOpen == true)
        {
            StopWalking();
            RelaxLook();
            return;
        }

        PixelPoint target = default;
        var hasTarget = false;
        var scale = RenderScaling;
        var centerOffsetY = (78 + Math.Max(0, Height - 78) * 0.52) * scale;
        var pixelWidth = Width * scale;
        var pixelHeight = Height * scale;
        var workArea = CurrentWorkArea();

        if (_followMouseMenuItem.IsChecked == true && DateTime.UtcNow >= _followPausedUntil)
        {
            var cursor = GetGlobalCursorPosition();
            target = new PixelPoint(
                (int)Clamp(cursor.X, workArea.X + pixelWidth / 2, workArea.Right - pixelWidth / 2),
                (int)Clamp(cursor.Y, workArea.Y + centerOffsetY, workArea.Bottom - pixelHeight + centerOffsetY));
            hasTarget = true;
        }
        else if (_followMouseMenuItem.IsChecked != true &&
                 _roamMenuItem.IsChecked == true &&
                 _hasRoamTarget)
        {
            target = _roamTarget;
            hasTarget = true;
        }

        if (!hasTarget)
        {
            StopWalking();
            if (_idleAction == IdleActionState.None)
            {
                RelaxLook();
            }
            return;
        }

        var catCenterX = Position.X + pixelWidth / 2;
        var catCenterY = Position.Y + centerOffsetY;
        var deltaX = target.X - catCenterX;
        var deltaY = target.Y - catCenterY;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        UpdateLook(deltaX, deltaY);

        var movementThreshold = (_isWalking ? StopFollowingDistance : StartFollowingDistance) * scale;
        if (distance <= movementThreshold)
        {
            var wasWalking = _isWalking;
            Decelerate();
            if (_followMouseMenuItem.IsChecked != true && !_isWalking && (wasWalking || _hasRoamTarget))
            {
                BeginRoamRest();
            }
            return;
        }

        var desiredSpeed = Clamp((distance - StopFollowingDistance * scale) * 0.09, 3.2 * scale, 16.5 * scale);
        var desiredVelocityX = deltaX / distance * desiredSpeed;
        var desiredVelocityY = deltaY / distance * desiredSpeed;
        _velocityX += (desiredVelocityX - _velocityX) * 0.18;
        _velocityY += (desiredVelocityY - _velocityY) * 0.18;

        var oldPosition = Position;
        Position = new PixelPoint(
            (int)Math.Round(Position.X + _velocityX),
            (int)Math.Round(Position.Y + _velocityY));
        ClampToWorkArea();

        if (Math.Abs(Position.X - oldPosition.X) + Math.Abs(Position.Y - oldPosition.Y) < 1)
        {
            StopWalking();
            if (_followMouseMenuItem.IsChecked == true)
            {
                _hasRoamTarget = false;
            }
            else
            {
                BeginRoamRest();
            }
            return;
        }

        _facingTransform.ScaleX = deltaX >= 0 ? 1 : -1;
        SetWalkingFrame();
    }

    private void Decelerate()
    {
        _velocityX *= 0.68;
        _velocityY *= 0.68;
        if (Math.Abs(_velocityX) + Math.Abs(_velocityY) < 0.35)
        {
            StopWalking();
            return;
        }

        Position = new PixelPoint(
            (int)Math.Round(Position.X + _velocityX),
            (int)Math.Round(Position.Y + _velocityY));
        ClampToWorkArea();
        SetWalkingFrame();
    }

    private void UpdateLook(double deltaX, double deltaY)
    {
        var targetAngle = Clamp(deltaX / 55, -6.5, 6.5);
        _lookTransform.Angle += (targetAngle - _lookTransform.Angle) * 0.18;
        var targetOffsetX = Clamp(deltaX / 95, -4.2, 4.2);
        var targetOffsetY = Clamp(deltaY / 110, -3.2, 3.2);
        _lookOffsetTransform.X += (targetOffsetX - _lookOffsetTransform.X) * 0.18;
        _lookOffsetTransform.Y += (targetOffsetY - _lookOffsetTransform.Y) * 0.18;
    }

    private void RelaxLook()
    {
        _lookTransform.Angle *= 0.88;
        _lookOffsetTransform.X *= 0.88;
        _lookOffsetTransform.Y *= 0.88;
    }

    private void SetWalkingFrame()
    {
        EndIdleAction();
        var now = DateTime.UtcNow;
        if (!_isWalking)
        {
            _isWalking = true;
            _walkCycleStarted = now;
            _walkFrameIndex = 0;
            _catImage.Source = _walkingSprites[0];
        }

        _isBlinking = false;
        _blinkRestoreTimer.Stop();
        var elapsedMilliseconds = (now - _walkCycleStarted).TotalMilliseconds;
        const double walkingFrameMilliseconds = 120;
        var cycleDuration = walkingFrameMilliseconds * _walkingSprites.Length;
        var cycleProgress = (elapsedMilliseconds % cycleDuration) / cycleDuration;
        var frameIndex = (int)(elapsedMilliseconds / walkingFrameMilliseconds) % _walkingSprites.Length;
        if (frameIndex != _walkFrameIndex)
        {
            _walkFrameIndex = frameIndex;
            _catImage.Source = _walkingSprites[_walkFrameIndex];
        }

        var phase = Math.PI * 2 * cycleProgress;
        _stepTransform.Y = -0.8 + Math.Sin(phase) * 2;
        _stepTransform.X = Math.Cos(phase) * 0.65;
    }

    private void StopWalking()
    {
        _velocityX = 0;
        _velocityY = 0;
        if (!_isWalking)
        {
            return;
        }

        _isWalking = false;
        _walkFrameIndex = 0;
        _walkCycleStarted = DateTime.MinValue;
        _stepTransform.X = 0;
        _stepTransform.Y = 0;
        if (!_isBlinking)
        {
            _catImage.Source = _idleSprite;
        }
    }

    private void BeginRoamRest()
    {
        _hasRoamTarget = false;
        _roamRestUntil = DateTime.UtcNow.AddSeconds(8 + _random.NextDouble() * 12);
        ScheduleNextIdleAction(1.5, 4);
    }

    private void SelectRoamTarget()
    {
        if (_followMouseMenuItem.IsChecked == true ||
            _roamMenuItem.IsChecked != true ||
            _pointerCaptured ||
            _isWalking ||
            _hasRoamTarget ||
            _idleAction != IdleActionState.None ||
            DateTime.UtcNow < _roamRestUntil)
        {
            return;
        }

        var area = CurrentWorkArea();
        var scale = RenderScaling;
        var centerOffsetY = (78 + Math.Max(0, Height - 78) * 0.52) * scale;
        var pixelWidth = Width * scale;
        var pixelHeight = Height * scale;
        var catCenterX = Position.X + pixelWidth / 2;
        var catCenterY = Position.Y + centerOffsetY;
        var minimumX = area.X + pixelWidth / 2;
        var maximumX = area.Right - pixelWidth / 2;
        var minimumY = area.Y + centerOffsetY;
        var maximumY = area.Bottom - pixelHeight + centerOffsetY;

        var goRight = _random.Next(2) == 0;
        var availableRoom = goRight ? maximumX - catCenterX : catCenterX - minimumX;
        var oppositeRoom = goRight ? catCenterX - minimumX : maximumX - catCenterX;
        if (availableRoom < 180 * scale && oppositeRoom > availableRoom)
        {
            goRight = !goRight;
            availableRoom = oppositeRoom;
        }

        var travelDistance = Math.Min(availableRoom, (200 + _random.NextDouble() * 320) * scale);
        var targetX = catCenterX + (goRight ? travelDistance : -travelDistance);
        var targetY = Clamp(catCenterY + _random.Next(-70, 71) * scale, minimumY, maximumY);
        _roamTarget = new PixelPoint(
            (int)Clamp(targetX, minimumX, maximumX),
            (int)targetY);
        _hasRoamTarget = true;
    }

    private void ScheduleNextIdleAction(double minimumSeconds, double maximumSeconds)
    {
        _nextIdleActionAt = DateTime.UtcNow.AddSeconds(
            minimumSeconds + _random.NextDouble() * Math.Max(0, maximumSeconds - minimumSeconds));
    }

    private void UpdateIdleAction()
    {
        var now = DateTime.UtcNow;
        if (_idleAction == IdleActionState.None)
        {
            _breathingTransform.ScaleY = 1 + Math.Sin(now.TimeOfDay.TotalSeconds * 2.8) * 0.014;
            if (now >= _nextIdleActionAt &&
                !_isWalking &&
                !_isBlinking &&
                !_pointerCaptured &&
                !_hasRoamTarget &&
                !_foodMenu.IsOpen &&
                _catImage.ContextMenu?.IsOpen != true)
            {
                StartRandomIdleAction();
            }
            return;
        }

        _breathingTransform.ScaleY = 1;
        if (_isWalking || _pointerCaptured || now >= _idleActionEnds)
        {
            EndIdleAction();
            return;
        }

        var elapsedSeconds = (now - _idleActionStarted).TotalSeconds;
        switch (_idleAction)
        {
            case IdleActionState.Grooming:
                if (_currentPetKind == PetKind.Leagle)
                {
                    SetTimedAnimationFrame(
                        _groomSprites,
                        elapsedSeconds,
                        LeagleRollFrameDurationsSeconds);
                }
                else
                {
                    SetAnimationFrame(_groomSprites, elapsedSeconds, 7, true, true);
                }
                break;
            case IdleActionState.Scratching:
                if (_currentPetKind == PetKind.Leagle)
                {
                    SetAnimationFrame(
                        _scratchSprites,
                        elapsedSeconds,
                        LeagleScratchFramesPerSecond,
                        true,
                        false);
                }
                else
                {
                    SetAnimationFrame(_scratchSprites, elapsedSeconds, 7, true, false);
                }
                break;
            case IdleActionState.Sleeping:
                SetAnimationFrame(
                    _sleepSprites,
                    elapsedSeconds,
                    2.5,
                    _currentPetKind != PetKind.Leagle,
                    false);
                break;
            case IdleActionState.Feeding:
                if (_activeInteractionSprites != null)
                {
                    SetAnimationFrame(_activeInteractionSprites, elapsedSeconds, 7.5, false, false);
                }
                break;
            case IdleActionState.Recycling:
                if (_activeInteractionSprites != null)
                {
                    SetAnimationFrame(_activeInteractionSprites, elapsedSeconds, 7.5, false, false);
                }
                UpdateRecycleItemAnimation(elapsedSeconds);
                break;
            case IdleActionState.Petting:
                if (_activeInteractionSprites != null)
                {
                    SetAnimationFrame(_activeInteractionSprites, elapsedSeconds, 7, false, false);
                }
                break;
        }
    }

    private void SetTimedAnimationFrame(
        Bitmap[] sprites,
        double elapsedSeconds,
        double[] frameDurationsSeconds)
    {
        var frameCount = Math.Min(sprites.Length, frameDurationsSeconds.Length);
        var frameIndex = frameCount - 1;
        var remainingSeconds = elapsedSeconds;

        for (var index = 0; index < frameCount; index++)
        {
            if (remainingSeconds < frameDurationsSeconds[index])
            {
                frameIndex = index;
                break;
            }

            remainingSeconds -= frameDurationsSeconds[index];
        }

        if (!ReferenceEquals(_catImage.Source, sprites[frameIndex]))
        {
            _catImage.Source = sprites[frameIndex];
        }
    }

    private void SetAnimationFrame(
        Bitmap[] sprites,
        double elapsedSeconds,
        double framesPerSecond,
        bool loop,
        bool pingPong)
    {
        var rawIndex = (int)(elapsedSeconds * framesPerSecond);
        int frameIndex;
        if (!loop)
        {
            frameIndex = Math.Min(sprites.Length - 1, rawIndex);
        }
        else if (pingPong && sprites.Length > 1)
        {
            var cycleLength = sprites.Length * 2 - 2;
            var cycleIndex = rawIndex % cycleLength;
            frameIndex = cycleIndex < sprites.Length ? cycleIndex : cycleLength - cycleIndex;
        }
        else
        {
            frameIndex = rawIndex % sprites.Length;
        }

        if (!ReferenceEquals(_catImage.Source, sprites[frameIndex]))
        {
            _catImage.Source = sprites[frameIndex];
        }
    }

    private void StartRandomIdleAction()
    {
        _blinkRestoreTimer.Stop();
        _isBlinking = false;
        _idleActionStarted = DateTime.UtcNow;
        var selection = _random.NextDouble();
        if (selection < 0.44)
        {
            _idleAction = IdleActionState.Grooming;
            _idleActionEnds = _currentPetKind == PetKind.Leagle
                ? _idleActionStarted.AddSeconds(8 + _random.NextDouble() * 4)
                : _idleActionStarted.AddSeconds(5 + _random.NextDouble() * 3);
            _catImage.Source = _groomSprites[0];
        }
        else if (selection < 0.74)
        {
            _idleAction = IdleActionState.Scratching;
            if (_currentPetKind == PetKind.Leagle)
            {
                var completeCycles = _random.Next(
                    LeagleMinimumScratchCycles,
                    LeagleMaximumScratchCyclesExclusive);
                var actionDurationSeconds =
                    completeCycles * _scratchSprites.Length / LeagleScratchFramesPerSecond;
                _idleActionEnds = _idleActionStarted.AddSeconds(actionDurationSeconds);
            }
            else
            {
                _idleActionEnds = _idleActionStarted.AddSeconds(
                    3.5 + _random.NextDouble() * 2.5);
            }
            _catImage.Source = _scratchSprites[0];
        }
        else
        {
            _idleAction = IdleActionState.Sleeping;
            _idleActionEnds = _idleActionStarted.AddSeconds(12 + _random.NextDouble() * 10);
            _catImage.Source = _sleepSprites[0];
        }
    }

    private void EndIdleAction()
    {
        if (_idleAction == IdleActionState.None)
        {
            return;
        }

        var endedAction = _idleAction;
        _idleAction = IdleActionState.None;
        _activeInteractionSprites = null;
        _idleActionStarted = DateTime.MinValue;
        _idleActionEnds = DateTime.MinValue;
        _stepTransform.X = 0;
        _stepTransform.Y = 0;
        _lookTransform.Angle = 0;
        _lookOffsetTransform.X = 0;
        _lookOffsetTransform.Y = 0;
        if (!_isWalking)
        {
            _catImage.Source = _idleSprite;
        }
        if (endedAction == IdleActionState.Recycling)
        {
            HideRecycleItemVisual();
        }
        ScheduleNextIdleAction(7, 16);
    }

    private void Blink()
    {
        _blinkTimer.Interval = TimeSpan.FromSeconds(2.8 + _random.NextDouble() * 3.2);
        if (_isWalking || _pointerCaptured || _idleAction != IdleActionState.None)
        {
            return;
        }

        _isBlinking = true;
        _catImage.Source = _blinkSprite;
        _blinkRestoreTimer.Stop();
        _blinkRestoreTimer.Interval = TimeSpan.FromMilliseconds(180);
        _blinkRestoreTimer.Start();
    }

    private void EndBlink()
    {
        _blinkRestoreTimer.Stop();
        _isBlinking = false;
        _catImage.Source = _isWalking ? _walkingSprites[_walkFrameIndex] : _idleSprite;
    }

    private void OnCatPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(_catImage).Properties;
        if (!properties.IsLeftButtonPressed || _recycleOperationActive)
        {
            return;
        }

        _hasRoamTarget = false;
        EndIdleAction();
        StopWalking();
        _pointerDownScreen = GetGlobalCursorPosition();
        _windowAtPointerDown = Position;
        e.Pointer.Capture(_catImage);
        _pointerCaptured = true;
        _dragging = false;
        _doubleClick = e.ClickCount >= 2;
        e.Handled = true;
    }

    private void OnCatPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pointerCaptured || !e.GetCurrentPoint(_catImage).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var current = GetGlobalCursorPosition();
        var deltaX = current.X - _pointerDownScreen.X;
        var deltaY = current.Y - _pointerDownScreen.Y;
        if (!_dragging && Math.Abs(deltaX) + Math.Abs(deltaY) > 5 * RenderScaling)
        {
            _dragging = true;
            HideSpeech();
        }

        if (_dragging)
        {
            Position = new PixelPoint(
                _windowAtPointerDown.X + deltaX,
                _windowAtPointerDown.Y + deltaY);
            ClampToWorkArea();
        }
    }

    private void OnCatPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pointerCaptured)
        {
            return;
        }

        e.Pointer.Capture(null);
        _pointerCaptured = false;
        _followPausedUntil = DateTime.UtcNow.AddSeconds(_dragging ? 5 : 1.8);
        if (_dragging)
        {
            Say("\u597d\u5566\uff0c\u6211\u5c31\u5728\u8fd9\u91cc\uff5e", 2.4);
        }
        else if (_doubleClick)
        {
            ShowFoodMenu();
        }
        else
        {
            Pat();
        }
        e.Handled = true;
    }

    private void OnCatPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var scales = _sizeMenuItems.Keys.OrderBy(value => value).ToArray();
        var current = Width / BaseWidth;
        var index = Array.FindIndex(scales, value => Math.Abs(value - current) < 0.02);
        if (index < 0)
        {
            index = 1;
        }
        index = e.Delta.Y > 0 ? Math.Min(scales.Length - 1, index + 1) : Math.Max(0, index - 1);
        SetPetSize(scales[index]);
        e.Handled = true;
    }

    private void OnCatDragEnter(object? sender, DragEventArgs e)
    {
        var paths = GetDroppedPaths(e.DataTransfer);
        if (_recycleOperationActive || paths.Length == 0)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        ShowRecycleItemVisual(paths);
        Say("\u677e\u5f00\u540e\uff0c\u6211\u4f1a\u628a\u5b83\u653e\u8fdb\u56de\u6536\u7ad9\uff5e", 2.2);
        e.Handled = true;
    }

    private void OnCatDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = !_recycleOperationActive && GetDroppedPaths(e.DataTransfer).Length > 0
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCatDragLeave(object? sender, DragEventArgs e)
    {
        if (!_recycleOperationActive)
        {
            HideRecycleItemVisual();
            HideSpeech();
        }
        e.Handled = true;
    }

    private async void OnCatDrop(object? sender, DragEventArgs e)
    {
        var paths = GetDroppedPaths(e.DataTransfer);
        e.Handled = true;

        if (_recycleOperationActive || paths.Length == 0)
        {
            e.DragEffects = DragDropEffects.None;
            HideRecycleItemVisual();
            Say("\u8fd9\u4e2a\u4e0d\u80fd\u653e\u8fdb\u56de\u6536\u7ad9\u3002", 2.2);
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        _recycleOperationActive = true;
        _hasRoamTarget = false;
        StopWalking();
        StartInteractionAnimation(IdleActionState.Recycling, _fishFeedingSprites, 1.8);
        ShowRecycleItemVisual(paths);
        Say(
            _currentPetKind == PetKind.Leagle
                ? paths.Length == 1
                    ? "\u55f7\u545c\uff01\u6211\u6765\u5904\u7406\uff5e"
                    : $"\u55f7\u545c\uff01\u8fd9 {paths.Length} \u9879\u4ea4\u7ed9\u6211\uff5e"
                : paths.Length == 1
                    ? "\u55b5\u545c\uff01\u4ea4\u7ed9\u6211\u5427\uff5e"
                    : $"\u55b5\u545c\uff01\u8fd9 {paths.Length} \u9879\u4ea4\u7ed9\u6211\uff5e",
            2.2);

        try
        {
            await Task.Delay(700);
            var result = await Task.Run(() => DesktopTrash.Move(paths));
            ShowRecycleResult(result);
        }
        finally
        {
            _recycleOperationActive = false;
        }
    }

    private static string[] GetDroppedPaths(IDataTransfer dataTransfer)
    {
        var files = dataTransfer.TryGetFiles();
        if (files is null)
        {
            return Array.Empty<string>();
        }

        return DesktopTrash.GetSupportedPaths(
            files.Select(file => file.Path.LocalPath));
    }

    private void ShowRecycleItemVisual(IReadOnlyCollection<string> paths)
    {
        var isSingleDirectory = paths.Count == 1 && Directory.Exists(paths.First());
        _recycleItemLabel.Text = paths.Count > 1
            ? $"{paths.Count} \u9879"
            : isSingleDirectory
                ? "\u6587\u4ef6\u5939"
                : "\u6587\u4ef6";
        _recycleItemScale.ScaleX = 1;
        _recycleItemScale.ScaleY = 1;
        _recycleItemRotation.Angle = -4;
        _recycleItemOffset.X = 0;
        _recycleItemOffset.Y = 0;
        _recycleItemVisual.Opacity = 1;
        _recycleItemVisual.IsVisible = true;
    }

    private void UpdateRecycleItemAnimation(double elapsedSeconds)
    {
        var progress = Clamp(elapsedSeconds / 1.25, 0, 1);
        var easedProgress = 1 - Math.Pow(1 - progress, 3);
        var scale = 1 - easedProgress * 0.84;

        _recycleItemScale.ScaleX = scale;
        _recycleItemScale.ScaleY = scale;
        _recycleItemRotation.Angle = -4 + easedProgress * 16;
        _recycleItemOffset.X = Math.Sin(progress * Math.PI * 4) * 7 * (1 - progress);
        _recycleItemOffset.Y = easedProgress * 112;
        _recycleItemVisual.Opacity = 1 - easedProgress * 0.96;
    }

    private void HideRecycleItemVisual()
    {
        _recycleItemVisual.IsVisible = false;
        _recycleItemVisual.Opacity = 0;
    }

    private void ShowRecycleResult(DesktopTrash.MoveResult result)
    {
        if (result.SucceededCount > 0 && result.FailedCount == 0)
        {
            Say(
                result.SucceededCount == 1
                    ? "\u5df2\u7ecf\u653e\u8fdb\u56de\u6536\u7ad9\u5566\uff01"
                    : $"\u90fd\u653e\u8fdb\u56de\u6536\u7ad9\u5566\uff0c\u5171 {result.SucceededCount} \u9879\uff01",
                3);
        }
        else if (result.SucceededCount > 0)
        {
            Say(
                $"\u653e\u8fdb\u53bb {result.SucceededCount} \u9879\uff0c\u53e6\u6709 {result.FailedCount} \u9879\u5931\u8d25\u4e86\u3002",
                3.4);
        }
        else
        {
            Say("\u8fd9\u4e2a\u6211\u5403\u4e0d\u6389\uff0c\u6ca1\u6709\u79fb\u52a8\u4efb\u4f55\u4e1c\u897f\u3002", 3.2);
        }
    }
    private void Pat()

    {
        if (_recycleOperationActive)
        {
            return;
        }

        var messages = _currentPetKind == PetKind.Leagle
            ? new[]
            {
                "\u518d\u6478\u6478\u6211\u7684\u8111\u888b\uff01",
                "\u8fd9\u91cc\u6700\u8212\u670d\u5566\uff5e",
                "\u5c3e\u5df4\u5feb\u8981\u6447\u8d77\u6765\u4e86\uff01",
                "\u6211\u6700\u559c\u6b22\u8fd9\u4e2a\uff01",
                "\u522b\u505c\uff0c\u518d\u6478\u4e00\u4e0b\uff5e"
            }
            : new[]
            {
                "\u547c\u565c\u547c\u565c\uff5e",
                "\u518d\u6478\u4e00\u4e0b\uff01",
                "\u55b5\u545c\uff5e",
                "\u4eca\u5929\u4e5f\u8981\u5f00\u5fc3\u5594\uff01",
                "\u6211\u5728\u966a\u4f60\u3002"
            };
        StartInteractionAnimation(IdleActionState.Petting, _pettingSprites, 1.25);
        React(1.04);
        Say(messages[_random.Next(messages.Length)]);
    }

    private void BeginFeeding(FoodKind food)
    {
        if (_recycleOperationActive)
        {
            return;
        }

        var now = DateTime.UtcNow;
        while (_recentFeedings.Count > 0 && (now - _recentFeedings.Peek()).TotalSeconds > 55)
        {
            _recentFeedings.Dequeue();
        }

        if (_recentFeedings.Count >= 3)
        {
            var fullMessages = _currentPetKind == PetKind.Leagle
                ? new[]
                {
                    "\u6211\u7684\u5c0f\u809a\u5b50\u5df2\u7ecf\u5706\u6eda\u6eda\u5566\uff01",
                    "\u5403\u9971\u4e86\uff0c\u6211\u8981\u8db4\u4e00\u4f1a\u513f\uff5e",
                    "\u518d\u5403\u5c31\u8dd1\u4e0d\u52a8\u5566\uff01"
                }
                : new[]
                {
                    "\u809a\u5b50\u5df2\u7ecf\u5706\u6eda\u6eda\u5566\uff0c\u7b49\u4f1a\u513f\u518d\u5403\uff5e",
                    "\u6211\u5403\u9971\u5566\uff0c\u5148\u4f11\u606f\u4e00\u4e0b\u5427\uff01",
                    "\u518d\u5403\u5c31\u8981\u8d70\u4e0d\u52a8\u5566\uff5e"
                };
            StartInteractionAnimation(IdleActionState.Petting, _pettingSprites, 1.25);
            Say(fullMessages[_random.Next(fullMessages.Length)], 3.1);
            return;
        }

        _recentFeedings.Enqueue(now);
        Bitmap[] sprites;
        string[] messages;
        switch (food)
        {
            case FoodKind.CannedFood:
                sprites = _cannedFoodFeedingSprites;
                messages = _currentPetKind == PetKind.Leagle
                    ? new[] { "\u6211\u95fb\u5230\u7f50\u5934\u5566\uff01", "\u6211\u8981\u5927\u53e3\u5403\uff01", "\u8fd9\u4e2a\u5473\u9053\u6211\u8bb0\u4f4f\u4e86\uff01" }
                    : new[] { "\u7f50\u5934\u597d\u9999\u5440\uff01", "\u5927\u53e3\u5403\u7f50\u7f50\uff5e", "\u8fd9\u4e2a\u5473\u9053\u6211\u559c\u6b22\uff01" };
                break;
            case FoodKind.Chicken:
                sprites = _chickenFeedingSprites;
                messages = _currentPetKind == PetKind.Leagle
                    ? new[] { "\u9e21\u8089\uff01\u5feb\u7ed9\u6211\uff01", "\u55f7\u545c\u4e00\u5927\u53e3\uff01", "\u5403\u5b8c\u8fd8\u60f3\u53bb\u73a9\uff5e" }
                    : new[] { "\u9e21\u8089\u771f\u597d\u5403\uff01", "\u55f7\u545c\u4e00\u5927\u53e3\uff5e", "\u8c22\u8c22\u4f60\u7684\u9e21\u8089\uff01" };
                break;
            default:
                sprites = _fishFeedingSprites;
                messages = _currentPetKind == PetKind.Leagle
                    ? new[] { "\u53d1\u73b0\u5c0f\u96f6\u98df\uff01", "\u5494\u56bc\u5494\u56bc\uff0c\u771f\u9999\uff01", "\u6211\u8fd8\u60f3\u8981\u4e00\u5757\uff5e" }
                    : new[] { "\u5c0f\u9c7c\u5e72\u771f\u9999\uff01", "\u5494\u56bc\u5494\u56bc\uff5e", "\u6700\u559c\u6b22\u5c0f\u9c7c\u5e72\u5566\uff01" };
                break;
        }

        StartInteractionAnimation(IdleActionState.Feeding, sprites, 1.4);
        Say(messages[_random.Next(messages.Length)], 2.8);
    }

    private void StartInteractionAnimation(IdleActionState state, Bitmap[] sprites, double durationSeconds)
    {
        _hasRoamTarget = false;
        StopWalking();
        EndIdleAction();
        _blinkRestoreTimer.Stop();
        _isBlinking = false;
        _stepTransform.X = 0;
        _stepTransform.Y = 0;
        var now = DateTime.UtcNow;
        _idleAction = state;
        _activeInteractionSprites = sprites;
        _idleActionStarted = now;
        _idleActionEnds = now.AddSeconds(durationSeconds);
        _followPausedUntil = now.AddSeconds(durationSeconds + 0.6);
        _roamRestUntil = now.AddSeconds(durationSeconds + 2);
        _catImage.Source = sprites[0];
    }

    private void React(double peakScale = 1.08)
    {
        _reactionTimer.Stop();
        _reactionTransform.ScaleX = peakScale;
        _reactionTransform.ScaleY = peakScale;
        _stepTransform.Y = -8;
        _reactionTimer.Start();
    }

    private void EndReaction()
    {
        _reactionTimer.Stop();
        _reactionTransform.ScaleX = 1;
        _reactionTransform.ScaleY = 1;
        _stepTransform.Y = 0;
    }

    private void Say(string message, double seconds = 2.3)
    {
        _speechTimer.Stop();
        _speechTimer.Interval = TimeSpan.FromSeconds(seconds);
        _speechText.Text = message;
        _speechBubble.Opacity = 1;
        _speechTimer.Start();
    }

    private void HideSpeech()
    {
        _speechTimer.Stop();
        _speechBubble.Opacity = 0;
    }

    private void SetPetSize(double scale)
    {
        var oldBottom = Position.Y + PixelHeight;
        Width = BaseWidth * scale;
        Height = BaseHeight * scale;
        Position = new PixelPoint(Position.X, oldBottom - PixelHeight);
        foreach (var pair in _sizeMenuItems)
        {
            pair.Value.IsChecked = Math.Abs(pair.Key - scale) < 0.01;
        }
        ClampToWorkArea();
        Say(scale < 0.9
            ? "\u8ff7\u4f60\u6a21\u5f0f\uff01"
            : scale > 1.1
                ? _currentPetKind == PetKind.Leagle ? "\u5927\u72d7\u767b\u573a\uff01" : "\u5927\u6a58\u767b\u573a\uff01"
                : "\u521a\u521a\u597d\uff5e");
    }

    private void PositionAtBottomRight()
    {
        var area = CurrentWorkArea();
        Position = new PixelPoint(area.Right - PixelWidth - 18, area.Bottom - PixelHeight - 8);
    }

    private void ClampToWorkArea()
    {
        var area = CurrentWorkArea();
        Position = new PixelPoint(
            (int)Clamp(Position.X, area.X, Math.Max(area.X, area.Right - PixelWidth)),
            (int)Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Bottom - PixelHeight)));
    }

    private PixelRect CurrentWorkArea()
    {
        return Screens.ScreenFromWindow(this)?.WorkingArea ??
               Screens.Primary?.WorkingArea ??
               new PixelRect(0, 0, 1440, 900);
    }

    private int PixelWidth => (int)Math.Round(Width * RenderScaling);
    private int PixelHeight => (int)Math.Round(Height * RenderScaling);

    private void StopAllTimers()
    {
        _motionTimer.Stop();
        _roamTimer.Stop();
        _blinkTimer.Stop();
        _blinkRestoreTimer.Stop();
        _speechTimer.Stop();
        _idleActionTimer.Stop();
        _reactionTimer.Stop();
        _foodMenu.Close();
    }

    private static PixelPoint GetGlobalCursorPosition()
    {
        if (OperatingSystem.IsWindows())
        {
            GetCursorPos(out var windowsPoint);
            return new PixelPoint(windowsPoint.X, windowsPoint.Y);
        }

        if (OperatingSystem.IsMacOS())
        {
            var eventRef = CGEventCreate(IntPtr.Zero);
            if (eventRef != IntPtr.Zero)
            {
                try
                {
                    var point = CGEventGetLocation(eventRef);
                    return new PixelPoint((int)Math.Round(point.X), (int)Math.Round(point.Y));
                }
                finally
                {
                    CFRelease(eventRef);
                }
            }
        }

        if (OperatingSystem.IsLinux())
        {
            var display = XOpenDisplay(IntPtr.Zero);
            if (display != IntPtr.Zero)
            {
                try
                {
                    var rootWindow = XDefaultRootWindow(display);
                    if (XQueryPointer(
                            display,
                            rootWindow,
                            out _,
                            out _,
                            out var rootX,
                            out var rootY,
                            out _,
                            out _,
                            out _) != 0)
                    {
                        return new PixelPoint(rootX, rootY);
                    }
                }
                finally
                {
                    XCloseDisplay(display);
                }
            }
        }

        return default;
    }

    private static string GetPlatformLabel()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "ARM64",
            Architecture.X64 => "x64",
            _ => RuntimeInformation.ProcessArchitecture.ToString()
        };

        if (OperatingSystem.IsMacOS())
        {
            return $"macOS {architecture}";
        }

        if (OperatingSystem.IsLinux())
        {
            return $"\u9e92\u9e9f/UOS {architecture}";
        }

        return architecture;
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WindowsPoint point);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern CoreGraphicsPoint CGEventGetLocation(IntPtr eventRef);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr handle);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XQueryPointer(
        IntPtr display,
        IntPtr window,
        out IntPtr rootReturn,
        out IntPtr childReturn,
        out int rootXReturn,
        out int rootYReturn,
        out int windowXReturn,
        out int windowYReturn,
        out uint maskReturn);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CoreGraphicsPoint
    {
        public double X;
        public double Y;
    }
}

