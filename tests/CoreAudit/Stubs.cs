using System;
using System.Collections.Generic;

namespace BepInEx
{
    public static class Paths
    {
        public static string PluginPath { get; set; }
    }
}

namespace UnityEngine
{
    public static class Application
    {
        public static string persistentDataPath { get; set; }
    }

    public static class JsonUtility
    {
        public static T FromJson<T>(string json)
        {
            return XingShangMod.Data.JsonCodec.Deserialize<T>(json);
        }
    }
}

namespace Mortal.Core
{
    public enum MonthStageType
    {
        上旬 = 1,
        中旬 = 2,
        下旬 = 3
    }

    public enum GameStatType
    {
        行動次數,
        銀兩,
        體力,
        輕功,
        內力,
        門派規模
    }

    public sealed class GameStat
    {
        public GameStatType StatType { get; set; }
        public int Value { get; private set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public int FinalValue { get { return Value; } }

        public GameStat(GameStatType type, int value, int min, int max)
        {
            StatType = type;
            Value = value;
            Min = min;
            Max = max;
        }

        public void AddValue(int value, bool totalAdd = true)
        {
            if (StatType == GameStatType.銀兩)
                Value = XingShangMod.Trading.MortalMoneyLimit.Clamp((long)Value + value);
            else
                Value = Math.Max(Min, Math.Min(Max, Value + value));
        }

        public void SetValue(int value)
        {
            Value = StatType == GameStatType.銀兩 ? XingShangMod.Trading.MortalMoneyLimit.Clamp(value) :
                Math.Max(Min, Math.Min(Max, value));
        }
    }

    public sealed class PlayerStatData
    {
        private readonly Dictionary<GameStatType, GameStat> _stats = new Dictionary<GameStatType, GameStat>();

        public void Add(GameStat stat)
        {
            _stats[stat.StatType] = stat;
        }

        public GameStat Get(GameStatType type)
        {
            return _stats[type];
        }
    }

    public sealed class GameTime
    {
        public int Year;
        public int Month;
        public MonthStageType Stage;
    }

    public sealed class PlayerStatManagerData
    {
        public static PlayerStatManagerData Instance { get; set; }
        public PlayerStatData Stats { get; set; }
        public GameTime GameTime { get; set; }
    }

    [Serializable]
    public class GameSave
    {
        public long TimeTick;
        public int CurrentYear;
        public int CurrentMonth;
        public MonthStageType CurrentStage;
        public string RestoreData;
    }

    [Serializable]
    public sealed class AutoGameSave
    {
        public string Slot = string.Empty;
        public GameSave GameSave;
    }
}
