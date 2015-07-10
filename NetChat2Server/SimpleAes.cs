using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NetChat2Server
{
    /// <summary>
    /// A class to encrypt/decrypt messages with a simple to use interface.
    ///
    /// Credit goes to Mark Brittingham on StackOverflow for his SimpleAES. All I did was change
    /// how it gets initialized and allowed the user to enter strings for custom keys/vectors at runtime
    /// </summary>
    public class SimpleAes
    {
        private readonly ICryptoTransform _decryptorTransform;
        private readonly ICryptoTransform _encryptorTransform;
        private readonly UTF8Encoding _utfEncoder;

        public SimpleAes(string keySeed, string vectorSeed)
        {
            var rand = new Random(keySeed.GetHashCode());
            var tempKey = new byte[32];
            rand.NextBytes(tempKey);
            this.Key = tempKey;

            rand = new Random(vectorSeed.GetHashCode());
            var tempVector = new byte[16];
            rand.NextBytes(tempVector);
            this.Vector = tempVector;

            var rm = new RijndaelManaged();
            this._encryptorTransform = rm.CreateEncryptor(this.Key, this.Vector);
            this._decryptorTransform = rm.CreateDecryptor(this.Key, this.Vector);

            this._utfEncoder = new UTF8Encoding();
        }

        public byte[] Key { get; private set; }

        public byte[] Vector { get; private set; }

        public string Decrypt(byte[] bytes)
        {
            var encryptedStream = new MemoryStream();
            var decryptStream = new CryptoStream(encryptedStream, this._decryptorTransform, CryptoStreamMode.Write);
            decryptStream.Write(bytes, 0, bytes.Length);
            decryptStream.FlushFinalBlock();

            encryptedStream.Position = 0;
            var decryptedBytes = new byte[encryptedStream.Length];
            encryptedStream.Read(decryptedBytes, 0, decryptedBytes.Length);
            encryptedStream.Close();
            return this._utfEncoder.GetString(decryptedBytes);
        }

        public byte[] Encrypt(string str)
        {
            byte[] bytes = this._utfEncoder.GetBytes(str);

            var memoryStream = new MemoryStream();

            var cs = new CryptoStream(memoryStream, this._encryptorTransform, CryptoStreamMode.Write);
            cs.Write(bytes, 0, bytes.Length);
            cs.FlushFinalBlock();

            memoryStream.Position = 0;
            var encrypted = new byte[memoryStream.Length];
            memoryStream.Read(encrypted, 0, encrypted.Length);

            cs.Close();
            memoryStream.Close();
            return encrypted;
        }
    }
}