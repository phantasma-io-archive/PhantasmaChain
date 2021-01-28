using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;
using System.Numerics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Neo.Core
{
    public class NEP5
    {
        public readonly UInt160 ScriptHash;
        public readonly NeoAPI api;

        public NEP5(NeoAPI api, string contractHash) : this(api, NeoAPI.GetScriptHashFromString(contractHash))
        {

        }

        public NEP5(NeoAPI api, byte[] contractHash) : this(api, new UInt160(contractHash))
        {

        }

        public NEP5(NeoAPI api, UInt160 contractHash)
        {
            this.api = api;
            this.ScriptHash = contractHash;
        }

        public NEP5(NeoAPI api, string contractHash, string name, BigInteger decimals)
            : this(api, contractHash)
        {
            this._decimals = decimals;
            this._name = name;
        }

        private string _name = null;
        public string Name
        {
            get
            {
                InvokeResult response = null;
                try
                {
                    if (_name == null)
                    {
                        response = api.InvokeScript(ScriptHash, "name", new object[] { "" });
                        _name = response.result.GetString();
                    }

                    return _name;
                }
                catch (Exception e)
                {
                    throw new NeoException("Api did not return a value.", e);
                }

            }
        }

        private string _symbol = null;
        public string Symbol
        {
            get
            {
                InvokeResult response = null;
                try
                {
                    if (_symbol == null)
                    {
                        response = api.InvokeScript(ScriptHash, "symbol", new object[] { "" });
                        _symbol = response.result.GetString();
                    }

                    return _symbol;
                }
                catch (Exception e)
                {
                    throw new NeoException("Api did not return a value.", e);
                }
            }
        }


        private BigInteger _decimals = -1;
        public BigInteger Decimals
        {
            get
            {
                InvokeResult response = null;
                try
                {
                    if (_decimals < 0)
                    {
                        response = api.InvokeScript(ScriptHash, "decimals", new object[] { "" });
                        _decimals = response.result.GetBigInteger();
                    }

                    return _decimals;
                }
                catch (Exception e)
                {
                    throw new NeoException("Api did not return a value.", e);
                }

            }
        }

        public BigInteger TotalSupply
        {
            get
            {
                InvokeResult response = null;
                try
                {
                    response = api.InvokeScript(ScriptHash, "totalSupply", new object[] { });
                    var totalSupply = response.result.GetBigInteger();

                    var decs = Decimals;
                    while (decs > 0)
                    {
                        totalSupply /= 10;
                        decs--;
                    }

                    return totalSupply;

                }
                catch (Exception e)
                {
                    throw new NeoException("Api did not return a value.", e);
                }

            }
        }

        // FIXME - I'm almost sure that this code won't return non-integer balances correctly...
        public static decimal ConvertToDecimal(BigInteger value, BigInteger decimals)
        {
            if (value == 0)
            {
                return 0;
            }

            var decs = decimals;
            while (decs > 0)
            {
                value /= 10;
                decs--;
            }
            return (decimal)value;
        }

        private BigInteger ConvertToBigInt(decimal value)
        {
            var decs = this.Decimals;
            while (decs > 0)
            {
                value *= 10;
                decs--;
            }
            return new BigInteger((ulong)value);
        }

        public decimal BalanceOf(string address)
        {
            return BalanceOf(address.GetScriptHashFromAddress());
        }

        public decimal BalanceOf(NeoKeys keys)
        {
            return BalanceOf(keys.Address);
        }

        public decimal BalanceOf(UInt160 hash)
        {
            return BalanceOf(hash.ToArray());
        }

        public decimal BalanceOf(byte[] addressHash)
        {
            InvokeResult response = new InvokeResult();
            try
            {
                response = api.InvokeScript(ScriptHash, "balanceOf", new object[] { addressHash });
                var balance = response.result.GetBigInteger();
                return ConvertToDecimal(balance, this.Decimals);
            }
            catch
            {
                throw new NeoException("Api did not return a value." + response);
            }
        }

        public Transaction Transfer(NeoKeys from_key, string to_address, decimal value, byte[] nonce = null, Action<string> usedRpc = null)
        {
            return Transfer(from_key, to_address.GetScriptHashFromAddress(), value, nonce, usedRpc);
        }

        public Transaction Transfer(NeoKeys from_key, UInt160 to_address_hash, decimal value, byte[] nonce = null, Action<string> usedRpc = null)
        {
            return Transfer(from_key, to_address_hash.ToArray(), value, nonce, usedRpc);
        }

        public Transaction Transfer(NeoKeys from_key, byte[] to_address_hash, decimal value, byte[] nonce = null, Action<string> usedRpc = null)
        {
            BigInteger amount = ConvertToBigInt(value);

            var sender_address_hash = from_key.Address.GetScriptHashFromAddress();
            var response = api.CallContract(from_key, ScriptHash, "transfer", new object[] { sender_address_hash
                    , to_address_hash, amount }, null, null, nonce, usedRpc);
            return response;
        }

        // transfer to multiple addresses
        public Transaction Transfer(NeoKeys from_key, Dictionary<string, decimal> transfers, Action<string> usedRpc = null)
        {
            var temp = new Dictionary<byte[], decimal>(new ByteArrayComparer());
            foreach (var entry in transfers)
            {
                if (!entry.Key.IsValidAddress())
                {
                    throw new ArgumentException($"{entry.Key} is not a valid address");
                }

                var hash = entry.Key.AddressToScriptHash();
                temp[hash] = entry.Value;
            }

            return Transfer(from_key, temp, usedRpc);
        }

        public const int max_transfer_count = 3;

        // transfer to multiple addresses
        public Transaction Transfer(NeoKeys from_key, Dictionary<byte[], decimal> transfers, Action<string> usedRpc = null)
        {
            if (transfers.Count > max_transfer_count)
            {
                throw new ArgumentException("Max transfers per call = " + max_transfer_count);
            }

            var scripts = new List<byte[]>();
            
            var sender_address_hash = from_key.Address.GetScriptHashFromAddress();

            int index = 0;
            foreach (var entry in transfers)
            {
                if (entry.Value <= 0)
                {
                    var addr = new UInt160(entry.Key).ToAddress();
                    throw new ArgumentException($"Invalid amount {entry.Value} for address {addr}");
                }

                BigInteger amount = ConvertToBigInt(entry.Value);

                var isLast = index == transfers.Count - 1;
                var args = new object[] { sender_address_hash, entry.Key, amount };
                var bytes = NeoAPI.GenerateScript(ScriptHash, new object[] { "transfer", args }, isLast ?  null : new byte[0]);

                scripts.Add(bytes);
                index++;
            }

            var final_size = scripts.Sum(x => x.Length);
            byte[] final_script = new byte[final_size];

            using (var stream = new MemoryStream(final_script))
            {
                foreach (byte[] bytes in scripts)
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
            
            var response = api.CallContract(from_key, ScriptHash, final_script, null, null, usedRpc);
            return response;
        }

        // optional methods, not all NEP5 support this!
        public decimal Allowance(string from_address, string to_address)
        {
            return Allowance(from_address.GetScriptHashFromAddress(), to_address.GetScriptHashFromAddress());

        }

        public decimal Allowance(byte[] from_address_hash, byte[] to_address_hash)
        {
            var response = api.InvokeScript(ScriptHash, "allowance", new object[] { from_address_hash, to_address_hash });

            try
            {
                return ConvertToDecimal(response.result.GetBigInteger(), this.Decimals);
            }
            catch (Exception e)
            {
                throw new NeoException("Api did not return a value.", e);
            }
        }

        public Transaction TransferFrom(byte[] originator, byte[] from, byte[] to, BigInteger amount)
        {
            throw new System.NotImplementedException();
        }

        public Transaction Approve(byte[] originator, byte[] to, BigInteger amount)
        {
            throw new System.NotImplementedException();
        }
    }

    public static class TokenSale
    {
        public static Transaction Deploy(NEP5 token, NeoKeys owner_key)
        {
            var response = token.api.CallContract(owner_key, token.ScriptHash, "deploy", new object[] { });
            return response;
        }

        public static Transaction MintTokens(NEP5 token, NeoKeys buyer_key, string symbol, decimal amount)
        {
            var attachs = new List<Transaction.Output>();
            attachs.Add(new Transaction.Output() { assetID = NeoAPI.GetAssetID(symbol), scriptHash = token.ScriptHash, value = amount });
            var response = token.api.CallContract(buyer_key, token.ScriptHash, "mintTokens", new object[] { }, symbol, attachs);
            return response;
        }
    }
}
