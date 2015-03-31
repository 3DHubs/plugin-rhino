using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Web;
using System.Security.Cryptography;
using System.Diagnostics;

namespace OAuth
{
    /* A value to be added to a request. If the value is an IO.Stream, it must
     * be seekable. */
    public struct ParameterValue
    {
        public object value;
        public bool encodeBase64;

        public ParameterValue(object value, bool encodeBase64 = false)
        {
            this.value = value;
            this.encodeBase64 = encodeBase64;
        }
    }

    /* URL-encodes a byte stream in the form required by OAuth. */
    class RFC3986Stream : Stream 
    {
        const int BlockSize = 4096;
        
        readonly byte[] HexDigits = Encoding.ASCII.GetBytes("0123456789ABCDEF");

        byte[] inputBuffer = new byte[BlockSize];
        byte[] outputBuffer = new byte[BlockSize * 3];
        int outputPos;
        int outputLength;
        Stream inner;
        bool spaceToPlus;

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush() { return; }

        public RFC3986Stream(Stream s, bool spaceToPlus = false)
        {
            inner = s;
            this.spaceToPlus = spaceToPlus;
        }

        public RFC3986Stream(object s, bool spaceToPlus = false)
        {
            this.spaceToPlus = spaceToPlus;
            if (s is Stream)
                inner = (Stream)s;
            else if (s is string)
                inner = new MemoryStream(Encoding.UTF8.GetBytes((string)s));
            else
                throw new ArgumentException("Non-encodable type supplied to RFC3986Stream().");
        }

        /* Populates the output buffer with encoded data, returning the number
         * of bytes encoded. */
        private int FillOutputBuffer()
        {
            outputPos = 0;
            outputLength = 0;
            int inputLength = inner.Read(inputBuffer, 0, inputBuffer.Length);
            for (int i = 0; i < inputLength; ++i) {
                byte ch = inputBuffer[i];
                if (ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z' ||
                    ch >= '0' && ch <= '9' || ch == '-' || ch == '.' ||
                    ch == '_' || ch == '~')
                {
                    outputBuffer[outputLength++] = ch;
                } 
                else if (ch == ' ' && spaceToPlus) 
                {
                    outputBuffer[outputLength++] = (byte)'+';
                } 
                else 
                {
                    outputBuffer[outputLength++] = (byte)'%';
                    outputBuffer[outputLength++] = HexDigits[ch >> 4];
                    outputBuffer[outputLength++] = HexDigits[ch & 15];
                }
            }
            return outputLength;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesWritten = 0;
            while (bytesWritten < count) {
                if (outputPos == outputLength && 0 == FillOutputBuffer())
                    break;
                int outputRemaining = outputLength - outputPos;
                int bytesToWrite = Math.Min(outputRemaining, count - bytesWritten);
                Array.Copy(
                    outputBuffer, outputPos, 
                    buffer, offset + bytesWritten, 
                    bytesToWrite);
                outputPos += bytesToWrite;
                bytesWritten += bytesToWrite;
            }
            return bytesWritten;
        }

        public static void EncodeTo(Stream outputStream, object s, bool spaceToPlus = false)
        {
            using (var encodedStream = new RFC3986Stream(s, spaceToPlus))
                encodedStream.CopyTo(outputStream);
        }

        public static string EncodeAsString(object s, bool spaceToPlus = false)
        {
            using (var encodedStream = new RFC3986Stream(s, spaceToPlus))
                using (var reader = new StreamReader(encodedStream, Encoding.ASCII)) // Encoded data is always 7-bit.
                    return reader.ReadToEnd();
        }

        public static int ComputeEncodedLength(object s, bool spaceToPlus = false)
        {
            if (s is string)
                return ComputeEncodedLength((string)s, spaceToPlus);
            if (s is Stream)
                return ComputeEncodedLength((Stream)s, spaceToPlus);
            throw new ArgumentException("String or stream required.");
        }

        public static int ComputeEncodedLength(string s, bool spaceToPlus = false)
        {
            var encoded = Encoding.UTF8.GetBytes(s);
            using (var stream = new MemoryStream(encoded))
                return ComputeEncodedLength(stream, spaceToPlus);
        }

        /* Computes the encoded length of a stream. */
        public static int ComputeEncodedLength(Stream s, bool spaceToPlus = false)
        {
            var buffer = new byte[BlockSize];
            int encodedLength = 0;
            for (; ; )
            {
                int inputLength = s.Read(buffer, 0, buffer.Length);
                if (inputLength == 0)
                    break;
                for (int i = 0; i < inputLength; ++i)
                {
                    byte ch = buffer[i];
                    if (ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z' ||
                        ch >= '0' && ch <= '9' || ch == '-' || ch == '.' ||
                        ch == '_' || ch == '~' ||
                        (ch == ' ' && spaceToPlus))
                    {
                        encodedLength += 1;
                    }
                    else
                    {
                        encodedLength += 3;
                    }
                }
            }
            return encodedLength;
        }
    }

    /* Stream-based query string encoder. */
    class QueryStringStream : Stream
    {
        enum State { Init, NextPair, WritePair };

        State state = State.Init;
        IEnumerable<KeyValuePair<string, ParameterValue>> data;
        IEnumerator<KeyValuePair<string, ParameterValue>> iter;
        byte[] outputBuffer;
        int outputBufferPos;
        Stream valueStream;
        bool spaceToPlus;
        int length = -1;
        
        public QueryStringStream(IEnumerable<KeyValuePair<string, ParameterValue>> data, 
            bool spaceToPlus = false)
        {
            this.data = data;
            this.spaceToPlus = spaceToPlus;
            Reset();
        }

        /* Non-destructively builds a string representation of the stream data.
         * For debugging. */
        public override string ToString()
        {
            var s = new QueryStringStream(data, spaceToPlus);
            var buffer = new byte[s.ComputeLength()];
            s.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer);
        }

        public void Reset()
        {
            iter = data.GetEnumerator();
            state = State.Init;
        }

        /* Computes the exact length of the query string. */
        public int ComputeLength()
        {
            if (this.length >= 0)
                return this.length;

            Reset();

            int length = 0;
            bool needDelimiter = false;
            foreach (var p in data)
            {
                length += needDelimiter ? 1 : 0;
                needDelimiter = true;
                length += RFC3986Stream.ComputeEncodedLength(p.Key, spaceToPlus);
                length += 1; // "="
                length += RFC3986Stream.ComputeEncodedLength(WrapValue(p.Value), spaceToPlus);
            }
            this.length = length;
            return length;
        }

        /* Builds a stream that transforms a parameter value for use in the query string. */
        private object WrapValue(ParameterValue pv)
        {
            object wrapped = pv.value;
            if (wrapped is Stream)
            {
                Stream vs = (Stream)pv.value;
                vs.Seek(0, SeekOrigin.Begin);
                if (pv.encodeBase64)
                    wrapped = new CryptoStream(vs, new ToBase64Transform(), CryptoStreamMode.Read);
            } 
            else if (wrapped is string) 
            {
                if (pv.encodeBase64)
                    wrapped = Convert.ToBase64String(Encoding.UTF8.GetBytes((string)wrapped));
            }

            return wrapped;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesWritten = 0;
            while (bytesWritten < count)
            {
                /* If required, move to the next pair and populate the output buffer with the delimiter
                 * and key. */
                if (state == State.Init || state == State.NextPair)
                {
                    if (!iter.MoveNext())
                        return bytesWritten;
                    var format = (state == State.NextPair) ? "&{0}=" : "{0}=";
                    var prefix = string.Format(format, RFC3986Stream.EncodeAsString(iter.Current.Key));
                    outputBuffer = Encoding.ASCII.GetBytes(prefix); // Guaranteed to be 7-bit.
                    outputBufferPos = 0;
                    valueStream = new RFC3986Stream(WrapValue(iter.Current.Value), spaceToPlus);
                    state = State.WritePair;
                }

                /* If the output buffer is non-empty, yield its data first. */
                if (outputBuffer != null && outputBufferPos != outputBuffer.Length)
                {
                    int outputBufferRemaining = outputBuffer.Length - outputBufferPos;
                    int outputBytesToWrite = Math.Min(count - bytesWritten, outputBufferRemaining);
                    Array.Copy(outputBuffer, outputBufferPos, buffer, offset + bytesWritten, outputBytesToWrite);
                    outputBufferPos += outputBytesToWrite;
                    bytesWritten += outputBytesToWrite;
                }

                /* If more data is required, copy it from the value stream. */
                if (bytesWritten < count)
                {
                    int valueBytesRead = valueStream.Read(buffer, 
                        offset + bytesWritten, count - bytesWritten);
                    bytesWritten += valueBytesRead;
                    if (valueBytesRead == 0)
                        state = State.NextPair;
                }
            }
            return bytesWritten;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush() { return; }
    }
    
    /* Builds HTTP requests signed with OAuth 1.0a. */
    class RequestSigner
    {
        private Random random = new Random();
        private string consumerKey;
        private string consumerSecret;

        public RequestSigner(string consumerKey, string consumerSecret)
        {
            this.consumerKey = consumerKey;
            this.consumerSecret = consumerSecret;
        }

        /* Builds an OAuth timestamp string for the current time. */
        private string MakeTimestamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }

        /* Builds an OAuth nonce value. */
        private string MakeNonce()
        {
            return random.Next().ToString();
        }

        /* Returns a URL in the canonical form required for the OAuth signature 
         * base string. */
        private static string CanonicalUrl(Uri url)
        {
            var s = new StringBuilder();
            s.AppendFormat("{0}://{1}", url.Scheme, url.Host);
            if (url.Port != 80 && url.Port != 443)
                s.AppendFormat(":{0}", url.Port);
            s.Append(url.AbsolutePath);
            return s.ToString();
        }

        /* Encodes a sequence of parameters in the form required for the OAuth
         * Authorization header. */
        private static string EncodeForAuthorizationHeader(
            IEnumerable<KeyValuePair<string, ParameterValue>> parameters)
        {
            StringBuilder encoded = new StringBuilder();

            bool needDelimiter = false;
            foreach (var p in parameters)
            {
                if (needDelimiter)
                    encoded.Append(',');
                needDelimiter = true;
                encoded.Append(RFC3986Stream.EncodeAsString(p.Key, false));
                encoded.Append('=');
                encoded.Append('"');
                encoded.Append(RFC3986Stream.EncodeAsString(p.Value.value, false));
                encoded.Append('"');
            }

            return encoded.ToString();
        }

        /* Feeds data from a stream into the hasher in small chunks. */
        private void HashStream(HMACSHA1 hasher, Stream s, int blockSize = 0x1000)
        {
            var block = new byte[blockSize];
            int bytesRead = 0;
            for (;;)
            {
                bytesRead = s.Read(block, 0, block.Length);
                if (bytesRead < blockSize)
                    break;
                hasher.TransformBlock(block, 0, bytesRead, block, 0);
            }
            hasher.TransformFinalBlock(block, 0, bytesRead);
        }

        public WebRequest BuildSignedRequest(out QueryStringStream body, Uri url,
            IDictionary<string, ParameterValue> data, bool useAuthHeader = true)
        {
            var oauthParameters = new Dictionary<string, ParameterValue> {
                { "oauth_consumer_key",      new ParameterValue(consumerKey)     },
                { "oauth_timestamp",         new ParameterValue(MakeTimestamp()) },
                { "oauth_nonce",             new ParameterValue(MakeNonce())     },
                { "oauth_signature_method",  new ParameterValue("HMAC-SHA1")     },
                { "oauth_version",           new ParameterValue("1.0")           }
            };

            /* The signature is based on the body parameters plus any parameters
             * query string of the endpoint URL. */
            var signingParameters = oauthParameters.Concat(data).
                ToDictionary(p => p.Key, p => p.Value);
            var queryParameters = HttpUtility.ParseQueryString(url.Query, Encoding.UTF8);
            foreach (var key in queryParameters.AllKeys)
                signingParameters.Add(key, new ParameterValue(queryParameters[key]));

            /* Build the signature base string. */
            string urlForSignature = CanonicalUrl(url);
            Stream parameterStream = new QueryStringStream(signingParameters.OrderBy(p => p.Key), false);

            /* The signature is the base64 encoded hash of the base string. */
            string signingKey = RFC3986Stream.EncodeAsString(consumerSecret) + "&";
            string signature;
            using (var hasher = new HMACSHA1(Encoding.UTF8.GetBytes(signingKey)))
            {
                /* Hash the prefix string. */
                string prefix = String.Format("POST&{0}&", 
                    RFC3986Stream.EncodeAsString(urlForSignature));
                var hashBuffer = Encoding.UTF8.GetBytes(prefix);
                hasher.TransformBlock(hashBuffer, 0, hashBuffer.Length, hashBuffer, 0);

                /* Hash the parameter string, which may be large. */
                HashStream(hasher, new RFC3986Stream(parameterStream, false));

                /* Compute the digest. */
                signature = Convert.ToBase64String(hasher.Hash);
            }
            oauthParameters.Add("oauth_signature", new ParameterValue(signature));
            signingParameters.Add("oauth_signature", new ParameterValue(signature));

            /* Form-encode the body data, which includes the OAuth parameters if
             * we're not sending an "Authorization" header. */
            var bodyParameters = useAuthHeader ? data : oauthParameters.Concat(data);
            body = new QueryStringStream(bodyParameters, true);

            /* Configure the request. */
            var request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Accept = "application/json";
            request.ContentLength = body.ComputeLength();
            body.Reset();

            /* Build an Authorization header from the OAuth parameters, including the signature. */
            if (useAuthHeader)
            {
                string authFields = EncodeForAuthorizationHeader(oauthParameters.OrderBy(p => p.Key));
                request.Headers.Add("Authorization", string.Format("OAuth {0}", authFields));
            }

            return request;
        }
    }
}
