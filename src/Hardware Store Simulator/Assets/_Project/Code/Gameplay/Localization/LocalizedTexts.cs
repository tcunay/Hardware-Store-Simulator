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
                ProductTypeId.BrickPack => LocalizationKey.ProductBrickPackName,
                ProductTypeId.DrywallSheet => LocalizationKey.ProductDrywallSheetName,
                ProductTypeId.PaintBucket => LocalizationKey.ProductPaintBucketName,
                ProductTypeId.InsulationRoll => LocalizationKey.ProductInsulationRollName,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(productType), productType, "Unsupported product type.")
            });

        public static LocalizedText ProductUnit(ProductTypeId productType) =>
            Text(productType switch
            {
                ProductTypeId.CementBag => LocalizationKey.ProductPieceUnit,
                ProductTypeId.BoardBundle => LocalizationKey.ProductPieceUnit,
                ProductTypeId.BrickPack => LocalizationKey.ProductPieceUnit,
                ProductTypeId.DrywallSheet => LocalizationKey.ProductPieceUnit,
                ProductTypeId.PaintBucket => LocalizationKey.ProductPieceUnit,
                ProductTypeId.InsulationRoll => LocalizationKey.ProductPieceUnit,
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
                CustomerProjectTypeId.GardenWall =>
                    LocalizationKey.ProjectGardenWallTitle,
                CustomerProjectTypeId.DrywallPartition =>
                    LocalizationKey.ProjectDrywallPartitionTitle,
                CustomerProjectTypeId.WorkshopRenovation =>
                    LocalizationKey.ProjectWorkshopRenovationTitle,
                CustomerProjectTypeId.GarageInsulation =>
                    LocalizationKey.ProjectGarageInsulationTitle,
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
                CustomerProjectTypeId.GardenWall =>
                    LocalizationKey.ProjectGardenWallRequest,
                CustomerProjectTypeId.DrywallPartition =>
                    LocalizationKey.ProjectDrywallPartitionRequest,
                CustomerProjectTypeId.WorkshopRenovation =>
                    LocalizationKey.ProjectWorkshopRenovationRequest,
                CustomerProjectTypeId.GarageInsulation =>
                    LocalizationKey.ProjectGarageInsulationRequest,
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
                CustomerProjectTypeId.GardenWall => offerIndex switch
                {
                    0 => LocalizationKey.ProjectGardenWallOffer0Title,
                    1 => LocalizationKey.ProjectGardenWallOffer1Title,
                    2 => LocalizationKey.ProjectGardenWallOffer2Title,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                CustomerProjectTypeId.DrywallPartition => offerIndex switch
                {
                    0 => LocalizationKey.ProjectDrywallPartitionOffer0Title,
                    1 => LocalizationKey.ProjectDrywallPartitionOffer1Title,
                    2 => LocalizationKey.ProjectDrywallPartitionOffer2Title,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                CustomerProjectTypeId.WorkshopRenovation => offerIndex switch
                {
                    0 => LocalizationKey.ProjectWorkshopRenovationOffer0Title,
                    1 => LocalizationKey.ProjectWorkshopRenovationOffer1Title,
                    2 => LocalizationKey.ProjectWorkshopRenovationOffer2Title,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                CustomerProjectTypeId.GarageInsulation => offerIndex switch
                {
                    0 => LocalizationKey.ProjectGarageInsulationOffer0Title,
                    1 => LocalizationKey.ProjectGarageInsulationOffer1Title,
                    2 => LocalizationKey.ProjectGarageInsulationOffer2Title,
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
                CustomerProjectTypeId.GardenWall => offerIndex switch
                {
                    0 => LocalizationKey.ProjectGardenWallOffer0Description,
                    1 => LocalizationKey.ProjectGardenWallOffer1Description,
                    2 => LocalizationKey.ProjectGardenWallOffer2Description,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                CustomerProjectTypeId.DrywallPartition => offerIndex switch
                {
                    0 => LocalizationKey.ProjectDrywallPartitionOffer0Description,
                    1 => LocalizationKey.ProjectDrywallPartitionOffer1Description,
                    2 => LocalizationKey.ProjectDrywallPartitionOffer2Description,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                CustomerProjectTypeId.WorkshopRenovation => offerIndex switch
                {
                    0 => LocalizationKey.ProjectWorkshopRenovationOffer0Description,
                    1 => LocalizationKey.ProjectWorkshopRenovationOffer1Description,
                    2 => LocalizationKey.ProjectWorkshopRenovationOffer2Description,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                CustomerProjectTypeId.GarageInsulation => offerIndex switch
                {
                    0 => LocalizationKey.ProjectGarageInsulationOffer0Description,
                    1 => LocalizationKey.ProjectGarageInsulationOffer1Description,
                    2 => LocalizationKey.ProjectGarageInsulationOffer2Description,
                    _ => throw InvalidOfferIndex(offerIndex)
                },
                _ => throw new ArgumentOutOfRangeException(
                    nameof(projectType), projectType, "Unsupported customer project type.")
            };

        private static ArgumentOutOfRangeException InvalidOfferIndex(int offerIndex) =>
            new(nameof(offerIndex), offerIndex, "Offer index must be between 0 and 2.");
    }
}
