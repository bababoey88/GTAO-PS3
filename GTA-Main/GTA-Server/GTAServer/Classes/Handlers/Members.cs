using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

#nullable disable

namespace GTAServer
{
    public class Members
    {
        /*public static Task<int> MpStats(Globals.Client client)
        {
            string session_ticket = client.request.Headers.Get("ros-SessionTicket");

            Globals.Member member = new Globals.Member();
            Database.GetMemberFromSessionTicket(ref member, session_ticket);

            ClientCrypto clientCrypto = new ClientCrypto(true);
            client.requestData = clientCrypto.Decrypt(client.requestData, member.platform_name);

            MultipartFormData multipartFormData = new MultipartFormData();
            bool result = multipartFormData.Parse(client.requestData, GetPath(client.path, member.platform_name));

            if (result)
            {
                Console.WriteLine(string.Format("[DEBUG] MpStats: {0} success", GetPath(client.path, member.platform_name)));
            }

            string file = File.ReadAllText(result ? "bin/Success.xml" : "bin/OK.txt");

            ServerCrypto serverCrypto = new ServerCrypto(true);
            client.responseData = serverCrypto.Encrypt(Encoding.UTF8.GetBytes(file), member.platform_name);

            client.response.StatusCode = (int)HttpStatusCode.OK;
            client.response.ContentType = "text/xml; charset=utf-8";
            client.response.ContentLength64 = client.responseData.Length;
            client.response.OutputStream.Write(client.responseData);

            return Task.FromResult(0);
        }*/

        private static string GetMemberDir(Globals.Member member)
        {
            if (string.IsNullOrEmpty(member.xuid))
                throw new InvalidOperationException("Member XUID is null or empty");

            string dir = Path.Combine("bin", "members", member.xuid);
            Directory.CreateDirectory(dir);
            return dir;
        }



        /*public static Task<int> MpStats(Globals.Client client)
        {
            string session_ticket = client.request.Headers.Get("ros-SessionTicket");

            Globals.Member member = new Globals.Member();
            Database.GetMemberFromSessionTicket(ref member, session_ticket);

            ClientCrypto clientCrypto = new ClientCrypto(true);
            byte[] decrypted = clientCrypto.Decrypt(client.requestData, member.platform_name);
            client.requestData = decrypted;

            // ===============================
            // ALWAYS DUMP RAW PAYLOAD TO BIN (with correct filename)
            // ===============================
            try
            {
                string memberDir = GetMemberDir(member);

                // Parse multipart headers to find the filename
                string filename = "save_default0000.bin"; // fallback default

                try
                {
                    // Convert bytes to string for header parsing (UTF8 should work for headers)
                    string headerText = Encoding.UTF8.GetString(decrypted);
                    var match = Regex.Match(headerText, @"filename=""(?<fname>[^""]+)""");
                    if (match.Success)
                    {
                        string fname = match.Groups["fname"].Value;
                        if (fname == "save_char0001.save" || fname == "save_char0002.save")
                            filename = fname; // use uploaded char filename
                        else if (fname == "save_default0000.save")
                            filename = fname; // use default save
                    }
                }
                catch
                {
                    // ignore parsing errors, keep fallback
                }

                string dumpPath = Path.Combine(memberDir, filename);

                // Write all bytes to disk
                File.WriteAllBytes(dumpPath, decrypted);

                Console.WriteLine(
                    $"[DEBUG][MpStats] Raw save dumped → {dumpPath} ({decrypted.Length} bytes)"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG][MpStats] Dump failed: {ex}");
            }

            // ===============================
            // THEN TRY MULTIPART PARSE
            // ===============================
            MultipartFormData multipartFormData = new MultipartFormData();
            bool result = multipartFormData.Parse(
                client.requestData,
                GetPath(client.path, member.platform_name)
            );

            Console.WriteLine(result
                ? "[DEBUG][MpStats] Multipart parse SUCCESS"
                : "[DEBUG][MpStats] Multipart parse FAILED");

            string file = File.ReadAllText(result ? "bin/Success.xml" : "bin/OK.txt");

            ServerCrypto serverCrypto = new ServerCrypto(true);
            client.responseData = serverCrypto.Encrypt(
                Encoding.UTF8.GetBytes(file),
                member.platform_name
            );

            client.response.StatusCode = (int)HttpStatusCode.OK;
            client.response.ContentType = "text/xml; charset=utf-8";
            client.response.ContentLength64 = client.responseData.Length;
            client.response.OutputStream.Write(client.responseData);

            return Task.FromResult(0);
        }*/

        public static Task<int> MpStats(Globals.Client client)
        {
            string session_ticket = client.request.Headers.Get("ros-SessionTicket");

            Globals.Member member = new Globals.Member();
            Database.GetMemberFromSessionTicket(ref member, session_ticket);

            ClientCrypto clientCrypto = new ClientCrypto(true);
            client.requestData = clientCrypto.Decrypt(client.requestData, member.platform_name);

            MultipartFormData multipartFormData = new MultipartFormData();
            bool result = multipartFormData.Parse(client.requestData, GetPath(client.path, member.platform_name));

            if (result)
            {
                Console.WriteLine(string.Format("[DEBUG] MpStats: {0} success", GetPath(client.path, member.platform_name)));
            }

            string file = File.ReadAllText(result ? "bin/Success.xml" : "bin/OK.txt");

            ServerCrypto serverCrypto = new ServerCrypto(true);
            client.responseData = serverCrypto.Encrypt(Encoding.UTF8.GetBytes(file), member.platform_name);

            client.response.StatusCode = (int)HttpStatusCode.OK;
            client.response.ContentType = "text/xml; charset=utf-8";
            client.response.ContentLength64 = client.responseData.Length;
            client.response.OutputStream.Write(client.responseData);

            return Task.FromResult(0);
        }



        private static readonly Regex XblPathFormat = new Regex(@"^/cloud/11/cloudservices/members/xbl/\d+/GTA5/saves/mpstats$", RegexOptions.IgnoreCase);
        private static readonly Regex XblFilePathFormat = new Regex(@"^/cloud/11/cloudservices/members/xbl/\d+/GTA5/saves/mpstats/(save_default0000.save|save_char0001.save|save_char0002.save)$", RegexOptions.IgnoreCase);

        private static readonly Regex NpPathFormat = new Regex(@"^/cloud/11/cloudservices/members/np/[^/]+/GTA5/saves/mpstats$", RegexOptions.IgnoreCase);
        private static readonly Regex NpFilePathFormat = new Regex(@"^/cloud/11/cloudservices/members/np/[^/]+/GTA5/saves/mpstats/(save_default0000.save|save_char0001.save|save_char0002.save)$", RegexOptions.IgnoreCase);

        public static string GetPath(string absolutePath, string platformName = "xbox360")
        {
            bool isFile = absolutePath.EndsWith(".save");

            switch (platformName.ToLower())
            {
                case "xbox360":
                    // Existing XBL code unchanged...
                    Match match = isFile ? XblFilePathFormat.Match(absolutePath) : XblPathFormat.Match(absolutePath);
                    if (match.Success)
                    {
                        string filePath = string.Format("bin/members/{0}", isFile
                            ? absolutePath.Replace("/cloud/11/cloudservices/members/xbl/", "").Replace("/GTA5/saves/mpstats/", "/")
                            : absolutePath.Replace("/cloud/11/cloudservices/members/xbl/", "").Replace("/GTA5/saves/mpstats", ""));
                        return isFile ? (File.Exists(filePath) ? filePath : "DoesNotExist") : (Directory.Exists(filePath) ? filePath : "DoesNotExist");
                    }
                    break;
                case "ps3":
                    Match npMatch = isFile ? NpFilePathFormat.Match(absolutePath) : NpPathFormat.Match(absolutePath);
                    if (npMatch.Success)
                    {
                        // Extract gamertag: /np/Itsjeboykiaro/...
                        string[] parts = absolutePath.Split('/');
                        if (parts.Length < 7) return "DoesNotExist";
                        string gamertag = parts[6];  // Itsjeboykiaro
                        string xuid = Tools.GenerateXUID(gamertag);  // 002CBB42CEF09726

                        string relativePath = isFile
                            ? $"{xuid}/{parts[^1]}"  // xuid/save_default0000.save
                            : xuid;  // xuid (for dir)

                        string filePath = Path.Combine("bin", "members", relativePath);
                        return isFile ? (File.Exists(filePath) ? filePath : "DoesNotExist")
                                      : (Directory.Exists(filePath) ? filePath : "DoesNotExist");
                    }
                    break;
            }
            return "DoesNotExist";
        }

        public class StatItem
        {
            public string HashKey { get; set; }
            public string Type { get; set; }
            public long Value { get; set; }
        }

        public static byte[] GetStats(string filePath)
        {
            string jsonString = File.ReadAllText(filePath);
            List<StatItem> statItems = JsonConvert.DeserializeObject<List<StatItem>>(jsonString);
            List<byte> data = new List<byte>();

            foreach (StatItem statItem in statItems)
            {
                byte[] hashKeyBytes = Convert.FromHexString(statItem.HashKey);
                byte typeByte = statItem.Type switch
                {
                    "int64" => (byte)0,
                    "int32" => (byte)4,
                    "float" => (byte)3,
                    _ => (byte)0xFF
                };
                data.AddRange(hashKeyBytes);
                data.Add(typeByte);

                switch (statItem.Type)
                {
                    case "int64":
                        {
                            byte[] valueBytes = BitConverter.GetBytes(statItem.Value);  // long → bytes
                            Array.Reverse(valueBytes);
                            data.AddRange(valueBytes);
                        }
                        break;
                    case "int32":
                    case "float":
                        {
                            byte[] valueBytes = BitConverter.GetBytes((int)statItem.Value);  // long → int
                            Array.Reverse(valueBytes);
                            data.AddRange(valueBytes);
                        }
                        break;
                }
            }
            return data.ToArray();
        }

        public static int UpdateStats(string filePath, byte[] compressedStats)
        {
            int count = 0;
            string tempFile = null;

            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[DEBUG] [UpdateStats] File not found: {filePath}");
                    return -1;
                }

                // 1. READ JSON SAFELY (with FileShare)
                string jsonString;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    jsonString = sr.ReadToEnd();
                }

                List<StatItem> statItems = JsonConvert.DeserializeObject<List<StatItem>>(jsonString);
                if (statItems == null || statItems.Count == 0) return -1;

                // 2. DECOMPRESS + PARSE
                byte[] decompressed = Tools.Decompress(compressedStats);

                if (decompressed.Length < 6) return -1;

                byte[] buffer = new byte[decompressed.Length - 6];
                Buffer.BlockCopy(decompressed, 6, buffer, 0, buffer.Length);

                int offset = 0;
                while (offset + 4 <= buffer.Length)
                {
                    string hashKey = BitConverter.ToString(buffer, offset, 4).Replace("-", "").ToLower();
                    offset += 4;

                    var stat = statItems.FirstOrDefault(s => s.HashKey.Equals(hashKey, StringComparison.OrdinalIgnoreCase));
                    if (stat == null) continue;

                    if (stat.Type == "int64" && offset + 8 <= buffer.Length)
                    {
                        byte[] val = new byte[8];
                        Buffer.BlockCopy(buffer, offset, val, 0, 8);
                        Array.Reverse(val);
                        stat.Value = BitConverter.ToInt64(val, 0);
                        offset += 8;
                        count++;
                    }
                    else if ((stat.Type == "int32" || stat.Type == "float") && offset + 4 <= buffer.Length)
                    {
                        byte[] val = new byte[4];
                        Buffer.BlockCopy(buffer, offset, val, 0, 4);
                        Array.Reverse(val);
                        stat.Value = BitConverter.ToInt32(val, 0);
                        offset += 4;
                        count++;
                    }
                }

                // 3. WRITE TO TEMP + ATOMIC MOVE
                string jsonOutput = JsonConvert.SerializeObject(statItems, Formatting.Indented);
                tempFile = Path.GetTempFileName();

                // Write safely
                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.Write(jsonOutput);
                }

                // FINAL: Replace with retry + delete old
                string backup = filePath + ".old";
                if (File.Exists(backup)) File.Delete(backup);

                try
                {
                    File.Replace(tempFile, filePath, backup);
                }
                catch
                {
                    // Fallback: Copy + Delete
                    File.Copy(tempFile, filePath, true);
                    File.Delete(tempFile);
                }

                tempFile = null; // Prevent cleanup
                Console.WriteLine($"[DEBUG] [UpdateStats] SUCCESS: Updated {count} stats → {filePath}");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] [UpdateStats] ERROR: {ex.Message}\n{ex.StackTrace}");
                if (tempFile != null && File.Exists(tempFile))
                    try { File.Delete(tempFile); } catch { }
                return -1;
            }
        }

        public static Task<int> MpChars(Globals.Client client)
        {
            string session_ticket = client.request.Headers.Get("ros-SessionTicket");

            Globals.Member member = new Globals.Member();
            Database.GetMemberFromSessionTicket(ref member, session_ticket);

            ClientCrypto clientCrypto = new ClientCrypto(true);
            client.requestData = clientCrypto.Decrypt(client.requestData, member.platform_name);

            MultipartFormData multipartFormData = new MultipartFormData();
            bool result = multipartFormData.Parse(client.requestData, GetPortrait(client.path, member.platform_name));

            if (result)
            {
                Console.WriteLine(string.Format("[DEBUG] MpChars: {0} success", GetPortrait(client.path, member.platform_name)));
            }

            string file = File.ReadAllText(result ? "bin/Success.xml" : "bin/OK.txt");

            ServerCrypto serverCrypto = new ServerCrypto(true);
            client.responseData = serverCrypto.Encrypt(Encoding.UTF8.GetBytes(file), member.platform_name);

            client.response.StatusCode = (int)HttpStatusCode.OK;
            client.response.ContentType = "text/xml; charset=utf-8";
            client.response.ContentLength64 = client.responseData.Length;
            client.response.OutputStream.Write(client.responseData);

            return Task.FromResult(0);
        }

        private static readonly Regex XblPortraitFormat = new Regex(@"^/cloud/11/cloudservices/members/xbl/\d+/share/gta5/mpchars$", RegexOptions.IgnoreCase);
        private static readonly Regex XblPortraitPathFormat = new Regex(@"^/cloud/11/cloudservices/members/xbl/\d+/share/gta5/mpchars/(0.dds|1.dds)$", RegexOptions.IgnoreCase);

        //private static readonly Regex NpPortraitFormat = new Regex(@"^/cloud/11/cloudservices/members/xbl/\d+/share/gta5/mpchars$", RegexOptions.IgnoreCase);
        //private static readonly Regex NpPortraitPathFormat = new Regex(@"^/cloud/11/cloudservices/members/xbl/\d+/share/gta5/mpchars/(0.dds|1.dds)$", RegexOptions.IgnoreCase);
        private static readonly Regex NpPortraitFormat = new Regex(@"^/cloud/11/cloudservices/members/np/[^/]+/share/gta5/mpchars$", RegexOptions.IgnoreCase);
        private static readonly Regex NpPortraitPathFormat = new Regex(@"^/cloud/11/cloudservices/members/np/[^/]+/share/gta5/mpchars/(0\.dds|1\.dds)$", RegexOptions.IgnoreCase);

        public static string GetPortrait(string absolutePath, string platformName = "xbox360")
        {
            bool isFile = absolutePath.EndsWith(".dds");
            switch (platformName.ToLower())
            {
                case "xbox360":
                    {
                        Match match = isFile ? XblPortraitPathFormat.Match(absolutePath) : XblPortraitFormat.Match(absolutePath);
                        if (match.Success)
                        {
                            string filePath = string.Format("bin/members/{0}", isFile
                                ? absolutePath.Replace("/cloud/11/cloudservices/members/xbl/", "").Replace("/share/gta5/mpchars/", "/")
                                : absolutePath.Replace("/cloud/11/cloudservices/members/xbl/", "").Replace("/share/gta5/mpchars", ""));
                            return (isFile ? File.Exists(filePath) : Directory.Exists(filePath)) ? filePath : string.Empty;
                        }
                        break;
                    }
                case "ps3":
                    {
                        Match match = isFile ? NpPortraitPathFormat.Match(absolutePath) : NpPortraitFormat.Match(absolutePath);
                        if (match.Success)
                        {
                            string[] parts = absolutePath.Split('/');
                            if (parts.Length < 8) return string.Empty;
                            string gamertag = parts[6];
                            string xuid = Tools.GenerateXUID(gamertag);
                            string relativePath = isFile ? $"{xuid}/{parts[^1]}" : xuid;
                            string filePath = Path.Combine("bin", "members", relativePath);
                            return (isFile ? File.Exists(filePath) : Directory.Exists(filePath)) ? filePath : string.Empty;
                        }
                        break;
                    }
            }
            return string.Empty;
        }
    }
}
