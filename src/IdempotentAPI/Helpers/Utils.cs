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
using System.Text.Json;

namespace IdempotentAPI.Helpers;

public static class Utils
{
    private static readonly JsonSerializerOptions _defaultSerializerOptions = CreateTypedOptions(null);

    /// <summary>
    /// Caches a converter-enabled copy per caller-provided <see cref="JsonSerializerOptions"/> instance,
    /// because STJ options become read-only after first use and must not be mutated.
    /// </summary>
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> _typedSerializerOptions = new();

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
    public static byte[]? Serialize(this object obj, JsonSerializerOptions? serializerOptions = null)
    {
        if (obj is null)
        {
            return null;
        }

        byte[] encodedData = JsonSerializer.SerializeToUtf8Bytes(obj, obj.GetType(), GetTypedOptions(serializerOptions));

        return Compress(encodedData);
    }

    /// <summary>
    /// DeSerialize Compressed data.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="compressedBytes"></param>
    /// <returns></returns>
    public static T? DeSerialize<T>(this byte[]? compressedBytes, JsonSerializerOptions? serializerOptions = null)
    {
        if (compressedBytes is null)
        {
            return default;
        }

        byte[]? encodedData = Decompress(compressedBytes);
        if (encodedData is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(encodedData, GetTypedOptions(serializerOptions));
    }

    /// <summary>
    /// Get the <see cref="JsonSerializerOptions"/> (default or caller-provided) extended with the
    /// <see cref="TypedObjectJsonConverter"/> that preserves runtime types of object values.
    /// </summary>
    public static JsonSerializerOptions GetTypedOptions(JsonSerializerOptions? serializerOptions)
    {
        if (serializerOptions is null)
        {
            return _defaultSerializerOptions;
        }

        if (serializerOptions.Converters.Any(c => c is TypedObjectJsonConverter))
        {
            return serializerOptions;
        }

        return _typedSerializerOptions.GetValue(serializerOptions, CreateTypedOptions);
    }

    private static JsonSerializerOptions CreateTypedOptions(JsonSerializerOptions? baseOptions)
    {
        JsonSerializerOptions options = baseOptions is null
            ? new JsonSerializerOptions()
            : new JsonSerializerOptions(baseOptions);

        options.Converters.Insert(0, new TypedObjectJsonConverter());
        return options;
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
        Func<object?, T> valueSelect)
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