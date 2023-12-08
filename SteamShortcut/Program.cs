using IWshRuntimeLibrary;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SteamShortcut
{
    public struct Data
    {
        public string steamid;

        public int linkAmount,
            sleepBetweenRenames,
            sleepBeforeIconChange;

        public List<string> desktopShortcuts;

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
            Data localData = getData();
            string iconFolderLocation = System.IO.Directory.GetCurrentDirectory() + "\\icon";

            /*Console.WriteLine("Deleting old shortcut");
            if (System.IO.File.Exists(SteamJSON.SearchJSON<string>(localData, "lastGame")))
                System.IO.File.Delete(SteamJSON.SearchJSON<string>(localData, "lastGame"));
            else
                Console.WriteLine("Old shortcut didn't exist");*/

            Console.WriteLine("Deleting old icon(s)");
            if (Directory.Exists(iconFolderLocation))
            {
                foreach (string file in Directory.GetFiles(iconFolderLocation))
                {
                    System.IO.File.Delete(file);
                }
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
                          + json!["response"]!["game_count"]!.ToString() + " games to find the "
                          + localData.linkAmount.ToString() + " most recently played game(s)");

            JsonArray gamedata = json!["response"]!["games"]!.AsArray();

            List<JsonNode> highestNGames = new List<JsonNode>();
            long currentGameTimePlayed;

            for (int i = 0; i < gamedata.Count; i++)
            {
                JsonNode game = gamedata![i]!;

                if (highestNGames.Count < localData.linkAmount)
                {
                    highestNGames.Add(game);
                    highestNGames.Sort(GameJSONNodeComparer);
                    continue;
                }

                currentGameTimePlayed = (long)game!["rtime_last_played"]!;

                if ((long) highestNGames[highestNGames.Count - 1]!["rtime_last_played"]! < currentGameTimePlayed)
                {
                    highestNGames.RemoveAt(highestNGames.Count - 1);
                    highestNGames.Add(game);
                    highestNGames.Sort(GameJSONNodeComparer);
                }
            }

            foreach (JsonNode game in highestNGames)
            {
                Console.WriteLine((string)game!["name"]!);
            }

            List<string> iconPaths = new List<string>(),
                shortcutPaths = new List<string>();
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\",
                tempPath;

            for (int i = 0; i < highestNGames.Count; i++)
            {
                Console.WriteLine("Attempting to get " + highestNGames[i]!["name"]! + "'s icon");

                iconPaths.Add(saveGameIcon((long)highestNGames[i]!["appid"]!, iconFolderLocation, localData));
            }

            for (int i = 0; i < localData.desktopShortcuts.Count; i++)
            {
                tempPath = desktopPath + i.ToString() + ".lnk";
                renameFile(localData.desktopShortcuts[0], tempPath);
                localData.desktopShortcuts.RemoveAt(0);
                localData.desktopShortcuts.Add(tempPath);
            }

            Thread.Sleep(localData.sleepBetweenRenames);

            for (int i = 0; i < highestNGames.Count; i++)
            {
                tempPath = desktopPath + removeChars((string)highestNGames[i]!["name"]!, badFilenameChars) + ".lnk";

                if(i < localData.desktopShortcuts.Count)
                {
                    renameFile(localData.desktopShortcuts[0], tempPath);
                    localData.desktopShortcuts.RemoveAt(0);
                }
                else
                {
                    modifyOrCreateShortcut(tempPath, iconPaths[i], "https://google.com");
                }

                localData.desktopShortcuts.Add(tempPath);
            }

            Thread.Sleep(localData.sleepBeforeIconChange);

            for (int i = 0; i < highestNGames.Count; i++)
            {
                tempPath = desktopPath + removeChars((string)highestNGames[i]!["name"]!, badFilenameChars) + ".lnk";
                modifyOrCreateShortcut(tempPath, iconPaths[i], "steam://rungameid/" 
                    + ((long)highestNGames[i]!["appid"]!).ToString());
            }

            writeData(localData);
            Thread.Sleep(1000);
        }

        public static string saveGameIcon(long appid, string iconFolderLocation, Data localData)
        {
            Stream streamSite;
            string? iconPath = "";
            if (localData.clientIcons.TryGetValue((appid).ToString(), out iconPath))
            {
                Console.WriteLine("Icon url found");
            }
            else
            {
                Console.WriteLine("Icon path not found, please find icon url on steamDB then enter below");
                Console.WriteLine("https://steamdb.info/app/" + appid + "/info/");
                iconPath = Console.ReadLine();
                localData.clientIcons.Add(appid.ToString(), iconPath);
                Console.WriteLine("Url added to local data");
            }

            try
            {
                streamSite = getSiteStream(iconPath, 10);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine(ex.Message);
                return "";
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

            string iconLocation = iconFolderLocation + "\\" + appid.ToString() + ".ico";
            using (FileStream fs = new FileStream(iconLocation, FileMode.Create))
                icon.Save(fs);

            Console.WriteLine("Icon saved locally");

            return iconLocation;
        }

        public static void renameFile(string oldPath, string newPath)
        {
            if (System.IO.File.Exists(oldPath))
            {
                if (oldPath != newPath)
                {
                    // changing the name
                    System.IO.File.Move(oldPath, newPath);
                }
            }
        }

        public static void modifyOrCreateShortcut(string path, string iconPath, string destination)
        {
            if (System.IO.File.Exists(path))
                Console.WriteLine("Editing shortcut");
            else
                Console.WriteLine("No existing shortcut, creating shortcut");

            // using code from https://www.codeproject.com/Articles/3905/Creating-Shell-Links-Shortcuts-in-NET-Programs-Usi
            // changing the icon and target address

            WshShell shell = new WshShell();
            IWshShortcut link = (IWshShortcut)shell.CreateShortcut(path);
            link.TargetPath = destination;
            link.IconLocation = iconPath;
            link.Save();
        }

        public static int GameJSONNodeComparer(JsonNode x, JsonNode y)
        {
            long xPlayed = (long)x!["rtime_last_played"]!,
                yPlayed = (long)y!["rtime_last_played"]!;

            if (xPlayed < yPlayed)
            {
                return 1;
            }
            else if (xPlayed == yPlayed)
            {
                return 0;
            }

            return -1;
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

        private static Data getData()
        {
            Data returnData = new Data();

            if (System.IO.File.Exists("data.txt"))
            {
                using (StreamReader sr = System.IO.File.OpenText("data.txt"))
                {
                    returnData = JsonSerializer.Deserialize<Data>(sr.ReadToEnd(), new JsonSerializerOptions { IncludeFields = true });
                }
            }

            writeData(returnData);
            return returnData;
        }

        private static void writeData(Data newData)
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