using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OrangeCatPet;

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
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint PrimaryMonitorFlag = 0x00000001;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

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
        "嗨～今天也要开心呀！",
        "喵！你终于来看我啦～",
        "今天过得怎么样？",
        "我会乖乖陪着你的。",
        "忙完记得休息一下哦！",
        "见到你真好～",
        "要不要陪我玩一会儿？",
        "喵呜～送你一个好心情！"
    };

    private static readonly string[] LeagleGreetingMessages =
    {
        "汪～我来报到！",
        "我刚才是不是偷偷斜眼看你了？",
        "要不要陪我玩一会儿？",
        "我会乖乖守在你旁边。",
        "忙完记得来摸摸我！",
        "看到你，尾巴就想摇起来～",
        "今天也带我一起玩吧！",
        "送你一个小狗的好心情！"
    };

    private readonly record struct MonitorWorkArea(
        NativeRect MonitorArea,
        NativeRect WorkArea,
        bool IsPrimary);

    private sealed class PetSpriteSet
    {
        public required BitmapSource Idle { get; init; }
        public required BitmapSource Blink { get; init; }
        public required BitmapSource[] Groom { get; init; }
        public required BitmapSource[] Scratch { get; init; }
        public required BitmapSource[] Sleep { get; init; }
        public required BitmapSource[] Walk { get; init; }
        public required BitmapSource[] Pet { get; init; }
        public required BitmapSource[] FishFeed { get; init; }
        public required BitmapSource[] CannedFeed { get; init; }
        public required BitmapSource[] ChickenFeed { get; init; }
    }

    private readonly Random _random = new();
    private readonly Border _speechBubble;
    private readonly TextBlock _speechText;
    private readonly Image _catImage;
    private readonly Border _recycleItemVisual;
    private readonly TextBlock _recycleItemLabel;
    private BitmapSource _idleSprite = null!;
    private BitmapSource _blinkSprite = null!;
    private BitmapSource[] _groomSprites = null!;
    private BitmapSource[] _scratchSprites = null!;
    private BitmapSource[] _sleepSprites = null!;
    private BitmapSource[] _walkingSprites = null!;
    private BitmapSource[] _pettingSprites = null!;
    private BitmapSource[] _fishFeedingSprites = null!;
    private BitmapSource[] _cannedFoodFeedingSprites = null!;
    private BitmapSource[] _chickenFeedingSprites = null!;
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
    private readonly MenuItem _followMouseMenuItem;
    private readonly MenuItem _roamMenuItem;
    private readonly MenuItem _topmostMenuItem;
    private readonly ContextMenu _foodMenu;
    private readonly Dictionary<PetKind, PetSpriteSet> _petSprites = new();
    private readonly Dictionary<PetKind, MenuItem> _petMenuItems = new();
    private readonly Dictionary<double, MenuItem> _sizeMenuItems = new();
    private readonly Queue<DateTime> _recentFeedings = new();

    private Point _pointerDownScreen;
    private Point _windowAtPointerDown;
    private Point _roamTarget;
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
    private BitmapSource[]? _activeInteractionSprites;

    public PetWindow()
    {
        Title = "李橘与 Leagle · 八帧互动桌宠";
        Width = BaseWidth;
        Height = BaseHeight;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        _petSprites[PetKind.LiJu] = LoadLiJuSprites();
        _petSprites[PetKind.Leagle] = LoadLeagleSprites();
        ApplyPetSprites(_petSprites[_currentPetKind]);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _speechText = new TextBlock
        {
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Microsoft YaHei UI"),
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
            Padding = new Thickness(14, 9, 14, 9),
            Margin = new Thickness(12, 5, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
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
            Margin = new Thickness(8, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            RenderTransformOrigin = new Point(0.5, 0.72),
            RenderTransform = transforms,
            Cursor = Cursors.Hand,
            ToolTip = "单击摸摸 · 双击喂食 · 拖文件给我放进回收站 · 右键菜单"
        };
        Grid.SetRow(_catImage, 1);

        _recycleItemLabel = new TextBlock
        {
            Text = "文件",
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(74, 105, 132)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
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
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 7, 0, 0),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = recycleItemTransforms,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        Grid.SetRow(_recycleItemVisual, 1);
        Panel.SetZIndex(_recycleItemVisual, 2);

        root.Children.Add(_speechBubble);
        root.Children.Add(_catImage);
        root.Children.Add(_recycleItemVisual);
        Content = root;

        _followMouseMenuItem = new MenuItem
        {
            Header = "跟随鼠标",
            IsCheckable = true,
            IsChecked = false
        };
        _followMouseMenuItem.Click += (_, _) =>
        {
            _hasRoamTarget = false;
            if (_followMouseMenuItem.IsChecked)
            {
                EndIdleAction();
                _followPausedUntil = DateTime.UtcNow.AddSeconds(1);
                Say("我来追你啦！");
            }
            else
            {
                StopWalking();
                Say("我先在这里待着～");
            }
            UpdateMotionTimerState();
        };

        _roamMenuItem = new MenuItem
        {
            Header = "自动散步（走走停停）",
            IsCheckable = true,
            IsChecked = false
        };
        _roamMenuItem.Click += (_, _) =>
        {
            _hasRoamTarget = false;
            if (_roamMenuItem.IsChecked)
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

        _topmostMenuItem = new MenuItem
        {
            Header = "始终置顶",
            IsCheckable = true,
            IsChecked = true
        };
        _topmostMenuItem.Click += (_, _) => Topmost = _topmostMenuItem.IsChecked;

        _catImage.ContextMenu = BuildContextMenu();
        _foodMenu = BuildFoodMenu();
        _catImage.MouseLeftButtonDown += OnCatMouseDown;
        _catImage.MouseMove += OnCatMouseMove;
        _catImage.MouseLeftButtonUp += OnCatMouseUp;
        _catImage.MouseWheel += OnCatMouseWheel;
        _catImage.AllowDrop = true;
        _catImage.DragEnter += OnCatDragEnter;
        _catImage.DragOver += OnCatDragOver;
        _catImage.DragLeave += OnCatDragLeave;
        _catImage.Drop += OnCatDrop;

        _motionTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
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

        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _motionTimer.Stop();
            _roamTimer.Stop();
            _blinkTimer.Stop();
            _blinkRestoreTimer.Stop();
            _speechTimer.Stop();
            _idleActionTimer.Stop();
            _foodMenu.IsOpen = false;
        };

        StartBreathing();
    }

    private static BitmapSource LoadSprite(string resourceName)
    {
        var sprite = new BitmapImage();
        using (var spriteStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
        {
            if (spriteStream == null)
            {
                throw new InvalidOperationException("Missing sprite resource: " + resourceName);
            }

            sprite.BeginInit();
            sprite.StreamSource = spriteStream;
            sprite.CacheOption = BitmapCacheOption.OnLoad;
            sprite.EndInit();
        }

        sprite.Freeze();
        return sprite;
    }

    private static BitmapSource[] LoadSpriteSequence(string petPrefix, string action)
    {
        return Enumerable.Range(1, AnimationFrameCount)
            .Select(index => LoadSprite($"OrangeCatPet.Assets.{petPrefix}-{action}-{index:00}.png"))
            .ToArray();
    }

    private static PetSpriteSet LoadLiJuSprites()
    {
        return new PetSpriteSet
        {
            Idle = LoadSprite("OrangeCatPet.Assets.cat-smooth-idle-v2.png"),
            Blink = LoadSprite("OrangeCatPet.Assets.cat-smooth-blink-v2.png"),
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
            Idle = LoadSprite("OrangeCatPet.Assets.dog-idle.png"),
            Blink = LoadSprite("OrangeCatPet.Assets.dog-blink-04.png"),
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
        var menu = new ContextMenu
        {
            FontFamily = new FontFamily("Microsoft YaHei UI")
        };

        var greetItem = new MenuItem { Header = "打个招呼" };
        greetItem.Click += (_, _) =>
        {
            React();
            var greetings = _currentPetKind == PetKind.Leagle
                ? LeagleGreetingMessages
                : LiJuGreetingMessages;
            Say(greetings[_random.Next(greetings.Length)]);
        };

        var petMenu = new MenuItem { Header = "更换宠物" };
        AddPetItem(petMenu, "橘猫 · 李橘", PetKind.LiJu);
        AddPetItem(petMenu, "幼年比格犬 · Leagle", PetKind.Leagle);
        _petMenuItems[_currentPetKind].IsChecked = true;

        var feedMenu = new MenuItem { Header = "投喂" };
        AddFoodItems(feedMenu);

        var sizeMenu = new MenuItem { Header = "大小" };
        AddSizeItem(sizeMenu, "小", 0.78);
        AddSizeItem(sizeMenu, "中", 1.0);
        AddSizeItem(sizeMenu, "大", 1.28);
        _sizeMenuItems[1.0].IsChecked = true;

        var homeItem = new MenuItem { Header = "回到右下角" };
        homeItem.Click += (_, _) =>
        {
            _hasRoamTarget = false;
            PositionAtBottomRight();
            Say("我回来啦！");
        };

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();

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
        var item = new MenuItem
        {
            Header = label,
            IsCheckable = true
        };
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
            ? "汪～我来啦！我可一直在斜眼看你哦。"
            : "喵～李橘回来啦！");
    }

    private ContextMenu BuildFoodMenu()
    {
        var menu = new ContextMenu
        {
            FontFamily = new FontFamily("Microsoft YaHei UI")
        };
        AddFoodItems(menu);
        return menu;
    }

    private void AddFoodItems(ItemsControl parent)
    {
        AddFoodItem(parent, "小零食", FoodKind.DriedFish);
        AddFoodItem(parent, "罐头", FoodKind.CannedFood);
        AddFoodItem(parent, "鸡肉", FoodKind.Chicken);
    }

    private void AddFoodItem(ItemsControl parent, string label, FoodKind food)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => BeginFeeding(food);
        parent.Items.Add(item);
    }

    private void ShowFoodMenu()
    {
        if (_idleAction == IdleActionState.Feeding || _recycleOperationActive)
        {
            return;
        }

        Say("今天想吃什么？", 1.8);
        _foodMenu.PlacementTarget = _catImage;
        _foodMenu.Placement = PlacementMode.MousePoint;
        _foodMenu.IsOpen = true;
    }

    private void AddSizeItem(MenuItem parent, string label, double scale)
    {
        var item = new MenuItem
        {
            Header = label,
            IsCheckable = true
        };
        item.Click += (_, _) => SetPetSize(scale);
        _sizeMenuItems.Add(scale, item);
        parent.Items.Add(item);
    }

    private void UpdateMotionTimerState()
    {
        if (_followMouseMenuItem.IsChecked || _roamMenuItem.IsChecked)
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

    private void OnLoaded(object sender, RoutedEventArgs e)
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
            Say("闲着会舔毛、挠痒和睡觉；拖文件给我可以放进回收站～", 5.2);
        };
        introTimer.Start();
    }

    private void UpdateMotion()
    {
        if (_pointerCaptured ||
            _recycleOperationActive ||
            _foodMenu.IsOpen ||
            (_catImage.ContextMenu != null && _catImage.ContextMenu.IsOpen))
        {
            StopWalking();
            RelaxLook();
            return;
        }

        var windowRect = GetWindowRectInPixels();
        var dpiScale = GetWindowDpiScale();
        var visualCenterY = GetVisualCenterOffsetInPixels(windowRect, dpiScale);
        var target = new Point();
        var hasTarget = false;
        var isFollowingCursor =
            _followMouseMenuItem.IsChecked && DateTime.UtcNow >= _followPausedUntil;

        if (isFollowingCursor && GetCursorPos(out var cursor))
        {
            var cursorArea = GetMonitorWorkAreaAtPoint(cursor).WorkArea;
            target = new Point(
                Clamp(
                    cursor.X,
                    cursorArea.Left + windowRect.Width / 2.0,
                    cursorArea.Right - windowRect.Width / 2.0),
                Clamp(
                    cursor.Y,
                    cursorArea.Top + visualCenterY,
                    cursorArea.Bottom - windowRect.Height + visualCenterY));
            hasTarget = true;
        }
        else if (!_followMouseMenuItem.IsChecked && _roamMenuItem.IsChecked && _hasRoamTarget)
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

        target = RouteTargetAcrossMonitors(windowRect, target, visualCenterY);

        var catCenterX = windowRect.Left + windowRect.Width / 2.0;
        var catCenterY = windowRect.Top + visualCenterY;
        var deltaX = target.X - catCenterX;
        var deltaY = target.Y - catCenterY;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        UpdateLook(deltaX / dpiScale, deltaY / dpiScale);

        var movementThreshold =
            (_isWalking ? StopFollowingDistance : StartFollowingDistance) * dpiScale;
        if (distance <= movementThreshold)
        {
            var wasWalking = _isWalking;
            Decelerate();
            if (!_followMouseMenuItem.IsChecked && !_isWalking && (wasWalking || _hasRoamTarget))
            {
                BeginRoamRest();
            }
            return;
        }

        var maximumSpeed = (isFollowingCursor ? 23.0 : 16.5) * dpiScale;
        var desiredSpeed = Clamp(
            (distance - StopFollowingDistance * dpiScale) * 0.09,
            3.2 * dpiScale,
            maximumSpeed);
        var desiredVelocityX = deltaX / distance * desiredSpeed;
        var desiredVelocityY = deltaY / distance * desiredSpeed;
        var acceleration = isFollowingCursor ? 0.26 : 0.18;
        _velocityX += (desiredVelocityX - _velocityX) * acceleration;
        _velocityY += (desiredVelocityY - _velocityY) * acceleration;

        SetWindowPositionInPixels(
            windowRect.Left + _velocityX,
            windowRect.Top + _velocityY);
        ClampToWorkArea();

        var movedRect = GetWindowRectInPixels();
        if (Math.Abs(movedRect.Left - windowRect.Left) +
            Math.Abs(movedRect.Top - windowRect.Top) < 0.5)
        {
            StopWalking();
            if (!_followMouseMenuItem.IsChecked)
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

        var windowRect = GetWindowRectInPixels();
        SetWindowPositionInPixels(
            windowRect.Left + _velocityX,
            windowRect.Top + _velocityY);
        ClampToWorkArea();

        var movedRect = GetWindowRectInPixels();
        if (Math.Abs(movedRect.Left - windowRect.Left) +
            Math.Abs(movedRect.Top - windowRect.Top) < 0.5)
        {
            StopWalking();
            return;
        }

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
        _stepTransform.Y = -0.8 + Math.Sin(phase) * 2.0;
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
        _stepTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _stepTransform.BeginAnimation(TranslateTransform.YProperty, null);
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
        if (_followMouseMenuItem.IsChecked ||
            !_roamMenuItem.IsChecked ||
            _pointerCaptured ||
            _isWalking ||
            _hasRoamTarget ||
            _idleAction != IdleActionState.None ||
            DateTime.UtcNow < _roamRestUntil)
        {
            return;
        }

        var monitors = GetMonitorWorkAreas();
        if (monitors.Count == 0)
        {
            return;
        }

        var windowRect = GetWindowRectInPixels();
        var dpiScale = GetWindowDpiScale();
        var centerOffsetY = GetVisualCenterOffsetInPixels(windowRect, dpiScale);
        var catCenter = new Point(
            windowRect.Left + windowRect.Width / 2.0,
            windowRect.Top + centerOffsetY);
        var currentMonitor = GetMonitorWorkAreaAtPoint(
            new NativePoint((int)Math.Round(catCenter.X), (int)Math.Round(catCenter.Y)));

        MonitorWorkArea targetMonitor;
        if (monitors.Count > 1 && _random.NextDouble() < 0.55)
        {
            var otherMonitors = monitors
                .Where(monitor => !monitor.WorkArea.Equals(currentMonitor.WorkArea))
                .ToArray();
            targetMonitor = otherMonitors.Length > 0
                ? otherMonitors[_random.Next(otherMonitors.Length)]
                : currentMonitor;
        }
        else
        {
            targetMonitor = currentMonitor;
        }

        var area = targetMonitor.WorkArea;
        var margin = 22 * dpiScale;
        var minimumX = area.Left + windowRect.Width / 2.0 + margin;
        var maximumX = area.Right - windowRect.Width / 2.0 - margin;
        var minimumY = area.Top + centerOffsetY + margin;
        var maximumY = area.Bottom - windowRect.Height + centerOffsetY - margin;

        if (maximumX < minimumX)
        {
            minimumX = maximumX = (area.Left + area.Right) / 2.0;
        }
        if (maximumY < minimumY)
        {
            minimumY = maximumY = (area.Top + area.Bottom) / 2.0;
        }

        var target = catCenter;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            target = new Point(
                minimumX + _random.NextDouble() * Math.Max(0, maximumX - minimumX),
                minimumY + _random.NextDouble() * Math.Max(0, maximumY - minimumY));
            if (!targetMonitor.WorkArea.Equals(currentMonitor.WorkArea) ||
                (target - catCenter).Length >= 180 * dpiScale)
            {
                break;
            }
        }

        _roamTarget = target;
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
            if (now >= _nextIdleActionAt &&
                !_isWalking &&
                !_isBlinking &&
                !_pointerCaptured &&
                !_hasRoamTarget &&
                !_foodMenu.IsOpen &&
                (_catImage.ContextMenu == null || !_catImage.ContextMenu.IsOpen))
            {
                StartRandomIdleAction();
            }
            return;
        }

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
                _stepTransform.X = 0;
                _stepTransform.Y = 0;
                _lookTransform.Angle = 0;
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
                _stepTransform.X = 0;
                _stepTransform.Y = 0;
                _lookTransform.Angle = 0;
                break;
            case IdleActionState.Sleeping:
                SetAnimationFrame(
                    _sleepSprites,
                    elapsedSeconds,
                    2.5,
                    _currentPetKind != PetKind.Leagle,
                    false);
                _stepTransform.Y = 0;
                _lookTransform.Angle = 0;
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
        BitmapSource[] sprites,
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
        BitmapSource[] sprites,
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
        _stepTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _stepTransform.BeginAnimation(TranslateTransform.YProperty, null);
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
        _blinkRestoreTimer.Interval = TimeSpan.FromMilliseconds(180);
        _isBlinking = false;
        _catImage.Source = _isWalking ? _walkingSprites[_walkFrameIndex] : _idleSprite;
    }

    private Point GetCursorPositionInPixels()
    {
        return GetCursorPos(out var cursor)
            ? new Point(cursor.X, cursor.Y)
            : _pointerDownScreen;
    }
    private void OnCatMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _recycleOperationActive)
        {
            return;
        }

        _hasRoamTarget = false;
        EndIdleAction();
        StopWalking();
        _pointerDownScreen = GetCursorPositionInPixels();
        var windowRect = GetWindowRectInPixels();
        _windowAtPointerDown = new Point(windowRect.Left, windowRect.Top);
        _pointerCaptured = _catImage.CaptureMouse();
        _dragging = false;
        _doubleClick = e.ClickCount >= 2;
        e.Handled = true;
    }
    private void OnCatMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pointerCaptured || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentScreen = GetCursorPositionInPixels();
        var deltaX = currentScreen.X - _pointerDownScreen.X;
        var deltaY = currentScreen.Y - _pointerDownScreen.Y;

        if (!_dragging &&
            Math.Abs(deltaX) + Math.Abs(deltaY) > 5 * GetWindowDpiScale())
        {
            _dragging = true;
            HideSpeech();
        }

        if (_dragging)
        {
            SetWindowPositionInPixels(
                _windowAtPointerDown.X + deltaX,
                _windowAtPointerDown.Y + deltaY);
            ClampToWorkArea();
        }
    }
    private void OnCatMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_pointerCaptured)
        {
            return;
        }

        _catImage.ReleaseMouseCapture();
        _pointerCaptured = false;
        _followPausedUntil = DateTime.UtcNow.AddSeconds(_dragging ? 1.5 : 1.8);

        if (_dragging)
        {
            Say("好啦，我就在这里～", 2.4);
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

    private void OnCatMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scales = _sizeMenuItems.Keys.OrderBy(value => value).ToArray();
        var current = Width / BaseWidth;
        var index = Array.FindIndex(scales, value => Math.Abs(value - current) < 0.02);
        if (index < 0)
        {
            index = 1;
        }
        index = e.Delta > 0 ? Math.Min(scales.Length - 1, index + 1) : Math.Max(0, index - 1);
        SetPetSize(scales[index]);
        e.Handled = true;
    }

    private void OnCatDragEnter(object sender, DragEventArgs e)
    {
        var paths = GetDroppedPaths(e.Data);
        if (_recycleOperationActive || paths.Length == 0)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        ShowRecycleItemVisual(paths);
        Say("松开后，我会把它放进回收站～", 2.2);
        e.Handled = true;
    }

    private void OnCatDragOver(object sender, DragEventArgs e)
    {
        e.Effects = !_recycleOperationActive && GetDroppedPaths(e.Data).Length > 0
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCatDragLeave(object sender, DragEventArgs e)
    {
        if (!_recycleOperationActive)
        {
            HideRecycleItemVisual();
            HideSpeech();
        }
        e.Handled = true;
    }

    private async void OnCatDrop(object sender, DragEventArgs e)
    {
        var paths = GetDroppedPaths(e.Data);
        e.Handled = true;

        if (_recycleOperationActive || paths.Length == 0)
        {
            e.Effects = DragDropEffects.None;
            HideRecycleItemVisual();
            Say("这个不能放进回收站。", 2.2);
            return;
        }

        e.Effects = DragDropEffects.Move;
        _recycleOperationActive = true;
        _hasRoamTarget = false;
        StopWalking();
        StartInteractionAnimation(IdleActionState.Recycling, _fishFeedingSprites, 1.8);
        ShowRecycleItemVisual(paths);
        Say(
            _currentPetKind == PetKind.Leagle
                ? paths.Length == 1 ? "嗷呜！我来处理～" : $"嗷呜！这 {paths.Length} 项交给我～"
                : paths.Length == 1 ? "喵呜！李橘来处理～" : $"喵呜！这 {paths.Length} 项交给李橘～",
            2.2);

        try
        {
            await Task.Delay(700);
            var result = await Task.Run(() => WindowsRecycleBin.Move(paths));
            ShowRecycleResult(result);
        }
        finally
        {
            _recycleOperationActive = false;
        }
    }

    private static string[] GetDroppedPaths(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop, true) ||
            data.GetData(DataFormats.FileDrop, true) is not string[] paths)
        {
            return Array.Empty<string>();
        }

        return WindowsRecycleBin.GetSupportedPaths(paths);
    }

    private void ShowRecycleItemVisual(IReadOnlyCollection<string> paths)
    {
        var isSingleDirectory = paths.Count == 1 && System.IO.Directory.Exists(paths.First());
        _recycleItemLabel.Text = paths.Count > 1 ? $"{paths.Count} 项" : isSingleDirectory ? "文件夹" : "文件";
        _recycleItemScale.ScaleX = 1;
        _recycleItemScale.ScaleY = 1;
        _recycleItemRotation.Angle = -4;
        _recycleItemOffset.X = 0;
        _recycleItemOffset.Y = 0;
        _recycleItemVisual.Opacity = 1;
        _recycleItemVisual.Visibility = Visibility.Visible;
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
        _recycleItemVisual.Visibility = Visibility.Collapsed;
        _recycleItemVisual.Opacity = 0;
    }

    private void ShowRecycleResult(WindowsRecycleBin.MoveResult result)
    {
        if (result.SucceededCount > 0 && result.FailedCount == 0)
        {
            Say(
                result.SucceededCount == 1
                    ? "已经放进回收站啦！"
                    : $"都放进回收站啦，共 {result.SucceededCount} 项！",
                3);
        }
        else if (result.SucceededCount > 0)
        {
            Say($"放进去 {result.SucceededCount} 项，另有 {result.FailedCount} 项失败了。", 3.4);
        }
        else
        {
            Say("这个我吃不掉，没有移动任何东西。", 3.2);
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
                "再摸摸我的脑袋！",
                "这里最舒服啦～",
                "尾巴快要摇起来了！",
                "我最喜欢这个！",
                "别停，再摸一下～"
            }
            : new[]
            {
                "呼噜呼噜～",
                "再摸一下！",
                "喵呜～",
                "今天也要开心呀！",
                "我在陪你。"
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
                    "我的小肚子已经圆滚滚啦！",
                    "吃饱了，我要趴一会儿～",
                    "再吃就跑不动啦！"
                }
                : new[]
                {
                    "肚子已经圆滚滚啦，等会儿再吃～",
                    "我吃饱啦，先休息一下吧！",
                    "再吃就要走不动啦～"
                };
            StartInteractionAnimation(IdleActionState.Petting, _pettingSprites, 1.25);
            Say(fullMessages[_random.Next(fullMessages.Length)], 3.1);
            return;
        }

        _recentFeedings.Enqueue(now);
        BitmapSource[] sprites;
        string[] messages;
        switch (food)
        {
            case FoodKind.CannedFood:
                sprites = _cannedFoodFeedingSprites;
                messages = _currentPetKind == PetKind.Leagle
                    ? new[] { "我闻到罐头啦！", "我要大口吃！", "这个味道我记住了！" }
                    : new[] { "罐头好香呀！", "大口吃罐罐～", "这个味道我喜欢！" };
                break;
            case FoodKind.Chicken:
                sprites = _chickenFeedingSprites;
                messages = _currentPetKind == PetKind.Leagle
                    ? new[] { "鸡肉！快给我！", "嗷呜一大口！", "吃完还想去玩～" }
                    : new[] { "鸡肉真好吃！", "嗷呜一大口～", "谢谢你的鸡肉！" };
                break;
            default:
                sprites = _fishFeedingSprites;
                messages = _currentPetKind == PetKind.Leagle
                    ? new[] { "发现小零食！", "咔嚓咔嚓，真香！", "我还想要一块～" }
                    : new[] { "小鱼干真香！", "咔嚓咔嚓～", "最喜欢小鱼干啦！" };
                break;
        }

        StartInteractionAnimation(IdleActionState.Feeding, sprites, 1.4);
        Say(messages[_random.Next(messages.Length)], 2.8);
    }

    private void StartInteractionAnimation(IdleActionState state, BitmapSource[] sprites, double durationSeconds)
    {
        _hasRoamTarget = false;
        StopWalking();
        EndIdleAction();
        _blinkRestoreTimer.Stop();
        _isBlinking = false;
        _stepTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _stepTransform.BeginAnimation(TranslateTransform.YProperty, null);
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
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var frames = new DoubleAnimationUsingKeyFrames();
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(peakScale, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130)), easing));
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(0.97, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)), easing));
        frames.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(410)), easing));
        _reactionTransform.BeginAnimation(ScaleTransform.ScaleXProperty, frames);
        _reactionTransform.BeginAnimation(ScaleTransform.ScaleYProperty, frames.Clone());

        var hop = new DoubleAnimationUsingKeyFrames();
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(-11, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140)), easing));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360)), easing));
        hop.FillBehavior = FillBehavior.Stop;
        _stepTransform.BeginAnimation(TranslateTransform.YProperty, hop);
    }

    private void StartBreathing()
    {
        var breathing = new DoubleAnimation
        {
            From = 0.985,
            To = 1.025,
            Duration = TimeSpan.FromSeconds(1.15),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        _breathingTransform.BeginAnimation(ScaleTransform.ScaleYProperty, breathing);
    }

    private void Say(string message, double seconds = 2.3)
    {
        _speechTimer.Stop();
        _speechTimer.Interval = TimeSpan.FromSeconds(seconds);
        _speechText.Text = message;
        _speechBubble.BeginAnimation(OpacityProperty, null);
        _speechBubble.Opacity = 1;
        _speechBubble.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        _speechTimer.Start();
    }

    private void HideSpeech()
    {
        _speechTimer.Stop();
        _speechBubble.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void SetPetSize(double scale)
    {
        var oldBottom = Top + ActualHeight;
        Width = BaseWidth * scale;
        Height = BaseHeight * scale;
        Top = oldBottom - Height;

        foreach (var pair in _sizeMenuItems)
        {
            pair.Value.IsChecked = Math.Abs(pair.Key - scale) < 0.01;
        }

        ClampToWorkArea();
        Say(scale < 0.9 ? "迷你模式！" : scale > 1.1 ? "大橘登场！" : "刚刚好～");
    }

    private void PositionAtBottomRight()
    {
        var windowRect = GetWindowRectInPixels();
        var area = GetPrimaryMonitorWorkArea().WorkArea;
        SetWindowPositionInPixels(
            area.Right - windowRect.Width - 18 * GetWindowDpiScale(),
            area.Bottom - windowRect.Height - 8 * GetWindowDpiScale());
        ClampToWorkArea();
    }

    private void ClampToWorkArea()
    {
        var windowRect = GetWindowRectInPixels();
        var monitors = GetMonitorWorkAreas();
        if (monitors.Count == 0)
        {
            return;
        }

        var intersectingMonitors = monitors
            .Where(monitor => RectanglesIntersect(windowRect, monitor.MonitorArea))
            .ToArray();

        // A window that overlaps two work areas is crossing a monitor boundary.
        // Do not clamp it back to the old monitor while that transition is in progress.
        if (intersectingMonitors.Length >= 2)
        {
            return;
        }

        var targetArea = intersectingMonitors.Length == 1
            ? intersectingMonitors[0].WorkArea
            : monitors
                .OrderBy(monitor => DistanceSquaredToRectangle(
                    windowRect.CenterX,
                    windowRect.CenterY,
                    monitor.WorkArea))
                .First()
                .WorkArea;

        var maximumLeft = Math.Max(targetArea.Left, targetArea.Right - windowRect.Width);
        var maximumTop = Math.Max(targetArea.Top, targetArea.Bottom - windowRect.Height);
        var clampedLeft = Clamp(windowRect.Left, targetArea.Left, maximumLeft);
        var clampedTop = Clamp(windowRect.Top, targetArea.Top, maximumTop);

        if (Math.Abs(clampedLeft - windowRect.Left) >= 0.5 ||
            Math.Abs(clampedTop - windowRect.Top) >= 0.5)
        {
            SetWindowPositionInPixels(clampedLeft, clampedTop);
        }
    }

    private Point RouteTargetAcrossMonitors(
        NativeRect windowRect,
        Point desiredTarget,
        double visualCenterY)
    {
        var currentCenter = new Point(
            windowRect.Left + windowRect.Width / 2.0,
            windowRect.Top + visualCenterY);
        var currentMonitor = GetMonitorWorkAreaAtPoint(
            new NativePoint(
                (int)Math.Round(currentCenter.X),
                (int)Math.Round(currentCenter.Y)));
        var targetMonitor = GetMonitorWorkAreaAtPoint(
            new NativePoint(
                (int)Math.Round(desiredTarget.X),
                (int)Math.Round(desiredTarget.Y)));

        if (currentMonitor.WorkArea.Equals(targetMonitor.WorkArea))
        {
            return desiredTarget;
        }

        var currentArea = currentMonitor.WorkArea;
        var targetArea = targetMonitor.WorkArea;
        var halfWidth = windowRect.Width / 2.0;
        var currentMinimumX = currentArea.Left + halfWidth;
        var currentMaximumX = currentArea.Right - halfWidth;
        var targetMinimumX = targetArea.Left + halfWidth;
        var targetMaximumX = targetArea.Right - halfWidth;
        var currentMinimumY = currentArea.Top + visualCenterY;
        var currentMaximumY = currentArea.Bottom - windowRect.Height + visualCenterY;
        var targetMinimumY = targetArea.Top + visualCenterY;
        var targetMaximumY = targetArea.Bottom - windowRect.Height + visualCenterY;
        var sharedMinimumY = Math.Max(currentMinimumY, targetMinimumY);
        var sharedMaximumY = Math.Min(currentMaximumY, targetMaximumY);
        var sharedMinimumX = Math.Max(currentMinimumX, targetMinimumX);
        var sharedMaximumX = Math.Min(currentMaximumX, targetMaximumX);

        if (targetArea.Left >= currentArea.Right && sharedMinimumY <= sharedMaximumY)
        {
            return new Point(
                targetArea.Left + halfWidth + 8,
                Clamp(currentCenter.Y, sharedMinimumY, sharedMaximumY));
        }

        if (targetArea.Right <= currentArea.Left && sharedMinimumY <= sharedMaximumY)
        {
            return new Point(
                targetArea.Right - halfWidth - 8,
                Clamp(currentCenter.Y, sharedMinimumY, sharedMaximumY));
        }

        if (targetArea.Top >= currentArea.Bottom && sharedMinimumX <= sharedMaximumX)
        {
            return new Point(
                Clamp(currentCenter.X, sharedMinimumX, sharedMaximumX),
                targetArea.Top + visualCenterY + 8);
        }

        if (targetArea.Bottom <= currentArea.Top && sharedMinimumX <= sharedMaximumX)
        {
            return new Point(
                Clamp(currentCenter.X, sharedMinimumX, sharedMaximumX),
                targetArea.Bottom - windowRect.Height + visualCenterY - 8);
        }

        return desiredTarget;
    }

    private NativeRect GetWindowRectInPixels()
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero && GetWindowRect(windowHandle, out var windowRect))
        {
            return windowRect;
        }

        var dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var actualWidth = ActualWidth > 0 ? ActualWidth : Width;
        var actualHeight = ActualHeight > 0 ? ActualHeight : Height;
        var left = double.IsNaN(Left) ? 0 : Left;
        var top = double.IsNaN(Top) ? 0 : Top;
        return new NativeRect(
            (int)Math.Round(left * dpiScale),
            (int)Math.Round(top * dpiScale),
            (int)Math.Round((left + actualWidth) * dpiScale),
            (int)Math.Round((top + actualHeight) * dpiScale));
    }

    private double GetWindowDpiScale()
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero)
        {
            var dpi = GetDpiForWindow(windowHandle);
            if (dpi > 0)
            {
                return dpi / 96.0;
            }
        }

        return VisualTreeHelper.GetDpi(this).DpiScaleX;
    }

    private static double GetVisualCenterOffsetInPixels(
        NativeRect windowRect,
        double dpiScale)
    {
        var speechRowHeight = 78 * dpiScale;
        return speechRowHeight +
               Math.Max(0, windowRect.Height - speechRowHeight) * 0.52;
    }

    private void SetWindowPositionInPixels(double left, double top)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero)
        {
            _ = SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                (int)Math.Round(left),
                (int)Math.Round(top),
                0,
                0,
                SwpNoSize | SwpNoZOrder | SwpNoActivate);
            return;
        }

        var dpiScale = GetWindowDpiScale();
        Left = left / dpiScale;
        Top = top / dpiScale;
    }

    private static IReadOnlyList<MonitorWorkArea> GetMonitorWorkAreas()
    {
        var monitors = new List<MonitorWorkArea>();
        MonitorEnumProc callback = (monitorHandle, _, _, _) =>
        {
            if (TryGetMonitorWorkArea(monitorHandle, out var monitor))
            {
                monitors.Add(monitor);
            }
            return true;
        };

        _ = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        return monitors;
    }

    private static MonitorWorkArea GetMonitorWorkAreaAtPoint(NativePoint point)
    {
        var monitorHandle = MonitorFromPoint(point, MonitorDefaultToNearest);
        if (monitorHandle != IntPtr.Zero &&
            TryGetMonitorWorkArea(monitorHandle, out var monitor))
        {
            return monitor;
        }

        return GetPrimaryMonitorWorkArea();
    }

    private static MonitorWorkArea GetPrimaryMonitorWorkArea()
    {
        var monitors = GetMonitorWorkAreas();
        if (monitors.Count > 0)
        {
            return monitors.FirstOrDefault(monitor => monitor.IsPrimary, monitors[0]);
        }

        var virtualScreen = new NativeRect(
            GetSystemMetrics(SmXVirtualScreen),
            GetSystemMetrics(SmYVirtualScreen),
            GetSystemMetrics(SmXVirtualScreen) + GetSystemMetrics(SmCxVirtualScreen),
            GetSystemMetrics(SmYVirtualScreen) + GetSystemMetrics(SmCyVirtualScreen));
        return new MonitorWorkArea(virtualScreen, virtualScreen, true);
    }

    private static bool TryGetMonitorWorkArea(
        IntPtr monitorHandle,
        out MonitorWorkArea monitor)
    {
        var monitorInfo = new MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<MonitorInfo>()
        };
        if (GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            monitor = new MonitorWorkArea(
                monitorInfo.MonitorArea,
                monitorInfo.WorkArea,
                (monitorInfo.Flags & PrimaryMonitorFlag) != 0);
            return true;
        }

        monitor = default;
        return false;
    }

    private static bool RectanglesIntersect(NativeRect left, NativeRect right)
    {
        return left.Left < right.Right &&
               left.Right > right.Left &&
               left.Top < right.Bottom &&
               left.Bottom > right.Top;
    }

    private static double DistanceSquaredToRectangle(
        double x,
        double y,
        NativeRect rectangle)
    {
        var deltaX = x < rectangle.Left
            ? rectangle.Left - x
            : x > rectangle.Right
                ? x - rectangle.Right
                : 0;
        var deltaY = y < rectangle.Top
            ? rectangle.Top - y
            : y > rectangle.Bottom
                ? y - rectangle.Bottom
                : 0;
        return deltaX * deltaX + deltaY * deltaY;
    }
    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }

    private delegate bool MonitorEnumProc(
        IntPtr monitorHandle,
        IntPtr monitorDc,
        IntPtr monitorRectangle,
        IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clipRectangle,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(
        NativePoint point,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        IntPtr monitorHandle,
        ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public NativeRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
        public readonly double CenterX => (Left + Right) / 2.0;
        public readonly double CenterY => (Top + Bottom) / 2.0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }
}





























