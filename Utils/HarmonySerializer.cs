/*
Copyright © 2022 Marcel Greter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

static class HarmonySerializer
{

    private static Dictionary<Type, Tuple<
        Func<BinaryReader, object>, // Encoder
        Action<BinaryWriter, object> // Decoder
    >> Serializers = new Dictionary<Type, Tuple<
            Func<BinaryReader, object>,
            Action<BinaryWriter, object>>>();

    public static void RegisterSerializer(Type type,
        Func<BinaryReader, object> encoder,
        Action<BinaryWriter, object> decoder)
    {
        Serializers.Add(type, new Tuple<
            Func<BinaryReader, object>,
            Action<BinaryWriter, object>>
                (encoder, decoder));
    }

    private static Action<BinaryWriter, object> GetTypeSerializer(Type type)
    {
        return Serializers.TryGetValue(type, out var converters)
            ? converters.Item2 : throw new Exception("No Serializer for "
                + type.FullDescription());
    }

    private static Func<BinaryReader, object> GetTypeDeserializer(Type type)
    {
        return Serializers.TryGetValue(type, out var converters)
            ? converters.Item1 : throw new Exception("No Serializer for "
                + type.FullDescription());
    }

    public static void Freeze(BinaryWriter wr, object arg)
    {
        // Special null case
        if (arg == null)
        {
            wr.Write("nil");
            return;
        }
        // Write the full type description with it
        // Quite a bit of overhead, but makes it versatile
        wr.Write(arg.GetType().FullDescription());
        // Dispatch value to binary writer or encoder
        switch (arg)
        {
            case byte value: wr.Write(value); break;
            case short value: wr.Write(value); break;
            case ushort value: wr.Write(value); break;
            case int value: wr.Write(value); break;
            case uint value: wr.Write(value); break;
            case long value: wr.Write(value); break;
            case ulong value: wr.Write(value); break;
            case float value: wr.Write(value); break;
            case double value: wr.Write(value); break;
            case string value: wr.Write(value); break;
            case bool value: wr.Write(value); break;
            case Vector2 value: StreamUtils.Write(wr, value); break;
            case Vector2i value: StreamUtils.Write(wr, value); break;
            case Vector3 value: StreamUtils.Write(wr, value); break;
            case Vector3i value: StreamUtils.Write(wr, value); break;
            // case Color32 value: StreamUtils.WriteColor32(wr, value); break;
            // case Color value: StreamUtils.Write(wr, value); break;
            case object[] value:
                wr.Write(value.Length);
                foreach (object val in value)
                    Freeze(wr, val);
                break;
            // The following will error hard if the type is not known!
            default: GetTypeSerializer(arg.GetType())(wr, arg); break;
        }
    }

    public static object Thaw(BinaryReader br)
    {
        string fqtn = br.ReadString();
        switch (fqtn)
        {
            case "nil": return null;
            case "System.Byte": return br.ReadByte();
            case "System.Int16": return br.ReadInt16();
            case "System.UInt16": return br.ReadUInt16();
            case "System.Int32": return br.ReadInt32();
            case "System.UInt32": return br.ReadUInt32();
            case "System.Int64": return br.ReadInt64();
            case "System.UInt64": return br.ReadUInt64();
            case "System.Single": return br.ReadSingle();
            case "System.Double": return br.ReadDouble();
            case "System.String": return br.ReadString();
            case "System.Boolean": return br.ReadBoolean();
            case "Vector2i": return StreamUtils.ReadVector2i(br);
            case "Vector3i": return StreamUtils.ReadVector3i(br);
            case "UnityEngine.Vector2": return StreamUtils.ReadVector2(br);
            case "UnityEngine.Vector3": return StreamUtils.ReadVector3(br);
            // case "UnityEngine.Color": return StreamUtils.ReadColor(br);
            // case "UnityEngine.Color32": return StreamUtils.ReadColor32(br);
            case "System.Object[]":
                int count = br.ReadInt32();
                object[] arr = new object[count];
                for (int i = 0; i < count; i++)
                    arr[i] = Thaw(br);
                return arr;
            default:
                Type type = AccessTools.TypeByName(fqtn);
                return GetTypeDeserializer(type)(br);
        }
    }

}
