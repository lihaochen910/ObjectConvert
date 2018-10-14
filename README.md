# ObjectConvert

**ObjectConvert** 是一个轻量级的序列化/反序列化的库.

### Features
* Fewer class files
* Faster serialization/deserialization speed
* Support for custom class, supporting nested classes in class
* Support Unity 5.0+

### Unsupport Features
* Struct
* Custom class with parameter constructor

### Usage
Code for Serialization:
```csharp
using ObjectConvert;

var classInstance = new yourCustomClass();

byte[] binaryData = Converter.ToBytes(classInstance);
```

Code for Deserialization:
```csharp
using ObjectConvert;

var classInstance = new yourCustomClass();

byte[] binaryDataFromSomewhere;

Converter.ToObject(ref classInstance, binaryDataFromSomewhere);
```

I use these codes in my game:
```csharp
using ObjectConvert;
using System.IO;

public class SaveData
{
    public float PlayTime { get; set; }
    public byte Level { get; set; }
    public uint Exp { get; set; }
    public byte[] SkillTreePoints { get; set; }
}

string SAVEDATA_PATH = $"{UnityEngine.Application.dataPath} + "/../SaveData.bin"";

var saveData = new SaveData();
saveData.PlayTime = 0xFF;
saveData.Level = 1;
saveData.Exp = 1000;
saveData.SkillTreePoints = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };

byte[] binaryData = Converter.ToBytes(saveData);

File.WriteAllBytes($"{SAVEDATA_PATH}", binaryData);
```

### Note
这段代码源自Psvita游戏:《尸体派对：驭血》
我在此基础上添加了一维数组的序列化/反序列化功能,<br>
多维数组当前无法被反序列化,<br>
请提交代码帮助我改进.