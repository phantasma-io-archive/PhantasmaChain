using System;
using System.Collections.Generic;
using LunarLabs.Parser;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;

namespace Phantasma.Domain
{
    public enum WalletStatus
    {
        Closed,
        Ready
    }
    public abstract class WalletLink
    {
        public const int WebSocketPort = 7090;

        public struct Error : IAPIResult
        {
            public string message;
        }

        public struct Authorization : IAPIResult
        {
            public string wallet;
            public string dapp;
            public string token;
        }

        public struct Balance : IAPIResult
        {
            public string symbol;
            public string value;
            public int decimals;
        }

        public struct File: IAPIResult
        {
            public string name;
            public int size;
            public uint date;
            public string hash;
        }

        public struct Account : IAPIResult
        {
            public string id;
            public string address;
            public string name;
            public string avatar;
            public Balance[] balances;
            public File[] files;
        }

        public struct Invocation : IAPIResult
        {
            public string result;
        }

        public struct Transaction : IAPIResult
        {
            public string hash;
        }

        private Random rnd = new Random();

        private Dictionary<string, string> authTokens = new Dictionary<string, string>();

        protected abstract PhantasmaKeys Keys { get; }
        protected abstract WalletStatus Status { get; }

        public string Name { get; private set; }

        private bool _isPendingRequest;

        public WalletLink(string name)
        {
            this.Name = name;
        }

        private DataNode ValidateRequest(string[] args)
        {
            if (args.Length >= 3)
            {
                string dapp = args[args.Length - 2];
                string token = args[args.Length - 1];

                if (authTokens.ContainsKey(dapp))
                {
                    if (authTokens[dapp] == token)
                    {
                        return null;
                    }
                }
            }

            return APIUtils.FromAPIResult(new Error() { message = "Invalid or missing API token" });
        }

        protected abstract void Authorize(string dapp, Action<bool, string> callback);

        protected abstract Account GetAccount(); // TODO this one also maybe use callbacks later, but not neessary for now...

        protected abstract void InvokeScript(byte[] script, int id, Action<byte[], string> callback);

        protected abstract void SignTransaction(string nexus, string chain, byte[] script, byte[] payload, int id, Action<Hash, string> callback);

        public void Execute(string cmd, Action<int, DataNode, bool> callback)
        {
            var args = cmd.Split(',');

            DataNode root;

            int id = 0;

            if (int.TryParse(args[0], out id))
            {

            }

            if (args.Length != 2)
            {
                root = APIUtils.FromAPIResult(new Error() { message = "Malformed request" });
                callback(id, root, false);
                return;
            }

            cmd = args[1];
            args = cmd.Split('/');

            bool success = false;

            var requestType = args[0];

            if (requestType != "authorize")
            {
                var status = this.Status;
                if (status != WalletStatus.Ready)
                {
                    root = APIUtils.FromAPIResult(new Error() { message = $"Wallet is {status}" });
                    callback(id, root, false);
                    return;
                }
            }

            if (_isPendingRequest)
            {
                root = APIUtils.FromAPIResult(new Error() { message = $"A previouus request is still pending" });
                callback(id, root, false);
                return;
            }

            _isPendingRequest = true;

            switch (requestType)
            {
                case "authorize":
                    {
                        if (args.Length == 2)
                        {
                            string token;
                            var dapp = args[1];

                            if (authTokens.ContainsKey(dapp))
                            {
                                token = authTokens[dapp];
                                success = true;
                                root = APIUtils.FromAPIResult(new Authorization() { wallet = this.Name, dapp = dapp, token = token });
                            }
                            else
                            {
                                this.Authorize(dapp, (authorized, error) =>
                                {
                                    if (authorized)
                                    {
                                        var bytes = new byte[32];
                                        rnd.NextBytes(bytes);
                                        token = Base16.Encode(bytes);
                                        authTokens[dapp] = token;

                                        success = true;
                                        root = APIUtils.FromAPIResult(new Authorization() { wallet = this.Name, dapp = dapp, token = token });
                                    }
                                    else
                                    {
                                        root = APIUtils.FromAPIResult(new Error() { message = error});
                                    }

                                    callback(id, root, success);
                                    _isPendingRequest = false;
                                });

                                return;
                            }

                        }
                        else
                        {
                            root = APIUtils.FromAPIResult(new Error() { message = $"authorize: Invalid amount of arguments: {args.Length} instead of 2" });
                        }

                        break;
                    }

                case "getAccount":
                    {
                        root = ValidateRequest(args);
                        if (root == null)
                        {
                            var account = this.GetAccount();
                            root = APIUtils.FromAPIResult(account);

                            success = true;
                        }

                        break;
                    }

                case "signTx":
                    {
                        root = ValidateRequest(args);
                        if (root == null)
                        {
                            if (args.Length == 7)
                            {
                                var nexus = args[1];
                                var chain = args[2];
                                var script = Base16.Decode(args[3]);
                                byte[] payload = args[4].Length > 0 ? Base16.Decode(args[4]): null;

                                SignTransaction(nexus, chain, script, payload, id, (hash, txError) => { 
                                    if (hash != Hash.Null)
                                    {
                                        success = true;
                                        root = APIUtils.FromAPIResult(new Transaction() { hash = hash.ToString() });
                                    }
                                    else
                                    {
                                        root = APIUtils.FromAPIResult(new Error() { message = txError });
                                    }

                                    callback(id, root, success);
                                    _isPendingRequest = false;
                                });

                                return;
                            }
                            else
                            {
                                root = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid amount of arguments: {args.Length} instead of 7" });
                            }

                        }
                        break;
                    }

                case "invokeScript":
                    {
                        root = ValidateRequest(args);
                        if (root == null)
                        {
                            if (args.Length == 4)
                            {
                                var script = Base16.Decode(args[1]);

                                InvokeScript(script, id, (invokeResult, invokeError) =>
                                {
                                    if (invokeResult != null)
                                    {
                                        success = true;
                                        root = APIUtils.FromAPIResult(new Invocation() {  result = Base16.Encode(invokeResult) });
                                    }
                                    else
                                    {
                                        root = APIUtils.FromAPIResult(new Error() { message = invokeError });
                                    }

                                    callback(id, root, success);
                                    _isPendingRequest = false;
                                });
                                return;
                            }
                            else
                            {
                                root = APIUtils.FromAPIResult(new Error() { message = $"invokeScript: Invalid amount of arguments: {args.Length} instead of 4" });
                            }

                        }
                        break;
                    }

                default:
                    root = APIUtils.FromAPIResult(new Error() { message = "Invalid request type" });
                    break;
            }

            callback(id, root, success);
            _isPendingRequest = false;
        }
    }
}
