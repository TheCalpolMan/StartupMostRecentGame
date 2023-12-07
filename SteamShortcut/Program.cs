using IWshRuntimeLibrary;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SteamShortcut
{
    public struct data
    {
        public string
            steamid,
            lastGame;

        public Dictionary<string, string> clientIcons;
    }

    internal class Program
    {
        private static char[] badFilenameChars = new char[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

        static void Main(string[] args)
        {
            try
            {
                steamShortcut();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Console.WriteLine("Press enter to exit (this should be saved to \"errorLog.txt\"...");
                Console.ReadLine();

                using (StreamWriter sw = System.IO.File.CreateText("errorLog.txt"))
                {
                    sw.WriteLine(e.Message + "\n\n------------------\n\n" + e.StackTrace);
                }
            }
        }

        static void steamShortcut()
        {
            data localData = getData();
            string iconFolderLocation = System.IO.Directory.GetCurrentDirectory() + "\\icon";

            /*Console.WriteLine("Deleting old shortcut");
            if (System.IO.File.Exists(SteamJSON.SearchJSON<string>(localData, "lastGame")))
                System.IO.File.Delete(SteamJSON.SearchJSON<string>(localData, "lastGame"));
            else
                Console.WriteLine("Old shortcut didn't exist");*/

            Console.WriteLine("Deleting old icon");
            if (Directory.Exists(iconFolderLocation))
            {
                foreach (string file in Directory.GetFiles(iconFolderLocation))
                {
                    System.IO.File.Delete(file);
                }
            }
            else
            {
                Console.WriteLine("Old icon didn't exist");
            }

            string key = getKey();

            Console.WriteLine("Key: " + key);

            Console.WriteLine("Attempting to get library data");

            string recentGames = APIRequestBuilder(key, "IPlayerService", "GetOwnedGames",
                "v0001", new string[] { "steamid=" + localData.steamid, "include_appinfo=true" });
            string stringSite;
            Stream streamSite;

            try
            {
                stringSite = getSiteString(recentGames, 10);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            Console.WriteLine("Success, now parsing");

            JsonNode json = JsonNode.Parse(stringSite)!;

            Console.WriteLine("Parse complete, seaching through "
                          + json!["response"]!["game_count"]!.ToString()
                          + " games to find most recently played game");

            JsonArray gamedata = json!["response"]!["games"]!.AsArray();

            JsonNode highest = gamedata![0]!;
            long highestValue = -1, currentValue;

            for (int i = 0; i < gamedata.Count; i++)
            {
                JsonNode game = gamedata![i]!;

                currentValue = (long)game!["rtime_last_played"]!;

                if (highestValue < currentValue)
                {
                    highest = game;
                    highestValue = currentValue;
                }
            }

            long appid = (long)highest!["appid"]!;
            Console.WriteLine("Last played game: " + highest!["name"]!);
            Console.WriteLine("Attempting to get game icon");

            // this was an attempt to get SteamCMD to hand over an app's icon hash. Couldn't get it to work, may revisit

            /*ProcessStartInfo processInfo;
            Process process;

            string appid = SteamJSON.SearchJSON<long>(highest, "appid").ToString();
            processInfo = new ProcessStartInfo("cmd", "/C steamcmd +app_info_update " + appid + " +app_info_print " + appid + " +quit 1> _output.txt 2>&1");
            processInfo.CreateNoWindow = false;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardInput = true;
            processInfo.RedirectStandardOutput = false;
            processInfo.WorkingDirectory = "C:\\shit\\SteamCMD";

            process = Process.Start(processInfo);

            Console.WriteLine("uhh?");*/

            string? iconPath = "";
            if (localData.clientIcons.TryGetValue(((long)highest!["appid"]!).ToString(), out iconPath))
            {
                Console.WriteLine("Icon url found");
            }
            else
            {
                Console.WriteLine("Icon path not found, please find icon url on steamDB then enter below");
                Console.WriteLine("https://steamdb.info/app/" + appid +"/info/");
                iconPath = Console.ReadLine();
                addAppIconToData(appid.ToString(), iconPath);
                Console.WriteLine("Url added to local data");
            }

            try
            {
                streamSite = getSiteStream(iconPath, 10);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            Console.WriteLine("Getting icon from url");

            Icon icon;

            // from https://stackoverflow.com/questions/35826848/c-sharp-get-icon-from-url-as-system-drawing-icon
            // the stream needs to be converted to a memorystream (which supports seeking) so that the icon constructor doesn't need to be passed dimensions

            using (var ms = new MemoryStream())
            {
                streamSite.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin); // See https://stackoverflow.com/a/72205381/640195 (from the original stackoverflow q)

                icon = new Icon(ms);
            }

            Directory.CreateDirectory(iconFolderLocation);

            string iconLocation = iconFolderLocation + "\\" + appid.ToString() + ".ico";
            using (FileStream fs = new FileStream(iconLocation, FileMode.Create))
                icon.Save(fs);

            Console.WriteLine("Icon saved locally");

            string oldLinkLocation = localData.lastGame;
            string newLinkLocation = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) +
                "\\" + removeChars((string)highest!["name"]!, badFilenameChars) + ".lnk";

            if (System.IO.File.Exists(oldLinkLocation))
            {
                Console.WriteLine("Editing shortcut");

                if(oldLinkLocation != newLinkLocation)
                {
                    // changing the name
                    System.IO.File.Move(oldLinkLocation, newLinkLocation);
                    Thread.Sleep(1000);

                    // changing the icon and target address

                    WshShell shell = new WshShell();
                    IWshShortcut link = (IWshShortcut)shell.CreateShortcut(newLinkLocation);
                    link.TargetPath = "steam://rungameid/" + appid.ToString();
                    link.IconLocation = iconLocation;
                    link.Save();
                }

                Console.WriteLine("Shortcut edited! Cleaning up");
            }
            else
            {
                Console.WriteLine("No existing shortcut, creating shortcut");

                // using code from https://www.codeproject.com/Articles/3905/Creating-Shell-Links-Shortcuts-in-NET-Programs-Usi

                WshShell shell = new WshShell();
                IWshShortcut link = (IWshShortcut)shell.CreateShortcut(newLinkLocation);
                link.TargetPath = "steam://rungameid/" + appid.ToString();
                link.IconLocation = iconLocation;
                link.Save();

                Console.WriteLine("Shortcut created! Cleaning up");
            }

            localData.lastGame = newLinkLocation;
            writeData(localData);
            Thread.Sleep(1000);
        }

        public static string APIRequestBuilder(string key, string service, string method, string version, string[] args)
        {
            string argsString = "";

            foreach (string arg in args)
            {
                argsString += "&" + arg;
            }

            return "https://api.steampowered.com/" + service + "/" + method + "/" + version + "/?key=" + key + argsString + "&format=json";
        }

        private static string removeChars(string baseString, char[] chars)
        {
            for (int i = 0; i < baseString.Length; i++)
            {
                while (isIn<char>(baseString[i], chars))
                {
                    baseString = baseString.Remove(i, 1);
                }
            }

            return baseString;
        }

        private static bool isIn<T>(T potentialElement, T[] array)
        {
            foreach (T element in array)
            {
                if(potentialElement.Equals(element))
                {
                    return true;
                }
            }

            return false;
        }

        private static void addAppIconToData(string appid, string iconPath)
        {
            string data, oldLastGame, newData;
            int clientIconsStartPoint;

            using (StreamReader sr = System.IO.File.OpenText("data.txt"))
            {
                data = sr.ReadLine();
            }

            clientIconsStartPoint = findString(data, "clientIcons\":{", true);
            newData = data.Substring(0, clientIconsStartPoint);
            newData += "\"" + appid + "\":" + "\""+ iconPath + "\"," + data.Substring(clientIconsStartPoint);

            using (StreamWriter sw = new StreamWriter("data.txt"))
            {
                sw.Write(newData);
            }
        }

        /// <summary>
        ///  Finds a string within another string
        /// </summary>
        /// <param name="toReadFrom">String to read from</param>
        /// <param name="target">Target string</param>
        /// <param name="returnEndPos">Decides whether to return the first character of the target string (false), or the last (true)</param>
        /// <returns>-1 if the target string doesn't exist within the base string, otherwise the position
        /// in the base string of either the start or the end of the target string, depending on returnEndPos</returns>
        private static int findString(string toReadFrom, string target, bool returnEndPos)
        {
            bool flag = true;

            for (int i = 0; i < toReadFrom.Length; i++)
            {
                if (toReadFrom[i] == target[0])
                {
                    flag = true;

                    for (int x = 1; x < target.Length; x++)
                    {
                        if (toReadFrom[i + x] != target[x])
                        {
                            flag = false;
                            break;
                        }
                    }

                    if (flag)
                    {
                        return returnEndPos ? i + target.Length : i;
                    }
                }
            }

            return -1;
        }

        private static string readString(string toReadFrom, int startPoint)
        {
            if (toReadFrom[startPoint] != '"')
            {
                throw new ArgumentException("startPoint should point to an occurance of '\"'");
            }

            for (int i = startPoint + 1; i < toReadFrom.Length; i++)
            {
                if (toReadFrom[i] == '"')
                {
                    return toReadFrom.Substring(startPoint + 1, i - startPoint - 1);
                }
            }

            return string.Empty;
        }

        private static data getData()
        {
            data returnData = new data();

            if (System.IO.File.Exists("data.txt"))
            {
                using (StreamReader sr = System.IO.File.OpenText("data.txt"))
                {
                    returnData = JsonSerializer.Deserialize<data>(sr.ReadToEnd(), new JsonSerializerOptions { IncludeFields = true });
                }
            }

            writeData(returnData);
            return returnData;
        }

        private static void writeData(data newData)
        {
            System.IO.File.WriteAllText("data.txt", JsonSerializer.Serialize(newData, new JsonSerializerOptions { IncludeFields = true }));
        }

        private static string getKey()
        {
            using (StreamReader sr = System.IO.File.OpenText("key.txt"))
            {
                return sr.ReadLine();
            }
        }

        private static async Task<string> CallUrlString(string fullUrl)
        {
            HttpClient client = new HttpClient();
            string response = await client.GetStringAsync(fullUrl);
            return response;
        }

        private static async Task<Stream> CallUrlStream(string fullUrl)
        {
            HttpClient client = new HttpClient();
            Stream response = await client.GetStreamAsync(fullUrl);
            return response;
        }

        private static string getSiteString(string url, int timeout)
        {
            DateTime maxTime = DateTime.Now.Add(TimeSpan.FromSeconds(timeout));
            Task<String> result;

            while (DateTime.Now < maxTime)
            {
                try
                {
                    result = CallUrlString(url);
                    return result.Result;
                }
                catch (AggregateException)
                {

                }
            }

            throw new TimeoutException("Took too long to connect to internet :(");
        }

        private static Stream getSiteStream(string url, int timeout)
        {
            DateTime maxTime = DateTime.Now.Add(TimeSpan.FromSeconds(timeout));
            Task<Stream> result;

            while (DateTime.Now < maxTime)
            {
                try
                {
                    result = CallUrlStream(url);
                    return result.Result;
                }
                catch (AggregateException)
                {

                }
            }

            throw new TimeoutException("Took too long to connect to internet :(");
        }
    }
}