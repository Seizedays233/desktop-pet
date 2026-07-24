using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OrangeCatPet;

internal sealed class PetWindow : Window
{
    private enum IdleActionState
    {
        None,
        Grooming,
        Scratching,
        Sleeping,
        Feeding,
        Petting
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

    private static readonly string[] GreetingMessages =
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

    private readonly Random _random = new();
    private readonly Border _speechBubble;
    private readonly TextBlock _speechText;
    private readonly Image _catImage;
    private readonly BitmapSource _idleSprite;
    private readonly BitmapSource _blinkSprite;
    private readonly BitmapSource[] _groomSprites;
    private readonly BitmapSource[] _scratchSprites;
    private readonly BitmapSource[] _sleepSprites;
    private readonly BitmapSource[] _walkingSprites;
    private readonly BitmapSource[] _pettingSprites;
    private readonly BitmapSource[] _fishFeedingSprites;
    private readonly BitmapSource[] _cannedFoodFeedingSprites;
    private readonly BitmapSource[] _chickenFeedingSprites;
    private readonly ScaleTransform _facingTransform = new(1, 1);
    private readonly ScaleTransform _breathingTransform = new(1, 1);
    private readonly ScaleTransform _reactionTransform = new(1, 1);
    private readonly RotateTransform _lookTransform = new();
    private readonly TranslateTransform _lookOffsetTransform = new();
    private readonly TranslateTransform _stepTransform = new();
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
    private BitmapSource[]? _activeInteractionSprites;

    public PetWindow()
    {
        Title = "橘猫桌宠 · 八帧互动版";
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

        _idleSprite = LoadSprite("OrangeCatPet.Assets.cat.png");
        _blinkSprite = LoadSprite("OrangeCatPet.Assets.cat-blink.png");
        _groomSprites = LoadSpriteSequence("groom");
        _scratchSprites = LoadSpriteSequence("scratch");
        _sleepSprites = LoadSpriteSequence("sleep");
        _walkingSprites = LoadSpriteSequence("walk");
        _pettingSprites = LoadSpriteSequence("pat");
        _fishFeedingSprites = LoadSpriteSequence("feed");
        _cannedFoodFeedingSprites = LoadSpriteSequence("feed-can");
        _chickenFeedingSprites = LoadSpriteSequence("feed-chicken");

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
            ToolTip = "会眨眼和走路 · 跟随鼠标 · 单击摸摸 · 双击喂食 · 拖动移动 · 右键菜单"
        };
        Grid.SetRow(_catImage, 1);

        root.Children.Add(_speechBubble);
        root.Children.Add(_catImage);
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

    private static BitmapSource[] LoadSpriteSequence(string action)
    {
        return Enumerable.Range(1, AnimationFrameCount)
            .Select(index => LoadSprite($"OrangeCatPet.Assets.cat-{action}-{index:00}.png"))
            .ToArray();
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
            Say(GreetingMessages[_random.Next(GreetingMessages.Length)]);
        };

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
        AddFoodItem(parent, "小鱼干", FoodKind.DriedFish);
        AddFoodItem(parent, "猫罐头", FoodKind.CannedFood);
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
        if (_idleAction == IdleActionState.Feeding)
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
            BeginFeeding(FoodKind.CannedFood);
        };
        introTimer.Start();
    }

    private void UpdateMotion()
    {
        if (_pointerCaptured || _foodMenu.IsOpen || (_catImage.ContextMenu != null && _catImage.ContextMenu.IsOpen))
        {
            StopWalking();
            RelaxLook();
            return;
        }

        Point target;
        var hasTarget = false;

        if (_followMouseMenuItem.IsChecked && DateTime.UtcNow >= _followPausedUntil)
        {
            NativePoint cursor;
            if (GetCursorPos(out cursor))
            {
                var area = SystemParameters.WorkArea;
                var visualCenterY = 78 + Math.Max(0, ActualHeight - 78) * 0.52;
                target = new Point(
                    Clamp(cursor.X, area.Left + ActualWidth / 2, area.Right - ActualWidth / 2),
                    Clamp(cursor.Y, area.Top + visualCenterY, area.Bottom - ActualHeight + visualCenterY));
                hasTarget = true;
            }
            else
            {
                target = new Point();
            }
        }
        else if (!_followMouseMenuItem.IsChecked && _roamMenuItem.IsChecked && _hasRoamTarget)
        {
            target = _roamTarget;
            hasTarget = true;
        }
        else
        {
            target = new Point();
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

        var catCenterX = Left + ActualWidth / 2;
        var catCenterY = Top + 78 + Math.Max(0, ActualHeight - 78) * 0.52;
        var deltaX = target.X - catCenterX;
        var deltaY = target.Y - catCenterY;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        UpdateLook(deltaX, deltaY);

        var movementThreshold = _isWalking ? StopFollowingDistance : StartFollowingDistance;
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

        var desiredSpeed = Clamp((distance - StopFollowingDistance) * 0.09, 3.2, 16.5);
        var desiredVelocityX = deltaX / distance * desiredSpeed;
        var desiredVelocityY = deltaY / distance * desiredSpeed;
        _velocityX += (desiredVelocityX - _velocityX) * 0.18;
        _velocityY += (desiredVelocityY - _velocityY) * 0.18;

        var oldLeft = Left;
        var oldTop = Top;
        Left += _velocityX;
        Top += _velocityY;
        ClampToWorkArea();

        if (Math.Abs(Left - oldLeft) + Math.Abs(Top - oldTop) < 0.05)
        {
            StopWalking();
            if (_followMouseMenuItem.IsChecked)
            {
                _hasRoamTarget = false;
            }
            else
            {
                BeginRoamRest();
            }
            return;
        }

        _facingTransform.ScaleX = deltaX >= 0 ? -1 : 1;
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

        var oldLeft = Left;
        var oldTop = Top;
        Left += _velocityX;
        Top += _velocityY;
        ClampToWorkArea();

        if (Math.Abs(Left - oldLeft) + Math.Abs(Top - oldTop) < 0.05)
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
        var cycleProgress = (elapsedMilliseconds % 800) / 800;
        var frameIndex = (int)(elapsedMilliseconds / 100) % _walkingSprites.Length;
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

        var area = SystemParameters.WorkArea;
        var centerOffsetY = 78 + Math.Max(0, ActualHeight - 78) * 0.52;
        var catCenterX = Left + ActualWidth / 2;
        var catCenterY = Top + centerOffsetY;
        var minimumX = area.Left + ActualWidth / 2;
        var maximumX = area.Right - ActualWidth / 2;
        var minimumY = area.Top + centerOffsetY;
        var maximumY = area.Bottom - ActualHeight + centerOffsetY;

        var goRight = _random.Next(2) == 0;
        var availableRoom = goRight ? maximumX - catCenterX : catCenterX - minimumX;
        var oppositeRoom = goRight ? catCenterX - minimumX : maximumX - catCenterX;
        if (availableRoom < 180 && oppositeRoom > availableRoom)
        {
            goRight = !goRight;
            availableRoom = oppositeRoom;
        }

        var travelDistance = Math.Min(availableRoom, 200 + _random.NextDouble() * 320);
        var targetX = catCenterX + (goRight ? travelDistance : -travelDistance);
        var targetY = Clamp(catCenterY + _random.Next(-70, 71), minimumY, maximumY);
        _roamTarget = new Point(Clamp(targetX, minimumX, maximumX), targetY);
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
                SetAnimationFrame(_groomSprites, elapsedSeconds, 8, true);
                _stepTransform.Y = Math.Sin(elapsedSeconds * 5.2) * 0.25;
                _lookTransform.Angle = 0;
                break;
            case IdleActionState.Scratching:
                SetAnimationFrame(_scratchSprites, elapsedSeconds, 10, true);
                _stepTransform.X = Math.Sin(elapsedSeconds * 20) * 0.2;
                _stepTransform.Y = 0;
                _lookTransform.Angle = 0;
                break;
            case IdleActionState.Sleeping:
                SetAnimationFrame(_sleepSprites, elapsedSeconds, 3, true);
                _stepTransform.Y = 0;
                _lookTransform.Angle = 0;
                break;
            case IdleActionState.Feeding:
                if (_activeInteractionSprites != null)
                {
                    SetAnimationFrame(_activeInteractionSprites, elapsedSeconds, 9, false);
                }
                break;
            case IdleActionState.Petting:
                if (_activeInteractionSprites != null)
                {
                    SetAnimationFrame(_activeInteractionSprites, elapsedSeconds, 8, false);
                }
                break;
        }
    }

    private void SetAnimationFrame(BitmapSource[] sprites, double elapsedSeconds, double framesPerSecond, bool loop)
    {
        var rawIndex = (int)(elapsedSeconds * framesPerSecond);
        var frameIndex = loop ? rawIndex % sprites.Length : Math.Min(sprites.Length - 1, rawIndex);
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
            _idleActionEnds = _idleActionStarted.AddSeconds(5 + _random.NextDouble() * 3);
            _catImage.Source = _groomSprites[0];
        }
        else if (selection < 0.74)
        {
            _idleAction = IdleActionState.Scratching;
            _idleActionEnds = _idleActionStarted.AddSeconds(3.5 + _random.NextDouble() * 2.5);
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

    private Point GetCursorPositionInDips()
    {
        NativePoint cursor;
        if (!GetCursorPos(out cursor))
        {
            return _pointerDownScreen;
        }

        var screenPoint = new Point(cursor.X, cursor.Y);
        var presentationSource = PresentationSource.FromVisual(this);
        if (presentationSource != null && presentationSource.CompositionTarget != null)
        {
            return presentationSource.CompositionTarget.TransformFromDevice.Transform(screenPoint);
        }

        return screenPoint;
    }

    private void OnCatMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _hasRoamTarget = false;
        EndIdleAction();
        StopWalking();
        _pointerDownScreen = GetCursorPositionInDips();
        _windowAtPointerDown = new Point(Left, Top);
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

        var currentScreen = GetCursorPositionInDips();
        var deltaX = currentScreen.X - _pointerDownScreen.X;
        var deltaY = currentScreen.Y - _pointerDownScreen.Y;

        if (!_dragging && Math.Abs(deltaX) + Math.Abs(deltaY) > 5)
        {
            _dragging = true;
            HideSpeech();
        }

        if (_dragging)
        {
            Left = _windowAtPointerDown.X + deltaX;
            Top = _windowAtPointerDown.Y + deltaY;
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
        _followPausedUntil = DateTime.UtcNow.AddSeconds(_dragging ? 5 : 1.8);

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

    private void Pat()
    {
        var messages = new[]
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
        var now = DateTime.UtcNow;
        while (_recentFeedings.Count > 0 && (now - _recentFeedings.Peek()).TotalSeconds > 55)
        {
            _recentFeedings.Dequeue();
        }

        if (_recentFeedings.Count >= 3)
        {
            var fullMessages = new[]
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
                messages = new[] { "罐头好香呀！", "大口吃罐罐～", "这个味道我喜欢！" };
                break;
            case FoodKind.Chicken:
                sprites = _chickenFeedingSprites;
                messages = new[] { "鸡肉真好吃！", "嗷呜一大口～", "谢谢你的鸡肉！" };
                break;
            default:
                sprites = _fishFeedingSprites;
                messages = new[] { "小鱼干真香！", "咔嚓咔嚓～", "最喜欢小鱼干啦！" };
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
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 18;
        Top = area.Bottom - Height - 8;
    }

    private void ClampToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        Left = Clamp(Left, area.Left, Math.Max(area.Left, area.Right - ActualWidth));
        Top = Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - ActualHeight));
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}


























