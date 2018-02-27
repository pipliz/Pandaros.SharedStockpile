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
        static string CONFIG_PATH = "";
        public static string GAMEDATA_FOLDER = @"";

        static Dictionary<int, int> _totalCount = new Dictionary<int, int>();
        static CombinedStock _stock = null;
        static bool _processing = true;

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "Pandaros.SharedStockpile.OnAssemblyLoaded")]
        public static void OnAssemblyLoaded(string path)
        {
            Log(path);
            GAMEDATA_FOLDER = path.Substring(0, path.IndexOf("gamedata") + "gamedata".Length) + "/";
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterSelectedWorld, "Pandaros.SharedStockpile.AfterSelectedWorld")]
        public static void AfterSelectedWorld()
        {
            CONFIG_PATH = GAMEDATA_FOLDER + "savegames/" + ServerManager.WorldName + "/SharedStockpile.xml";
            
        }

        [ModLoader.ModCallbackAttribute(ModLoader.EModCallbackType.AfterStartup, "Pandaros.SharedStockpile.AfterStartup")]
        public static void AfterStartup()
        {
            Log("Active.");
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterWorldLoad, "Pandaros.SharedStockpile.AfterWorldLoad")]
        public static void AfterWorldLoad()
        {
            Log("World Detected: " + ServerManager.WorldName);
            LoadCounts();
            _processing = false;
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnUpdate, "Pandaros.SharedStockpile.OnUpdate")]
        public static void OnUpdate()
        {
            Process();
        }

        private static void Process()
        {
            try
            {
                if (!_processing && !string.IsNullOrEmpty(ServerManager.WorldName) && Players.CountConnected > 0)
                {
                    _processing = true;

                    SerializableDictionary<ushort, int> counts = new SerializableDictionary<ushort, int>();
                    Dictionary<ushort, int> original = new Dictionary<ushort, int>(_stock.ItemCounts);

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
                                    if (original.ContainsKey(i) && _stock.Players.Contains(player.Name))
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

                        if (!_stock.Players.Contains(player.ID.ToString()))
                            _stock.Players.Add(player.ID.ToString());

                        foreach (var item in counts)
                        {
                            var current = stock.Contains(item.Key) ? stock.AmountContained(item.Key) : 0;

                            if (current != item.Value)
                            {
                                var diffInStock = item.Value - current;

                                if (diffInStock > 0)
                                    stock.Add(item.Key, diffInStock);
                                else
                                    stock.TryRemove(item.Key, diffInStock * -1);
                            }

                        }
                    }

                    _stock.ItemCounts = counts;
                    _processing = false;
                }
            }
            catch (Exception ex)
            {
                ServerLog.LogAsyncExceptionMessage(new Pipliz.LogExceptionMessage("Process Stock", ex));
            }
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerDisconnected, "Pandaros.SharedStockpile.OnPlayerDisconnected")]
        public static void OnPlayerDisconnected(Players.Player p)
        {
            SaveCounts();
        }

        /// <summary>
        ///     Save the _stock to a XML file.
        /// </summary>
        private static void SaveCounts()
        {
            try
            {
                var stringWriter = new StringWriter();

                XmlSerializer xmlserializer = new XmlSerializer(typeof(CombinedStock));
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
                    XmlSerializer xmlserializer = new XmlSerializer(typeof(CombinedStock));
                    using (StreamReader reader = new StreamReader(CONFIG_PATH))
                        _stock = (CombinedStock)xmlserializer.Deserialize(reader);
                }
                else
                    Log("Unable to find existing config file. Creating new file.");

                if (_stock == null)
                {
                    _stock = new CombinedStock();
                    _stock.World = ServerManager.WorldName;
                }
                else
                {
                    Log("Stock Loaded from Config file.");
                    Log("World: " + _stock.World);

                    var players = string.Empty;

                    foreach (var p in _stock.Players)
                        players += " " + p;

                    Log("Indexed Players: " + players);
                    
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
