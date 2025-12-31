using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Specialized;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using static GTAServer.QueryContentData;

#nullable disable

namespace GTAServer
{
    public class Ugc
    {

        private static readonly object GlobalMissionsLock = new();

        public static readonly Dictionary<string, string> HashList =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "fa468f46f028ab31a9cc586e1322aa8ee408211c", "bin/TheFleecaJob.xml" },
            { "574ce380eb86a5f30909a025df4145f6dff6d84d", "bin/FleecaJobScopeOut.xml" },
            { "f5a9e6e44a7ba877c3ff72d1e60f35d087359f43", "bin/ThePrisonBreak.xml" },
            { "c81988c00621dd1433664562256dee7b0d69630e", "bin/TheHumaneLabsRaid.xml" },
            { "9676191e2b2d03637a5e7691cf5850c1c041f187", "bin/SeriesAFunding.xml" },
            { "42c08ee9b772e708fc1fa34f1452b1c36e8fb6c5", "bin/ThePacificStandardJob.xml" }
        };

        public class Query
        {
            public List<string> Lang { get; set; }
            public string Category { get; set; }
            public string Hash { get; set; }
        }

        class PhotoEntry
        {
            public string contentId;
            public string title;
            public string description;
            public string dataJson;
            public string language;
            public long createdDate;
            public long publishedDate;
        }

        static string GenerateContentId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
            var sb = new StringBuilder(22);
            for (int i = 0; i < 22; i++)
                sb.Append(chars[Random.Shared.Next(chars.Length)]);
            return sb.ToString();
        }

        static string BuildGetMyContentXml(List<PhotoEntry> photos, string psn)
        {
            var sb = new StringBuilder();

            sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            sb.AppendLine(@"<Response xmlns=""QueryContent"">");
            sb.AppendLine("<Status>1</Status>");
            sb.AppendLine($"<Result Count=\"{photos.Count}\" Total=\"{photos.Count}\" Hash=\"0\">");

            foreach (var p in photos)
            {
                sb.AppendLine($@"
                <r c=""{p.contentId}"">
                  <m ca=""psn"" cd=""{p.createdDate}"" f2=""-1"" n=""{p.title}"" l=""{p.language}""
                     pd=""{p.publishedDate}"" rci="""" un=""{psn}"" v=""1"">
                    <da><![CDATA[{p.dataJson}]]></da>
                    <de><![CDATA[{p.description}]]></de>
                  </m>
                  <r a=""0.00""/>
                  <s>{{""pt"":0,""pu"":0,""qt"":0,""qu"":0}}</s>
                </r>");
            }

            sb.AppendLine("</Result>");
            sb.AppendLine("</Response>");
            return sb.ToString();
        }

        public static Dictionary<string, string> MissionList = new()
        {
            { "rstar", "bin/ugc/QueryContent1.xml" },
            { "verif", "bin/ugc/QueryContent2.xml" }
        };

        public static Task<int> QueryContent(Globals.Client client)
        {
            string session_ticket = client.request.Headers.Get("ros-SessionTicket");

            Globals.Member member = new();
            Database.GetMemberFromSessionTicket(ref member, session_ticket);

            ClientCrypto clientCrypto = new(true);
            client.requestData = clientCrypto.Decrypt(client.requestData, member.platform_name);

            int queryDataLength = client.requestData.Length - 0x14;
            byte[] queryData = new byte[queryDataLength];
            Buffer.BlockCopy(client.requestData, 0, queryData, 0, queryDataLength);

            string queryString = Encoding.UTF8.GetString(queryData);
            NameValueCollection collection = HttpUtility.ParseQueryString(queryString);

            string contentType = collection["contentType"];
            string queryName = collection["queryName"];

            Console.WriteLine($"[UGC] contentType={contentType} queryName={queryName}");

            string jsonString = collection["queryParams"]?.Replace("'", "\"") ?? "{}";
            jsonString = Regex.Replace(jsonString, @"(\w+):", "\"$1\":");

            ServerCrypto serverCrypto = new(true);

            if (contentType == "gta5mission")
            {
                if (queryName == "GetContentByCategory")
                {
                    Query query = JsonConvert.DeserializeObject<Query>(jsonString);
                    string file = File.ReadAllText(MissionList[query.Category]);
                    client.responseData = serverCrypto.Encrypt(Encoding.UTF8.GetBytes(file), member.platform_name);
                }
                else if (queryName == "GetLatestVersionByContentId" || queryName == "GetContentByContentId")
                {
                    JObject obj = JObject.Parse(jsonString);
                    Console.WriteLine(obj);
                    string[] contentids = obj["contentids"]?.ToObject<string[]>();
                    Console.WriteLine(contentids);
                    string file = QueryContentData.GenerateXml(contentids);
                    client.responseData = serverCrypto.Encrypt(Encoding.UTF8.GetBytes(file), member.platform_name);
                }
                else if (queryName == "GetMyContent")
                {
                    string path = Path.Combine(
                        "bin",
                        "members",
                        member.xuid,
                        "GetMyContent",
                        "Missions_GetMyContent.xml"
                    );

                    string file;
                    if (File.Exists(path))
                    {
                        Console.WriteLine("[GetMyContent] Sending Missions_GetMyContent.xml");
                        file = File.ReadAllText(path);
                    }
                    else
                    {
                        Console.WriteLine("[GetMyContent] Missions file missing, sending QueryContent4.xml");
                        file = File.ReadAllText("bin/ugc/QueryContent4.xml");
                    }

                    client.responseData = serverCrypto.Encrypt(
                        Encoding.UTF8.GetBytes(file),
                        member.platform_name
                    );
                }
                else
                {
                    string file = File.ReadAllText("bin/ugc/QueryContent4.xml");
                    client.responseData = serverCrypto.Encrypt(Encoding.UTF8.GetBytes(file), member.platform_name);
                }
            }
            else if (contentType == "gta5photo" && queryName == "GetMyContent")
            {
                string path = Path.Combine("bin", "members", member.xuid, "GetMyContent", "Photos_GetMyContent.xml");
                string file = File.Exists(path)
                    ? File.ReadAllText(path)
                    : File.ReadAllText("bin/ugc/QueryContent4.xml");

                client.responseData = serverCrypto.Encrypt(Encoding.UTF8.GetBytes(file), member.platform_name);
            }

            client.response.StatusCode = (int)HttpStatusCode.OK;
            client.response.ContentType = "text/xml; charset=utf-8";
            client.response.ContentLength64 = client.responseData.Length;
            client.response.OutputStream.Write(client.responseData);

            return Task.FromResult(0);
        }
        public static Task<int> CreateContent(Globals.Client client)
        {
            Globals.Member member = new();
            Database.GetMemberFromSessionTicket(ref member, client.request.Headers.Get("ros-SessionTicket"));
            ClientCrypto clientCrypto = new(true);
            client.requestData = clientCrypto.Decrypt(client.requestData, member.platform_name);
            string ascii = Encoding.ASCII.GetString(client.requestData);
            string contentType = "unknown";
            int ctIdx = ascii.IndexOf("contentType=");
            if (ctIdx != -1)
            {
                int end = ascii.IndexOf('&', ctIdx);
                contentType = end == -1
                    ? ascii[(ctIdx + 12)..]
                    : ascii.Substring(ctIdx + 12, end - (ctIdx + 12));
            }
            // ParamJson=
            int paramsStart = ascii.IndexOf("paramsJson=");
            int dataStart = ascii.IndexOf("&data=");
            string decodedParams = "{}";
            if (paramsStart != -1 && dataStart > paramsStart)
            {
                string encoded = ascii.Substring(
                    paramsStart + "paramsJson=".Length,
                    dataStart - (paramsStart + "paramsJson=".Length)
                );
                decodedParams = Uri.UnescapeDataString(encoded);
                // Sanatize json inline
                int startCurly = decodedParams.IndexOf('{');
                if (startCurly != -1)
                    decodedParams = decodedParams[startCurly..];
                decodedParams = decodedParams.Replace("'", "\"");
                decodedParams = System.Text.RegularExpressions.Regex.Replace(decodedParams, @"(\w+):", "\"$1\":");
            }
            JObject paramsObj = JObject.Parse(decodedParams);
            string title = (paramsObj["ContentName"]?.ToString() ?? "Untitled").Replace('+', ' ');
            string description = paramsObj["Description"]?.ToString() ?? "";
            string language = paramsObj["Language"]?.ToString() ?? "en";
            byte[] dataPayload = Tools.ByteArraySplit(client.requestData, "data="u8.ToArray());
            string contentId = GenerateContentId();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // ============================================================
            // PHOTO
            // ============================================================
            if (contentType == "gta5photo")
            {
                int size = Tools.ToIntBigEndian(dataPayload);
                byte[] image = new byte[size];
                Buffer.BlockCopy(dataPayload, 8, image, 0, size);
                string photoDir = Path.Combine("bin", "members", member.xuid, "gta5photo", contentId);
                Directory.CreateDirectory(photoDir);
                File.WriteAllBytes(Path.Combine(photoDir, "0_0.jpg"), image);
                string dbPath = Path.Combine("bin", "members", member.xuid, "photos.json");
                List<PhotoEntry> db = File.Exists(dbPath)
                    ? JsonConvert.DeserializeObject<List<PhotoEntry>>(File.ReadAllText(dbPath))
                    : new();
                db.Add(new PhotoEntry
                {
                    contentId = contentId,
                    title = SecurityElement.Escape(title),
                    description = description,
                    dataJson = decodedParams,
                    language = language,
                    createdDate = now,
                    publishedDate = now + 10
                });
                File.WriteAllText(dbPath, JsonConvert.SerializeObject(db, Formatting.Indented));
                string getMyContentDir = Path.Combine("bin", "members", member.xuid, "GetMyContent");
                Directory.CreateDirectory(getMyContentDir);
                string xml = BuildGetMyContentXml(db, member.gamertag);
                File.WriteAllText(Path.Combine(getMyContentDir, "Photos_GetMyContent.xml"), xml);
            }
            // ============================================================
            // MISSIONS
            // ============================================================
            else if (contentType == "gta5mission")
            {
                // extract json
                int jsonEnd = -1;
                int depth = 0;
                bool inString = false;
                for (int i = 0; i < dataPayload.Length; i++)
                {
                    byte b = dataPayload[i];
                    if (b == '"' && (i == 0 || dataPayload[i - 1] != '\\'))
                        inString = !inString;
                    if (inString)
                        continue;
                    if (b == '{') depth++;
                    else if (b == '}' && --depth == 0)
                    {
                        jsonEnd = i + 1;
                        break;
                    }
                }
                if (jsonEnd <= 0)
                    throw new Exception("Mission JSON not found");
                string missionJsonRaw = Encoding.UTF8.GetString(dataPayload, 0, jsonEnd);
                // sanitize
                int startCurly = missionJsonRaw.IndexOf('{');
                if (startCurly != -1)
                    missionJsonRaw = missionJsonRaw[startCurly..];

                missionJsonRaw = missionJsonRaw.Replace("'", "\"");
                missionJsonRaw = System.Text.RegularExpressions.Regex.Replace(
                    missionJsonRaw,
                    @"(\w+):",
                    "\"$1\":"
                );

                string ugcDir = Path.Combine("bin", "ugc", "gta5missions", contentId);
                Directory.CreateDirectory(ugcDir);

                File.WriteAllText(
                    Path.Combine(ugcDir, $"0_0_{language}.json"),
                    missionJsonRaw,
                    new UTF8Encoding(false)
                );

                JObject missionData = JObject.Parse(missionJsonRaw);
                JObject gen = missionData["mission"]?["gen"] as JObject;
                if (gen == null)
                    throw new Exception("Mission JSON missing mission.gen structure");
                // make the json more compact
                JObject minimalGen = new JObject
                {
                    ["cam"] = gen["cam"],
                    ["area"] = gen["area"],
                    ["camh"] = gen["camh"],
                    ["camp"] = gen["camp"],
                    ["min"] = gen["min"],
                    ["num"] = gen["num"],
                    ["rank"] = gen["rank"],
                    ["start"] = gen["start"],
                    ["rad"] = gen["rad"],
                    ["tnum"] = gen["tnum"],
                    ["type"] = gen["type"]
                };
                JObject compactedMission = new JObject
                {
                    ["mission"] = new JObject { ["gen"] = minimalGen }
                };
                string compactedJson = compactedMission.ToString(Formatting.None);
                                                                                  
                //find the jpeg
                int jpegOffset = -1;
                for (int i = jsonEnd; i < dataPayload.Length - 2; i++)
                {
                    if (dataPayload[i] == 0xFF &&
                        dataPayload[i + 1] == 0xD8 &&
                        dataPayload[i + 2] == 0xFF)
                    {
                        jpegOffset = i;
                        break;
                    }
                }
                if (jpegOffset != -1)
                {
                    byte[] thumb = new byte[dataPayload.Length - jpegOffset];
                    Buffer.BlockCopy(
                        dataPayload,
                        jpegOffset,
                        thumb,
                        0,
                        thumb.Length
                    );
                    File.WriteAllBytes(Path.Combine(ugcDir, "1_0.jpg"), thumb);
                }

                string compactedForDb = compactedMission.ToString(Formatting.None);
                string dbPath = Path.Combine("bin", "members", member.xuid, "missions.json");
                List<PhotoEntry> db = File.Exists(dbPath)
                    ? JsonConvert.DeserializeObject<List<PhotoEntry>>(File.ReadAllText(dbPath))
                    : new();
                db.Add(new PhotoEntry
                {
                    contentId = contentId,
                    title = SecurityElement.Escape(title),
                    description = description,
                    dataJson = compactedForDb,
                    language = language,
                    createdDate = now,
                    publishedDate = now + 10
                });
                File.WriteAllText(dbPath, JsonConvert.SerializeObject(db, Formatting.Indented));
                string getMyContentDir = Path.Combine("bin", "members", member.xuid, "GetMyContent");
                Directory.CreateDirectory(getMyContentDir);
                string xml = BuildGetMyContentXml(db, member.gamertag);
                File.WriteAllText(Path.Combine(getMyContentDir, "Missions_GetMyContent.xml"), xml);

                // add mission to json.txt
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string globalMissionsPath = Path.Combine(exeDir, "json.txt");

                JObject newGlobalEntry = new JObject
                {
                    ["_c"] = contentId,
                    ["m"] = new JObject
                    {
                        ["_ca"] = "psn",
                        ["_f2"] = "-1",
                        ["_n"] = title,
                        ["_l"] = language,
                        ["_rci"] = contentId,
                        ["da"] = compactedMission,
                        ["de"] = string.IsNullOrEmpty(description)
                            ? "<![CDATA[]]>"
                            : $"<![CDATA[{description}]]>"
                    },
                    ["r"] = new JObject
                    {
                        ["_a"] = "0",
                        ["_u"] = "0",
                        ["_n"] = "0",
                        ["_p"] = "0"
                    },
                    ["s"] = null
                };

                lock (GlobalMissionsLock)
                {
                    if (!File.Exists(globalMissionsPath))
                    {
                        File.WriteAllText(globalMissionsPath, "{ \"r\": [] }");
                    }

                    string content = File.ReadAllText(globalMissionsPath, Encoding.UTF8);

                    JArray globalMissions;
                    string trimmed = content.TrimStart();

                    try
                    {
                        if (trimmed.StartsWith("{"))
                        {
                            JObject obj = JObject.Parse(content);
                            globalMissions = obj["r"] as JArray ?? new JArray();
                        }
                        else if (trimmed.StartsWith("["))
                        {
                            
                            globalMissions = JArray.Parse(content);
                        }
                        else
                        {
                            throw new JsonReaderException("Invalid json.txt root");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[ERROR] Failed to parse json.txt: " + ex.Message);

                        string backupPath = globalMissionsPath +
                                            ".corrupted_" +
                                            DateTime.Now.ToString("yyyyMMddHHmmss");

                        try { File.Copy(globalMissionsPath, backupPath, true); }
                        catch { }

                        globalMissions = new JArray();
                    }

                    globalMissions.Add(newGlobalEntry);

                    JObject saveWrapper = new JObject
                    {
                        ["r"] = globalMissions
                    };

                    string output = saveWrapper.ToString(Formatting.Indented);

                    string tmp = globalMissionsPath + ".tmp";
                    File.WriteAllText(tmp, output, new UTF8Encoding(false));
                    File.Replace(tmp, globalMissionsPath, null);

                    Console.WriteLine(
                        $"[INFO] Successfully added mission '{title}' → json.txt now contains {globalMissions.Count} missions."
                    );
                }
            }

                string response = File.ReadAllText("bin/ugc/CreateContent.xml");
            ServerCrypto serverCrypto = new(true);
            client.responseData = serverCrypto.Encrypt(
                Encoding.UTF8.GetBytes(response),
                member.platform_name
            );
            client.response.StatusCode = (int)HttpStatusCode.OK;
            client.response.ContentType = "text/xml; charset=utf-8";
            client.response.ContentLength64 = client.responseData.Length;
            client.response.OutputStream.Write(client.responseData);
            return Task.FromResult(0);
        }

        public static async Task<int> UpdateContent(Globals.Client client)
        {
            try
            {
                string sessionTicket = client.request.Headers.Get("ros-SessionTicket");
                Globals.Member member = new Globals.Member();
                if (!Database.GetMemberFromSessionTicket(ref member, sessionTicket))
                {
                    client.response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    client.response.Close();
                    return -1;
                }

                if (client.requestData == null || client.requestData.Length == 0)
                {
                    Console.WriteLine("[UGC] UpdateContent: empty request body");
                    client.response.StatusCode = (int)HttpStatusCode.BadRequest;
                    client.response.Close();
                    return -1;
                }

                ClientCrypto crypto = new ClientCrypto(true);
                byte[] decrypted = crypto.Decrypt(client.requestData, member.platform_name);

                int payloadOffset = 8;
                if (decrypted.Length <= payloadOffset)
                {
                    Console.WriteLine("[UGC] UpdateContent: decrypted payload too short");
                    client.response.StatusCode = (int)HttpStatusCode.BadRequest;
                    client.response.Close();
                    return -1;
                }

                string payloadStr = Encoding.UTF8.GetString(decrypted, payloadOffset, decrypted.Length - payloadOffset);

                //parse URL-encoded parameters
                var queryParams = System.Web.HttpUtility.ParseQueryString(payloadStr);
                string contentId = queryParams["contentId"];
                string updateJson = queryParams["updateJson"];

                if (string.IsNullOrEmpty(contentId) || string.IsNullOrEmpty(updateJson))
                {
                    Console.WriteLine("[UGC] UpdateContent: missing contentId or updateJson");
                    client.response.StatusCode = (int)HttpStatusCode.BadRequest;
                    client.response.Close();
                    return -1;
                }

                //decode json
                string decodedJson = Uri.UnescapeDataString(updateJson);
                JObject jsonObj = JObject.Parse(decodedJson);
                string contentName = jsonObj["ContentName"]?.ToString() ?? "";
                string dataJson = jsonObj["DataJson"]?.ToString() ?? "{}";
                string description = jsonObj["Description"]?.ToString() ?? "";

                string memberDir = Path.Combine("bin", "members", member.xuid);
                string photosJsonPath = Path.Combine(memberDir, "photos.json");
                string xmlPath = Path.Combine(memberDir, "GetMyContent", "GetMyContent.xml");

                // update photos.json
                if (File.Exists(photosJsonPath))
                {
                    var photoList = JsonConvert.DeserializeObject<List<PhotoEntry>>(File.ReadAllText(photosJsonPath));
                    var photo = photoList.Find(p => p.contentId == contentId);
                    if (photo != null)
                    {
                        photo.title = contentName;
                        photo.dataJson = dataJson;
                        photo.description = description;

                        File.WriteAllText(photosJsonPath, JsonConvert.SerializeObject(photoList, Formatting.Indented));
                        Console.WriteLine($"[UpdateContent] Updated photos.json for {contentId}");
                    }
                }

                //rebuild getmycontent from photos.json (had some bugs with trying to rebuild the xml directly)
                if (File.Exists(photosJsonPath))
                {
                    var photoList = JsonConvert.DeserializeObject<List<PhotoEntry>>(File.ReadAllText(photosJsonPath));

                    string getMyContentDir = Path.Combine("bin", "members", member.xuid, "GetMyContent");
                    Directory.CreateDirectory(getMyContentDir);

                    string xml = BuildGetMyContentXml(photoList, member.gamertag);
                    File.WriteAllText(Path.Combine(getMyContentDir, "GetMyContent.xml"), xml);

                    Console.WriteLine($"[UpdateContent] Rebuilt GetMyContent.xml from photos.json for {contentId}");
                }

                client.response.StatusCode = (int)HttpStatusCode.OK;
                client.response.Close();
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpdateContent] Exception: {ex}");
                client.response.StatusCode = (int)HttpStatusCode.InternalServerError;
                client.response.Close();
                return -1;
            }
        }
        public static async Task<int> SetDeleted(Globals.Client client)
        {
            try
            {
                string sessionTicket = client.request.Headers.Get("ros-SessionTicket");
                Globals.Member member = new Globals.Member();
                if (!Database.GetMemberFromSessionTicket(ref member, sessionTicket))
                {
                    client.response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    client.response.Close();
                    return -1;
                }

                if (client.requestData == null || client.requestData.Length == 0)
                {
                    Console.WriteLine("[SetDeleted] empty request body");
                    client.response.StatusCode = (int)HttpStatusCode.BadRequest;
                    client.response.Close();
                    return -1;
                }

                ClientCrypto crypto = new ClientCrypto(true);
                byte[] decrypted = crypto.Decrypt(client.requestData, member.platform_name);

                int payloadOffset = 8;
                if (decrypted.Length <= payloadOffset)
                {
                    Console.WriteLine("[SetDeleted] decrypted payload too short");
                    client.response.StatusCode = (int)HttpStatusCode.BadRequest;
                    client.response.Close();
                    return -1;
                }

                string payloadStr = Encoding.UTF8.GetString(decrypted, payloadOffset, decrypted.Length - payloadOffset);

                var queryParams = System.Web.HttpUtility.ParseQueryString(payloadStr);
                string contentId = queryParams["contentId"];
                string deleted = queryParams["deleted"];

                if (string.IsNullOrEmpty(contentId))
                {
                    Console.WriteLine("[SetDeleted] missing contentId");
                    client.response.StatusCode = (int)HttpStatusCode.BadRequest;
                    client.response.Close();
                    return -1;
                }

                // check if deleted == true
                if (!string.IsNullOrEmpty(deleted) && deleted.ToLower().Contains("true"))
                {
                    Console.WriteLine($"[SetDeleted] Deleting contentId {contentId}");

                    string memberDir = Path.Combine("bin", "members", member.xuid);
                    string photosJsonPath = Path.Combine(memberDir, "photos.json");

                    if (File.Exists(photosJsonPath))
                    {
                        // --- Remove from JSON ---
                        var photoList = JsonConvert.DeserializeObject<List<PhotoEntry>>(File.ReadAllText(photosJsonPath));
                        int removed = photoList.RemoveAll(p => p.contentId == contentId);

                        if (removed > 0)
                        {
                            File.WriteAllText(photosJsonPath, JsonConvert.SerializeObject(photoList, Formatting.Indented));
                            Console.WriteLine($"[SetDeleted] Removed {contentId} from photos.json");
                        }
                        else
                        {
                            Console.WriteLine($"[SetDeleted] contentId {contentId} not found in photos.json");
                        }

                        // delete photo folder
                        string photoDir = Path.Combine(memberDir, "gta5photo", contentId);
                        if (Directory.Exists(photoDir))
                        {
                            Directory.Delete(photoDir, true);
                            Console.WriteLine($"[SetDeleted] Deleted folder {photoDir}");
                        }

                        // rebuild xml
                        string xmlPath = Path.Combine(memberDir, "GetMyContent", "GetMyContent.xml");
                        Directory.CreateDirectory(Path.GetDirectoryName(xmlPath));
                        string xml = BuildGetMyContentXml(photoList, member.gamertag);
                        File.WriteAllText(xmlPath, xml);
                        Console.WriteLine($"[SetDeleted] Rebuilt GetMyContent.xml");
                    }
                }

                client.response.StatusCode = (int)HttpStatusCode.OK;
                client.response.Close();
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SetDeleted] Exception: {ex}");
                client.response.StatusCode = (int)HttpStatusCode.InternalServerError;
                client.response.Close();
                return -1;
            }
        }
    }
}
