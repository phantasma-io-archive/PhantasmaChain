using System;
using System.Text;
using Phantasma.Core;
using Phantasma.Numerics;

// A matrix over the 8-bit Galois field.
// This class is not performance-critical, so the implementation is simple and straightforward

namespace Phantasma.Storage.Sharding
{
    public class Matrix
    {

        // The number of rows in the matrix.
        public int Rows { get; private set; }

        // The number of columns in the matrix.
        public int Columns { get; private set; }

        // The data in the matrix, in row major form.
        private readonly byte[,] _data;

        /// <summary>
        /// Initialize a matrix of zeros. 
        /// </summary>
        /// <param name="initRows">The number of rows in the matrix.</param>
        /// <param name="initColumns">The number of columns in the matrix.</param>
        public Matrix(int initRows, int initColumns)
        {
            this.Rows = initRows;
            this.Columns = initColumns;
            _data = new byte[Rows, Columns];
        }

        /// <summary>
        /// Initializes a matrix with the given row-major data. 
        /// </summary>
        public Matrix(byte[,] initData, int rows, int columns)
        {
            this.Rows = rows;
            this.Columns = columns;
            _data = new byte[rows, columns];
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    _data[r, c] = initData[r, c];
                }
            }
        }

        /// <summary>
        /// Returns an identity matrix of the given size. 
        /// </summary>
        public static Matrix Identity(int size)
        {
            Matrix result = new Matrix(size, size);
            for (int i = 0; i < size; i++)
            {
                result.Set(i, i, (byte)1);
            }
            return result;
        }

        // Output example: [[1, 2], [3, 4]]
        public override String ToString()
        {
            var result = new StringBuilder();
            result.Append('[');
            for (int r = 0; r < Rows; r++)
            {
                if (r != 0)
                {
                    result.Append(", ");
                }
                result.Append('[');
                for (int c = 0; c < Columns; c++)
                {
                    if (c != 0)
                    {
                        result.Append(", ");
                    }
                    result.Append(_data[r, c] & 0xFF);
                }
                result.Append(']');
            }
            result.Append(']');
            return result.ToString();
        }

        public byte Get(int row, int column)
        {
            Throw.If(row < 0 || Rows <= row, $"Row index out of range: {row}");
            Throw.If(column < 0 || Columns <= column, $"Column index out of range: {column}");

            return _data[row, column];
        }

        public void Set(int row, int column, byte value)
        {
            Throw.If(row < 0 || Rows <= row, $"Row index out of range: {row}");
            Throw.If(column < 0 || Columns <= column, $"Column index out of range: {column}");

            _data[row, column] = value;
        }

        public override int GetHashCode()
        {
            return this._data.GetHashCode();
        }

        /// <summary>
        /// Returns true if this matrix is identical to the other.
        /// </summary>
        public override bool Equals(Object other)
        {
            if (!(other is Matrix))
            {
                return false;
            }

            var m = (Matrix)other;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (_data[r, c] != m._data[r, c])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Multiplies this matrix (the one on the left) by another matrix (the one on the right).
        /// </summary>
        public Matrix Multiply(Matrix right)
        {
            Throw.If(this.Columns != right.Rows, $"Columns on left ({this.Columns}) is different than rows on right ({right.Rows})");

            Matrix result = new Matrix(this.Rows, right.Columns);
            for (int r = 0; r < this.Rows; r++)
            {
                for (int c = 0; c < right.Columns; c++)
                {
                    byte value = 0;
                    for (int i = 0; i < this.Columns; i++)
                    {
                        value ^= GaloisField.Multiply(Get(r, i), right.Get(i, c));
                    }
                    result.Set(r, c, value);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the concatenation of this matrix and the matrix on the right.
        /// </summary>
        public Matrix Augment(Matrix right)
        {
            Throw.If(Rows != right.Rows, "Matrices don't have the same number of rows");

            var result = new Matrix(Rows, Columns + right.Columns);

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    result._data[r, c] = _data[r, c];
                }

                for (int c = 0; c < right.Columns; c++)
                {
                    result._data[r, Columns + c] = right._data[r, c];
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a part of this matrix.
        /// </summary>
        public Matrix SubMatrix(int rmin, int cmin, int rmax, int cmax)
        {
            Matrix result = new Matrix(rmax - rmin, cmax - cmin);

            for (int r = rmin; r < rmax; r++)
            {
                for (int c = cmin; c < cmax; c++)
                {
                    result._data[r - rmin, c - cmin] = _data[r, c];
                }
            }

            return result;
        }

        /// <summary>
        /// Returns one row of the matrix as a byte array.
        /// </summary>
        public byte[] GetRow(int row)
        {
            byte[] result = new byte[Columns];
            for (int c = 0; c < Columns; c++)
            {
                result[c] = Get(row, c);
            }
            return result;
        }

        /// <summary>
        /// Exchanges two rows in the matrix.
        /// </summary>
        public void SwapRows(int r1, int r2)
        {
            Throw.If(r1 < 0 || Rows <= r1 || r2 < 0 || Rows <= r2, "Row index out of range");

            for (int c = 0; c < Columns; c++)
            {
                var tmp = _data[r1, c];
                _data[r1, c] = _data[r2, c];
                _data[r2, c] = tmp;
            }
        }

        /// <summary>
        /// Returns the inverse of this matrix.
        /// </summary>
        public Matrix Invert()
        {
            Throw.If(this.Rows != this.Columns, "Only square matrices can be inverted");

            // Create a working matrix by augmenting this one with an identity matrix on the right.
            Matrix work = Augment(Identity(Rows));

            // Do Gaussian elimination to transform the left half into an identity matrix.
            work.GaussianElimination();

            // The right half is now the inverse.
            return work.SubMatrix(0, Rows, Columns, Columns * 2);
        }

        /// <summary>
        /// Does the work of matrix inversion. Assumes that this is an r by 2r matrix.
        /// </summary>
        private void GaussianElimination()
        {
            // Clear out the part below the main diagonal and scale the main diagonal to be 1.
            for (int r = 0; r < Rows; r++)
            {
                // If the element on the diagonal is 0, find a row below
                // that has a non-zero and swap them.
                if (_data[r, r] == (byte)0)
                {
                    for (int rowBelow = r + 1; rowBelow < Rows; rowBelow++)
                    {
                        if (_data[rowBelow, r] != 0)
                        {
                            SwapRows(r, rowBelow);
                            break;
                        }
                    }
                }
                
                // If we couldn't find one, the matrix is singular.
                Throw.If(_data[r, r] == 0, "Matrix is singular");

                // Scale to 1.
                if (_data[r, r] != 1)
                {
                    byte scale = GaloisField.Divide(1, _data[r, r]);
                    for (int c = 0; c < Columns; c++)
                    {
                        _data[r, c] = GaloisField.Multiply(_data[r, c], scale);
                    }
                }

                // Make everything below the 1 be a 0 by subtracting a multiple of it.  
                // Note: subtraction and addition are both exclusive or in the Galois field.
                for (int rowBelow = r + 1; rowBelow < Rows; rowBelow++)
                {
                    if (_data[rowBelow, r] != 0)
                    {
                        byte scale = _data[rowBelow, r];
                        for (int c = 0; c < Columns; c++)
                        {
                            _data[rowBelow, c] ^= GaloisField.Multiply(scale, _data[r, c]);
                        }
                    }
                }
            }

            // Now clear the part above the main diagonal.
            for (int d = 0; d < Rows; d++)
            {
                for (int rowAbove = 0; rowAbove < d; rowAbove++)
                {
                    if (_data[rowAbove, d] != (byte)0)
                    {
                        byte scale = _data[rowAbove, d];
                        for (int c = 0; c < Columns; c++)
                        {
                            _data[rowAbove, c] ^= GaloisField.Multiply(scale, _data[d, c]);
                        }
                    }
                }
            }
        }
    }

}