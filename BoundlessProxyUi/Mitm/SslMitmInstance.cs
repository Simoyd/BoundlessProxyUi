using BoundlessProxyUi.ProxyManager;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BoundlessProxyUi.WsData;
using ComponentAce.Compression.Libs.zlib;
using BoundlessProxyUi.ProxyUi;
using BoundlessProxyUi.Util;
using BoundlessProxyUi.ProxyManager.Components;
using System.Windows.Media.Animation;
using System.Runtime.Remoting.Messaging;
using System.Windows.Ink;
using BoundlessProxyUi.JsonUpload;

namespace BoundlessProxyUi.Mitm
{
    class SslMitmInstance
    {
        private static readonly Dictionary<string, KeyValuePair<string, int>> planetLookup = new Dictionary<string, KeyValuePair<string, int>>();

        static SslMitmInstance()
        {
            return;
        }

        public static bool Terminate = false;

        public static string PlayerName = null;

        public static Dictionary<string, UdpProxy> planetPorts = new Dictionary<string, UdpProxy>();

        public static object playerPlanetLock = new object();
        public static string playerPlanet = string.Empty;

        private readonly Stream m_client;
        private readonly Stream m_server;
        private readonly ConnectionInstance m_connectionInstance;

        private readonly Dictionary<CommPacketDirection, BlockingCollection<WsFrame>> websocketDataQueue = new Dictionary<CommPacketDirection, BlockingCollection<WsFrame>>
        {
            { CommPacketDirection.ClientToServer, new BlockingCollection<WsFrame>() },
            { CommPacketDirection.ServerToClient, new BlockingCollection<WsFrame>() },
        };

        private static readonly Dictionary<CommPacketDirection, ConcurrentDictionary<string, BlockingCollection<WsMessage>>> OutgoingQueueDirection = new Dictionary<CommPacketDirection, ConcurrentDictionary<string, BlockingCollection<WsMessage>>>
        {
            { CommPacketDirection.ClientToServer, new ConcurrentDictionary<string, BlockingCollection<WsMessage>>() },
            { CommPacketDirection.ServerToClient, new ConcurrentDictionary<string, BlockingCollection<WsMessage>>() },
        };

        public bool ReplaceIpaddr { get; set; } = false;

        public delegate void OnFrameHandler(int planetId, string planetDisplayName, WsFrame frame);

        private OnFrameHandler onFrameIn;

        public event OnFrameHandler OnFrameIn
        {
            add
            {
                onFrameIn += value;
            }
            remove
            {
                onFrameIn -= value;
            }
        }

        private OnFrameHandler onFrameOut;

        public event OnFrameHandler OnFrameOut
        {
            add
            {
                onFrameOut += value;
            }
            remove
            {
                onFrameOut -= value;
            }
        }

        private string planetStringName = null;
        private string planetDisplayName = null;
        private int planetId = -1;

        public static async Task InitPlanets(Dictionary<string, string> hostLookup)
        {
            string blah = $"https://{Constants.DiscoveryServer}:8902/list-gameservers";

            HttpClient client = new HttpClient();
            var result = await client.GetAsync(blah);
            if (!result.IsSuccessStatusCode)
            {
                throw new Exception("Error getting planets from server");
            }

            var serverList = JArray.Parse(await result.Content.ReadAsStringAsync());

            int curPort = 1000;

            foreach (var something in serverList)
            {
                string planetId = something["name"].Value<string>();
                string planetName = something["displayName"].Value<string>();
                var planetNum = something["id"].Value<int>();

                string addr = something["addr"].Value<string>();
                string ipAddr = hostLookup[addr];

                planetLookup.Add(planetId, new KeyValuePair<string, int>(planetName, planetNum));

                UdpProxy proxy = null;

                while (curPort.ToString().Length == 4)
                {

                    try
                    {
                        proxy = new UdpProxy(curPort++, ipAddr, addr);
                    }
                    catch
                    {
                        continue;
                    }

                    break;
                }

                if (proxy == null)
                {
                    throw new Exception("too many ports in use");
                }

                planetPorts.Add(planetId, proxy);
            }

            // UDP Thread
            new Thread(() =>
            {
                var tasks = planetPorts.Values.Select(cur => cur.StartAsync()).ToArray();
                Task.WaitAny(tasks);

                var exception = tasks.FirstOrDefault(cur => cur.Exception != null)?.Exception?.InnerException;

                if (exception != null)
                {
                    MessageBox.Show(exception.Message);
                    KillUdp();
                }
            }).Start();
        }

        public static void KillUdp()
        {
            planetPorts.Values.ToList().ForEach(cur => cur.Kill());
            planetPorts.Clear();
        }

        public SslMitmInstance(Stream client, Stream server, ConnectionInstance connectionInstance, int chunkSize = 4096)
        {
            ProxyManagerWindow.Instance.Dispatcher.BeginInvoke(new Action(() =>
            {
                ((ManagerWindowViewModel)ProxyManagerWindow.Instance.DataContext).RefreshConversations();
            }));

            m_connectionInstance = connectionInstance;
            connectionInstance.SslMitmInstance = this;

            m_client = client;
            m_server = server;

            OnFrameIn += JsonUploadWindow.Instance.OnFrameIn;

            ForwardStreamNew(client, server, new byte[chunkSize], CommPacketDirection.ClientToServer);
            ForwardStreamNew(server, client, new byte[chunkSize], CommPacketDirection.ServerToClient);

            Dispatcher(CommPacketDirection.ServerToClient);
            Dispatcher(CommPacketDirection.ClientToServer);
        }

        public static void AddOutgoingMessage(string planet, CommPacketDirection direction, WsMessage message)
        {
            var outgoingQueue = OutgoingQueueDirection[direction];

            if (!outgoingQueue.TryGetValue(planet, out var collection))
            {
                collection = new BlockingCollection<WsMessage>();
                if (!outgoingQueue.TryAdd(planet, collection))
                {
                    collection = outgoingQueue[planet];
                }
            }

            collection.Add(message);
        }

        private static List<WsMessage> GetOutgoingMessages(string planet, CommPacketDirection direction)
        {
            var outgoingQueue = OutgoingQueueDirection[direction];

            List<WsMessage> result = new List<WsMessage>();

            if (outgoingQueue.TryGetValue(planet, out var collection))
            {
                while (collection.TryTake(out var curItem))
                {
                    result.Add(curItem);
                }
            }

            return result;
        }

        public void Kill(bool client)
        {
            m_connectionInstance.IsConnectionOpen = false;

            ProxyManagerWindow.Instance.Dispatcher.BeginInvoke(new Action(() =>
            {
                ((ManagerWindowViewModel)ProxyManagerWindow.Instance.DataContext).RefreshConversations();
            }));

            if (client)
            {
                try
                {
                    m_client.Flush();
                    m_client.Close();
                    m_client.Dispose();
                }
                catch { }

                try
                {
                    websocketDataQueue[CommPacketDirection.ServerToClient].CompleteAdding();
                }
                catch { }
            }
            else
            {
                try
                {
                    m_server.Flush();
                    m_server.Close();
                    m_server.Dispose();
                }
                catch { }

                try
                {
                    websocketDataQueue[CommPacketDirection.ClientToServer].CompleteAdding();
                }
                catch { }
            }

            var proxyWindowInstance = ProxyUiWindow.Instance;
            var managerWindowInstance = ProxyManagerWindow.Instance;

            if (proxyWindowInstance != null && managerWindowInstance != null)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(proxyWindowInstance.GetDataContext<ProxyUiWindowViewModel>().DeathTimeout * 1000);

                    managerWindowInstance.Dispatcher.Invoke(new Action(() =>
                    {
                        m_connectionInstance.Parent.Instances.Remove(m_connectionInstance);
                    }));
                });
            }
        }

        private void Dispatcher(CommPacketDirection direction)
        {
            new Thread(() =>
            {
                try
                {
                    var myQueue = websocketDataQueue[direction];
                    OnFrameHandler myHandler()
                    {
                        return direction == CommPacketDirection.ClientToServer ? onFrameOut : onFrameIn;
                    }

                    while (true)
                    {
                        WsFrame curFrame;

                        try
                        {
                            curFrame = myQueue.Take();
                        }
                        catch
                        {
                            break;
                        }

                        var curHandler = myHandler();

                        if (curHandler != null)
                        {
                            foreach (OnFrameHandler curInvoke in curHandler.GetInvocationList())
                            {
                                try
                                {
                                    curInvoke(planetId, planetDisplayName ?? string.Empty, curFrame);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }).Start();
        }

        private void ForwardStreamNew(Stream source, Stream destination, byte[] buffer, CommPacketDirection direction)
        {
            var rawVerbs = new string[] { "GET", "HEAD", "POST", "PUT", "DELETE", "TRACE", "CONNECT" };
            byte[][] verbs = rawVerbs.Select(cur => Encoding.UTF8.GetBytes(cur).Take(2).ToArray()).ToArray();
            Regex requestLinePattern = new Regex($"^({string.Join("|", rawVerbs)}) [^ ]+ HTTP/1.1$");
            Regex contentLengthPattern = new Regex($"^Content-Length: ([0-9]+)$");
            Regex chunkedPattern = new Regex($"^Transfer-Encoding: chunked$");
            Regex statusLinePattern = new Regex($"^HTTP/1.1 ([0-9]+) (.*)$");
            Regex websocketPlanet = new Regex("GET /([0-9]+)/websocket/game HTTP/1.1");
            Regex udpPortPattern = new Regex("\"udpPort\"\\:([0-9]+)\\,");

            try
            {
                destination = new BufferedStream(destination);
            }
            catch
            {
                return;
            }

            new Thread(() =>
            {
                MemoryStream ms = null;

                try
                {
                    bool isWebSocket = false;

                    while (true)
                    {
                        try
                        {
                            destination.Flush();
                        }
                        catch
                        {
                            Kill(direction == CommPacketDirection.ServerToClient);
                        }

                        if (!m_connectionInstance.IsConnectionOpen)
                        {
                            break;
                        }

                        //if (ms != null)
                        //{
                        //    ms.Position = 0;
                        //    messagecache[direction].Enqueue(ms);
                        //}

                        //while (messagecache[direction].Count > 10)
                        //{
                        //    messagecache[direction].Dequeue();
                        //}

                        ms = new MemoryStream();
                        StreamWriter sw = null;

                        bool ReadBytes(int count)
                        {
                            int offset = 0;

                            while (offset < count)
                            {
                                int bytesRead = 0;

                                try
                                {
                                    bytesRead = source.Read(buffer, offset, count - offset);
                                }
                                catch
                                {
                                    Kill(direction == CommPacketDirection.ClientToServer);
                                }

                                if (bytesRead == 0)
                                {
                                    return false;
                                }

                                offset += bytesRead;
                            }

                            return true;
                        }

                        string ReadLine()
                        {
                            List<byte> result = new List<byte>();

                            byte? prevByte = null;

                            while (true)
                            {
                                byte[] readBuffer = new byte[1];
                                if (source.Read(readBuffer, 0, 1) != 1)
                                {
                                    return null;
                                }

                                result.Add(readBuffer[0]);

                                if (prevByte == '\r' && readBuffer[0] == '\n')
                                {
                                    break;
                                }

                                prevByte = readBuffer[0];
                            }

                            return Encoding.UTF8.GetString(result.ToArray()).TrimEnd('\r', '\n');
                        }

                        void DoHttpHeadersContentAndForward()
                        {
                            List<string> headers = new List<string>();

                            ulong contentLength = 0;
                            bool chunked = false;

                            string curLine = null;
                            while ((curLine = ReadLine()) != null && curLine != string.Empty)
                            {
                                sw.WriteLine(curLine);

                                curLine = curLine.Trim();

                                Match m = contentLengthPattern.Match(curLine);
                                if (m.Success)
                                {
                                    contentLength = Convert.ToUInt64(m.Groups[1].Value);
                                }

                                m = chunkedPattern.Match(curLine);
                                if (m.Success)
                                {
                                    chunked = true;
                                }

                                headers.Add(curLine.ToLower());
                            }

                            sw.WriteLine();
                            sw.Flush();

                            if (curLine == null)
                            {
                                throw new Exception("HTTP unexpected end of stream while reading headers");
                            }

                            if (chunked && contentLength > 0)
                            {
                                throw new Exception("Chunked content with length not supported");
                            }

                            void DoReadLength(ulong curLength)
                            {
                                while (curLength > 0)
                                {
                                    int bytesRead = 0;

                                    try
                                    {
                                        bytesRead = source.Read(buffer, 0, Math.Min(buffer.Length, (int)Math.Min(int.MaxValue, curLength)));
                                    }
                                    catch
                                    {
                                        Kill(direction == CommPacketDirection.ClientToServer);
                                    }

                                    if (bytesRead == 0)
                                    {
                                        throw new Exception("HTTP unexpected end of stream while reading content");
                                    }

                                    ms.Write(buffer, 0, bytesRead);
                                    curLength -= (ulong)bytesRead;
                                }
                            }

                            DoReadLength(contentLength);

                            if (chunked)
                            {
                                bool lastChunk = false;

                                while (!lastChunk && (curLine = ReadLine()) != null && curLine != string.Empty)
                                {
                                    sw.WriteLine(curLine);
                                    sw.Flush();

                                    var length = ulong.Parse(curLine.Trim());

                                    if (length > 0)
                                    {
                                        DoReadLength(length);
                                    }
                                    else
                                    {
                                        lastChunk = true;
                                    }

                                    curLine = ReadLine();

                                    if (curLine == null || curLine.Length != 0)
                                    {
                                        throw new Exception("HTTP protocol failure");
                                    }

                                    sw.WriteLine();
                                    sw.Flush();
                                }
                            }

                            ms.Position = 0;
                            if (!headers.Contains("content-type: application/json".ToLower()))
                            {
                                DestinationWrite(destination, ms.ToArray(), (int)ms.Length, direction);
                            }
                            else
                            {
                                var orgLength = ms.Length;
                                string entireMessage = new StreamReader(ms).ReadToEnd();
                                ForwardHttpData(destination, entireMessage, direction);
                            }
                        }

                        void forwardWebsocketFrame()
                        {
                            WsFrame frame = null;

                            try
                            {
                                frame = new WsFrame(buffer, 2, source);
                            }
                            catch { }

                            var worldData = frame?.Messages.FirstOrDefault(cur => cur.ApiId.HasValue && cur.ApiId.Value == 0);

                            if (worldData != null)
                            {
                                string theJson = Encoding.UTF8.GetString(worldData.Buffer, 0, worldData.Buffer.Length);

                                Match m = udpPortPattern.Match(theJson);

                                if (m.Success && planetStringName != null)
                                {
                                    int serverPort = Convert.ToInt32(m.Groups[1].Value);

                                    if (serverPort.ToString().Length != 4)
                                    {
                                        throw new Exception("Length change of udpPort");
                                    }

                                    if (!planetPorts.ContainsKey(planetStringName))
                                    {
                                        //throw new Exception($"Planet dictionary does not contain {planetStringName}");
                                    }
                                    else
                                    {
                                        planetPorts[planetStringName].RemotePort = serverPort;
                                        theJson = udpPortPattern.Replace(theJson, $"\"udpPort\":{planetPorts[planetStringName].LocalPort},");

                                        byte[] sendData = Encoding.UTF8.GetBytes(theJson);

                                        if (sendData.Length != worldData.Buffer.Length)
                                        {
                                            throw new Exception("JSON length error");
                                        }

                                        worldData.Buffer = sendData;
                                    }
                                }
                            }

                            if (planetStringName != null)
                            {
                                frame?.Messages.AddRange(GetOutgoingMessages(planetStringName, direction));
                            }

                            try
                            {
                                frame?.Send(destination);
                            }
                            catch
                            {
                                Kill(direction == CommPacketDirection.ServerToClient);
                            }

                            //if (frame.readStream.Length != frame.writeStream.Length)
                            //{
                            //    throw new Exception("frame length mismatch.");
                            //}

                            //if (!frame.readStream.ToArray().SequenceEqual(frame.writeStream.ToArray()))
                            //{
                            //    throw new Exception("frame data mismatch.");
                            //}

                            if (frame == null)
                            {
                                frame = new WsFrame()
                                {
                                    Messages = new List<WsMessage>
                                    {
                                        new WsMessage(24, null, Encoding.UTF8.GetBytes("Frame decoding failure!")),
                                    },
                                };
                            }

                            try
                            {
                                websocketDataQueue[direction].Add(frame);
                            }
                            catch { }

                            if (ProxyUiWindow.Instance.GetDataContext<ProxyUiWindowViewModel>().CaptureEnabled)
                            {
                                var parentPacket = new CommPacket
                                {
                                    Data = frame.HeaderBytes,
                                    Direction = direction,
                                    Id = Guid.NewGuid(),
                                    Instance = m_connectionInstance,
                                    ParentPacket = null,
                                    Header = "Websocket Frame",
                                };

                                foreach (var curMessage in frame.Messages)
                                {
                                    var headerPacket = new CommPacket
                                    {
                                        Data = BitConverter.GetBytes((ushort)(curMessage.Buffer.Length + 1)).Concat(new byte[] { curMessage.ApiId ?? 0 }).ToArray(),
                                        Direction = direction,
                                        Id = Guid.NewGuid(),
                                        Instance = m_connectionInstance,
                                        ParentPacket = parentPacket,
                                        Header = $"Websocket Message[0x{curMessage.ApiId ?? 0:X2}]",
                                    };

                                    var payloadPacket = new CommPacket
                                    {
                                        Data = curMessage.Buffer,
                                        Direction = direction,
                                        Id = Guid.NewGuid(),
                                        Instance = m_connectionInstance,
                                        ParentPacket = parentPacket,
                                        Header = $"Websocket Payload",
                                    };

                                    headerPacket.ChildPackets.Add(payloadPacket);
                                    parentPacket.ChildPackets.Add(headerPacket);
                                };

                                ProxyUiWindow.Instance.GetDataContext<ProxyUiWindowViewModel>().SendBytesToUi(m_connectionInstance, parentPacket);
                            }
                        }

                        if (!ReadBytes(2))
                        {
                            // Connection terminated waiting for new message. This is fine.
                            break;
                        }

                        if (direction == CommPacketDirection.ClientToServer)
                        {
                            if (!verbs.Any(cur => cur[0] == buffer[0] && cur[1] == buffer[1]) && !isWebSocket)
                            {
                                isWebSocket = true;
                                ProxyManagerWindow.Instance.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    m_connectionInstance.Parent.Instances.Remove(m_connectionInstance);

                                    var wsg = ComponentEngine.Instance.GetComponent<TcpComponent>().websocketGroup;
                                    m_connectionInstance.Parent = wsg;
                                    wsg.Instances.Add(m_connectionInstance);
                                }));
                            }

                            if (!isWebSocket)
                            {
                                // GET /index.html HTTP/1.1
                                sw = new StreamWriter(ms);

                                string requestLine = (Encoding.UTF8.GetString(buffer, 0, 2) + ReadLine()).Trim();
                                sw.WriteLine(requestLine);

                                if (!requestLinePattern.IsMatch(requestLine))
                                {
                                    throw new Exception("HTTP request line invalid");
                                }

                                Match m = websocketPlanet.Match(requestLine);
                                if (m.Success)
                                {
                                    var newVal = Convert.ToInt32(m.Groups[1].Value);

                                    if (planetId != -1)
                                    {
                                        if (planetId != newVal)
                                        {
                                            throw new Exception("Multiple planets detected on a single stream");
                                        }
                                    }
                                    else
                                    {
                                        planetId = newVal;
                                        planetStringName = planetLookup.Where(cur => cur.Value.Value == newVal).FirstOrDefault().Key;
                                        planetDisplayName = planetLookup[planetStringName].Key;
                                    }
                                }

                                DoHttpHeadersContentAndForward();
                            }
                            else
                            {
                                forwardWebsocketFrame();
                            }
                        }
                        else
                        {
                            if ((buffer[0] != 'H' || buffer[1] != 'T') && !isWebSocket)
                            {
                                isWebSocket = true;
                                ProxyManagerWindow.Instance.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    m_connectionInstance.Parent.Instances.Remove(m_connectionInstance);

                                    var wsg = ComponentEngine.Instance.GetComponent<TcpComponent>().websocketGroup;
                                    m_connectionInstance.Parent = wsg;
                                    wsg.Instances.Add(m_connectionInstance);
                                }));
                            }

                            if (!isWebSocket)
                            {
                                // HTTP/1.1 200 OK
                                sw = new StreamWriter(ms);

                                string statusLine = (Encoding.UTF8.GetString(buffer, 0, 2) + ReadLine()).Trim();

                                sw.WriteLine(statusLine);

                                if (!statusLinePattern.IsMatch(statusLine))
                                {
                                    throw new Exception("HTTP status line invalid");
                                }

                                DoHttpHeadersContentAndForward();
                            }
                            else
                            {
                                forwardWebsocketFrame();
                            }
                        }
                    }

                    Kill(direction == CommPacketDirection.ClientToServer);
                }
                catch (Exception)
                {
                    //MainWindow.Instance.Dispatcher.Invoke(new Action(() =>
                    //{
                    //    MessageBox.Show($"4 Fatal error: {ex.Message}");
                    //}));

                    Kill(direction == CommPacketDirection.ClientToServer);
                }
            }).Start();
        }

        private void ForwardHttpData(Stream destination, string entireMessage, CommPacketDirection direction)
        {
            if (planetDisplayName == null &&
                entireMessage.Contains("\"worldData\"") &&
                entireMessage.Contains("\"displayName\"") &&
                entireMessage.Contains("\"id\"") &&
                entireMessage.Contains("\"name\""))
            {
                try
                {
                    var gameserverJson = JObject.Parse(entireMessage.Substring(entireMessage.IndexOf("\r\n\r\n") + 4));

                    planetId = gameserverJson["worldData"]["id"].Value<int>();
                    planetDisplayName = gameserverJson["worldData"]["displayName"].ToString();
                    planetStringName = gameserverJson["worldData"]["name"].ToString();

                    if (!planetLookup.ContainsKey(planetStringName))
                    {
                        planetLookup.Add(planetStringName, new KeyValuePair<string, int>(planetDisplayName, planetId));
                    }
                }
                catch { }
            }

            if (ReplaceIpaddr && entireMessage.Contains("ipAddr"))
            {
                Regex ipSubPattern = new Regex("\\,\"ipAddr\":\"(?!127\\.0\\.0\\.1)[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\"\\,");
                Match ipSubMatch = ipSubPattern.Match(entireMessage);

                if (ipSubMatch.Success)
                {
                    Regex reg = new Regex("Content-Length\\: ([0-9]+)", RegexOptions.IgnoreCase);
                    Match m = reg.Match(entireMessage);

                    int length = Convert.ToInt32(m.Groups[1].Value);
                    int newLength = length;

                    if (!m.Success)
                    {
                        throw new Exception("This shouldn't happen...");
                    }

                    int lenBeforeReplace = entireMessage.Length;

                    while (ipSubMatch.Success)
                    {
                        string org = ipSubMatch.Groups[0].Value;
                        string rep = ",\"ipAddr\":\"127.0.0.1\",";
                        entireMessage = entireMessage.Replace(org, rep);

                        ipSubMatch = ipSubPattern.Match(entireMessage);
                    }

                    int lenAfterReplace = entireMessage.Length;
                    newLength += lenAfterReplace - lenBeforeReplace;

                    entireMessage = entireMessage.Replace(m.Groups[0].Value, $"Content-Length: {newLength}");

                    DestinationWrite(destination, entireMessage, direction);
                }
                else
                {
                    DestinationWrite(destination, entireMessage, direction);
                }
            }
            else
            {
                DestinationWrite(destination, entireMessage, direction);
            }
        }

        private void DestinationWrite(Stream destination, string entireMessage, CommPacketDirection direction)
        {
            byte[] sendBytes = Encoding.UTF8.GetBytes(entireMessage);
            DestinationWrite(destination, sendBytes, sendBytes.Length, direction);
        }

        private void DestinationWrite(Stream destination, byte[] buffer, int count, CommPacketDirection direction)
        {
            if (ProxyUiWindow.Instance.GetDataContext<ProxyUiWindowViewModel>().CaptureEnabled)
            {
                byte[] saveData = new byte[count];
                Buffer.BlockCopy(buffer, 0, saveData, 0, count);
                SendBytesToUi(saveData, direction);
            }

            TryWriteStream(destination, buffer, 0, count, direction == CommPacketDirection.ServerToClient);
        }

        private void TryWriteStream(Stream destination, byte[] buffer, int offset, int count, bool client)
        {
            try
            {
                destination.Write(buffer, offset, count);
            }
            catch
            {
                Kill(client);
            }
        }

        private void SendBytesToUi(byte[] saveData, CommPacketDirection direction)
        {
            var length = saveData.Search(saveData.Length, new byte[] { 13, 10 });
            var dataStringSegments = Encoding.UTF8.GetString(saveData, 0, length).Split(' ');

            string header = "HTTP Data";

            if (dataStringSegments[2].StartsWith("HTTP"))
            {
                header = $"{dataStringSegments[0]} {dataStringSegments[1]}";
            }
            else if (dataStringSegments[0].StartsWith("HTTP"))
            {
                var desc = dataStringSegments[2];

                for (int i = 3; i < dataStringSegments.Length; ++i)
                {
                    desc += $" {dataStringSegments[i]}";
                }

                header = $"{dataStringSegments[1]} {desc}";
            }

            ProxyUiWindow.Instance.GetDataContext<ProxyUiWindowViewModel>().SendBytesToUi(m_connectionInstance, new CommPacket
            {
                Data = saveData,
                Direction = direction,
                Id = Guid.NewGuid(),
                Instance = m_connectionInstance,
                ParentPacket = null,
                Header = header,
            });
        }

        public static void ChunkToBoundless(out int boundlessEast, out int boundlessNorth, 
                                       int chunkEast, int chunkSouth, 
                                       int blockEast, int blockSouth)
        {
            if (chunkEast > 144)
            {
                chunkEast -= 288;
            }

            if (chunkSouth > 144)
            {
                chunkSouth -= 288;
            }

            boundlessEast = chunkEast * 16 + blockEast;
            boundlessNorth = -(chunkSouth * 16 + blockSouth);
        }
    }
}
