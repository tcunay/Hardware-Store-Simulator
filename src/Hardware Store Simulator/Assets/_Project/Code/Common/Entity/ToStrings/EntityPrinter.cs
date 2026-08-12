using System;
using System.Text;
using DesperateDevs.Extensions;
using Entitas;

namespace HardwareStore.Common.Entity.ToStrings
{
    public sealed class EntityPrinter
    {
        private readonly INamedEntity _entity;
        private string _previousBaseToString;
        private string _toStringCache;
        private StringBuilder _builder;

        public EntityPrinter(INamedEntity entity)
        {
            _entity = entity;
        }

        public string BuildToString()
        {
            if (_toStringCache != null)
                return _toStringCache;

            _builder ??= new StringBuilder();
            _builder.Clear();

            IComponent[] components = _entity.GetComponents();
            if (components.Length == 0)
                return "No components";

            _builder.Append(_entity.EntityName(components));
            _builder.Append("  ‹");

            int lastIndex = components.Length - 1;
            for (int index = 0; index < components.Length; index++)
            {
                IComponent component = components[index];
                Type componentType = component.GetType();
                bool overridesToString =
                    componentType.GetMethod(nameof(ToString)).DeclaringType
                        .ImplementsInterface<IComponent>();

                _builder.Append(overridesToString
                    ? component.ToString()
                    : componentType.Name.RemoveComponentSuffix());

                if (index < lastIndex)
                    _builder.Append(", ");
            }

            _builder.Append("›  retained: ");
            _builder.Append(_entity.retainCount);

            _toStringCache = _builder.ToString();
            _previousBaseToString = _entity.BaseToString();
            return _toStringCache;
        }

        public void InvalidateCache()
        {
            if (_previousBaseToString != _entity.BaseToString())
                _toStringCache = null;
        }
    }
}
