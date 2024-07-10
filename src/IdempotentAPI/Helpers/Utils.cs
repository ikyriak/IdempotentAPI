using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace IdempotentAPI.Helpers
{
    public static class Utils
    {
        private static readonly JsonSerializerSettings _jsonSerializerSettingsAll = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All };
        private static readonly JsonSerializerSettings _jsonSerializerSettingsAuto = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };

        public static string GetHash(HashAlgorithm hashAlgorithm, string input)
        {
            return GetHash(hashAlgorithm, Encoding.UTF8.GetBytes(input));
        }

        public static string GetHash(HashAlgorithm hashAlgorithm, byte[] input)
        {
            // Convert the input string to a byte array and compute the hash.
            byte[] data = hashAlgorithm.ComputeHash(input);

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            var sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        /// <summary>
        /// Serialize and Compress object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static byte[]? Serialize(this object obj, JsonSerializerSettings? serializerSettings = null)
        {
            if (obj is null)
            {
                return null;
            }

            string jsonString;
            if (serializerSettings is null)
            {
                jsonString = JsonConvert.SerializeObject(obj, _jsonSerializerSettingsAll);
            }
            else
            {
                serializerSettings.TypeNameHandling = TypeNameHandling.All;
                jsonString = JsonConvert.SerializeObject(obj, serializerSettings);
            }

            byte[] encodedData = Encoding.UTF8.GetBytes(jsonString);

            return Compress(encodedData);
        }

        /// <summary>
        /// DeSerialize Compressed data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="compressedBytes"></param>
        /// <returns></returns>
        public static T? DeSerialize<T>(this byte[]? compressedBytes, JsonSerializerSettings? serializerSettings = null)
        {
            if (compressedBytes is null)
            {
                return default;
            }

            byte[]? encodedData = Decompress(compressedBytes);

            string jsonString = Encoding.UTF8.GetString(encodedData);
            if (serializerSettings is null)
            {
                return JsonConvert.DeserializeObject<T>(jsonString, _jsonSerializerSettingsAuto);
            }
            else
            {
                serializerSettings.TypeNameHandling = TypeNameHandling.Auto;
                return JsonConvert.DeserializeObject<T>(jsonString, serializerSettings);
            }
        }


        public static byte[]? Compress(byte[] input)
        {
            if (input is null)
            {
                return null;
            }

            byte[] compressesData;

            using (var outputStream = new MemoryStream())
            {
                using (var zip = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    zip.Write(input, 0, input.Length);
                }

                compressesData = outputStream.ToArray();
            }

            return compressesData;
        }

        public static byte[]? Decompress(byte[]? input)
        {
            if (input is null)
            {
                return null;
            }

            byte[] decompressedData;

            using (var outputStream = new MemoryStream())
            {
                using (var inputStream = new MemoryStream(input))
                {
                    using (var zip = new GZipStream(inputStream, CompressionMode.Decompress))
                    {
                        zip.CopyTo(outputStream);
                    }
                }

                decompressedData = outputStream.ToArray();
            }

            return decompressedData;
        }

        public static IDictionary<string, T> AnonymousObjectToDictionary<T>(
            object obj,
            Func<object, T> valueSelect)
        {
            return TypeDescriptor.GetProperties(obj)
                .OfType<PropertyDescriptor>()
                .ToDictionary(
                    prop => prop.Name,
                    prop => valueSelect(prop.GetValue(obj))
                );
        }

        public static bool IsAnonymousType(this object obj)
        {
            if (obj is null)
            {
                return false;
            }

            // HACK: The only way to detect anonymous types right now.
            Type type = obj.GetType();
            return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
                       && type.IsGenericType && type.Name.Contains("AnonymousType")
                       && (type.Name.StartsWith("<>", StringComparison.OrdinalIgnoreCase) ||
                           type.Name.StartsWith("VB$", StringComparison.OrdinalIgnoreCase))
                       && (type.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;
        }
    }
}