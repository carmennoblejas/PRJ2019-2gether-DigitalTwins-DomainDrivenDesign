using ChildHDT.Domain.DomainServices;
using ChildHDT.Domain.ValueObjects;
using ChildHDT.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using System.Net.Http;
using ChildHDT.Infrastructure.InfrastructureServices;
using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;

namespace ChildHDT.Infrastructure.IntegrationServices
{
    public class PWAStressService : IStressService
    {

        private readonly INotificationHandler notificationHandler;
        private readonly HttpClient httpClient;
        private readonly RepositoryChild rc;
        private readonly string apiUrl;
        private readonly Thread stressThread;
        private bool isRunning;
        private readonly IConfiguration _configuration;

        // METHODS

        public PWAStressService(INotificationHandler nh, RepositoryChild rc, IConfiguration configuration) 
        {
            notificationHandler = nh;
            httpClient = new HttpClient();
            this.rc = rc;
            _configuration = configuration;
            apiUrl = _configuration["API:URL"];
        }

        public async Task<Stress> CalculateStress(Child child)
        {
            // METHOD ONLY CALLED BY VICTIMS
            if (!child.IsVictim())
            {
                return new Stress(0,"Controlled");
            }
            var children = rc.GetAll();
            var bully = await GetClosestBully(child);
            if (bully == null)
            {
                return new Stress(0, "Controlled");
            }
            var locationChild = (child.Features as PWAFeatures).LocationRegistry.GetLastEvent().Location;
            var locationBully = (bully.Features as PWAFeatures).LocationRegistry.GetLastEvent().Location;
            var orientation = (child.Features as PWAFeatures).OrientationRegistry.GetLastEvent().Orientation;
            var speed = (bully.Features as PWAFeatures).SpeedRegistry.GetLastEvent().Speed;

            var requestData = new
            {
                angulo_de_vision = FieldOfViewService.CalculateFieldOfViewService(locationChild, locationBully, orientation.angle).angle,
                proximidad = ProximityService.CalculateProximity(locationChild, locationBully),
                velocidad = speed.value
            };

            var jsonContent = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<Stress>(responseString);
                var value = Convert.ToDouble(result.value);
                var level = EvaluateStress(child, value);
                return new Stress(value, level);
            }
            else
            {
                throw new Exception("ERROR: BAD CALL TO API");
            }
        }

        public string EvaluateStress(Child child, double value) 
        {
            string level;
            if (value > 0.6)
            {
                level = "High";
                SendStressNotification().Wait();
                //child.StressLevelShotUp(notificationHandler);
                //ALMACEN
            } else
            {
                level = "Controlled";
            }
            return level;
        }

        private async Task<Child> GetClosestBully(Child child)
        {
            var children = await rc.GetAll();

            Child closestBully = null;
            double minDistance = double.MaxValue;
            Location childLocation, iLocation;

            foreach (var i_child in children)
            {
                if (i_child.IsBully()) 
                {
                    childLocation = (child.Features as PWAFeatures).LocationRegistry.GetLastEvent().Location;
                    iLocation = (i_child.Features as PWAFeatures).LocationRegistry.GetLastEvent().Location;
                    double distance = ProximityService.CalculateProximity(childLocation, iLocation);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestBully = i_child;
                    }
                }
            }

            return closestBully;
        }
        private async Task SendStressNotification()
        {
            var children = await rc.GetAll();
            foreach (var child in children)
            {
                child.StressLevelShotUp(notificationHandler);
            }
        }

    }
}
