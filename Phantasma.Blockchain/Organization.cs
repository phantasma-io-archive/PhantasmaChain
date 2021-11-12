using System.Collections.Generic;
using System.Linq;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain
{
    public class Organization : IOrganization
    {
        public string ID { get; private set; }
        public string Name { get; private set; }

        private Address _address;
        public Address Address
        {
            get
            {
                if (_address.IsNull)
                {
                    _address = Address.FromHash(ID);
                }

                return _address;
            }
        }

        public byte[] Script { get; private set; }

        public BigInteger Size => GetMemberList().Count();

        private StorageContext Storage;

        public bool IsInvalid { get; private set; }

        public Organization(string name, StorageContext storage)
        {
            this.Storage = storage;
 
            this.ID = name;
            this.IsInvalid = false;

            var key = GetKey("script");
            if (Storage.Has(key))
            {
                this.Script = Storage.Get(key);
            }
            else
            {
                this.Script = null;
            }

            key = GetKey("name");
            if (Storage.Has(key))
            {
                this.Name = Storage.Get<string>(key);
            }
            else
            {
                this.Name = null;
            }
        }

        public void Init(string name, byte[] script)
        {
            this.Name = name;
            var key = GetKey("name");
            this.Storage.Put(key, name);

            this.Script = script;
            key = GetKey("script");
            this.Storage.Put(key, script);
        }

        private byte[] GetKey(string key)
        {
            return System.Text.Encoding.UTF8.GetBytes($".org.{ID}.{key}");
        }

        private StorageList GetMemberList()
        {
            var key = GetKey("list");
            return new StorageList(key, this.Storage);
        }

        public Address[] GetMembers()
        {
            var list = GetMemberList();
            return list.All<Address>();
        }

        public bool IsMember(Address address)
        {
            if (IsInvalid)
            {
                return false;
            }

            var list = GetMemberList();
            return list.Contains<Address>(address);
        }

        public bool AddMember(RuntimeVM Runtime, Address from, Address target)
        {
            Runtime.Expect(!IsInvalid, "cannot add member to invalid organization");

            if (from.IsSystem)
            {
                Runtime.Expect(from != this.Address, "can't add organization as member of itself");
            }

            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            var list = GetMemberList();

            if (list.Contains<Address>(target))
            {
                return false;
            }

            list.Add<Address>(target);

            Runtime.Notify(EventKind.OrganizationAdd, from, new OrganizationEventData(this.ID, target));
            return true;
        }

        public bool RemoveMember(RuntimeVM Runtime, Address from, Address target)
        {
            Runtime.Expect(!IsInvalid, "cannot remove member from invalid organization");

            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            var list = GetMemberList();

            if (!list.Contains<Address>(target))
            {
                return false;
            }

            list.Remove<Address>(target);

            Runtime.Notify(EventKind.OrganizationRemove, from, new OrganizationEventData(this.ID, target));
            return true;
        }

        public bool IsWitness(Transaction transaction)
        {
            if (IsInvalid)
            {
                return false;
            }

            var size = this.Size;
            if (size < 1)
            {
                return false;
            }

            var majorityCount = (size / 2) + 1;
            if (transaction == null || transaction.Signatures.Length < majorityCount)
            {
                return false;
            }

            int witnessCount = 0;

            var members = new List<Address>(this.GetMembers());
            var msg = transaction.ToByteArray(false);

            foreach (var sig in transaction.Signatures)
            {
                if (witnessCount >= majorityCount)
                {
                    break; // dont waste time if we already reached a majority
                }

                //ring signature not supported yet here
                if (sig.Kind == SignatureKind.Ring)
                {
                    continue;
                }

                foreach (var addr in members)
                {
                    if (sig.Verify(msg, addr))
                    {
                        witnessCount++;
                        members.Remove(addr);
                        break;
                    }
                }
            }

            return witnessCount >= majorityCount;
        }

        public bool MigrateMember(RuntimeVM Runtime, Address admin, Address from, Address to)
        {
            Runtime.Expect(!IsInvalid, "cannot migrate member of invalid organization");

            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            if (to.IsSystem)
            {
                Runtime.Expect(to!= this.Address, "can't add organization as member of itself");
            }

            var list = GetMemberList();

            if (!list.Contains<Address>(from))
            {
                return false;
            }

            Runtime.Expect(!list.Contains<Address>(to), "target address is already a member of organization");

            list.Remove<Address>(from);
            list.Add<Address>(to);         

            Runtime.Notify(EventKind.OrganizationRemove, admin, new OrganizationEventData(this.ID, from));
            Runtime.Notify(EventKind.OrganizationAdd, admin, new OrganizationEventData(this.ID, to));

            return true;
        }

        private Dictionary<Address, BigInteger> FindTransferHistoryForFungibleToken(List<Event> events, string symbol)
        {
            var result = new Dictionary<Address, BigInteger>();

            foreach (var evt in events)
            {
                Address address = Address.Null;
                BigInteger amount = 0;

                switch (evt.Kind)
                {
                    case EventKind.TokenReceive:
                        {
                            var data = evt.GetContent<TokenEventData>();

                            if (data.Symbol == symbol)
                            {
                                address = evt.Address;
                                amount = -data.Value;
                            }
                            break;
                        }

                    case EventKind.TokenSend:
                        {
                            var data = evt.GetContent<TokenEventData>();

                            if (data.Symbol == symbol)
                            {
                                address = evt.Address;
                                amount = data.Value;
                            }
                            break;
                        }
                }

                if (amount != 0 && address != this.Address)
                {
                    if (!address.IsSystem || IsMember(address))
                    {
                        var balance = result.ContainsKey(address) ? result[address] : 0;
                        balance += amount;

                        result[address] = balance;

                    }
                }
            }

            return result;
        }

        private Dictionary<Address, HashSet<BigInteger>> FindTransferHistoryForNFT(List<Event> events, string symbol)
        {
            var result = new Dictionary<Address, HashSet<BigInteger>>();

            foreach (var evt in events)
            {
                switch (evt.Kind)
                {
                    case EventKind.TokenReceive:
                        {
                            var data = evt.GetContent<TokenEventData>();

                            if (data.Symbol == symbol)
                            {
                                HashSet<BigInteger> list = result.ContainsKey(evt.Address) ? result[evt.Address] : new HashSet<BigInteger>();
                                list.Add(data.Value);
                            }
                            break;
                        }

                    case EventKind.TokenSend:
                        {
                            var data = evt.GetContent<TokenEventData>();

                            if (data.Symbol == symbol)
                            {
                                HashSet<BigInteger> list = result.ContainsKey(evt.Address) ? result[evt.Address] : new HashSet<BigInteger>();
                                list.Remove(data.Value);
                            }
                            break;
                        }
                }
            }

            return result;
        }

        private List<Event> GetEventList(RuntimeVM Runtime, Hash[] hashes)
        {
            var eventList = new List<Event>();

            foreach (var hash in hashes)
            {
                var events = Runtime.GetTransactionEvents(hash).Where(x => x.Kind == EventKind.TokenReceive || x.Kind == EventKind.TokenSend);
                eventList.AddRange(events);
            }

            return eventList;
        }

        public bool Kill(RuntimeVM Runtime, Address from)
        {
            Runtime.Expect(!IsInvalid, "cannot kill invalid organization");

            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            Runtime.Expect(!IsSpecialOrganization(Name) && !Name.Equals(DomainSettings.PhantomForceOrganizationName), "cannot kill organization: " + Name);

            var hashes = Runtime.GetTransactionHashesForAddress(this.Address);
            //var transactions = hashes.Select(hash => Runtime.GetTransaction(hash)).ToArray();
            var events = GetEventList(Runtime, hashes);

            // return all assets to previous owners, if possible
            var symbols = Runtime.GetTokens();
            foreach (var symbol in symbols)
            {
                var balance = Runtime.GetBalance(symbol, this.Address);
                if (balance > 0)
                {
                    var info = Runtime.GetToken(symbol);
                    if (info.IsFungible())
                    {
                        var entries = FindTransferHistoryForFungibleToken(events, symbol);

                        /*foreach (var entry in entries)
                        {
                            var amount = entry.Value;

                            if (amount <= 0)
                            {
                                continue;
                            }

                            var target = entry.Key;
                            balance -= amount;

                            System.Console.WriteLine($"{target},{UnitConversion.ToDecimal(amount, info.Decimals)},{symbol}");
                        }*/

                        foreach (var entry in entries)
                        {
                            var amount = entry.Value;

                            if (amount <= 0)
                            {
                                continue;
                            }

                            var target = entry.Key;
                            balance -= amount;

                            Runtime.Expect(balance >= 0, $"balance underflow while returning {symbol} to organization members during destruction");

                            Runtime.TransferTokens(symbol, this.Address, target, amount);
                        }
                    }
                    else
                    {
                        var entries = FindTransferHistoryForNFT(events, symbol);
                        foreach (var entry in entries)
                        {
                            var target = entry.Key;

                            var tokenIDs = entry.Value;

                            foreach (var tokenID in tokenIDs)
                            {
                                Runtime.TransferToken(symbol, this.Address, target, tokenID);
                                balance--;
                            }
                        }
                    }

                    if (/*symbol != DomainSettings.FuelTokenSymbol && */balance > 0)
                    {
                        if (info.IsFungible())
                        {
                            balance = Runtime.Chain.GetTokenBalance(Runtime.Storage, symbol, this.Address);
                            Runtime.TransferTokens(symbol, this.Address, from, balance);
                        }
                        else
                        {
                            var tokenIDs = Runtime.Chain.GetOwnedTokens(Runtime.Storage, symbol, this.Address);
                            foreach (var tokenID in tokenIDs)
                            {
                                Runtime.TransferToken(symbol, this.Address, from, tokenID);
                            }
                        }
                    }
                }
            }

            // remove all keys from storage
            var key = GetKey("name");
            this.Storage.Delete(key);
            key = GetKey("script");
            this.Storage.Delete(key);

            var list = GetMemberList();
            list.Clear();

            Runtime.Notify(EventKind.OrganizationKill, from, new OrganizationEventData(this.ID, from));

            this.IsInvalid = true;
            return true;
        }

        // returns true if name represents an organization that is required to the chain rules to work 
        public static bool IsSpecialOrganization(string name)
        {
            return (name.Equals(DomainSettings.MastersOrganizationName) || name.Equals(DomainSettings.StakersOrganizationName) || name.Equals(DomainSettings.ValidatorsOrganizationName));
        }
    }
}
