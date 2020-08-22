using System;
using System.Text;
using Phantasma.Ethereum.Hex.HexConvertors.Extensions;

namespace Phantasma.Ethereum.RLP
{
    public class RLPStringFormatter
    {
        public static string Format(IRLPElement element)
        {
            var output = new StringBuilder();
            if (element == null)
                throw new Exception("RLPElement object can't be null");
            if (element is RLPCollection rlpCollection)
            {
                output.Append("[");
                foreach (var innerElement in rlpCollection)
                    Format(innerElement);
                output.Append("]");
            }
            else
            {
                output.Append(element.RLPData.ToHex() + ", ");
            }
            return output.ToString();
        }
    }
}
