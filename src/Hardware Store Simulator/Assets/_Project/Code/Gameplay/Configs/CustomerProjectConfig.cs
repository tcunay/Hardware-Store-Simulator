using System;
using System.Collections.Generic;
using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "CustomerProjectConfig",
        menuName = "Hardware Store/Gameplay/Customer Project Config")]
    public sealed class CustomerProjectConfig : ScriptableObject, IValidatableConfig
    {
        public const int MaxLinesPerOffer = 2;

        [SerializeField] private CustomerProjectTypeId _projectType = CustomerProjectTypeId.CementFoundation;
        [SerializeField] private string _title = "Небольшой строительный проект";
        [SerializeField, TextArea] private string _request =
            "Подберите материалы под мой проект.";
        [SerializeField, Range(0, 2)] private int _defaultOfferIndex = 1;
        [SerializeField] private CustomerProjectOfferDefinition[] _offers =
        {
            new("Минимальный", "Только необходимый минимум.",
                new CustomerProjectLineDefinition(ProductTypeId.CementBag, 1)),
            new("Стандартный", "Базовый объём с небольшим запасом.",
                new CustomerProjectLineDefinition(ProductTypeId.CementBag, 2)),
            new("С запасом", "Больше материала, но выше выручка.",
                new CustomerProjectLineDefinition(ProductTypeId.CementBag, 3))
        };

        public CustomerProjectTypeId ProjectType => _projectType;
        public string ProjectTitle => _title;
        public string Request => _request;
        public int DefaultOfferIndex => _defaultOfferIndex;
        public IReadOnlyList<CustomerProjectOfferDefinition> Offers => _offers;

        public void Configure(CustomerProjectTypeId projectType, string title, string request,
            int defaultOfferIndex, CustomerProjectOfferDefinition[] offers)
        {
            _projectType = projectType;
            _title = title;
            _request = request;
            _defaultOfferIndex = defaultOfferIndex;
            _offers = offers == null
                ? null
                : (CustomerProjectOfferDefinition[])offers.Clone();
            Validate();
        }

        public void Validate()
        {
            const string owner = nameof(CustomerProjectConfig);
            ConfigValidation.RequireDefined(_projectType, owner, nameof(ProjectType));
            ConfigValidation.RequireNotBlank(_title, owner, nameof(ProjectTitle));
            ConfigValidation.RequireNotBlank(_request, owner, nameof(Request));
            if (_offers == null || _offers.Length != 3)
                throw new InvalidOperationException(
                    $"{owner}.{nameof(Offers)} must contain exactly three offers.");
            if (_defaultOfferIndex < 0 || _defaultOfferIndex >= _offers.Length)
                throw new InvalidOperationException(
                    $"{owner}.{nameof(DefaultOfferIndex)} must reference a configured offer.");

            for (int index = 0; index < _offers.Length; index++)
            {
                CustomerProjectOfferDefinition offer = _offers[index] ??
                    throw new InvalidOperationException(
                        $"{owner}.{nameof(Offers)}[{index}] must be configured.");
                offer.Validate(owner, index);
            }
        }
    }

    [Serializable]
    public sealed class CustomerProjectOfferDefinition
    {
        [SerializeField] private string _title;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private CustomerProjectLineDefinition[] _lines;

        public CustomerProjectOfferDefinition(string title, string description,
            params CustomerProjectLineDefinition[] lines)
        {
            _title = title;
            _description = description;
            _lines = lines == null ? null : (CustomerProjectLineDefinition[])lines.Clone();
        }

        public string OfferTitle => _title;
        public string Description => _description;
        public IReadOnlyList<CustomerProjectLineDefinition> Lines => _lines;

        internal void Validate(string owner, int offerIndex)
        {
            string offerOwner = $"{owner}.{nameof(CustomerProjectConfig.Offers)}[{offerIndex}]";
            ConfigValidation.RequireNotBlank(_title, offerOwner, nameof(OfferTitle));
            ConfigValidation.RequireNotBlank(_description, offerOwner, nameof(Description));
            if (_lines == null || _lines.Length == 0)
                throw new InvalidOperationException(
                    $"{offerOwner}.{nameof(Lines)} must contain at least one line.");
            if (_lines.Length > CustomerProjectConfig.MaxLinesPerOffer)
                throw new InvalidOperationException(
                    $"{offerOwner}.{nameof(Lines)} must contain no more than " +
                    $"{CustomerProjectConfig.MaxLinesPerOffer} lines.");

            var productTypes = new HashSet<ProductTypeId>();
            for (int lineIndex = 0; lineIndex < _lines.Length; lineIndex++)
            {
                CustomerProjectLineDefinition line = _lines[lineIndex] ??
                    throw new InvalidOperationException(
                        $"{offerOwner}.{nameof(Lines)}[{lineIndex}] must be configured.");
                line.Validate(offerOwner, lineIndex);
                if (!productTypes.Add(line.ProductType))
                    throw new InvalidOperationException(
                        $"{offerOwner}.{nameof(Lines)} contains duplicate product type " +
                        $"{line.ProductType}.");
            }
        }
    }

    [Serializable]
    public sealed class CustomerProjectLineDefinition
    {
        [SerializeField] private ProductTypeId _productType;
        [SerializeField, Min(1)] private int _requiredCount;

        public CustomerProjectLineDefinition(ProductTypeId productType, int requiredCount)
        {
            _productType = productType;
            _requiredCount = requiredCount;
        }

        public ProductTypeId ProductType => _productType;
        public int RequiredCount => _requiredCount;

        internal void Validate(string offerOwner, int lineIndex)
        {
            string lineOwner = $"{offerOwner}.{nameof(CustomerProjectOfferDefinition.Lines)}" +
                               $"[{lineIndex}]";
            ConfigValidation.RequireDefined(_productType, lineOwner, nameof(ProductType));
            ConfigValidation.RequirePositive(_requiredCount, lineOwner, nameof(RequiredCount));
        }
    }
}
