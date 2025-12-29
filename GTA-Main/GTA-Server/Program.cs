using System.Net;
using System.Text;

#nullable disable

namespace GTAServer
{
    class Program
    {
        private static HttpListener listener;

        private static byte[] GetRequestData(HttpListenerRequest request)
        {
            try
            {
                byte[] buffer = new byte[4096];
                int count;

                List<byte> data = new List<byte>();

                while ((count = request.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    byte[] temp = new byte[count];
                    Buffer.BlockCopy(buffer, 0, temp, 0, count);
                    data.AddRange(temp);
                }

                return data.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[DEBUG] [GetRequestData] Exception: {0}", ex.Message));
            }

            return null;
        }

        private static void HandlePostRequest(Globals.Client client)
        {
            if (object.ReferenceEquals(client, null))
            {
                Console.WriteLine("[DEBUG] client is null");
                return;
            }

            if (object.ReferenceEquals(client.path, null))
            {
                Console.WriteLine("[DEBUG] client.path is null");
                client.response?.Close();
                return;
            }

            if (object.ReferenceEquals(client.response, null))
            {
                Console.WriteLine("[DEBUG] client.response is null");
                return;
            }


            try
            {
                int functionNameOffset = client.path.LastIndexOf('/') + 1;
                string functionName = string.Empty;

                if (functionNameOffset != -1)
                {
                    functionName = client.path.Substring(functionNameOffset);
                }

                if (Globals.PostList.TryGetValue(functionName, out Func<Globals.Client, Task<int>> function))
                {
                    function(client);
                }
                else
                {
                    client.response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] [HandlePostRequest] Exception: {ex}");
                client.response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            finally
            {
                client.response.Close();
            }
        }


        private static void HandleGetRequest(Globals.Client client)
        {
            try
            {
                int fileNameOffset = client.path.LastIndexOf('/') + 1;
                string fileName = fileNameOffset != -1 ? client.path.Substring(fileNameOffset) : "";
                int fileExtOffset = client.path.LastIndexOf('.') + 1;
                string fileExt = fileExtOffset != -1 ? client.path.Substring(fileExtOffset) : "";

                if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(fileExt))
                {
                    client.response.StatusCode = (int)HttpStatusCode.BadRequest;
                    client.response.Close();
                    return;
                }

                // === 1. CLOUD MEMBER FILES (mpstats, saves) ===
                if (client.path.Contains("members"))
                {
                    string session_ticket = client.request.Headers.Get("ros-SessionTicket");
                    Globals.Member member = new Globals.Member();
                    if (!Database.GetMemberFromSessionTicket(ref member, session_ticket))
                    {
                        client.response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        client.response.Close();
                        return;
                    }

                    string filePath = Members.GetPath(client.path, member.platform_name);

                    // ← CRITICAL: RETURN 404 IF FILE DOES NOT EXIST
                    if (filePath == "DoesNotExist" || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        client.response.StatusCode = (int)HttpStatusCode.NotFound;
                        client.response.ContentType = "text/plain";
                        client.responseData = Encoding.UTF8.GetBytes("File not found");
                        client.response.ContentLength64 = client.responseData.Length;
                        client.response.OutputStream.Write(client.responseData);
                        client.response.Close();
                        return;
                    }

                    // ← SERVE FILE
                    byte[] fileData = File.ReadAllBytes(filePath);
                    ServerCrypto serverCrypto = new ServerCrypto(true);
                    client.responseData = serverCrypto.Encrypt(fileData, member.platform_name);

                    client.response.StatusCode = (int)HttpStatusCode.OK;
                    client.response.ContentType = Tools.GetContentType(fileExt);
                    client.response.ContentLength64 = client.responseData.Length;
                    client.response.OutputStream.Write(client.responseData);
                    client.response.Close();
                    return;
                }

                // === 2. MISSIONS ===
                else if (client.path.Contains("gta5mission"))
                {
                    string session_ticket = client.request.Headers.Get("ros-SessionTicket");
                    Globals.Member member = new Globals.Member();
                    Database.GetMemberFromSessionTicket(ref member, session_ticket);

                    // Extract everything after "gta5mission/"
                    int missionIndex = client.path.IndexOf("gta5mission/") + "gta5mission/".Length;
                    string relativePath = client.path.Substring(missionIndex);

                    // Normalize slashes
                    relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

                    // Final mission file path
                    string filePath = Path.Combine("bin", "ugc", "gta5missions", relativePath);

                    Console.WriteLine(filePath);

                    if (!File.Exists(filePath))
                    {
                        client.response.StatusCode = (int)HttpStatusCode.NotFound;
                        client.response.Close();
                        return;
                    }

                    byte[] fileData = File.ReadAllBytes(filePath);
                    ServerCrypto serverCrypto = new ServerCrypto(true);
                    client.responseData = serverCrypto.Encrypt(fileData, member.platform_name);

                    client.response.StatusCode = (int)HttpStatusCode.OK;
                    client.response.ContentType = Tools.GetContentType(fileExt);
                    client.response.ContentLength64 = client.responseData.Length;
                    client.response.OutputStream.Write(client.responseData);
                    client.response.Close();
                    return;
                }

                // === GTA5 PHOTO  ===
                else if (client.path.Contains("/ugc/gta5photo/"))
                {
                    string session_ticket = client.request.Headers.Get("ros-SessionTicket");
                    Globals.Member member = new Globals.Member();
                    Database.GetMemberFromSessionTicket(ref member, session_ticket);

                    string[] parts = client.path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    int idx = Array.IndexOf(parts, "gta5photo");

                    if (idx == -1 || parts.Length < idx + 3)
                    {
                        client.response.StatusCode = (int)HttpStatusCode.BadRequest;
                        client.response.Close();
                        return;
                    }

                    string contentId = parts[idx + 1];
                    string imageName = parts[idx + 2];

                    string photoPath = Path.Combine(
                        "bin",
                        "members",
                        member.xuid,
                        "gta5photo",
                        contentId,
                        imageName
                    );

                    if (!File.Exists(photoPath))
                    {
                        client.response.StatusCode = (int)HttpStatusCode.NotFound;
                        client.response.Close();
                        return;
                    }

                    byte[] fileData = File.ReadAllBytes(photoPath);
                    ServerCrypto serverCrypto = new ServerCrypto(true);
                    client.responseData = serverCrypto.Encrypt(fileData, member.platform_name);

                    client.response.StatusCode = (int)HttpStatusCode.OK;
                    client.response.ContentType = Tools.GetContentType(Path.GetExtension(imageName).TrimStart('.'));
                    client.response.ContentLength64 = client.responseData.Length;
                    client.response.OutputStream.Write(client.responseData);
                    client.response.Close();

                    Console.WriteLine($"[UGC-CDN] Served gta5photo {contentId}/{imageName}");
                    return;
                }

                // === 3. REDIRECT TUNABLES ===
                else if (client.path.Count(c => c == '/') > 1)
                {
                    string redirectUrl = $"http://tunables.gtao.ca/{fileName}";
                    client.response.Redirect(redirectUrl);
                    client.responseData = Encoding.UTF8.GetBytes($"Found. Redirecting to {redirectUrl}");
                    client.response.ContentLength64 = client.responseData.Length;
                    client.response.OutputStream.Write(client.responseData);
                    client.response.Close();
                    return;
                }

                // === 4. CREW EMBLEM ===
                else if (client.path.EndsWith("emblem_128.dds"))
                {
                    string crew_id = "68330145";
                    byte[] crew_emblem = File.ReadAllBytes(Path.Combine("bin/dds", fileName));

                    string session_ticket = client.request.Headers.Get("ros-SessionTicket");
                    Globals.Member member = new Globals.Member();
                    if (Database.GetMemberFromSessionTicket(ref member, session_ticket))
                    {
                        crew_id = member.crew_id;
                        string crewPath = $"bin/crews/{crew_id}/";
                        string fileNamePattern = "emblem_128";
                        string[] extensions = { ".png", ".jpg", ".webp" };
                        string foundFile = extensions
                            .Select(ext => Path.Combine(crewPath, $"{fileNamePattern}{ext}"))
                            .FirstOrDefault(File.Exists);

                        if (foundFile != null)
                            crew_emblem = Tools.ConvertPngToDdsBytes(foundFile);
                    }

                    client.responseData = crew_emblem;
                    client.response.StatusCode = (int)HttpStatusCode.OK;
                    client.response.ContentType = "image/dds";
                    client.response.ContentLength64 = client.responseData.Length;
                    client.response.OutputStream.Write(client.responseData);
                    client.response.Close();
                    return;
                }

                // === 5. STATIC FILES (bin/) ===
                else
                {
                    string filePath = Path.Combine("bin/static", fileName);
                    if (!File.Exists(filePath))
                    {
                        client.response.StatusCode = (int)HttpStatusCode.NotFound;
                        client.response.Close();
                        return;
                    }

                    client.responseData = File.ReadAllBytes(filePath);
                    client.response.StatusCode = (int)HttpStatusCode.OK;
                    client.response.ContentType = Tools.GetContentType(fileExt);
                    client.response.ContentLength64 = client.responseData.Length;
                    client.response.OutputStream.Write(client.responseData);
                    client.response.Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] [HandleGetRequest] Exception: {ex.Message}\n{ex.StackTrace}");
                client.response.StatusCode = (int)HttpStatusCode.InternalServerError;
                client.response.Close();
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            try
            {
                Globals.Client client = new Globals.Client()
                {
                    request = context.Request,
                    requestData = null,

                    response = context.Response,
                    responseData = null,

                    endPoint = context.Request.RemoteEndPoint,

                    method = context.Request.HttpMethod,
                    path = context.Request.Url.AbsolutePath
                };

                Console.WriteLine(string.Format("[{0}] {1} {2}", client.endPoint.ToString(), client.method, client.path));

                client.requestData = GetRequestData(client.request);

                Globals.Member member = new Globals.Member();

                bool Exists = Database.GetMemberFromSessionTicket(ref member, client.request.Headers.Get("ros-SessionTicket"));

                if (Exists)
                {
                    member.last_online = DateTime.Now;
                    member.session_ticket = client.request.Headers.Get("ros-SessionTicket");
                    Database.UpdateLastOnline(ref member);
                }

                switch (client.method)
                {
                    case "POST":
                        HandlePostRequest(client);
                        break;
                    case "GET":
                        HandleGetRequest(client);
                        break;
                    default:
                        client.response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        client.response.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[DEBUG] [HandleRequest] Exception: {0}", ex.Message));
            }
        }

        private static void Listen()
        {
            while (true)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();

                    new Thread(new ThreadStart(() => HandleRequest(context))).Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("[DEBUG] [Listen] Exception: {0}", ex.Message));
                }
            }
        }

        private static void Main(string[] args)
        {
            try
            {
                Console.Title = "GTAServer";

                listener = new HttpListener();
                listener.Prefixes.Add("http://+:80/");
                listener.Start();

                new Thread(new ThreadStart(Listen)).Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[DEBUG] [Main] Exception: {0}", ex.Message));
            }
        }
    }
}
