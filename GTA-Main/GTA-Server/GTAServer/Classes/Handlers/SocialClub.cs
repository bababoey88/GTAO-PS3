using Newtonsoft.Json;
using System.Net;
using System.Text;

#nullable disable

namespace GTAServer
{
    public class SocialClub
    {
        public static Task<int> GetPasswordRequirements(Globals.Client client)
        {
            string session_ticket = client.request.Headers.Get("ros-SessionTicket");

            Globals.Member member = new Globals.Member();
            Database.GetMemberFromSessionTicket(ref member, session_ticket);

            string file = File.ReadAllText("bin/socialclub/GetPasswordRequirements.xml");

            ServerCrypto serverCrypto = new ServerCrypto(true);
            client.responseData = serverCrypto.Encrypt(Encoding.UTF8.GetBytes(file), member.platform_name);

            client.response.StatusCode = (int)HttpStatusCode.OK;
            client.response.ContentType = "text/xml; charset=utf-8";
            client.response.ContentLength64 = client.responseData.Length;
            client.response.OutputStream.Write(client.responseData);

            return Task.FromResult(0);
        }

        public static Task<int> CheckText(Globals.Client client)
        {
            string session_ticket = client.request.Headers.Get("ros-SessionTicket");

            Globals.Member member = new Globals.Member();
            Database.GetMemberFromSessionTicket(ref member, session_ticket);

            string file = File.ReadAllText("bin/socialclub/CheckText.xml");

            ServerCrypto serverCrypto = new ServerCrypto(true);
            client.responseData = serverCrypto.Encrypt(Encoding.UTF8.GetBytes(file), member.platform_name);

            client.response.StatusCode = (int)HttpStatusCode.OK;
            client.response.ContentType = "text/xml; charset=utf-8";
            client.response.ContentLength64 = client.responseData.Length;
            client.response.OutputStream.Write(client.responseData);

            return Task.FromResult(0);
        }
        public static Task<int> PostUserFeedActivity(Globals.Client client)
        {
            string session_ticket = client.request.Headers.Get("ros-SessionTicket");
            Globals.Member member = new Globals.Member();
            Database.GetMemberFromSessionTicket(ref member, session_ticket);
            ClientCrypto clientCrypto = new ClientCrypto(true);
            client.requestData = clientCrypto.Decrypt(client.requestData, member.platform_name);

            //parse URL-encoded form data
            string requestText = Encoding.UTF8.GetString(client.requestData);
            var query = System.Web.HttpUtility.ParseQueryString(requestText);

            string trackId = query["activityData"];
            Console.WriteLine(trackId);

            // Load global tracks.json 
            string globalTracksFile = Path.Combine("bin", "tracks.json");
            Dictionary<string, (string artist, string song, string radio)> globalTracks = new();
            if (File.Exists(globalTracksFile))
            {
                var trackData = JsonConvert.DeserializeObject<List<dynamic>>(File.ReadAllText(globalTracksFile));
                foreach (var t in trackData)
                {
                    string tid = t.trackid;
                    string artist = t.artist;
                    string song = t.song != null ? t.song : null;
                    string radio = t.radio;
                    globalTracks[tid] = (artist, song, radio);
                }
            }

            // Prepare member tracks folder
            string memberTracksFolder = Path.Combine("bin", "members", member.xuid, "tracks");
            if (!Directory.Exists(memberTracksFolder))
                Directory.CreateDirectory(memberTracksFolder);

            string memberTracksFile = Path.Combine(memberTracksFolder, "tracks.json");
            List<dynamic> userTracks = new();
            if (File.Exists(memberTracksFile))
                userTracks = JsonConvert.DeserializeObject<List<dynamic>>(File.ReadAllText(memberTracksFile));

            if (!string.IsNullOrEmpty(trackId))
            {
                if (globalTracks.ContainsKey(trackId))
                {
                    var trackInfo = globalTracks[trackId];

                    // Log artist, song, radio
                    Console.WriteLine($"[INFO] Shared Track: Artist='{trackInfo.artist}', Song='{trackInfo.song}', Radio='{trackInfo.radio}'");

                    var userTrack = new
                    {
                        trackid = trackId,
                        artist = trackInfo.artist,
                        song = trackInfo.song,
                        radio = trackInfo.radio,
                        sharedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                    userTracks.Add(userTrack);
                }
                else
                {
                    Console.WriteLine($"[WARN] Track ID {trackId} not found in global tracks.json");
                }
            }

            // Save updated member tracks.json
            File.WriteAllText(memberTracksFile, JsonConvert.SerializeObject(userTracks, Formatting.Indented));

            string file = File.ReadAllText("bin/Success.xml");
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
        }
    }
}
