using BepInEx;
using SwyfLib;
using SwyfLib.Calls;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScammerPaybackPopup
{
    [BepInPlugin("org.scam-and-mod-with-friends.payback-popup", "Scammer Payback Popup", "1.0.0")]
    [BepInDependency("org.scam-and-mod-with-friends.swyflib")]
    public class Plugin : BaseUnityPlugin
    {
        static Plugin Instance = null!;
        UIDocument? _doc;
        VisualElement? _root;
        readonly List<PaybackWindow> _windows = new();
        static readonly System.Random _rng = new System.Random();

        // Pierogi-isms
        static readonly string[] _greetings = new[]
        {
            "How are you?",
            "Hello, how are you today?",
            "Oh hello there, how are you?",
            "Well hello! How are you doing?",
        };
        static readonly string[] _hangupLines = new[]
        {
            "You've been scammerpaybacked!",
            "That's a wrap! Pierogi out.",
            "Refund initiated. Just kidding.",
            "Did you enjoy that? I did.",
        };

        void Awake()
        {
            Instance = this;
            CallEvents.OnCallStarted += OnCallStarted;
            CallEvents.OnCallEnded   += OnCallEnded;
        }

        void OnCallStarted(CallInfo call)
        {
            StartCoroutine(SpawnAfterDelay(call, 1f));
        }

        void OnCallEnded(CallInfo call)
        {
            foreach (var w in _windows) w.Remove();
            _windows.Clear();
        }

        IEnumerator SpawnAfterDelay(CallInfo call, float delay)
        {
            yield return new WaitForSeconds(delay);
            EnsureDoc();
            SpawnWindow(call);
        }

        void EnsureDoc()
        {
            if (_doc != null && _doc.rootVisualElement != null) return;
            var go = new GameObject("ScammerPaybackPopupDoc");
            DontDestroyOnLoad(go);
            _doc = go.AddComponent<UIDocument>();
            // Sort on top of game UI
            _doc.sortingOrder = 9999;
            _root = _doc.rootVisualElement;
            _root.style.position = Position.Absolute;
            _root.style.left   = 0; _root.style.top    = 0;
            _root.style.right  = 0; _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;
        }

        internal void SpawnWindow(CallInfo call)
        {
            // Random screen position, biased towards center
            float sw = Screen.width;
            float sh = Screen.height;
            float x = _rng.Next((int)(sw * 0.1f), (int)(sw * 0.6f));
            float y = _rng.Next((int)(sh * 0.1f), (int)(sh * 0.6f));
            // Offset each duplicate so they don't stack exactly
            x += _windows.Count * 28f;
            y += _windows.Count * 24f;

            string greeting = _greetings[_rng.Next(_greetings.Length)];
            string hangup   = _hangupLines[_rng.Next(_hangupLines.Length)];

            var win = new PaybackWindow(call, _root!, x, y, greeting, hangup, this);
            _windows.Add(win);
        }

        internal void OnWindowClosed(PaybackWindow win, CallInfo call)
        {
            // Don't actually remove — spawn more
            int spawnCount = Math.Min(_windows.Count + 1, 8); // cap at 8 to avoid madness
            for (int i = 0; i < spawnCount; i++)
                SpawnWindow(call);
        }
    }

    sealed class PaybackWindow
    {
        readonly VisualElement _win;
        readonly VisualElement _root;
        readonly Label _charLabel;
        readonly CallInfo _call;
        readonly Plugin _plugin;
        bool _removed;

        // Bouncing character state
        float _charX, _charY, _vx, _vy;
        static readonly System.Random _rng = new System.Random();

        static readonly string[] _frames = new[]
        {
            "( •_•)",
            "( •_•)>⌐■-■",
            "(⌐■_■)",
            "(⌐■_■) 🕶️",
            "( •_•)",
        };
        int _frame;
        float _frameTimer;
        float _typingTimer;
        int _typingIdx;
        string _fullGreeting;
        bool _typed;

        // Drag state
        bool _dragging;
        Vector2 _dragOffset;
        float _winX, _winY;

        public PaybackWindow(CallInfo call, VisualElement root, float x, float y,
            string greeting, string hangupLine, Plugin plugin)
        {
            _call    = call;
            _root    = root;
            _plugin  = plugin;
            _fullGreeting = $"{greeting}\n\"How are you, {call.CallerName}?\"";
            _winX = x; _winY = y;

            // Window container
            _win = new VisualElement();
            _win.style.position = Position.Absolute;
            _win.style.left = x; _win.style.top = y;
            _win.style.width  = 320;
            _win.style.minHeight = 200;
            _win.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.97f);
            _win.style.borderTopLeftRadius     = 8;
            _win.style.borderTopRightRadius    = 8;
            _win.style.borderBottomLeftRadius  = 8;
            _win.style.borderBottomRightRadius = 8;
            _win.style.borderTopWidth     = 2;
            _win.style.borderBottomWidth  = 2;
            _win.style.borderLeftWidth    = 2;
            _win.style.borderRightWidth   = 2;
            _win.style.borderTopColor     = new Color(0.85f, 0.1f, 0.1f);
            _win.style.borderBottomColor  = new Color(0.85f, 0.1f, 0.1f);
            _win.style.borderLeftColor    = new Color(0.85f, 0.1f, 0.1f);
            _win.style.borderRightColor   = new Color(0.85f, 0.1f, 0.1f);

            // Title bar
            var titleBar = new VisualElement();
            titleBar.style.flexDirection     = FlexDirection.Row;
            titleBar.style.justifyContent   = Justify.SpaceBetween;
            titleBar.style.alignItems       = Align.Center;
            titleBar.style.backgroundColor  = new Color(0.85f, 0.1f, 0.1f);
            titleBar.style.paddingLeft  = 10;
            titleBar.style.paddingRight = 6;
            titleBar.style.paddingTop   = titleBar.style.paddingBottom = 5;
            titleBar.style.borderTopLeftRadius  = 6;
            titleBar.style.borderTopRightRadius = 6;

            var title = new Label("⚠ SCAMMER PAYBACK");
            title.style.color    = Color.white;
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            var closeBtn = new Button();
            closeBtn.text = "✕";
            closeBtn.style.color           = Color.white;
            closeBtn.style.backgroundColor = new Color(0.6f, 0.05f, 0.05f);
            closeBtn.style.width  = 24;
            closeBtn.style.height = 24;
            closeBtn.style.fontSize = 12;
            closeBtn.style.borderTopLeftRadius = closeBtn.style.borderTopRightRadius =
            closeBtn.style.borderBottomLeftRadius = closeBtn.style.borderBottomRightRadius = 4;
            closeBtn.clicked += OnClose;

            titleBar.Add(title);
            titleBar.Add(closeBtn);
            _win.Add(titleBar);

            // Drag on title bar
            titleBar.RegisterCallback<MouseDownEvent>(e => { _dragging = true; _dragOffset = new Vector2(_winX - e.mousePosition.x, _winY - e.mousePosition.y); e.StopPropagation(); });
            _win.RegisterCallback<MouseMoveEvent>(e => { if (!_dragging) return; _winX = e.mousePosition.x + _dragOffset.x; _winY = e.mousePosition.y + _dragOffset.y; _win.style.left = _winX; _win.style.top = _winY; });
            _win.RegisterCallback<MouseUpEvent>(_ => _dragging = false);

            // Character area (bouncing)
            var charArea = new VisualElement();
            charArea.style.height = 90;
            charArea.style.overflow = Overflow.Hidden;
            charArea.style.position = Position.Relative;
            charArea.style.borderBottomWidth = 1;
            charArea.style.borderBottomColor = new Color(0.85f, 0.1f, 0.1f, 0.4f);

            _charLabel = new Label(_frames[0]);
            _charLabel.style.position = Position.Absolute;
            _charLabel.style.color    = new Color(1f, 0.85f, 0.1f);
            _charLabel.style.fontSize = 20;
            _charLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _charX = 10; _charY = 20;
            _charLabel.style.left = _charX;
            _charLabel.style.top  = _charY;
            _vx = (float)(_rng.NextDouble() * 80 + 40) * (_rng.Next(2) == 0 ? 1 : -1);
            _vy = (float)(_rng.NextDouble() * 60 + 30) * (_rng.Next(2) == 0 ? 1 : -1);

            charArea.Add(_charLabel);
            _win.Add(charArea);

            // Typing greeting text
            var greeting_lbl = new Label("");
            greeting_lbl.name = "greeting";
            greeting_lbl.style.color   = new Color(0.9f, 0.9f, 0.9f);
            greeting_lbl.style.fontSize = 13;
            greeting_lbl.style.whiteSpace = WhiteSpace.Normal;
            greeting_lbl.style.paddingLeft = greeting_lbl.style.paddingRight = 12;
            greeting_lbl.style.paddingTop  = greeting_lbl.style.paddingBottom = 10;

            _win.Add(greeting_lbl);

            // Hang up button
            var hangupBtn = new Button();
            hangupBtn.text = "HANG UP";
            hangupBtn.style.backgroundColor = new Color(0.85f, 0.1f, 0.1f);
            hangupBtn.style.color           = Color.white;
            hangupBtn.style.marginLeft      = 12;
            hangupBtn.style.marginRight     = 12;
            hangupBtn.style.marginBottom    = 12;
            hangupBtn.style.paddingTop      = hangupBtn.style.paddingBottom = 8;
            hangupBtn.style.fontSize        = 14;
            hangupBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            hangupBtn.style.borderTopLeftRadius     = 4;
            hangupBtn.style.borderTopRightRadius    = 4;
            hangupBtn.style.borderBottomLeftRadius  = 4;
            hangupBtn.style.borderBottomRightRadius = 4;
            hangupBtn.clicked += () =>
            {
                call.HangUp();
                greeting_lbl.text = hangupLine;
            };
            _win.Add(hangupBtn);

            root.Add(_win);

            // Kick off animation via UIElements IVisualElementScheduler
            _win.schedule.Execute(Tick).Every(33); // ~30fps
        }

        void Tick()
        {
            if (_removed) return;
            float dt = 0.033f;

            // Bounce character
            _charX += _vx * dt;
            _charY += _vy * dt;
            if (_charX < 0)   { _charX = 0;   _vx = Mathf.Abs(_vx); }
            if (_charX > 260) { _charX = 260;  _vx = -Mathf.Abs(_vx); }
            if (_charY < 0)   { _charY = 0;    _vy = Mathf.Abs(_vy); }
            if (_charY > 60)  { _charY = 60;   _vy = -Mathf.Abs(_vy); }
            _charLabel.style.left = _charX;
            _charLabel.style.top  = _charY;

            // Cycle frame
            _frameTimer += dt;
            if (_frameTimer > 0.35f)
            {
                _frameTimer = 0;
                _frame = (_frame + 1) % _frames.Length;
                _charLabel.text = _frames[_frame];
            }

            // Typewriter greeting
            if (!_typed)
            {
                _typingTimer += dt;
                if (_typingTimer > 0.045f)
                {
                    _typingTimer = 0;
                    _typingIdx = Math.Min(_typingIdx + 1, _fullGreeting.Length);
                    var lbl = _win.Q<Label>("greeting");
                    if (lbl != null) lbl.text = _fullGreeting.Substring(0, _typingIdx);
                    if (_typingIdx >= _fullGreeting.Length) _typed = true;
                }
            }
        }

        void OnClose()
        {
            // "Close" = spawn more, leave this one
            _plugin.OnWindowClosed(this, _call);
        }

        public void Remove()
        {
            _removed = true;
            _root.Remove(_win);
        }
    }
}
