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
        internal static Texture2D? TexFace, TexEyes, TexMouth, TexOutline;

        readonly List<PopupWindow> _windows = new();
        static readonly System.Random _rng = new System.Random();

        UIDocument? _doc;
        VisualElement? _root;

        static readonly string[] _lines = new[]
        {
            "How are you?",
            "Hello there! How are you?",
            "Well hello! How are you doing?",
        };
        static readonly string[] _hangupLines = new[]
        {
            "You've been scammerpaybacked!",
            "That's a wrap! Pierogi out.",
            "Did you enjoy that? I did.",
        };

        void Awake()
        {
            CallEvents.OnCallStarted += OnCallStarted;
            CallEvents.OnCallEnded   += OnCallEnded;
            StartCoroutine(LoadTextures());
        }

        IEnumerator LoadTextures()
        {
            yield return null;
            string callers = System.IO.Path.Combine(Application.streamingAssetsPath, "Callers");
            TexFace    = LoadPng(System.IO.Path.Combine(callers, "Pierogi.png"));
            TexEyes    = LoadPng(System.IO.Path.Combine(callers, "Normal Eyes.png"));
            TexMouth   = LoadPng(System.IO.Path.Combine(callers, "Open Mouth.png"));
            TexOutline = LoadPng(System.IO.Path.Combine(callers, "CallerHead.png"));
        }

        static Texture2D? LoadPng(string path)
        {
            try
            {
                byte[] data = System.IO.File.ReadAllBytes(path);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(data);
                return t;
            }
            catch (Exception ex) { Log.Warn($"[Payback] Load failed {path}: {ex.Message}"); return null; }
        }

        readonly HashSet<uint> _spawned = new();
        void OnCallStarted(CallInfo call)
        {
            if (_spawned.Contains(call.NetId)) return;
            _spawned.Add(call.NetId);
            StartCoroutine(Spawn(call));
        }
        void OnCallEnded(CallInfo call)
        {
            _spawned.Remove(call.NetId);
            _windows.RemoveAll(w => w.Call.NetId == call.NetId);
        }

        IEnumerator Spawn(CallInfo call)
        {
            yield return new WaitForSeconds(0.5f);

            // Hang up immediately
            call.HangUp();

            yield return new WaitForSeconds(0.3f);

            EnsureDoc();
            float sw = Screen.width, sh = Screen.height;
            float x = _rng.Next((int)(sw * 0.15f), (int)(sw * 0.5f));
            float y = _rng.Next((int)(sh * 0.1f),  (int)(sh * 0.5f));
            x += _windows.Count * 32f;
            y += _windows.Count * 28f;

            var win = new PopupWindow(
                call, _root!, x, y,
                _lines[_rng.Next(_lines.Length)],
                _hangupLines[_rng.Next(_hangupLines.Length)],
                this);
            _windows.Add(win);
        }

        void EnsureDoc()
        {
            if (_doc != null) return;

            // Grab PanelSettings from existing game UIDocument
            PanelSettings? ps = null;
            foreach (var existing in FindObjectsOfType<UIDocument>())
            {
                if (existing.panelSettings != null) { ps = existing.panelSettings; break; }
            }

            var go = new GameObject("PaybackUI");
            DontDestroyOnLoad(go);
            _doc = go.AddComponent<UIDocument>();
            if (ps != null) _doc.panelSettings = ps;
            _doc.sortingOrder = 9999;
            _root = _doc.rootVisualElement;
            _root.style.position = Position.Absolute;
            _root.style.left = _root.style.top = _root.style.right = _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;
        }

        internal void OnClose(PopupWindow closed)
        {
            int count = Math.Min(_windows.Count + 1, 8);
            for (int i = 0; i < count; i++)
            {
                float x = _rng.Next(40, Screen.width  - 360);
                float y = _rng.Next(40, Screen.height - 300);
                _windows.Add(new PopupWindow(
                    closed.Call, _root!, x, y,
                    _lines[_rng.Next(_lines.Length)],
                    _hangupLines[_rng.Next(_hangupLines.Length)],
                    this));
            }
        }
    }

    // ── Window ────────────────────────────────────────────────────────────────

    sealed class PopupWindow
    {
        public readonly CallInfo Call;
        readonly VisualElement _win;
        readonly VisualElement _root;
        readonly VisualElement _faceImg;
        readonly VisualElement _eyesImg;
        readonly VisualElement _mouthImg;
        readonly Label _greeting;
        readonly Plugin _plugin;

        bool   _dragging;
        float  _dragOffX, _dragOffY, _winX, _winY;
        float  _faceX, _faceY, _vx, _vy;
        string _fullText;
        int    _typingIdx;
        float  _typingTimer;
        bool   _typed;

        static readonly System.Random _rng = new System.Random();

        public PopupWindow(CallInfo call, VisualElement root, float x, float y,
                           string greeting, string hangupLine, Plugin plugin)
        {
            Call     = call;
            _root    = root;
            _plugin  = plugin;
            _fullText = $"{greeting}\n\"How are you, {call.CallerName}?\"";
            _winX = x; _winY = y;

            // ── Outer window ──────────────────────────────────────────────
            _win = new VisualElement();
            _win.style.position  = Position.Absolute;
            _win.style.left      = x;
            _win.style.top       = y;
            _win.style.width     = 340;
            _win.style.backgroundColor = new Color(0.09f, 0.09f, 0.09f, 0.97f);
            SetBorder(_win, new Color(0.85f, 0.1f, 0.1f), 2, 10);

            // ── Title bar ─────────────────────────────────────────────────
            var bar = new VisualElement();
            bar.style.flexDirection  = FlexDirection.Row;
            bar.style.alignItems     = Align.Center;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.backgroundColor = new Color(0.85f, 0.1f, 0.1f);
            bar.style.paddingLeft = bar.style.paddingRight = 10;
            bar.style.paddingTop  = bar.style.paddingBottom = 6;
            bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius = 8;

            var title = new Label("⚠  SCAMMER PAYBACK");
            title.style.color    = Color.white;
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            var close = new Button();
            close.text = "✕";
            SetBorder(close, new Color(0.6f, 0.05f, 0.05f), 0, 4);
            close.style.backgroundColor = new Color(0.6f, 0.05f, 0.05f);
            close.style.color  = Color.white;
            close.style.width  = 26;
            close.style.height = 26;
            close.style.fontSize = 13;
            close.clicked += () => _plugin.OnClose(this);

            bar.Add(title); bar.Add(close);
            _win.Add(bar);

            // Drag on title bar
            bar.RegisterCallback<MouseDownEvent>(e =>
            {
                _dragging = true;
                _dragOffX = _winX - e.mousePosition.x;
                _dragOffY = _winY - e.mousePosition.y;
                e.StopPropagation();
            });
            _win.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (!_dragging) return;
                _winX = e.mousePosition.x + _dragOffX;
                _winY = e.mousePosition.y + _dragOffY;
                _win.style.left = _winX;
                _win.style.top  = _winY;
            });
            _win.RegisterCallback<MouseUpEvent>(_ => _dragging = false);

            // ── Face area ─────────────────────────────────────────────────
            var faceArea = new VisualElement();
            faceArea.style.height = 160;
            faceArea.style.overflow = Overflow.Hidden;
            faceArea.style.position = Position.Relative;
            faceArea.style.backgroundColor = new Color(0.06f, 0.06f, 0.06f);
            faceArea.style.borderBottomWidth = 1;
            faceArea.style.borderBottomColor = new Color(0.85f, 0.1f, 0.1f, 0.5f);

            // Layered face (same as game: head fill → eyes → mouth → outline ring)
            _faceImg  = FaceLayer(Plugin.TexFace);
            _eyesImg  = FaceLayer(Plugin.TexEyes);
            _mouthImg = FaceLayer(Plugin.TexMouth);

            _faceX = 95; _faceY = 10; // center horizontally in 340px window
            foreach (var el in new[] { _faceImg, _eyesImg, _mouthImg })
            {
                el.style.left = _faceX;
                el.style.top  = _faceY;
                faceArea.Add(el);
            }

            _vx = (float)(_rng.NextDouble() * 55 + 25) * (_rng.Next(2) == 0 ? 1 : -1);
            _vy = (float)(_rng.NextDouble() * 30 + 15) * (_rng.Next(2) == 0 ? 1 : -1);

            _win.Add(faceArea);

            // ── Greeting label ────────────────────────────────────────────
            _greeting = new Label("");
            _greeting.style.color     = new Color(0.92f, 0.92f, 0.92f);
            _greeting.style.fontSize  = 13;
            _greeting.style.whiteSpace = WhiteSpace.Normal;
            _greeting.style.paddingLeft = _greeting.style.paddingRight = 12;
            _greeting.style.paddingTop  = _greeting.style.paddingBottom = 10;
            _win.Add(_greeting);

            // ── Hang up button ────────────────────────────────────────────
            var hangup = new Button();
            hangup.text = "HANG UP";
            hangup.style.backgroundColor = new Color(0.85f, 0.1f, 0.1f);
            hangup.style.color  = Color.white;
            hangup.style.fontSize = 14;
            hangup.style.unityFontStyleAndWeight = FontStyle.Bold;
            hangup.style.marginLeft = hangup.style.marginRight = 12;
            hangup.style.marginBottom = 12;
            hangup.style.paddingTop   = hangup.style.paddingBottom = 8;
            SetBorder(hangup, new Color(0.85f, 0.1f, 0.1f), 0, 4);
            hangup.clicked += () =>
            {
                call.HangUp();
                _greeting.text = hangupLine;
                _typed = true;
            };
            _win.Add(hangup);

            root.Add(_win);

            // Animate
            _win.schedule.Execute(Tick).Every(33);
        }

        static VisualElement FaceLayer(Texture2D? tex)
        {
            var el = new VisualElement();
            el.style.position = Position.Absolute;
            el.style.width    = 130;
            el.style.height   = 130;
            el.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            if (tex != null)
                el.style.backgroundImage = new StyleBackground(tex);
            return el;
        }

        static void SetBorder(VisualElement el, Color c, float width, float radius)
        {
            el.style.borderTopWidth = el.style.borderBottomWidth =
            el.style.borderLeftWidth = el.style.borderRightWidth = width;
            el.style.borderTopColor = el.style.borderBottomColor =
            el.style.borderLeftColor = el.style.borderRightColor = c;
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius =
            el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = radius;
        }

        void Tick()
        {
            float dt = 0.033f;

            // Bounce all face layers together
            _faceX += _vx * dt;
            _faceY += _vy * dt;
            if (_faceX < 5)   { _faceX = 5;   _vx =  Mathf.Abs(_vx); }
            if (_faceX > 195) { _faceX = 195;  _vx = -Mathf.Abs(_vx); }
            if (_faceY < 5)   { _faceY = 5;    _vy =  Mathf.Abs(_vy); }
            if (_faceY > 25)  { _faceY = 25;   _vy = -Mathf.Abs(_vy); }
            foreach (var el in new[] { _faceImg, _eyesImg, _mouthImg })
            {
                el.style.left = _faceX;
                el.style.top  = _faceY;
            }

            // Typewriter
            if (!_typed)
            {
                _typingTimer += dt;
                if (_typingTimer > 0.045f)
                {
                    _typingTimer = 0;
                    _typingIdx = Math.Min(_typingIdx + 1, _fullText.Length);
                    _greeting.text = _fullText.Substring(0, _typingIdx);
                    if (_typingIdx >= _fullText.Length) _typed = true;
                }
            }
        }

        public void Remove() => _root.Remove(_win);
    }
}
