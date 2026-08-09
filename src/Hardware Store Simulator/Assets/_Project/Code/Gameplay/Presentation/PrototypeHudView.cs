using System;
using System.Globalization;
using UnityEngine;

namespace HardwareStore.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    public sealed class PrototypeHudView : MonoBehaviour, IHudService, INotificationService
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        private HudSnapshot _snapshot;
        private bool _hasSnapshot;
        private string _notification = string.Empty;
        private float _notificationUntil;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _promptStyle;
        private GUIStyle _centerStyle;
        private float _canvasWidth;
        private float _canvasHeight;

        public void Present(HudSnapshot snapshot)
        {
            _snapshot = snapshot;
            _hasSnapshot = true;
        }

        public void Show(string message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (message.Length == 0)
                throw new ArgumentException("Notification message cannot be empty.", nameof(message));

            _notification = message;
            _notificationUntil = Time.unscaledTime + 2.2f;
        }

        private void OnGUI()
        {
            if (!_hasSnapshot)
                return;

            Matrix4x4 previousMatrix = GUI.matrix;
            float scale = Mathf.Max(1f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            _canvasWidth = Screen.width / scale;
            _canvasHeight = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            EnsureStyles();
            DrawStatusPanel();
            DrawCrosshair();
            DrawInteractionPrompt();
            DrawControls();
            DrawNotification();

            if (!_snapshot.CursorLocked)
                DrawCursorHint();

            GUI.matrix = previousMatrix;
        }

        private void DrawStatusPanel()
        {
            Rect panel = new(24f, 24f, 440f, 164f);
            DrawPanel(panel, new Color(0.035f, 0.045f, 0.055f, 0.9f));
            GUI.Label(new Rect(42f, 38f, 370f, 32f), "СТРОЙБАЗА • ПРОТОТИП", _titleStyle);
            GUI.Label(new Rect(42f, 76f, 400f, 28f), ResolveObjective(), _bodyStyle);
            GUI.Label(new Rect(42f, 108f, 400f, 28f),
                $"Остаток на складе: {_snapshot.StockCount}", _bodyStyle);
            GUI.Label(new Rect(42f, 140f, 400f, 28f),
                $"Баланс: {_snapshot.Money.ToString("N0", RussianCulture)} ₽", _bodyStyle);
        }

        private void DrawCrosshair()
        {
            float centerX = _canvasWidth * 0.5f;
            float centerY = _canvasHeight * 0.5f;
            Color previous = GUI.color;
            GUI.color = _snapshot.CanInteract
                ? new Color(1f, 0.58f, 0.12f)
                : _snapshot.HasFocus
                    ? new Color(0.72f, 0.78f, 0.82f)
                    : Color.white;
            GUI.DrawTexture(new Rect(centerX - 7f, centerY - 1f, 14f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centerX - 1f, centerY - 7f, 2f, 14f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawInteractionPrompt()
        {
            if (string.IsNullOrEmpty(_snapshot.Prompt))
                return;

            float width = Mathf.Min(620f, _canvasWidth - 40f);
            Rect rect = new((_canvasWidth - width) * 0.5f, _canvasHeight * 0.66f, width, 48f);
            DrawPanel(rect, new Color(0.03f, 0.04f, 0.05f, 0.88f));
            GUI.Label(rect, _snapshot.Prompt, _promptStyle);
        }

        private void DrawControls()
        {
            string carried = _snapshot.HasItem ? "  •  В руках: мешок цемента" : string.Empty;
            string controls = $"WASD — идти   Shift — бег   E — действие   G — бросить   Esc — курсор{carried}";
            Rect rect = new(22f, _canvasHeight - 54f, _canvasWidth - 44f, 34f);
            GUI.Label(rect, controls, _bodyStyle);
        }

        private void DrawNotification()
        {
            if (Time.unscaledTime > _notificationUntil)
                return;

            float width = Mathf.Min(520f, _canvasWidth - 40f);
            Rect rect = new((_canvasWidth - width) * 0.5f, 28f, width, 48f);
            DrawPanel(rect, new Color(0.9f, 0.43f, 0.08f, 0.94f));
            GUI.Label(rect, _notification, _promptStyle);
        }

        private void DrawCursorHint()
        {
            Rect background = new(0f, 0f, _canvasWidth, _canvasHeight);
            DrawPanel(background, new Color(0f, 0f, 0f, 0.42f));
            GUI.Label(new Rect(0f, _canvasHeight * 0.45f, _canvasWidth, 54f),
                "Курсор свободен • нажмите Esc, чтобы продолжить", _centerStyle);
        }

        private string ResolveObjective()
        {
            if (_snapshot.OrderState == HudOrderState.Waiting &&
                _snapshot.StockCount < _snapshot.RequiredCount)
            {
                return _snapshot.HasActiveDelivery
                    ? $"Цель: принять поставку — {_snapshot.DeliveryStockedCount}/{_snapshot.DeliveryProductCount}"
                    : $"Цель: заказать поставку — {_snapshot.DeliveryProductCount} мешка";
            }

            return _snapshot.OrderState switch
            {
                HudOrderState.Waiting => "Цель: принять заказ у клиента",
                HudOrderState.Active =>
                    $"Цель: отгрузить цемент — {_snapshot.LoadedCount}/{_snapshot.RequiredCount}",
                HudOrderState.Completed => "Заказ выполнен • автомобиль загружен",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.68f, 0.2f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = Color.white }
            };
            _promptStyle = new GUIStyle(_bodyStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _centerStyle = new GUIStyle(_promptStyle)
            {
                fontSize = 22
            };
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
