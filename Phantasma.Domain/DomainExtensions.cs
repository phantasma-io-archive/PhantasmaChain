using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.VM;
using System;

namespace Phantasma.Domain
{
    public static class DomainExtensions
    {
        public static bool IsFungible(this IToken token)
        {
            return token.Flags.HasFlag(TokenFlags.Fungible);
        }

        public static bool IsBurnable(this IToken token)
        {
            return token.Flags.HasFlag(TokenFlags.Burnable);
        }

        public static bool IsTransferable(this IToken token)
        {
            return token.Flags.HasFlag(TokenFlags.Transferable);
        }

        public static bool IsCapped(this IToken token)
        {
            return token.MaxSupply > 0;
        }

        public static T GetKind<T>(this IEvent evt)
        {
            return (T)(object)evt.Kind;
        }

        public static T GetContent<T>(this IEvent evt)
        {
            return Serialization.Unserialize<T>(evt.Data);
        }

        public static T DecodeCustomEvent<T>(this EventKind kind)
        {
            if (kind < EventKind.Custom)
            {
                throw new Exception("Cannot cast system event");
            }

            var type = typeof(T);
            if (!type.IsEnum)
            {
                throw new Exception("Can only cast event to other enum");
            }

            var intVal = ((int)kind - (int)EventKind.Custom);
            var temp = (T)Enum.Parse(type, intVal.ToString());
            return temp;
        }

        public static EventKind EncodeCustomEvent(Enum kind)
        {
            var temp = (EventKind)((int)Convert.ChangeType(kind, kind.GetTypeCode()) + (int)EventKind.Custom);
            return temp;
        }

        public static IBlock GetLastBlock(this IRuntime runtime)
        {
            if (runtime.Chain.Height < 1)
            {
                return null;
            }

            return runtime.GetBlockByHeight(runtime.Chain.Height);
        }

        public static VMObject CallContext(this IRuntime runtime, NativeContractKind nativeContract, string methodName, params object[] args)
        {
            return runtime.CallContext(nativeContract.GetName(), methodName, args);
        }

        public static IContract GetContract(this IRuntime runtime, NativeContractKind nativeContract)
        {
            return runtime.GetContract(nativeContract.GetName());
        }

        public static Address GetContractAddress(this IRuntime runtime, string contractName)
        {
            return Address.FromHash(contractName);
        }

        public static Address GetContractAddress(this IRuntime runtime, NativeContractKind nativeContract)
        {
            return Address.FromHash(nativeContract.GetName());
        }

        public static string GetName(this NativeContractKind nativeContract)
        {
            return nativeContract.ToString().ToLower();
        }

        public static IChain GetRootChain(this IRuntime runtime)
        {
            return runtime.GetChainByAddress(runtime.Nexus.RootChainAddress);
        }

        public static void Notify<T>(this IRuntime runtime, Enum kind, Address address, T content)
        {
            var intVal = (int)(object)kind;
            runtime.Notify<T>((EventKind)(EventKind.Custom + intVal), address, content);
        }

        public static void Notify<T>(this IRuntime runtime, EventKind kind, Address address, T content)
        {
            var bytes = content == null ? new byte[0] : Serialization.Serialize(content);
            runtime.Notify(kind, address, bytes);
        }

        public static bool IsReadOnlyMode(this IRuntime runtime)
        {
            return runtime.Transaction == null;
        }

        public static bool IsRootChain(this IRuntime runtime)
        {
            return runtime.Chain.Address == runtime.Nexus.RootChainAddress;
        }

        public static InteropBlock ReadBlockFromOracle(this IRuntime runtime, string platform, string chain, Hash hash)
        {
            var bytes = runtime.ReadOracle($"interop://{platform}/{chain}/block/{hash}");
            var block = Serialization.Unserialize<InteropBlock>(bytes);
            return block;
        }

        public static InteropTransaction ReadTransactionFromOracle(this IRuntime runtime, string platform, string chain, Hash hash)
        {
            var bytes = runtime.ReadOracle($"interop://{platform}/{chain}/tx/{hash}");
            var tx = Serialization.Unserialize<InteropTransaction>(bytes);
            return tx;
        }

        public static BigInteger GetBlockCount(this IArchive archive)
        {
            return archive.Size / DomainSettings.ArchiveBlockSize;
        }

        #region TRIGGERS
        public static bool InvokeTriggerOnAccount(this IRuntime runtime, Address address, AccountTrigger trigger, params object[] args)
        {
            if (address.IsNull)
            {
                return false;
            }

            if (address.IsUser)
            {
                var accountScript = runtime.GetAddressScript(address);
                return runtime.InvokeTrigger(accountScript, trigger.ToString(), args);
            }

            return true;
        }

        public static bool InvokeTriggerOnToken(this IRuntime runtime, IToken token, TokenTrigger trigger, params object[] args)
        {
            return runtime.InvokeTrigger(token.Script, trigger.ToString(), args);
        }

        #endregion
    }
}
