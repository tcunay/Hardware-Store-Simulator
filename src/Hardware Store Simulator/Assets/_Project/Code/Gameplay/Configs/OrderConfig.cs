using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "OrderConfig", menuName = "Hardware Store/Gameplay/Order Config")]
    public sealed class OrderConfig : ScriptableObject, IValidatableConfig
    {
        [SerializeField] private ProductTypeId _requiredProductType = ProductTypeId.CementBag;
        [SerializeField] private string _customerProjectTitle = "Небольшой строительный проект";
        [SerializeField, TextArea] private string _customerRequest =
            "Подберите объём материала под мой проект.";
        [SerializeField, Range(0, 2)] private int _defaultOfferIndex = 1;
        [SerializeField] private OrderOfferDefinition[] _offers =
        {
            new("Минимальный", "Только необходимый минимум.", 1, 350),
            new("Стандартный", "Базовый объём с небольшим запасом.", 2, 700),
            new("С запасом", "Больше материала и ручной работы, но выше выручка.", 3, 1050)
        };

        public ProductTypeId RequiredProductType => _requiredProductType;
        public string CustomerProjectTitle => _customerProjectTitle;
        public string CustomerRequest => _customerRequest;
        public int DefaultOfferIndex => _defaultOfferIndex;
        public IReadOnlyList<OrderOfferDefinition> Offers => _offers;

        public void Configure(ProductTypeId requiredProductType, string customerProjectTitle,
            string customerRequest, int defaultOfferIndex, OrderOfferDefinition[] offers)
        {
            _requiredProductType = requiredProductType;
            _customerProjectTitle = customerProjectTitle;
            _customerRequest = customerRequest;
            _defaultOfferIndex = defaultOfferIndex;
            _offers = offers == null ? null : (OrderOfferDefinition[])offers.Clone();
            Validate();
        }

        public void Validate()
        {
            const string owner = nameof(OrderConfig);
            ConfigValidation.RequireDefined(
                _requiredProductType,
                owner,
                nameof(RequiredProductType));
            ConfigValidation.RequireNotBlank(
                _customerProjectTitle,
                owner,
                nameof(CustomerProjectTitle));
            ConfigValidation.RequireNotBlank(
                _customerRequest,
                owner,
                nameof(CustomerRequest));
            if (_offers == null || _offers.Length != 3)
                throw new InvalidOperationException(
                    $"{owner}.{nameof(Offers)} must contain exactly three offers.");
            if (_defaultOfferIndex < 0 || _defaultOfferIndex >= _offers.Length)
                throw new InvalidOperationException(
                    $"{owner}.{nameof(DefaultOfferIndex)} must reference a configured offer.");

            int previousProductCount = 0;
            for (int index = 0; index < _offers.Length; index++)
            {
                OrderOfferDefinition offer = _offers[index] ??
                    throw new InvalidOperationException(
                        $"{owner}.{nameof(Offers)}[{index}] must be configured.");
                offer.Validate(owner, index);
                if (offer.RequiredProductCount <= previousProductCount)
                {
                    throw new InvalidOperationException(
                        $"{owner}.{nameof(Offers)} must use strictly increasing product counts.");
                }

                previousProductCount = offer.RequiredProductCount;
            }
        }
    }

    [Serializable]
    public sealed class OrderOfferDefinition
    {
        [SerializeField] private string _title;
        [SerializeField, TextArea] private string _description;
        [SerializeField, Min(1)] private int _requiredProductCount;
        [SerializeField, Min(0)] private int _reward;

        public OrderOfferDefinition(string title, string description,
            int requiredProductCount, int reward)
        {
            _title = title;
            _description = description;
            _requiredProductCount = requiredProductCount;
            _reward = reward;
        }

        public string Title => _title;
        public string Description => _description;
        public int RequiredProductCount => _requiredProductCount;
        public int Reward => _reward;

        internal void Validate(string owner, int index)
        {
            string offerOwner = $"{owner}.{nameof(OrderConfig.Offers)}[{index}]";
            ConfigValidation.RequireNotBlank(_title, offerOwner, nameof(Title));
            ConfigValidation.RequireNotBlank(_description, offerOwner, nameof(Description));
            ConfigValidation.RequirePositive(
                _requiredProductCount,
                offerOwner,
                nameof(RequiredProductCount));
            ConfigValidation.RequireNonNegative(_reward, offerOwner, nameof(Reward));
        }
    }
}
