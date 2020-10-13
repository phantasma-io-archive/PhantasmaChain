using System;
using System.Reflection;
using Phantasma.Core;
using Phantasma.VM;
using Phantasma.Storage.Context;
using Phantasma.Storage;
using System.IO;
using Phantasma.Domain;

namespace Phantasma.Blockchain
{
    public static class ContractPatch
    {
        public static readonly uint UnstakePatch = 1578238531;
    }

    public abstract class NativeContract : SmartContract
    {
        public override string Name => Kind.GetName();

        public abstract NativeContractKind Kind { get; }

        public override ExecutionContext CreateContext()
        {
            return new NativeExecutionContext(this);
        }

        // here we auto-initialize any fields from storage
        public void LoadRuntimeData(IRuntime runtime)
        {
            if (this.Runtime != null && this.Runtime != runtime)
            {
                runtime.Throw("runtime already set on this contract");
            }

            this.Runtime = runtime;

            var contractType = this.GetType();
            FieldInfo[] fields = contractType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var baseKey = GetKeyForField(this.Name, field.Name, true);

                var isStorageField = typeof(IStorageCollection).IsAssignableFrom(field.FieldType);
                if (isStorageField)
                {
                    var args = new object[] { baseKey, (StorageContext)runtime.Storage };
                    var obj = Activator.CreateInstance(field.FieldType, args);

                    field.SetValue(this, obj);
                    continue;
                }

                if (typeof(ISerializable).IsAssignableFrom(field.FieldType))
                {
                    ISerializable obj;

                    if (runtime.Storage.Has(baseKey))
                    {
                        var bytes = runtime.Storage.Get(baseKey);
                        obj = (ISerializable)Activator.CreateInstance(field.FieldType);
                        using (var stream = new MemoryStream(bytes))
                        {
                            using (var reader = new BinaryReader(stream))
                            {
                                obj.UnserializeData(reader);
                            }
                        }

                        field.SetValue(this, obj);
                        continue;
                    }
                }

                if (runtime.Storage.Has(baseKey))
                {
                    var obj = runtime.Storage.Get(baseKey, field.FieldType);
                    field.SetValue(this, obj);
                    continue;
                }
            }
        }

        // here we persist any modifed fields back to storage
        public void UnloadRuntimeData()
        {
            Throw.IfNull(this.Runtime, nameof(Runtime));

            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            var contractType = this.GetType();
            FieldInfo[] fields = contractType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var baseKey = GetKeyForField(this.Name, field.Name, true);

                var isStorageField = typeof(IStorageCollection).IsAssignableFrom(field.FieldType);
                if (isStorageField)
                {
                    continue;
                }

                if (typeof(ISerializable).IsAssignableFrom(field.FieldType))
                {
                    var obj = (ISerializable)field.GetValue(this);
                    var bytes = obj.Serialize();
                    this.Runtime.Storage.Put(baseKey, bytes);
                }
                else
                {
                    var obj = field.GetValue(this);
                    var bytes = Serialization.Serialize(obj);
                    this.Runtime.Storage.Put(baseKey, bytes);
                }
            }
        }

    }
}
