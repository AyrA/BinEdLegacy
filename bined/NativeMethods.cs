using System;
using System.Runtime.InteropServices;

namespace BinEd
{
    public static class NativeMethods
    {
        /// <summary>
        /// Compares two byte arrays faster than C# ever would
        /// </summary>
        /// <param name="b1">Array 1</param>
        /// <param name="b2">Array 2</param>
        /// <param name="count">Number of bytes to compare</param>
        /// <returns>0 if identical, otherwise the numerical difference</returns>
        [DllImport("msvcrt.dll", EntryPoint = "memcmp", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int CompareBytes(byte[] b1, byte[] b2, UIntPtr count);
    }
}
