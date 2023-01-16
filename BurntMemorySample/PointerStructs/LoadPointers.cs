using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using BurntMemory;
using System.Reflection;

namespace BurntMemorySample.PointerStructs
{
    public class LoadPointers
    {
        private static string GetIntFromHexString(string m)
        {
            return int.Parse(m, System.Globalization.NumberStyles.HexNumber).ToString();
        }

        public bool Load(string processName, string processVersion, PointerStructs pointerStructs)
        {
            int winStoreFlag = processName.Contains("WinStore") ? 1 : 0; //False if process is the Steam version of the game, true if it's the Windows Store version of the game. Casted to int to use as array index later.
            string pstring = (winStoreFlag == 1) ? "_WinStore" : "_Steam";
            // First, check if the github has a processVersion.json containing the offsets for this version. And try to download it. It's okay if this fails, we might still have a local copy.
            try
            {
                String url = "https://raw.githubusercontent.com/Burnt-o/BurntMemory/master/BurntMemorySample/Pointers/" + processVersion + pstring + ".json";
                System.Net.WebClient client = new System.Net.WebClient();
                String json = client.DownloadString(url);
                System.IO.File.WriteAllText("Pointers\\" + processVersion + ".json", json);
            }
            catch { }


            string pathToPointerFile = System.AppDomain.CurrentDomain.BaseDirectory + "Pointers\\" + processVersion + pstring + ".json";
            if (!File.Exists(pathToPointerFile)) // Return false if file doesn't exist for this version of the game.
            {
                return false;
            }

            LiteralStructs literalStructs = new LiteralStructs();
            // File exists, so we need to unpack the json
            try
            {
                using (StreamReader reader = new StreamReader(pathToPointerFile))
                {
                    string jsonContents = reader.ReadToEnd();
                    reader.Close();

                    ITraceWriter traceWriter = new MemoryTraceWriter();
                    var res = Regex.Replace(jsonContents, @"(?i)\b0x([a-f0-9]+)\b", m => GetIntFromHexString(m.Groups[1].Value));

                    literalStructs = JsonConvert.DeserializeObject<LiteralStructs>(res, new JsonSerializerSettings { TraceWriter = traceWriter, }); //Trace writter outputs errors to console 
                }
            }
            catch
            {
                return false;
            }


            if (literalStructs == null)
            {
                return false;
            }

            Record record = new Record();

            PropertyInfo[] properties = typeof(Record).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                property.SetValue(record, value);
            }


            return PointerStructs.General.LevelName != null;

            
        }

    }
}
