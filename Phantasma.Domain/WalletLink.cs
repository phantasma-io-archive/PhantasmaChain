using System;
using System.Collections.Generic;
using System.Linq;
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
        public const int LinkProtocol = 2;

        public struct Error : IAPIResult
        {
            public string message;
        }

        public struct Authorization : IAPIResult
        {
            public string wallet;
            public string nexus;
            public string dapp;
            public string token;
            public int version;
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
            public string platform;
            public string external;
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

        public class Connection
        {
            public readonly string Token;
            public readonly int Version;

            public Connection(string token, int version)
            {
                this.Token = token;
                this.Version = version;
            }
        }

        private Random rnd = new Random();

        private Dictionary<string, Connection> _connections = new Dictionary<string, Connection>();

        protected abstract WalletStatus Status { get; }

        public abstract string Nexus { get; }

        public abstract string Name { get; }

        private bool _isPendingRequest;

        public WalletLink()
        {
        }

        private Connection ValidateRequest(string[] args)
        {
            if (args.Length >= 3)
            {
                string dapp = args[args.Length - 2];
                string token = args[args.Length - 1];

                if (_connections.ContainsKey(dapp))
                {
                    var connection = _connections[dapp];
                    if (connection.Token == token)
                    {
                        return connection;
                    }
                }
            }

            return null;
        }

        protected abstract void Authorize(string dapp, string token, int version, Action<bool, string> callback);

        protected abstract void GetAccount(string platform, Action<Account, string> callback);

        protected abstract void InvokeScript(string chain, byte[] script, int id, Action<byte[], string> callback);

        // NOTE for security, signData should not be usable as a way of signing transaction. That way the wallet is responsible for appending random bytes to the message, and return those in callback
        protected abstract void SignData(string platform, SignatureKind kind, byte[] data, int id, Action<string, string, string> callback);

        protected abstract void SignTransaction(string platform, SignatureKind kind, string chain, byte[] script, byte[] payload, int id, Action<Hash, string> callback);

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

            Connection connection = null;

            if (requestType != "authorize")
            {
                connection = ValidateRequest(args);
                if (connection == null)
                {
                    answer = APIUtils.FromAPIResult(new Error() { message = "Invalid or missing API token" });
                    callback(id, answer, false);
                    return;
                }

                // exclude dapp/token args
                args = args.Take(args.Length - 2).ToArray();
            }

            args = args.Skip(1).ToArray();

            switch (requestType)
            {
                case "authorize":
                    {
                        if (args.Length == 1 || args.Length == 2)
                        {
                            string token;
                            var dapp = args[0];

                            int version;
                            
                            if (args.Length == 2)
                            {
                                var str = args[1];
                                if (!int.TryParse(str, out version))
                                {
                                    answer = APIUtils.FromAPIResult(new Error() { message = $"authorize: Invalid version: {str}"});
                                    callback(id, answer, false);
                                    _isPendingRequest = false;
                                    return;
                                }
                            }
                            else 
                            { 
                                version = 1; 
                            }

                            if (_connections.ContainsKey(dapp))
                            {
                                connection = _connections[dapp];
                                success = true;
                                answer = APIUtils.FromAPIResult(new Authorization() { wallet = this.Name, nexus = this.Nexus, dapp = dapp, token = connection.Token, version = connection.Version });
                            }
                            else
                            {
                                var bytes = new byte[32];
                                rnd.NextBytes(bytes);
                                token = Base16.Encode(bytes);

                                this.Authorize(dapp, token, version, (authorized, error) =>
                                {
                                    if (authorized)
                                    {
                                        _connections[dapp] = new Connection(token, version);

                                        success = true;
                                        answer = APIUtils.FromAPIResult(new Authorization() { wallet = this.Name, nexus = this.Nexus, dapp = dapp, token = token });
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
                            answer = APIUtils.FromAPIResult(new Error() { message = $"authorize: Invalid amount of arguments: {args.Length}" });
                        }

                        break;
                    }

                case "getAccount":
                    {
                        int expectedLength;

                        switch (connection.Version)
                        {
                            case 1:
                                expectedLength = 0;
                                break;

                            default:
                                expectedLength = 1;
                                break;
                        }

                        if (args.Length == expectedLength)
                        {
                            string platform;

                            if (connection.Version >= 2)
                            {
                                platform = args[0].ToLower();
                            }
                            else
                            {
                                platform = "phantasma";
                            }

                            GetAccount(platform, (account, error) => {
                                if (error == null)
                                {
                                    success = true;
                                    answer = APIUtils.FromAPIResult(account);
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
                            answer = APIUtils.FromAPIResult(new Error() { message = $"getAccount: Invalid amount of arguments: {args.Length}" });
                        }

                        break;
                    }

                case "signData":
                    {
                        int expectedLength;

                        switch (connection.Version)
                        {
                            case 1:
                                expectedLength = 2;
                                break;

                            default:
                                expectedLength = 3;
                                break;
                        }

                        if (args.Length == expectedLength)
                        {
                            var data = Base16.Decode(args[0], false);
                            if (data == null)
                            {
                                answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid input received" });
                            }
                            else
                            {
                                SignatureKind signatureKind;

                                if (!Enum.TryParse<SignatureKind>(args[1], out signatureKind))
                                {
                                    answer = APIUtils.FromAPIResult(new Error() { message = $"signData: Invalid signature: " + args[1] });
                                    callback(id, answer, false);
                                    _isPendingRequest = false;
                                    return;
                                }

                                var platform = connection.Version >= 2 ? args[2].ToLower() : "phantasma";

                                SignData(platform, signatureKind, data, id, (signature, random, txError) => {
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
                            }

                            return;
                        }
                        else
                        {
                            answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid amount of arguments: {args.Length}" });
                        }
                        break;
                    }

                case "signTx":
                    {
                        int expectedLength;

                        switch (connection.Version)
                        {
                            case 1:
                                expectedLength = 3;
                                break;

                            default:
                                expectedLength = 5;
                                break;
                        }

                        if (args.Length == expectedLength)
                        {
                            var chain = args[0];
                            var script = Base16.Decode(args[1], false);

                            if (script == null)
                            {
                                answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid script data" });
                            }
                            else
                            {
                                byte[] payload = args[2].Length > 0 ? Base16.Decode(args[2], false) : null;

                                string platform;
                                SignatureKind signatureKind;

                                if (connection.Version >= 2) {
                                    if (!Enum.TryParse<SignatureKind>(args[3], out signatureKind))
                                    {
                                        answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid signature: " + args[3] });
                                        callback(id, answer, false);
                                        _isPendingRequest = false;
                                        return;
                                    }

                                    platform = args[4].ToLower();
                                }
                                else {
                                    platform = "phantasma";
                                    signatureKind = SignatureKind.Ed25519;
                                }

                                SignTransaction(platform, signatureKind, chain, script, payload, id, (hash, txError) => {
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
                            }

                            return;
                        }
                        else
                        {
                            answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid amount of arguments: {args.Length}" });
                        }
                        break;
                    }

                case "invokeScript":
                    {
                        if (args.Length == 2)
                        {
                            var chain = args[0];
                            var script = Base16.Decode(args[1], false);

                            if (script == null)
                            {
                                answer = APIUtils.FromAPIResult(new Error() { message = $"signTx: Invalid script data" });
                            }
                            else
                            {
                                InvokeScript(chain, script, id, (invokeResult, invokeError) =>
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
                        }
                        else
                        {
                            answer = APIUtils.FromAPIResult(new Error() { message = $"invokeScript: Invalid amount of arguments: {args.Length}"});
                        }

                        break;
                    }

                case "writeArchive":
                    {
                        if (args.Length == 3)
                        {
                            var archiveHash = Hash.Parse(args[0]);
                            var blockIndex = int.Parse(args[1]);
                            var bytes = Base16.Decode(args[2], false);

                            if (bytes == null)
                            {
                                answer = APIUtils.FromAPIResult(new Error() { message = $"invokeScript: Invalid archive data"});
                            }
                            else
                            {
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
                            }

                            return;
                        }
                        else
                        {
                            answer = APIUtils.FromAPIResult(new Error() { message = $"writeArchive: Invalid amount of arguments: {args.Length}" });
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
            Throw.If(!_connections.ContainsKey(dapp), "unknown dapp");

            var connection = _connections[dapp];
            Throw.If(connection.Token != token, "invalid token");

            _connections.Remove(dapp);
        }
    }
}
