using Phantasma.Cryptography;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    // TODO refactor this into "metadata", no need for a custom struct with limited fields
    public struct AppInfo
    {
        public string id;
        public string title;
        public string url;
        public string description;
        public Hash icon;
    }

    public class AppsContract : SmartContract
    {
        public override string Name => "apps";

        internal StorageList _apps;

        public AppsContract() : base()
        {
        }

        public void RegisterApp(Address owner, string name)
        {
            Runtime.Expect(IsWitness(owner), "invalid witness");

            var chain = this.Runtime.Nexus.CreateChain(this.Storage, owner, name, Runtime.Chain, new string[] {/*TODO*/ });
            var app = new AppInfo()
            {
                id = name,
                title = name,
                url = "",
                description = "",
                icon = Hash.Null,
            };

            _apps.Add(app);
        }

        private int FindAppIndex(string name)
        {
            var count = _apps.Count();
            for (int i = 0; i < count; i++)
            {
                var app = _apps.Get<AppInfo>(i);
                if (app.id == name)
                {
                    return i;
                }
            }

            return -1;
        }

        public void SetAppTitle(string name, string title)
        {
            var index = FindAppIndex(name);
            Runtime.Expect(index >= 0, "app not found");

            var app = _apps.Get<AppInfo>(index);
            app.title = title;
            _apps.Replace(index, app);
        }

        public void SetAppUrl(string name, string url)
        {
            var index = FindAppIndex(name);
            Runtime.Expect(index >= 0, "app not found");

            var app = _apps.Get<AppInfo>(index);
            app.url = url;
            _apps.Replace(index, app);
        }

        public void SetAppDescription(string name, string description)
        {
            var index = FindAppIndex(name);
            Runtime.Expect(index >= 0, "app not found");

            var app = _apps.Get<AppInfo>(index);
            app.description = description;
            _apps.Replace(index, app);
        }

        public AppInfo[] GetApps()
        {
            return _apps.All<AppInfo>();
        }
    }
}