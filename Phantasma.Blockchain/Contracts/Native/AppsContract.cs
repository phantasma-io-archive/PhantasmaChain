using Phantasma.Blockchain.Storage;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Contracts.Native
{
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

        private const string APP_LIST = "_apps";

        public AppsContract() : base()
        {
        }

        public void RegisterApp(Address owner, string name)
        {
            Runtime.Expect(IsWitness(owner), "invalid witness");

            var chain = this.Runtime.Nexus.CreateChain(owner, name, Runtime.Chain, Runtime.Block);
            var app = new AppInfo()
            {
                id = name,
                title = name,
                url = "",
                description = "",
                icon = Hash.Null,
            };
            
            var list = Storage.FindCollectionForContract<AppInfo>(APP_LIST);
            list.Add(app);
        }

        private int FindAppIndex(string name, Collection<AppInfo> list)
        {
            var count = list.Count();
            for (int i=0; i<count; i++)
            {
                var app = list.Get(i);
                if (app.id == name)
                {
                    return i;
                }
            }

            return -1;
        }

        public void SetAppTitle(string name, string title)
        {
            var list = Storage.FindCollectionForContract<AppInfo>(APP_LIST);
            var index = FindAppIndex(name, list);
            Runtime.Expect(index >= 0, "app not found");

            var app = list.Get(index);
            app.title = title;
            list.Replace(index, app);
        }

        public void SetAppUrl(string name, string url)
        {
            var list = Storage.FindCollectionForContract<AppInfo>(APP_LIST);
            var index = FindAppIndex(name, list);
            Runtime.Expect(index >= 0, "app not found");

            var app = list.Get(index);
            app.url = url;
            list.Replace(index, app);
        }

        public void SetAppDescription(string name, string description)
        {
            var list = Storage.FindCollectionForContract<AppInfo>(APP_LIST);
            var index = FindAppIndex(name, list);
            Runtime.Expect(index >= 0, "app not found");

            var app = list.Get(index);
            app.description = description;
            list.Replace(index, app);
        }

        public AppInfo[] GetApps()
        {
            var list = Storage.FindCollectionForContract<AppInfo>(APP_LIST);
            return list.All();
        }
    }
}
