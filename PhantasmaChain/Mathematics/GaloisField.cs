using Phantasma.Utils;
using System.Runtime.CompilerServices;

/**
 * 8-bit Galois Field
 *
 * This class implements multiplication, division, addition, subtraction, and exponentiation.
 *
 */

namespace Phantasma.Mathematics
{

    public class GaloisField
    {
        // The number of elements in the field.
        public const int FIELD_SIZE = 256;

        /**
         * The polynomial used to generate the logarithm table.
         *
         * There are a number of polynomials that work to generate a Galois field of 256 elements.  
         * The choice is arbitrary, and we just use the first one.
         *
         * The possibilities are: 29, 43, 45, 77, 95, 99, 101, 105, 113, 135, 141, 169, 195, 207, 231, and 245.
         */
        public const int POLYNOMIAL = 29;

        /**
         * Mapping from members of the Galois Field to their integer logarithms.  
         * The entry for 0 is meaningless because there is no log of 0.
         */

        public static readonly byte[] LOG_TABLE = GenerateLogTable(POLYNOMIAL);
        /**
         * Inverse of the logarithm table.  Maps integer logarithms to members of the field.  
         * There is no entry for 255 because the highest log is 254.
         */

        public static readonly byte[] EXP_TABLE = GenerateExpTable(LOG_TABLE);

        /**
         * A multiplication table for the Galois field.
         *
         * Using this table is an alternative to using the multiply() method,
         * which uses log/exp table lookups.
         */
        public readonly static byte[,] MULTIPLICATION_TABLE = GenerateMultiplicationTable();

        /**
         * Adds two elements of the field.  
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Add(byte a, byte b)
        {
            return (byte)(a ^ b);
        }

        /**
         * Inverse of addition. 
         */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Subtract(byte a, byte b)
        {
            return (byte)(a ^ b);
        }

        /**
         * Multiplies two elements of the field.
         */
        public static byte Multiply(byte a, byte b)
        {
            if (a == 0 || b == 0)
            {
                return 0;
            }

            int logA = LOG_TABLE[a];
            int logB = LOG_TABLE[b];
            int logResult = logA + logB;
            return EXP_TABLE[logResult];
        }

        /**
         * Inverse of multiplication.
         */
        public static byte Divide(byte a, byte b)
        {
            if (a == 0)
            {
                return 0;
            }

            Throw.If(b == 0, "Division by zero");

            int logA = LOG_TABLE[a];
            int logB = LOG_TABLE[b];
            int logResult = logA - logB;

            if (logResult < 0)
            {
                logResult += 255;
            }

            return EXP_TABLE[logResult];
        }

        /**
         * Computes a**n.
         *
         * The result will be the same as multiplying a times itself n times.
         *
         * @param a A member of the field.
         * @param n A plain-old integer.
         * @return The result of multiplying a by itself n times.
         */
        public static byte Exp(byte a, int n)
        {
            if (n == 0)
            {
                return 1;
            }

            if (a == 0)
            {
                return 0;
            }
            
            int logA = LOG_TABLE[a];
            int logResult = logA * n;
            while (255 <= logResult)
            {
                logResult -= 255;
            }

            return EXP_TABLE[logResult];
        }

        /**
         * Generates a logarithm table given a starting polynomial.
         */
        internal static byte[] GenerateLogTable(int polynomial)
        {
            byte[] result = new byte[FIELD_SIZE];

            int b = 1;
            for (int log = 0; log < FIELD_SIZE - 1; log++)
            {
                result[b] = (byte)log;
                b = (b << 1);

                if (FIELD_SIZE <= b)
                {
                    b = ((b - FIELD_SIZE) ^ polynomial);
                }
            }

            return result;
        }

        /**
         * Generates the inverse log table.
         */
        internal static byte[] GenerateExpTable(byte[] logTable)
        {
            byte[] result = new byte[FIELD_SIZE * 2 - 2];
            for (int i = 1; i < FIELD_SIZE; i++)
            {
                int log = logTable[i];
                result[log] = (byte)i;
                result[log + FIELD_SIZE - 1] = (byte)i;
            }
            return result;
        }

        /**
         * Generates a multiplication table as an array of byte arrays.
         *
         * To get the result of multiplying a and b:
         *
         *     MULTIPLICATION_TABLE[a][b]
         */
        internal static byte[,] GenerateMultiplicationTable()
        {
            byte[,] result = new byte[256, 256];
            for (int a = 0; a < FIELD_SIZE; a++)
            {
                for (int b = 0; b < FIELD_SIZE; b++)
                {
                    result[a, b] = Multiply((byte)a, (byte)b);
                }
            }
            return result;
        }
    }

}