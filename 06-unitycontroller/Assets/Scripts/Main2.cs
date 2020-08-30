using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ZLogger;


public class Main2 : MonoBehaviour, IMqttClientConnectedHandler, IMqttClientDisconnectedHandler, IMqttApplicationMessageReceivedHandler
{

    private static readonly ILogger<Main2> logger = LogManager.GetLogger<Main2>();


    public LayoutGroup InfoGroup;
    public GameObject BridgeInfoPrefab;

    private CubeManager cubeManager;

    private IMqttClientOptions options;
    private IMqttClient client;


    void Start()
    {
        Application.targetFrameRate = 60;

        client = new MqttFactory().CreateMqttClient();
        client.UseConnectedHandler(this);
        client.UseDisconnectedHandler(this);
        client.UseApplicationMessageReceivedHandler(this);
        options = new MqttClientOptionsBuilder()
            .WithClientId("controller")
            .WithTcpServer("localhost")
            .WithCleanSession()
            .Build();
        client.ConnectAsync(options);

        cubeManager = new CubeManager(client);
    }


    public Task HandleConnectedAsync(MqttClientConnectedEventArgs eventArgs)
    {
        logger.ZLogDebug("Connected");
        return Task.Run(async () =>
        {
            var topics = new[] { "+/connected", "+/disconnected", "+/button", "+/battery" };
            foreach (var t in topics)
            {
                await client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(t).Build());
                logger.ZLogDebug("Subscribed to topic: " + t);
            }
        });
    }


    public Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
    {
        return Task.Run(async () =>
        {
            logger.ZLogDebug("### DISCONNECTED FROM SERVER ###");
            await Task.Delay(System.TimeSpan.FromSeconds(5));
            try
            {
                await client.ConnectAsync(options, CancellationToken.None);
            }
            catch
            {
                logger.ZLogDebug("### RECONNECTING FAILED ###");
            }
        });
    }


    public Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var m = e.ApplicationMessage;
            var payload = m.Payload != null ? Encoding.UTF8.GetString(m.Payload) : "";
            logger.ZLogDebug($"Message received: Client = {e.ClientId}, Topic = {m.Topic}, Payload = {payload}");
            var t = m.Topic.Split('/');
            var address = t[0];
            switch (t[1])
            {
                case "connected":
                    var cube = cubeManager.CreateCube(address);
                    cube.SetLamp(Color.white);
                    break;

                case "disconnected":
                    break;

                case "button":
                    byte state = m.Payload[0];
                    if (state > 0)
                    {
                        Dispatcher.runOnUiThread(() =>
                        {
                            var color = new Color(Random.value, Random.value, Random.value);
                            logger.ZLogDebug(color.ToString());
                            cubeManager.SetLamp(color);
                        });
                    }
                    break;

                case "battery":
                    byte value = m.Payload[0];
                    cubeManager.SetBattery(address, value);
                    break;
            }
        }
        catch (System.Exception ex)
        {
            logger.ZLogDebug(ex, "");
        }
        return null;
    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var color = new Color(Random.value, Random.value, Random.value);
            logger.ZLogDebug(color.ToString());
            cubeManager.SetLamp(color);
        }
    }


    async void OnApplicationQuit()
    {
        await client.DisconnectAsync();
        client.Dispose();
        client = null;
    }

}
