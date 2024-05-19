using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant;

public static class Shuffler
{
    private static Random rng = new Random();

    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }


    public static List<RaffleEntry> Raffle(this IList<RaffleEntry> list)
    {
        List<RaffleEntry> raffled = new List<RaffleEntry>();
        var list2 = new List<RaffleEntry>(list);
        list.Shuffle();
        int count = 0;
        foreach(var entry in list2)
            count += Math.Min(3, entry.Entries.Count);

        while(list2.Count > 0 && count > 0)
        {
            int value = rng.Next(count);
            foreach (var entry in list2)
            {
                value -= Math.Min(3, entry.Entries.Count);
                if(value <= 0 && entry.Entries.Count > 0)
                {
                    raffled.Add(entry);
                    list2.Remove(entry);
                    count -= Math.Min(3, entry.Entries.Count);
                    break;
                }
            }
        }
        return raffled;
    }
}
