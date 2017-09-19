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
        const string CONFIG_PATH = "gamedata/mods/Pandaros.SharedStockpile/config.xml";

        static XmlSerializer _xmlserializer = new XmlSerializer(typeof(CombinedStock));
        static Dictionary<int, int> _totalCount = new Dictionary<int, int>();
        static CombinedStock _stock = new CombinedStock();
        static bool _processing = false;

        [ModLoader.ModCallbackAttribute(ModLoader.EModCallbackType.AfterStartup, "AfterStartup"), ModLoader.ModCallbackDependsOnAttribute("colonyapi.initialise")]
        public static void AfterStartup()
        {
            LoadCounts();
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnFixedUpdate, "FixedUpdate")]
        public static void FixedUpdate()
        {
            Process();
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerMoved, "OnPlayerMoved")]
        public static void OnPlayerMoved(Players.Player p)
        {
            Process();
        }

        private static void Process()
        {
            try
            {
                if (!_processing && Players.CountConnected > 1)
                {
                    _processing = true;

                    if (_stock == null)
                        _stock = new CombinedStock();

                    Dictionary<ushort, int> counts = new Dictionary<ushort, int>();
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

                        if (!_stock.Players.Contains(player.Name))
                            _stock.Players.Add(player.Name);

                        foreach (var item in counts)
                        {
                            var current = stock.Contains(item.Key) ? stock.AmountContained(item.Key) : 0;

                            if (current != item.Value)
                            {
                                var diffInStock = current - item.Value;

                                if (diffInStock > 0)
                                    stock.Add(item.Key, diffInStock);
                                else
                                    stock.Remove(item.Key, diffInStock * -1);
                            }
             
                        }
                    }

                    _stock.ItemCounts = counts;

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
                var xmlserializer = new XmlSerializer(typeof(CombinedStock));
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
                    _stock = (CombinedStock)_xmlserializer.Deserialize(reader);
            }
            catch (Exception ex)
            {
                ServerLog.LogAsyncExceptionMessage(new Pipliz.LogExceptionMessage("LoadCounts", ex));
            }
        }
    }

    [Serializable]
    public class CombinedStock
    {
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
