using Entitas;

namespace HardwareStore.Common.Entity.ToStrings
{
    public interface INamedEntity : IEntity
    {
        string EntityName(IComponent[] components);
        string BaseToString();
    }
}
