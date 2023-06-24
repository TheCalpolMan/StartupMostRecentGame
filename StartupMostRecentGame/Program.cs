using HtmlAgilityPack;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.IO;
using static System.Net.WebRequestMethods;

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
                if (layer == 0)
                {
                    break;
                }

                // continues looping until on the correct layer again
                if (layer != 1)
                {
                    continue;
                }

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

                            if (!returnDict.TryAdd(currentKey, tempValue))
                            {
                                throw new ArgumentException("Duplicate keys in JSON");
                            }

                            i += tempValue.Length + 1;
                        }
                        else
                        {
                            // assuming int for ease

                            tempValueInt = readInt(json, i + 1);

                            if (!returnDict.TryAdd(currentKey, tempValueInt))
                            {
                                throw new ArgumentException("Duplicate keys in JSON");
                            }

                            i += tempValueInt.ToString().Length + 1;
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

        private static List<Dictionary<string, object>> ParseJSONList(string json, int startPoint)
        {
            if (json[startPoint] != '[')
            {
                throw new ArgumentException("startPoint should point to an occurance of '['");
            }

            List<Dictionary<string, object>> returnList = new List<Dictionary<string, object>>();
            int currentPos = startPoint + 1,
                endPos = matchingBrace(json, startPoint, '[', ']') - 1;

            while (currentPos < endPos)
            {
                returnList.Add(ParseJSON(json, currentPos));
                currentPos = matchingBrace(json, currentPos, '{', '}') + 2;
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

            Console.WriteLine("Attempting to get recently played games");

            string recentGames = SteamJSON.APIRequestBuilder(key, "IPlayerService", "GetRecentlyPlayedGames",
                "v0001", new string[] { "steamid=76561198136424150" });
            string site;

            try
            {
                site = getSite(recentGames, 10);
            } catch (TimeoutException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            Console.WriteLine("Success, now parsing and finding most recently played game");

            Dictionary<string, object> json = SteamJSON.ParseJSON(site);
        }

        private static string getKey()
        {
            using (StreamReader sr = System.IO.File.OpenText("C:\\shit\\steam_api_key.txt"))
            {
                return sr.ReadLine();
            }
        }

        private static async Task<string> CallUrl(string fullUrl)
        {
            HttpClient client = new HttpClient();
            var response = await client.GetStringAsync(fullUrl);
            return response;
        }

        private static string getSite(string url, int timeout)
        {
            DateTime maxTime = DateTime.Now.Add(TimeSpan.FromSeconds(timeout));
            Task<String> result;

            while (DateTime.Now < maxTime)
            {
                try
                {
                    result = CallUrl(url);
                    return result.Result;
                }catch (AggregateException)
                {

                }
            }

            throw new TimeoutException("Took too long to connect to internet :(");
        }
    }
}