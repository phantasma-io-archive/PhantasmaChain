using System;
using System.Collections.Generic;
using LunarLabs.Parser;
using Phantasma.Core;
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
        public const int LinkProtocol = 1;

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
            public string alias;
            public string address;
            public string name;
            public string avatar;
            public string nexus;
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

        public struct Signature : IAPIResult
        {
            public string signature;
            public string random;
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

        protected abstract void Authorize(string dapp, string token, Action<bool, string> callback);

        protected abstract Account GetAccount(); // TODO this one also maybe use callbacks later, but not neessary for now...

        protected abstract void InvokeScript(byte[] script, int id, Action<byte[], string> callback);

        // NOTE for security, signData should not be usable as a way of signing transaction. That way the wallet is responsible for appending random bytes to the message, and return those in callback
        protected abstract void SignData(byte[] data, SignatureKind kind, int id, Action<string, string, string> callback);

        protected abstract void SignTransaction(string nexus, string chain, byte[] script, byte[] payload, int id, Action<Hash, string> callback);

        protected abstract void WriteArchive(Hash hash, int blockIndex, byte[] data, Action<bool, string> callback);

        public void Execute(string cmd, Action<int, DataNode, bool> callback)
        {
            var args = cmd.Split(',');

            DataNode answer;

            int id = 0;

            if (!int.TryParse(args[0], out id))
            {
                answer = APIUtils.FromAPIResult(new Error() { message = "Invalid request id" });
                callback(id, answer, false);
                return;
            }

            if (args.Length != 2)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = "Malformed request" });
                callback(id, answer, false);
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
                    answer = APIUtils.FromAPIResult(new Error() { message = $"Wallet is {status}" });
                    callback(id, answer, false);
                    return;
                }
            }

            if (_isPendingRequest)
            {
                answer = APIUtils.FromAPIResult(new Error() { message = $"A previous request is still pending" });
                callback(id, answer, false);
                return;
            }

            _isPendingRequest = true;

            switch (requestType)
            {
                case "version":
                    {
                        answer = APIUtils.FromAPIResult(new Invocation() { result = LinkProtocol.ToString() });
                        success = true;
                        break;
                    }

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
                                answer = APIUtils.FromAPIResult(new Authorization() { wallet = this.Name, dapp = dapp, token = token });
                            }
                            else
                            {
                                var bytes = new byte[32];
                                rnd.NextBytes(bytes);
                                token = Base16.Encode(bytes);

                                this.Authorize(dapp, token, (authorized, error) =>
                                {
                                    if (authorized)
                                    {
                                        authTokens[dapp] = token;

                                        success = true;
                                        answer = APIUtils.FromAPIResult(new Authorization() { wallet = this.Name, dapp = dapp, token = token });
                                    }
                                    else
                                    {
                                        answer = APIUtils.FromAPIResult(new Error() { message = error});
                                    }

                                    callback(id, answer, success);
                                    _isPendingRequest = false;
                                });

                                return;
                            }

                        }
                        else
                        {
                            answer = APIUtils.FromAPIResult(new Error() { message = $"authorize: Invalid amount of arguments: {args.Length} instead of 2" });
                        }

                        break;
                    }

                case "getAccount":
                    {
                        answer = ValidateRequest(args);
                        if (answer == null)
                        {
                            var account = this.GetAccount();
                            answer = APIUtils.FromAPIResult(account);

                            success = true;
                        }

                        break;
                    }

                case "signData":
                    {
                        answer = ValidateRequest(args);
                        if (answer == null)
                        {
                            if (args.Length == 5)
                            {
                                var data = Base16.Decode(args[3]);
                                var signatureKind = (SignatureKind) Enum.Parse(typeof(SignatureKind), args[4], true);

                                SignData(data, signatureKind, id, (signature, random, txError) => {
                                    if (signature != null)
                                    {
                                        success = true;
                                        answer = APIUtils.FromAPIResult(new Signature() { signature = signature, random = random });
                                    }
                                    else
                                    {
                                        answer = APIUtils.FromAPIResult(new Error() { message = txError });
                                    }

                                    callback(id, answer, success);
                                    _isPendingRequest = false;
                                });

                                return;
                            }
                            else
                            {
                                answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid amount of arguments: {args.Length} instead of 5" });
                            }

                        }
                        break;
                    }

                case "signTx":
                    {
                        answer = ValidateRequest(args);
                        if (answer == null)
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
                                        answer = APIUtils.FromAPIResult(new Transaction() { hash = hash.ToString() });
                                    }
                                    else
                                    {
                                        answer = APIUtils.FromAPIResult(new Error() { message = txError });
                                    }

                                    callback(id, answer, success);
                                    _isPendingRequest = false;
                                });

                                return;
                            }
                            else
                            {
                                answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid amount of arguments: {args.Length} instead of 7" });
                            }

                        }
                        break;
                    }

                case "invokeScript":
                    {
                        answer = ValidateRequest(args);
                        if (answer == null)
                        {
                            if (args.Length == 4)
                            {
                                var script = Base16.Decode(args[1]);

                                InvokeScript(script, id, (invokeResult, invokeError) =>
                                {
                                    if (invokeResult != null)
                                    {
                                        success = true;
                                        answer = APIUtils.FromAPIResult(new Invocation() { result = Base16.Encode(invokeResult) });
                                    }
                                    else
                                    {
                                        answer = APIUtils.FromAPIResult(new Error() { message = invokeError });
                                    }

                                    callback(id, answer, success);
                                    _isPendingRequest = false;
                                });
                                return;
                            }
                            else
                            {
                                answer = APIUtils.FromAPIResult(new Error() { message = $"invokeScript: Invalid amount of arguments: {args.Length} instead of 4" });
                            }

                        }
                        break;
                    }

                case "writeArchive":
                    {
                        answer = ValidateRequest(args);
                        if (answer == null)
                        {
                            if (args.Length == 6)
                            {
                                var archiveHash = Hash.Parse(args[1]);
                                var blockIndex = int.Parse(args[2]);
                                var bytes = Base16.Decode(args[3]);

                                WriteArchive(archiveHash, blockIndex, bytes, (result, error) =>
                                {
                                    if (result)
                                    {
                                        success = true;
                                        answer = APIUtils.FromAPIResult(new Transaction() { hash = archiveHash.ToString() });
                                    }
                                    else
                                    {
                                        answer = APIUtils.FromAPIResult(new Error() { message = error });
                                    }

                                    callback(id, answer, success);
                                    _isPendingRequest = false;
                                });
                                return;
                            }
                            else
                            {
                                answer = APIUtils.FromAPIResult(new Error() { message = $"invokeScript: Invalid amount of arguments: {args.Length} instead of 6" });
                            }

                        }
                        break;
                    }

                default:
                    answer = APIUtils.FromAPIResult(new Error() { message = "Invalid request type" });
                    break;
            }

            callback(id, answer, success);
            _isPendingRequest = false;
        }

        public void Revoke(string dapp, string token)
        {
            Throw.If(!authTokens.ContainsKey(dapp), "unknown dapp");

            var currentToken = authTokens[dapp];
            Throw.If(currentToken != token, "invalid token");

            authTokens.Remove(dapp);
        }
    }
}
