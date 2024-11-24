using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json.Linq;
using RomAssistant.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.db;
public class Raffle
{
    [Key]
    public int Id { get; set; }
    public ulong DiscordServerId { get; set; }
    public ulong DiscordChannelid { get; set; }
    public ulong DiscordMessageId { get; set; }
    public RaffleType Type { get; set; }
    public string AnswerRegex { get; set; } = string.Empty;
    [NotMapped]
    public StringTionary<string, int> RaffleCount { get; set; } = new();
    public string Counts { get => RaffleCount.Storage; set { RaffleCount.Storage = value; } }
    public bool Opened { get; set; } = true;
}

public class StringTionary<Key, Value> : IDictionary<Key, Value> where Key : notnull
{
    public string Storage { get; set; } = string.Empty;

    private Dictionary<Key, Value> BuildDict() => Storage
        .Split("|")
        .Select(x => x.Split(":"))
        .ToDictionary(
                k => (Key)Convert.ChangeType(k[0], typeof(Key)), 
                val => (Value)Convert.ChangeType(val.Length > 1 ? val[1] : (default(Value)?.ToString() ?? "") , typeof(Value)));
    private void Save(Dictionary<Key, Value> dict)
    {
        Storage = dict.Select(x => $"{x.Key}:{x.Value}").Aggregate((x, y) => $"{x}|{y}");
    }

    Value IDictionary<Key, Value>.this[Key key] { get => BuildDict()[key]; set { var dict = BuildDict(); dict[key] = value; Save(dict); } }
    public Value this[Key key] { get => BuildDict()[key]; set { var dict = BuildDict(); dict[key] = value; Save(dict); } }

    ICollection<Key> IDictionary<Key, Value>.Keys => BuildDict().Keys;

    ICollection<Value> IDictionary<Key, Value>.Values => BuildDict().Values;

    int ICollection<KeyValuePair<Key, Value>>.Count => BuildDict().Count;

    bool ICollection<KeyValuePair<Key, Value>>.IsReadOnly => false;

    void IDictionary<Key, Value>.Add(Key key, Value value)
    {
        var dict = BuildDict();
        dict.Add(key, value);
        Save(dict);
    }

    void ICollection<KeyValuePair<Key, Value>>.Add(KeyValuePair<Key, Value> item)
    {
        var dict = BuildDict();
        dict.Add(item.Key, item.Value);
        Save(dict);
    }

    void ICollection<KeyValuePair<Key, Value>>.Clear()
    {
        Storage = "";
    }

    bool ICollection<KeyValuePair<Key, Value>>.Contains(KeyValuePair<Key, Value> item)
    {
        return BuildDict().Contains(item);
    }

    bool IDictionary<Key, Value>.ContainsKey(Key key)
    {
        return BuildDict().ContainsKey(key);
    }

    void ICollection<KeyValuePair<Key, Value>>.CopyTo(KeyValuePair<Key, Value>[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    IEnumerator<KeyValuePair<Key, Value>> IEnumerable<KeyValuePair<Key, Value>>.GetEnumerator()
    {
        return BuildDict().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return BuildDict().GetEnumerator();
    }

    bool IDictionary<Key, Value>.Remove(Key key)
    {
        var dict = BuildDict();
        var ret = dict.Remove(key);
        Save(dict);
        return ret;
    }

    bool ICollection<KeyValuePair<Key, Value>>.Remove(KeyValuePair<Key, Value> item)
    {
        var dict = BuildDict();
        var ret = dict.Remove(item.Key);
        Save(dict);
        return ret;
    }

    bool IDictionary<Key, Value>.TryGetValue(Key key, out Value value)
    {
        return BuildDict().TryGetValue(key, out value!);
    }
}

public enum RaffleType
{
    Answer,
    Picture
}