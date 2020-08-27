﻿using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;


public class Main : MonoBehaviour
{

    IMqttServer server;
    Dictionary<string, Bridge> bridges = new Dictionary<string, Bridge>();


    void Start()
    {
        var optionsBuilder = new MqttServerOptionsBuilder()
        .WithDefaultEndpoint()
        .WithConnectionValidator(ConnectionValidator)
        .WithSubscriptionInterceptor(SubscriptionInterceptor);
        // .WithApplicationMessageInterceptor(ApplicationMessageInterceptor);

        server = new MqttFactory().CreateMqttServer();
        server.UseClientDisconnectedHandler(ClientDisconnectedHandler);
        server.UseApplicationMessageReceivedHandler(ApplicationMessageReceivedHandler);
        server.StartAsync(optionsBuilder.Build());

        NewCubeHandler("aa");
    }


    void ConnectionValidator(MqttConnectionValidatorContext c)
    {
        c.ReasonCode = MqttConnectReasonCode.Success;
        LogMessage(c, false);
    }


    void SubscriptionInterceptor(MqttSubscriptionInterceptorContext c)
    {
        c.AcceptSubscription = true;
        LogMessage(c, true);
    }


    void ApplicationMessageInterceptor(MqttApplicationMessageInterceptorContext c)
    {
        c.AcceptPublish = true;
        LogMessage(c);
    }


    void ClientDisconnectedHandler(MqttServerClientDisconnectedEventArgs e)
    {
        Debug.LogWarning($"client disconnected: id={e.ClientId}, type={e.DisconnectType}");
        if (bridges.TryGetValue(e.ClientId, out var bridge))
        {
            bridge.Dispose();
            bridges.Remove(e.ClientId);
        }
    }


    void ApplicationMessageReceivedHandler(MqttApplicationMessageReceivedEventArgs e)
    {
        var m = e.ApplicationMessage;
        var payload = Encoding.ASCII.GetString(m.Payload);
        Debug.Log($"{e.ClientId},{m.Topic},{payload}");
        switch (m.Topic)
        {
            case "hello":
                HelloHandler(e.ClientId, payload);
                break;
            case "scanner":
                NewCubeHandler(payload);
                break;
            default:
                if (bridges.TryGetValue(e.ClientId, out var bridge))
                {
                    bridge.ProcessPayload(m.Topic, payload);
                }
                break;
        }
    }


    void HelloHandler(string clientId, string mode)
    {
        switch (mode)
        {
            case "scanner":
                // do nothing...
                break;
            case "bridge":
                if (!bridges.ContainsKey(clientId))
                {
                    bridges.Add(clientId, new Bridge(clientId, server));
                }
                break;
            default:
                Debug.LogWarning("Unknow mode: " + mode);
                break;
        }
    }


    void NewCubeHandler(string cubeId)
    {
        var kv = bridges
        .Where(i => !i.Value.IsBusy && i.Value.NumConnectedCubes < Client.MAX_CUBES_PER_CLIENT)
        .OrderBy(i => i.Value.NumConnectedCubes)
        .FirstOrDefault();
        if (kv.Value != null)
        {
            kv.Value.Connect(new Cube(cubeId));
        }
        else
        {
            Debug.Log("no bridges available now...");
        }
    }


    void OnApplicationQuit()
    {
        server?.StopAsync();
    }


    private static void LogMessage(MqttConnectionValidatorContext context, bool showPassword)
    {
        if (context == null)
        {
            return;
        }

        if (showPassword)
        {
            Debug.Log(
                $"New connection: ClientId = {context.ClientId}, Endpoint = {context.Endpoint},"
                + $" Username = {context.Username}, Password = {context.Password},"
                + $" CleanSession = {context.CleanSession}");
        }
        else
        {
            Debug.Log(
                $"New connection: ClientId = {context.ClientId}, Endpoint = {context.Endpoint},"
                + $" Username = {context.Username}, CleanSession = {context.CleanSession}");
        }
    }


    private static void LogMessage(MqttSubscriptionInterceptorContext context, bool successful)
    {
        if (context == null)
        {
            return;
        }

        Debug.Log(successful ? $"New subscription: ClientId = {context.ClientId}, TopicFilter = {context.TopicFilter}" : $"Subscription failed for clientId = {context.ClientId}, TopicFilter = {context.TopicFilter}");
    }


    private static void LogMessage(MqttApplicationMessageInterceptorContext context)
    {
        if (context == null)
        {
            return;
        }

        var payload = context.ApplicationMessage?.Payload == null ? null : Encoding.UTF8.GetString(context.ApplicationMessage?.Payload);

        Debug.Log(
            $"Message: ClientId = {context.ClientId}, Topic = {context.ApplicationMessage?.Topic},"
            + $" Payload = {payload}, QoS = {context.ApplicationMessage?.QualityOfServiceLevel},"
            + $" Retain-Flag = {context.ApplicationMessage?.Retain}");
    }

}
