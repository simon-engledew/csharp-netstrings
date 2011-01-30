using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Crypto
{
    class Netstrings : IEnumerator<String>, IEnumerable<String>
    {
        static readonly Regex SizePattern = new Regex("^(?<size>[1-9]{0,9}[0-9])(?<terminator>:)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Emits the specified string as a netstring.
        /// </summary>
        /// <param name="value">The string to encode as a netstring.</param>
        /// <returns>A netstring.</returns>
        public static string Encode(string value)
        {
            return String.Format("{0}:{1},", value.Length, value);
        }

        /// <summary>
        /// Decodes the specified netstring and returns its payload. For streams of netstrings use the netstring object instead of multiple calls to this method.
        /// </summary>
        /// <param name="value">The netstring to decode.</param>
        /// <exception cref="System.OverflowException" />Raised if value requests a size greater than Int32.MaxLength characters.</exception>
        /// <exception cref="System.IO.InvalidDataException">Raised if value does not strictly adhere to the netstring protocol.</exception>
        /// <returns>The value of this netstring.</returns>
        public static string Decode(string value)
        {
            Match match = Netstrings.SizePattern.Match(value);

            if (match.Success == false || match.Groups["terminator"].Success == false)
            {
                throw new InvalidDataException("Illegal size field");
            }

            Group sizeGroup = match.Groups["size"];

            int size = Convert.ToInt32(sizeGroup.Value);

            value = value.Remove(0, match.Length);

            if (value.Length == size + 1)
            {
                try
                {
                    return value.Substring(0, size);
                }
                finally
                {
                    if (value.Remove(0, size) != ",")
                    {
                        throw new InvalidDataException("Payload terminator not found");
                    }
                }
            }
            else
            {
                throw new InvalidDataException("Exceeded requested size");
            }
        }

        /// <summary>
        /// Constructs a new Netsrings instance which will emit netstrings found in reader.
        /// </summary>
        /// <param name="reader">A stream of netstrings.</param>
        public Netstrings(TextReader reader, int maxLength = Int32.MaxValue)
        {
            this.reader = reader;
            this.builder = new StringBuilder(2048, maxLength);
        }

        private int? size;
        private char[] buffer = new char[2048];
        private TextReader reader;
        private StringBuilder builder;
        private string current;

        /// <summary>
        /// The maximum length possible for any one netstring.
        /// </summary>
        public int MaxLength
        {
            get { return this.builder.MaxCapacity; }
        }

        /// <summary>
        /// The most recently read netstring in reader.
        /// </summary>
        public string Current
        {
            get { return this.current; }
        }

        public void Dispose()
        {
            this.reader.Dispose();
        }

        object IEnumerator.Current
        {
            get { return this.current; }
        }

        /// <summary>
        /// Advances reader until a complete netstring is found.
        /// </summary>
        /// <exception cref="System.OverflowException" />Raised if value requests a size greater than Int32.MaxLength characters.</exception>
        /// <exception cref="System.IO.InvalidDataException">Raised if value does not strictly adhere to the netstring protocol.</exception>
        /// <returns>false if Netstrings is at the end of the stream.</returns>
        public bool MoveNext()
        {
            int? read = null;

            while ((builder.Length > 0 && this.current != null) || (read = reader.ReadBlock(buffer, 0, buffer.Length)) > 0)
            {
                if (read != null)
                {
                    builder.Append(buffer, 0, (int)read);
                }

                this.current = null;
                read = null;

                if (this.size == null)
                {
                    Match match = Netstrings.SizePattern.Match(builder.ToString());

                    if (match.Success == false)
                    {
                        throw new InvalidDataException("Illegal size field");
                    }

                    if (match.Groups["terminator"].Success)
                    {
                        Group sizeGroup = match.Groups["size"];

                        this.size = Convert.ToInt32(sizeGroup.Value);

                        if (this.size > this.MaxLength)
                        {
                            throw new OverflowException("Requested size exceeded maximum length");
                        }

                        builder.Remove(0, match.Length);
                    }
                    else
                    {
                        if (builder.Length > 10)
                        {
                            throw new OverflowException("Size field exceeded maximum width");
                        }
                    }
                }

                if (this.size != null)
                {
                    int size = (int)this.size;

                    if (builder.Length > size)
                    {
                        char[] output = new char[size];

                        builder.CopyTo(0, output, 0, size);

                        this.current = new String(output);

                        builder.Remove(0, size);

                        if (builder[0] != ',')
                        {
                            throw new InvalidDataException("Payload terminator not found");
                        }

                        builder.Remove(0, 1);

                        this.size = null;

                        return true;
                    }
                }
                else
                {
                    if (builder.Length > 10)
                    {
                        throw new InvalidDataException("Size field terminator not found");
                    }
                }
            }

            if (builder.Length > 0)
            {
                throw new InvalidDataException("Unexpected EOF");
            }

            return false;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<string> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }
    }
}
