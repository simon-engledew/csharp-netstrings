using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections;

namespace Crypto
{
    class Netstrings : IEnumerator<String>, IEnumerable<String>
    {
        static readonly Regex SizePattern = new Regex("^(?<size>[1-9]{0,9}[0-9])(?<terminator>:)?");
        
        public Netstrings(TextReader reader)
        {
            this.reader = reader;
        }

        public static string Encode(string value)
        {
            return String.Format("{0}:{1},", value.Length, value);
        }

        private int? size;
        private char[] buffer = new char[2048];
        private TextReader reader;
        private StringBuilder builder = new StringBuilder(4096, 40960);
        private string current;

        public string Current
        {
            get { return this.current; }
        }

        public void Dispose()
        {
        }

        object IEnumerator.Current
        {
            get { return this.current; }
        }

        public bool MoveNext()
        {
            int read;

            while ((read = reader.ReadBlock(buffer, 0, buffer.Length)) > 0 || builder.Length > 0)
            {
                if (read > 0)
                {
                    builder.Append(buffer, 0, read);
                }

                if (this.size == null && read > 0)
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

                        builder.Remove(0, match.Length);
                    }
                    else
                    {
                        if (builder.Length > 10)
                        {
                            throw new OverflowException("Exceeded maximum size");
                        }
                    }
                }
                else
                {
                    throw new InvalidDataException("Size field terminator not found");
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
