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
        private ConsultationSnapshot? _consultation;
        private bool _hasSnapshot;
        private string _notification = string.Empty;
        private float _notificationUntil;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _promptStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardBodyStyle;
        private GUIStyle _cardMetaStyle;
        private float _canvasWidth;
        private float _canvasHeight;

        public void Present(HudSnapshot snapshot)
        {
            _snapshot = snapshot;
            _hasSnapshot = true;
        }

        public void PresentConsultation(ConsultationSnapshot? snapshot) =>
            _consultation = snapshot;

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
            float scale = Mathf.Max(0.65f,
                Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            _canvasWidth = Screen.width / scale;
            _canvasHeight = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            EnsureStyles();
            if (_consultation.HasValue)
            {
                DrawConsultation(_consultation.Value);
                DrawNotification();
                GUI.matrix = previousMatrix;
                return;
            }

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
            Rect panel = new(24f, 24f, 560f, 164f);
            DrawPanel(panel, new Color(0.035f, 0.045f, 0.055f, 0.9f));
            GUI.Label(new Rect(42f, 38f, 370f, 32f), "СТРОЙБАЗА • ПРОТОТИП", _titleStyle);
            GUI.Label(new Rect(42f, 76f, 520f, 28f), ResolveObjective(), _bodyStyle);
            GUI.Label(new Rect(42f, 108f, 520f, 28f), ResolveStockStatus(), _bodyStyle);
            GUI.Label(new Rect(42f, 140f, 520f, 28f),
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
            string carried = _snapshot.HasItem
                ? $"  •  В руках: {_snapshot.CarriedProductDisplayName}"
                : string.Empty;
            string controls =
                $"WASD — идти   Shift — бег   E — действие   G — бросить   " +
                $"←/→ — выбор товара   Esc — курсор{carried}";
            Rect rect = new(22f, _canvasHeight - 54f, _canvasWidth - 44f, 34f);
            GUI.Label(rect, controls, _bodyStyle);
        }

        private void DrawConsultation(ConsultationSnapshot consultation)
        {
            DrawPanel(
                new Rect(0f, 0f, _canvasWidth, _canvasHeight),
                new Color(0.015f, 0.02f, 0.025f, 0.92f));

            float panelWidth = Mathf.Min(1260f, _canvasWidth - 48f);
            float panelHeight = Mathf.Min(700f, _canvasHeight - 64f);
            Rect panel = new(
                (_canvasWidth - panelWidth) * 0.5f,
                (_canvasHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            DrawPanel(panel, new Color(0.055f, 0.065f, 0.075f, 0.98f));

            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 24f, panel.width - 64f, 34f),
                "КОНСУЛЬТАЦИЯ КЛИЕНТА",
                _titleStyle);
            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 64f, panel.width - 64f, 34f),
                consultation.ProjectTitle,
                _centerStyle);
            GUI.Label(
                new Rect(panel.x + 56f, panel.y + 104f, panel.width - 112f, 54f),
                consultation.CustomerRequest,
                _cardBodyStyle);

            const float cardGap = 18f;
            float cardsLeft = panel.x + 28f;
            float cardsWidth = panel.width - 56f;
            float cardWidth = (cardsWidth - cardGap * 2f) / 3f;
            float cardTop = panel.y + 174f;
            float cardHeight = panel.height - 254f;
            for (int index = 0; index < consultation.Offers.Count; index++)
            {
                ConsultationOfferSnapshot offer = consultation.Offers[index];
                Rect border = new(
                    cardsLeft + index * (cardWidth + cardGap),
                    cardTop,
                    cardWidth,
                    cardHeight);
                DrawPanel(
                    border,
                    offer.Selected
                        ? new Color(1f, 0.56f, 0.12f, 1f)
                        : new Color(0.16f, 0.18f, 0.2f, 1f));
                Rect card = new(
                    border.x + 3f,
                    border.y + 3f,
                    border.width - 6f,
                    border.height - 6f);
                DrawPanel(
                    card,
                    offer.Selected
                        ? new Color(0.13f, 0.095f, 0.055f, 0.98f)
                        : new Color(0.075f, 0.085f, 0.095f, 0.98f));

                GUI.Label(
                    new Rect(card.x + 18f, card.y + 16f, card.width - 36f, 34f),
                    $"ВАРИАНТ {offer.Index + 1} • {offer.Title}",
                    _cardTitleStyle);
                GUI.Label(
                    new Rect(card.x + 18f, card.y + 56f, card.width - 36f, 76f),
                    offer.Description,
                    _cardBodyStyle);

                string details =
                    $"Товар: {offer.ProductDisplayName}\n" +
                    $"Объём: {offer.RequiredProductCount} {offer.ProductUnitLabel}\n" +
                    $"В наличии: {offer.AvailableProductCount}/" +
                    $"{offer.RequiredProductCount} {offer.ProductUnitLabel}\n" +
                    $"Выручка: {offer.Revenue.ToString("N0", RussianCulture)} ₽\n" +
                    $"Ожидаемая прибыль: " +
                    $"{offer.ExpectedProfit.ToString("N0", RussianCulture)} ₽";
                GUI.Label(
                    new Rect(card.x + 18f, card.y + 144f, card.width - 36f, 152f),
                    details,
                    _cardMetaStyle);

                if (offer.Selected)
                {
                    GUI.Label(
                        new Rect(card.x + 18f, card.yMax - 52f, card.width - 36f, 34f),
                        "ВЫБРАНО",
                        _promptStyle);
                }
            }

            GUI.Label(
                new Rect(panel.x + 28f, panel.yMax - 58f, panel.width - 56f, 34f),
                "← — предыдущее   → — следующее   Enter — подтвердить   Esc — закрыть",
                _promptStyle);
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
            if (_snapshot.HasActiveDelivery &&
                _snapshot.DeliveryStockedCount < _snapshot.DeliveryProductCount)
            {
                return $"Принять поставку • {_snapshot.DeliveryProductDisplayName}: " +
                       $"{_snapshot.DeliveryStockedCount}/" +
                       $"{_snapshot.DeliveryProductCount} " +
                       $"{_snapshot.DeliveryProductUnitLabel}";
            }

            return _snapshot.OrderState switch
            {
                HudOrderState.NoCustomer => "Ожидаем следующего клиента",
                HudOrderState.Arriving => "Клиент подъезжает",
                HudOrderState.Consulting => "Обсудить проект с клиентом у стойки",
                HudOrderState.Waiting when
                    _snapshot.AvailableProductCount < _snapshot.RequiredCount =>
                    $"Пополнить товар • {_snapshot.RequiredProductDisplayName}: " +
                    $"{_snapshot.AvailableProductCount}/{_snapshot.RequiredCount} " +
                    $"{_snapshot.RequiredProductUnitLabel}",
                HudOrderState.Waiting =>
                    $"Принять заказ • {_snapshot.RequiredProductDisplayName}: " +
                    $"{_snapshot.RequiredCount} {_snapshot.RequiredProductUnitLabel}",
                HudOrderState.Active =>
                    $"Отгрузить товар • {_snapshot.RequiredProductDisplayName}: " +
                    $"{_snapshot.LoadedCount}/{_snapshot.RequiredCount} " +
                    $"{_snapshot.RequiredProductUnitLabel}",
                HudOrderState.Completed => "Заказ выполнен • автомобиль загружен",
                HudOrderState.Departing => "Клиент уезжает",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private string ResolveStockStatus()
        {
            if (_snapshot.OrderState == HudOrderState.NoCustomer)
                return $"Товаров на складе: {_snapshot.StockCount}";

            if (_snapshot.OrderState is HudOrderState.Arriving or HudOrderState.Consulting)
                return $"Запрос клиента • {_snapshot.RequiredProductDisplayName}";

            return $"Доступно • {_snapshot.RequiredProductDisplayName}: " +
                   $"{_snapshot.AvailableProductCount} " +
                   $"{_snapshot.RequiredProductUnitLabel} • всего: {_snapshot.StockCount}";
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
            _cardTitleStyle = new GUIStyle(_promptStyle)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.72f, 0.28f) }
            };
            _cardBodyStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = 16,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            _cardMetaStyle = new GUIStyle(_cardBodyStyle)
            {
                fontSize = 17
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
