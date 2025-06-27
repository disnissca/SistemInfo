using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SistemInfo
{
    internal class RamInfo
    {

public static void Ram_Info(string[] args)
    {
        var disksData = new List<Dictionary<string, object>>();
        int slotIndex = 1;

        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
            var results = searcher.Get().Cast<ManagementObject>().ToList();

            // Если WMI-класс вернул 0 объектов — это тоже может быть индикатором
            if (results.Count == 0)
            {
                Console.WriteLine("Не поддерживается");
                return;
            }

            foreach (var mo in results)
            {
                ulong capacityBytes = (ulong)(mo["Capacity"] ?? 0);
                double capacityGB = capacityBytes / 1024.0 / 1024 / 1024;

                var ramModule = new Dictionary<string, object>
                {
                    [$"{{#SLOT}}"] = mo["BankLabel"]?.ToString() ?? "",
                    ["MODEL"] = mo["PartNumber"]?.ToString() ?? "",
                    ["MANUFACTURER"] = mo["Manufacturer"]?.ToString() ?? "",
                    ["SN"] = mo["SerialNumber"]?.ToString() ?? "",
                    ["GB"] = $"{capacityGB:F2}",
                    ["MHZ"] = mo["Speed"]?.ToString() ?? ""
                };

                disksData.Add(ramModule);
                slotIndex++;
            }
        }
        catch (ManagementException)
        {
            Console.WriteLine("Не поддерживается");
            return;
        }
        catch (Exception ex)
        {
            // Любая другая непредвиденная ошибка
            Console.WriteLine($"Ошибка: {ex.Message}");
            return;
        }

        // ==== Дальше работает старая логика ====
        if (args.Length == 0)
        {
            var lldData = new { data = disksData };
            string json = JsonSerializer.Serialize(lldData, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            Console.WriteLine(json);
            return;
        }

        if (args.Length >= 2)
        {
            string slotValue = string.Join(" ", args.Take(args.Length - 1));
            string keyArg = args.Last().ToUpper();

            var ramModule = disksData.FirstOrDefault(dict =>
                dict.Any(kv => kv.Key.StartsWith("{#SLOT", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(kv.Value?.ToString(), slotValue, StringComparison.OrdinalIgnoreCase)));

            if (ramModule != null)
            {
                if (ramModule.TryGetValue(keyArg, out var value))
                {
                    Console.WriteLine(value);
                }
                else
                {
                    Console.WriteLine($"Ключ '{keyArg}' не найден в модуле с меткой '{slotValue}'");
                }
            }
            else
            {
                Console.WriteLine($"Слот с меткой '{slotValue}' не найден");
            }
        }
    }



    public static void Ram_Name()
        {
            int i = 0;
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");

            foreach (ManagementObject mo in searcher.Get())
            {
                i++;
                ulong capacityBytes = (ulong)mo["Capacity"];
                double capacityGB = capacityBytes / 1024.0 / 1024 / 1024;
                Console.WriteLine($"SLOT: {mo["BankLabel"]}");
                Console.WriteLine($"MODEL: {mo["PartNumber"]}");

            }
            i = 0;
        }
        //public static void Ram_Present()
        //{
        //    int i = 0;
        //    var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");

        //    foreach (ManagementObject mo in searcher.Get())
        //    {
        //        i++;
        //        ulong capacityBytes = (ulong)mo["Capacity"];
        //        double capacityGB = capacityBytes / 1024.0 / 1024 / 1024;
        //        Console.WriteLine($"Slot{i}: {mo["BankLabel"]}");
        //        Console.WriteLine($"Model: {mo["PartNumber"]}");

        //    }
        //    i = 0;
        //}
    }
}
