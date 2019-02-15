// PSV Corpse Party
// Type: ObjectConvert.Converter
#define UNITY_ENGINE

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
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
            if (bytes == null)
                return;

            if (obj == null)
            {
                obj = (T)typeof(T).Assembly.CreateInstance(typeof(T).FullName);
            }

            Converter.ToObjectConvert toObjectConvert = new Converter.ToObjectConvert(bytes);
            Converter.ValueAction(toObjectConvert.Action, new Converter.ObjectDummyAccessor(obj), null);
        }


        private delegate bool ValueActionDelegate(Converter.Accessor accessor, object[] attributes);

        private static void ValueAction(Converter.ValueActionDelegate action, Converter.Accessor accessor, object[] attributes)
        {
            // 普通字段
            if (action(accessor, attributes))
                return;
            
            // 数组字段、自定义类字段
            Type type = accessor.type;
            if (type.IsArray)
            {
                if (accessor.obj == null)
                    return;
                Converter.ArrayEnumeration(action, accessor, attributes);
            }
            else if (type.IsGenericType) // 泛型类序列化支持
            {
                if (accessor.obj == null)
                    return;
                if (type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>)) // List类型序列化
                {
                    Converter.ListEnumeration(action, accessor, attributes);
                }
                else if (type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.Dictionary<,>)) // 字典类型序列化
                {
                    Converter.DictionaryEnumeration(action, accessor, attributes);
                }
            }
            else
            {
                if (!type.IsClass) // 不支持struct
                    return;
                Converter.FieldEnumeration(action, accessor);
            }
        }

        #region Element Enumeration
        /// <summary>
        /// 类字段值枚举
        /// </summary>
        /// <param name="action"></param>
        /// <param name="accessor"></param>
        private static void FieldEnumeration(Converter.ValueActionDelegate action, Converter.Accessor accessor)
        {
            Type type = accessor.type;

            Converter.FieldAccessor fieldAccessor = new Converter.FieldAccessor(accessor.obj);

            //Debug.Log($"FieldEnumeration 开始枚举{type.Name}类的字段 -->");

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!(Attribute.GetCustomAttribute(field, typeof(NotConvertAttribute)) is NotConvertAttribute))
                {
                    fieldAccessor.SetFieldInfo(field);

                    //Debug.Log($"   ->   <color=aqua>{type.Name}::{field.Name}</color>");
                    Converter.ValueAction(action, fieldAccessor, field.GetCustomAttributes(false));
                    //Debug.Log($"      <color=lime>{type.Name}::{field.Name} ({field.FieldType}) = {field.GetValue(accessor.obj)}   <-</color>");
                    //Debug.Log(string.Empty);
                }
            }
        }

        private static void PropertyEnumeration(Converter.ValueActionDelegate action, Converter.Accessor accessor)
        {
            Type type = accessor.type;

            Converter.PropertyAccessor propertyAccessor = new Converter.PropertyAccessor(accessor.obj);

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!(Attribute.GetCustomAttribute(property, typeof(NotConvertAttribute)) is NotConvertAttribute))
                {
                    propertyAccessor.SetPropertyInfo(property);
                    Converter.ValueAction(action, propertyAccessor, property.GetCustomAttributes(false));
                }
            }
        }

        private static void ArrayEnumeration(Converter.ValueActionDelegate action, Converter.Accessor accessor, object[] attributes)
        {
            Converter.ArrayAccessor arrayAccessor = new Converter.ArrayAccessor(accessor.obj as Array);
            Converter.ArrayEnumeration(action, arrayAccessor, 0, attributes);
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

        private static void ListEnumeration(Converter.ValueActionDelegate action, Converter.Accessor accessor, object[] attributes)
        {
            Converter.ListAccessor listAccessor = new Converter.ListAccessor(accessor.obj);

            PropertyInfo countProperty = accessor.obj.GetType().GetProperty("Count");

            for (int index = 0; index < (int)countProperty.GetValue(accessor.obj, null); ++index)
            {
                listAccessor.index = index;
                Converter.ValueAction(action, listAccessor, attributes);
            }
        }

        private static void DictionaryEnumeration(Converter.ValueActionDelegate action, Converter.Accessor accessor, object[] attributes)
        {
            Converter.DictionaryAccessor dictionaryAccessor = new Converter.DictionaryAccessor(accessor.obj);
            //Converter.ObjectAccessor dictionaryCountAccessor = new Converter.ObjectAccessor(dictionaryAccessor.count);

            if (action(dictionaryAccessor, attributes))
                return;

            // 序列化
            //if (dictionaryAccessor.dictionary.Count == (int) dictionaryCountAccessor.obj)
            if (!dictionaryAccessor.isDeserialization)
            {
                //Debug.Log($"<color=yellow>字典成员枚举：序列化模式</color>");

                foreach (var kvp in dictionaryAccessor.dictionary)
                {
                    var key = kvp.GetType().GetProperty("Key");
                    var value = kvp.GetType().GetProperty("Value");

                    dictionaryAccessor.key = key.GetValue(kvp, null);
                    dictionaryAccessor.value = value.GetValue(kvp, null);
                
                    if (dictionaryAccessor.key == null)
                        continue;

                    //Console.WriteLine($"序列化kv:{dictionaryAccessor.key} {dictionaryAccessor.value}");

                    var keyAccessor = new ObjectAccessor(dictionaryAccessor.key);
                    var valueAccessor = new ObjectAccessor(dictionaryAccessor.value);
                    keyAccessor.SetType(dictionaryAccessor.keyType);
                    valueAccessor.SetType(dictionaryAccessor.valueType);

                    Converter.ValueAction(action, keyAccessor, attributes);
                    Converter.ValueAction(action, valueAccessor, attributes);
                }
            }
            else // 反序列化
            {
                //Debug.Log($"<color=yellow>字典成员枚举：反序列化模式</color>");
                //for (var i = 0; i < (int) dictionaryCountAccessor.obj; ++i)
                for (var i = 0; i < dictionaryAccessor.count; ++i)
                {
                    var keyAccessor = new ObjectAccessor(null);
                    var valueAccessor = new ObjectAccessor(null);
                    keyAccessor.SetType(dictionaryAccessor.keyType);
                    valueAccessor.SetType(dictionaryAccessor.valueType);

                    //Console.WriteLine($"准备反序列化kv类型:{keyAccessor.type} {valueAccessor.type}");

                    Converter.ValueAction(action, keyAccessor, attributes);
                    //Console.WriteLine($"反序列化key:{keyAccessor.obj}");
                    Converter.ValueAction(action, valueAccessor, attributes);
                    //Console.WriteLine($"反序列化value:{valueAccessor.obj}");

                    if (keyAccessor.obj == null)
                        continue;
                    
                    dictionaryAccessor.dictionary.Add(keyAccessor.obj, valueAccessor.obj);
                }
            }
        }
        #endregion

        #region Accessor
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

            public override Type type => this.obj.GetType();
        }
        
        private class ObjectAccessor : Accessor
        {
            private object obj_;
            private Type type_;
            public ObjectAccessor(object obj)
            {
                this.obj_ = obj;
                this.type_ = obj?.GetType();
            }

            public override object obj { get { return obj_; } set { obj_ = value; } }

            public override Type type => this.type_;
            
            public void SetType(Type type)
            {
                this.type_ = type;
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

            public override Type type => this.field_info_.FieldType;

            public void SetFieldInfo(FieldInfo field_info)
            {
                this.field_info_ = field_info;
            }
        }

        private class PropertyAccessor : Accessor
        {
            private object obj_;
            private PropertyInfo prooperty_info_;

            public PropertyAccessor(object obj)
            {
                this.obj_ = obj;
            }

            public override object obj
            {
                get
                {
                    return this.prooperty_info_.GetValue(this.obj_);
                }
                set
                {
                    this.prooperty_info_.SetValue(this.obj_, value);
                }
            }

            public override Type type => this.prooperty_info_.PropertyType;

            public void SetPropertyInfo(PropertyInfo property_info)
            {
                this.prooperty_info_ = property_info;
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

        private class ListAccessor : Accessor
        {
            private Type type_;

            public ListAccessor(object list)
            {
                this.list = list;
                this.index = 0;
                this.type_ = list.GetType().GetGenericArguments()[0];
            }

            public override object obj
            {
                get
                {
                    return list.GetType().GetMethod("get_Item").Invoke(list, new object[] { this.index });
                }
                set
                {
                    list.GetType().GetMethod("set_Item").Invoke(list, new object[] { this.index, value });
                }
            }   

            public override Type type { get { return type_; } }

            public object list { get; private set; }

            public int index { get; set; }
        }

        private class DictionaryAccessor : Accessor
        {
            private Type keyType_;
            private Type valueType_;

            public DictionaryAccessor(object dictionary)
            {
                this.dictionary = (IDictionary)dictionary;
                this.key = null;
                this.keyType_ = dictionary.GetType().GetGenericArguments()[0];
                this.valueType_ = dictionary.GetType().GetGenericArguments()[1];
                this.count = this.dictionary.Keys.Count;
                //this.count = this.dictionary != null ? this.dictionary.Keys.Count : 0;
            }

            public override object obj
            {
                get
                {
                    //return dictionary.GetType().GetMethod("get_Item").Invoke(dictionary, new object[] { this.key });
                    //return dictionary[key];
                    //return kvp;
                    return dictionary;
                }
                set
                {
                    //dictionary.GetType().GetMethod("set_Item").Invoke(dictionary, new object[] { this.key, value });
                    //dictionary[key] = value;
                    //kvp = value;
                    dictionary = (IDictionary)value;
                }
            }

            public override Type type { get { return dictionary.GetType(); } }

            public Type keyType => keyType_;
            public Type valueType => valueType_;

            public IDictionary dictionary { get; private set; }

            public int count { get; set; }

            public object key { get; set; }
            public object value { get; set; }

            public bool isDeserialization { get; set; }
        }
        #endregion

        private class ToBytesConvert
        {
            private MemoryStream stream_ = new MemoryStream();

            public byte[] bytes => this.stream_.ToArray();

            public bool Action(Converter.Accessor accessor, object[] attributes)
            {
                //Debug.Log($"<color=yellow>0x{this.stream_.Position}</color> write: {accessor.obj} {accessor.type}");
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
                else if (type == typeof(Vector2Int))
                    buffer = Vector2IntToBytes((Vector2Int)obj);
                else if (type == typeof(Vector3))
                    buffer = Vector3ToBytes((Vector3)obj);
                else if (type == typeof(Vector3Int))
                    buffer = Vector3IntToBytes((Vector3Int)obj);
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
                else if (type.IsArray)  // 如果是数组,则在数组数据开头写入数组长度,以便反序列化
                {
                    Array array = obj as Array;

                    // 写入Null/NotNull标志
                    bool classIsNull = obj == null;

                    this.stream_.Write(BitConverter.GetBytes(!classIsNull), 0, sizeof(bool));

                    // 写入数组长度
                    if (array == null)
                    {
                        this.stream_.Write(BitConverter.GetBytes(0), 0, sizeof(int));
                    }
                    else
                    {
                        this.stream_.Write(BitConverter.GetBytes(array.Length), 0, sizeof(int));
                    }

                    //Debug.Log($"ToBytesConvert.Action() 数据开头写入数组长度:{array.Length}  {ByteArrayToReadableString(BitConverter.GetBytes(array.Length))}");

                    return false;
                }
                else if (type.IsGenericType) // 泛型类序列化支持, TODO: Test
                {
                    if (type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>)) // List类型序列化
                    {
                        System.Collections.IList list = obj as System.Collections.IList;

                        // 写入Null/NotNull标志
                        bool classIsNull = obj == null;

                        this.stream_.Write(BitConverter.GetBytes(!classIsNull), 0, sizeof(bool));

                        // 写入List长度
                        if (list == null)
                        {
                            this.stream_.Write(BitConverter.GetBytes(0), 0, sizeof(int));
                        }
                        else
                        {
                            this.stream_.Write(BitConverter.GetBytes(list.Count), 0, sizeof(int));
                        }

                        return false;
                    }
                    else if (type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.Dictionary<,>)) // 字典类型序列化
                    {
                        //Debug.Log("<color=yellow>ToBytesConvert::IDictionary</color>");

                        if (accessor is FieldAccessor)
                        {
                            // 写入Null/NotNull标志
                            bool classIsNull = obj == null;

                            this.stream_.Write(BitConverter.GetBytes(!classIsNull), 0, sizeof(bool));
                        }
                        else if (accessor is DictionaryAccessor)
                        {
                            System.Collections.IDictionary dictionary = obj as System.Collections.IDictionary;

                            // 字典比较特殊，在此处写入长度的话，在外面无法取到
                            // 写入字典长度
                            if (dictionary == null)
                            {
                                this.stream_.Write(BitConverter.GetBytes(0), 0, sizeof(int));
                            }
                            else
                            {
                                this.stream_.Write(BitConverter.GetBytes(dictionary.Count), 0, sizeof(int));
                            }
                        }

                        return false;
                    }
                }
                else if (type.IsClass)
                {
                    bool classIsNull = obj == null;
                    // 使用bool作为自定义类状态标识符(空/非空)
                    this.stream_.Write(BitConverter.GetBytes(!classIsNull), 0, sizeof(bool));
                    return classIsNull;
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
                //Debug.Log($"<color=yellow>0x{this.offset}</color> start read");
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
                else if (type == typeof(Vector2Int))
                {
                    accessor.obj = BytesToVector2Int(this.bytes_, this.offset);
                    this.offset += sizeof(int) * 2;
                }
                else if (type == typeof(Vector3))
                {
                    accessor.obj = BytesToVector3(this.bytes_, this.offset);
                    this.offset += sizeof(float) * 3;
                }
                else if (type == typeof(Vector3Int))
                {
                    accessor.obj = BytesToVector3Int(this.bytes_, this.offset);
                    this.offset += sizeof(int) * 3;
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
                        int stringLenth = BitConverter.ToInt32(this.bytes_, this.offset);
                        this.offset += 4;
                        char[] chArray = new char[stringLenth];
                        int index = 0;
                        while (index < stringLenth)
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

                    if (this.bytes_ == null || this.bytes_.Length < sizeof(int))
                    {
                        throw new Exception($"尝试反序列化数组时失败: 没找到Array的二进制数值{accessor.obj}");
                    }

                    bool arrayIsNull = !BitConverter.ToBoolean(this.bytes_, this.offset);
                    this.offset++;

                    int arrayLength = BitConverter.ToInt32(this.bytes_, this.offset);
                    this.offset += sizeof(int);

                    // 检查Null/NotNull标志
                    if (arrayIsNull)
                    {
                        accessor.obj = null;
                        return true;
                    }

                    // 数组字段序列化时是空的值
                    if (this.offset >= this.bytes_.Length)
                    {
                        accessor.obj = null;
                        return true;
                    }

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
                    else if (elementType == typeof(Vector2Int))
                        array = new Vector2Int[arrayLength];
                    else if (elementType == typeof(Vector3))
                        array = new Vector3[arrayLength];
                    else if (elementType == typeof(Vector3Int))
                        array = new Vector3Int[arrayLength];
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
                else if (type.IsGenericType) // 泛型类反序列化支持, TODO: Test
                {
                    if (type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>)) // List类型反序列化
                    {
                        if (this.bytes_ == null || this.bytes_.Length < sizeof(byte) + sizeof(int))
                        {
                            throw new Exception($"尝试反序列化数组时失败: 没找到List的二进制数值{accessor.obj}");
                        }

                        bool listIsNull = !BitConverter.ToBoolean(this.bytes_, this.offset);
                        this.offset++;

                        int listCount = BitConverter.ToInt32(this.bytes_, this.offset);
                        this.offset += sizeof(int);

                        // 检查空List标识符
                        if (listIsNull)
                        {
                            accessor.obj = null;
                            return true;
                        }

                        Type elementType = type.GenericTypeArguments[0];
                        var list = Activator.CreateInstance(type.GetGenericTypeDefinition().MakeGenericType(elementType));

                        try
                        {
                            for (int i = 0; i < listCount; ++i)
                            {
                                object elementInstance = null;

                                if (elementType == typeof(string) || elementType == typeof(System.String))
                                    elementInstance = string.Empty;
                                else elementType.Assembly.CreateInstance(elementType.FullName);
                                //Debug.Log($"创建数组元素实例:({i} / {arrayLength}) {elementType} {elementInstance}  elementInstance == null ? {elementInstance == null}");
                                list.GetType().GetMethod("Add").Invoke(list, new[] { elementInstance });
                            }
                        }
                        catch (Exception e)
                        {
#if UNITY_ENGINE
                            Debug.LogError($"Error on ToObjectConvert.Action(*) Create Custom Class List<> Element Instance. {e.Message}");
#endif
                            return false;
                        }

                        accessor.obj = list;

                        return false;
                    }
                    else if (type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.Dictionary<,>)) // 字典类型序列化
                    {
                        //Debug.Log($"<color=yellow>ToObjectConvert::IDictionary</color> {accessor.GetType()}");

                        if (accessor is FieldAccessor)
                        {
                            bool dictionaryIsNull = !BitConverter.ToBoolean(this.bytes_, this.offset);
                            this.offset++;

                            // 检查Null标识符
                            if (dictionaryIsNull)
                            {
                                accessor.obj = null;
                            }
                            else
                            {
                                Type keyType = type.GenericTypeArguments[0];
                                Type valueType = type.GenericTypeArguments[1];

                                accessor.obj = Activator.CreateInstance(type.GetGenericTypeDefinition().MakeGenericType(keyType, valueType));
                            }

                            return dictionaryIsNull;
                        }
                        else if (accessor is DictionaryAccessor)
                        {
                            int dictionaryCount = BitConverter.ToInt32(this.bytes_, this.offset);
                            this.offset += sizeof(int);

                            DictionaryAccessor dictionaryAccessor = accessor as DictionaryAccessor;
                            dictionaryAccessor.isDeserialization = true;
                            dictionaryAccessor.count = dictionaryCount;
                        }

                        return false;
                    }
                }
                else if (type.IsClass)
                {
                    bool classIsNull = !BitConverter.ToBoolean(this.bytes_, this.offset);
                    this.offset++;

                    // 检查空Class标识符
                    if (classIsNull)
                    {
                        accessor.obj = null;
                        return true;
                    }
                    
                    accessor.obj = accessor.type.Assembly.CreateInstance(accessor.type.FullName);
                    return false;
                }

                //Debug.Log($"end read {accessor.type}: {accessor.obj} (0x{this.offset})");

                return true;
            }
        }


        /// <summary>
        /// 将byte数组转为人类可读的16进制字符串
        /// </summary>
        /// <param name="arrInput"></param>
        public static string ByteArrayToReadableString(byte[] arrInput)
        {
            int i;
            var sOutput = new System.Text.StringBuilder(arrInput.Length);
            for (i = 0; i < arrInput.Length; i++)
            {
                sOutput.Append(arrInput[i].ToString("X2"));
            }
            return sOutput.ToString();
        }


        /// <summary>
        /// 将byte数组转为人类可读的16进制字符串(Fast)
        /// </summary>
        /// <param name="barray"></param>
        /// <returns></returns>
        public static string ByteArrayToHex(byte[] barray)
        {
            char[] c = new char[barray.Length * 2];
            byte b;
            for (int i = 0; i < barray.Length; ++i)
            {
                b = ((byte)(barray[i] >> 4));
                c[i * 2] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                b = ((byte)(barray[i] & 0xF));
                c[i * 2 + 1] = (char)(b > 9 ? b + 0x37 : b + 0x30);
            }
            return new string(c);
        }


        /// <summary>
        /// 将人类可读的16进制字符串转为byte数组
        /// </summary>
        /// <param name="hex"></param>
        public static byte[] ReadableStringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
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
        public static byte[] Vector2IntToBytes(UnityEngine.Vector2Int vect)
        {
            byte[] buff = new byte[sizeof(int) * 2];
            Buffer.BlockCopy(BitConverter.GetBytes(vect.x), 0, buff, 0 * sizeof(int), sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(vect.y), 0, buff, 1 * sizeof(int), sizeof(int));
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
        public static byte[] Vector3IntToBytes(UnityEngine.Vector3Int vect)
        {
            byte[] buff = new byte[sizeof(int) * 3];
            Buffer.BlockCopy(BitConverter.GetBytes(vect.x), 0, buff, 0 * sizeof(int), sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(vect.y), 0, buff, 1 * sizeof(int), sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(vect.z), 0, buff, 3 * sizeof(int), sizeof(int));
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
        public static UnityEngine.Vector2Int BytesToVector2Int(byte[] buff, int startIndex)
        {
            if (buff == null)
                return default(UnityEngine.Vector2Int);
            UnityEngine.Vector2Int vect = UnityEngine.Vector2Int.zero;
            vect.x = BitConverter.ToInt32(buff, startIndex + 0 * sizeof(int));
            vect.y = BitConverter.ToInt32(buff, startIndex + 1 * sizeof(int));
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
        public static UnityEngine.Vector3Int BytesToVector3Int(byte[] buff, int startIndex)
        {
            if (buff == null)
                return default(UnityEngine.Vector3Int);
            UnityEngine.Vector3Int vect = UnityEngine.Vector3Int.zero;
            vect.x = BitConverter.ToInt32(buff, startIndex + 0 * sizeof(int));
            vect.y = BitConverter.ToInt32(buff, startIndex + 1 * sizeof(int));
            vect.z = BitConverter.ToInt32(buff, startIndex + 2 * sizeof(int));
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
