using ChildHDT.Domain.Entities;
using Microsoft.Extensions.Configuration;
using ChildHDT.Infrastructure.EventSourcing.Events;
using MQTTnet.Client;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;

namespace ChildHDT.Infrastructure.EventSourcing.Registries
{
    public abstract class EventStore<T> where T : Event
    {
        protected List<T> Events { get; set; } = new List<T>();
        private readonly IMqttClient _client;
        private readonly string _topic;

        protected EventStore (Guid id, string topic, IConfiguration _configuration)
        {
            _topic = "" + id + "/" + topic;
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            var mqttServer = _configuration["MQTT:Server"];
            var mqttPort = Convert.ToInt32(_configuration["MQTT:Port"]);
            var mqttUserName = _configuration["MQTT:UserName"];
            var mqttPassword = _configuration["MQTT:Password"];

            var options = new MqttClientOptionsBuilder()
                .WithClientId($"ChildClient-{id}-{topic}")
                .WithTcpServer(mqttServer, mqttPort)
                .WithCredentials(mqttUserName, mqttPassword)
                .WithCleanSession()
                .Build();
           
            
            Start(options).Wait();
        }

        protected EventStore(Guid id, string topic, IConfiguration _configuration, IMqttClient client)
        {
            _topic = "" + id + "/" + topic;
            _client = client;

            var mqttServer = _configuration["MQTT:Server"];
            var mqttPort = Convert.ToInt32(_configuration["MQTT:Port"]);
            var mqttUserName = _configuration["MQTT:UserName"];
            var mqttPassword = _configuration["MQTT:Password"];

            var options = new MqttClientOptionsBuilder()
                .WithClientId($"ChildClient-{id}-{topic}")
                .WithTcpServer(mqttServer, mqttPort)
                .WithCredentials(mqttUserName, mqttPassword)
                .WithCleanSession()
                .Build();
           
            Start(options).Wait();
        }

        public async Task Start(MqttClientOptions options)
        {

            _client.ApplicationMessageReceivedAsync += e =>
            {
                var message = e.ApplicationMessage;
                var payload = Encoding.UTF8.GetString(message.PayloadSegment);
                var eventData = DeserializeEvent(payload);
                ReceiveEvent(eventData);
                return Task.CompletedTask;
            };
            
            await _client.ConnectAsync(options);
            await _client.SubscribeAsync(_topic);

        }

        public void ReceiveEvent(T eventData)
        {
            if (Events.Count >= 50)
            {
                Events.Clear();
            }

            Events.Add(eventData);
            Console.WriteLine($"EVENTO RECIBIDO en instancia {GetHashCode()}");
        }

        public List<T> GetEvents()
        {
            
            return Events;
        }

        public abstract T GetLastEvent();

        public List<T> GetEventsBetweenDates(DateTime from, DateTime to)
        {
            if (from > to)
            {
                throw new ArgumentException("Start date cannot be greater than end date");
            }

            return Events.Where(e => e.Timestamp >= from && e.Timestamp <= to).ToList();
        }

        public void PublishEvent(T eventData)
        {
            var payload = JsonSerializer.Serialize(eventData);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_topic)
                .WithPayload(payload)
                .Build();

            _client.PublishAsync(message).Wait();
        }

        protected abstract T DeserializeEvent(string payload);
    }
}
