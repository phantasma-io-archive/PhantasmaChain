using System;
using System.Collections.Generic;
using LunarLabs.Parser;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Domain
{
    public enum WalletStatus
    {
        Closed,
        Ready
    }
    public abstract class WalletLink
    {
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

        public struct Account : IAPIResult
        {
            public string id;
            public string address;
            public string name;
            public string avatar;
            public Balance[] balances;
        }

        public struct Invocation : IAPIResult
        {
            public string result;
        }


        private Random rnd = new Random();

        private Dictionary<string, string> authTokens = new Dictionary<string, string>();

        protected abstract PhantasmaKeys Keys { get; } 
        protected abstract WalletStatus Status { get; }

        public string Name { get; private set; }

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

        protected abstract Account GetAccount();

        protected abstract void InvokeScript(string script, int id, Action<int, DataNode, bool> callback);
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
                            }
                            else
                            {
                                var bytes = new byte[32];
                                rnd.NextBytes(bytes);
                                token = Base16.Encode(bytes);
                                authTokens[dapp] = token;
                            }

                            success = true;
                            root = APIUtils.FromAPIResult(new Authorization() { wallet = this.Name, dapp = dapp, token = token });
                        }
                        else
                        {
                            root = APIUtils.FromAPIResult(new Error() { message = "Invalid amount of arguments" });
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

                case "invokeScript":
                    {
                        root = ValidateRequest(args);
                        if (root == null)
                        {
                            if (args.Length == 3)
                            {
                                var script = args[1];

                                InvokeScript(script, id, callback);
                                return;
                            }
                            else
                            {
                                root = APIUtils.FromAPIResult(new Error() { message = "Invalid amount of arguments" });
                            }

                        }
                        break;
                    }

                default:
                    root = APIUtils.FromAPIResult(new Error() { message = "Invalid request type" });
                    break;
            }

            callback(id, root, success);
        }
    }
}