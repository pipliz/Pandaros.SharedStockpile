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
                if (!_processing && !string.IsNullOrEmpty(ServerManager.WorldName))
                {
                    _processing = true;

                    SerializableDictionary<ushort, int> counts = new SerializableDictionary<ushort, int>();
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
                var stringWriter = new StringWriter();

                XmlSerializer xmlserializer = new XmlSerializer(typeof(ConbminedConfig));
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
                Log("Loading from Config file from " + CONFIG_PATH);

                if (File.Exists(CONFIG_PATH))
                {
                    XmlSerializer xmlserializer = new XmlSerializer(typeof(ConbminedConfig));
                    using (StreamReader reader = new StreamReader(CONFIG_PATH))
                        _stock = (ConbminedConfig)xmlserializer.Deserialize(reader);
                }
                else
                    Log("Unable to find existing config file. Creating new file.");

                if (_stock == null)
                {
                    Log("Failed to load stock from config file.");

                    if (_stock == null)
                        _stock = new ConbminedConfig();

                    if (_stock.Stockpiles == null)
                        _stock.Stockpiles = new SerializableDictionary<string, CombinedStock>();
                }
                else
                {
                    Log("Stock Loaded from Config file.");

                    foreach (var pile in _stock.Stockpiles)
                    {
                        Log("World: " + pile.Value.World);

                        var players = string.Empty;

                        foreach (var p in pile.Value.Players)
                            players += " " + p;

                        Log("Indexed Players: " + players);
                    }
                }
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
        [XmlElement]
        public SerializableDictionary<string, CombinedStock> Stockpiles { get; set; }

        public ConbminedConfig()
        {
            Stockpiles = new SerializableDictionary<string, CombinedStock>();
        }
    }

    [Serializable]
    public class CombinedStock
    {
        [XmlElement]
        public string World { get; set; }
        [XmlElement]
        public List<string> Players { get; set; }
        [XmlElement]
        public SerializableDictionary<ushort, int> ItemCounts { get; set; }

        public CombinedStock()
        {
            Players = new List<string>();
            ItemCounts = new SerializableDictionary<ushort, int>();
        }
    }

    [XmlRoot("dictionary")]
    public class SerializableDictionary<TKey, TValue>
        : Dictionary<TKey, TValue>, IXmlSerializable
    {
        #region IXmlSerializable Members
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(System.Xml.XmlReader reader)
        {
            XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
            XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

            bool wasEmpty = reader.IsEmptyElement;
            reader.Read();

            if (wasEmpty)
                return;

            while (reader.NodeType != System.Xml.XmlNodeType.EndElement)
            {
                reader.ReadStartElement("item");

                reader.ReadStartElement("key");
                TKey key = (TKey)keySerializer.Deserialize(reader);
                reader.ReadEndElement();

                reader.ReadStartElement("value");
                TValue value = (TValue)valueSerializer.Deserialize(reader);
                reader.ReadEndElement();

                this.Add(key, value);

                reader.ReadEndElement();
                reader.MoveToContent();
            }
            reader.ReadEndElement();
        }

        public void WriteXml(System.Xml.XmlWriter writer)
        {
            XmlSerializer keySerializer = new XmlSerializer(typeof(TKey));
            XmlSerializer valueSerializer = new XmlSerializer(typeof(TValue));

            foreach (TKey key in this.Keys)
            {
                writer.WriteStartElement("item");

                writer.WriteStartElement("key");
                keySerializer.Serialize(writer, key);
                writer.WriteEndElement();

                writer.WriteStartElement("value");
                TValue value = this[key];
                valueSerializer.Serialize(writer, value);
                writer.WriteEndElement();

                writer.WriteEndElement();
            }
        }
        #endregion
    }
}
