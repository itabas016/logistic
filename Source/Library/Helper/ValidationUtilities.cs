using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    /// <summary>
	/// This class contains common string operation methods and useful casting methods.
	/// All simple type casts should be funnelled through the methods of this class.  When a cast fails this class
	/// provides explanatory error messages that includes the value used and the datatype attempted to cast to.
	/// </summary>
	public static class ValidationUtilities
    {
        #region String Methods
        /// <summary> 
        /// Splits a string and ensures the resulting array contains the minimum number of elements.
        /// Use this function to prevent index-out-of-bounds exceptions from being thrown after accessing the resulting array.
        /// </summary>
        /// <param name="input">The string to split.</param>
        /// <param name="minimumLength">The minimum required length of the resultant array.</param>
        /// <param name="removeEmptyEntries">If true then empty entries will be removed.</param>
        /// <param name="separators">The separators on which to split the string.</param>
        /// <returns></returns>
        public static string[] Split(string input, int minimumLength, bool removeEmptyEntries, params string[] separators)
        {
            string[] parts = input.Split(separators, (removeEmptyEntries ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None));
            if (parts.Length < minimumLength)
                throw new IntegrationException(string.Format("Invalid content.  The input \"{0}\" should have at least {1} part(s) separated the characters \"{2}\".", Elipsify(input, 500), minimumLength, string.Join(" ", separators)));

            return parts;
        }

        /// <summary> 
        /// Splits a string and ensures the resulting array contains the minimum number of elements.
        /// Use this function to prevent index-out-of-bounds exceptions from being thrown after accessing the resulting array.
        /// </summary>
        /// <param name="input">The string to split.</param>
        /// <param name="minimumLength">The minimum required length of the resultant array.</param>
        /// <param name="separators">The separators on which to split the string.</param>
        /// <returns></returns>
        public static string[] Split(string input, int minimumLength, params string[] separators)
        {
            return Split(input, minimumLength, false, separators);
        }

        /// <summary>
        /// Returns the part of the string that falss within the startIndex and length.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string SafeSubstring(string input, int startIndex, int length)
        {
            string output = string.Empty;

            // Massage the input.
            if (startIndex < 0)
                startIndex = 0;
            length = Math.Min(input.Length - startIndex, length);

            if (startIndex < input.Length)
                output = input.Substring(startIndex, length);

            return output;
        }

        /// <summary>
        /// Returns the substring but throws an informative error if the range is invalid.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string ValidSubstring(string input, int startIndex, int length)
        {
            string output = string.Empty;
            try
            {
                output = input.Substring(startIndex, length);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new IntegrationException(string.Format("Failed to extract a substring from \"{0}\" starting from {1} with length {2}.", input, startIndex, length));
            }

            return output;
        }

        /// <summary>
        /// Returns the substring but throws an informative error if the range is invalid.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        public static string ValidSubstring(string input, int startIndex)
        {
            string output = string.Empty;
            try
            {
                output = input.Substring(startIndex);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new IntegrationException(string.Format("Failed to extract a substring from \"{0}\" starting from {1}.", input, startIndex));
            }

            return output;
        }

        // FORNOW - Obviously an Elipsify method does not belong in ValidationUtilities.
        /// <summary>
        /// Replaces the portion of a string that is too long with an elipsis.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="maximumLength"></param>
        /// <returns></returns>
        public static string Elipsify(string input, int maximumLength)
        {
            // Validate the input.
            if (input == null)
                input = string.Empty;
            if (maximumLength < 0)
                throw new ArgumentOutOfRangeException(string.Format("The maximumLength supplied to the Elipsify method ({0}) must be greater than zero (0).", maximumLength));

            // Define the clip length and the elipsis string.
            int clipLength;
            string elipsis = string.Empty;
            switch (maximumLength)
            {
                case 0:
                    clipLength = 0;
                    break;
                case 1:
                    // Give priority to the first letter.
                    clipLength = 1;
                    break;
                case 2:
                    // The elipsis gets priority after the first letter.
                    clipLength = 1;
                    elipsis = ".";
                    break;
                case 3:
                    // The elipsis gets priority after the first letter.
                    clipLength = 1;
                    elipsis = "..";
                    break;
                default:
                    clipLength = maximumLength - 3;
                    elipsis = "...";
                    break;
            }

            return input.Length <= maximumLength ? input : input.Substring(0, clipLength) + elipsis;
        }
        #endregion

        #region String + Object Parsing Methods

        /// <summary>
        /// Parses an byte and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static byte ParseByte(string input)
        {
            byte output;
            if (!byte.TryParse(input, out output))
                throw new ArgumentException(string.Format("Failed to parse value \"{0}\" as an byte.", input));

            return output;
        }

        /// <summary>
        /// Parses an byte and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="style">The Style ot use while parsing the number </param>
        /// <returns></returns>
        public static byte ParseByte(string input, System.Globalization.NumberStyles style)
        {
            byte output;
            if (!byte.TryParse(input, style, null, out output))
                throw new Exception(string.Format("Failed to parse value \"{0}\" as an byte.", input));

            return output;
        }

        /// <summary>
        /// Parses an byte and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static byte ParseByte(object input)
        {
            return ParseByte(input.ToString());
        }

        /// <summary>
        /// Parses an short and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static short ParseShort(string input)
        {
            short output;
            if (!short.TryParse(input, out output))
                throw new ArgumentException(string.Format("Failed to parse value \"{0}\" as an short.", input));

            return output;
        }

        /// <summary>
        /// Parses an short and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="style">The Style ot use while parsing the number </param>
        /// <returns></returns>
        public static short ParseShort(string input, System.Globalization.NumberStyles style)
        {
            short output;
            if (!short.TryParse(input, style, null, out output))
                throw new Exception(string.Format("Failed to parse value \"{0}\" as an short.", input));

            return output;
        }

        /// <summary>
        /// Parses an short and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static short ParseShort(object input)
        {
            return ParseShort(input.ToString());
        }

        /// <summary>
        /// Parses an int and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static int ParseInt(string input)
        {
            int output;
            if (!int.TryParse(input, out output))
                throw new ArgumentException(string.Format("Failed to parse value \"{0}\" as an int.", input));

            return output;
        }

        /// <summary>
        /// Parses an int and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="style">The Style ot use while parsing the number </param>
        /// <returns></returns>
        public static int ParseInt(string input, System.Globalization.NumberStyles style)
        {
            int output;
            if (!int.TryParse(input, style, null, out output))
                throw new Exception(string.Format("Failed to parse value \"{0}\" as an int.", input));

            return output;
        }

        /// <summary>
		/// Parses an int and throws an informative error if fails.
		/// </summary>
		/// <param name="input">The input.</param>
		/// <returns></returns>
		public static int ParseInt(object input)
        {
            return ParseInt(input.ToString());
        }

        /// <summary>
        /// Parses an UInt16 and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static UInt16 ParseUInt16(string input)
        {
            UInt16 output;
            if (!UInt16.TryParse(input, out output))
                throw new Exception(string.Format("Failed to parse value \"{0}\" as an UInt16.", input));

            return output;
        }

        /// <summary>
        /// Parses an UInt16 and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="style"></param>
        /// <returns></returns>
        public static UInt16 ParseUInt16(string input, System.Globalization.NumberStyles style)
        {
            UInt16 output;
            if (!UInt16.TryParse(input, style, null, out output))
                throw new Exception(string.Format("Failed to parse value \"{0}\" as an UInt16.", input));

            return output;
        }

        /// <summary>
        /// Parses an UInt16 and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static UInt16 ParseUInt16(object input)
        {
            return ParseUInt16(input.ToString());
        }


        /// <summary>
        /// Parses an UInt32 and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static UInt32 ParseUInt32(string input)
        {
            UInt32 output;
            if (!UInt32.TryParse(input, out output))
                throw new Exception(string.Format("Failed to parse value \"{0}\" as an UInt32.", input));

            return output;
        }

        /// <summary>
        /// Parses an UInt32 and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="style"></param>
        /// <returns></returns>
        public static UInt32 ParseUInt32(string input, System.Globalization.NumberStyles style)
        {
            UInt32 output;
            if (!UInt32.TryParse(input, style, null, out output))
                throw new Exception(string.Format("Failed to parse value \"{0}\" as an UInt32.", input));

            return output;
        }

        /// <summary>
        /// Parses an UInt32 and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static UInt32 ParseUInt32(object input)
        {
            return ParseUInt32(input.ToString());
        }

        /// <summary>
        /// Parses a long and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static long ParseLong(string input)
        {
            long output;
            if (!long.TryParse(input, out output))
                throw new ArgumentException(string.Format("Failed to parse value \"{0}\" as an long.", input));

            return output;
        }

        /// <summary>
        /// Parses a long and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="style">The Style ot use while parsing the number </param>
        /// <returns></returns>
        public static long ParseLong(string input, System.Globalization.NumberStyles style)
        {
            long output;
            if (!long.TryParse(input, style, null, out output))
                throw new Exception(string.Format("Failed to parse value \"{0}\" as an long.", input));

            return output;
        }

        /// <summary>
        /// Parses an long and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static long ParseLong(object input)
        {
            return ParseLong(input.ToString());
        }

        /// <summary>
        /// Parses a decimal value and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static decimal ParseDecimal(string input)
        {
            decimal output;
            if (!decimal.TryParse(input, out output))
                throw new ArgumentException(string.Format("Failed to parse value \"{0}\" as a decimal.", input));

            return output;
        }

        /// <summary>
        /// Parses a decimal value and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static decimal ParseDecimal(object input)
        {
            return ParseDecimal(input.ToString());
        }

        /// <summary>
        /// Parses a bool and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static bool ParseBool(string input)
        {
            bool output;
            if (!bool.TryParse(input, out output))
                throw new ArgumentException(string.Format("Failed to parse value \"{0}\" as a bool.", input));

            return output;
        }

        /// <summary>
        /// Parses a bool and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static bool ParseBool(object input)
        {
            return ParseBool(input.ToString());
        }

        /// <summary>
        /// Parses an int of zero or one as false or true, respectively.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static bool ParseDbBool(int input)
        {
            bool result = false;
            switch (input)
            {
                case 0:
                    result = false;
                    break;
                case 1:
                    result = true;
                    break;
                default:
                    throw new ArgumentException(string.Format("Failed to parse value \"{0}\" as a DB boolean (0 or 1).", input));
            }

            return result;
        }

        /// <summary>
        /// Translates a string containing "0" or "1" to false or true, respectively.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static bool ParseDbBool(string input)
        {
            int value = ParseInt(input);
            return ParseDbBool(value);
        }

        /// <summary>
        /// Parses an int of zero or one as false or true, respectively.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static bool ParseDbBool(object input)
        {
            return ParseDbBool(input.ToString());
        }

        /// <summary>
        /// Parses a DateTime and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static DateTime ParseDateTime(string input)
        {
            DateTime output;
            if (!DateTime.TryParse(input, out output))
                throw new ArgumentException(string.Format("Failed to parse value \"{0}\" as a DateTime.", input));

            return output;
        }

        /// <summary>
        /// Parses a DateTime and throws an informative error if fails.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns></returns>
        public static DateTime ParseDateTime(object input)
        {
            return ParseDateTime(input.ToString());
        }
        #endregion
    }
}
