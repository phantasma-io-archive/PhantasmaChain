using Phantasma.Core;
using Phantasma.Numerics;

/**
 * Reed-Solomon Coding over 8-bit values.
 * Code based in the Java implementation of Reed-Solomon by Backblaze:
 * https://www.backblaze.com/open-source-reed-solomon.html
 */

namespace Phantasma.Storage.Sharding
{
    public class ReedSolomon
    {
        private int dataShardCount;
        private int parityShardCount;
        private int totalShardCount;
        private Matrix matrix;

        /**
         * Rows from the matrix for encoding parity, each one as its own
         * byte array to allow for efficient access while encoding.
         */
        private byte[][] parityRows;

        public ReedSolomon(int dataShardCount, int parityShardCount)
        {

            // We can have at most 256 shards total, as any more would lead to duplicate rows in the Vandermonde matrix, 
            // which would then lead to duplicate rows in the built matrix below. 
            // Then any subset of the rows containing the duplicate rows would be singular.
            Throw.If(256 < dataShardCount + parityShardCount, "too many shards - max is 256");

            this.dataShardCount = dataShardCount;
            this.parityShardCount = parityShardCount;
            this.totalShardCount = dataShardCount + parityShardCount;

            matrix = buildMatrix(dataShardCount, this.totalShardCount);
            parityRows = new byte[parityShardCount][];
            for (int i = 0; i < parityShardCount; i++)
            {
                parityRows[i] = matrix.GetRow(dataShardCount + i);
            }
        }

        // Returns the number of data shards.
        public int getDataShardCount()
        {
            return dataShardCount;
        }

        // Returns the number of parity shards.
        public int getParityShardCount()
        {
            return parityShardCount;
        }

        // Returns the total number of shards.
        public int getTotalShardCount()
        {
            return totalShardCount;
        }

        /**
         * Encodes parity for a set of data shards.
         *
         * @param shards An array containing data shards followed by parity shards.
         *               Each shard is a byte array, and they must all be the same
         *               size.
         * @param offset The index of the first byte in each shard to encode.
         * @param byteCount The number of bytes to encode in each shard.
         *
         */
        public void encodeParity(Shard[] shards, int offset, int byteCount)
        {
            // Check arguments.
            CheckBuffersAndSizes(shards, offset, byteCount);

            // Build the array of output buffers.
            Shard[] outputs = new Shard[parityShardCount];
            for (int i = 0; i < parityShardCount; i++)
            {
                outputs[i] = shards[dataShardCount + i];
            }

            // Do the coding.
            CodeShards(parityRows, shards, dataShardCount, outputs, parityShardCount, offset, byteCount);
        }

        /**
         * Returns true if the parity shards contain the right data.
         *
         * @param shards An array containing data shards followed by parity shards.
         *               Each shard is a byte array, and they must all be the same
         *               size.
         * @param firstByte The index of the first byte in each shard to check.
         * @param byteCount The number of bytes to check in each shard.
         */
        public bool isParityCorrect(Shard[] shards, int firstByte, int byteCount)
        {
            // Check arguments.
            CheckBuffersAndSizes(shards, firstByte, byteCount);

            // Build the array of buffers being checked.
            Shard[] toCheck = new Shard[parityShardCount];
            for (int i = 0; i < parityShardCount; i++)
            {
                toCheck[i] = shards[dataShardCount + i];
            }

            // Do the checking.
            return CheckSomeShards(parityRows, shards, dataShardCount, toCheck, parityShardCount, firstByte, byteCount, null);
        }

        /**
         * Returns true if the parity shards contain the right data.
         *
         * This method may be significantly faster than the one above that does
         * not use a temporary buffer.
         *
         * @param shards An array containing data shards followed by parity shards.
         *               Each shard is a byte array, and they must all be the same
         *               size.
         * @param firstByte The index of the first byte in each shard to check.
         * @param byteCount The number of bytes to check in each shard.
         * @param tempBuffer A temporary buffer (the same size as each of the
         *                   shards) to use when computing parity.
         */
        public bool IsParityCorrect(Shard[] shards, int firstByte, int byteCount, byte[] tempBuffer)
        {
            // Check arguments.
            CheckBuffersAndSizes(shards, firstByte, byteCount);
            Throw.If(tempBuffer.Length < firstByte + byteCount, "tempBuffer is not big enough");

            // Build the array of buffers being checked.
            Shard[] toCheck = new Shard[parityShardCount];
            for (int i = 0; i < parityShardCount; i++)
            {
                toCheck[i] = shards[dataShardCount + i];
            }

            // Do the checking.
            return CheckSomeShards(parityRows, shards, dataShardCount, toCheck, parityShardCount, firstByte, byteCount, tempBuffer);
        }

        /**
         * Given a list of shards, some of which contain data, fills in the
         * ones that don't have data.
         *
         * Quickly does nothing if all of the shards are present.
         *
         * If any shards are missing (based on the flags in shardsPresent),
         * the data in those shards is recomputed and filled in.
         */
        public void decodeMissing(Shard[] shards,
                                  bool[] shardPresent,
                                  int offset,
                                  int byteCount)
        {
            // Check arguments.
            CheckBuffersAndSizes(shards, offset, byteCount);

            // Quick check: are all of the shards present?  If so, there's
            // nothing to do.
            int numberPresent = 0;
            for (int i = 0; i < totalShardCount; i++)
            {
                if (shardPresent[i])
                {
                    numberPresent += 1;
                }
            }
            if (numberPresent == totalShardCount)
            {
                // Cool.  All of the shards data data.  We don't
                // need to do anything.
                return;
            }

            // More complete sanity check
            Throw.If(numberPresent < dataShardCount, "Not enough shards present");

            // Pull out the rows of the matrix that correspond to the shards that we have and build a square matrix.  
            // This matrix could be used to generate the shards that we have from the original data.
            //
            // Also, pull out an array holding just the shards that correspond to the rows of the submatrix.  
            // These shards will be the input to the decoding process that re-creates the missing data shards.
            Matrix subMatrix = new Matrix(dataShardCount, dataShardCount);
            Shard[] subShards = new Shard[dataShardCount];

            int subMatrixRow = 0;
            for (int matrixRow = 0; matrixRow < totalShardCount && subMatrixRow < dataShardCount; matrixRow++)
            {
                if (shardPresent[matrixRow])
                {
                    for (int c = 0; c < dataShardCount; c++)
                    {
                        subMatrix.Set(subMatrixRow, c, matrix.Get(matrixRow, c));
                    }
                    subShards[subMatrixRow] = shards[matrixRow];
                    subMatrixRow += 1;
                }
            }

            // Invert the matrix, so we can go from the encoded shards
            // back to the original data.  Then pull out the row that
            // generates the shard that we want to decode.  Note that
            // since this matrix maps back to the orginal data, it can
            // be used to create a data shard, but not a parity shard.
            Matrix dataDecodeMatrix = subMatrix.Invert();

            // Re-create any data shards that were missing.
            //
            // The input to the coding is all of the shards we actually
            // have, and the output is the missing data shards.  The computation
            // is done using the special decode matrix we just built.
            Shard[] outputs = new Shard[parityShardCount];
            byte[][] matrixRows = new byte[parityShardCount][];
            int outputCount = 0;
            for (int iShard = 0; iShard < dataShardCount; iShard++)
            {
                if (!shardPresent[iShard])
                {
                    outputs[outputCount] = shards[iShard];
                    matrixRows[outputCount] = dataDecodeMatrix.GetRow(iShard);
                    outputCount += 1;
                }
            }

            CodeShards(matrixRows, subShards, dataShardCount, outputs, outputCount, offset, byteCount);

            // Now that we have all of the data shards intact, we can
            // compute any of the parity that is missing.
            //
            // The input to the coding is ALL of the data shards, including
            // any that we just calculated.  The output is whichever of the
            // data shards were missing.
            outputCount = 0;
            for (int iShard = dataShardCount; iShard < totalShardCount; iShard++)
            {
                if (!shardPresent[iShard])
                {
                    outputs[outputCount] = shards[iShard];
                    matrixRows[outputCount] = parityRows[iShard - dataShardCount];
                    outputCount += 1;
                }
            }

            CodeShards(matrixRows, shards, dataShardCount, outputs, outputCount, offset, byteCount);
        }

        // Checks the consistency of arguments passed to public methods.
        private void CheckBuffersAndSizes(Shard[] shards, int offset, int byteCount)
        {
            // The number of buffers should be equal to the number of
            // data shards plus the number of parity shards.
            Throw.If(shards.Length != totalShardCount, $"wrong number of shards: {shards.Length}");

            // All of the shard buffers should be the same length.
            int shardLength = shards[0].Bytes.Length;
            for (int i = 1; i < shards.Length; i++)
            {
                Throw.If(shards[i].Bytes.Length != shardLength, "Shards are different sizes");
            }

            // The offset and byteCount must be non-negative and fit in the buffers.
            Throw.If(offset < 0, "offset is negative: " + offset);
            Throw.If(byteCount < 0, "byteCount is negative: " + byteCount);
            Throw.If(shardLength < offset + byteCount, "buffers to small: " + byteCount + offset);
        }

        /**
         * Create the matrix to use for encoding, given the number of data shards and the number of total shards.
         *
         * The top square of the matrix is guaranteed to be an identity matrix, which means that the data shards are unchanged after encoding.
         */
        private static Matrix buildMatrix(int dataShards, int totalShards)
        {
            // Start with a Vandermonde matrix.  
            // This matrix would work, in theory, but doesn't have the property that the data shards are unchanged after encoding.
            Matrix vandermonde = CreateVandermondeMatrix(totalShards, dataShards);

            // Multiple by the inverse of the top square of the matrix.
            // This will make the top square be the identity matrix, but
            // preserve the property that any square subset of rows is
            // invertible.
            Matrix top = vandermonde.SubMatrix(0, 0, dataShards, dataShards);
            return vandermonde.Multiply(top.Invert());
        }

        /// <summary>
        /// Create a Vandermonde matrix, which is guaranteed to have the property that any subset of rows that forms a square matrix is invertible.
        /// </summary>
        private static Matrix CreateVandermondeMatrix(int rows, int cols)
        {
            Matrix result = new Matrix(rows, cols);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    result.Set(r, c, GaloisField.Exp((byte)r, c));
                }
            }
            return result;
        }

        /// <summary>
        /// Generates Reed-Solomon erasure codes based on the input shards
        /// </summary>
        public void CodeShards(byte[][] matrixRows, Shard[] inputs, int inputCount, Shard[] outputs, int outputCount, int offset, int byteCount)
        {
            byte[,] table = GaloisField.MULTIPLICATION_TABLE;
            for (int iOutput = 0; iOutput < outputCount; iOutput++)
            {
                byte[] outputShard = outputs[iOutput].Bytes;
                byte[] matrixRow = matrixRows[iOutput];
                {
                    int iInput = 0;
                    byte[] inputShard = inputs[iInput].Bytes;
                    byte row = (byte)(matrixRow[iInput] & 0xFF);
                    for (int iByte = offset; iByte < offset + byteCount; iByte++)
                    {
                        outputShard[iByte] = table[row, inputShard[iByte] & 0xFF];
                    }
                }
                for (int iInput = 1; iInput < inputCount; iInput++)
                {
                    byte[] inputShard = inputs[iInput].Bytes;
                    byte row = (byte)(matrixRow[iInput] & 0xFF);
                    for (int iByte = offset; iByte < offset + byteCount; iByte++)
                    {
                        outputShard[iByte] ^= table[row, inputShard[iByte] & 0xFF];
                    }
                }
            }
        }

        /// <summary>
        /// Validates the integrity of the shards data
        /// </summary>
        public bool CheckShards(byte[][] matrixRows, Shard[] inputs, int inputCount, Shard[] toCheck, int checkCount, int offset, int byteCount)
        {
            byte[,] table = GaloisField.MULTIPLICATION_TABLE;

            for (int iByte = offset; iByte < offset + byteCount; iByte++)
            {
                for (int iOutput = 0; iOutput < checkCount; iOutput++)
                {
                    byte[] matrixRow = matrixRows[iOutput];
                    uint value = 0;
                    for (int iInput = 0; iInput < inputCount; iInput++)
                    {
                        value ^= table[matrixRow[iInput], inputs[iInput].Bytes[iByte]];
                    }

                    if (toCheck[iOutput].Bytes[iByte] != (byte)value)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // Faster version of the above method, optimized by using a temp buffer
        public bool CheckSomeShards(byte[][] matrixRows, Shard[] inputs, int inputCount, Shard[] toCheck, int checkCount, int offset, int byteCount, byte[] tempBuffer)
        {
            byte[,] table = GaloisField.MULTIPLICATION_TABLE;
            for (int iOutput = 0; iOutput < checkCount; iOutput++)
            {
                byte[] outputShard = toCheck[iOutput].Bytes;
                byte[] matrixRow = matrixRows[iOutput];
                {
                    int iInput = 0;
                    byte[] inputShard = inputs[iInput].Bytes;
                    byte row = matrixRow[iInput];
                    for (int iByte = offset; iByte < offset + byteCount; iByte++)
                    {
                        tempBuffer[iByte] = table[row, inputShard[iByte]];
                    }
                }

                for (int iInput = 1; iInput < inputCount; iInput++)
                {
                    byte[] inputShard = inputs[iInput].Bytes;
                    byte row = matrixRow[iInput];
                    for (int iByte = offset; iByte < offset + byteCount; iByte++)
                    {
                        tempBuffer[iByte] ^= table[row, inputShard[iByte]];
                    }
                }

                for (int iByte = offset; iByte < offset + byteCount; iByte++)
                {
                    if (tempBuffer[iByte] != outputShard[iByte])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

    }

}