using System;
using System.Collections.Generic;
using System.Text;

namespace Crossplay
{
    public static class CrossplayExtensions
    {
        /// <summary> Swaps the value's of bytes from the new buffer to the original array. </summary>
        /// <returns> The new byte array </returns>
        /// <param name="array">The buffer that will have the new bytes transferred from the <paramref name="newBuffer"/></param>
        /// <param name="start">The start position of the value swap</param>
        /// <param name="length">The amount of bytes to read</param>
        /// <param name="newBuffer">The buffer to provide the values for the <paramref name="array"/></param>
        public static byte[] SwapBytes(this byte[] array, int start, int length, byte[] newBuffer)
        {
            byte[] newArray = array;
            for (int i = start; i < length + start; i++)
            {
                array[i] = newBuffer[i - start];
            }
            return newArray;
        }
    }
}
