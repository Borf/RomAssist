using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.Models;
public enum Server
{
    Unknown = 0,
    EU = 0x1000,
    EUEL,
    NA = 0x2000,
    NAEL,
    NADP,
    SEA = 0x4000,
    SEAEL,
    SEAMP,
    SEAMOF,
    SEAVG,
    SEAPC,
}

public static class ServerHelper
{
    public static bool IsServer(this Server server) => ((int)server & 0xfff) != 0;
    public static string FullString(this Server server) => server switch
    {
        Server.EU => "EU",
        Server.EUEL => "EU Eternal Love",
        Server.NA => "Global",
        Server.NAEL => "Global Eternal Love",
        Server.NADP => "Global Destiny's Promise",
        Server.SEA => "SEA",
        Server.SEAEL => "SEA Eternal Love",
        Server.SEAMP => "SEA Midnight Party",
        Server.SEAMOF => "SEA Memory of Faith",
        Server.SEAVG => "SEA Valhalla Glory",
        Server.SEAPC => "SEA Port City",
        _ => throw new Exception("You did something wrong"),
    };

    public static Server BaseServer(this Server server) => (Server)((int)server & 0xff000);
}