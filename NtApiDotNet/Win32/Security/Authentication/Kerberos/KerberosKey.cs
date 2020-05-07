﻿//  Copyright 2020 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

using NtApiDotNet.Utilities.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NtApiDotNet.Win32.Security.Authentication.Kerberos
{
    /// <summary>
    /// A single kerberos key.
    /// </summary>
    public sealed class KerberosKey
    {
        private readonly byte[] _key;

        /// <summary>
        /// The Key encryption type.
        /// </summary>
        public KerberosEncryptionType KeyEncryption { get; }
        /// <summary>
        /// The key.
        /// </summary>
        public byte[] Key => (byte[])_key.Clone();
        /// <summary>
        /// The key name type.
        /// </summary>
        public KerberosNameType NameType { get; }
        /// <summary>
        /// The Realm for the key.
        /// </summary>
        public string Realm { get; }
        /// <summary>
        /// The name components for the key.
        /// </summary>
        public IEnumerable<string> Components { get; }
        /// <summary>
        /// Principal name as a string.
        /// </summary>
        public string Principal => $"{string.Join("/", Components)}@{Realm}";
        /// <summary>
        /// Timestamp when key was created.
        /// </summary>
        public DateTime Timestamp { get; }
        /// <summary>
        /// Key Version Number (KVNO).
        /// </summary>
        public uint Version { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="key_encryption">The Key encryption type.</param>
        /// <param name="key">The key.</param>
        /// <param name="name_type">The key name type.</param>
        /// <param name="realm">The Realm for the key.</param>
        /// <param name="components">The name components for the key.</param>
        /// <param name="timestamp">Timestamp when key was created.</param>
        /// <param name="version">Key Version Number (KVNO).</param>
        public KerberosKey(KerberosEncryptionType key_encryption, byte[] key, KerberosNameType name_type, 
            string realm, string[] components, DateTime timestamp, uint version)
        {
            KeyEncryption = key_encryption;
            _key = key;
            NameType = name_type;
            Realm = realm;
            Components = components;
            Timestamp = timestamp;
            Version = version;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="key_encryption">The Key encryption type.</param>
        /// <param name="key">The key.</param>
        /// <param name="name_type">The key name type.</param>
        /// <param name="realm">The Realm for the key.</param>
        /// <param name="components">The name components for the key.</param>
        /// <param name="timestamp">Timestamp when key was created.</param>
        /// <param name="version">Key Version Number (KVNO).</param>
        public KerberosKey(KerberosEncryptionType key_encryption, byte[] key, KerberosNameType name_type,
            string realm, IEnumerable<string> components, DateTime timestamp, uint version)
        {
            KeyEncryption = key_encryption;
            _key = (byte[])key.Clone();
            NameType = name_type;
            Realm = realm;
            Components = components.ToArray();
            Timestamp = timestamp;
            Version = version;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="key_encryption">The Key encryption type.</param>
        /// <param name="key">The key.</param>
        /// <param name="name_type">The key name type.</param>
        /// <param name="principal">Principal for key, in form TYPE/name@realm.</param>
        /// <param name="timestamp">Timestamp when key was created.</param>
        /// <param name="version">Key Version Number (KVNO).</param>
        public KerberosKey(KerberosEncryptionType key_encryption, byte[] key, KerberosNameType name_type,
            string principal, DateTime timestamp, uint version)
            : this(key_encryption, key, name_type, GetRealm(principal),
                  GetComponents(principal), timestamp, version)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="key_encryption">The Key encryption type.</param>
        /// <param name="key">The key as a hex string.</param>
        /// <param name="name_type">The key name type.</param>
        /// <param name="principal">Principal for key, in form TYPE/name@realm.</param>
        /// <param name="timestamp">Timestamp when key was created.</param>
        /// <param name="version">Key Version Number (KVNO).</param>
        public KerberosKey(KerberosEncryptionType key_encryption, string key, KerberosNameType name_type,
            string principal, DateTime timestamp, uint version)
            : this(key_encryption, GetKey(key), name_type, principal, timestamp, version)
        {
        }

        /// <summary>
        /// Derive a key from a password.
        /// </summary>
        /// <remarks>Not all encryption types are supported.</remarks>
        /// <param name="key_encryption">The key encryption to use.</param>
        /// <param name="password">The password to derice from.</param>
        /// <param name="iterations">Iterations for the password derivation.</param>
        /// <param name="name_type">The key name type.</param>
        /// <param name="principal">Principal for key, in form TYPE/name@realm.</param>
        /// <param name="salt">Salt for the key.</param>
        /// <param name="version">Key Version Number (KVNO).</param>
        /// <returns></returns>
        public static KerberosKey DeriveKey(KerberosEncryptionType key_encryption, string password, 
            int iterations, KerberosNameType name_type, string principal, string salt, uint version)
        {
            if (principal is null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            byte[] key;

            switch (key_encryption)
            {
                case KerberosEncryptionType.ARCFOUR_HMAC_MD5:
                case KerberosEncryptionType.ARCFOUR_HMAC_MD5_56:
                case KerberosEncryptionType.ARCFOUR_HMAC_OLD:
                case KerberosEncryptionType.ARCFOUR_HMAC_OLD_EXP:
                    key = MD4.CalculateHash(Encoding.Unicode.GetBytes(password));
                    break;
                case KerberosEncryptionType.AES128_CTS_HMAC_SHA1_96:
                    key = DeriveAesKey(password, MakeSalt(salt, principal), iterations, 16);
                    break;
                case KerberosEncryptionType.AES256_CTS_HMAC_SHA1_96:
                    key = DeriveAesKey(password, MakeSalt(salt, principal), iterations, 32);
                    break;
                default:
                    throw new ArgumentException($"Unsupported key type {key_encryption}", nameof(key_encryption));
            }

            return new KerberosKey(key_encryption, key, name_type, principal, DateTime.Now, version);
        }

        private static string MakeSalt(string salt, string principal)
        {
            if (!string.IsNullOrEmpty(salt))
                return salt;
            return GetRealm(principal).ToUpper() + string.Join("", GetComponents(principal));
        }

        private static string GetRealm(string principal)
        {
            int index = principal.LastIndexOf('@');
            if (index < 0)
                return string.Empty;
            return principal.Substring(index + 1);
        }

        private static string[] GetComponents(string principal)
        {
            int index = principal.LastIndexOf('@');
            if (index >= 0)
                principal = principal.Substring(0, index);
            return principal.Split('/');
        }

        private static byte[] GetKey(string key)
        {
            if ((key.Length % 1) != 0)
                throw new ArgumentException("Invalid key length.");
            byte[] ret = new byte[key.Length / 2];
            for (int i = 0; i < ret.Length; ++i)
            {
                ret[i] = Convert.ToByte(key.Substring(i * 2, 2), 16);
            }
            return ret;
        }

        private static byte[] DeriveAesKey(string password, string salt, int iterations, int key_size)
        {
            // "kerberos" n-folded out to 16 bytes.
            byte[] folded_key = { 0x6b, 0x65, 0x72, 0x62, 0x65, 0x72, 0x6f, 0x73, 0x7b, 0x9b, 0x5b, 0x2b, 0x93, 0x13, 0x2b, 0x93 };

            Rfc2898DeriveBytes pbkdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(salt), iterations);
            byte[] random_key = pbkdf.GetBytes(key_size);
            Aes encrypt = new AesManaged();
            encrypt.Mode = CipherMode.ECB;

            byte[] ret = new byte[random_key.Length];
            var transform = encrypt.CreateEncryptor(random_key, new byte[16]);
            transform.TransformBlock(folded_key, 0, 16, folded_key, 0);
            Array.Copy(folded_key, ret, 16);
            if (ret.Length > 16)
            {
                transform.TransformBlock(folded_key, 0, 16, folded_key, 0);
                Array.Copy(folded_key, 0, ret, 16, 16);
            }
            return ret;
        }
    }
}
