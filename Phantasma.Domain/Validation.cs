using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.VM;

namespace Phantasma.Domain
{
    public enum ValidatorType
    {
        Invalid,
        Proposed,
        Primary,
        Secondary, // aka StandBy
    }

    public struct ValidatorEntry
    {
        public Address address;
        public Timestamp election;
        public ValidatorType type;
    }

    public static class ValidationUtils
    {
        public static readonly string ANONYMOUS = "anonymous";
        public static readonly string GENESIS = "genesis";

        public static string[] prefixNames = new string[] {
            "phantasma", "neo", "ethereum", "bitcoin", "litecoin", "eos",
            "decentraland", "elastos", "loopring", "grin", "nuls",
            "bancor", "ark", "nos", "bluzelle", "satoshi", "gwei", "nacho",
            "oracle", "oracles", "dex", "exchange", "wallet", "account",
            "airdrop", "giveaway", "free", "mail", "dapp", "charity","address", "system",
            "coin", "token", "nexus", "deposit", "phantom", "cityofzion", "coz",
            "huobi", "binance", "kraken", "kucoin", "coinbase", "switcheo", "bittrex","bitstamp",
            "bithumb", "okex", "hotbit", "bitmart", "bilaxy", "vitalik", "nakamoto",
            };

        public static string[] reservedNames = new string[] {
            "ripple", "tether", "tron", "chainchanged", "libra","loom", "enigma", "wax",
            "monero", "dash", "tezos", "cosmos", "maker", "ontology", "dogecoin", "zcash", "vechain",
            "qtum", "omise",  "holo", "nano", "augur", "waves", "icon" , "dai", "bitshares",
            "siacoin", "komodo", "zilliqa", "steem", "enjin", "aelf", "nash", "stratis",
            "windows", "osx", "ios","android", "google", "yahoo", "facebook", "alibaba", "ebay",
            "apple", "amazon", "microsoft", "samsung", "verizon", "walmart", "ibm", "disney",
            "netflix", "alibaba", "tencent", "baidu", "visa", "mastercard", "instagram", "paypal",
            "adobe", "huawei", "vodafone", "dell", "uber", "youtube", "whatsapp", "snapchat", "pinterest",
            "gamecenter", "pixgamecenter", "seal", "crosschain", "blacat",
            "bitladon", "bitcoinmeester" , "ico", "ieo", "sto", "kyc", };


        public static bool IsReservedIdentifier(string name)
        {
            //System.Console.WriteLine("Trying to register: " + name);
            bool isReserved = false;
            for (int i = 0; i < prefixNames.Length; i++)
            {
                if (name.StartsWith(prefixNames[i]))
                {
                    //System.Console.WriteLine("Starts with : " + prefixNames[i]+ " at index " +i);
                    isReserved = true;
                    break;
                }
            }

            for (int i = 0; i < reservedNames.Length; i++)
            {
                if (name == reservedNames[i])
                {
                    //System.Console.WriteLine("Reserved with : " + reservedNames[i]);
                    isReserved = true;
                    break;
                }
            }

            return isReserved;
        }

        public static bool IsValidIdentifier(string name)
        {
            if (name == null)
            {
                return false;
            }

            if (name.Length < 3 || name.Length > 15)
            {
                return false;
            }

            if (name == ANONYMOUS || name == GENESIS || name == VirtualMachine.EntryContextName)
            {
                return false;
            }

            int index = 0;
            while (index < name.Length)
            {
                var c = name[index];
                index++;

                if (c >= 97 && c <= 122) continue; // lowercase allowed
                if (c == 95) continue; // underscore allowed
                if (index > 1 && c >= 48 && c <= 57) continue; // numbers allowed except first char

                return false;
            }

            return true;
        }

        public static bool IsValidTicker(string name)
        {
            if (name == null)
            {
                return false;
            }

            if (name.Length < 2 || name.Length > 5)
            {
                return false;
            }


            int index = 0;
            while (index < name.Length)
            {
                var c = name[index];
                index++;

                if (c >= 65 && c <= 90) continue; // uppercase allowed

                return false;
            }

            return true;
        }
    }
}
