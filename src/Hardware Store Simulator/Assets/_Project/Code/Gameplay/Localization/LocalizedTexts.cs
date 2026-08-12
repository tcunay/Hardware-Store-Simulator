using System;
using HardwareStore.Gameplay.Components;
using StoreDayPhaseId = HardwareStore.Gameplay.Presentation.StoreDayPhase;

namespace HardwareStore.Gameplay.Localization
{
    public static class LocalizedTexts
    {
        public static LocalizedText Text(LocalizationKey key,
            params LocalizationArgument[] arguments) =>
            new(key, arguments);

        public static LocalizedText ProductName(ProductTypeId productType) =>
            Text(productType switch
            {
                ProductTypeId.CementBag => LocalizationKey.ProductCementBagName,
                ProductTypeId.BoardBundle => LocalizationKey.ProductBoardBundleName,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(productType), productType, "Unsupported product type.")
            });

        public static LocalizedText ProductUnit(ProductTypeId productType) =>
            Text(productType switch
            {
                ProductTypeId.CementBag => LocalizationKey.ProductPieceUnit,
                ProductTypeId.BoardBundle => LocalizationKey.ProductPieceUnit,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(productType), productType, "Unsupported product type.")
            });

        public static LocalizedText ProjectTitle(CustomerProjectTypeId projectType) =>
            Text(projectType switch
            {
                CustomerProjectTypeId.CementFoundation =>
                    LocalizationKey.ProjectCementFoundationTitle,
                CustomerProjectTypeId.LumberShelving =>
                    LocalizationKey.ProjectLumberShelvingTitle,
                CustomerProjectTypeId.WorkbenchFoundation =>
                    LocalizationKey.ProjectWorkbenchFoundationTitle,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(projectType), projectType, "Unsupported customer project type.")
            });

        public static LocalizedText ProjectRequest(CustomerProjectTypeId projectType) =>
            Text(projectType switch
            {
                CustomerProjectTypeId.CementFoundation =>
                    LocalizationKey.ProjectCementFoundationRequest,
                CustomerProjectTypeId.LumberShelving =>
                    LocalizationKey.ProjectLumberShelvingRequest,
                CustomerProjectTypeId.WorkbenchFoundation =>
                    LocalizationKey.ProjectWorkbenchFoundationRequest,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(projectType), projectType, "Unsupported customer project type.")
            });

        public static LocalizedText OfferTitle(CustomerProjectTypeId projectType,
            int offerIndex) =>
            Text(OfferTitleKey(projectType, offerIndex));

        public static LocalizedText OfferDescription(CustomerProjectTypeId projectType,
            int offerIndex) =>
            Text(OfferDescriptionKey(projectType, offerIndex));

        public static LocalizedText StoreDayPhase(StoreDayPhaseId phase) =>
            Text(phase switch
            {
                StoreDayPhaseId.Preparing =>
                    LocalizationKey.HudDayPhasePreparing,
                StoreDayPhaseId.Open => LocalizationKey.HudDayPhaseOpen,
                StoreDayPhaseId.Closing =>
                    LocalizationKey.HudDayPhaseClosing,
                StoreDayPhaseId.Report => LocalizationKey.HudDayPhaseReport,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(phase), phase, "Unsupported store day phase.")
            });

        private static LocalizationKey OfferTitleKey(CustomerProjectTypeId projectType,
            int offerIndex) =>
            projectType switch
            {
                CustomerProjectTypeId.CementFoundation => offerIndex switch
                {
                    0 => LocalizationKey.ProjectCementFoundationOffer0Title,
                    1 => LocalizationKey.ProjectCementFoundationOffer1Title,
                    2 => LocalizationKey.ProjectCementFoundationOffer2Title,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                CustomerProjectTypeId.LumberShelving => offerIndex switch
                {
                    0 => LocalizationKey.ProjectLumberShelvingOffer0Title,
                    1 => LocalizationKey.ProjectLumberShelvingOffer1Title,
                    2 => LocalizationKey.ProjectLumberShelvingOffer2Title,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                CustomerProjectTypeId.WorkbenchFoundation => offerIndex switch
                {
                    0 => LocalizationKey.ProjectWorkbenchFoundationOffer0Title,
                    1 => LocalizationKey.ProjectWorkbenchFoundationOffer1Title,
                    2 => LocalizationKey.ProjectWorkbenchFoundationOffer2Title,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                _ => throw new ArgumentOutOfRangeException(
                    nameof(projectType), projectType, "Unsupported customer project type.")
            };

        private static LocalizationKey OfferDescriptionKey(
            CustomerProjectTypeId projectType, int offerIndex) =>
            projectType switch
            {
                CustomerProjectTypeId.CementFoundation => offerIndex switch
                {
                    0 => LocalizationKey.ProjectCementFoundationOffer0Description,
                    1 => LocalizationKey.ProjectCementFoundationOffer1Description,
                    2 => LocalizationKey.ProjectCementFoundationOffer2Description,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                CustomerProjectTypeId.LumberShelving => offerIndex switch
                {
                    0 => LocalizationKey.ProjectLumberShelvingOffer0Description,
                    1 => LocalizationKey.ProjectLumberShelvingOffer1Description,
                    2 => LocalizationKey.ProjectLumberShelvingOffer2Description,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                CustomerProjectTypeId.WorkbenchFoundation => offerIndex switch
                {
                    0 => LocalizationKey.ProjectWorkbenchFoundationOffer0Description,
                    1 => LocalizationKey.ProjectWorkbenchFoundationOffer1Description,
                    2 => LocalizationKey.ProjectWorkbenchFoundationOffer2Description,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                _ => throw new ArgumentOutOfRangeException(
                    nameof(projectType), projectType, "Unsupported customer project type.")
            };

        private static ArgumentOutOfRangeException InvalidOfferIndex(int offerIndex) =>
            new(nameof(offerIndex), offerIndex, "Offer index must be between 0 and 2.");
    }
}
