using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class SaleContract : SmartContract
    {
        public override string Name => "sale";

        public SaleContract() : base()
        {
        }
    }
}
