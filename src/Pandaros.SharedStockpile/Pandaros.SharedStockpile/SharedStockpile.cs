using Pipliz;
using Pipliz.Collections;
using System;

namespace Pandaros.SharedStockpile
{
    [ModLoader.ModManager]
    public class SharedStockpile
    {
        static double _lastUpdate = 0.0;
        static double _cooldown = 1.0;

        static SortedList<ushort, long> _totalCounts = new SortedList<ushort, long>(200);

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, "Pandaros.SharedStockpile.OnAssemblyLoaded")]
        public static void OnAssemblyLoaded(string path)
        {
            Log(path);
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterStartup, "Pandaros.SharedStockpile.AfterStartup")]
        public static void AfterStartup()
        {
            Log("Active.");
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnUpdate, "Pandaros.SharedStockpile.OnUpdate")]
        public static void OnUpdate()
        {
            Process();
        }

        private static void Process()
        {
            if (Time.SecondsSinceStartDouble - _lastUpdate <= _cooldown) {
                return;
            }

            _totalCounts.Clear();

            if (Players.CountConnected <= 1) {
                return;
            }

            for (int p = 0; p < Players.CountConnected; p++) {
                Players.Player player = Players.GetConnectedByIndex(p);
                Stockpile stockpile = Stockpile.GetStockPile(player);

                for (int i = 0; i < stockpile.SpotCount; i++) {
                    InventoryItem item = stockpile.GetByIndex(i);

                    long count;
                    if (_totalCounts.TryGetValue(item.Type, out count)) {
                        count += item.Amount;
                    } else {
                        count = item.Amount;
                    }
                    _totalCounts[item.Type] = count;
                }

                stockpile._items.Clear();
            }

            for (int p = 0; p < Players.CountConnected; p++) {
                Players.Player player = Players.GetConnectedByIndex(p);
                Stockpile stockpile = Stockpile.GetStockPile(player);

                for (int i = 0; i < _totalCounts.Count; i++) {
                    ushort type = _totalCounts.GetKeyAtIndex(i);
                    long totalCount = _totalCounts.GetValueAtIndex(i);

                    int desiredCount = (int)(totalCount / Players.CountConnected);
                    if (p == 0) {
                        // give remainder to first guy in list
                        desiredCount += (int)(totalCount % Players.CountConnected);
                    }

                    stockpile.Add(type, desiredCount);
                }
            }
        }

        private static void Log(string message)
        {
            ServerLog.LogAsyncMessage(new LogMessage(string.Format("[{0}]<Pandaros => SharedStockpile> {1}", DateTime.Now, message), UnityEngine.LogType.Log));
        }
    }
}
