using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using ChildHDT.Domain.Entities;
using ChildHDT.Domain.ValueObjects;
using ChildHDT.Infrastructure.InfrastructureServices;
using ChildHDT.Infrastructure.InfrastructureServices.Context;
using MQTTnet;
using MQTTnet.Client;
using System.Text;
using ChildHDT.Infrastructure.IntegrationServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ChildHDT.API.ApplicationServices;
using ChildHDT.Domain.DomainServices;
using ChildHDT.Infrastructure.EventSourcing.Events;
using Microsoft.EntityFrameworkCore;

namespace ChildHDT.UIT.Testing
{
    [TestClass]
    public class SystemTest
    {
        private RepositoryChild _repositoryChild;
        private IUnitOfwork _unitOfWork;
        private ChildContext _context;
        private IConfiguration _configuration;
        private IHost _host;
        private IStressService _stressService;
        private INotificationHandler _notificationHandler;
        private MqttClientOptions _mqttOptions;

        [TestInitialize]
        public async Task SetUp()
        {
            var inMemorySettings = new Dictionary<string, string>
            {
                {"ASPNETCORE_ENVIRONMENT", "Development"},
                {"ConnectionStrings:PostgreSQL", "Host=localhost;Database=mydatabase;Username=myuser;Password=mypassword"},
                {"RabbitMQ:HostName", "localhost"},
                {"MQTT:Server", "localhost"},
                {"MQTT:Port", "1883"},
                {"MQTT:UserName", "admin"},
                {"MQTT:Password", "public"},
                {"API:URL", "http://localhost:8081/stresslevel"}
            };

            var configurationBuilder = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _configuration = configurationBuilder;

            var optionsBuilder = new DbContextOptionsBuilder<ChildContext>()
                .UseNpgsql(_configuration.GetConnectionString("PostgreSQL"));

            _mqttOptions = new MqttClientOptionsBuilder()
                .WithClientId("Test")
                .WithTcpServer(_configuration["MQTT:Server"], Convert.ToInt32(_configuration["MQTT:Port"]))
                .WithCredentials(_configuration["MQTT:UserName"], _configuration["MQTT:Password"])
                .WithCleanSession()
                .Build();

            _context = new ChildContext(optionsBuilder.Options);
            _unitOfWork = new UnitOfwork(_context);

            _repositoryChild = new RepositoryChild(_unitOfWork, _configuration);
            _notificationHandler = new NotificationHandler(_configuration);
            _stressService = new PWAStressService(_notificationHandler, _repositoryChild, _configuration);

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(_stressService);
                    services.AddSingleton(_notificationHandler);
                    services.AddSingleton(_context);
                    services.AddSingleton<IUnitOfwork>(_unitOfWork);
                    services.AddSingleton(_repositoryChild);
                    services.AddSingleton(_configuration);
                    services.AddHostedService<StressMonitoringService>();
                })
                .Build();


            await _host.StartAsync();
        }

        [TestMethod]
        [TestCategory("ExternalService")]
        public async Task SystemTesting()
        {
            // ARRANGE

            var victimLatitude = 40.7128000;
            var victimLongitude = -74.0060000;
            var bullyLatitude = 40.7128400;
            var bullyLongitude = -74.0060000;
            var victimOrientationAngle = 23;
            var victimOrientationScore = 2;
            var bullySpeed = 4;

            var factory = new MqttFactory();
            var mqttClient = factory.CreateMqttClient();

            var victim = new Child("Peter", "Parker", 10, "4ºB");
            victim.AssignRole(new Victim());
            await _repositoryChild.Add(victim);

            var bully = new Child("Harry", "Osborn", 10, "4ºB");
            bully.AssignRole(new Bully());
            await _repositoryChild.Add(bully);

            // ACT

            await mqttClient.ConnectAsync(_mqttOptions, CancellationToken.None);


            for (int i = 1; i <= 5; i++)
            {
                victimLatitude += 0.001 * i;
                victimLongitude += 0.001 * i;
                bullyLatitude += 0.002 * i;
                bullyLongitude += 0.002 * i;
                victimOrientationAngle = (victimOrientationAngle + 5) % 360;
                victimOrientationScore = (victimOrientationScore + 1) % 10;
                bullySpeed = (bullySpeed + 1) % 10;

                var pl_v_location = Encoding.UTF8.GetBytes($"{{\"latitude\": {victimLatitude}, \"longitude\": {victimLongitude}}}");
                var t_v_location = $"{victim.Id}/location";
                var m_v_location = new MqttApplicationMessageBuilder()
                    .WithTopic(t_v_location)
                    .WithPayload(pl_v_location)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                    .Build();

                var pl_b_location = Encoding.UTF8.GetBytes($"{{\"latitude\": {bullyLatitude}, \"longitude\": {bullyLongitude}}}");
                var t_b_location = $"{bully.Id}/location";
                var m_b_location = new MqttApplicationMessageBuilder()
                    .WithTopic(t_b_location)
                    .WithPayload(pl_b_location)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                    .Build();

                var pl_b_speed = Encoding.UTF8.GetBytes($"{{\"value\": {bullySpeed}}}");
                var t_b_speed = $"{bully.Id}/speed";
                var m_b_speed = new MqttApplicationMessageBuilder()
                    .WithTopic(t_b_speed)
                    .WithPayload(pl_b_speed)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                    .Build();

                var pl_v_orientation = Encoding.UTF8.GetBytes($"{{\"angle\": {victimOrientationAngle}, \"score\": {victimOrientationScore}}}");
                var t_v_orientation = $"{victim.Id}/orientation";
                var m_v_orientation = new MqttApplicationMessageBuilder()
                    .WithTopic(t_v_orientation)
                    .WithPayload(pl_v_orientation)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                .Build();

                await mqttClient.PublishAsync(m_v_location);
                await mqttClient.PublishAsync(m_b_location);
                await mqttClient.PublishAsync(m_b_speed);
                await mqttClient.PublishAsync(m_v_orientation);

                await Task.Delay(1000);
            }

            // ASSERT

            await Task.Delay(10000);
            var events = (victim.Features as PWAFeatures).StressRegistry.GetEvents();
            Assert.IsNotNull(events);
            Assert.IsTrue(events.Count > 0);
            var lastEvent = events[events.Count - 1];
            Assert.IsInstanceOfType(lastEvent, typeof(StressEvent));
            var stress = lastEvent.Stress;
            Assert.IsTrue(stress.value >= 0);
            Assert.IsTrue(stress.value <= 1);
            Assert.IsTrue(stress.level == "Controlled" || stress.level == "High");

        }
    }
}