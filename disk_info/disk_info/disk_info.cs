using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

public class disk_info
{
    public static void disk(string[] args)
    {
        //string[] args = new string[0];
        string pathToSmartctl = @"smartctl.exe";
        string outputFilePath = @"disk_info.txt";
        string jsonOutputPath = @"zabbix_lld.json";
        var disksData = new List<Dictionary<string, object>>();

        try
        {
            string scanOutput = RunCommand(pathToSmartctl, "--scan");

            //File.WriteAllText(outputFilePath, "Информация о дисках:\n\n");

            foreach (var line in scanOutput.Split('\n'))
            {
                int g = 0;
                
                if (!line.Contains("/dev/") && !line.Contains("PhysicalDrive")) continue;

                string device = line.Split(' ')[0];
                string type = "auto";

                var match = Regex.Match(line, @"-d\s+(\w+)");
                if (match.Success)
                    type = match.Groups[1].Value.Trim();

                //File.AppendAllText(outputFilePath, $"=== {device} ({type}) ===\n");

                var diskInfo = new Dictionary<string, object>
                {
                    ["{#DEVICE}"] = device.Substring(5),
                    ["TYPE"] = type
                };
                //Console.Write($"{{#DEVICE}}:\"{device}\",");
                //Console.Write($"{{#TYPE}}:\"{type}\",");
                if (type.ToLower() != "nvme")
                {
                    string smartOutput = RunCommand(pathToSmartctl, $"-a {device}");
                    string currentSerial = ExtractField(smartOutput, @"Serial Number:\s+(.*)");

                    if (!disksData.Any(d => d["SERIAL"].ToString() == currentSerial))
                    {
                        ProcessSmartDevice(smartOutput, device, outputFilePath, diskInfo);
                        g = 1;
                    }
                }
                else
                {
                    string nvmeOutput = RunCommand(pathToSmartctl, $"-a -d nvme {device}");
                    string currentSerial = ExtractField(nvmeOutput, @"Serial Number:\s+(.*)");

                    if (!disksData.Any(d => d["SERIAL"].ToString() == currentSerial))
                    {
                        ProcessNvmeDevice(nvmeOutput, device, outputFilePath, diskInfo);
                        g = 1;

                    }
                }
                if (g == 1)
                {
                    disksData.Add(diskInfo);
                }
                //File.AppendAllText(outputFilePath, "\n");
            }

            //// Сохраняем данные в JSON для Zabbix
            //var lldData = new { data = disksData };
            //string json = JsonSerializer.Serialize(lldData, new JsonSerializerOptions
            //{
            //    WriteIndented = true,
            //    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            //});
            //Console.WriteLine(json);
            ////Console.WriteLine(JsonSerializer.Serialize(jsonOutputPath));
            // Если нет параметров - выводим весь JSON для LLD
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
                string modelArg = args[0];
                string keyArg = args[1].ToUpper();  // Ключ в верхнем регистре, чтобы было проще искать

                var disk = disksData.FirstOrDefault(d =>
                    d.TryGetValue("{#DEVICE}", out var modelValue) &&
                    modelValue.ToString().Equals(modelArg, StringComparison.OrdinalIgnoreCase));

                if (disk != null)
                {
                    // Приводим ключ к формату с фигурными скобками и верхним регистром
                    string formattedKey = "" + keyArg + "";

                    if (disk.TryGetValue(formattedKey, out var value))
                    {
                        Console.WriteLine(value);
                    }
                    else
                    {
                        Console.WriteLine();  // Ключ не найден
                    }
                }
                else
                {
                    Console.WriteLine();  // Модель не найдена
                }
                return;
            }





            // Если параметры неполные - выводим весь JSON
            var fullData = new { data = disksData };
            Console.WriteLine(JsonSerializer.Serialize(fullData, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка: " + ex.Message);
            //File.AppendAllText("disk_info.txt", "Ошибка: " + ex.Message + Environment.NewLine);
        }
    }

    static string RunCommand(string exe, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi);
        return process.StandardOutput.ReadToEnd();
    }

    static string ExtractField(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    static void ProcessSmartDevice(string output, string device, string filePath, Dictionary<string, object> diskInfo)
    {
        string model = ExtractField(output, @"Device Model:\s+(.*)") ?? "неизвестно";
        string serial = ExtractField(output, @"Serial Number:\s+(.*)") ?? "неизвестно";
        string wear = ExtractField(output, @"SSD_Life_Left.*?(\d+)\s*$")
    ?? (ExtractField(output, @"170\s+Unknown_Attribute.*?(\d+)\s*$") is string s170 && s170 != ""
        ? (s170.Length <= 2 ? s170 : ExtractField(output, @"Reallocated_Sector_Ct.*?(\d+)\s*$"))
    : ExtractField(output, @"Reallocated_Sector_Ct.*?(\d+)\s*$")
    ?? "неизвестно");
        string Reallocated_Sector_Ct = ExtractField(output, @"Reallocated_Sector_Ct.*?(\d+)\s*$");
        string Reallocated_Event_Count = ExtractField(output, @"Reallocated_Event_Count.*?(\d+)\s*$") ?? "неизвестно";
        string Current_Pending_Sector = ExtractField(output, @"Current_Pending_Sector.*?(\d+)\s*$") ?? "неизвестно";
        string powerOnHours = ExtractField(output, @"Power_On_Hours.*?(\d+)\s*$") ??
                              ExtractField(output, @"Power_On_Hours.*?(\d+)");

        // Добавляем данные для Zabbix
        diskInfo["{#MODEL}"] = model;
        diskInfo["SERIAL"] = serial;
        diskInfo["WEAR"] = wear;
        if (!string.IsNullOrEmpty(Reallocated_Sector_Ct)) diskInfo["Reallocated_Sector_Ct"] = Reallocated_Sector_Ct;
        if (!string.IsNullOrEmpty(Reallocated_Event_Count)) diskInfo["Reallocated_Event_Count"] = Reallocated_Event_Count;
        if (!string.IsNullOrEmpty(Current_Pending_Sector)) diskInfo["Current_Pending_Sector"] = Current_Pending_Sector;
        if (!string.IsNullOrEmpty(powerOnHours)) diskInfo["POWERONHOURS"] = powerOnHours;

        // Можно раскомментировать логгер:
        // File.AppendAllText(filePath, $"Модель: {model}\n");
        // File.AppendAllText(filePath, $"Серийный номер: {serial}\n");
        // File.AppendAllText(filePath, $"Износ (Wear): {wear}\n");
        // File.AppendAllText(filePath, $"Время работы (SMART): {powerOnHours}\n");
    }


    static void ProcessNvmeDevice(string output, string device, string filePath, Dictionary<string, object> diskInfo)
    {
        string model = ExtractField(output, @"Model Number:\s+(.*)") ?? "неизвестно";
        string serial = ExtractField(output, @"Serial Number:\s+(.*)") ?? "неизвестно";
        string powerOnRaw = ExtractField(output, @"Power [Oo]n Hours:\s+([^\r\n]+)") ?? "неизвестно";
        string wear = ExtractField(output, @"Percentage Used:\s+(\d+)");
        if (model == "Viper M.2 VPN100")
            wear = (100 - int.Parse(wear)).ToString();

        powerOnRaw = new string(powerOnRaw.Where(char.IsDigit).ToArray());

        // Добавляем данные для Zabbix
        diskInfo["{#MODEL}"] = model;
        diskInfo["SERIAL"] = serial;
        if (!string.IsNullOrEmpty(wear)) diskInfo["WEAR"] = wear;
        if (!string.IsNullOrEmpty(powerOnRaw)) diskInfo["POWERONHOURS"] = powerOnRaw;
        
        
        // Записываем в текстовый файл
        //File.AppendAllText(filePath, $"Модель: {model}\n");
        //File.AppendAllText(filePath, $"Серийный номер: {serial}\n");
        //File.AppendAllText(filePath, $"Время работы (NVMe): {powerOnRaw ?? "неизвестно"}\n");
        //File.AppendAllText(filePath, $"Износ (Wear): {wear ?? "неизвестно"}\n");
        //Console.Write($",{{#MODEL}}:\"{model}\"");
        //Console.Write($",{{#SERIAL}}:\"{serial}\"");
        //Console.Write($",{{#WEAR}}:\"{powerOnRaw ?? "неизвестно"}\"");
        //Console.Write($",{{#POWERONHOURS}}:\"{wear ?? "неизвестно"}\"");
    }
}