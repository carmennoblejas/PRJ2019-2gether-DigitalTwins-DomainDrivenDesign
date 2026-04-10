using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChildHDT.Domain.Entities;
using ChildHDT.Domain.ValueObjects;
using ChildHDT.Infrastructure.InfrastructureServices.Context;
using ChildHDT.Infrastructure.IntegrationServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading;

namespace ChildHDT.Infrastructure.InfrastructureServices
{
    public class RepositoryChild : ControllerBase
    {
        private readonly IUnitOfwork _unitOfWork;
        private DbSet<Child> children;
        private Dictionary<Guid, IFeatures> _featuresCache;
        private readonly IConfiguration _configuration;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public string server;
        public int port;
        public string user;
        public string pwd;

        public RepositoryChild(IUnitOfwork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            children = _unitOfWork.Context.Set<Child>();
            _configuration = configuration;
            server = _configuration["MQTT:Server"];
            port = Convert.ToInt32(_configuration["MQTT:Port"]);
            user = _configuration["MQTT:UserName"];
            pwd = _configuration["MQTT:Password"];
            InitializeFeaturesCache();
        } 

        private void InitializeFeaturesCache()
        {
            _featuresCache = new Dictionary<Guid, IFeatures>();
            foreach (var child in children)
            {
                var features = new PWAFeatures(child.Id, _configuration);
                child.Features = features;
                _featuresCache[child.Id] = child.Features;
            }
        }

        public void UpdateFeaturesCache(Guid id, IFeatures features)
        {
            _featuresCache[id] = features;
        }

        public async Task<Child> FindById(Guid id)
        {
            await _semaphore.WaitAsync();
            try
            {
                var data = await children.FindAsync(id);
                if (data == null) return null;
                if (_featuresCache.TryGetValue(id, out var features))
                {
                    data.Features = features;
                }
                else
                {
                    data.Features = new PWAFeatures(data.Id, _configuration);
                    _featuresCache[data.Id] = data.Features;
                }
                return data;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<Child> Add(Child child)
        {
            await _semaphore.WaitAsync();
            try
            {
                children.Add(child);
                await _unitOfWork.SaveChangesAsync();
                child.Features = new PWAFeatures(child.Id, _configuration);
                _featuresCache[child.Id] = child.Features;
                return child;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<Child> Update(Child child)
        {
            await _semaphore.WaitAsync();
            try
            {
                children.Update(child);
                await _unitOfWork.SaveChangesAsync();
                if (child.Features != null)
                {
                    _featuresCache[child.Id] = child.Features;
                }
                return child;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<Child>> GetAll()
        {
            await _semaphore.WaitAsync();
            try
            {
                var childrenList = await children.ToListAsync();
                foreach (var child in childrenList)
                {
                    if (_featuresCache.TryGetValue(child.Id, out var features))
                    {
                        child.Features = features;
                    }
                    else
                    {
                        child.Features = new PWAFeatures(child.Id, _configuration);
                        _featuresCache[child.Id] = child.Features;
                    }
                }
                return childrenList;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> Delete(Guid id)
        {
            await _semaphore.WaitAsync();
            try
            {
                var data = await children.FindAsync(id);
                if (data == null) return false;
                children.Remove(data);
                await _unitOfWork.SaveChangesAsync();
                _featuresCache.Remove(id);
                return true;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
