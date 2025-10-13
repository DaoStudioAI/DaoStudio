using System;
using MessagePack;

namespace DaoStudio.DBStorage.Models;



[MessagePackObject]
public class BinaryData
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public int Type { get; set; }

    [Key(2)]
    public byte[] Data { get; set; } = Array.Empty<byte>();
} 