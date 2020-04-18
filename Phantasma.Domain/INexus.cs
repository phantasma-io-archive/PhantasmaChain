using Phantasma.Storage.Context;

namespace Phantasma.Domain
{
    public interface INexus
    {
        void Attach(IOracleObserver observer);

        void Detach(IOracleObserver observer);

        void Notify(StorageContext storage);
    }
}

