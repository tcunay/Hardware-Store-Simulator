using System;
using HardwareStore.Gameplay.Common.Economy;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Localization;
using UnityEngine;
using Zenject;

namespace HardwareStore.Gameplay.Presentation
{
    [DisallowMultipleComponent]
    public sealed class PrototypeHudView : MonoBehaviour, IHudService, INotificationService
    {
        public const float NewDayFadeHoldSeconds = 0.2f;
        public const float NewDayFadeOutSeconds = 0.8f;

        private ILocalizationService _localization;
        private HudSnapshot _snapshot;
        private DayReportSnapshot? _dayReport;
        private ConsultationSnapshot? _consultation;
        private ProcurementSnapshot? _procurement;
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
        private float _newDayFadeStartedAt = -1f;

        [Inject]
        private void Construct(ILocalizationService localization) =>
            _localization = localization;

        private void OnEnable() =>
            ResetPresentation();

        private void OnDisable() =>
            ResetPresentation();

        public void Present(HudSnapshot snapshot)
        {
            if (_hasSnapshot &&
                ShouldStartNewDayFade(_snapshot.DayClock, snapshot.DayClock))
            {
                _newDayFadeStartedAt = Time.unscaledTime;
            }

            _snapshot = snapshot;
            _hasSnapshot = true;
        }

        public static bool ShouldStartNewDayFade(
            DayClockSnapshot previous,
            DayClockSnapshot current) =>
            previous.Phase == StoreDayPhase.Report &&
            current.Phase == StoreDayPhase.Preparing &&
            previous.DayNumber < int.MaxValue &&
            current.DayNumber == previous.DayNumber + 1;

        public static float EvaluateNewDayFadeAlpha(float elapsedSeconds)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) ||
                elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (elapsedSeconds <= NewDayFadeHoldSeconds)
                return 1f;

            float fadeProgress = Mathf.Clamp01(
                (elapsedSeconds - NewDayFadeHoldSeconds) / NewDayFadeOutSeconds);
            return 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
        }

        public void PresentDayReport(DayReportSnapshot? snapshot) =>
            _dayReport = snapshot;

        public void PresentConsultation(ConsultationSnapshot? snapshot) =>
            _consultation = snapshot;

        public void PresentProcurement(ProcurementSnapshot? snapshot) =>
            _procurement = snapshot;

        public void Show(LocalizedText message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            string resolvedMessage = Resolve(message);
            if (resolvedMessage.Length == 0)
                throw new ArgumentException("Notification message cannot be empty.", nameof(message));

            _notification = resolvedMessage;
            _notificationUntil = Time.unscaledTime + 2.2f;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !_hasSnapshot)
                return;

            Matrix4x4 previousMatrix = GUI.matrix;
            float scale = Mathf.Max(0.65f,
                Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            _canvasWidth = Screen.width / scale;
            _canvasHeight = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            EnsureStyles();
            if (_dayReport.HasValue)
            {
                DrawDayReport(_dayReport.Value);
                DrawNotification();
                DrawNewDayFade();
                GUI.matrix = previousMatrix;
                return;
            }
            if (_consultation.HasValue)
            {
                DrawConsultation(_consultation.Value);
                DrawNotification();
                DrawNewDayFade();
                GUI.matrix = previousMatrix;
                return;
            }
            if (_procurement.HasValue)
            {
                DrawProcurement(_procurement.Value);
                DrawNotification();
                DrawNewDayFade();
                GUI.matrix = previousMatrix;
                return;
            }

            DrawStatusPanel();
            DrawDayClock();
            DrawCrosshair();
            DrawInteractionPrompt();
            DrawControls();
            DrawNotification();

            if (!_snapshot.CursorLocked)
                DrawCursorHint();

            DrawNewDayFade();

            GUI.matrix = previousMatrix;
        }

        private void ResetPresentation()
        {
            _snapshot = default;
            _dayReport = null;
            _consultation = null;
            _procurement = null;
            _hasSnapshot = false;
            _notification = string.Empty;
            _notificationUntil = 0f;
            _newDayFadeStartedAt = -1f;
        }

        private void DrawStatusPanel()
        {
            bool hasOrderLines = _snapshot.OrderLines.Count > 0;
            float workerRowHeight = _snapshot.WarehouseWorkerStatus.HasValue ? 30f : 0f;
            float panelHeight = (hasOrderLines ? 270f : 212f) + workerRowHeight;
            Rect panel = new(24f, 24f, 740f, panelHeight);
            DrawPanel(panel, new Color(0.035f, 0.045f, 0.055f, 0.9f));
            GUI.Label(new Rect(42f, 38f, 370f, 32f),
                Resolve(LocalizationKey.HudStoreTitle), _titleStyle);
            GUI.Label(new Rect(42f, 74f, 640f, 28f), ResolveObjective(), _bodyStyle);
            GUI.Label(
                new Rect(42f, 106f, 700f, 46f),
                Resolve(
                    LocalizationKey.HudCustomerFlow,
                    _snapshot.CustomerFlow.TotalActiveCount,
                    checked(
                        _snapshot.CustomerFlow.QueuedCount +
                        _snapshot.CustomerFlow.ConsultingCount),
                    _snapshot.CustomerFlow.LoadingPipelineCount,
                    _snapshot.CustomerFlow.LeavingCount),
                _cardMetaStyle);
            if (hasOrderLines)
            {
                for (int index = 0; index < _snapshot.OrderLines.Count; index++)
                {
                    OrderLineSnapshot line = _snapshot.OrderLines[index];
                    GUI.Label(
                        new Rect(42f, 154f + index * 30f, 640f, 28f),
                        Resolve(
                            LocalizationKey.HudOrderLineStatus,
                            LocalizedTexts.ProductName(line.ProductType),
                            line.AvailableProductCount,
                            line.LoadedProductCount,
                            line.RequiredProductCount,
                            LocalizedTexts.ProductUnit(line.ProductType)),
                        _bodyStyle);
                }

                GUI.Label(new Rect(42f, panel.yMax - 38f, 640f, 28f),
                    Resolve(LocalizationKey.HudStockAndBalance,
                        _snapshot.StockCount, _snapshot.Money),
                    _bodyStyle);
            }
            else
            {
                GUI.Label(new Rect(42f, 154f, 640f, 28f), ResolveStockStatus(), _bodyStyle);
                GUI.Label(new Rect(
                        42f,
                        _snapshot.WarehouseWorkerStatus.HasValue ? 216f : 186f,
                        640f,
                        28f),
                    Resolve(LocalizationKey.HudBalance, _snapshot.Money), _bodyStyle);
            }

            if (_snapshot.WarehouseWorkerStatus.HasValue)
            {
                GUI.Label(
                    new Rect(
                        42f,
                        hasOrderLines ? panel.yMax - 68f : 186f,
                        640f,
                        28f),
                    ResolveWarehouseWorkerStatus(
                        _snapshot.WarehouseWorkerStatus.Value),
                    _bodyStyle);
            }
        }

        private void DrawDayClock()
        {
            float panelWidth = Mathf.Min(420f, _canvasWidth - 48f);
            const float panelY = 24f;
            const float phaseTopOffset = 44f;
            const float horizontalPadding = 16f;
            string phaseText = Resolve(
                LocalizedTexts.StoreDayPhase(_snapshot.DayClock.Phase));
            float phaseHeight = Mathf.Max(
                28f,
                _promptStyle.CalcHeight(
                    new GUIContent(phaseText),
                    panelWidth - horizontalPadding * 2f));
            float panelHeight = phaseTopOffset + phaseHeight + 10f;
            Rect panel = new(
                _canvasWidth - panelWidth - 24f,
                panelY,
                panelWidth,
                panelHeight);
            DrawPanel(panel, new Color(0.035f, 0.045f, 0.055f, 0.9f));

            int hour = _snapshot.DayClock.CurrentDayMinute / 60;
            int minute = _snapshot.DayClock.CurrentDayMinute % 60;
            GUI.Label(
                new Rect(panel.x + 18f, panel.y + 10f, panel.width - 36f, 32f),
                Resolve(
                    LocalizationKey.HudDayClock,
                    _snapshot.DayClock.DayNumber,
                    hour,
                    minute),
                _titleStyle);

            Color previous = GUI.color;
            GUI.color = _snapshot.DayClock.Phase switch
            {
                StoreDayPhase.Preparing => new Color(0.7f, 0.82f, 0.92f),
                StoreDayPhase.Open => new Color(0.4f, 0.9f, 0.48f),
                StoreDayPhase.Closing => new Color(1f, 0.58f, 0.18f),
                StoreDayPhase.Report => new Color(0.78f, 0.82f, 0.86f),
                _ => throw new ArgumentOutOfRangeException()
            };
            GUI.Label(
                new Rect(
                    panel.x + horizontalPadding,
                    panel.y + phaseTopOffset,
                    panel.width - horizontalPadding * 2f,
                    phaseHeight),
                phaseText,
                _promptStyle);
            GUI.color = previous;
        }

        private void DrawNewDayFade()
        {
            if (_newDayFadeStartedAt < 0f)
                return;

            float elapsedSeconds = Time.unscaledTime - _newDayFadeStartedAt;
            float alpha = EvaluateNewDayFadeAlpha(elapsedSeconds);
            if (alpha <= 0f)
            {
                _newDayFadeStartedAt = -1f;
                return;
            }

            DrawPanel(
                new Rect(0f, 0f, _canvasWidth, _canvasHeight),
                new Color(0f, 0f, 0f, alpha));
        }

        private void DrawDayReport(DayReportSnapshot report)
        {
            DrawPanel(
                new Rect(0f, 0f, _canvasWidth, _canvasHeight),
                new Color(0.012f, 0.018f, 0.03f, 0.95f));

            float panelWidth = Mathf.Min(780f, _canvasWidth - 48f);
            float panelHeight = Mathf.Min(650f, _canvasHeight - 64f);
            Rect panel = new(
                (_canvasWidth - panelWidth) * 0.5f,
                (_canvasHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            DrawPanel(panel, new Color(0.055f, 0.065f, 0.075f, 0.99f));

            GUI.Label(
                new Rect(panel.x + 36f, panel.y + 28f, panel.width - 72f, 40f),
                Resolve(LocalizationKey.HudDayReportTitle, report.DayNumber),
                _centerStyle);
            GUI.Label(
                new Rect(panel.x + 52f, panel.y + 92f, panel.width - 104f, 32f),
                Resolve(
                    LocalizationKey.HudDayReportOrders,
                    report.CompletedOrderCount),
                _bodyStyle);
            GUI.Label(
                new Rect(panel.x + 52f, panel.y + 124f, panel.width - 104f, 32f),
                Resolve(
                    LocalizationKey.HudDayReportLostCustomers,
                    report.LostCustomerCount),
                _bodyStyle);

            GUI.Label(
                new Rect(panel.x + 52f, panel.y + 166f, panel.width - 104f, 32f),
                Resolve(LocalizationKey.HudDayReportRevenue, report.Revenue),
                _bodyStyle);
            GUI.Label(
                new Rect(panel.x + 52f, panel.y + 206f, panel.width - 104f, 32f),
                Resolve(
                    LocalizationKey.HudDayReportProcurementExpenses,
                    report.ProcurementExpenses),
                _bodyStyle);
            GUI.Label(
                new Rect(panel.x + 52f, panel.y + 246f, panel.width - 104f, 32f),
                Resolve(
                    LocalizationKey.HudDayReportUpgradeExpenses,
                    report.UpgradeExpenses),
                _bodyStyle);
            GUI.Label(
                new Rect(panel.x + 52f, panel.y + 286f, panel.width - 104f, 32f),
                Resolve(
                    LocalizationKey.HudDayReportPayrollExpenses,
                    report.PayrollExpenses),
                _bodyStyle);

            DrawPanel(
                new Rect(panel.x + 48f, panel.y + 340f, panel.width - 96f, 2f),
                new Color(0.28f, 0.31f, 0.34f, 1f));
            Color previous = GUI.color;
            GUI.color = report.NetCashFlow >= 0
                ? new Color(1f, 0.7f, 0.25f)
                : new Color(1f, 0.35f, 0.28f);
            GUI.Label(
                new Rect(panel.x + 52f, panel.y + 364f, panel.width - 104f, 38f),
                Resolve(
                    LocalizationKey.HudDayReportNetCashFlow,
                    report.NetCashFlow),
                _cardTitleStyle);
            GUI.color = previous;

            GUI.Label(
                new Rect(panel.x + 52f, panel.y + 426f, panel.width - 104f, 32f),
                Resolve(
                    LocalizationKey.HudDayReportBalance,
                    report.OpeningBalance,
                    report.ClosingBalance),
                _bodyStyle);
            GUI.Label(
                new Rect(panel.x + 52f, panel.y + 466f, panel.width - 104f, 32f),
                Resolve(
                    LocalizationKey.HudDayReportStock,
                    report.StorageProductCount),
                _bodyStyle);

            GUI.Label(
                new Rect(panel.x + 36f, panel.yMax - 82f, panel.width - 72f, 44f),
                Resolve(
                    LocalizationKey.HudDayReportContinue,
                    checked(report.DayNumber + 1)),
                _promptStyle);
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
            if (_snapshot.Prompt == null)
                return;

            float width = Mathf.Min(620f, _canvasWidth - 40f);
            Rect rect = new((_canvasWidth - width) * 0.5f, _canvasHeight * 0.66f, width, 48f);
            DrawPanel(rect, new Color(0.03f, 0.04f, 0.05f, 0.88f));
            GUI.Label(rect, Resolve(_snapshot.Prompt), _promptStyle);
        }

        private void DrawControls()
        {
            string controls = _snapshot.IsPushingTrolley
                ? Resolve(LocalizationKey.HudControlsPushingTrolley)
                : _snapshot.HasItem
                ? Resolve(
                    LocalizationKey.HudControlsCarrying,
                    LocalizedTexts.ProductName(_snapshot.CarriedProductType.Value))
                : Resolve(LocalizationKey.HudControls);
            Rect rect = new(22f, _canvasHeight - 54f, _canvasWidth - 44f, 34f);
            GUI.Label(rect, controls, _bodyStyle);
        }

        private void DrawConsultation(ConsultationSnapshot consultation)
        {
            DrawPanel(
                new Rect(0f, 0f, _canvasWidth, _canvasHeight),
                new Color(0.015f, 0.02f, 0.025f, 0.92f));

            float panelWidth = Mathf.Min(1280f, _canvasWidth - 48f);
            float panelHeight = Mathf.Min(760f, _canvasHeight - 64f);
            Rect panel = new(
                (_canvasWidth - panelWidth) * 0.5f,
                (_canvasHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            DrawPanel(panel, new Color(0.055f, 0.065f, 0.075f, 0.98f));

            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 24f, panel.width - 64f, 34f),
                Resolve(LocalizationKey.HudConsultationTitle),
                _titleStyle);
            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 58f, panel.width - 64f, 34f),
                Resolve(LocalizedTexts.ProjectTitle(consultation.ProjectType)),
                _centerStyle);
            GUI.Label(
                new Rect(panel.x + 56f, panel.y + 96f, panel.width - 112f, 48f),
                Resolve(LocalizedTexts.ProjectRequest(consultation.ProjectType)),
                _cardBodyStyle);
            GUI.Label(
                new Rect(panel.x + 56f, panel.y + 142f, panel.width - 112f, 28f),
                Resolve(LocalizationKey.HudConsultationCapacity,
                    consultation.CargoCapacity),
                _promptStyle);

            const float cardGap = 18f;
            float cardsLeft = panel.x + 28f;
            float cardsWidth = panel.width - 56f;
            float cardWidth = (cardsWidth - cardGap * 2f) / 3f;
            float cardTop = panel.y + 178f;
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
                    Resolve(
                        LocalizationKey.HudConsultationOfferHeader,
                        offer.Index + 1,
                        LocalizedTexts.OfferTitle(
                            consultation.ProjectType,
                            offer.Index)),
                    _cardTitleStyle);
                GUI.Label(
                    new Rect(card.x + 18f, card.y + 56f, card.width - 36f, 76f),
                    Resolve(LocalizedTexts.OfferDescription(
                        consultation.ProjectType,
                        offer.Index)),
                    _cardBodyStyle);

                string details = string.Join(
                    Environment.NewLine,
                    BuildOfferLineDetails(offer),
                    Resolve(LocalizationKey.HudCargoSlots,
                        offer.TotalUnitCount, consultation.CargoCapacity),
                    Resolve(LocalizationKey.HudProductCost, offer.ProductCost),
                    Resolve(LocalizationKey.HudRevenue, offer.OrderReward),
                    Resolve(LocalizationKey.HudProfit, offer.ExpectedProfit));
                GUI.Label(
                    new Rect(card.x + 18f, card.y + 138f, card.width - 36f, 250f),
                    details,
                    _cardMetaStyle);

                if (offer.Selected)
                {
                    GUI.Label(
                        new Rect(card.x + 18f, card.yMax - 52f, card.width - 36f, 34f),
                        Resolve(LocalizationKey.HudSelected),
                        _promptStyle);
                }
            }

            GUI.Label(
                new Rect(panel.x + 28f, panel.yMax - 58f, panel.width - 56f, 34f),
                Resolve(LocalizationKey.HudConsultationControls),
                _promptStyle);
        }

        private void DrawProcurement(ProcurementSnapshot procurement)
        {
            DrawPanel(
                new Rect(0f, 0f, _canvasWidth, _canvasHeight),
                new Color(0.015f, 0.02f, 0.025f, 1f));

            const int cardsPerPage = 6;
            const int cardColumnCount = 3;
            const float contentGap = 24f;
            const float cartWidth = 416f;
            float panelWidth = Mathf.Min(1520f, _canvasWidth - 48f);
            float panelHeight = Mathf.Min(840f, _canvasHeight - 48f);
            Rect panel = new(
                (_canvasWidth - panelWidth) * 0.5f,
                (_canvasHeight - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            DrawPanel(panel, new Color(0.055f, 0.065f, 0.075f, 0.98f));

            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 18f, panel.width - 64f, 34f),
                Resolve(LocalizationKey.HudProcurementTitle),
                _titleStyle);
            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 52f, panel.width - 64f, 34f),
                Resolve(
                    procurement.DemandKind == ProcurementDemandKind.ProjectForecast
                        ? LocalizationKey.HudProcurementForecastTitle
                        : LocalizationKey.HudProcurementOrderTitle,
                    LocalizedTexts.ProjectTitle(procurement.ProjectType)),
                _centerStyle);
            GUI.Label(
                new Rect(panel.x + 32f, panel.y + 86f, panel.width - 64f, 30f),
                Resolve(LocalizationKey.HudProcurementBalanceStorage,
                    procurement.Money, procurement.FreeStorageSlotCount),
                _promptStyle);

            float contentTop = panel.y + 134f;
            float contentHeight = panel.height - 236f;
            float cardsLeft = panel.x + 32f;
            float cardsWidth = panel.width - 64f - contentGap - cartWidth;
            float cartLeft = cardsLeft + cardsWidth + contentGap;
            int pageIndex = procurement.SelectedProductIndex / cardsPerPage;
            int pageCount = Mathf.CeilToInt(
                procurement.Products.Count / (float)cardsPerPage);
            GUI.Label(
                new Rect(cardsLeft + cardsWidth - 180f, panel.y + 18f, 180f, 28f),
                Resolve(LocalizationKey.HudProcurementPage,
                    pageIndex + 1, pageCount),
                _cardMetaStyle);

            const float cardGap = 16f;
            float cardWidth =
                (cardsWidth - cardGap * (cardColumnCount - 1)) / cardColumnCount;
            float cardHeight = (contentHeight - cardGap) * 0.5f;
            int firstProductIndex = pageIndex * cardsPerPage;
            int finalProductIndex = Math.Min(
                firstProductIndex + cardsPerPage,
                procurement.Products.Count);
            for (int index = firstProductIndex; index < finalProductIndex; index++)
            {
                ProcurementProductSnapshot product = procurement.Products[index];
                int visibleIndex = index - firstProductIndex;
                int column = visibleIndex % cardColumnCount;
                int row = visibleIndex / cardColumnCount;
                bool selected = product.Index == procurement.SelectedProductIndex;
                Rect border = new(
                    cardsLeft + column * (cardWidth + cardGap),
                    contentTop + row * (cardHeight + cardGap),
                    cardWidth,
                    cardHeight);
                DrawPanel(
                    border,
                    selected
                        ? new Color(1f, 0.56f, 0.12f, 1f)
                        : product.CartPackageCount > 0
                            ? new Color(0.28f, 0.54f, 0.34f, 1f)
                        : new Color(0.16f, 0.18f, 0.2f, 1f));
                Rect card = new(
                    border.x + 3f,
                    border.y + 3f,
                    border.width - 6f,
                    border.height - 6f);
                DrawPanel(
                    card,
                    selected
                        ? new Color(0.13f, 0.095f, 0.055f, 0.98f)
                        : new Color(0.075f, 0.085f, 0.095f, 0.98f));

                GUI.Label(
                    new Rect(card.x + 16f, card.y + 12f, card.width - 32f, 34f),
                    Resolve(LocalizedTexts.ProductName(product.ProductType))
                        .ToUpper(_localization.Culture),
                    _cardTitleStyle);
                GUI.Label(
                    new Rect(card.x + 16f, card.y + 48f, card.width - 32f, 42f),
                    Resolve(
                        LocalizationKey.HudProcurementPackageDetails,
                        product.PackageProductCount,
                        LocalizedTexts.ProductUnit(product.ProductType),
                        product.PackageCost),
                    _cardMetaStyle);
                GUI.Label(
                    new Rect(card.x + 16f, card.y + 90f, card.width - 32f, 78f),
                    ResolveProductCounts(procurement, product),
                    _cardMetaStyle);
                GUI.Label(
                    new Rect(card.x + 16f, card.yMax - 82f, card.width - 32f, 38f),
                    Resolve(
                        LocalizationKey.HudProcurementCardCartQuantity,
                        product.CartPackageCount,
                        product.CartProductCount,
                        LocalizedTexts.ProductUnit(product.ProductType)),
                    _promptStyle);

                if (selected)
                {
                    GUI.Label(
                        new Rect(card.x + 16f, card.yMax - 42f, card.width - 32f, 28f),
                        Resolve(LocalizationKey.HudSelected),
                        _promptStyle);
                }
            }

            DrawProcurementCart(
                procurement,
                new Rect(cartLeft, contentTop, cartWidth, contentHeight));
            GUI.Label(
                new Rect(panel.x + 28f, panel.yMax - 66f, panel.width - 56f, 34f),
                Resolve(LocalizationKey.HudProcurementControls),
                _promptStyle);
        }

        private void DrawProcurementCart(ProcurementSnapshot procurement, Rect rect)
        {
            DrawPanel(rect, new Color(0.035f, 0.043f, 0.052f, 0.98f));
            ProcurementCartSnapshot cart = procurement.Cart;
            GUI.Label(
                new Rect(rect.x + 18f, rect.y + 14f, rect.width - 36f, 34f),
                Resolve(LocalizationKey.HudProcurementCartTitle,
                    cart.PackageCount, cart.PackageCapacity),
                _cardTitleStyle);

            float lineTop = rect.y + 56f;
            float maximumLineAreaHeight = 246f;
            float lineHeight = cart.Lines.Count == 0
                ? 0f
                : Mathf.Min(82f, maximumLineAreaHeight / cart.Lines.Count);
            for (int index = 0; index < cart.Lines.Count; index++)
            {
                ProcurementCartLineSnapshot line = cart.Lines[index];
                Rect lineRect = new(
                    rect.x + 14f,
                    lineTop + index * lineHeight,
                    rect.width - 28f,
                    lineHeight - 6f);
                DrawPanel(lineRect, new Color(0.075f, 0.085f, 0.095f, 0.98f));
                GUI.Label(
                    new Rect(
                        lineRect.x + 12f,
                        lineRect.y + 6f,
                        lineRect.width - 24f,
                        lineRect.height - 12f),
                    Resolve(
                        LocalizationKey.HudProcurementCartLine,
                        LocalizedTexts.ProductName(line.ProductType),
                        line.PackageCount,
                        line.ProductCount,
                        LocalizedTexts.ProductUnit(line.ProductType),
                        line.LineCost),
                    _cardMetaStyle);
            }

            ProductTypeId unitProductType = cart.Lines.Count > 0
                ? cart.Lines[0].ProductType
                : procurement.Products[procurement.SelectedProductIndex].ProductType;
            float summaryTop = rect.yMax - 238f;
            GUI.Label(
                new Rect(rect.x + 18f, summaryTop, rect.width - 36f, 28f),
                Resolve(LocalizationKey.HudProcurementCartProductTotal,
                    cart.ProductCount,
                    LocalizedTexts.ProductUnit(unitProductType)),
                _cardMetaStyle);
            GUI.Label(
                new Rect(rect.x + 18f, summaryTop + 30f, rect.width - 36f, 28f),
                Resolve(LocalizationKey.HudProcurementCartStorage,
                    cart.RequiredStorageSlotCount,
                    procurement.FreeStorageSlotCount),
                _cardMetaStyle);
            GUI.Label(
                new Rect(rect.x + 18f, summaryTop + 64f, rect.width - 36f, 30f),
                Resolve(LocalizationKey.HudProcurementCartCost, cart.TotalCost),
                _bodyStyle);
            GUI.Label(
                new Rect(rect.x + 18f, summaryTop + 96f, rect.width - 36f, 30f),
                Resolve(LocalizationKey.HudProcurementCartBalanceAfter,
                    cart.MoneyAfterPurchase),
                _bodyStyle);

            Color previousColor = GUI.color;
            GUI.color = cart.CanCheckout
                ? new Color(1f, 0.7f, 0.25f)
                : new Color(0.86f, 0.46f, 0.32f);
            GUI.Label(
                new Rect(rect.x + 18f, rect.yMax - 92f, rect.width - 36f, 66f),
                ResolveCartStatus(procurement),
                _promptStyle);
            GUI.color = previousColor;

            if (cart.PackageCount == cart.PackageCapacity && cart.PackageCount > 0)
            {
                GUI.Label(
                    new Rect(rect.x + 18f, summaryTop - 34f, rect.width - 36f, 28f),
                    Resolve(LocalizationKey.HudProcurementCartCapacityReached,
                        cart.PackageCapacity),
                    _promptStyle);
            }
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
                Resolve(LocalizationKey.HudCursorHint), _centerStyle);
        }

        private string ResolveObjective()
        {
            if (_snapshot.DayClock.Phase == StoreDayPhase.Preparing)
                return Resolve(LocalizationKey.HudObjectivePreparing);

            if (_snapshot.DayClock.Phase == StoreDayPhase.Closing &&
                _snapshot.OrderState == HudOrderState.NoCustomer)
            {
                return Resolve(LocalizationKey.HudObjectiveClosing);
            }

            if (_snapshot.Delivery.HasValue &&
                _snapshot.Delivery.Value.StockedProductCount <
                _snapshot.Delivery.Value.ProductCount)
            {
                DeliveryProgressSnapshot delivery = _snapshot.Delivery.Value;
                return Resolve(
                    LocalizationKey.HudObjectiveMixedDelivery,
                    delivery.StockedProductCount,
                    delivery.ProductCount,
                    delivery.IncompleteLineCount);
            }

            return _snapshot.OrderState switch
            {
                HudOrderState.NoCustomer => Resolve(LocalizationKey.HudObjectiveNoCustomer),
                HudOrderState.Arriving => Resolve(LocalizationKey.HudObjectiveArriving),
                HudOrderState.Consulting => Resolve(LocalizationKey.HudObjectiveConsulting),
                HudOrderState.Active =>
                    Resolve(LocalizationKey.HudObjectiveLoading,
                        _snapshot.TotalLoadedProductCount,
                        _snapshot.TotalRequiredProductCount),
                HudOrderState.Completed => Resolve(LocalizationKey.HudObjectiveCompleted),
                HudOrderState.Returning => Resolve(LocalizationKey.HudObjectiveReturning),
                HudOrderState.Departing => Resolve(LocalizationKey.HudObjectiveDeparting),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private string ResolveStockStatus()
        {
            if (_snapshot.OrderState == HudOrderState.NoCustomer)
                return Resolve(LocalizationKey.HudStockNoCustomer, _snapshot.StockCount);

            if (_snapshot.OrderState is HudOrderState.Arriving or HudOrderState.Consulting)
                return Resolve(
                    LocalizationKey.HudStockProject,
                    LocalizedTexts.ProjectTitle(_snapshot.ProjectType.Value));

            return Resolve(LocalizationKey.HudStockOrder,
                _snapshot.OrderLines.Count, _snapshot.StockCount);
        }

        private string ResolveWarehouseWorkerStatus(
            WarehouseWorkerStatusSnapshot snapshot) =>
            snapshot.Status switch
            {
                WarehouseWorkerStatusId.OffShift =>
                    Resolve(LocalizationKey.HudWarehouseWorkerOffShift),
                WarehouseWorkerStatusId.Idle =>
                    Resolve(LocalizationKey.HudWarehouseWorkerIdle),
                WarehouseWorkerStatusId.StorageFull =>
                    Resolve(LocalizationKey.HudWarehouseWorkerStorageFull),
                WarehouseWorkerStatusId.MovingToPickup => Resolve(
                    LocalizationKey.HudWarehouseWorkerMovingToPickup,
                    LocalizedTexts.ProductName(snapshot.ProductType.Value)),
                WarehouseWorkerStatusId.MovingToStorage => Resolve(
                    LocalizationKey.HudWarehouseWorkerMovingToStorage,
                    LocalizedTexts.ProductName(snapshot.ProductType.Value)),
                WarehouseWorkerStatusId.MovingToCustomerLoading => Resolve(
                    LocalizationKey.HudWarehouseWorkerMovingToCustomerLoading,
                    LocalizedTexts.ProductName(snapshot.ProductType.Value)),
                WarehouseWorkerStatusId.MovingToWorkerTrolley => Resolve(
                    LocalizationKey.HudWarehouseWorkerMovingToWorkerTrolley),
                WarehouseWorkerStatusId.MovingWorkerTrolleyToCustomerLoading =>
                    Resolve(
                        LocalizationKey.HudWarehouseWorkerMovingWorkerTrolleyToCustomerLoading,
                        snapshot.BatchProductCount.Value),
                WarehouseWorkerStatusId.ReturningWorkerTrolley => Resolve(
                    LocalizationKey.HudWarehouseWorkerReturningWorkerTrolley),
                WarehouseWorkerStatusId.Blocked =>
                    Resolve(LocalizationKey.HudWarehouseWorkerBlocked),
                _ => throw new ArgumentOutOfRangeException()
            };

        private string BuildOfferLineDetails(ConsultationOfferSnapshot offer)
        {
            var lines = new string[offer.Lines.Count + 1];
            lines[0] = Resolve(LocalizationKey.HudMaterialsHeader);
            for (int index = 0; index < offer.Lines.Count; index++)
            {
                ConsultationOfferLineSnapshot line = offer.Lines[index];
                lines[index + 1] = Resolve(
                    LocalizationKey.HudMaterialLine,
                    LocalizedTexts.ProductName(line.ProductType),
                    line.AvailableProductCount,
                    line.RequiredProductCount,
                    LocalizedTexts.ProductUnit(line.ProductType));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string ResolveProductCounts(
            ProcurementSnapshot procurement,
            ProcurementProductSnapshot product) =>
            Resolve(
                procurement.DemandKind == ProcurementDemandKind.ProjectForecast
                    ? LocalizationKey.HudProcurementForecastCounts
                    : LocalizationKey.HudProcurementConfirmedCounts,
                product.StockProductCount,
                product.InTransitProductCount,
                procurement.DemandKind == ProcurementDemandKind.ProjectForecast
                    ? product.MinimumRequiredProductCount
                    : product.RemainingRequiredProductCount,
                procurement.DemandKind == ProcurementDemandKind.ProjectForecast
                    ? product.MaximumRequiredProductCount
                    : product.ProjectedDeficitProductCount,
                LocalizedTexts.ProductUnit(product.ProductType));

        private string ResolveCartStatus(ProcurementSnapshot procurement)
        {
            ProcurementCartSnapshot cart = procurement.Cart;
            if (cart.PackageCount == 0)
                return Resolve(LocalizationKey.HudProcurementCartEmpty);

            return cart.PurchaseState switch
            {
                ProcurementPurchaseState.InsufficientStorage => Resolve(
                    LocalizationKey.ProcurementStatusInsufficientStorage,
                    procurement.FreeStorageSlotCount,
                    cart.RequiredStorageSlotCount),
                ProcurementPurchaseState.InsufficientMoney => Resolve(
                    LocalizationKey.ProcurementStatusInsufficientMoney,
                    cart.TotalCost),
                ProcurementPurchaseState.PlanWouldBecomeUnfulfillable => Resolve(
                    procurement.DemandKind == ProcurementDemandKind.ConfirmedOrder
                        ? LocalizationKey.ProcurementStatusPlanWouldBlockOrder
                        : LocalizationKey.ProcurementStatusPlanWouldBlockForecast),
                ProcurementPurchaseState.Available =>
                    Resolve(procurement.DemandKind == ProcurementDemandKind.ProjectForecast
                        ? LocalizationKey.ProcurementStatusPrepurchaseAvailable
                        : LocalizationKey.ProcurementStatusAvailable),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private string Resolve(LocalizationKey key) =>
            _localization.Resolve(key);

        private string Resolve(LocalizationKey key,
            params LocalizationArgument[] arguments) =>
            _localization.Resolve(LocalizedTexts.Text(key, arguments));

        private string Resolve(LocalizedText text) =>
            _localization.Resolve(text);

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
                fontStyle = FontStyle.Bold,
                wordWrap = true
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
                fontSize = 16
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
