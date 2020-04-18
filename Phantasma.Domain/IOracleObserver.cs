using Phantasma.Storage.Context;

namespace Phantasma.Domain
{
    public interface IOracleObserver
    {
        void Update(INexus nexus, StorageContext storage);
    }
}

