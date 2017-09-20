using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Pandaros.SharedStockpile
{
    [ModLoader.ModManager]
    public class SharedStockpile
    {
        const string CONFIG_PATH = "gamedata/mods/Pandaros/SharedStockpile/config.xml";

        static XmlSerializer _xmlserializer = new XmlSerializer(typeof(ConbminedConfig));
        static Dictionary<int, int> _totalCount = new Dictionary<int, int>();
        static ConbminedConfig _stock = new ConbminedConfig();
        static bool _processing = true;

        [ModLoader.ModCallbackAttribute(ModLoader.EModCallbackType.AfterStartup, "AfterStartup")]
        public static void AfterStartup()
        {
            Log("Active.");
            LoadCounts();
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterWorldLoad, "AfterWorldLoad")]
        public static void AfterWorldLoad()
        {
            Log("World Detected: " + ServerManager.WorldName);

            if (_stock == null)
                _stock = new ConbminedConfig();

            if (_stock.Stockpiles == null)
                _stock.Stockpiles = new Dictionary<string, CombinedStock>();

            if (!_stock.Stockpiles.ContainsKey(ServerManager.WorldName))
                _stock.Stockpiles.Add(ServerManager.WorldName, new CombinedStock());

            _processing = false;
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnUpdate, "OnUpdate")]
        public static void OnUpdate()
        {
            Process();
        }

        private static void Process()
        {
            try
            {
                if (!_processing && !string.IsNullOrEmpty(ServerManager.WorldName) && Players.CountConnected > 1)
                {
                    _processing = true;

                    if (_stock == null)
                        _stock = new ConbminedConfig();

                    Dictionary<ushort, int> counts = new Dictionary<ushort, int>();
                    Dictionary<ushort, int> original = new Dictionary<ushort, int>(_stock.Stockpiles[ServerManager.WorldName].ItemCounts);

                    for (int p = 0; p < Players.CountConnected; p++)
                    {
                        var player = Players.GetConnectedByIndex(p);
                        var stock = Stockpile.GetStockPile(player);

                        for (ushort i = 0; i < ItemTypes.IndexLookup.MaxRegistered; i++)
                        {
                            if (stock.Contains(i))
                            {
                                var current = stock.AmountContained(i);

                                if (!counts.ContainsKey(i))
                                    counts.Add(i, current);
                                else if (counts[i] != current)
                                {
                                    if (original.ContainsKey(i) && _stock.Stockpiles[ServerManager.WorldName].Players.Contains(player.Name))
                                    {
                                        var diff = original[i] - current;
                                        counts[i] += diff;

                                        if (counts[i] < 0)
                                            counts[i] = 0;
                                    }
                                    else
                                    {
                                        counts[i] += current;
                                    }
                                }
                            }
                        }
                    }

                    for (int p = 0; p < Players.CountConnected; p++)
                    {
                        var player = Players.GetConnectedByIndex(p);
                        var stock = Stockpile.GetStockPile(player);

                        if (!_stock.Stockpiles[ServerManager.WorldName].Players.Contains(player.Name))
                            _stock.Stockpiles[ServerManager.WorldName].Players.Add(player.Name);

                        foreach (var item in counts)
                        {
                            var current = stock.Contains(item.Key) ? stock.AmountContained(item.Key) : 0;

                            if (current != item.Value)
                            {
                                var diffInStock = item.Value - current;

                                if (diffInStock > 0)
                                    stock.Add(item.Key, diffInStock);
                                else
                                    stock.Remove(item.Key, diffInStock * -1);
                            }

                        }
                    }

                    _stock.Stockpiles[ServerManager.WorldName].ItemCounts = counts;

                    SaveCounts();
                    _processing = false;
                }
            }
            catch (Exception ex)
            {
                ServerLog.LogAsyncExceptionMessage(new Pipliz.LogExceptionMessage("Process Stock", ex));
            }
        }

        /// <summary>
        ///     Save the _stock to a XML file.
        /// </summary>
        private static void SaveCounts()
        {
            try
            {
                var xmlserializer = new XmlSerializer(typeof(ConbminedConfig));
                var stringWriter = new StringWriter();

                using (var writer = XmlWriter.Create(stringWriter))
                {
                    xmlserializer.Serialize(writer, _stock);
                    File.WriteAllText(CONFIG_PATH, stringWriter.ToString());
                }
            }
            catch (Exception ex)
            {
                ServerLog.LogAsyncExceptionMessage(new Pipliz.LogExceptionMessage("SaveCounts", ex));
            }
        }

        /// <summary>
        ///     Load the _stock to a XML file.
        /// </summary>
        private static void LoadCounts()
        {
            try
            {
                if (File.Exists(CONFIG_PATH))
                using (StreamReader reader = new StreamReader(CONFIG_PATH))
                    _stock = (ConbminedConfig)_xmlserializer.Deserialize(reader);

                Log("Stock Loaded from Config file.");
            }
            catch (Exception ex)
            {
                ServerLog.LogAsyncExceptionMessage(new Pipliz.LogExceptionMessage("LoadCounts", ex));
            }
        }

        private static void Log(string message)
        {
            ServerLog.LogAsyncMessage(new Pipliz.LogMessage(string.Format("[{0}]<Pandaros => SharedStockpile> {1}", DateTime.Now, message), UnityEngine.LogType.Log));
        }
    }

    [Serializable]
    public class ConbminedConfig
    {
        [XmlIgnore]
        public Dictionary<string, CombinedStock> Stockpiles { get; set; }

        [XmlElement("Stockpiles")]
        public List<KeyValuePair<string, CombinedStock>> XMLStockpilesProxy
        {
            get
            {
                return new List<KeyValuePair<string, CombinedStock>>(Stockpiles);
            }
            set
            {
                Stockpiles = new Dictionary<string, CombinedStock>();

                foreach (var pair in value)
                    Stockpiles[pair.Key] = pair.Value;
            }
        }

        public ConbminedConfig()
        {
            Stockpiles = new Dictionary<string, CombinedStock>();
        }
    }

    [Serializable]
    public class CombinedStock
    {
        [XmlElement]
        public string World { get; set; }
        [XmlElement]
        public List<string> Players { get; set; }

        [XmlElement("ItemCounts")]
        public List<KeyValuePair<ushort, int>> XMLItemCountsProxy
        {
            get
            {
                return new List<KeyValuePair<ushort, int>>(ItemCounts);
            }
            set
            {
                ItemCounts = new Dictionary<ushort, int>();

                foreach (var pair in value)
                    ItemCounts[pair.Key] = pair.Value;
            }
        }

        [XmlIgnore]
        public Dictionary<ushort, int> ItemCounts { get; set; }

        public CombinedStock()
        {
            Players = new List<string>();
            ItemCounts = new Dictionary<ushort, int>();
        }
    }
}
