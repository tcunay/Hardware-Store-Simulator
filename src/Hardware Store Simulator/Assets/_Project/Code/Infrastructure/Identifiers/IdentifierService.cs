namespace HardwareStore.Infrastructure.Identifiers
{
    public sealed class IdentifierService : IIdentifierService
    {
        private int _nextIdentifier = 1;

        public int Next() => _nextIdentifier++;
    }
}
