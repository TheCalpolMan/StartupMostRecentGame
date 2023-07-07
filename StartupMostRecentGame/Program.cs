using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace StartupMostRecentGame
{
    public class SteamJSON
    {
        public static string APIRequestBuilder(string key, string service, string method, string version, string[] args)
        {
            string argsString = "";

            foreach (string arg in args)
            {
                argsString += "&" + arg;
            }

            return "https://api.steampowered.com/" + service + "/" + method + "/" + version + "/?key=" + key + argsString + "&format=json";
        }

        public static T SearchJSON<T>(Dictionary<string, object> ParsedJSON, string key)
        {
            return SearchJSON<T>(ParsedJSON, new string[] { key });
        }

        public static T SearchJSON<T>(Dictionary<string, object> ParsedJSON, string[] keys)
        {
            object currentResult = ParsedJSON;

            for (int i = 0; i < keys.Length; i++)
            {
                ((Dictionary<string, object>)currentResult).TryGetValue(keys[i], out currentResult);
            }

            return (T)currentResult;
        }

        public static Dictionary<string, object> ParseJSON(string json)
        {
            return ParseJSON(json, 0);
        }

        private static Dictionary<string, object> ParseJSON(string json, int startPos)
        {
            if (json[startPos] != '{')
            {
                throw new ArgumentException("startPoint should point to an occurance of '{'");
            }

            Dictionary<string, object> returnDict = new Dictionary<string, object>();
            int layer = 0;
            string currentKey = string.Empty, tempValue;
            long tempValueInt;

            for (int i = startPos; i < json.Length; i++)
            {
                switch (json[i])
                {
                    case '{':
                        layer++;
                        break;
                    case '}':
                        layer--;
                        break;
                    default:
                        break;
                }

                // exits loop when outside the target curly brackets
                if (layer < 1)
                {
                    break;
                }

                // continues looping until on the correct layer again
                if (layer != 1)
                {
                    continue;
                }

                //Console.WriteLine(i);

                switch (json[i])
                {
                    case '"':
                        currentKey = readString(json, i);
                        i += currentKey.Length + 1;

                        break;
                    case ':':
                        if (json[i + 1] == '{')
                        {
                            if (!returnDict.TryAdd(currentKey, ParseJSON(json, i + 1)))
                            {
                                throw new ArgumentException("Duplicate keys in JSON");
                            }

                            i = matchingBrace(json, i + 1, '{', '}');
                        }
                        else if (json[i + 1] == '[')
                        {
                            if (!returnDict.TryAdd(currentKey, ParseJSONList(json, i + 1)))
                            {
                                throw new ArgumentException("Duplicate keys in JSON");
                            }

                            i = matchingBrace(json, i + 1, '[', ']');
                        }
                        else if (json[i + 1] == '"')
                        {
                            tempValue = readString(json, i + 1);
                            // Console.WriteLine(tempValue);

                            if (!returnDict.TryAdd(currentKey, tempValue))
                            {
                                throw new ArgumentException("Duplicate keys in JSON");
                            }

                            i += tempValue.Length + 2;
                        }
                        else if (json[i + 1] == 't' || json[i + 1] == 'f')
                        {
                            if (!returnDict.TryAdd(currentKey, json[i + 1] == 't'))
                            {
                                throw new ArgumentException("Duplicate keys in JSON");
                            }

                            i += 4;
                        }
                        else
                        {
                            // assuming int for ease

                            tempValueInt = readInt(json, i + 1);

                            if (!returnDict.TryAdd(currentKey, tempValueInt))
                            {
                                throw new ArgumentException("Duplicate keys in JSON");
                            }

                            i += tempValueInt.ToString().Length;
                        }

                        currentKey = string.Empty;
                        break;
                    default:
                        break;
                }
            }

            return returnDict;
        }

        private static string readString(string json, int startPoint)
        {
            if (json[startPoint] != '"')
            {
                throw new ArgumentException("startPoint should point to an occurance of '\"'");
            }

            for (int i = startPoint + 1; i < json.Length; i++)
            {
                if (json[i] == '"')
                {
                    return json.Substring(startPoint + 1, i - startPoint - 1);
                }
            }

            return string.Empty;
        }

        private static long readInt(string json, int startPoint)
        {
            int currentPoint = startPoint;

            while ('0' <= json[currentPoint] && json[currentPoint] <= '9')
            {
                currentPoint++;
            }

            return long.Parse(json.Substring(startPoint, currentPoint - startPoint));
        }

        private static List<object> ParseJSONList(string json, int startPoint) // todo make work with int lists
        {
            if (json[startPoint] != '[')
            {
                throw new ArgumentException("startPoint should point to an occurance of '['");
            }

            List<object> returnList = new List<object>();
            int currentPos = startPoint + 1,
                endPos = matchingBrace(json, startPoint, '[', ']') - 1;

            while (currentPos < endPos)
            {
                if (json[currentPos] == '{')
                {
                    returnList.Add(ParseJSON(json, currentPos));
                    currentPos = matchingBrace(json, currentPos, '{', '}') + 2;
                }
                else
                {
                    // assuming int for ease (again)
                    returnList.Add(readInt(json, currentPos));
                    currentPos += returnList[returnList.Count - 1].ToString().Length + 1;
                }
            }

            return returnList;
        }

        private static int matchingBrace(string text, int startPoint, char upLevel, char downLevel)
        {
            if (text[startPoint] != upLevel)
            {
                throw new ArgumentException("startPoint should point to an occurance of upLevel");
            }

            int level = 0;

            for (int i = startPoint; i < text.Length; i++)
            {
                if (text[i] == upLevel)
                {
                    level++;
                }
                else if (text[i] == downLevel)
                {
                    level--;
                }

                if (level == 0)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            string key = getKey();

            Console.WriteLine("Key: " + key);

            Console.WriteLine("Attempting to get library data");

            string recentGames = SteamJSON.APIRequestBuilder(key, "IPlayerService", "GetOwnedGames",
                "v0001", new string[] { "steamid=76561198136424150", "include_appinfo=true" });
            string stringSite;
            Stream streamSite;

            try
            {
                stringSite = getSiteString(recentGames, 10);
            } catch (TimeoutException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            Console.WriteLine("Success, now parsing");

            Dictionary<string, object> json = SteamJSON.ParseJSON(stringSite);

            Console.WriteLine("Parse complete, seaching through "
                          + SteamJSON.SearchJSON<long>(json, new string[] { "response", "game_count" }).ToString()
                          + " games to find most recently played game");

            List<object> gamedata = SteamJSON.SearchJSON<List<object>>(json, new string[] { "response", "games" });

            Dictionary<string, object> highest = new Dictionary<string, object>();
            long highestValue = -1, currentValue;
            string[] searchKeys = new string[] { "rtime_last_played" };

            foreach (object game in gamedata)
            {
                currentValue = SteamJSON.SearchJSON<long>((Dictionary<string, object>)game, searchKeys);

                if (highestValue < currentValue)
                {
                    highest = (Dictionary<string, object>)game;
                    highestValue = currentValue;
                }
            }

            Console.WriteLine("Last played game: " + SteamJSON.SearchJSON<string>(highest, "name"));
            Console.WriteLine("Attempting to get game icon");

            ProcessStartInfo processInfo;
            Process process;

            string appid = SteamJSON.SearchJSON<long>(highest, "appid").ToString();
            processInfo = new ProcessStartInfo("cmd", "/C steamcmd +app_info_update " + appid + " +app_info_print " + appid + " +quit 1> _output.txt 2>&1");
            processInfo.CreateNoWindow = false;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardInput = true;
            processInfo.RedirectStandardOutput = false;
            processInfo.WorkingDirectory = "C:\\shit\\SteamCMD";

            process = Process.Start(processInfo);

            Console.WriteLine("uhh?");


            /*try
            {
                streamSite = getSiteStream("http://media.steampowered.com/steamcommunity/public/images/apps/"
                               + SteamJSON.SearchJSON<long>(highest, "appid").ToString()
                               + "/"
                               + SteamJSON.SearchJSON<string>(highest, "img_icon_url")
                               + ".jpg", 10);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            Image image = Image.FromStream(streamSite);
            Icon icon = Icon.FromHandle(((Bitmap)image).GetHicon());
            using (FileStream fs = new FileStream("C:\\users\\jacob\\Downloads\\iconico", FileMode.Create))
                icon.Save(fs);

            // using code from https://www.codeproject.com/Articles/3905/Creating-Shell-Links-Shortcuts-in-NET-Programs-Usi

            WshShell shell = new WshShell();
            IWshShortcut link = (IWshShortcut)shell.CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\hello.lnk");
            link.TargetPath = "https://google.com";
            link.IconLocation = "C:\\users\\jacob\\Downloads\\icon.ico";
            link.Save();*/
        }

        private static string getKey()
        {
            using (StreamReader sr = System.IO.File.OpenText("C:\\shit\\steam_api_key.txt"))
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
                }catch (AggregateException)
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