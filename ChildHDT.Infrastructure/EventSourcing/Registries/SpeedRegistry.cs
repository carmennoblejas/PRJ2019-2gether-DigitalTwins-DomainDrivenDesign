using ChildHDT.Domain.Entities;
using ChildHDT.Domain.ValueObjects;
using ChildHDT.Infrastructure.EventSourcing.Events;
using Microsoft.Extensions.Configuration;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChildHDT.Infrastructure.EventSourcing.Registries
{
    public class SpeedRegistry : EventStore<SpeedEvent>
    {
        public SpeedRegistry(Guid id, IConfiguration configuration) : base(id, "speed", configuration) { }
        public SpeedRegistry(Guid id, IConfiguration configuration, IMqttClient client) : base(id, "speed", configuration, client) { }

        public override SpeedEvent GetLastEvent()
        {
            //Console.WriteLine($"LEYENDO instancia {GetHashCode()} con {Events.Count} eventos");

            if (Events.Count == 0) return new SpeedEvent(new SpeedMS(0), DateTime.Now);
            return Events[Events.Count - 1];
        }

        protected override SpeedEvent DeserializeEvent(string payload)
        {

            var data = JsonSerializer.Deserialize<SpeedMS>(payload);
            return new SpeedEvent(data, DateTime.Now);
        }
    }
}
