using System.IO;
using System.Text;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct RelayMessage : ISerializable
    {
        public string nexus;
        public string channel;
        public BigInteger index;
        public Timestamp timestamp;
        public Address sender;
        public byte[] script;

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    SerializeData(writer);
                }
                return stream.ToArray();
            }
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(nexus);
            writer.WriteVarString(channel);
            writer.WriteBigInteger(index);
            writer.Write(timestamp.Value);
            writer.WriteAddress(sender);
            writer.WriteByteArray(script);
        }

        public void UnserializeData(BinaryReader reader)
        {
            nexus = reader.ReadVarString();
            channel = reader.ReadVarString();
            index = reader.ReadBigInteger();
            timestamp = new Timestamp(reader.ReadUInt32());
            sender = reader.ReadAddress();
            script = reader.ReadByteArray();
        }
    }

    public struct RelayReceipt : ISerializable
    {
        public RelayMessage message;
        public Signature signature;

        public void SerializeData(BinaryWriter writer)
        {
            message.SerializeData(writer);
            writer.WriteSignature(signature);
        }

        public void UnserializeData(BinaryReader reader)
        {
            message.UnserializeData(reader);
            signature = reader.ReadSignature();
        }

        public static RelayReceipt FromBytes(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var receipt = new RelayReceipt();
                    receipt.UnserializeData(reader);
                    return receipt;
                }
            }
        }

        public static RelayReceipt FromMessage(RelayMessage msg, KeyPair keys)
        {
            var bytes = msg.ToByteArray();
            var signature = keys.Sign(bytes);
            return new RelayReceipt()
            {
                message = msg,
                signature = signature
            };
        }
    }

    public struct RelayChannel
    {
        public Address owner;
        public string chain;
        public Timestamp creationTime;
        public string symbol; // token symbol
        public BigInteger balance;
        public BigInteger fee; // per message
        public bool active;
        public BigInteger index;
    }

    public sealed class RelayContract : SmartContract
    {
        public override string Name => "relay";

        public static readonly int MinimumReceiptsPerTransaction = 20;

        internal StorageMap _channelMap; //<string, ChannelEntry>
        internal StorageMap _channelList; //<Address, List<string>>

        public RelayContract() : base()
        {
        }

        private string MakeKey(Address address, string channelName)
        {
            return address.Text + "." + channelName;
        }

        public RelayChannel GetChannel(Address from, string channelName)
        {
            var key = MakeKey(from, channelName);
            Runtime.Expect(_channelMap.ContainsKey<string>(key), "invalid channel");

            var channel = _channelMap.Get<string, RelayChannel>(key);

            return channel;
        }

        public string[] GetOpenChannels(Address from)
        {
            var list = _channelList.Get<Address, StorageList>(from);
            return list.All<string>();
        }

        public Address GetAddress(Address from, string channelName)
        {
            var key = MakeKey(from, channelName);
            Runtime.Expect(_channelMap.ContainsKey<string>(key), "invalid channel");

            var bytes = Encoding.UTF8.GetBytes(key);
            var hash = CryptoExtensions.SHA256(bytes);
            return new Address(hash);
        }

        public void OpenChannel(Address from, Address to, string chainName, string channelName, string tokenSymbol, BigInteger amount, BigInteger fee)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(from != to, "invalid target");

            Runtime.Expect(amount > 0, "invalid amount");

            Runtime.Expect(fee > 0 && fee<amount, "invalid fee");

            Runtime.Expect(Runtime.Nexus.ChainExists(chainName), "invalid chain");

            Runtime.Expect(Runtime.Nexus.TokenExists(tokenSymbol), "invalid base token");
            var token = Runtime.Nexus.GetTokenInfo(tokenSymbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var key = MakeKey(from, channelName);
            Runtime.Expect(!_channelMap.ContainsKey<string>(key), "channel already open");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, tokenSymbol, from, Runtime.Chain.Address, amount), "insuficient balance");

            var channel = new RelayChannel()
            {
                balance = amount,
                fee = fee,
                owner = to,
                chain = chainName,
                creationTime = Runtime.Time,
                symbol = tokenSymbol,
                active = true,
                index = 0,
            };
            _channelMap.Set<string, RelayChannel>(key, channel);

            var list = _channelList.Get<Address, StorageList>(from);
            list.Add<string>(channelName);

            var address = GetAddress(from, chainName);
            // TODO create auto address 


            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = channel.symbol });
            Runtime.Notify(EventKind.ChannelOpen, from, channelName);
        }

        public void CloseChannel(Address from, string channelName)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var key = MakeKey(from, channelName);
            Runtime.Expect(_channelMap.ContainsKey<string>(key), "invalid channel");

            var channel = _channelMap.Get<string, RelayChannel>(key);
            Runtime.Expect(channel.active, "channel already closed");

            channel.active = false;
            _channelMap.Set<string, RelayChannel>(key, channel);
            Runtime.Notify(EventKind.ChannelClose, from, channelName);
        }

        public void TopUpChannel(Address from, string channelName, BigInteger amount)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var key = MakeKey(from, channelName);
            Runtime.Expect(_channelMap.ContainsKey<string>(key), "invalid channel");

            var channel = _channelMap.Get<string, RelayChannel>(key);
            Runtime.Expect(channel.active, "channel already closed");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, channel.symbol, from, Runtime.Chain.Address, amount), "insuficient balance");

            channel.balance += amount;
            _channelMap.Set<string, RelayChannel>(key, channel);

            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = channel.symbol });
        }

        public void UpdateChannel(Address from, string channelName, RelayReceipt[] receipts)
        {
            var key = MakeKey(from, channelName);
            Runtime.Expect(_channelMap.ContainsKey<string>(key), "invalid channel");

            var channel = _channelMap.Get<string, RelayChannel>(key);

            if (channel.active)
            {
                Runtime.Expect(receipts.Length >= MinimumReceiptsPerTransaction, "more receipts are necessary");
            }

            BigInteger payout = 0;

            for (int i=0; i<receipts.Length; i++)
            {
                var receipt = receipts[i];

                Runtime.Expect(channel.index == receipt.message.index, "invalid receipt index");
                Runtime.Expect(channel.balance >= channel.fee, "insuficient balance");

                var bytes = receipt.message.ToByteArray();
                Runtime.Expect(receipt.signature.Verify(bytes, from), "invalid signature");

                payout += channel.fee;
                channel.balance -= channel.fee;
                channel.index = channel.index + 1;
            }

            Runtime.Expect(payout > 0, "invalid payout");

            if (channel.owner != Runtime.Chain.Address)
            {
                Runtime.Nexus.TransferTokens(Runtime, channel.symbol, Runtime.Chain.Address, channel.owner, payout);
                Runtime.Notify(EventKind.TokenReceive, channel.owner, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = payout, symbol = channel.symbol });
            }

            if (!channel.active || channel.balance == 0)
            {
                if (channel.balance > 0)
                {
                    Runtime.Nexus.TransferTokens(Runtime, channel.symbol, Runtime.Chain.Address, from, channel.balance);
                    Runtime.Notify(EventKind.TokenReceive, from, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = channel.balance, symbol = channel.symbol });
                }

                _channelMap.Remove<string>(key);
                Runtime.Notify(EventKind.ChannelDestroy, from, channelName);

                var list = _channelList.Get<Address, StorageList>(from);
                list.Remove<string>(channelName);
            }
        }
    }
}
