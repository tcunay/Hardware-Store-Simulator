using System;
using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Configs;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class ConsultationOfferFactory : IConsultationOfferFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public ConsultationOfferFactory(IIdentifierService identifiers,
            IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public void CreateOffers(GameEntity customerVisit)
        {
            ValidateCustomerVisit(customerVisit);

            CustomerProjectConfig project =
                _staticData.GetProject(customerVisit.CustomerProjectType);
            for (int offerIndex = 0; offerIndex < project.Offers.Count; offerIndex++)
            {
                CustomerProjectOfferDefinition definition = project.Offers[offerIndex];
                int reward = 0;
                int procurementCost = 0;
                try
                {
                    foreach (CustomerProjectLineDefinition line in definition.Lines)
                    {
                        ProductConfig product = _staticData.GetProduct(line.ProductType);
                        DeliveryConfig delivery = _staticData.GetDelivery(line.ProductType);
                        reward = checked(
                            reward + checked(product.UnitPrice * line.RequiredCount));
                        procurementCost = checked(
                            procurementCost +
                            checked(delivery.PurchaseUnitPrice * line.RequiredCount));
                    }
                }
                catch (OverflowException exception)
                {
                    throw new InvalidOperationException(
                        $"Customer project {project.ProjectType} offer {offerIndex} totals must " +
                        "fit a 32-bit signed integer.",
                        exception);
                }

                GameEntity offer = CreateEntity.Empty(_identifiers.Next())
                    .AddConsultationOfferVisitEntityId(customerVisit.EntityId)
                    .AddOfferIndex(offerIndex)
                    .AddOfferTitle(definition.OfferTitle)
                    .AddOfferDescription(definition.Description)
                    .AddOrderReward(reward)
                    .AddExpectedProfit(checked(reward - procurementCost))
                    .With(x => x.isConsultationOffer = true)
                    .With(x => x.isSelectedConsultationOffer =
                        offerIndex == project.DefaultOfferIndex);

                for (int lineIndex = 0; lineIndex < definition.Lines.Count; lineIndex++)
                {
                    CustomerProjectLineDefinition line = definition.Lines[lineIndex];
                    CreateEntity.Empty(_identifiers.Next())
                        .AddConsultationOfferEntityId(offer.EntityId)
                        .AddStorageZoneEntityId(customerVisit.StorageZoneEntityId)
                        .AddLineIndex(lineIndex)
                        .AddProductType(line.ProductType)
                        .AddRequiredProductCount(line.RequiredCount)
                        .AddAvailableProductCount(0)
                        .With(x => x.isConsultationOfferLine = true);
                }
            }
        }

        private static void ValidateCustomerVisit(GameEntity customerVisit)
        {
            if (customerVisit == null)
                throw new ArgumentNullException(nameof(customerVisit));
            if (!customerVisit.isCustomerVisit || !customerVisit.hasEntityId ||
                !customerVisit.hasCustomerProjectType ||
                !customerVisit.hasStorageZoneEntityId || customerVisit.isOrder)
            {
                throw new InvalidOperationException(
                    "Consultation offers require a fresh configured customer visit.");
            }
        }
    }
}
