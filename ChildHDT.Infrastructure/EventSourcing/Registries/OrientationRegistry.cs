using ChildHDT.Domain.Entities;
using ChildHDT.Domain.ValueObjects;
using ChildHDT.Infrastructure.EventSourcing.Events;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChildHDT.Infrastructure.EventSourcing.Registries
{
    public class OrientationRegistry : EventStore<OrientationEvent>
    {
        public OrientationRegistry(Guid id, IConfiguration configuration) : base(id, "orientation", configuration) { }

        public override OrientationEvent GetLastEvent()
        {
            Console.WriteLine($"LEYENDO instancia {GetHashCode()} con {Events.Count} eventos");
            if (Events.Count == 0) return new OrientationEvent(new Orientation(0), DateTime.Now);
            return Events[Events.Count - 1];
        }

        protected override OrientationEvent DeserializeEvent(string payload)
        {
            var data = JsonSerializer.Deserialize<Orientation>(payload);
            return new OrientationEvent(data, DateTime.Now);
        }
    }
}
