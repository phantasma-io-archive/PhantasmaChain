using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Phantasma.Ethereum.Hex.HexConvertors.Extensions;
using Phantasma.Ethereum.Model;

namespace Phantasma.Ethereum.Signer
{
    public class CliqueBlockHeaderRecovery
    {
        public string RecoverCliqueSigner(BlockHeader blockHeader)
        {
            var blockEncoded = BlockHeaderEncoder.Current.EncodeCliqueSigHeader(blockHeader);
            var signature = blockHeader.ExtraData.Skip(blockHeader.ExtraData.Length - 65).ToArray();
            return
                new MessageSigner().EcRecover(BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(blockHeader),
                    signature.ToHex());
        }
    }
}
