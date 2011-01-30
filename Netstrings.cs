// Copyright (c) 2011 Simon Engledew

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Netstrings
{
    class NetstringWriter
    {
        /// <summary>
        /// Emits the specified string as a netstring.
        /// </summary>
        /// <param name="value">The string to encode as a netstring.</param>
        /// <returns>A netstring.</returns>
        public static string Encode(string value)
        {
            return String.Format("{0}:{1},", value.Length, value);
        }

        private TextWriter writer;

        public NetstringWriter(TextWriter writer)
        {
            this.writer = writer;
        }

        public void Write(string value)
        {
            this.writer.Write(NetstringWriter.Encode(value));
        }

        public void WriteLine(string value)
        {
            this.writer.Write(NetstringWriter.Encode(String.Concat(value, Environment.NewLine)));
        }

        public void Flush()
        {
            this.writer.Flush();
        }
    }

    class NetstringReader : IEnumerator<String>, IEnumerable<String>
    {
        static readonly Regex SizePattern = new Regex("^(?<size>[1-9]{0,9}[0-9])(?<terminator>:)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Decodes a single netstring and returns its payload. For streams of netstrings use the netstring object instead of multiple calls to this method.
        /// </summary>
        /// <param name="value">The netstring to decode.</param>
        /// <exception cref="System.OverflowException" />Raised if value requests a size greater than Int32.MaxLength characters.</exception>
        /// <exception cref="System.IO.InvalidDataException">Raised if value does not strictly adhere to the netstring protocol.</exception>
        /// <returns>The value of this netstring.</returns>
        public static string Decode(string value)
        {
            Match match = NetstringReader.SizePattern.Match(value);

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
        /// Convenience method which constructs a StringReader for value.
        /// </summary>
        /// <param name="value">A string containing one or more complete netstrings.</param>
        /// <param name="maxLength">The maximum length allowed for any one netstring.</param>
        public NetstringReader(String value, int maxLength = Int32.MaxValue) : this(new StringReader(value), maxLength)
        {
        }

        /// <summary>
        /// Constructs a new Netsrings instance which will emit netstrings found in reader.
        /// </summary>
        /// <param name="reader">A stream of netstrings.</param>
        /// /// <param name="maxLength">The maximum length allowed for any one netstring.</param>
        public NetstringReader(TextReader reader, int maxLength = Int32.MaxValue)
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
        /// The maximum length allowed for any one netstring.
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

            // do not read on first entry to the loop if there is a backlog in builder and you already have a netstring
            // there  might be another netstring available without consuming from the stream
            while ((builder.Length > 0 && this.current != null) || (read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (read != null)
                {
                    builder.Append(buffer, 0, (int)read);
                }

                // if a netstring is not found on this pass, setting current to null will trigger another read
                this.current = null;
                read = null;

                if (this.size == null)
                {
                    Match match = NetstringReader.SizePattern.Match(builder.ToString());

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

