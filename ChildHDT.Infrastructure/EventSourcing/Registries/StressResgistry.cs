using ChildHDT.Domain.DomainServices;
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
    public class StressRegistry : EventStore<StressEvent>
    {
        public StressRegistry(Guid id, IConfiguration configuration) : base(id, "stress", configuration) { }

        public override StressEvent GetLastEvent()
        {
            Console.WriteLine($"LEYENDO instancia {GetHashCode()} con {Events.Count} eventos");
            if (Events.Count == 0) return new StressEvent(new Stress(0, "Controlled"), DateTime.Now);
            return Events[Events.Count - 1];
        }

        protected override StressEvent DeserializeEvent(string payload)
        {
            var data = JsonSerializer.Deserialize<Stress>(payload);
            return new StressEvent(data, DateTime.Now);
        }
    }
}
