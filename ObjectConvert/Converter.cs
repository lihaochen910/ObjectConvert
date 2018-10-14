// PSV Corpse Party
// Type: ObjectConvert.Converter
#define UNITY_ENGINE

using System;
using System.IO;
using System.Reflection;
#if UNITY_ENGINE
using UnityEngine;
#endif

namespace ObjectConvert
{
    public class Converter
    {
        public static byte[] ToBytes(object obj)
        {
            if (obj == null)
                return null;
            Converter.ToBytesConvert toBytesConvert = new Converter.ToBytesConvert();
            Converter.ValueAction(new Converter.ValueActionDelegate(toBytesConvert.Action), new Converter.ObjectDummyAccessor(obj), null);
            return toBytesConvert.bytes;
        }

        public static byte[] ToBytes<T>(T obj) where T : new()
        {
            if (obj == null)
                return null;
            Converter.ToBytesConvert toBytesConvert = new Converter.ToBytesConvert();
            Converter.ValueAction(toBytesConvert.Action, new Converter.ObjectDummyAccessor(obj), null);
            return toBytesConvert.bytes;
        }

        public static void ToObject(object obj, byte[] bytes)
        {
            if (obj == null || bytes == null)
                return;
            Converter.ToObjectConvert toObjectConvert = new Converter.ToObjectConvert(bytes);
            Converter.ValueAction(toObjectConvert.Action, new Converter.ObjectDummyAccessor(obj), null);
        }

        public static void ToObject<T>(ref T obj, byte[] bytes) where T : new()
        {
            if (obj == null || bytes == null)
                return;
            Converter.ToObjectConvert toObjectConvert = new Converter.ToObjectConvert(bytes);
            Converter.ValueAction(toObjectConvert.Action, new Converter.ObjectDummyAccessor(obj), null);
        }

        private static void ValueAction(Converter.ValueActionDelegate action, Converter.Accessor accessor, object[] attributes)
        {
            // 普通字段
            if (action(accessor, attributes))
                return;

            // 数组字段、自定义类字段
            Type type = accessor.type;
            if (type.IsArray)
            {
                Converter.ArrayEnumeration(action, accessor, attributes);
            }
            else
            {
                if (!type.IsClass) // 不支持struct
                    return;
                Converter.FieldEnumeration(action, accessor);
            }
        }

        /// <summary>
        /// 类字段值枚举
        /// </summary>
        /// <param name="action"></param>
        /// <param name="accessor"></param>
        private static void FieldEnumeration(Converter.ValueActionDelegate action, Converter.Accessor accessor)
        {
            Type type = accessor.type;

            Converter.FieldAccessor fieldAccessor = new Converter.FieldAccessor(accessor.obj);

            //Debug.Log($"FieldEnumeration 开始枚举{type.Name}的字段 -->");

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!(Attribute.GetCustomAttribute(field, typeof(NotConvertAttribute)) is NotConvertAttribute))
                {
                    fieldAccessor.SetFieldInfo(field);
                    //if (accessor.obj != null)
                    //    Debug.Log($"{type.Name}.{field.Name} = {field.GetValue(accessor.obj)}");
                    Converter.ValueAction(action, fieldAccessor, field.GetCustomAttributes(false));
                }
            }
        }

        private static void ArrayEnumeration(Converter.ValueActionDelegate action, Converter.Accessor accessor, object[] attributes)
        {
            Converter.ArrayAccessor accessor1 = new Converter.ArrayAccessor(accessor.obj as Array);
            Converter.ArrayEnumeration(action, accessor1, 0, attributes);
        }

        private static void ArrayEnumeration(Converter.ValueActionDelegate action, Converter.ArrayAccessor accessor, int rank, object[] attributes)
        {
            Array array = accessor.array;
            int lowerBound = array.GetLowerBound(rank);
            int upperBound = array.GetUpperBound(rank);
            if (rank + 1 < array.Rank)
            {
                for (int index = lowerBound; index <= upperBound; ++index)
                {
                    accessor.index[rank] = index;
                    Converter.ArrayEnumeration(action, accessor, rank + 1, attributes);
                }
            }
            else
            {
                for (int index = lowerBound; index <= upperBound; ++index)
                {
                    //Debug.Log($"ArrayEnumeration 枚举数组数据({index} / {array.Length})");

                    accessor.index[rank] = index;
                    Converter.ValueAction(action, accessor, attributes);
                }
            }
        }

        private abstract class Accessor
        {
            public abstract object obj { get; set; }

            public abstract Type type { get; }
        }

        private class ObjectDummyAccessor : Accessor
        {
            private object obj_;
            public ObjectDummyAccessor(object obj)
            {
                this.obj_ = obj;
            }

            public override object obj { get { return obj_; } set { /*obj_ = value;*/ } }

            public override Type type
            {
                get
                {
                    return this.obj.GetType();
                }
            }
        }

        private class FieldAccessor : Accessor
        {
            private object obj_;
            private FieldInfo field_info_;

            public FieldAccessor(object obj)
            {
                this.obj_ = obj;
            }

            public override object obj
            {
                get
                {
                    return this.field_info_.GetValue(this.obj_);
                }
                set
                {
                    this.field_info_.SetValue(this.obj_, value);
                }
            }

            public override Type type
            {
                get
                {
                    return this.field_info_.FieldType;
                }
            }

            public void SetFieldInfo(FieldInfo field_info)
            {
                this.field_info_ = field_info;
            }
        }

        private class ArrayAccessor : Accessor
        {
            private Type type_;

            public ArrayAccessor(Array array)
            {
                this.array = array;
                Type type = this.array.GetType();
                this.index = new int[type.GetArrayRank()];
                this.type_ = type.GetElementType();
            }

            public override object obj
            {
                get
                {
                    return this.array.GetValue(this.index);
                }
                set
                {
                    this.array.SetValue(value, this.index);
                }
            }

            public override Type type { get { return type_; } }

            public Array array { get; private set; }

            public int[] index { get; private set; }
        }

        private class ToBytesConvert
        {
            private MemoryStream stream_ = new MemoryStream();

            public byte[] bytes => this.stream_.ToArray();

            public bool Action(Converter.Accessor accessor, object[] attributes)
            {
                object obj = accessor.obj;
                byte[] buffer = null;
                Type type = accessor.type;

                if (type == typeof(bool))
                    buffer = BitConverter.GetBytes((bool)obj);
                else if (type == typeof(char))
                    buffer = BitConverter.GetBytes((char)obj);
                else if (type == typeof(byte) || type == typeof(sbyte))
                    buffer = new byte[1] { (byte)obj };
                else if (type == typeof(short))
                    buffer = BitConverter.GetBytes((short)obj);
                else if (type == typeof(int))
                    buffer = BitConverter.GetBytes((int)obj);
                else if (type == typeof(long))
                    buffer = BitConverter.GetBytes((long)obj);
                else if (type == typeof(double))
                    buffer = BitConverter.GetBytes((double)obj);
                else if (type == typeof(float))
                    buffer = BitConverter.GetBytes((float)obj);
                else if (type == typeof(ushort))
                    buffer = BitConverter.GetBytes((ushort)obj);
                else if (type == typeof(uint))
                    buffer = BitConverter.GetBytes((uint)obj);
                else if (type == typeof(ulong))
                    buffer = BitConverter.GetBytes((ulong)obj);
#if UNITY_ENGINE
                else if (type == typeof(Vector2))
                    buffer = Vector2ToBytes((Vector2)obj);
                else if (type == typeof(Vector3))
                    buffer = Vector3ToBytes((Vector3)obj);
                else if (type == typeof(Vector4))
                    buffer = Vector4ToBytes((Vector4)obj);
                else if (type == typeof(Quaternion))
                    buffer = QuaternionToBytes((Quaternion)obj);
                else if (type == typeof(Color))
                    buffer = ColorToBytes((Color)obj);
#endif
                else if (type == typeof(string))
                {
                    string str = obj as string ?? string.Empty;
                    FixedStringAttribute fixedStringAttribute = null;
                    foreach (object attribute in attributes)
                    {
                        if (attribute is FixedStringAttribute)
                        {
                            fixedStringAttribute = attribute as FixedStringAttribute;
                            break;
                        }
                    }
                    char[] charArray = str.ToCharArray();
                    if (fixedStringAttribute != null)
                    {
                        buffer = new byte[fixedStringAttribute.length * 2];
                        int length = str.Length;
                        if (length > fixedStringAttribute.length)
                            length = fixedStringAttribute.length;
                        int index = 0;
                        int destinationIndex = 0;
                        while (index < length)
                        {
                            Array.Copy(BitConverter.GetBytes(charArray[index]), 0, buffer, destinationIndex, 2);
                            ++index;
                            destinationIndex += 2;
                        }
                    }
                    else
                    {
                        int length = str.Length;
                        buffer = new byte[4 + length * 2];
                        Array.Copy(BitConverter.GetBytes(length), 0, buffer, 0, 4);
                        int index = 0;
                        int destinationIndex = 4;
                        while (index < length)
                        {
                            Array.Copy(BitConverter.GetBytes(charArray[index]), 0, buffer, destinationIndex, 2);
                            ++index;
                            destinationIndex += 2;
                        }
                    }
                }
                else if (type.IsArray)  // 如果是数组,则在数据开头写入数组长度,以便反序列化
                {
                    Array array = obj as Array;

                    // x86使用最大长度为Int32.MaxValue的数组
                    this.stream_.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));

                    //Debug.Log($"ToBytesConvert.Action() 数据开头写入数组长度:{array.Length}  {ByteArrayToReadableString(BitConverter.GetBytes(array.Length))}");

                    return false;
                }

                if (buffer == null)
                    return false;

                //if (type == typeof(Vector2))
                //{
                //    Debug.Log($"ToBytesConvert.Action() {(Vector2)obj} IsLittleEndian:{BitConverter.IsLittleEndian} Vector2ToBytes: {ByteArrayToReadableString(buffer)} (0x{stream_.Position})");
                //}

                if (buffer.Length > 0)
                    this.stream_.Write(buffer, 0, buffer.Length);

                return true;
            }
        }

        private class ToObjectConvert
        {
            private byte[] bytes_;

            public ToObjectConvert(byte[] bytes)
            {
                this.bytes_ = bytes;
            }

            public int offset { get; private set; }

            public bool Action(Converter.Accessor accessor, object[] attributes)
            {
                Type type = accessor.type;
                if (type == typeof(bool))
                {
                    accessor.obj = BitConverter.ToBoolean(this.bytes_, this.offset);
                    ++this.offset;
                }
                else if (type == typeof(char))
                {
                    accessor.obj = BitConverter.ToChar(this.bytes_, this.offset);
                    this.offset += 2;
                }
                else if (type == typeof(byte) || type == typeof(sbyte))
                {
                    accessor.obj = this.bytes_[this.offset];
                    ++this.offset;
                }
                else if (type == typeof(short))
                {
                    accessor.obj = BitConverter.ToInt16(this.bytes_, this.offset);
                    this.offset += 2;
                }
                else if (type == typeof(int))
                {
                    accessor.obj = BitConverter.ToInt32(this.bytes_, this.offset);
                    this.offset += 4;
                }
                else if (type == typeof(long))
                {
                    accessor.obj = BitConverter.ToInt64(this.bytes_, this.offset);
                    this.offset += 8;
                }
                else if (type == typeof(double))
                {
                    accessor.obj = BitConverter.ToDouble(this.bytes_, this.offset);
                    this.offset += 8;
                }
                else if (type == typeof(float))
                {
                    accessor.obj = BitConverter.ToSingle(this.bytes_, this.offset);
                    this.offset += 4;
                }
                else if (type == typeof(ushort))
                {
                    accessor.obj = BitConverter.ToUInt16(this.bytes_, this.offset);
                    this.offset += 2;
                }
                else if (type == typeof(uint))
                {
                    accessor.obj = BitConverter.ToUInt32(this.bytes_, this.offset);
                    this.offset += 4;
                }
                else if (type == typeof(ulong))
                {
                    accessor.obj = BitConverter.ToUInt64(this.bytes_, this.offset);
                    this.offset += 8;
                }
#if UNITY_ENGINE
                else if (type == typeof(Vector2))
                {
                    accessor.obj = BytesToVector2(this.bytes_, this.offset);

                    //if (type == typeof(Vector2)) {
                    //    byte[] ar = new byte[sizeof(float) * 2];
                    //    Array.Copy(bytes_, offset, ar, 0, sizeof(float) * 2);
                    //    Debug.Log($"ToObjectConvert.Action() {(Vector2)accessor.obj} IsLittleEndian:{BitConverter.IsLittleEndian} Vector2ToBytes: {ByteArrayToReadableString(ar)} (0x{offset})");
                    //}

                    this.offset += sizeof(float) * 2;
                }
                else if (type == typeof(Vector3))
                {
                    accessor.obj = BytesToVector3(this.bytes_, this.offset);
                    this.offset += sizeof(float) * 3;
                }
                else if (type == typeof(Vector4))
                {
                    accessor.obj = BytesToVector4(this.bytes_, this.offset);
                    this.offset += sizeof(float) * 4;
                }
                else if (type == typeof(Quaternion))
                {
                    accessor.obj = BytesToQuaternion(this.bytes_, this.offset);
                    this.offset += sizeof(float) * 4;
                }
                else if (type == typeof(Color))
                {
                    accessor.obj = BytesToColor(this.bytes_, this.offset);
                    this.offset += sizeof(float) * 3;
                }
#endif
                else if (type == typeof(string))
                {
                    FixedStringAttribute fixedStringAttribute = null;
                    foreach (object attribute in attributes)
                    {
                        if (attribute is FixedStringAttribute)
                        {
                            fixedStringAttribute = attribute as FixedStringAttribute;
                            break;
                        }
                    }
                    string str;
                    if (fixedStringAttribute != null)
                    {
                        int length = fixedStringAttribute.length;
                        char[] chArray = new char[length];
                        int index1 = 0;
                        while (index1 < length)
                        {
                            chArray[index1] = BitConverter.ToChar(this.bytes_, this.offset);
                            ++index1;
                            this.offset += 2;
                        }
                        for (int index2 = 0; index2 < length; ++index2)
                        {
                            if (chArray[index2] == char.MinValue)
                            {
                                length = index2;
                                break;
                            }
                        }
                        str = new string(chArray, 0, length);
                    }
                    else
                    {
                        int int32 = BitConverter.ToInt32(this.bytes_, this.offset);
                        this.offset += 4;
                        char[] chArray = new char[int32];
                        int index = 0;
                        while (index < int32)
                        {
                            chArray[index] = BitConverter.ToChar(this.bytes_, this.offset);
                            ++index;
                            this.offset += 2;
                        }
                        str = new string(chArray);
                    }

                    accessor.obj = str;
                }
                else if (type.IsArray)
                {
                    Array array = null;
                    Type elementType = type.GetElementType();

                    int arrayLength = BitConverter.ToInt32(this.bytes_, this.offset);
                    this.offset += sizeof(int);

                    if (elementType == typeof(bool))
                        array = new bool[arrayLength];
                    else if (elementType == typeof(char))
                        array = new char[arrayLength];
                    else if (elementType == typeof(byte))
                        array = new byte[arrayLength];
                    else if (elementType == typeof(sbyte))
                        array = new sbyte[arrayLength];
                    else if (elementType == typeof(short))
                        array = new short[arrayLength];
                    else if (elementType == typeof(int))
                        array = new int[arrayLength];
                    else if (elementType == typeof(long))
                        array = new long[arrayLength];
                    else if (elementType == typeof(double))
                        array = new double[arrayLength];
                    else if (elementType == typeof(float))
                        array = new float[arrayLength];
                    else if (elementType == typeof(ushort))
                        array = new ushort[arrayLength];
                    else if (elementType == typeof(uint))
                        array = new uint[arrayLength];
                    else if (elementType == typeof(ulong))
                        array = new ulong[arrayLength];
                    else if (elementType == typeof(string) || elementType == typeof(System.String))
                        array = new string[arrayLength];
#if UNITY_ENGINE
                    else if (elementType == typeof(Vector2))
                        array = new Vector2[arrayLength];
                    else if (elementType == typeof(Vector3))
                        array = new Vector3[arrayLength];
                    else if (elementType == typeof(Vector4))
                        array = new Vector4[arrayLength];
                    else if (elementType == typeof(Quaternion))
                        array = new Quaternion[arrayLength];
                    else if (elementType == typeof(Color))
                        array = new Color[arrayLength];
#endif
                    else if (elementType.IsClass)
                    {
                        array = Array.CreateInstance(elementType, arrayLength);

                        try
                        {
                            for (int i = 0; i < arrayLength; ++i)
                            {
                                var elementInstance = elementType.Assembly.CreateInstance(elementType.FullName);
                                //Debug.Log($"创建数组元素实例:({i} / {arrayLength}) {elementType} {elementInstance}  elementInstance == null ? {elementInstance == null}");
                                array.SetValue(elementInstance, i);
                            }
                        }
                        catch (Exception e)
                        {
#if UNITY_ENGINE
                            Debug.LogError($"Error on ToObjectConvert.Action(*) Create Custom Class Array Element Instance. {e.Message}");
#endif
                            return false;
                        }
                    }

                    accessor.obj = array;

                    //Debug.Log($"read {accessor.obj}  ArrayLength:{arrayLength} (0x{this.offset})");

                    return false;
                }
                else if (type.IsClass)
                    return false;

                //Debug.Log($"read {accessor.type}: {accessor.obj} (0x{this.offset})");

                return true;
            }
        }

        private delegate bool ValueActionDelegate(Converter.Accessor accessor, object[] attributes);

        private static string ByteArrayToReadableString(byte[] arrInput)
        {
            int i;
            var sOutput = new System.Text.StringBuilder(arrInput.Length);
            for (i = 0; i < arrInput.Length; i++)
            {
                sOutput.Append(arrInput[i].ToString("X2"));
            }
            return sOutput.ToString();
        }

        #region Extension
#if UNITY_ENGINE
        public static byte[] Vector2ToBytes(UnityEngine.Vector2 vect)
        {
            byte[] buff = new byte[sizeof(float) * 2];
            Buffer.BlockCopy(BitConverter.GetBytes(vect.x), 0, buff, 0 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(vect.y), 0, buff, 1 * sizeof(float), sizeof(float));
            return buff;
        }
        public static byte[] Vector3ToBytes(UnityEngine.Vector3 vect)
        {
            byte[] buff = new byte[sizeof(float) * 3];
            Buffer.BlockCopy(BitConverter.GetBytes(vect.x), 0, buff, 0 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(vect.y), 0, buff, 1 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(vect.z), 0, buff, 3 * sizeof(float), sizeof(float));
            return buff;
        }
        public static byte[] Vector4ToBytes(UnityEngine.Vector4 vect)
        {
            byte[] buff = new byte[sizeof(float) * 4];
            Buffer.BlockCopy(BitConverter.GetBytes(vect.x), 0, buff, 0 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(vect.y), 0, buff, 1 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(vect.z), 0, buff, 2 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(vect.w), 0, buff, 3 * sizeof(float), sizeof(float));
            return buff;
        }
        public static byte[] QuaternionToBytes(UnityEngine.Quaternion quat)
        {
            byte[] buff = new byte[sizeof(float) * 4];
            Buffer.BlockCopy(BitConverter.GetBytes(quat.x), 0, buff, 0 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(quat.y), 0, buff, 1 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(quat.z), 0, buff, 2 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(quat.w), 0, buff, 3 * sizeof(float), sizeof(float));
            return buff;
        }
        public static byte[] ColorToBytes(UnityEngine.Color color)
        {
            byte[] buff = new byte[sizeof(float) * 4];
            Buffer.BlockCopy(BitConverter.GetBytes(color.r), 0, buff, 0 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(color.g), 0, buff, 1 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(color.b), 0, buff, 2 * sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(color.a), 0, buff, 3 * sizeof(float), sizeof(float));
            return buff;
        }
        public static UnityEngine.Vector2 BytesToVector2(byte[] buff)
        {
            return BytesToVector2(buff, 0);
        }
        public static UnityEngine.Vector2 BytesToVector2(byte[] buff, int startIndex)
        {
            if (buff == null)
                return default(UnityEngine.Vector2);
            UnityEngine.Vector2 vect = UnityEngine.Vector2.zero;
            vect.x = BitConverter.ToSingle(buff, startIndex + 0 * sizeof(float));
            vect.y = BitConverter.ToSingle(buff, startIndex + 1 * sizeof(float));
            return vect;
        }
        public static UnityEngine.Vector3 BytesToVector3(byte[] buff)
        {
            return BytesToVector3(buff, 0);
        }
        public static UnityEngine.Vector3 BytesToVector3(byte[] buff, int startIndex)
        {
            if (buff == null)
                return default(UnityEngine.Vector3);
            UnityEngine.Vector3 vect = UnityEngine.Vector3.zero;
            vect.x = BitConverter.ToSingle(buff, startIndex + 0 * sizeof(float));
            vect.y = BitConverter.ToSingle(buff, startIndex + 1 * sizeof(float));
            vect.z = BitConverter.ToSingle(buff, startIndex + 2 * sizeof(float));
            return vect;
        }
        public static UnityEngine.Vector4 BytesToVector4(byte[] buff)
        {
            return BytesToVector4(buff, 0);
        }
        public static UnityEngine.Vector4 BytesToVector4(byte[] buff, int startIndex)
        {
            if (buff == null)
                return default(UnityEngine.Vector4);
            UnityEngine.Vector4 vect = UnityEngine.Vector4.zero;
            vect.x = BitConverter.ToSingle(buff, startIndex + 0 * sizeof(float));
            vect.y = BitConverter.ToSingle(buff, startIndex + 1 * sizeof(float));
            vect.z = BitConverter.ToSingle(buff, startIndex + 2 * sizeof(float));
            vect.w = BitConverter.ToSingle(buff, startIndex + 3 * sizeof(float));
            return vect;
        }
        public static UnityEngine.Quaternion BytesToQuaternion(byte[] buff)
        {
            return BytesToQuaternion(buff, 0);
        }
        public static UnityEngine.Quaternion BytesToQuaternion(byte[] buff, int startIndex)
        {
            if (buff == null)
                return default(UnityEngine.Quaternion);
            UnityEngine.Quaternion quat = UnityEngine.Quaternion.identity;
            quat.x = BitConverter.ToSingle(buff, startIndex + 0 * sizeof(float));
            quat.y = BitConverter.ToSingle(buff, startIndex + 1 * sizeof(float));
            quat.z = BitConverter.ToSingle(buff, startIndex + 2 * sizeof(float));
            quat.w = BitConverter.ToSingle(buff, startIndex + 3 * sizeof(float));
            return quat;
        }
        public static UnityEngine.Color BytesToColor(byte[] buff)
        {
            return BytesToColor(buff, 0);
        }
        public static UnityEngine.Color BytesToColor(byte[] buff, int startIndex)
        {
            if (buff == null)
                return default(UnityEngine.Color);
            UnityEngine.Color color = UnityEngine.Color.white;
            color.r = BitConverter.ToSingle(buff, startIndex + 0 * sizeof(float));
            color.g = BitConverter.ToSingle(buff, startIndex + 1 * sizeof(float));
            color.b = BitConverter.ToSingle(buff, startIndex + 2 * sizeof(float));
            color.a = BitConverter.ToSingle(buff, startIndex + 3 * sizeof(float));
            return color;
        }
#endif
        #endregion
    }
}
