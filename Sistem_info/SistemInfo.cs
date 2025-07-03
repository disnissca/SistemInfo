using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using LibreHardwareMonitor.Hardware;
//using SistemInfo;
using System.Reflection;
namespace SistemInfo
{
    class SistemInfo
    {
        static void Main(string[] args)
        {
            var assembly = Assembly.GetExecutingAssembly();
            Version versio = assembly.GetName().Version;

            if (args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                return;
            }

            string mode = args[0].ToLower();

            var computer = new Computer();
            if (mode == "parts_list")
            {
                computer.IsCpuEnabled = true;
                computer.IsGpuEnabled = true;
                computer.IsStorageEnabled = true;
                computer.IsMotherboardEnabled = true;

            }
            else
            {
                computer.IsCpuEnabled = mode == "cpu";
                computer.IsGpuEnabled = mode == "gpu";
                computer.IsStorageEnabled = /*mode == "disk" ||*/ mode == "hdd" || mode == "ssd";
                computer.IsMotherboardEnabled = mode == "mb" || mode == "mainboard";
            }

            computer.Open();
            if (mode == "cpu" || mode == "gpu" || mode == "mb" || mode == "hdd" || mode == "ssd")
            {
                string attribute = args.Length > 1 ? args[1].ToLower() : null;
                foreach (var hardware in computer.Hardware)
                {
                    hardware.Update();

                    if (!IsMatch(hardware.HardwareType, mode))
                        continue;

                    // Если нет второго аргумента — выводим DEVICE - NAME
                    if (string.IsNullOrEmpty(attribute))
                    {
                        Console.WriteLine($"{hardware.HardwareType} - {hardware.Name}");

                        foreach (var sensor in hardware.Sensors.Where(s => s.Value != null))
                        {
                            Console.WriteLine($"{sensor.SensorType}: {sensor.Name} = {sensor.Value} {GetUnit(sensor)}");
                        }
                    }
                    else
                    {
                        // Есть второй аргумент — ищем строго совпадение по "SensorType: SensorName"
                        var sensor = hardware.Sensors.FirstOrDefault(s =>
                            s.Value != null &&
                            $"{s.SensorType}: {s.Name}".ToLower() == attribute);

                        if (sensor != null)
                            Console.WriteLine(sensor.Value); // Вывод только значения
                        else
                            Console.WriteLine("ATTRIBUTE NOT FOUND");
                    }
                }
            }


            if (mode == "parts_list")
            {

                foreach (var hardware in computer.Hardware)
                {
                    hardware.Update();


                    if (hardware.HardwareType == HardwareType.Memory && hardware.Name == "Generic Memory")
                        continue;

                    Console.WriteLine($"{hardware.HardwareType} - {hardware.Name}");

                    //foreach (var sensor in hardware.Sensors.Where(s => s.Value != null))
                    //{
                    //    Console.WriteLine($"{sensor.SensorType}: {sensor.Name} = {sensor.Value} {GetUnit(sensor)}");
                    //}
                }
            }

            if (args[0] == "part" && args.Length > 1)
            {
                string mod_i = args[1].ToLower(); // 💥 Заменили Substring(5) на args[1]

                computer.IsCpuEnabled = mod_i == "cpu";
                computer.IsGpuEnabled = mod_i == "gpu";
                computer.IsMemoryEnabled = mod_i == "ram";
                computer.IsStorageEnabled = mod_i == "hdd" || mod_i == "ssd";
                computer.IsMotherboardEnabled = mod_i == "mb" || mod_i == "mainboard";

                computer.Open();

                foreach (var hardware in computer.Hardware)
                {
                    hardware.Update();

                    // ⚠ Пропустить Generic Memory
                    if (hardware.HardwareType == HardwareType.Memory && hardware.Name == "Generic Memory")
                        continue;

                    if (!IsMatch(hardware.HardwareType, mod_i))
                        continue;

                    Console.WriteLine($"{hardware.HardwareType} - {hardware.Name}");
                }

                if (mod_i == "ram")
                    RamInfo.Ram_Name();
            }


            if (mode == "ram")
            {
                var ramArgs = args.Skip(1).ToArray();

                if (ramArgs.Length == 0)
                {
                    // Просто "ram" — вывести всю информацию
                    RamInfo.Ram_Info(new string[0]);
                }
                else
                {
                    // Любое количество аргументов — передаем в Ram_Info
                    RamInfo.Ram_Info(ramArgs);
                }
                computer.Close();
                return;
            }

            if (mode == "disk")
            {
                // args: ["disk"] - только команда
                // args: ["disk", "{MODEL}", "{ATTRIBUTE}"] - параметры

                var diskArgs = args.Skip(1).ToArray();

                if (diskArgs.Length == 0)
                {
                    // Просто disk — вывести всю информацию
                    disk_info.disk(new string[0]);
                }
                else if (diskArgs.Length == 2)
                {
                    // disk {MODEL} {ATTRIBUTE}
                    disk_info.disk(diskArgs);
                }
                computer.Close();
                return;
            }
            if (mode == "versio")
                Console.WriteLine(versio);

            computer.Close();
        }

        static void PrintHelp()
        {
            Console.WriteLine("Разделы:");
            Console.WriteLine("parts_list                       - Список комплектующих");
            Console.WriteLine("part DEVICE                      - Название комплектующего");
            Console.WriteLine("cpu                              - Информация о процессоре");
            Console.WriteLine("gpu                              - Температура и загрузка видеокарты");
            Console.WriteLine("hdd                              - Температура и статус накопителей");
            Console.WriteLine("mb                               - Материнская плата и сенсоры (запуск от админа)");
            Console.WriteLine("DEVICE {ATTRIBUTE}               - Результат части");
            Console.WriteLine("ram                              - Детальная информация по каждой планке ОЗУ");
            Console.WriteLine("ram SLOT0, {ATTRIBUTE}           - Вывод параметра по номеру SLOT");
            Console.WriteLine("disk                             - Детальная информация по всем физическим дискам");
            Console.WriteLine("disk DEVICE, {ATTRIBUTE}         - Вывод параметра по номеру DEVICE");
            Console.WriteLine($"versio                           - Версия - ");

        }

        static bool IsMatch(HardwareType type, string mode) => type switch
        {
            HardwareType.Cpu => mode == "cpu",
            HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => mode == "gpu",
            HardwareType.Memory => mode == "ram",
            HardwareType.Storage => mode == "hdd" || mode == "ssd" || mode == "disk",
            HardwareType.Motherboard => mode == "mb" || mode == "mainboard",
            _ => false
        };

        static string GetUnit(ISensor sensor) => sensor.SensorType switch
        {
            SensorType.Temperature => "°C",
            SensorType.Load => "%",
            SensorType.Clock => "MHz",
            SensorType.Power => "W",
            SensorType.Voltage => "V",
            SensorType.Data => "GB",
            SensorType.Fan => "RPM",
            SensorType.Throughput => "MB/s",
            _ => ""
        };
        

    }
    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware)
                subHardware.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }

        public void VisitParameter(IParameter parameter) { }
    }


}
