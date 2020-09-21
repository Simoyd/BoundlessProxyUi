
namespace BoundlessProxyUi.Util
{
    /// <summary>
    /// Extension menthods for Byte object
    /// </summary>
    static class ByteExtensions
    {
        /// <summary>
        /// Searches a byte array for another byte array
        /// </summary>
        /// <param name="src">The byte array to search in</param>
        /// <param name="length">The length of the source to search in</param>
        /// <param name="pattern">The byte sequence to search for</param>
        /// <returns>Indes of the start of the found sequence in the source sequence. -1 if not found</returns>
        public static int Search(this byte[] src, int length, byte[] pattern)
        {
            // Short circuit if searching for nothing
            if (pattern.Length < 1)
            {
                return -1;
            }

            // Determine the highest possible start index
            int count = length - pattern.Length + 1;

            // Tracks how many charactrers are left to match on the comparison at the current start index
            int j;

            // Loop through every applicable start index
            for (int i = 0; i < count; i++)
            {
                // Ensure the first character matches
                if (src[i] != pattern[0])
                {
                    continue;
                }

                // Ensure the remaining characters match starting from the end
                for (j = pattern.Length - 1; j >= 1 && src[i + j] == pattern[j]; j--) ;

                // If all characters match, return the current start index
                if (j == 0)
                {
                    return i;
                }
            }

            // No matches found
            return -1;
        }
    }
}
